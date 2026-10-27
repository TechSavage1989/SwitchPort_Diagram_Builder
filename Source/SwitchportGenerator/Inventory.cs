using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
namespace SwitchportGenerator;
public sealed class Inventory
{
    public string Schema { get; set; } = "1.4";
    public string Hostname { get; set; } = "Unknown";
    public string Vendor { get; set; } = "Unknown";
    public string OS { get; set; } = "Unknown";
    public string Firmware { get; set; } = "Unknown";
    public string Topology { get; set; } = "Unknown";
    public string ChassisModel { get; set; } = "";
    public string ReportModel { get; set; } = "";
    public int ChassisSlots { get; set; }
    public bool FlexibleUplinks { get; set; }
    public List<Group> Groups { get; set; } = new();
    public List<DeviceIdentity> Devices { get; set; } = new();
    public List<Port> Ports { get; set; } = new();
    public List<Port> LogicalInterfaces { get; set; } = new();
    public List<Vlan> Vlans { get; set; } = new();
    public List<Svi> SVIs { get; set; } = new();
    public List<string> Routes { get; set; } = new();
    public List<string> Management { get; set; } = new();
    public List<string> Mirrors { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public List<string> Blockers { get; set; } = new();
    // Optional render context is retained in exported inventory so combined reports
    // reproduce exactly the same workspace colors as their individual diagrams.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int,string>? VlanColorOverrides { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string,string>? AggregateColorOverrides { get; set; }
}
public sealed class Group
{
    public string Id { get; set; } = "";
    public string Product { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Device { get; set; } = "1";
    public string Slot { get; set; } = "";
    public int Copper { get; set; }
    public int Optical { get; set; }
    public string OpticalSide { get; set; } = "right";
    public string OpticalType { get; set; } = "SFP+";
    public bool OpticalOnly { get; set; }
    public bool BlankLeft { get; set; }
    public bool Rear { get; set; }
    public List<HardwareSocket> SocketLayout { get; set; } = new();
    public int Expected => Copper + Optical;
}
public sealed class HardwareSocket
{
    public int Number {get;set;}
    public double Column {get;set;}
    public int Row {get;set;}
    public string Media {get;set;}="RJ-45";
    public string InterfaceKind {get;set;}="";
    public string Suffix {get;set;}="";
}
public sealed class Port
{
    public string Id { get; set; } = "";
    public string Group { get; set; } = "";
    public int Number { get; set; }
    public string Description { get; set; } = "";
    public string DisplayNumber { get; set; } = "";
    public List<string> RelatedInterfaces { get; set; } = new();
    public string Media { get; set; } = "Unknown";
    public string Mode { get; set; } = "Unknown";
    public int? UntaggedVlan { get; set; }
    public string UntaggedState { get; set; } = "Unknown";
    public List<int> TaggedVlans { get; set; } = new();
    public int? VoiceVlan { get; set; }
    public string Aggregate { get; set; } = "";
    public string ConnectedDevice { get; set; } = "Unknown/Confirm before use";
    public string Usage { get; set; } = "Unknown/Confirm before use";
    public bool? Disabled { get; set; }
    public string LiveState { get; set; } = "Unknown/Confirm before use";
    public bool StackLink { get; set; }
    public string Speed { get; set; } = "Not specified";
    public List<string> Notes { get; set; } = new();
}
public sealed class Vlan { public int Id { get; set; } public string Name { get; set; } = ""; public bool Voice { get; set; } }
public sealed class Svi { public string Interface { get; set; } = ""; public string Address { get; set; } = ""; }
public static class Parser
{
    public const string SharedPl48Profile="CX6000-6100-48G-4OPT";
    static readonly Dictionary<string,(int Copper,int Optical,string Side,string Media,bool Only,bool Blank)> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        ["J9573A"]=(24,2,"right","SFP+",false,true),
        ["J4820A"]=(24,0,"right","",false,false), ["J4878A"]=(0,4,"right","SFP",true,false), ["J4907A"]=(16,0,"right","",false,false),
        [SharedPl48Profile]=(48,4,"left","SFP/SFP+",false,false),
        ["JL727B"]=(48,4,"right","SFP+",false,false), ["JL675A"]=(48,4,"left","SFP+",false,false), ["JL676A"]=(48,4,"left","SFP+",false,false),
        ["JL558A"]=(48,4,"left","SFP+",false,false), ["JL256A"]=(48,4,"left","SFP+",false,false),
        ["JL661A"]=(48,4,"right","SFP56",false,false), ["JL662A"]=(24,4,"right","SFP56",false,true),
        ["J9536A"]=(20,2,"right","SFP+",false,false), ["J9538A"]=(0,8,"right","SFP+",true,false),
        ["J9993A"]=(0,8,"right","SFP+",true,false), ["J9990A"]=(20,4,"right","SFP+",false,false),
        ["J9534A"]=(24,0,"right","",false,false), ["J9986A"]=(24,0,"right","",false,false), ["J9987A"]=(24,0,"right","",false,false),
        ["J8702A"]=(24,0,"right","",false,false), ["J8705A"]=(20,4,"right","SFP",false,false), ["J8706A"]=(0,24,"right","SFP",false,false),
        ["J9535A"]=(20,4,"right","SFP",false,false), ["J9537A"]=(0,24,"right","SFP",false,false),
        ["J9548A"]=(20,2,"right","SFP+",false,false), ["J9549A"]=(20,4,"right","SFP",false,false), ["J9550A"]=(24,0,"right","",false,false),
        ["JL071A"]=(24,0,"right","",false,true), ["JL073A"]=(24,0,"right","",false,true),
        ["JL072A"]=(48,0,"right","",false,false), ["JL074A"]=(48,0,"right","",false,false),
        ["JL075A"]=(0,16,"right","SFP+",true,false), ["JL076A"]=(48,0,"right","",false,false),
        ["JL083A"]=(0,4,"right","SFP+",true,false), ["JL078A"]=(0,1,"right","QSFP+",true,false), ["JL079A"]=(0,2,"right","QSFP+",true,false), ["JL081A"]=(4,0,"right","",true,false)
    };
    static readonly HashSet<string> LegacyModels=Models.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    static Parser()
    {
        foreach(var model in HardwareCatalog.Models)
            Models.TryAdd(model.Sku,(model.Copper,model.Optical,model.Side,model.OpticalType,model.SingleRow,model.BlankLeft));
    }
    public static IEnumerable<string> SupportedModelCodes=>Models.Keys.Where(k=>k!=SharedPl48Profile).Order(StringComparer.OrdinalIgnoreCase);
    static string JoinPortLists(string source)
    {
        var lines=source.Split('\n').ToList();
        for(int i=0;i<lines.Count;i++)
        {
            if(!Regex.IsMatch(lines[i],@"^\s*(?:no\s+)?(?:tagged|untagged)\b",RegexOptions.IgnoreCase))continue;
            // Backups can wrap inside any part of a port token, including Trk.
            // A bare "tagged" command also contains only port-token characters.
            // Preserve membership command boundaries before joining fragments.
            while(i+1<lines.Count
                &&!Regex.IsMatch(lines[i+1],@"^\s*(?:no\s+)?(?:tagged|untagged)\b",RegexOptions.IgnoreCase)
                &&Regex.IsMatch(lines[i+1].Trim(),@"^[A-LTRK0-9/,\-]+$",RegexOptions.IgnoreCase))
            {
                string tail=lines[i+1].Trim();
                lines[i]+=Regex.IsMatch(lines[i],@"(?:tagged|untagged)\s*$",RegexOptions.IgnoreCase)?" "+tail:tail;
                lines.RemoveAt(i+1);
            }
        }
        return string.Join('\n',lines);
    }
    public static string Safe(string value)
    {
        // Do not export credential-bearing text, including descriptions containing such fields.
        if (Regex.IsMatch(value,@"(?i)\b(password|passwd|secret|private.key|credential|token|psk|encrypted)\b")) return "[REDACTED]";
        return string.Concat(value.Trim().Trim('"').Where(c=> !char.IsControl(c)));
    }
    static string Match(string text,string pattern) => Regex.Match(text,pattern,RegexOptions.IgnoreCase|RegexOptions.Multiline).Groups[1].Value.Trim().Trim('"');
    public static Inventory Parse(string source,string? selectedModels=null)
    {
        var inv=new Inventory();
        if(source.Length>20_000_000) { inv.Blockers.Add("Input exceeds the 20 MB limit."); return inv; }
        if(Regex.IsMatch(source,@"(?im)^vInstantOn_"))
        {
            return InstantOnParser.Parse(source,selectedModels);
        }
        if(MikroTikParser.IsConfiguration(source))return MikroTikParser.Parse(source,selectedModels);
        if(ComwareParser.IsConfiguration(source))return ComwareParser.Parse(source,selectedModels);
        string text=JoinPortLists(source.Replace("\r",""));
        bool cx=Regex.IsMatch(text,@"(?i)(ArubaOS-CX|AOS-CX|!Version\s+\w+\.10\.)");
        bool aos=Regex.IsMatch(text,@"(?im)(Configuration Editor|^\s*module\s+\w+\s+type|^\s*untagged\s+|^\s*vlan\s+\d+)");
        if(!cx&&!aos) {inv.Blockers.Add("Unsupported configuration format. Use a supported AOS-Switch, AOS-CX, Comware or Instant On text backup with a verified hardware profile.");return inv;}
        inv.Vendor="Aruba/HPE"; inv.OS=cx?"AOS-CX":"AOS-Switch";
        string name=Match(text,@"^\s*hostname\s+(.+)$");if(name.Length>0)inv.Hostname=Safe(name);
        string version=cx?Match(text,@"!Version\s+ArubaOS-CX\s+([A-Za-z0-9._-]+)"):Match(text,@"Created on release\s+#?([A-Za-z0-9._-]+)");
        if(version.Length==0)version=Match(text,@"(?:ArubaOS-CX|AOS-CX)\s+(?:Version\s+)?((?:[A-Z]{1,4}\.)?\d+\.[A-Za-z0-9._-]+)");
        if(version.Length>0)inv.Firmware=Safe(version);
        bool stack=Regex.IsMatch(text,@"(?im)^\s*(?:vsf|stacking|virtual-chassis)\b");
        inv.Topology=Regex.IsMatch(text,@"(?im)^vsx\b")?"VSX":stack?"Stack/VSF":"Fixed";
        var declared=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(Match m in Regex.Matches(text,@"(?im)^\s*module\s+([\w/]+)\s+type\s+""?([A-Z0-9-]+)""?")) declared[m.Groups[1].Value]=m.Groups[2].Value;
        string member="";
        foreach(string raw in text.Split('\n'))
        {
            var mm=Regex.Match(raw,@"^\s*(?:vsf\s+)?member\s+(\d+)\b",RegexOptions.IgnoreCase);
            if(mm.Success&& !raw.Contains("flexible-module",StringComparison.OrdinalIgnoreCase)){member=mm.Groups[1].Value; var inline=Regex.Match(raw,@"\btype\s+""?([A-Z0-9]+)",RegexOptions.IgnoreCase);if(inline.Success)declared[member]=inline.Groups[1].Value;continue;}
            var ty=Regex.Match(raw,@"^\s+type\s+""?([A-Z0-9]+)",RegexOptions.IgnoreCase);
            if(member.Length>0&&ty.Success)declared[member]=ty.Groups[1].Value;
            if(raw.Length>0&&!char.IsWhiteSpace(raw[0])&&!Regex.IsMatch(raw,@"^(vsf|stacking)\b",RegexOptions.IgnoreCase))member="";
        }
        foreach(Match m in Regex.Matches(text,@"(?im)^\s*member\s+(\d+)\s+flexible-module\s+([AB])\s+type\s+([A-Z0-9]+)"))
            declared[m.Groups[1].Value+"/"+m.Groups[2].Value.ToUpperInvariant()]=m.Groups[3].Value;
        HardwareCatalog.ApplySelection(inv,text,selectedModels,declared);
        if(declared.Keys.Count(k=>Regex.IsMatch(k,@"^\d+$"))>1){stack=true;inv.Topology="Stack/member inventory";}
        // The legacy module spelling is only resolved against a corroborating product header.
        foreach(string key in declared.Keys.ToList())
            if(declared[key].Equals("J9573X",StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(text,@"(?im)^;.*\bJ9573A\b"))declared[key]="J9573A";
        HardwareProfiles.Identify(inv,text,declared);
        bool rearUplinks=declared.Values.Any(HardwareProfiles.Is2920);
        if(inv.ChassisSlots>0)
            foreach(string key in declared.Keys.Where(k=>Regex.IsMatch(k,@"^\d+$")).ToList())
            {
                int slot=int.Parse(key);string value=declared[key];declared.Remove(key);
                if(slot<1||slot>inv.ChassisSlots){inv.Blockers.Add("Numeric module slot exceeds chassis capacity.");continue;}
                string letter=((char)('A'+slot-1)).ToString();
                if(declared.ContainsKey(letter))inv.Blockers.Add("Duplicate module slot assignment: "+letter);else declared[letter]=value;
            }
        bool modular=!inv.FlexibleUplinks&&!rearUplinks&&declared.Keys.Any(k=>Regex.IsMatch(k,@"^(?:\d+/)?[A-Za-z]$"));
        if(modular)inv.Topology="Modular";
        if(declared.Count==0)
        {
            var codes=HardwareCatalog.ProductCodes(text);
            var known=codes.Where(Models.ContainsKey).ToList();
            if(known.Count==1&&codes.Count==1&&!stack)declared["1"]=known[0];
            else if(codes.Count==0&&cx&&inv.Topology=="Fixed"&&inv.ChassisSlots==0&&
                !Regex.IsMatch(text,@"(?im)^[ \t]*(?:product|model|chassis|system[ \t]+model)\b")&&
                Regex.IsMatch(text,@"(?im)^!Version[ \t]+ArubaOS-CX[ \t]+PL\.10\.\d+\.\d+[\w.-]*[ \t]*$")&&
                HasCompletePl48Inventory(text))
            {
                // PL is shared by the 6000 and 6100 families. The complete interface
                // inventory establishes their common faceplate, not SKU or link speed.
                declared["1"]=SharedPl48Profile;
                inv.Assumptions.Add("6000/6100 shared 48G/4-optical front geometry selected from PL firmware and the complete single-switch 1/1/1-1/1/52 inventory. Hostname and port descriptions are not hardware evidence. Exact SKU, PoE variant and SFP versus SFP+ capability are unconfirmed; check the chassis label.");
            }
            else inv.Blockers.Add(codes.Count>1?"Multiple product identifiers found without explicit module/member assignments.":"No supported product identifier and member inventory could be established.");
        }
        foreach(var d in declared.OrderBy(d=>d.Key,StringComparer.OrdinalIgnoreCase))
        {
            if(!Models.TryGetValue(d.Value,out var model)){inv.Blockers.Add("Unsupported module/member product: "+Safe(d.Value)+". Physical counts and geometry cannot be confirmed.");continue;}
            // Interface spelling is derived from the OS and explicit stack/module context.
            bool flexModule=inv.FlexibleUplinks&&Regex.IsMatch(d.Key,@"^(?:\d+/)?[AB]$",RegexOptions.IgnoreCase);
            bool rearModule=rearUplinks&&Regex.IsMatch(d.Key,@"^(?:\d+/)?[AB]$",RegexOptions.IgnoreCase);
            bool slot=modular||flexModule||rearModule;
            string prefix=slot?d.Key.ToUpperInvariant():cx?d.Key+"/1/":stack?d.Key+"/":"";
            var g=new Group{Id=slot?"Module "+d.Key:stack?"Member "+d.Key:"Switch",Product=d.Value.ToUpperInvariant(),Prefix=prefix,Device=d.Key.Contains('/')?d.Key.Split('/')[0]:slot?"1":d.Key,Slot=slot?d.Key.Split('/').Last().ToUpperInvariant():"",Copper=model.Copper,Optical=model.Optical,OpticalSide=model.Side,OpticalType=model.Media,OpticalOnly=model.Only,BlankLeft=model.Blank,Rear=rearModule};
            var catalogue=HardwareCatalog.Find(d.Value);
            if(catalogue!=null)
            {
                if(catalogue.OS!=inv.OS){inv.Blockers.Add("Selected hardware operating system differs from the configuration: "+d.Value);continue;}
                if(!LegacyModels.Contains(d.Value))g.SocketLayout=catalogue.Sockets;
            }
            inv.Groups.Add(g);
            for(int n=1;n<=g.Expected;n++)inv.Ports.Add(new Port{Id=prefix+n,Group=g.Id,Number=n,Media=g.SocketLayout.FirstOrDefault(s=>s.Number==n)?.Media??(n<=g.Copper?"RJ-45":g.OpticalType)});
        }
        HardwareProfiles.Add2920RearModules(inv,text,stack);
        foreach(var g in inv.Groups.Where(g=>g.Product=="J4907A"))
            foreach(var p in inv.Ports.Where(p=>p.Group==g.Id&&p.Number>=15))
            {p.Notes.Add("Dual-personality interface: T RJ-45 and M SFP sockets share this interface. Only one may be used; active medium is not recorded in saved configuration.");}
        foreach(var g in inv.Groups.Where(g=>g.SocketLayout.Count>0))foreach(var pair in g.SocketLayout.GroupBy(s=>s.Number).Where(s=>s.Count()>1))
            inv.Ports.Single(p=>p.Group==g.Id&&p.Number==pair.Key).Notes.Add("Dual-personality interface: RJ45 and SFP sockets share one interface; active medium is not established by this configuration.");
        var ports=inv.Ports.ToDictionary(p=>p.Id,StringComparer.OrdinalIgnoreCase);
        HardwareProfiles.Validate(inv);
        // Without a model, every interface would produce the same secondary error.
        // Keep the identity failure actionable; geometry validation adds its summary.
        if(ports.Count==0){inv.Blockers=inv.Blockers.Distinct().ToList();return inv;}
        var aggregateMembers=new Dictionary<string,List<Port>>(StringComparer.OrdinalIgnoreCase);
        var aggregateConfig=new Dictionary<string,Port>(StringComparer.OrdinalIgnoreCase);
        List<Port> Resolve(string spec,bool block=true)
        {
            var result=new List<Port>();
            foreach(string token in Expand(spec,inv))
            {
                if(ports.TryGetValue(token,out var p))result.Add(p);
                else if(aggregateMembers.TryGetValue(token,out var members))result.AddRange(members);
                else if(Regex.IsMatch(token,@"^(?:trk\d+|lag\d+)$",RegexOptions.IgnoreCase)) { }
                else if(block)inv.Blockers.Add("Configured physical interface is outside the validated model inventory: "+Safe(token));
            }
            return result.Distinct().ToList();
        }
        foreach(Match m in Regex.Matches(text,@"(?im)^\s*trunk\s+(\S+)\s+(trk\d+)\s+(\w+)"))
        {
            var members=Resolve(m.Groups[1].Value);string agg=m.Groups[2].Value;
            aggregateMembers[agg]=members;
            foreach(var p in members){p.Aggregate=agg;p.Notes.Add("Aggregation mode: "+Safe(m.Groups[3].Value));}
        }
        int? vlan=null;List<Port> current=new();string context="";string currentAgg="";string neighborPort="";string vsfMember="1";
        List<Port> StackPorts(string spec)
        {
            string qualified=string.Join(',',spec.Split(',').Select(t=>Regex.IsMatch(t,@"^\d+(?:-\d+)?$")?vsfMember+(cx?"/1/":"/")+t:t));
            return Resolve(qualified);
        }
        void MarkStack(IEnumerable<Port> members)
        {
            foreach(var p in members){p.StackLink=true;p.Mode="Stack/VSF";p.Usage="Stack link - confirm live state";if(!p.Notes.Contains("Configured VSF/stack link"))p.Notes.Add("Configured VSF/stack link");}
        }
        foreach(string raw in text.Split('\n'))
        {
            string line=raw.Trim();
            if(line.Length==0||line.StartsWith(';')||line.StartsWith('!')||line.StartsWith('#'))continue;
            if(Regex.IsMatch(line,@"(?i)\b(password|passwd|secret|private-key|credential|token|psk)\b"))continue;
            Match m;
            if((m=Regex.Match(line,@"^(?:vsf\s+)?member\s+(\d+)",RegexOptions.IgnoreCase)).Success)vsfMember=m.Groups[1].Value;
            if(cx&&(m=Regex.Match(line,@"^vlan\s+(\d+[\d,\-]*)$",RegexOptions.IgnoreCase)).Success&&(m.Groups[1].Value.Contains(',')||m.Groups[1].Value.Contains('-')))
            {foreach(int id in VlanIds(m.Groups[1].Value,inv))GetVlan(inv,id);context="";vlan=null;current=new();continue;}
            if((m=Regex.Match(line,@"^vlan\s+(\d+)$",RegexOptions.IgnoreCase)).Success){vlan=int.Parse(m.Groups[1].Value);context="vlan";current=new();GetVlan(inv,vlan.Value);continue;}
            if((m=Regex.Match(line,@"^interface\s+vlan\s+(\d+)",RegexOptions.IgnoreCase)).Success){vlan=int.Parse(m.Groups[1].Value);context="svi";current=new();continue;}
            if((m=Regex.Match(line,@"^interface\s+lag\s+(\d+)",RegexOptions.IgnoreCase)).Success)
            {
                currentAgg="lag"+m.Groups[1].Value;var p=new Port{Id=currentAgg};aggregateConfig[currentAgg]=p;current=new(){p};context="port";vlan=null;continue;
            }
            if((m=Regex.Match(line,@"^interface\s+([\dA-Za-z][\dA-Za-z/,\- ]*)$",RegexOptions.IgnoreCase)).Success)
            {
                string spec=m.Groups[1].Value.Trim();
                if(Regex.IsMatch(spec,@"^(loopback|mgmt|vlan|vxlan|tunnel)\b",RegexOptions.IgnoreCase)){context=spec.StartsWith("mgmt")?"management":"other";current=new();continue;}
                current=Resolve(spec);context="port";vlan=null;currentAgg="";continue;
            }
            if(line.Equals("exit",StringComparison.OrdinalIgnoreCase)||line.Equals("end",StringComparison.OrdinalIgnoreCase)){context="";vlan=null;current=new();continue;}
            if((m=Regex.Match(line,@"^ip\s+route\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Routes.Add(Safe(m.Groups[1].Value));continue;}
            if((m=Regex.Match(line,@"^ip\s+default-gateway\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Management.Add("Default gateway: "+Safe(m.Groups[1].Value));continue;}
            if(Regex.IsMatch(line,@"^(?:mirror|monitor\s+session)\b",RegexOptions.IgnoreCase))inv.Mirrors.Add(Safe(line));
            if((m=Regex.Match(line,@"^(?:Local Port|Local Interface)\s*:\s*(\S+)",RegexOptions.IgnoreCase)).Success)neighborPort=m.Groups[1].Value;
            if(neighborPort.Length>0&&(m=Regex.Match(line,@"^System Name\s*:\s*(.+)$",RegexOptions.IgnoreCase)).Success)
                foreach(var p in Resolve(neighborPort,false))p.ConnectedDevice=Safe(m.Groups[1].Value);
            if((m=Regex.Match(line,@"^link\s+\d+\s+(?:name\s+\S+\s+)?(?:interface\s+)?([\d/,\-]+)$",RegexOptions.IgnoreCase)).Success)
                MarkStack(StackPorts(m.Groups[1].Value));
            if((m=Regex.Match(line,@"^vsf\s+member\s+\d+\s+link\s+\d+\s+([\d/,\-]+)$",RegexOptions.IgnoreCase)).Success)
                MarkStack(StackPorts(m.Groups[1].Value));
            if(context=="vlan"&&vlan.HasValue)
            {
                var v=GetVlan(inv,vlan.Value);
                if((m=Regex.Match(line,@"^name\s+(.+)$",RegexOptions.IgnoreCase)).Success)v.Name=Safe(m.Groups[1].Value);
                else if(line.Equals("voice",StringComparison.OrdinalIgnoreCase))v.Voice=true;
                else if((m=Regex.Match(line,@"^(no\s+)?(untagged|tagged)\s+(.+)$",RegexOptions.IgnoreCase)).Success)
                {
                    foreach(var p in Resolve(m.Groups[3].Value))
                    {
                        bool no=m.Groups[1].Success;
                        if(m.Groups[2].Value.Equals("untagged",StringComparison.OrdinalIgnoreCase))
                        {
                            if(no){if(p.UntaggedVlan==v.Id||p.UntaggedVlan==null){p.UntaggedVlan=null;p.UntaggedState="None";}}
                            else SetUntagged(p,v.Id,inv);
                        }
                        else if(no)p.TaggedVlans.Remove(v.Id);else if(!p.TaggedVlans.Contains(v.Id))p.TaggedVlans.Add(v.Id);
                    }
                }
                else if((m=Regex.Match(line,@"^ip\s+address\s+(.+)$",RegexOptions.IgnoreCase)).Success)inv.SVIs.Add(new Svi{Interface="vlan "+v.Id,Address=Safe(m.Groups[1].Value)});
            }
            else if(context=="svi"&&vlan.HasValue&&(m=Regex.Match(line,@"^ip(?:v6)?\s+address\s+(.+)$",RegexOptions.IgnoreCase)).Success)inv.SVIs.Add(new Svi{Interface="vlan "+vlan,Address=Safe(m.Groups[1].Value)});
            else if(context=="management"&&(m=Regex.Match(line,@"^ip(?:v6)?\s+(?:address|static)\s+(.+)$",RegexOptions.IgnoreCase)).Success)inv.Management.Add(Safe(m.Groups[1].Value));
            else if(context=="port")
            {
                if((m=Regex.Match(line,@"^(?:name|description)\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(var p in current)p.Description=Safe(m.Groups[1].Value);
                else if(Regex.IsMatch(line,@"^(?:stack-port|stacking-port|vsf-link)(?:\s|$)",RegexOptions.IgnoreCase))MarkStack(current);
                else if(line.Equals("disable",StringComparison.OrdinalIgnoreCase)||line.Equals("shutdown",StringComparison.OrdinalIgnoreCase))foreach(var p in current)p.Disabled=true;
                else if(line.Equals("enable",StringComparison.OrdinalIgnoreCase)||line.Equals("no shutdown",StringComparison.OrdinalIgnoreCase))foreach(var p in current)p.Disabled=false;
                else if((m=Regex.Match(line,@"^vlan\s+(?:access|trunk\s+native)\s+(\d+)(.*)$",RegexOptions.IgnoreCase)).Success)
                {
                    foreach(var p in current){SetUntagged(p,int.Parse(m.Groups[1].Value),inv);p.Mode=line.Contains("access")?"Access":"Trunk";if(m.Groups[2].Value.Contains("tag",StringComparison.OrdinalIgnoreCase)){p.UntaggedVlan=null;p.UntaggedState="None";p.TaggedVlans.Add(int.Parse(m.Groups[1].Value));}}
                }
                else if((m=Regex.Match(line,@"^vlan\s+trunk\s+allowed\s+(.+)$",RegexOptions.IgnoreCase)).Success)
                {
                    if(m.Groups[1].Value.Equals("all",StringComparison.OrdinalIgnoreCase))foreach(var p in current){p.Mode="Trunk";p.Notes.Add("Tagged VLANs: all allowed by configuration; actual active VLANs require confirmation.");}
                    else foreach(var p in current){p.Mode="Trunk";p.TaggedVlans=VlanIds(m.Groups[1].Value,inv);}
                }
                else if((m=Regex.Match(line,@"^lag\s+(\d+)",RegexOptions.IgnoreCase)).Success)
                {
                    string agg="lag"+m.Groups[1].Value;if(!aggregateMembers.ContainsKey(agg))aggregateMembers[agg]=new();
                    foreach(var p in current){p.Aggregate=agg;aggregateMembers[agg].Add(p);}
                }
                else if((m=Regex.Match(line,@"^lacp\s+mode\s+(active|passive)$",RegexOptions.IgnoreCase)).Success)foreach(var p in current)p.Notes.Add("Aggregation mode: LACP "+m.Groups[1].Value);
                else if((m=Regex.Match(line,@"^(?:speed|speed-duplex)\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(var p in current)p.Speed=Safe(m.Groups[1].Value);
                else if(Regex.IsMatch(line,@"^no\s+routing$",RegexOptions.IgnoreCase)) { }
                else if(line.Equals("routing",StringComparison.OrdinalIgnoreCase))foreach(var p in current){p.Mode="Routed";p.UntaggedState="None";p.UntaggedVlan=null;}
                else if((m=Regex.Match(line,@"^ip(?:v6)?\s+address\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(var p in current){p.Mode="Routed";p.Notes.Add("Address: "+Safe(m.Groups[1].Value));}
                else if(Regex.IsMatch(line,@"^vlan\s",RegexOptions.IgnoreCase))inv.Blockers.Add("Unrecognized VLAN syntax on a physical interface.");
            }
        }
        foreach(var a in aggregateConfig)
        {
            if(!aggregateMembers.TryGetValue(a.Key,out var members)||members.Count==0){inv.Blockers.Add("LAG has no physical member inventory: "+a.Key);continue;}
            foreach(var p in members)
            {
                var q=a.Value;if(q.UntaggedVlan.HasValue)SetUntagged(p,q.UntaggedVlan.Value,inv);
                if(q.UntaggedState=="None")p.UntaggedState="None";
                p.TaggedVlans=p.TaggedVlans.Concat(q.TaggedVlans).Distinct().ToList();
                if(q.Mode!="Unknown")p.Mode=q.Mode;
                p.Notes.AddRange(q.Notes);
            }
        }
        foreach(var p in inv.Ports)
        {
            p.TaggedVlans=p.TaggedVlans.Distinct().Order().ToList();
            if(p.UntaggedVlan.HasValue&&p.TaggedVlans.Contains(p.UntaggedVlan.Value)){p.TaggedVlans.Remove(p.UntaggedVlan.Value);p.Notes.Add("Native VLAN excluded from tagged membership.");}
            var voices=inv.Vlans.Where(v=>v.Voice&&(p.UntaggedVlan==v.Id||p.TaggedVlans.Contains(v.Id))).Select(v=>v.Id).ToList();
            if(voices.Count==1)p.VoiceVlan=voices[0];else if(voices.Count>1)p.Notes.Add("Multiple voice VLAN memberships require confirmation.");
            if(p.Mode=="Unknown"&&(p.TaggedVlans.Count>0||p.Notes.Any(n=>n.StartsWith("Tagged VLANs:"))))p.Mode="Tagged/mixed";
            else if(p.Mode=="Unknown"&&p.UntaggedVlan.HasValue)p.Mode="Untagged";
        }
        inv.Assumptions.Add("Physical geometry follows the identified product or explicitly annotated shared hardware profile. No unconfigured VLAN, live link state, serial number, or connected device is inferred.");
        inv.Assumptions.Add("Optical labels identify cage capability, not an installed transceiver. Configured speed is not a measured link speed.");
        inv.Assumptions.Add("AOS-Switch and AOS-CX only; unsupported products and interfaces block rendering. Operational neighbor data is accepted only as Local Port/Interface and System Name detail fields.");
        inv.Assumptions.Add("No implicit default VLAN membership is applied. Omitted values remain Unknown/Confirm before use. Unknown configuration directives outside the supported fields are not interpreted.");
        inv.Blockers=inv.Blockers.Distinct().ToList();return inv;
    }
    static bool HasCompletePl48Inventory(string text)
    {
        var physical=Regex.Matches(text,@"(?im)^[ \t]*interface[ \t]+(\d[\d/, \t-]*)[ \t]*$")
            .Select(m=>m.Groups[1].Value.Trim()).ToList();
        return physical.Count==52&&Enumerable.Range(1,52).Select(n=>"1/1/"+n).ToHashSet().SetEquals(physical);
    }
    static Vlan GetVlan(Inventory inv,int id){var v=inv.Vlans.Find(v=>v.Id==id);if(v==null){v=new Vlan{Id=id};inv.Vlans.Add(v);}if(id<1||id>4094)inv.Blockers.Add("VLAN ID outside 1-4094.");return v;}
    static void SetUntagged(Port p,int vlan,Inventory inv)
    {
        if(vlan<1||vlan>4094)inv.Blockers.Add("VLAN ID outside 1-4094.");
        if(p.UntaggedVlan.HasValue&&p.UntaggedVlan!=vlan)inv.Blockers.Add("Contradictory untagged/native VLANs on interface "+p.Id);
        p.UntaggedVlan=vlan;p.UntaggedState="Known";
    }
    static List<int> VlanIds(string text,Inventory inv)
    {
        var result=new List<int>();foreach(var t in text.Split(','))
        {
            var m=Regex.Match(t.Trim(),@"^(\d+)(?:-(\d+))?$");if(!m.Success){inv.Blockers.Add("Unrecognized VLAN list.");continue;}
            int a=int.Parse(m.Groups[1].Value),b=m.Groups[2].Success?int.Parse(m.Groups[2].Value):a;
            if(a<1||b>4094||b<a){inv.Blockers.Add("Invalid VLAN range.");continue;}
            for(int n=a;n<=b;n++)result.Add(n);
        }return result.Distinct().Order().ToList();
    }
    static IEnumerable<string> Expand(string spec,Inventory inv)
    {
        foreach(string t in spec.Replace(" ","").Split(',',StringSplitOptions.RemoveEmptyEntries))
        {
            if(!t.Contains('-')){yield return t;continue;}
            string[] halves=t.Split('-');
            var a=Regex.Match(halves[0],@"^(.*?)(\d+)$");var b=halves.Length==2?Regex.Match(halves[1],@"^(.*?)(\d+)$"):System.Text.RegularExpressions.Match.Empty;
            if(!a.Success||!b.Success){inv.Blockers.Add("Invalid interface range.");continue;}
            string prefix=a.Groups[1].Value;string endPrefix=b.Groups[1].Value;
            if(endPrefix.Length>0&&!endPrefix.Equals(prefix,StringComparison.OrdinalIgnoreCase)){inv.Blockers.Add("Interface range crosses inventory groups.");continue;}
            int start=int.Parse(a.Groups[2].Value),end=int.Parse(b.Groups[2].Value);
            if(end<start||end-start>512){inv.Blockers.Add("Invalid interface range length.");continue;}
            for(int n=start;n<=end;n++)yield return prefix+n;
        }
    }
}
