using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public static class CiscoParser
{
    const string Physical=@"(?:FastEthernet|GigabitEthernet|TenGigabitEthernet|TwentyFiveGigabitEthernet|TwentyFiveGigE|FortyGigabitEthernet|HundredGigE)\d+(?:/\d+){0,2}(?::\d+)?(?:\.\d+)?";
    const string Secret=@"(?i)\b(?:password|passwd|secret|community|private-key|credential|token|cipher|certificate|key-string)\b";
    public static bool IsConfiguration(string source)=>Regex.IsMatch(source,@"(?im)^version (?:12|15|16|17)\.\d+")&&Regex.IsMatch(source,@"(?im)^hostname\s+\S+")&&Regex.IsMatch(source,@"(?im)^(?:interface\s+(?:"+Physical+@"|Port-channel\d+)|switch\s+\d+\s+provision\s+)");
    sealed class Settings
    {
        public Port Port=new();
        public string Mode="",ChannelMode="";
        public int? Access,Native,Voice;
        public HashSet<int> Allowed=new();
        public bool AllowedSet,All=true,Routed,Meaningful;
    }
    public static string EvidenceText(string source)
    {
        var lines=new List<string>();string delimiter="";bool certificate=false;
        foreach(string raw in source.Split('\n'))
        {
            string line=raw.Trim();
            if(certificate){if(line.Equals("quit",StringComparison.OrdinalIgnoreCase))certificate=false;continue;}
            if(delimiter.Length>0){if(line.Contains(delimiter,StringComparison.Ordinal))delimiter="";continue;}
            if(Regex.IsMatch(line,@"^crypto pki certificate chain\b",RegexOptions.IgnoreCase)){certificate=true;continue;}
            var banner=Regex.Match(line,@"^banner\s+\S+\s+(\^.|.)(.*)$",RegexOptions.IgnoreCase);
            if(banner.Success){delimiter=banner.Groups[2].Value.Contains(banner.Groups[1].Value,StringComparison.Ordinal)?"":banner.Groups[1].Value;continue;}
            lines.Add(raw);
        }
        return string.Join('\n',lines);
    }
    public static Inventory Parse(string source,string? selectedModels=null)
    {
        string text=EvidenceText(source.Replace("\r",""));
        var inv=new Inventory{Vendor="Cisco",OS=Regex.IsMatch(text,@"(?m)^version (?:16|17)\.")?"Cisco IOS XE":"Cisco IOS",Topology="Fixed"};
        inv.Hostname=Parser.Safe(Regex.Match(text,@"(?m)^hostname\s+(.+)$").Groups[1].Value);
        inv.Firmware=Parser.Safe(Regex.Match(text,@"(?m)^version\s+(.+)$").Groups[1].Value);
        var declared=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(Match m in Regex.Matches(text,@"(?im)^switch\s+(\d+)\s+provision\s+([\w-]+)\s*$"))
        {
            string id=m.Groups[1].Value,sku=CiscoHardware.CanonicalModel(m.Groups[2].Value);
            if(declared.TryGetValue(id,out var old)&&old!=sku)inv.Blockers.Add("Conflicting Cisco switch provisioning for member "+id+".");
            declared[id]=sku;
        }
        var exactIdentities=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        void ExactIdentity(string id,string sku)
        {
            if(exactIdentities.TryGetValue(id,out var previous)&&previous!=sku)
            {inv.Blockers.Add("Conflicting exact Cisco product identities for member "+id+".");return;}
            exactIdentities[id]=sku;
            if(declared.TryGetValue(id,out var provision)&&provision!=sku&&!CiscoHardware.CompatibleProvision(provision,sku))
                inv.Blockers.Add("Show inventory chassis PID conflicts with configured Cisco member "+id+".");
            else declared[id]=sku;
        }
        // Only trusted identity lines can supply a PID. Hostnames, descriptions and
        // boot filenames are not evidence of installed switch or module hardware.
        var pids=Regex.Matches(text,@"(?im)^\s*(?:[!#;]\s*(?:PID:|model\s+)|license udi pid\s+)([\w-]+)").Select(m=>CiscoHardware.CanonicalModel(m.Groups[1].Value)).Where(sku=>!CiscoModuleCatalog.IsNetworkModulePid(sku)).Distinct().ToList();
        if(pids.Count==1&&declared.Count<=1)ExactIdentity(declared.Keys.FirstOrDefault()??"1",pids[0]);
        else if(pids.Count==1&&declared.Count>1)inv.Assumptions.Add("Unassigned Cisco product PID cannot identify a stack member; member provisioning and assigned show inventory records remain authoritative.");
        if(pids.Count>1)inv.Blockers.Add("Multiple Cisco product identities need explicit member assignments.");
        foreach(Match record in Regex.Matches(text,@"(?im)^[ \t]*NAME:\s*""([^""]+)""[^\n]*\n[ \t]*PID:\s*((?:WS-C(?:3650|3560E|3560X|3750X|3850)-|C9(?:200|300|500)(?:X|L|LM)?-)[\w-]+)\s*,"))
        {
            string sku=CiscoHardware.CanonicalModel(record.Groups[2].Value);
            if(CiscoModuleCatalog.IsNetworkModulePid(sku))continue;
            var member=Regex.Match(record.Groups[1].Value,@"^(?:Switch|Slot)\s+(\d+)\b",RegexOptions.IgnoreCase);
            string id=member.Success?member.Groups[1].Value:"1";
            ExactIdentity(id,sku);
        }
        foreach(string token in (selectedModels??"").Split(',',StringSplitOptions.RemoveEmptyEntries))
        {
            var m=Regex.Match(token.Trim(),@"^(?:(\d+)=)?([\w-]+)$");
            string id=m.Groups[1].Success?m.Groups[1].Value:"1",sku=CiscoHardware.CanonicalModel(m.Groups[2].Value);
            if(!m.Success||CiscoHardware.Find(sku)==null){inv.Blockers.Add("Cisco hardware model has no verified port layout: "+Parser.Safe(token));continue;}
            if(declared.TryGetValue(id,out var old)&&old!=sku&&(exactIdentities.ContainsKey(id)||!CiscoHardware.CompatibleProvision(old,sku)))
                inv.Blockers.Add("Selected hardware conflicts with configured Cisco member "+id+".");
            else
            {
                if(old!=null&&old!=sku)inv.Assumptions.Add("Operator exact model "+sku+" refines provisioning "+old+" for member "+id+". The documented port layout is identical; installed power supply and PoE budget are not inferred.");
                declared[id]=sku;
            }
        }
        var names=Regex.Matches(text,@"(?im)^interface\s+("+Physical+@")\s*$").Select(m=>m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dataNames=names.Where(n=>!IsManagement(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(declared.Count==0)
        {
            // A complete, exact native C9500 Y4C signature determines shared socket
            // geometry. No inference from the last configured port or a hostname.
            foreach(var model in CiscoHardware.Models.Where(m=>m.Code.StartsWith("C9500-")))
                if(dataNames.SetEquals(model.Sockets.Select(s=>CiscoHardware.Interface(s,"1"))))
                {declared["1"]=model.Code;inv.Assumptions.Add("Cisco "+model.Code+" socket layout matched from the complete native interface inventory. License bundle, installed optics and live speeds remain unconfirmed.");break;}
            if(declared.Count==0&&CiscoHardware.Complete3560E48Signature(text,dataNames,names))
            {declared["1"]=CiscoHardware.Shared3560E48Profile;inv.Assumptions.Add("Complete IOS 15 / FastEthernet0 / Gi0/1-52 / Te0/1-2 signature matches the 48-port 3560-E shared base layout. Exact SKU and PoE are unconfirmed unless supplied separately; software uplink entries do not confirm installed sockets.");}
        }
        if(declared.Count==0)inv.Blockers.Add("Cisco backup does not establish a verified physical model and uplink module. Supply show inventory (switch PID and installed uplink/converter PIDs); hostnames and unused interface stubs do not confirm installed sockets.");
        if(declared.Count>1)inv.Topology="StackWise";
        bool tagNative=Regex.IsMatch(text,@"(?im)^vlan dot1q tag native\s*$");
        var states=new Dictionary<string,Settings>(StringComparer.OrdinalIgnoreCase);
        var aliases=new Dictionary<string,(Settings State,string Name)>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in declared.OrderBy(d=>int.TryParse(d.Key,out int n)?n:int.MaxValue))
        {
            var model=CiscoHardware.Find(entry.Value);
            if(model==null||!int.TryParse(entry.Key,out int member)||member<1||member>9){inv.Blockers.Add("Unsupported Cisco member/model assignment: "+entry.Key+"="+entry.Value);continue;}
            if(CiscoModuleCatalog.IsStandalone(model.Code)&&(declared.Count>1||member!=1))
            {inv.Blockers.Add("This Catalyst model is standalone; its interfaces cannot be assigned to multiple members. Import each switch separately.");continue;}
            if(model.Code.StartsWith("C9500-")&&declared.Count>1&&!Regex.IsMatch(text,@"(?im)^stackwise-virtual\b"))inv.Blockers.Add("Multiple C9500 members require explicit StackWise Virtual configuration.");
            foreach(var section in model.Sockets.GroupBy(s=>s.ConfiguredInterface))
            {
                bool configured=section.Key;var sockets=section.Where(s=>!configured||CiscoHardware.Is3560E48(model.Code)||CiscoHardware.Names(s,entry.Key).Any(names.Contains)).ToList();if(sockets.Count==0)continue;
                var group=new Group{Id=configured?(declared.Count>1?"Member "+member+" / ":"")+"Uplink interfaces - module unconfirmed":declared.Count>1?"Member "+member:"Switch",Device=entry.Key,Product=configured?"":model.Code,ConfiguredInterfaces=configured,Copper=sockets.Count(s=>s.Media=="RJ-45"),Optical=sockets.Count(s=>s.Media!="RJ-45"),OpticalType=configured?"Unspecified":string.Join(" / ",sockets.Where(s=>s.Media!="RJ-45").Select(s=>s.Media).Distinct())};
                for(int index=0;index<sockets.Count;index++)
                {
                    var socket=sockets[index];int number=index+1;string name=CiscoHardware.Interface(socket,entry.Key);
                    var p=new Port{Id=name,Group=group.Id,Number=number,DisplayNumber=socket.Label,Media=socket.Media,RelatedInterfaces=CiscoHardware.Names(socket,entry.Key).Skip(1).ToList()};
                    if(configured)p.Notes.Add("Configured uplink interface only; installed uplink module and active alternative are unconfirmed.");
                    var state=new Settings{Port=p};states[name]=state;
                    foreach(string alias in CiscoHardware.Names(socket,entry.Key))aliases[alias]=(state,alias);
                    group.SocketLayout.Add(new(){Number=number,Column=socket.Column,Row=socket.Row,Media=socket.Media});inv.Ports.Add(p);
                }
                inv.Groups.Add(group);
            }
            inv.Assumptions.Add("Cisco base layout reference: "+model.Code+". Hardware reference: "+model.Source);
            if(CiscoHardware.Is3560E48(model.Code))inv.Assumptions.Add("The 3560-E uplink table preserves six independent software interface settings, not six installed sockets. Top bay: Te0/1 OR Gi0/49 and Gi0/50. Bottom bay: Te0/2 OR Gi0/51 and Gi0/52. Installed X2/TwinGig modules and active alternatives are unconfirmed; stale settings are not merged or discarded.");
        }
        if(inv.Groups.Count==0)
        {
            // Preserve configured interfaces for diagnosis without fabricating a
            // physical inventory from software stubs or unknown converter bays.
            foreach(string name in dataNames.Order(StringComparer.OrdinalIgnoreCase))
            {
                var state=new Settings{Port=new(){Id=name,Media="Unconfirmed"}};
                states[name]=state;aliases[name]=(state,name);inv.LogicalInterfaces.Add(state.Port);
            }
        }
        var aggregates=new Dictionary<string,Settings>(StringComparer.OrdinalIgnoreCase);
        var vlans=new SortedDictionary<int,Vlan>();
        Vlan Vlan(int id){if(!vlans.TryGetValue(id,out var v)){v=new(){Id=id};vlans[id]=v;}return v;}
        List<int> VlanList(string value)
        {
            var result=new List<int>();
            foreach(string part in value.Split(','))
            {
                var m=Regex.Match(part.Trim(),@"^(\d+)(?:\s*-\s*(\d+))?$");
                if(!m.Success||!int.TryParse(m.Groups[1].Value,out int first)||first<1||first>4094){inv.Blockers.Add("Unrecognized or out-of-range Cisco VLAN list.");return new();}
                int last=first;if(m.Groups[2].Success&&(!int.TryParse(m.Groups[2].Value,out last)||last<first||last>4094)){inv.Blockers.Add("Invalid Cisco VLAN range.");return new();}
                result.AddRange(Enumerable.Range(first,last-first+1));
            }return result.Distinct().Order().ToList();
        }
        var configuredAliases=new Dictionary<Settings,HashSet<string>>();
        foreach(Match block in Regex.Matches(text,@"(?m)^interface ([^\n]+)\n((?:[ \t]+[^\n]*(?:\n|$))*)"))
        {
            string name=block.Groups[1].Value.Trim();
            if(!aliases.TryGetValue(name,out var alias)||!Regex.IsMatch(block.Groups[2].Value,@"(?im)^\s*(?:switchport\b|no switchport\b|channel-group\b|ip address\b)"))continue;
            if(!configuredAliases.TryGetValue(alias.State,out var configured))configuredAliases[alias.State]=configured=new(StringComparer.OrdinalIgnoreCase);
            configured.Add(name);
            if(configured.Count>1)inv.Blockers.Add("Cisco uplink has multiple configured interface aliases. Confirm its active interface: "+string.Join(", ",configured.Order(StringComparer.OrdinalIgnoreCase)));
            else if(!alias.State.Port.Id.Equals(name,StringComparison.OrdinalIgnoreCase))
            {var p=alias.State.Port;p.RelatedInterfaces.Add(p.Id);p.RelatedInterfaces.RemoveAll(n=>n.Equals(name,StringComparison.OrdinalIgnoreCase));p.Id=name;}
        }
        var context=new List<Settings>();List<int> currentVlans=new();string svi="",bannerDelimiter="";bool inCertificate=false;
        foreach(string raw in text.Split('\n'))
        {
            string line=raw.Trim();Match m;
            if(inCertificate){if(line.Equals("quit",StringComparison.OrdinalIgnoreCase))inCertificate=false;continue;}
            if(line.StartsWith("crypto pki certificate chain",StringComparison.OrdinalIgnoreCase)){context.Clear();currentVlans.Clear();svi="";inCertificate=true;continue;}
            if(bannerDelimiter.Length>0){if(line.Contains(bannerDelimiter,StringComparison.Ordinal))bannerDelimiter="";continue;}
            if((m=Regex.Match(line,@"^banner\s+\S+\s+(\^.|.)(.*)$",RegexOptions.IgnoreCase)).Success)
            {bannerDelimiter=m.Groups[2].Value.Contains(m.Groups[1].Value,StringComparison.Ordinal)?"":m.Groups[1].Value;context.Clear();currentVlans.Clear();svi="";continue;}
            if(line.Length==0||line.StartsWith('!')){context.Clear();currentVlans.Clear();svi="";continue;}
            if(Regex.IsMatch(line,Secret))continue;
            if(!char.IsWhiteSpace(raw[0])){context.Clear();currentVlans.Clear();svi="";}
            if((m=Regex.Match(line,@"^vlan\s+([\d, -]+)$",RegexOptions.IgnoreCase)).Success){currentVlans=VlanList(m.Groups[1].Value);foreach(int id in currentVlans)Vlan(id);continue;}
            if((m=Regex.Match(line,@"^interface\s+Port-channel(\d+)$",RegexOptions.IgnoreCase)).Success)
            {
                string id="Po"+m.Groups[1].Value;if(!aggregates.TryGetValue(id,out var state)){state=new(){Port=new(){Id=id}};aggregates[id]=state;}context.Add(state);continue;
            }
            if((m=Regex.Match(line,@"^interface\s+Vlan(\d+)$",RegexOptions.IgnoreCase)).Success){svi="Vlan"+m.Groups[1].Value;continue;}
            if((m=Regex.Match(line,@"^interface\s+(?:range\s+)?(.+)$",RegexOptions.IgnoreCase)).Success)
            {
                string spec=m.Groups[1].Value;
                if(IsManagement(spec)||Regex.IsMatch(spec,@"^(?:Loopback|Tunnel|Null|NVI|AppGigabitEthernet|Bluetooth)",RegexOptions.IgnoreCase))continue;
                if(!Regex.IsMatch(spec,@"^"+Physical+@"$",RegexOptions.IgnoreCase)) {inv.Blockers.Add("Unsupported Cisco interface range, breakout or physical syntax: "+Parser.Safe(spec));continue;}
                if(aliases.TryGetValue(spec,out var alias))
                {
                    context.Add(alias.State);
                }
                else if(inv.Groups.Count>0)inv.Blockers.Add("Cisco interface is outside the verified hardware inventory: "+spec);
                continue;
            }
            if(currentVlans.Count>0){if((m=Regex.Match(line,@"^name\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(int id in currentVlans)Vlan(id).Name=Parser.Safe(m.Groups[1].Value);continue;}
            if(svi.Length>0){if((m=Regex.Match(line,@"^ip(?:v6)? address\s+(.+)$",RegexOptions.IgnoreCase)).Success)inv.SVIs.Add(new(){Interface=svi,Address=Parser.Safe(m.Groups[1].Value)});continue;}
            if((m=Regex.Match(line,@"^ip route\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Routes.Add(Parser.Safe(m.Groups[1].Value));continue;}
            if((m=Regex.Match(line,@"^ip default-gateway\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Management.Add("Default gateway: "+Parser.Safe(m.Groups[1].Value));continue;}
            if(line.StartsWith("monitor session ",StringComparison.OrdinalIgnoreCase)){inv.Mirrors.Add(Parser.Safe(line));continue;}
            if(context.Count==0)continue;
            foreach(var state in context)
            {
                var p=state.Port;
                if((m=Regex.Match(line,@"^description\s+(.+)$",RegexOptions.IgnoreCase)).Success){p.Description=Parser.Safe(m.Groups[1].Value);continue;}
                if(line.Equals("shutdown",StringComparison.OrdinalIgnoreCase)||line.Equals("no shutdown",StringComparison.OrdinalIgnoreCase)){p.Disabled=line.StartsWith("shutdown",StringComparison.OrdinalIgnoreCase);continue;}
                if((m=Regex.Match(line,@"^speed\s+(.+)$",RegexOptions.IgnoreCase)).Success){p.Speed=Parser.Safe(m.Groups[1].Value);continue;}
                if((m=Regex.Match(line,@"^ip(?:v6)? address\s+(.+)$",RegexOptions.IgnoreCase)).Success){p.Notes.Add("Address: "+Parser.Safe(m.Groups[1].Value));continue;}
                if((m=Regex.Match(line,@"^channel-group\s+(\d+)\s+mode\s+(active|passive|on|auto|desirable)$",RegexOptions.IgnoreCase)).Success)
                {string id="Po"+m.Groups[1].Value;if(p.Aggregate.Length>0&&p.Aggregate!=id)inv.Blockers.Add("Cisco port belongs to more than one port channel: "+p.Id);p.Aggregate=id;state.ChannelMode=m.Groups[2].Value.ToLowerInvariant();state.Meaningful=true;continue;}
                if(line.Equals("no switchport",StringComparison.OrdinalIgnoreCase)){state.Routed=true;state.Meaningful=true;continue;}
                if(line.Equals("switchport",StringComparison.OrdinalIgnoreCase)){state.Routed=false;state.Meaningful=true;continue;}
                if(line.Equals("switchport trunk encapsulation dot1q",StringComparison.OrdinalIgnoreCase))continue;
                if((m=Regex.Match(line,@"^switchport mode (access|trunk|dynamic auto|dynamic desirable)$",RegexOptions.IgnoreCase)).Success){state.Mode=m.Groups[1].Value.ToLowerInvariant();state.Routed=false;state.Meaningful=true;continue;}
                if((m=Regex.Match(line,@"^switchport (access|trunk native|voice) vlan (\d+)$",RegexOptions.IgnoreCase)).Success)
                {
                    var ids=VlanList(m.Groups[2].Value);if(ids.Count!=1)continue;int id=ids[0];Vlan(id);
                    if(m.Groups[1].Value.Equals("access",StringComparison.OrdinalIgnoreCase))state.Access=id;
                    else if(m.Groups[1].Value.Equals("voice",StringComparison.OrdinalIgnoreCase)){state.Voice=id;Vlan(id).Voice=true;}
                    else state.Native=id;state.Meaningful=true;continue;
                }
                if((m=Regex.Match(line,@"^switchport trunk allowed vlan\s+(?:(add|remove|except)\s+)?(.+)$",RegexOptions.IgnoreCase)).Success)
                {
                    string operation=m.Groups[1].Value.ToLowerInvariant(),value=m.Groups[2].Value;
                    bool all=value.Equals("all",StringComparison.OrdinalIgnoreCase),none=value.Equals("none",StringComparison.OrdinalIgnoreCase);
                    if(operation.Length>0&&(all||none)){inv.Blockers.Add("Unsupported Cisco allowed-VLAN operation with all/none on "+p.Id+".");continue;}
                    var ids=all||none?new List<int>():VlanList(value);
                    if(operation=="except"){state.All=true;state.Allowed=ids.ToHashSet();}
                    else if(operation=="remove"){if(state.All)state.Allowed.UnionWith(ids);else state.Allowed.ExceptWith(ids);}
                    else if(operation=="add"){if(state.All)state.Allowed.ExceptWith(ids);else state.Allowed.UnionWith(ids);}
                    else {state.All=all;state.Allowed=ids.ToHashSet();}
                    foreach(int id in ids)if(operation is not ("remove" or "except"))Vlan(id);
                    state.AllowedSet=true;state.Meaningful=true;continue;
                }
                if(Regex.IsMatch(line,@"^(?:no )?switchport (?:access|trunk (?:allowed|native|encapsulation)|voice|mode|private-vlan)|^channel-group\b|^encapsulation\b",RegexOptions.IgnoreCase))inv.Blockers.Add("Unsupported Cisco VLAN, aggregation or encapsulation command on "+p.Id+".");
            }
        }
        void Finish(Settings state)
        {
            var p=state.Port;
            if(state.Routed){p.Mode="Routed";p.UntaggedState="None";return;}
            if(state.Mode=="trunk")
            {
                p.Mode="Trunk";int native=state.Native??1;Vlan(native);
                bool Allowed(int id)=>state.All?!state.Allowed.Contains(id):state.Allowed.Contains(id);
                if(!tagNative&&Allowed(native)){p.UntaggedVlan=native;p.UntaggedState="Known";}else p.UntaggedState="None";
                p.TaggedVlans=(state.All?vlans.Keys.Where(Allowed):state.Allowed).Where(id=>tagNative||id!=native).Order().ToList();
                if(state.All)p.TaggedVlanPolicy="All"+(state.Allowed.Count>0?" except "+string.Join(',',state.Allowed.Order()):"")+(tagNative?" (native tagged)":"; native VLAN "+native+" is untagged if allowed");
                if(state.All)p.Notes.Add("Tagged VLANs: all allowed by configuration"+(state.Allowed.Count>0?" except "+string.Join(',',state.Allowed.Order()):"")+"; diagram markers show VLANs identified in this backup; actual active VLANs require confirmation.");
                if(!state.Native.HasValue)p.Notes.Add("Native VLAN 1 is the documented IOS trunk default.");
            }
            else if(state.Mode=="access"||state.Access.HasValue)
            {
                p.Mode=state.Mode=="access"?"Access":"Dynamic (configured access VLAN; verify operational mode)";
                p.UntaggedVlan=state.Access??1;p.UntaggedState="Known";Vlan(p.UntaggedVlan.Value);
                if(state.Mode!="access")p.Notes.Add("Static access mode is not configured. DTP negotiated mode is not established by this backup.");
            }
            else if(state.Mode.StartsWith("dynamic")){p.Mode=state.Mode;p.Notes.Add("DTP operational mode and VLAN membership are unconfirmed.");}
            if(state.Voice.HasValue&&state.Mode!="trunk"){p.VoiceVlan=state.Voice;if(!p.TaggedVlans.Contains(state.Voice.Value)&&state.Voice!=p.UntaggedVlan)p.TaggedVlans.Add(state.Voice.Value);}
            else if(state.Voice.HasValue)p.Notes.Add("Configured access-mode voice VLAN is not applied to a static trunk; trunk allowed membership is authoritative.");
            if(state.AllowedSet&&state.Mode!="trunk")p.Notes.Add("Configured trunk allowed VLANs are inactive or unconfirmed without static trunk mode.");
        }
        // Complete the VLAN universe before all/except trunks are expanded.
        foreach(var state in states.Values.Concat(aggregates.Values)){if(state.Mode=="trunk")Vlan(state.Native??1);if(state.Mode=="access")Vlan(state.Access??1);}
        foreach(var state in states.Values)
        {
            var p=state.Port;
            if(p.Aggregate.Length>0)
            {
                if(!aggregates.TryGetValue(p.Aggregate,out var aggregate)){inv.Blockers.Add("Cisco port channel has no explicit configuration: "+p.Aggregate);Finish(state);continue;}
                // The channel mode may be established on members while the channel
                // contains only its allowed list. Merge only explicitly supplied fields.
                var effective=new Settings{Port=p,Mode=aggregate.Mode.Length>0?aggregate.Mode:state.Mode,Access=aggregate.Access??state.Access,Native=aggregate.Native??state.Native,Voice=aggregate.Voice??state.Voice,Routed=aggregate.Routed||state.Routed,All=aggregate.AllowedSet?aggregate.All:state.All,Allowed=aggregate.AllowedSet?aggregate.Allowed:state.Allowed,AllowedSet=aggregate.AllowedSet||state.AllowedSet};
                Finish(effective);p.Notes.Add("Aggregation mode: "+(state.ChannelMode is "active" or "passive"?"LACP":state.ChannelMode is "auto" or "desirable"?"PAgP":"static")+" ("+state.ChannelMode+")");
                p.Notes.Add("Port-channel configuration applied from "+p.Aggregate+".");
                if(p.Description.Length==0)p.Description=aggregate.Port.Description;
            }
            else Finish(state);
            p.TaggedVlans=p.TaggedVlans.Distinct().Order().ToList();
        }
        foreach(var pair in aggregates)
        {Finish(pair.Value);inv.LogicalInterfaces.Add(pair.Value.Port);if(!inv.Ports.Any(p=>p.Aggregate==pair.Key))inv.Assumptions.Add("Configured "+pair.Key+" has no declared physical members; retained as a logical interface only.");}
        inv.Vlans=vlans.Values.ToList();
        inv.Assumptions.Add("Saved Cisco configuration describes intended settings, not live links, DTP negotiation, installed transceivers or forwarding state. Unconfigured ports remain unknown. Management, loopback and SVI interfaces are not front-panel data sockets.");
        CiscoModuleCatalog.ApplyDetected(inv,text);
        if(CiscoModuleCatalog.HasUnconfirmedModules(inv)&&!inv.Groups.Any(g=>g.ConfiguredInterfaces))inv.Assumptions.Add("Optional uplink module inventory is unconfirmed. No additional sockets are inferred from missing or unused software interface stubs.");
        inv.Blockers=inv.Blockers.Distinct().ToList();return inv;
    }
    static bool IsManagement(string name)=>Regex.IsMatch(name,@"^(?:GigabitEthernet0/0|FastEthernet0)$",RegexOptions.IgnoreCase);
}
