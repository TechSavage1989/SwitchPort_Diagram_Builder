using System.Text.RegularExpressions;
namespace HUBTechGenerator;

public static class ComwareParser
{
    const string Physical=@"(?:GigabitEthernet|Ten-GigabitEthernet|Twenty-FiveGigE|FortyGigE|HundredGigE|FourHundredGigE)\d+/\d+/\d+";
    public static bool IsConfiguration(string source)=>Regex.IsMatch(source,@"(?im)^\s*sysname\s+")||Regex.IsMatch(source,@"(?im)^\s*version\s+[5-9]\.\d+.*$")&&Regex.IsMatch(source,@"(?im)^\s*interface\s+"+Physical);
    sealed class Settings
    {
        public Port Port=new();
        public int? Pvid;
        public HashSet<int> Permitted=new(),Untagged=new();
        public bool HasPermitted,All,VlanConfigured;
    }
    public static Inventory Parse(string source,string? selectedModels=null)
    {
        string text=source.Replace("\r","");
        var inv=new Inventory{Vendor="HPE",OS="Comware",Topology="Fixed"};
        string Value(string pattern)=>Parser.Safe(Regex.Match(text,pattern,RegexOptions.Multiline|RegexOptions.IgnoreCase).Groups[1].Value);
        string hostname=Value(@"^\s*sysname\s+(.+)$");if(hostname.Length>0)inv.Hostname=hostname;
        string version=Value(@"^\s*version\s+(.+)$");if(version.Length>0)inv.Firmware=version;
        var declared=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(Match m in Regex.Matches(text,@"(?im)^\s*[#!;]\s*member\s+(\d+)\s+(?:model|type)\s+([A-Z0-9]+)\s*$"))declared[m.Groups[1].Value]=m.Groups[2].Value;
        HardwareCatalog.ApplySelection(inv,text,selectedModels,declared);
        var members=Regex.Matches(text,@"(?im)^\s*interface\s+"+Physical).Select(m=>Regex.Match(m.Value,@"(\d+)/\d+/\d+$").Groups[1].Value).Distinct().ToList();
        if(declared.Count==0)
        {
            var codes=HardwareCatalog.ProductCodes(text);
            if(codes.Count==1&&HardwareCatalog.Find(codes[0])?.OS=="Comware"&&members.Count<=1)declared[members.FirstOrDefault()??"1"]=codes[0];
            else inv.Blockers.Add("Comware configuration does not establish a verified model for every member. Select the exact SKU, or enter member=SKU pairs from chassis labels.");
        }
        var irf=Regex.Matches(text,@"(?im)^\s*irf\s+member\s+(\d+)\b").Select(m=>m.Groups[1].Value).Concat(members).Distinct().ToList();
        foreach(string id in irf.Where(id=>!declared.ContainsKey(id)))inv.Blockers.Add("No verified hardware model for Comware member "+id+".");
        if(declared.Count>1||Regex.IsMatch(text,@"(?im)^\s*irf-port\s+"))inv.Topology="IRF";
        foreach(var entry in declared.OrderBy(d=>int.TryParse(d.Key,out int n)?n:int.MaxValue))
        {
            var model=HardwareCatalog.Find(entry.Value);
            if(!int.TryParse(entry.Key,out int member)||member<1||member>16||model?.OS!="Comware")
            {inv.Blockers.Add("Unsupported Comware hardware/member assignment: "+entry.Key+"="+entry.Value);continue;}
            var group=new Group{Id=inv.Topology=="IRF"?"Member "+member:"Switch",Device=member.ToString(),Product=model.Sku,Prefix=member+"/0/",Copper=model.Copper,Optical=model.Optical,OpticalSide=model.Side,OpticalType=model.OpticalType,OpticalOnly=model.SingleRow,SocketLayout=model.Sockets};
            inv.Groups.Add(group);
            foreach(var socket in model.Sockets.GroupBy(s=>s.Number).Select(g=>g.First()).OrderBy(s=>s.Number))
                inv.Ports.Add(new Port{Id=socket.InterfaceKind+member+"/0/"+socket.Number,Group=group.Id,Number=socket.Number,Media=socket.Media});
        }
        if(inv.Ports.Count==0)return inv;
        foreach(var g in inv.Groups)foreach(var pair in g.SocketLayout.GroupBy(s=>s.Number).Where(s=>s.Count()>1))
            inv.Ports.Single(p=>p.Group==g.Id&&p.Number==pair.Key).Notes.Add("Dual-personality interface: RJ45 and SFP sockets share one interface; active medium is unconfirmed.");
        var states=inv.Ports.ToDictionary(p=>p.Id,p=>new Settings{Port=p},StringComparer.OrdinalIgnoreCase);
        var aggregates=new Dictionary<string,Settings>(StringComparer.OrdinalIgnoreCase);
        var vlans=new Dictionary<int,Vlan>();
        Vlan Vlan(int id){if(!vlans.TryGetValue(id,out var v)){v=new Vlan{Id=id};vlans[id]=v;inv.Vlans.Add(v);}return v;}
        List<int> VlanList(string list)
        {
            var result=new List<int>();var tokens=Regex.Split(list.Trim(),@"[\s,]+");
            for(int i=0;i<tokens.Length;i++)
            {
                if(!int.TryParse(tokens[i],out int first)||first<1||first>4094){inv.Blockers.Add("Unrecognized or out-of-range Comware VLAN list.");return new();}
                int last=first;
                if(i+1<tokens.Length&&tokens[i+1].Equals("to",StringComparison.OrdinalIgnoreCase))
                {if(i+2>=tokens.Length||!int.TryParse(tokens[i+2],out last)||last<first||last>4094){inv.Blockers.Add("Invalid Comware VLAN range.");return new();}i+=2;}
                for(int n=first;n<=last;n++){result.Add(n);Vlan(n);}
            }return result.Distinct().Order().ToList();
        }
        List<Settings> Resolve(string spec)
        {
            var match=Regex.Match(spec.Trim(),@"^("+Physical+@")(?:\s+to\s+("+Physical+@"))?$",RegexOptions.IgnoreCase);
            if(!match.Success){inv.Blockers.Add("Unsupported Comware physical interface or breakout syntax.");return new();}
            string first=match.Groups[1].Value;
            if(!match.Groups[2].Success)
            {
                if(states.TryGetValue(first,out var state))return new(){state};
                inv.Blockers.Add("Configured Comware interface is outside the selected hardware inventory: "+first);return new();
            }
            var a=Regex.Match(first,@"^(.*?/)(\d+)$");var b=Regex.Match(match.Groups[2].Value,@"^(.*?/)(\d+)$");
            if(!a.Groups[1].Value.Equals(b.Groups[1].Value,StringComparison.OrdinalIgnoreCase)||!int.TryParse(a.Groups[2].Value,out int start)||!int.TryParse(b.Groups[2].Value,out int end)||end<start||end-start>2000)
            {inv.Blockers.Add("Invalid Comware physical interface range.");return new();}
            return Enumerable.Range(start,end-start+1).SelectMany(n=>Resolve(a.Groups[1].Value+n)).ToList();
        }
        List<Settings> current=new();List<int> currentVlans=new();string svi="";bool irfContext=false;
        foreach(string raw in text.Split('\n'))
        {
            string line=raw.Trim();Match m;
            if(line.Length==0)continue;
            if(line=="#"||line.Equals("quit",StringComparison.OrdinalIgnoreCase)||line.Equals("return",StringComparison.OrdinalIgnoreCase)){current=new();currentVlans=new();svi="";irfContext=false;continue;}
            if(line.StartsWith('#')||Regex.IsMatch(line,@"(?i)\b(password|passwd|secret|community|private-key|credential|token|cipher|certificate)\b"))continue;
            if((m=Regex.Match(line,@"^vlan\s+([\d ,]+(?:to[\d ,]+)?)$",RegexOptions.IgnoreCase)).Success){currentVlans=VlanList(m.Groups[1].Value);current=new();svi="";continue;}
            if((m=Regex.Match(line,@"^interface\s+(Bridge-Aggregation|Route-Aggregation)\s*(\d+)$",RegexOptions.IgnoreCase)).Success)
            {
                string key=(m.Groups[1].Value.StartsWith("Route",StringComparison.OrdinalIgnoreCase)?"RAGG":"BAGG")+m.Groups[2].Value;
                if(!aggregates.TryGetValue(key,out var state)){state=new Settings{Port=new Port{Id=key}};aggregates[key]=state;}
                if(key.StartsWith("RAGG")){state.Port.Mode="Routed";state.Port.UntaggedState="None";}
                current=new(){state};currentVlans=new();svi="";continue;
            }
            if((m=Regex.Match(line,@"^interface\s+Vlan-interface\s*(\d+)$",RegexOptions.IgnoreCase)).Success){svi="Vlan-interface"+m.Groups[1].Value;current=new();currentVlans=new();continue;}
            if((m=Regex.Match(line,@"^interface\s+(?:range\s+)?(.+)$",RegexOptions.IgnoreCase)).Success)
            {
                currentVlans=new();svi="";irfContext=false;
                if(Regex.IsMatch(m.Groups[1].Value,@"^(?:LoopBack|NULL|M-GigabitEthernet|Tunnel|Vsi-interface)",RegexOptions.IgnoreCase)){current=new();continue;}
                current=Resolve(m.Groups[1].Value);continue;
            }
            if(Regex.IsMatch(line,@"^irf-port\s+\d+/\d+$",RegexOptions.IgnoreCase)){irfContext=true;current=new();currentVlans=new();continue;}
            if(irfContext&&(m=Regex.Match(line,@"^port\s+group\s+interface\s+(.+)$",RegexOptions.IgnoreCase)).Success)
            {foreach(var state in Resolve(m.Groups[1].Value)){state.Port.StackLink=true;state.Port.Mode="Stack/IRF";state.Port.Notes.Add("Configured IRF link; verify live adjacency.");}continue;}
            if(Regex.IsMatch(line,@"^port-group\s+",RegexOptions.IgnoreCase)){inv.Blockers.Add("Comware port-group configuration must be exported as resolved per-interface commands.");continue;}
            if(currentVlans.Count>0)
            {
                if((m=Regex.Match(line,@"^(?:name|description)\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(int id in currentVlans)Vlan(id).Name=Parser.Safe(m.Groups[1].Value);
                continue;
            }
            if(svi.Length>0&&(m=Regex.Match(line,@"^ip(?:v6)? address\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.SVIs.Add(new Svi{Interface=svi,Address=Parser.Safe(m.Groups[1].Value)});continue;}
            if((m=Regex.Match(line,@"^ip route-static\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Routes.Add(Parser.Safe(m.Groups[1].Value));continue;}
            if(current.Count==0)continue;
            if((m=Regex.Match(line,@"^description\s+(.+)$",RegexOptions.IgnoreCase)).Success){foreach(var state in current)state.Port.Description=Parser.Safe(m.Groups[1].Value);continue;}
            if(line.Equals("shutdown",StringComparison.OrdinalIgnoreCase)||line.Equals("undo shutdown",StringComparison.OrdinalIgnoreCase)){foreach(var state in current)state.Port.Disabled=!line.StartsWith("undo",StringComparison.OrdinalIgnoreCase);continue;}
            if((m=Regex.Match(line,@"^port link-type\s+(access|trunk|hybrid)$",RegexOptions.IgnoreCase)).Success){foreach(var state in current)state.Port.Mode=m.Groups[1].Value.ToUpperInvariant();continue;}
            if((m=Regex.Match(line,@"^port access vlan\s+(\d+)$",RegexOptions.IgnoreCase)).Success)
            {var ids=VlanList(m.Groups[1].Value);foreach(var state in current){state.VlanConfigured=true;state.Untagged=ids.ToHashSet();state.Port.Mode="Access";}continue;}
            if((m=Regex.Match(line,@"^port (?:trunk|hybrid) pvid vlan\s+(\d+)$",RegexOptions.IgnoreCase)).Success)
            {var ids=VlanList(m.Groups[1].Value);if(ids.Count==1)foreach(var state in current){state.Pvid=ids[0];state.VlanConfigured=true;state.Port.Notes.Add("Configured ingress PVID: "+ids[0]);}continue;}
            if((m=Regex.Match(line,@"^(undo )?port trunk permit vlan\s+(.+)$",RegexOptions.IgnoreCase)).Success)
            {
                bool remove=m.Groups[1].Success,all=m.Groups[2].Value.Equals("all",StringComparison.OrdinalIgnoreCase);var ids=all?new List<int>():VlanList(m.Groups[2].Value);
                foreach(var state in current)
                {
                    state.HasPermitted=state.VlanConfigured=true;
                    if(all){state.All=!remove;if(remove)state.Permitted.Clear();}
                    else if(remove){if(state.All)inv.Blockers.Add("Comware all-VLAN trunk with explicit exclusions cannot be rendered as a complete fixed-size dot set.");state.Permitted.ExceptWith(ids);}
                    else state.Permitted.UnionWith(ids);
                }continue;
            }
            if((m=Regex.Match(line,@"^(undo )?port hybrid vlan\s+(.+?)\s+(tagged|untagged)$",RegexOptions.IgnoreCase)).Success)
            {var ids=VlanList(m.Groups[2].Value);foreach(var state in current){state.VlanConfigured=true;var set=m.Groups[3].Value.Equals("tagged",StringComparison.OrdinalIgnoreCase)?state.Permitted:state.Untagged;if(m.Groups[1].Success)set.ExceptWith(ids);else set.UnionWith(ids);}continue;}
            if((m=Regex.Match(line,@"^undo port hybrid vlan\s+(.+)$",RegexOptions.IgnoreCase)).Success)
            {var ids=VlanList(m.Groups[1].Value);foreach(var state in current){state.VlanConfigured=true;state.Permitted.ExceptWith(ids);state.Untagged.ExceptWith(ids);}continue;}
            if((m=Regex.Match(line,@"^(?:voice-vlan|voice vlan)\s+(\d+)\s+enable$",RegexOptions.IgnoreCase)).Success)
            {var ids=VlanList(m.Groups[1].Value);if(ids.Count==1)foreach(var state in current){state.Port.VoiceVlan=ids[0];state.Port.Notes.Add("Voice VLAN configured; OUI admission/live membership is not established by the saved configuration.");}continue;}
            if((m=Regex.Match(line,@"^port link-aggregation group\s+(\d+)$",RegexOptions.IgnoreCase)).Success)
            {foreach(var state in current){if(state.Port.Aggregate.Length>0)inv.Blockers.Add("Comware physical interface assigned to more than one aggregate.");state.Port.Aggregate="BAGG"+m.Groups[1].Value;}continue;}
            if((m=Regex.Match(line,@"^link-aggregation mode\s+(dynamic|static)$",RegexOptions.IgnoreCase)).Success)
            {foreach(var state in current)state.Port.Notes.Add("Aggregation mode: "+(m.Groups[1].Value.Equals("dynamic",StringComparison.OrdinalIgnoreCase)?"LACP dynamic":"Static"));continue;}
            if((m=Regex.Match(line,@"^speed\s+(.+)$",RegexOptions.IgnoreCase)).Success){foreach(var state in current)state.Port.Speed=Parser.Safe(m.Groups[1].Value);continue;}
            if(line.Equals("port link-mode route",StringComparison.OrdinalIgnoreCase)){foreach(var state in current){state.Port.Mode="Routed";state.Port.UntaggedState="None";}continue;}
            if(Regex.IsMatch(line,@"^(?:undo )?(?:port (?:access|trunk|hybrid).*vlan|voice-vlan\s+\d|voice vlan\s+\d)",RegexOptions.IgnoreCase))inv.Blockers.Add("Unrecognized Comware VLAN syntax on a physical or aggregate interface.");
        }
        void Finish(Settings state)
        {
            var p=state.Port;
            if(state.Untagged.Count>1){inv.Blockers.Add("Multiple explicit untagged VLANs cannot be represented by one native fill on "+p.Id);return;}
            int? native=state.Untagged.Count==1?state.Untagged.Single():null;
            if(p.Mode.Equals("TRUNK",StringComparison.OrdinalIgnoreCase)&&state.Pvid.HasValue&&(state.All||state.Permitted.Contains(state.Pvid.Value)))native=state.Pvid;
            p.UntaggedVlan=native;p.UntaggedState=native.HasValue?"Known":state.HasPermitted&&state.Pvid.HasValue?"None":p.UntaggedState;
            p.TaggedVlans=state.Permitted.Where(v=>v!=native).Order().ToList();
            if(state.All)p.Notes.Add("Tagged VLANs: all allowed by configuration; actual active VLANs require confirmation.");
        }
        foreach(var a in aggregates)Finish(a.Value);
        foreach(var state in states.Values)
        {
            var p=state.Port;
            if(p.Aggregate.Length>0)
            {
                if(!aggregates.TryGetValue(p.Aggregate,out var aggregate)){inv.Blockers.Add("Comware aggregate has no explicit configuration: "+p.Aggregate);continue;}
                if(state.VlanConfigured)p.Notes.Add("Member VLAN shadow configuration replaced by the explicit aggregate configuration.");
                var q=aggregate.Port;p.UntaggedVlan=q.UntaggedVlan;p.UntaggedState=q.UntaggedState;p.TaggedVlans=q.TaggedVlans.ToList();p.VoiceVlan=q.VoiceVlan;p.Mode=q.Mode;p.Notes.AddRange(q.Notes);
                if(q.Description.Length>0)p.Notes.Add("Aggregate description: "+q.Description);
                if(p.StackLink)inv.Blockers.Add("Comware interface is both an IRF link and an Ethernet aggregate member: "+p.Id);
            }
            else Finish(state);
        }
        foreach(var a in aggregates.Keys)if(!inv.Ports.Any(p=>p.Aggregate==a))inv.Blockers.Add("Comware aggregate has no physical members: "+a);
        inv.Assumptions.Add("Comware VLAN, voice, bridge-aggregation and configured IRF fields are parsed from saved commands. Omitted defaults, dynamic admission, live links and connected devices remain unconfirmed.");
        inv.Assumptions.Add("Hardware positions and media follow the selected source-referenced catalogue profile. Optical capability does not identify an installed transceiver.");
        inv.Blockers=inv.Blockers.Distinct().ToList();return inv;
    }
}
