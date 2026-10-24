using HUBTechGenerator;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
static class PaletteChecks
{
    static int assertions;
    static void Check(bool value,string message){if(!value)throw new Exception(message);assertions++;}
    public static (double L,double A,double B) Lab(string color)
    {
        var c=SkiaSharp.SKColor.Parse(color);
        double Linear(double x)=>x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);
        double F(double t)=>t>216.0/24389?Math.Cbrt(t):t*841.0/108+4.0/29;
        double r=Linear(c.Red/255.0),g=Linear(c.Green/255.0),b=Linear(c.Blue/255.0);
        double x=F((.4124564*r+.3575761*g+.1804375*b)/.95047),y=F(.2126729*r+.7151522*g+.072175*b),z=F((.0193339*r+.119192*g+.9503041*b)/1.08883);
        return (116*y-16,500*(x-y),200*(y-z));
    }
    public static double Distance((double L,double A,double B) a,(double L,double A,double B) b)=>Math.Sqrt(Math.Pow(a.L-b.L,2)+Math.Pow(a.A-b.A,2)+Math.Pow(a.B-b.B,2));
    static double Minimum(IEnumerable<string> colors,Func<string,string,double> difference)
    {
        var values=colors.ToArray();double min=100;
        for(int i=0;i<values.Length;i++)for(int j=0;j<i;j++)min=Math.Min(min,difference(values[i],values[j]));return min;
    }
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        string data=Path.Combine(AppContext.BaseDirectory,"Data","ciede2000-reference.tsv");
        foreach(string row in File.ReadAllLines(data))
        {
            if(string.IsNullOrWhiteSpace(row))continue;
            var n=row.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(double.Parse).ToArray();
            double actual=PerceptualColor.Difference((n[0],n[1],n[2]),(n[3],n[4],n[5]));
            Check(Math.Abs(actual-n[6])<.00005,"CIEDE2000 reference mismatch: "+actual+" vs "+n[6]);
            Check(Math.Abs(actual-PerceptualColor.Difference((n[3],n[4],n[5]),(n[0],n[1],n[2])))<1e-10,"Perceptual metric must be symmetric.");
        }
        Check(PerceptualColor.Difference("#ffffff","#ffffff")==0,"Identical colors have zero perceptual difference.");
        var output=new List<object>();
        foreach(bool vlanOne in new[]{false,true})foreach(int count in new[]{2,8,12,20,28,36})
        {
            var inv=new Inventory{Vlans=Enumerable.Range(vlanOne?1:100,count).Select(id=>new Vlan{Id=id}).ToList()};
            var clock=Stopwatch.StartNew();var colors=VlanPalette.Assign(inv);clock.Stop();double elapsed=clock.Elapsed.TotalMilliseconds;
            Check(colors.Count==count&&colors.Values.Distinct().Count()==count,"Palette has one distinct color for every VLAN.");
            Check(colors.SequenceEqual(VlanPalette.Assign(inv)),"Identical input gives stable colors.");
            foreach(string color in colors.Values)
            {
                var rgb=SkiaSharp.SKColor.Parse(color);var lab=Lab(color);
                Check(lab.L>=60&&lab.L<=91&&Math.Min(rgb.Red,Math.Min(rgb.Green,rgb.Blue))>=95,"All colors stay within the soft palette range.");
                foreach(string reference in new[]{"#ffffff","#e6e6e6","#000000"})Check(PerceptualColor.Difference(color,reference)>7,"VLAN color remains separated from reserved visual states.");
            }
            double minimum=Minimum(colors.Values,PerceptualColor.Difference),legacy=Minimum(colors.Values,AggregatePalette.Difference),old=0;bool previousBlocked=false;
            try{old=Minimum(PreviousVlanPalette.Assign(inv).Values,PerceptualColor.Difference);}catch(OnePageCapacityException){previousBlocked=true;}
            if(count<=28)Check(legacy+1e-6>=VlanPalette.MinimumDistance,"Preferred Lab separation guard is preserved through 28 VLANs.");
            Console.WriteLine($"{count} VLANs, includes VLAN1={vlanOne}: new min DE00 {minimum:F3}; previous {(previousBlocked?"blocked":old.ToString("F3"))}; Lab min {legacy:F3}; first search {elapsed:F1} ms");
            if(!previousBlocked)Check(minimum+1e-3>=old,"New palette's weakest perceptual separation must improve on prior palette.");
            output.Add(new{Count=count,VlanOne=vlanOne,MinimumDE00=minimum,PreviousDE00=old,PreviousBlocked=previousBlocked,MinimumLab=legacy,Milliseconds=elapsed,Colors=colors});
        }
        var contextual=new Inventory{Vlans=Enumerable.Range(100,20).Select(id=>new Vlan{Id=id}).ToList()};
        for(int n=0;n<48;n++)contextual.Ports.Add(new(){Id=(n+1).ToString(),Group="Switch",UntaggedVlan=100+n%20,TaggedVlans=new(){100+(n+3)%20,100+(n+11)%20}});
        var mapped=VlanPalette.Assign(contextual);Check(mapped.SequenceEqual(VlanPalette.Assign(contextual)),"Context mapping is deterministic.");
        Check(mapped.Values.Order().SequenceEqual(VlanPalette.Assign(new Inventory{Vlans=contextual.Vlans}).Values.Order()),"Context optimization changes assignment, not the separated palette.");
        var large=VlanPalette.Assign(new Inventory{Vlans=Enumerable.Range(100,64).Select(id=>new Vlan{Id=id}).ToList()});Check(large.Count==64&&large.Values.Distinct().Count()==64,"Crowded palette must not stop a report solely on the old color-distance threshold.");
        if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS "+assertions+" CIEDE2000 reference, crowded palette, soft-color, stability and contextual-mapping assertions.");
    }
}
