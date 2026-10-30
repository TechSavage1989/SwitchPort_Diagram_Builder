using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public static class InstantOnParser
{
    static readonly Dictionary<string,int> Models=new(StringComparer.OrdinalIgnoreCase)
    {
        ["JL680A"]=8,["JL681A"]=8,["JL682A"]=24,["JL683A"]=24,["JL683B"]=24,
        ["JL684A"]=24,["JL684B"]=24,["JL685A"]=48,["JL686A"]=48,["JL686B"]=48
    };
    static List<HardwareSocket> Layout1930(int copper)
    {
        var sockets=Enumerable.Range(1,copper).Select(n=>new HardwareSocket{Number=n,Column=copper==8?n-1+.5*((n-1)/4):(copper==24?13:0)+(n-1)/2+.5*((n-1)/12),Row=copper==8?0:(n-1)%2,Media="RJ-45"}).ToList();
        double optical=copper==8?9.25:26.25;
        for(int n=1;n<=(copper==8?2:4);n++)sockets.Add(new(){Number=copper+n,Column=optical+(copper==8?n-1:(n-1)/2),Row=copper==8?0:(n-1)%2,Media=copper==8?"SFP":"SFP+"});
        return sockets;
    }
    public static Inventory Parse(string source,string? selectedModels=null)
    {
        string text=source.Replace("\r","");
        var inv=new Inventory{Vendor="HPE Networking Instant On",OS="Instant On 1930",Topology="Fixed"};
        var version=Regex.Match(text,@"(?im)^vInstantOn_(\d+)_([\d.]+)");
        if(!version.Success||version.Groups[1].Value is not ("1930" or "1960"))
        {inv.Blockers.Add("Unsupported Instant On family. This parser supports validated 1930/1960 configurations only.");return inv;}
        bool series1960=version.Groups[1].Value=="1960";string family=version.Groups[1].Value;inv.OS="Instant On "+family;
        inv.Firmware=version.Groups[2].Value;
        string skuLine=Regex.Match(text,@"(?im)^SKU Description\s+(.+)$").Groups[1].Value;
        var skus=Regex.Matches(skuLine,@"\bJL\d{3}[AB]\b",RegexOptions.IgnoreCase).Select(m=>m.Value.ToUpperInvariant()).Distinct().ToList();
        int copper=0;
        if(skus.Count!=1||(series1960?HardwareCatalog.Find(skus[0])?.OS!="Instant On 1960":!Models.TryGetValue(skus[0],out copper)))
        {inv.Blockers.Add("No supported Instant On "+family+" SKU could be established from SKU Description.");return inv;}
        string sku=skus[0];var baseProfile=series1960?HardwareCatalog.Find(sku):null;
        if(series1960)copper=baseProfile!.Copper;
        int optical=series1960?baseProfile!.Optical:copper==8?2:4;
        var selected=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        HardwareCatalog.ApplySelection(inv,text,selectedModels,selected);
        if(!series1960&&selected.Count>0&&(selected.Count!=1||!selected.TryGetValue("1",out var selected1930)||selected1930!=sku))inv.Blockers.Add("Instant On 1930 hardware selection must match the standalone configured SKU.");
        if(selected.Count>0&&(!series1960||selected.TryGetValue("1",out var lead)&&lead!=sku)&&selected.Values.Any(value=>value!=sku))inv.Blockers.Add("Selected hardware conflicts with the Instant On SKU Description.");
        inv.ChassisModel="Instant On "+family+" "+sku;
        var units=Regex.Matches(text,@"(?im)^unit-type unit\s+(\d+)\s+network\s+(\w+)\s+uplink\s+(\w+)\s*$");
        var memberIds=new List<int>{1};bool qualified=series1960&&Regex.IsMatch(text,@"(?im)^interface\s+\d+/\d+");
        if(series1960&&units.Count>0)
        {
            memberIds=units.Select(m=>int.Parse(m.Groups[1].Value)).Distinct().Order().ToList();
            if(memberIds.Any(id=>id<1||id>4)||sku=="JL805A"&&selected.Count==0&&units.Any(m=>!m.Groups[2].Value.Equals("te",StringComparison.OrdinalIgnoreCase)||!m.Groups[3].Value.Equals("none",StringComparison.OrdinalIgnoreCase)))
                inv.Blockers.Add("1960 member hardware signature is not validated for the JL805A profile. Mixed/ambiguous stacks require per-member hardware identities.");
        }
        if(series1960&&selected.Count>0)
        {
            if(selected.Keys.Any(k=>!int.TryParse(k,out int id)||id<1||id>4)||selected.Values.Any(s=>HardwareCatalog.Find(s)?.OS!="Instant On 1960"))inv.Blockers.Add("Invalid selected Instant On 1960 member inventory.");
            else if(memberIds.Any(id=>!selected.ContainsKey(id.ToString())))inv.Blockers.Add("Select an exact SKU for every Instant On stack member.");
            else memberIds=selected.Keys.Select(int.Parse).Order().ToList();
        }
        if(series1960&&sku!="JL805A"&&memberIds.Count>1&&selected.Count==0)inv.Blockers.Add("The lead SKU does not identify every 1960 member. Enter member=SKU pairs from chassis labels.");
        if(series1960&&memberIds.Count>1&&!qualified)inv.Blockers.Add("Multiple 1960 members require qualified member/port interface identities.");
        if(memberIds.Count>1)inv.Topology="Stack (declared units)";
        foreach(int memberId in memberIds)
        {
            string memberSku=selected.GetValueOrDefault(memberId.ToString(),sku);
            var profile=series1960?HardwareCatalog.Find(memberSku):null;
            if(series1960&&profile?.OS!="Instant On 1960")continue;
            int memberCopper=profile?.Copper??copper,memberOptical=profile?.Optical??optical;
            string prefix=qualified?memberId+"/":"";
            var group=new Group{Id=qualified?"Member "+memberId:"Switch",Device=memberId.ToString(),Product=memberSku,Prefix=prefix,Copper=memberCopper,Optical=memberOptical,OpticalType=profile?.OpticalType??(copper==8?"SFP":"SFP+"),OpticalSide="right",OpticalOnly=copper==8||series1960&&memberSku=="JL805A",BlankLeft=series1960&&(memberSku=="JL805A"||memberCopper==26),SocketLayout=series1960?profile!.Sockets:Layout1930(memberCopper)};
            inv.Groups.Add(group);
            for(int n=1;n<=group.Expected;n++)inv.Ports.Add(new Port{Id=prefix+n,Group=group.Id,Number=n,Media=n<=memberCopper?"RJ-45":group.OpticalType});
        }
        var native=inv.Ports.ToDictionary(p=>p.Id,p=>new HashSet<int>());
        var trunks=new Dictionary<string,Port>(StringComparer.OrdinalIgnoreCase);
        var pvids=new Dictionary<string,int>();List<Port> current=new();int? vlan=null,voiceId=null;string voiceState="";bool database=false;
        Vlan GetVlan(int id)
        {var v=inv.Vlans.FirstOrDefault(v=>v.Id==id);if(v==null){v=new Vlan{Id=id};inv.Vlans.Add(v);}return v;}
        List<int> VlanList(string value)
        {
            var ids=new List<int>();
            foreach(string token in value.Split(',',StringSplitOptions.RemoveEmptyEntries))
            {
                var range=Regex.Match(token.Trim(),@"^(\d+)(?:-(\d+))?$");
                if(!range.Success||!int.TryParse(range.Groups[1].Value,out int first)||first<1||first>4094)
                {inv.Blockers.Add("Invalid Instant On VLAN ID/range.");continue;}
                int last=first;
                if(range.Groups[2].Success&&(!int.TryParse(range.Groups[2].Value,out last)||last<first||last>4094))
                {inv.Blockers.Add("Invalid Instant On VLAN ID/range.");continue;}
                ids.AddRange(Enumerable.Range(first,last-first+1));
            }
            return ids.Distinct().Order().ToList();
        }
        List<Port> Resolve(string spec)
        {
            var ports=new List<Port>();
            foreach(string token in spec.Replace(" ","").Split(',',StringSplitOptions.RemoveEmptyEntries))
            {
                var m=Regex.Match(token,@"^(?:(\d+)/)?(\d+)(?:-(?:(\d+)/)?(\d+))?$");
                string prefix=m.Groups[1].Success?m.Groups[1].Value+"/":"";
                var group=inv.Groups.FirstOrDefault(g=>g.Prefix==prefix);
                if(!m.Success||group==null||!int.TryParse(m.Groups[2].Value,out int first)||first<1||first>group.Expected)
                {inv.Blockers.Add("Instant On physical interface is outside the validated inventory.");continue;}
                int last=first;
                if(m.Groups[4].Success&&(!int.TryParse(m.Groups[4].Value,out last)||last<first||last>group.Expected||(m.Groups[3].Success&&m.Groups[3].Value!=m.Groups[1].Value)))
                {inv.Blockers.Add("Instant On interface range is outside the validated inventory.");continue;}
                ports.AddRange(inv.Ports.Where(p=>p.Group==group.Id&&p.Number>=first&&p.Number<=last));
            }
            return ports.Distinct().ToList();
        }
        foreach(string raw in text.Split('\n'))
        {
            string line=raw.Trim();Match m;
            if(line=="!"||line.Equals("exit",StringComparison.OrdinalIgnoreCase))
            {current=new();vlan=null;database=false;continue;}
            // Only allowlisted configuration fields are emitted. Key/certificate payloads are ignored.
            if(Regex.IsMatch(line,@"(?i)\b(password|passwd|secret|community|private-key|credential|token|encrypted|certificate)\b"))continue;
            if((m=Regex.Match(line,@"^hostname\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Hostname=Parser.Safe(m.Groups[1].Value);continue;}
            if((m=Regex.Match(line,@"^voice vlan id\s+(\d+)$",RegexOptions.IgnoreCase)).Success){voiceId=VlanList(m.Groups[1].Value).FirstOrDefault();continue;}
            if((m=Regex.Match(line,@"^voice vlan state\s+(\S+)$",RegexOptions.IgnoreCase)).Success){voiceState=m.Groups[1].Value.ToLowerInvariant();continue;}
            if(line.Equals("vlan database",StringComparison.OrdinalIgnoreCase)){database=true;continue;}
            if(database&&(m=Regex.Match(line,@"^vlan\s+([\d,\- ]+)$",RegexOptions.IgnoreCase)).Success){foreach(int id in VlanList(m.Groups[1].Value))GetVlan(id);continue;}
            if((m=Regex.Match(line,@"^interface vlan\s+(\d+)$",RegexOptions.IgnoreCase)).Success)
            {vlan=VlanList(m.Groups[1].Value).FirstOrDefault();if(vlan>0)GetVlan(vlan.Value);current=new();database=false;continue;}
            if(series1960&&(m=Regex.Match(line,@"^interface\s+(TRK\d+)$",RegexOptions.IgnoreCase)).Success)
            {
                string id=m.Groups[1].Value.ToUpperInvariant();
                if(!trunks.TryGetValue(id,out var trunk)){trunk=new Port{Id=id};trunks[id]=trunk;native[id]=new HashSet<int>();}
                current=new(){trunk};vlan=null;database=false;continue;
            }
            if((m=Regex.Match(line,@"^interface\s+(?:range\s+)?([\d/,\- ]+)$",RegexOptions.IgnoreCase)).Success)
            {current=Resolve(m.Groups[1].Value);vlan=null;database=false;continue;}
            if(line.StartsWith("interface ",StringComparison.OrdinalIgnoreCase))
            {inv.Blockers.Add("Unsupported Instant On interface or aggregation syntax.");current=new();vlan=null;continue;}
            if(vlan.HasValue)
            {
                if((m=Regex.Match(line,@"^name\s+(.+)$",RegexOptions.IgnoreCase)).Success)GetVlan(vlan.Value).Name=Parser.Safe(m.Groups[1].Value);
                else if((m=Regex.Match(line,@"^ip(?:v6)? address\s+(.+)$",RegexOptions.IgnoreCase)).Success)inv.SVIs.Add(new Svi{Interface="VLAN "+vlan.Value,Address=Parser.Safe(m.Groups[1].Value)});
                continue;
            }
            if((m=Regex.Match(line,@"^management vlan\s+(\d+)$",RegexOptions.IgnoreCase)).Success)
            {inv.Management.Add("Management VLAN: "+m.Groups[1].Value);continue;}
            if((m=Regex.Match(line,@"^ip default-gateway\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Management.Add("Default gateway: "+Parser.Safe(m.Groups[1].Value));continue;}
            if((m=Regex.Match(line,@"^ip route\s+(.+)$",RegexOptions.IgnoreCase)).Success){inv.Routes.Add(Parser.Safe(m.Groups[1].Value));continue;}
            if(series1960&&current.Count>0&&(m=Regex.Match(line,@"^channel-group\s+(\d+)\s+mode\s+(auto|on)$",RegexOptions.IgnoreCase)).Success)
            {
                string id="TRK"+m.Groups[1].Value;
                if(!int.TryParse(m.Groups[1].Value,out int number)||number<1||number>16)inv.Blockers.Add("Invalid Instant On aggregate ID.");
                foreach(var p in current)
                {
                    if(!inv.Ports.Contains(p)){inv.Blockers.Add("Nested Instant On aggregates are unsupported.");continue;}
                    if(p.Aggregate.Length>0&&p.Aggregate!=id)inv.Blockers.Add("Physical port is assigned to multiple Instant On aggregates.");
                    p.Aggregate=id;p.Notes.Add("Aggregation mode: "+m.Groups[2].Value.ToUpperInvariant());
                }
                continue;
            }
            if(Regex.IsMatch(line,@"^(?:channel-group|port-channel|lacp|trunk|lag|stack|stacking)\b",RegexOptions.IgnoreCase))
            {inv.Blockers.Add("Unsupported Instant On aggregation syntax.");continue;}
            if(current.Count>0)
            {
                if((m=Regex.Match(line,@"^switchport general allowed vlan (add|remove)\s+([\d,\-]+)\s+(tagged|untagged)$",RegexOptions.IgnoreCase)).Success)
                {
                    var ids=VlanList(m.Groups[2].Value);bool tagged=m.Groups[3].Value.Equals("tagged",StringComparison.OrdinalIgnoreCase),add=m.Groups[1].Value.Equals("add",StringComparison.OrdinalIgnoreCase);
                    foreach(int id in ids)GetVlan(id);
                    foreach(var p in current)
                    {
                        p.Mode="General";
                        if(tagged){if(add)p.TaggedVlans=p.TaggedVlans.Concat(ids).Distinct().ToList();else p.TaggedVlans.RemoveAll(ids.Contains);}
                        else if(add)native[p.Id].UnionWith(ids);else native[p.Id].ExceptWith(ids);
                    }
                }
                else if((m=Regex.Match(line,@"^switchport general pvid\s+(\d+)$",RegexOptions.IgnoreCase)).Success)
                {var ids=VlanList(m.Groups[1].Value);if(ids.Count==1)foreach(var p in current)pvids[p.Id]=ids[0];}
                else if(line.Equals("switchport mode general",StringComparison.OrdinalIgnoreCase))foreach(var p in current)p.Mode="General";
                else if(line.Equals("shutdown",StringComparison.OrdinalIgnoreCase))foreach(var p in current)p.Disabled=true;
                else if(line.Equals("no shutdown",StringComparison.OrdinalIgnoreCase))foreach(var p in current)p.Disabled=false;
                else if((m=Regex.Match(line,@"^(?:description|name)\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(var p in current)p.Description=Parser.Safe(m.Groups[1].Value);
                else if((m=Regex.Match(line,@"^(?:speed|speed-duplex)\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(var p in current)p.Speed=Parser.Safe(m.Groups[1].Value);
                else if(Regex.IsMatch(line,@"^(?:switchport|channel-group|lacp|trunk|lag)\b",RegexOptions.IgnoreCase))inv.Blockers.Add("Unsupported Instant On port VLAN or aggregation syntax.");
            }
        }
        foreach(var members in inv.Ports.Where(p=>p.Aggregate.Length>0).GroupBy(p=>p.Aggregate))
        {
            if(!trunks.TryGetValue(members.Key,out var trunk)){inv.Blockers.Add("Instant On aggregate has members but no explicit TRK interface configuration: "+members.Key);continue;}
            if(members.SelectMany(p=>p.Notes).Where(n=>n.StartsWith("Aggregation mode: ")).Distinct().Count()>1)inv.Blockers.Add("Instant On aggregate members have conflicting modes: "+members.Key);
            foreach(var p in members)
            {
                if(native[p.Id].Count>0||p.TaggedVlans.Count>0||pvids.ContainsKey(p.Id))p.Notes.Add("Per-port VLAN settings are inactive shadow configuration while assigned to "+members.Key);
                native[p.Id]=new HashSet<int>(native[trunk.Id]);p.TaggedVlans=trunk.TaggedVlans.ToList();p.Mode=trunk.Mode;
                pvids.Remove(p.Id);if(pvids.TryGetValue(trunk.Id,out int pv))pvids[p.Id]=pv;
                if(trunk.Description.Length>0)p.Notes.Add("Aggregate description: "+trunk.Description);
                if(trunk.Disabled==true)p.Disabled=true;
            }
        }
        foreach(var trunk in trunks.Values)
            if(!inv.Ports.Any(p=>p.Aggregate==trunk.Id))inv.Blockers.Add("Instant On TRK interface has no declared physical members: "+trunk.Id);
        if(voiceId>0)
        {
            var voice=GetVlan(voiceId.Value);voice.Voice=voiceState!="disabled";
            inv.Management.Add("Voice VLAN: "+voiceId+"; state: "+(voiceState.Length>0?voiceState:"Not supplied"));
        }
        foreach(var p in inv.Ports)
        {
            if(native[p.Id].Count>1)inv.Blockers.Add("Multiple untagged VLANs cannot be represented on Instant On interface "+p.Id+".");
            else if(native[p.Id].Count==1){p.UntaggedVlan=native[p.Id].Single();p.UntaggedState="Known";}
            if(pvids.TryGetValue(p.Id,out int pvid))
            {
                p.Notes.Add("Configured ingress PVID: "+pvid);
                if(p.UntaggedVlan.HasValue&&p.UntaggedVlan!=pvid)inv.Blockers.Add("PVID and egress untagged VLAN differ on Instant On interface "+p.Id+"; separate ingress/egress representation is required.");
            }
            p.TaggedVlans=p.TaggedVlans.Distinct().Order().ToList();
            if(p.UntaggedVlan.HasValue&&p.TaggedVlans.Contains(p.UntaggedVlan.Value))inv.Blockers.Add("Instant On VLAN has conflicting tagged/untagged membership on interface "+p.Id+".");
            if(voiceId>0&&inv.Vlans.Any(v=>v.Id==voiceId&&v.Voice)&&(p.UntaggedVlan==voiceId||p.TaggedVlans.Contains(voiceId.Value)))p.VoiceVlan=voiceId;
        }
        inv.Assumptions.Add("Instant On "+family+" local configuration syntax only. No live link state, installed transceiver, physical stack-link topology or omitted default VLAN membership is inferred.");
        inv.Assumptions.Add("The native fill uses explicit egress untagged membership. Ingress PVID is retained in port notes. Omitted egress settings remain Unknown, including PVID-only ports.");
        inv.Assumptions.Add("Voice diamonds identify the configured global voice VLAN on explicit member ports. OUI-based dynamic membership and actual phone detection are not inferred.");
        inv.Assumptions.Add(series1960?"1960 JL805A automatic member profiles use unit declarations with network te/uplink none. Other stack members require an explicit operator-supplied SKU for every unit. Declared members are not proof of live presence. Physical stacking ports are not inferred. TRK membership uses channel-group definitions; AUTO/ON labels preserve configured mode without inferring an active negotiation state.":"Instant On 1930 aggregation syntax remains unvalidated; encountered aggregation directives block diagrams.");
        inv.Blockers=inv.Blockers.Distinct().ToList();return inv;
    }
}
