namespace SwitchportGenerator;

// Choose outlines against the actual port fills, rather than reusing VLAN-like
// colors from a fixed list. Lab distance is an engineering separation guard,
// not a claim of accessibility certification or printer color accuracy.
public static class AggregatePalette
{
    public const double MinimumMemberDistance=35;
    record Candidate(string Hex,(double L,double A,double B) Lab);
    static readonly List<Candidate> Candidates=MakeCandidates();
    static readonly Lazy<List<Candidate>> DenseCandidates=new(()=>MakeCandidates(true));
    static double Linear(double x)=>x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);
    internal static (double L,double A,double B) Lab(string color)
    {
        var c=SkiaSharp.SKColor.Parse(color);double r=Linear(c.Red/255.0),g=Linear(c.Green/255.0),b=Linear(c.Blue/255.0);
        double F(double t)=>t>216.0/24389?Math.Cbrt(t):t*841.0/108+4.0/29;
        double x=F((.4124564*r+.3575761*g+.1804375*b)/.95047),y=F(.2126729*r+.7151522*g+.072175*b),z=F((.0193339*r+.119192*g+.9503041*b)/1.08883);
        return (116*y-16,500*(x-y),200*(y-z));
    }
    internal static double Distance((double L,double A,double B) a,(double L,double A,double B) b)=>Math.Sqrt(Math.Pow(a.L-b.L,2)+Math.Pow(a.A-b.A,2)+Math.Pow(a.B-b.B,2));
    public static double Difference(string a,string b)=>Distance(Lab(a),Lab(b));
    static double HueDifference((double L,double A,double B) a,(double L,double A,double B) b)
    {
        double d=Math.Abs(Math.Atan2(a.B,a.A)-Math.Atan2(b.B,b.A))*180/Math.PI;return Math.Min(d,360-d);
    }
    internal static double Luminance(string color)
    {
        var c=SkiaSharp.SKColor.Parse(color);return .2126*Linear(c.Red/255.0)+.7152*Linear(c.Green/255.0)+.0722*Linear(c.Blue/255.0);
    }
    static List<Candidate> MakeCandidates(bool dense=false)
    {
        var values=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(double lightness in dense?new[]{.24,.28,.32,.36,.42,.48,.54,.60,.66}:new[]{.12,.18,.24,.32,.42,.54,.66})
        foreach(double saturation in dense?new[]{.45,.55,.65,.75,.85,.95}:new[]{.55,.75,.95})
        for(int hue=0;hue<360;hue+=dense?5:10)
        {
            double h=hue/60.0,c=(1-Math.Abs(2*lightness-1))*saturation,x=c*(1-Math.Abs(h%2-1)),m=lightness-c/2;
            (double r,double g,double b)=h<1?(c,x,0.0):h<2?(x,c,0.0):h<3?(0.0,c,x):h<4?(0.0,x,c):h<5?(x,0.0,c):(c,0.0,x);
            values.Add($"#{(int)Math.Round((r+m)*255):x2}{(int)Math.Round((g+m)*255):x2}{(int)Math.Round((b+m)*255):x2}");
        }
        return values.Select(h=>new Candidate(h,Lab(h))).Where(c=>c.Lab.L>=40&&c.Lab.L<=82&&Math.Sqrt(c.Lab.A*c.Lab.A+c.Lab.B*c.Lab.B)>=32&&Math.Sqrt(c.Lab.A*c.Lab.A+c.Lab.B*c.Lab.B)<=80&&1.05/(Luminance(c.Hex)+.05)>=3).OrderBy(c=>c.Hex,StringComparer.Ordinal).ToList();
    }
    public static Dictionary<string,string> Assign(Inventory inv,Func<Port,string>? fill=null,IEnumerable<string>? referenceColors=null)
    {
        if(fill==null&&inv.VlanColorOverrides!=null)
        {
            var palette=VlanPalette.Assign(inv);fill=port=>VlanPalette.NativeFill(port,palette);
        }
        fill??=Charts.Color;
        var references=(referenceColors??Array.Empty<string>()).ToList();
        try{return Assign(inv,fill,references,Candidates);}
        catch(OnePageCapacityException){return Assign(inv,fill,references,DenseCandidates.Value);}
    }
    static Dictionary<string,string> Assign(Inventory inv,Func<Port,string> fill,IEnumerable<string> referenceColors,List<Candidate> candidates)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var native=inv.Ports.Select(fill).Concat(referenceColors??Array.Empty<string>()).Distinct().Select(Lab).ToList();
        var selected=new List<(double L,double A,double B)>();
        var groups=inv.Ports.Where(p=>p.Aggregate.Length>0).GroupBy(p=>p.Aggregate,StringComparer.OrdinalIgnoreCase).OrderBy(g=>g.Key.Length).ThenBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).ToList();
        var pinned=inv.AggregateColorOverrides??new Dictionary<string,string>();
        foreach(var pair in pinned)
        {
            RenderPalette.ValidateColor(pair.Value);
            var group=groups.FirstOrDefault(candidate=>candidate.Key.Equals(pair.Key,StringComparison.OrdinalIgnoreCase))
                ??throw new InvalidOperationException("LAG palette contains a label absent from this configuration.");
            string color=pair.Value.ToLowerInvariant();var lab=Lab(color);
            if(group.Any(port=>Distance(lab,Lab(fill(port)))<MinimumMemberDistance)||native.Any(reference=>Distance(lab,reference)<18)||1.05/(Luminance(color)+.05)<3)
                throw new InvalidOperationException("Saved LAG color for "+pair.Key+" does not have sufficient contrast against its member fills and workspace palette.");
            if(result.Any(existing=>WorkspacePalette.AggregateKey(existing.Key)!=WorkspacePalette.AggregateKey(pair.Key)&&Distance(Lab(existing.Value),lab)<22))
                throw new InvalidOperationException("Distinct saved LAG colors are not sufficiently separated.");
            result.Add(group.Key,color);selected.Add(lab);
        }
        foreach(var group in groups.Where(group=>!result.ContainsKey(group.Key)))
        {
            var member=group.Select(fill).Distinct().Select(Lab).ToList();
            var chromatic=member.Where(c=>Math.Sqrt(c.A*c.A+c.B*c.B)>=18).ToList();
            var scores=candidates.Where(c=>!result.Values.Contains(c.Hex)).Select(c=>new
            {
                Candidate=c,Own=member.Min(n=>Distance(c.Lab,n)),Global=native.Min(n=>Distance(c.Lab,n)),Previous=selected.Count==0?80:selected.Min(n=>Distance(c.Lab,n)),HueGap=chromatic.Count==0?90:chromatic.Min(n=>HueDifference(c.Lab,n))
            }).Where(c=>c.Own>=MinimumMemberDistance).ToList();
            var pool=scores.Where(c=>c.Global>=25&&c.Previous>=24).ToList();
            if(pool.Count==0)pool=scores.Where(c=>c.Global>=20&&c.Previous>=22).ToList();
            if(pool.Count==0)pool=scores.Where(c=>c.Global>=18&&c.Previous>=22).ToList();
            if(pool.Count==0)throw new OnePageCapacityException("cannot assign a sufficiently different outline color to aggregate "+group.Key);
            // Maximize the weakest separation first. Hue preference cannot
            // outweigh a less similar available outline or native-fill color.
            var chosen=pool.OrderByDescending(c=>Math.Min(c.Own/MinimumMemberDistance,Math.Min(c.Global/18,c.Previous/22)))
                .ThenByDescending(c=>c.Previous).ThenByDescending(c=>Math.Min(c.HueGap,90))
                .ThenBy(c=>c.Candidate.Hex,StringComparer.Ordinal).First().Candidate;
            result.Add(group.Key,chosen.Hex);selected.Add(chosen.Lab);
        }
        return result;
    }
}
