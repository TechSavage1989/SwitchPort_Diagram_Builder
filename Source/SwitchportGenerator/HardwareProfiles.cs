using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public static class HardwareProfiles
{
    // Front-view slot positions. Service bands are structural guides, not installed inventory.
    static readonly Dictionary<string,(string Name,int Slots)> Chassis=new(StringComparer.OrdinalIgnoreCase)
    {
        ["J9821A"]=("5406R zl2",6),["J9850A"]=("5406R zl2",6),["J9823A"]=("5406R zl2",6),["J9824A"]=("5406R zl2",6),["J9868A"]=("5406R zl2",6),["JL002A"]=("5406R zl2",6),["JL003A"]=("5406R zl2",6),["JL095A"]=("5406R zl2",6),
        ["J9822A"]=("5412R zl2",12),["J9851A"]=("5412R zl2",12),["J9825A"]=("5412R zl2",12),["J9826A"]=("5412R zl2",12),["JL001A"]=("5412R zl2",12),
        ["J8697A"]=("5406 zl",6),["J8699A"]=("5406 zl",6),["J9447A"]=("5406 zl",6),
        ["J8698A"]=("5412 zl",12),["J8700A"]=("5412 zl",12),["J9448A"]=("5412 zl",12),
        ["J8715A"]=("8212 zl",12),["J8715B"]=("8212 zl",12),["J9091A"]=("8212 zl",12),["J9091B"]=("8212 zl",12),["J9639A"]=("8212 zl",12),
        ["J4850A"]=("5304 xl",4),["J4819A"]=("5308 xl",8),["J9477A"]=("8206 zl",6),
        ["J9475A"]=("8206 zl",6),["J9638A"]=("8206 zl",6),["J9640A"]=("8206 zl",6)
    };
    static readonly string[] FlexBases={"JL071A","JL072A","JL073A","JL074A","JL075A","JL076A"};
    public static IReadOnlyDictionary<string,(string Name,int Slots)> ChassisModels=>Chassis;
    public static bool Is2920(string product)=>new[]{"J9726A","J9727A","J9728A","J9729A","J9836A"}.Contains(product,StringComparer.OrdinalIgnoreCase);
    public static void Add2920RearModules(Inventory inv,string text,bool stack)
    {
        var bases=inv.Groups.Where(g=>Is2920(g.Product)).ToList();
        if(bases.Count==0)return;
        if(bases.Count>4)inv.Blockers.Add("2920 hardware stacks support at most four members.");
        if(inv.Groups.Any(g=>g.Product is "J9731A" or "J9732A"&&!g.Rear))inv.Blockers.Add("2920 uplink modules require rear slot A or B on a declared member.");
        var specs=Regex.Matches(text,@"(?im)^\s*(?:interface|trunk|(?:no\s+)?(?:tagged|untagged))\s+(\S+)").Select(m=>m.Groups[1].Value);
        var references=specs.SelectMany(s=>Regex.Matches(s,@"(?<![\w/])(?:(\d+)/)?([AB])[12](?!\d)",RegexOptions.IgnoreCase).Cast<Match>())
            .Select(m=>(Member:m.Groups[1].Success?m.Groups[1].Value:"1",Slot:m.Groups[2].Value.ToUpperInvariant())).Distinct();
        foreach(var reference in references)
        {
            if(!bases.Any(g=>g.Device==reference.Member)||inv.Groups.Any(g=>g.Device==reference.Member&&g.Slot==reference.Slot))continue;
            string prefix=(stack?reference.Member+"/":"")+reference.Slot;
            var group=new Group{Id="Rear "+reference.Member+"/"+reference.Slot,Device=reference.Member,Slot=reference.Slot,Prefix=prefix,Rear=true,Optical=2,OpticalOnly=true,OpticalType="Unspecified"};
            for(int n=1;n<=2;n++)
            {
                group.SocketLayout.Add(new(){Number=n,Column=n-1,Row=0,Media="Unspecified"});
                inv.Ports.Add(new(){Id=prefix+n,Group=group.Id,Number=n,Media="Unspecified"});
            }
            inv.Groups.Add(group);
            inv.Assumptions.Add("2920 member "+reference.Member+" rear slot "+reference.Slot+" is referenced by configuration. Its compatible cards have two ports; no J9731A/J9732A SKU is supplied, so media remains Unspecified and uses neutral rectangles. Other unreported rear bays and stacking sockets are not inferred. Hardware reference: https://support.hpe.com/hpesc/public/api/document/c03898362");
        }
        inv.Groups=inv.Groups.OrderBy(g=>int.TryParse(g.Device,out int n)?n:int.MaxValue).ThenBy(g=>g.Slot,StringComparer.OrdinalIgnoreCase).ToList();
        foreach(var group in inv.Groups.Where(g=>g.Rear))
            if(!bases.Any(g=>g.Device==group.Device)||group.Slot is not ("A" or "B")||group.Product.Length>0&&group.Product is not ("J9731A" or "J9732A"))
                inv.Blockers.Add("Rear module is incompatible with the configured 2920 member: "+group.Id);
    }
    public static void Identify(Inventory inv,string text,Dictionary<string,string> declared)
    {
        string identity=string.Join('\n',text.Split('\n').Where(l=>Regex.IsMatch(l,@"(?i)^\s*(?:[;!#]|(?:product|model|chassis|system\s+model)\b)")));
        var profiles=Chassis.Where(p=>Regex.IsMatch(identity,@"\b"+p.Key+@"\b",RegexOptions.IgnoreCase)||declared.Values.Contains(p.Key,StringComparer.OrdinalIgnoreCase)).Select(p=>p.Value).Distinct().ToList();
        foreach(var name in new[]{"5406R","5412R","5406","5412","8212","8206"})
            if(Regex.IsMatch(identity,@"\b"+name+@"(?:zl2?|\s|$)",RegexOptions.IgnoreCase))profiles.Add((name,name.EndsWith("12")||name=="5412R"?12:6));
        if(profiles.Select(p=>p.Slots).Distinct().Count()>1)inv.Blockers.Add("Contradictory chassis capacity identifiers.");
        if(profiles.Count>0){inv.ChassisModel=profiles[0].Name;inv.ChassisSlots=profiles[0].Slots;}
        foreach(string key in declared.Where(d=>Chassis.ContainsKey(d.Value)).Select(d=>d.Key).ToList())declared.Remove(key);
        inv.FlexibleUplinks=declared.Values.Any(v=>FlexBases.Contains(v,StringComparer.OrdinalIgnoreCase))||FlexBases.Any(v=>Regex.IsMatch(identity,@"\b"+v+@"\b",RegexOptions.IgnoreCase));
        if(inv.FlexibleUplinks)
        {
            inv.ChassisModel="3810M";
            if(!declared.Values.Any(v=>FlexBases.Contains(v,StringComparer.OrdinalIgnoreCase)))
            {
                var bases=FlexBases.Where(v=>Regex.IsMatch(identity,@"\b"+v+@"\b",RegexOptions.IgnoreCase)).ToList();
                if(bases.Count==1)declared["1"]=bases[0];
            }
        }
    }
    public static void Validate(Inventory inv)
    {
        if(inv.ChassisSlots>0&&inv.Groups.Any(g=>g.Slot.Length!=1||g.Slot[0]<'A'||g.Slot[0]>='A'+inv.ChassisSlots))inv.Blockers.Add("Module slot is outside the identified chassis capacity.");
        if(inv.FlexibleUplinks)
            foreach(var device in inv.Groups.GroupBy(g=>g.Device))
            {
                var bases=device.Where(g=>g.Slot.Length==0).ToList();
                if(bases.Count!=1){inv.Blockers.Add("3810M requires one explicit base switch per member.");continue;}
                int slots=bases[0].Product=="JL075A"?2:1;
                if(device.Any(g=>g.Slot.Length>0&&(g.Slot.Length!=1||g.Slot[0]<'A'||g.Slot[0]>='A'+slots)))inv.Blockers.Add("Uplink module slot is outside the 3810M base model capacity.");
            }
        if(inv.Topology=="Modular"&&inv.ChassisSlots==0)inv.Assumptions.Add("Chassis identity is not supplied. Module slots retain paired front-view positions through the highest declared slot; total chassis capacity is unconfirmed.");
    }
}

public record ModuleFrame(string Group,string Slot,double X,double Y,double W,double H);
public record HardwareBand(string Text,double X,double Y,double W,double H);
public sealed class HardwareView
{
    public List<ModuleFrame> Frames {get;}=new();
    public List<HardwareBand> Bands {get;}=new();
    public double Height {get;set;}
}
