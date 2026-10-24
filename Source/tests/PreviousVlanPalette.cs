namespace HUBTechGenerator;

static class PreviousVlanPalette
{
    public const double MinimumDistance=18;
    record Candidate(string Hex,(double L,double A,double B) Lab);
    static readonly List<Candidate> Candidates=MakeCandidates();
    public static string NativeFill(Port p,IReadOnlyDictionary<int,string> palette)=>p.StackLink?"#000000":p.UntaggedState=="None"?"#ffffff":p.UntaggedVlan.HasValue?palette[p.UntaggedVlan.Value]:"#e6e6e6";
    static bool SoftComponents(string color)
    {
        var rgb=SkiaSharp.SKColor.Parse(color);return Math.Min(rgb.Red,Math.Min(rgb.Green,rgb.Blue))>=95;
    }
    static List<Candidate> MakeCandidates()
    {
        var values=new HashSet<string>();
        foreach(double l in new[]{.54,.62,.70,.78,.84})foreach(double s in new[]{.35,.50,.65,.80})for(int hue=0;hue<360;hue+=3)
        {
            double h=hue/60.0,c=(1-Math.Abs(2*l-1))*s,x=c*(1-Math.Abs(h%2-1)),m=l-c/2;
            (double r,double g,double b)=h<1?(c,x,0.0):h<2?(x,c,0.0):h<3?(0.0,c,x):h<4?(0.0,x,c):h<5?(x,0.0,c):(c,0.0,x);
            values.Add($"#{(int)Math.Round((r+m)*255):x2}{(int)Math.Round((g+m)*255):x2}{(int)Math.Round((b+m)*255):x2}");
        }
        return values.Select(h=>new Candidate(h,PaletteChecks.Lab(h))).Where(c=>c.Lab.L>=60&&c.Lab.L<=92&&Math.Sqrt(c.Lab.A*c.Lab.A+c.Lab.B*c.Lab.B)>=14&&Math.Sqrt(c.Lab.A*c.Lab.A+c.Lab.B*c.Lab.B)<=65&&SoftComponents(c.Hex)).OrderBy(c=>c.Hex,StringComparer.Ordinal).ToList();
    }
    public static Dictionary<int,string> Assign(Inventory inv)
    {
        var ids=inv.Vlans.Select(v=>v.Id).Concat(inv.Ports.SelectMany(p=>p.TaggedVlans.Concat(p.UntaggedVlan.HasValue?new[]{p.UntaggedVlan.Value}:Array.Empty<int>()).Concat(p.VoiceVlan.HasValue?new[]{p.VoiceVlan.Value}:Array.Empty<int>()))).Distinct().Order().ToList();
        var result=new Dictionary<int,string>();
        var selected=new List<(double L,double A,double B)>{PaletteChecks.Lab("#ffffff"),PaletteChecks.Lab("#e6e6e6"),PaletteChecks.Lab("#000000")};
        if(ids.Contains(1)){result.Add(1,"#9faab5");selected.Add(PaletteChecks.Lab(result[1]));}
        var available=new List<Candidate>();
        for(int i=0;i<ids.Count-result.Count;i++)
        {
            var chosen=Candidates.Where(c=>!available.Any(a=>a.Hex==c.Hex)).Select(c=>(Candidate:c,Distance:selected.Min(s=>PaletteChecks.Distance(c.Lab,s)))).OrderByDescending(c=>c.Distance).ThenBy(c=>c.Candidate.Hex,StringComparer.Ordinal).First();
            if(chosen.Distance<MinimumDistance)throw new OnePageCapacityException("too many VLAN colors to keep the pastel key sufficiently distinct");
            available.Add(chosen.Candidate);selected.Add(chosen.Candidate.Lab);
        }
        // Preserve familiar approximate hues where possible, after selecting
        // a complete, separated palette. Mapping stays local to this diagram.
        int[] familiar={10,110,120,140,150,160,254,300,500,600};
        foreach(int id in ids.Where(id=>id!=1).OrderBy(id=>Array.IndexOf(familiar,id)<0?100:Array.IndexOf(familiar,id)).ThenBy(id=>id))
        {
            var original=SkiaSharp.SKColor.Parse(Charts.VlanColor(id));
            string softened=$"#{(int)Math.Round(original.Red*.65+255*.35):x2}{(int)Math.Round(original.Green*.65+255*.35):x2}{(int)Math.Round(original.Blue*.65+255*.35):x2}";
            var target=PaletteChecks.Lab(softened);
            var chosen=available.OrderBy(c=>PaletteChecks.Distance(c.Lab,target)).ThenBy(c=>c.Hex,StringComparer.Ordinal).First();
            result.Add(id,chosen.Hex);available.Remove(chosen);
        }
        return result;
    }
}
