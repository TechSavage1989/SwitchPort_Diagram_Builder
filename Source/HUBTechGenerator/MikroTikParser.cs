using System.Text.Json;
using System.Text.RegularExpressions;
namespace HUBTechGenerator;

public static class MikroTikParser
{
    public sealed class Interface {public string Key{get;set;}="";public string Name{get;set;}="";public string DefaultName{get;set;}="";public string Comment{get;set;}="";public bool Disabled{get;set;}}
    public sealed class Bridge {public string Key{get;set;}="";public string Name{get;set;}="";public bool? Filtering{get;set;}}
    public sealed class BridgePort {public string Interface{get;set;}="";public string Bridge{get;set;}="";public int Pvid{get;set;}=1;public string Frames{get;set;}="admit-all";public bool Disabled{get;set;}public bool TagStacking{get;set;}}
    public sealed class VlanRow {public string Bridge{get;set;}="";public List<int> Ids{get;set;}=new();public List<string> Tagged{get;set;}=new();public List<string> Untagged{get;set;}=new();public string Comment{get;set;}="";public bool Disabled{get;set;}}
    public sealed class Bond {public string Name{get;set;}="";public List<string> Slaves{get;set;}=new();public string Mode{get;set;}="balance-rr";public bool Disabled{get;set;}}
    public sealed class Snapshot {public bool Binary{get;set;}public string Hostname{get;set;}="Unknown";public string Firmware{get;set;}="Unknown";public string Model{get;set;}="";public List<Interface> Interfaces{get;set;}=new();public List<Bridge> Bridges{get;set;}=new();public List<BridgePort> BridgePorts{get;set;}=new();public List<VlanRow> Vlans{get;set;}=new();public List<Bond> Bonds{get;set;}=new();public List<string> Unsupported{get;set;}=new();}
    public sealed record Model(string Code,int Copper,int Fiber,int Qsfp,bool FiberRows);
    public static readonly Model[] Models={new("CRS326-24S+2Q+RM",1,24,2,true),new("CRS328-24P-4S+RM",24,4,0,true),new("CRS317-1G-16S+RM",1,16,0,false),new("CRS309-1G-8S+IN",1,8,0,false)};
    public static bool IsConfiguration(string source)=>source.StartsWith(MikroTikBackup.Prefix,StringComparison.Ordinal)||Regex.IsMatch(source,@"(?im)^#.*RouterOS\s+\d|^/interface(?:[ /]+)(?:bridge|ethernet|bonding)\b|^/system[ /]+identity\b");
    public static string ReadHostname(string source)
    {
        try{string hostname=SnapshotFrom(source).Hostname;return hostname=="Unknown"?"":hostname;}catch{return "";}
    }
    static string Unquote(string value)
    {
        if(value.StartsWith('"')&&value.EndsWith('"'))value=value[1..^1];
        return Regex.Replace(value,@"\\([0-9A-Fa-f]{2}|.)",m=>m.Groups[1].Value.Length==2?((char)Convert.ToInt32(m.Groups[1].Value,16)).ToString():m.Groups[1].Value switch{"n"=>" ","r"=>" ","t"=>" ",var s=>s});
    }
    static Dictionary<string,string> Properties(string command)=>Regex.Matches(command,"(?<key>[A-Za-z][A-Za-z0-9-]*)=(?<value>\"(?:\\\\.|[^\"\\\\])*\"|[^\\s\\]]+)").Cast<Match>().ToDictionary(m=>m.Groups["key"].Value,m=>Unquote(m.Groups["value"].Value),StringComparer.OrdinalIgnoreCase);
    static List<string> Names(string text)=>text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList();
    static List<int> Vlans(string text)
    {
        var ids=new List<int>();foreach(string token in text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {var range=token.Split('-');if(range.Length>2||!int.TryParse(range[0],out int first)||!int.TryParse(range[^1],out int last)||first<1||last>4094||last<first)throw new InvalidOperationException("Invalid RouterOS VLAN range.");ids.AddRange(Enumerable.Range(first,last-first+1));}return ids.Distinct().Order().ToList();
    }
    static Snapshot SnapshotFrom(string source)
    {
        if(source.StartsWith(MikroTikBackup.Prefix,StringComparison.Ordinal))return JsonSerializer.Deserialize<Snapshot>(source[MikroTikBackup.Prefix.Length..])??throw new InvalidOperationException("Invalid RouterOS inventory.");
        var s=new Snapshot();var modelMatch=Regex.Match(source,@"(?im)^#\s*(?:model|board-name)\s*=\s*(.+)$");if(modelMatch.Success)s.Model=modelMatch.Groups[1].Value.Trim();
        var fw=Regex.Match(source,@"(?im)^#.*RouterOS\s+([\w.()-]+)");if(fw.Success)s.Firmware=fw.Groups[1].Value;
        string section="";
        foreach(string raw in Regex.Replace(source.Replace("\r",""),@"\\\s*\n\s*","").Split('\n'))
        {
            string line=raw.Trim();if(line.Length==0||line.StartsWith('#'))continue;
            if(line.StartsWith('/'))
            {
                var command=Regex.Match(line,@"^(.*?)\s+(add|set|remove|enable|disable|:|print|export)\b(.*)$");
                if(!command.Success){section=line.Trim('/').Replace('/',' ').Trim();continue;}
                section=command.Groups[1].Value.Trim('/').Replace('/',' ').Trim();line=command.Groups[2].Value+command.Groups[3].Value;
            }
            if(!line.StartsWith("add ")&&!line.StartsWith("set "))
            {if(section.StartsWith("interface bridge")||section.StartsWith("interface ethernet")||section=="interface bonding")s.Unsupported.Add("Unsupported RouterOS switching command. Supply a complete saved .rsc export.");continue;}
            var p=Properties(line);string Get(string key,string fallback="")=>p.GetValueOrDefault(key,fallback);
            if(section=="system identity")s.Hostname=Parser.Safe(Get("name","Unknown"));
            else if(section=="interface ethernet")
            {
                string original=Get("default-name");if(original.Length==0){var target=Regex.Match(line,@"^set\s+([^\s\[]+)");if(target.Success)original=Unquote(target.Groups[1].Value);}
                if(original.Length==0){s.Unsupported.Add("RouterOS Ethernet selector has no default-name. Export using normal or verbose RouterOS export.");continue;}
                s.Interfaces.Add(new(){Key=Get("name",original),Name=Get("name",original),DefaultName=original,Comment=Parser.Safe(Get("comment")),Disabled=Get("disabled")=="yes"});
            }
            else if(section=="interface bridge")
            {
                if(!line.StartsWith("add ")){s.Unsupported.Add("RouterOS bridge set selectors require a complete export with bridge creation.");continue;}
                s.Bridges.Add(new(){Key=Get("name"),Name=Get("name"),Filtering=Get("vlan-filtering","no")=="yes"});
            }
            else if(section=="interface bridge port")
                s.BridgePorts.Add(new(){Interface=Get("interface"),Bridge=Get("bridge"),Pvid=int.TryParse(Get("pvid","1"),out int id)?id:0,Frames=Get("frame-types","admit-all"),Disabled=Get("disabled")=="yes",TagStacking=Get("tag-stacking")=="yes"});
            else if(section=="interface bridge vlan")
                s.Vlans.Add(new(){Bridge=Get("bridge"),Ids=Vlans(Get("vlan-ids")),Tagged=Names(Get("tagged")),Untagged=Names(Get("untagged")),Comment=Parser.Safe(Get("comment")),Disabled=Get("disabled")=="yes"});
            else if(section=="interface bonding")
                s.Bonds.Add(new(){Name=Get("name"),Slaves=Names(Get("slaves")),Mode=Get("mode","balance-rr"),Disabled=Get("disabled")=="yes"});
            else if(section.StartsWith("interface ethernet switch"))s.Unsupported.Add("Legacy RouterOS switch-chip VLAN syntax is not yet validated. Use bridge VLAN filtering configuration.");
            else if(section is "interface bridge filter" or "interface bridge nat" or "interface bridge mlag")s.Unsupported.Add("RouterOS bridge filter/NAT/MLAG rules need additional validation before diagram export.");
        }
        return s;
    }
    public static Inventory Parse(string source,string? selectedModels=null)
    {
        var inv=new Inventory{Vendor="MikroTik",OS="RouterOS",Topology="Fixed"};
        try
        {
            var s=SnapshotFrom(source);inv.Hostname=Parser.Safe(s.Hostname);inv.Firmware=Parser.Safe(s.Firmware);inv.Blockers.AddRange(s.Unsupported);
            string selected=(selectedModels??"").Trim();if(selected.StartsWith("1="))selected=selected[2..];
            var profile=Models.FirstOrDefault(m=>m.Code.Equals(s.Model,StringComparison.OrdinalIgnoreCase));
            bool confirmed=profile!=null;
            if(s.Model.Length>0&&profile==null)inv.Blockers.Add("No verified MikroTik hardware layout for "+Parser.Safe(s.Model)+".");
            if(selected.Length>0)
            {
                var chosen=Models.FirstOrDefault(m=>m.Code.Equals(selected,StringComparison.OrdinalIgnoreCase));
                if(chosen==null)inv.Blockers.Add("No verified MikroTik layout for the selected model.");
                else if(profile!=null&&chosen.Code!=profile.Code)inv.Blockers.Add("Selected MikroTik model conflicts with the configuration.");
                else {profile=chosen;confirmed=true;}
            }
            // Only the complete, unique interface signature enables anonymous geometry.
            if(profile==null&&s.Model.Length==0&&s.Binary)
                profile=Models.SingleOrDefault(m=>DefaultNames(m).Order().SequenceEqual(s.Interfaces.Select(i=>i.DefaultName).Order()));
            if(profile==null){inv.Blockers.Add("MikroTik model not identified. Select a supported model in Device details, or use a complete RouterOS export containing its model header.");return inv;}
            var expected=DefaultNames(profile).ToList();
            if(s.Interfaces.GroupBy(i=>i.DefaultName).Any(g=>g.Count()>1)||s.Interfaces.GroupBy(i=>i.Name).Any(g=>g.Count()>1))inv.Blockers.Add("Duplicate MikroTik interface identity.");
            if(s.Interfaces.Any(i=>!expected.Contains(i.DefaultName)))inv.Blockers.Add("RouterOS interface inventory is outside the selected model layout.");
            if(s.Binary&&s.Interfaces.Count!=expected.Count)inv.Blockers.Add("MikroTik binary interface inventory is incomplete.");
            if(inv.Blockers.Count>0)return inv;
            // In text exports, omitted Ethernet settings use the model's default names.
            foreach(string name in expected.Where(name=>!s.Interfaces.Any(i=>i.DefaultName==name)))s.Interfaces.Add(new(){Key=name,Name=name,DefaultName=name});
            var byKey=s.Interfaces.ToDictionary(i=>i.Key);var logical=new Dictionary<string,Port>();
            foreach(string name in expected)
            {
                var i=s.Interfaces.Single(i=>i.DefaultName==name);
                logical.Add(i.Key,new(){Id=i.Name,Description=i.Comment,Media=name.StartsWith("ether")?"RJ-45":name.StartsWith("qsfp")?"QSFP+":"SFP+",Disabled=i.Disabled});
            }
            var bridges=s.Bridges.ToDictionary(b=>b.Key);var bonds=s.Bonds.ToDictionary(b=>b.Name);
            IEnumerable<Port> Resolve(string name)
            {
                if(logical.TryGetValue(name,out var port))return new[]{port};
                if(bonds.TryGetValue(name,out var bond))return bond.Slaves.Select(slave=>logical.GetValueOrDefault(slave)).Where(p=>p!=null).Cast<Port>();
                if(bridges.ContainsKey(name))return Array.Empty<Port>();
                inv.Blockers.Add("Unresolved RouterOS interface reference: "+Parser.Safe(name));return Array.Empty<Port>();
            }
            foreach(var bond in s.Bonds)
            {
                if(bond.Slaves.Count<2||bond.Slaves.Any(n=>!logical.ContainsKey(n))){inv.Blockers.Add("Invalid RouterOS bond members.");continue;}
                foreach(var p in Resolve(bond.Name))
                {if(p.Aggregate.Length>0)inv.Blockers.Add("RouterOS interface belongs to multiple bonds.");p.Aggregate=Parser.Safe(bond.Name);p.Notes.Add("Aggregation mode: "+bond.Mode);}
            }
            void Vlan(int id,string name="")
            {if(id<1||id>4094){inv.Blockers.Add("Invalid RouterOS PVID.");return;}var v=inv.Vlans.FirstOrDefault(v=>v.Id==id);if(v==null)inv.Vlans.Add(new(){Id=id,Name=name});else if(v.Name.Length==0)v.Name=name;}
            void Native(Port p,int id)
            {if(p.UntaggedVlan.HasValue&&p.UntaggedVlan!=id)inv.Blockers.Add("Multiple untagged RouterOS VLANs on "+p.Id);p.UntaggedVlan=id;p.UntaggedState="Known";}
            foreach(var row in s.Vlans.Where(v=>!v.Disabled))
            {
                if(!bridges.TryGetValue(row.Bridge,out var bridge)){inv.Blockers.Add("Unresolved RouterOS bridge reference.");continue;}
                foreach(int id in row.Ids)Vlan(id,row.Comment);
                if(bridge.Filtering==false){inv.Assumptions.Add("Bridge VLAN filtering is disabled on "+bridge.Name+"; stored VLAN-table assignments are inactive and are not drawn.");continue;}
                foreach(string name in row.Tagged.Concat(row.Untagged))
                    if(!bridges.ContainsKey(name)&&!s.BridgePorts.Any(p=>p.Interface==name&&p.Bridge==row.Bridge))inv.Blockers.Add("RouterOS VLAN member is not assigned to the specified bridge: "+Parser.Safe(name));
                foreach(string name in row.Tagged)foreach(var p in Resolve(name))foreach(int id in row.Ids)if(!p.TaggedVlans.Contains(id))p.TaggedVlans.Add(id);
                foreach(string name in row.Untagged)foreach(var p in Resolve(name))foreach(int id in row.Ids)Native(p,id);
            }
            foreach(var bp in s.BridgePorts)
            {
                if(!bridges.TryGetValue(bp.Bridge,out var bridge)){inv.Blockers.Add("Unresolved RouterOS bridge reference.");continue;}
                if(bp.Pvid<1||bp.Pvid>4094){inv.Blockers.Add("Invalid RouterOS PVID.");continue;}
                if(bp.TagStacking)inv.Blockers.Add("RouterOS tag-stacking cannot be represented by one native VLAN fill.");
                foreach(var p in Resolve(bp.Interface))
                {
                    p.Notes.Add("Configured ingress PVID: "+bp.Pvid);p.Notes.Add("Bridge: "+bridge.Name);
                    if(bp.Disabled){p.Notes.Add("Bridge membership disabled");continue;}
                    if(bridge.Filtering==true&&bp.Frames!="admit-only-vlan-tagged"){Vlan(bp.Pvid);Native(p,bp.Pvid);}
                    if(p.UntaggedVlan.HasValue&&bp.Frames!="admit-only-vlan-tagged"&&p.UntaggedVlan!=bp.Pvid)inv.Blockers.Add("RouterOS ingress PVID and untagged egress VLAN differ on "+p.Id);
                    if(bridge.Filtering==true&&bp.Frames=="admit-only-vlan-tagged"&&!p.UntaggedVlan.HasValue)p.UntaggedState="None";
                }
            }
            foreach(var p in logical.Values){p.TaggedVlans.Sort();p.Mode=p.TaggedVlans.Count>0?"Trunk":p.UntaggedVlan.HasValue?"Access":"Unknown";if(p.UntaggedVlan.HasValue&&p.TaggedVlans.Contains(p.UntaggedVlan.Value))inv.Blockers.Add("RouterOS VLAN is both tagged and untagged on "+p.Id);}
            inv.LogicalInterfaces=JsonSerializer.Deserialize<List<Port>>(JsonSerializer.Serialize(logical.Values))!;
            for(int n=0;n<inv.LogicalInterfaces.Count;n++){inv.LogicalInterfaces[n].Number=n+1;inv.LogicalInterfaces[n].Group="Switch";}
            var g=new Group{Id="Switch",Device="1",Product=confirmed?profile.Code:"MIKROTIK-SHARED-"+profile.Code,Copper=profile.Copper,Optical=profile.Fiber+profile.Qsfp,OpticalType=profile.Qsfp>0?"SFP+ / QSFP+":"SFP+"};inv.Groups.Add(g);
            void Add(string defaultName,string label,double column,int row)
            {
                var i=s.Interfaces.Single(i=>i.DefaultName==defaultName);var p=logical[i.Key];
                p.Group=g.Id;p.Number=inv.Ports.Count+1;p.DisplayNumber=label;
                inv.Ports.Add(p);g.SocketLayout.Add(new(){Number=p.Number,Column=column,Row=row,Media=p.Media});
            }
            // Physical front views use the vendor's port ordering; console is not an Ethernet port.
            if(profile.Qsfp>0)
            {
                for(int i=1;i<=profile.Fiber;i++)Add("sfp-sfpplus"+i,i.ToString(),(i-1)/2,(i-1)%2);
                for(int q=1;q<=profile.Qsfp;q++)
                {
                    var lanes=Enumerable.Range(1,4).Select(l=>logical[s.Interfaces.Single(i=>i.DefaultName==$"qsfpplus{q}-{l}").Key]).ToList();
                    string Signature(Port p)=>$"{p.UntaggedState}:{p.UntaggedVlan}:{string.Join(',',p.TaggedVlans)}:{p.Aggregate}:{p.Disabled}";
                    if(lanes.Select(Signature).Distinct().Count()!=1)inv.Blockers.Add("QSFP+ "+q+" has different lane assignments. A lane-level physical diagram is not yet supported.");
                    var first=lanes[0];var physical=JsonSerializer.Deserialize<Port>(JsonSerializer.Serialize(first))!;
                    physical.Id="qsfpplus"+q;physical.RelatedInterfaces=lanes.Select(p=>p.Id).ToList();physical.Description=string.Join(" / ",lanes.Select(p=>p.Description).Where(x=>x.Length>0).Distinct());
                    physical.Group=g.Id;physical.Number=inv.Ports.Count+1;physical.DisplayNumber="Q"+q;inv.Ports.Add(physical);g.SocketLayout.Add(new(){Number=physical.Number,Column=12.75,Row=q-1,Media="QSFP+"});
                }
                Add("ether1","E1",14,1);
            }
            else if(profile.Copper==1)
            {Add("ether1","E1",0,0);for(int i=1;i<=profile.Fiber;i++)Add("sfp-sfpplus"+i,i.ToString(),i+1,0);}
            else
            {for(int i=1;i<=profile.Copper;i++)Add("ether"+i,i.ToString(),(i-1)/2,(i-1)%2);for(int i=1;i<=profile.Fiber;i++)Add("sfp-sfpplus"+i,"S"+i,profile.Copper/2+1+(i-1)/2,(i-1)%2);}
            // Logical records are independent from the physical cage presentation.
            inv.Vlans=inv.Vlans.OrderBy(v=>v.Id).ToList();
            inv.Assumptions.Add("MikroTik hardware and port ordering: https://mikrotik.com/product/"+(profile.Code=="CRS326-24S+2Q+RM"?"crs326_24s_2q_rm":profile.Code=="CRS328-24P-4S+RM"?"crs328_24p_4s_rm":profile.Code=="CRS317-1G-16S+RM"?"crs317_1g_16s_rm":"crs309_1g_8s_in"));
            if(!confirmed)inv.Assumptions.Add("Complete RouterOS interface signature selects verified physical geometry. Exact model identity is unconfirmed and omitted from client reports.");
            if(s.Binary)inv.Assumptions.Add("Binary import shows configured static VLAN-table assignments only. Ingress PVID and bridge membership state are retained separately. Dynamic membership, bridge-filtering state, firmware and exact model are not inferred. Use an .rsc export for full bridge semantics.");
        }
        catch(Exception ex) when(ex is InvalidOperationException or JsonException or ArgumentException)
        {inv.Blockers.Add(ex is JsonException?"Invalid RouterOS inventory data.":ex.Message);}
        return inv;
    }
    static IEnumerable<string> DefaultNames(Model m)=>Enumerable.Range(1,m.Copper).Select(n=>"ether"+n).Concat(Enumerable.Range(1,m.Fiber).Select(n=>"sfp-sfpplus"+n)).Concat(Enumerable.Range(1,m.Qsfp).SelectMany(q=>Enumerable.Range(1,4).Select(l=>$"qsfpplus{q}-{l}")));
}
