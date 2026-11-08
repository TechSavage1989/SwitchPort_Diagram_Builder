using System.Text.RegularExpressions;

namespace SwitchportGenerator;

/// <summary>
/// FortiGate 200G/201G physical front panel and FortiOS configuration parser.
/// Only explicitly identified models are assigned hardware. Security policies,
/// encrypted keys and the full configuration are never copied into the inventory.
/// </summary>
public static class FortiOsParser
{
    public const string HardwareReference = "https://docs.fortinet.com/product/fortigate/hardware";
    const string PhysicalReference = "FortiGate 200G Series QuickStart Guide, front panel, pages 8-9";
    static readonly Regex Version = new(@"(?m)^#config-version=(FG2H[01]G)-(\d+\.\d+(?:\.\d+)?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex SectionStart = new(@"(?m)^config ([^\r\n]+)\r?$", RegexOptions.Compiled);
    static readonly Regex TopLevelEnd = new(@"(?m)^end\s*$", RegexOptions.Compiled);
    static readonly Regex InterfaceEntry = new(@"(?ms)^    edit (?:""([^""\r\n]+)""|(\S+))\s*\r?\n(.*?)^    next\s*$", RegexOptions.Compiled);
    static readonly Regex Parameter = new(@"(?m)^\s{8}set ([a-z0-9-]+)\s+([^\r\n]+)\r?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsConfiguration(string source) =>
        source.Length < 20_000_000 && (Regex.IsMatch(source, @"(?m)^#config-version=FG", RegexOptions.IgnoreCase) ||
        (Regex.IsMatch(source, @"(?m)^config system global\s*$") &&
         Regex.IsMatch(source, @"(?m)^config system interface\s*$")));

    static string Section(string source, string name)
    {
        var start=Regex.Match(source, @"(?m)^config "+Regex.Escape(name)+@"\s*\r?$", RegexOptions.IgnoreCase);
        if (!start.Success) return "";
        var end=TopLevelEnd.Match(source, start.Index+start.Length);
        return end.Success ? source.Substring(start.Index+start.Length,end.Index-start.Index-start.Length) : "";
    }

    static Dictionary<string,string> SetValues(string entry)
    {
        var values = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(Match match in Parameter.Matches(entry))
            values[match.Groups[1].Value]=match.Groups[2].Value.Trim();
        return values;
    }

    static string Value(IReadOnlyDictionary<string,string> values,string key)
    {
        if (!values.TryGetValue(key,out var raw)) return "";
        return raw.Trim().Trim('"');
    }

    static string Clean(string value,int maximum=120)
    {
        string safe=Parser.Safe(value);
        return safe.Length>maximum?safe[..maximum]:safe;
    }

    static IEnumerable<(string Name,Dictionary<string,string> Values)> Entries(string section)
    {
        foreach(Match entry in InterfaceEntry.Matches(section))
        {
            string name=entry.Groups[1].Success?entry.Groups[1].Value:entry.Groups[2].Value;
            if(name.Length is <1 or >160) continue;
            yield return (name,SetValues(entry.Groups[3].Value));
        }
    }

    static int? VlanId(string value) =>
        int.TryParse(value,out var id) && id is >=1 and <=4094 ? id : null;

    public static string ReadHostname(string source)
    {
        var match=Regex.Match(Section(source,"system global"),@"(?m)^\s+set hostname\s+(.+)$");
        return match.Success?Clean(match.Groups[1].Value.Trim().Trim('"'),100):"";
    }

    public static Inventory Parse(string source)
    {
        var inv=new Inventory { Vendor="Fortinet", OS="FortiOS", Topology="Standalone" };
        if(source.Length>20_000_000)
        {
            inv.Blockers.Add("FortiOS input exceeds the 20 MB limit.");
            return inv;
        }
        var version=Version.Match(source);
        if(!version.Success)
        {
            inv.Blockers.Add("FortiGate physical model is unconfirmed. This parser supports FG2H0G (200G) and FG2H1G (201G), identified by the backup configuration-version header. Do not infer a faceplate from port names.");
            return inv;
        }
        bool is201=version.Groups[1].Value.Equals("FG2H1G",StringComparison.OrdinalIgnoreCase);
        string model=is201?"FG-201G":"FG-200G";
        inv.Hostname=ReadHostname(source);
        if(inv.Hostname.Length==0)inv.Hostname="Unknown";
        inv.Firmware=version.Groups[2].Value;
        inv.ReportModel="FortiGate "+(is201?"201G":"200G");
        inv.Topology=Regex.IsMatch(Section(source,"system ha"),@"(?m)^\s+set mode\s+a-p\s*$",RegexOptions.IgnoreCase)
            ? "HA active-passive (this node)" : "Standalone";
        var group=new Group
        {
            Id="Front panel", Product=model, Device="1", Copper=18, Optical=12,
            OpticalSide="right", OpticalType="SFP+/SFP",
        };
        // Exact left-to-right, two-row physical faceplate:
        // HA/MGMT, ports 1-8 (GE RJ45), 9-16 (5GE RJ45),
        // X1-X8 (10GE SFP+), ports 17-20 (GE SFP).
        // Numbers are internal ordinal positions; Id/DisplayNumber are printed labels.
        for(int ordinal=1;ordinal<=30;ordinal++)
        {
            string id,media,display;
            double col;int row;
            if(ordinal<=8)
            {
                id="port"+ordinal;media="RJ-45";display=ordinal.ToString();
                col=2+(ordinal-1)/2;row=(ordinal-1)%2;
            }
            else if(ordinal<=16)
            {
                id="port"+ordinal;media="RJ-45";display=ordinal.ToString();
                col=7+(ordinal-9)/2;row=(ordinal-9)%2;
            }
            else if(ordinal<=24)
            {
                int n=ordinal-16;id="x"+n;media="SFP+";display="X"+n;
                col=12+(n-1)/2;row=(n-1)%2;
            }
            else if(ordinal<=28)
            {
                int n=ordinal-8;id="port"+n;media="SFP";display=n.ToString();
                col=17+(n-17)/2;row=(n-17)%2;
            }
            else
            {
                id=ordinal==29?"ha":"mgmt";media="RJ-45";display=ordinal==29?"HA":"MGMT";
                col=0;row=ordinal==29?0:1;
            }
            group.SocketLayout.Add(new HardwareSocket
            { Number=ordinal,Column=col,Row=row,Media=media });
            inv.Ports.Add(new Port
            {
                Id=id,Group=group.Id,Number=ordinal,DisplayNumber=display,Media=media,
                Mode="Physical",UntaggedState="Unknown"
            });
        }
        inv.Groups.Add(group);
        inv.Assumptions.Add("Physical faceplate verified: "+PhysicalReference+
            ". 8 GE RJ45, 8 5GE RJ45, 8 10GE SFP+, 4 GE SFP, dedicated HA and MGMT RJ45. "+
            "Console, USB and virtual modem are not Ethernet data sockets. "+HardwareReference);
        inv.Assumptions.Add("FortiOS routed interfaces do not imply an access/native VLAN. "+
            "Tagged VLAN markers represent configured 802.1Q subinterfaces, not measured switchport trunk membership.");

        var physical=inv.Ports.ToDictionary(p=>p.Id,StringComparer.OrdinalIgnoreCase);
        var interfaceSection=Section(source,"system interface");
        if(interfaceSection.Length==0)
        {
            inv.Blockers.Add("No FortiOS system interface section was found.");
            return inv;
        }
        var logical=new List<(string Name,Dictionary<string,string> Values)>();
        var aggregates=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
        var seenPhysical=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in Entries(interfaceSection))
        {
            string name=entry.Name;var values=entry.Values;
            if(physical.TryGetValue(name,out var p))
            {
                seenPhysical.Add(p.Id);
                string configuredType=Value(values,"type");
                if(configuredType.Length>0&&!configuredType.Equals("physical",StringComparison.OrdinalIgnoreCase))
                {
                    inv.Blockers.Add("Physical front-panel port "+p.Id+" has incompatible type "+Clean(configuredType)+".");
                    continue;
                }
                string alias=Value(values,"alias");
                if(alias.Length>0)p.Description=Clean(alias,140);
                string role=Value(values,"role");
                if(role.Length>0)p.Usage="Configured role: "+Clean(role,40);
                string status=Value(values,"status");
                if(status.Equals("down",StringComparison.OrdinalIgnoreCase)){p.Disabled=true;p.Notes.Add("FortiOS administrative status: down");}
                else if(status.Length==0||status.Equals("up",StringComparison.OrdinalIgnoreCase))p.Disabled=false;
                string speed=Value(values,"speed");
                if(speed.Length>0)p.Speed=Clean(speed,50);
                if(name.Equals("ha",StringComparison.OrdinalIgnoreCase))
                {
                    p.Mode="HA heartbeat";
                    p.Usage="Dedicated HA socket";
                    p.Notes.Add("HA role is based on dedicated hardware, not a stack port.");
                }
                if(name.Equals("mgmt",StringComparison.OrdinalIgnoreCase))
                {
                    p.Mode="Management";
                    p.Usage="Dedicated management socket";
                }
                string ip=Value(values,"ip");
                if(ip.Length>0)
                {
                    p.Notes.Add("Address: "+Clean(ip,100));
                    if(!name.Equals("ha",StringComparison.OrdinalIgnoreCase)&&!name.Equals("mgmt",StringComparison.OrdinalIgnoreCase))
                        p.Mode="Routed L3";
                    inv.SVIs.Add(new Svi{Interface=name,Address=Clean(ip,100)});
                }
                else if(Value(values,"mode").Equals("dhcp",StringComparison.OrdinalIgnoreCase))
                {
                    p.Mode=name=="mgmt"?"Management (DHCP)":"Routed DHCP";
                    p.Notes.Add("Address: DHCP (address assigned at runtime)");
                }
                if(Value(values,"fortilink").Equals("enable",StringComparison.OrdinalIgnoreCase))
                    p.Notes.Add("FortiLink enabled in saved configuration");
                continue;
            }
            string type=Value(values,"type");
            if(type.Equals("aggregate",StringComparison.OrdinalIgnoreCase))
            {
                string member=Value(values,"member");
                aggregates[name]=Regex.Matches(member,"\"([^\"]+)\"|([^\\s]+)").Select(m=>
                    m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value).ToArray();
                logical.Add(entry);
                continue;
            }
            if(VlanId(Value(values,"vlanid")).HasValue||type.Equals("vlan",StringComparison.OrdinalIgnoreCase))
            {
                logical.Add(entry);
                continue;
            }
            // Other FortiOS logical objects (tunnels, loopbacks, virtual links,
            // PPPoE modem) are not installed front-panel Ethernet sockets.
        }
        foreach(var aggregate in aggregates)
        {
            foreach(string name in aggregate.Value)
            {
                if(!physical.TryGetValue(name,out var p))
                {
                    inv.Assumptions.Add("Aggregate "+Clean(aggregate.Key)+" references a member outside the verified front-panel inventory: "+Clean(name)+".");
                    continue;
                }
                if(p.Aggregate.Length>0&&p.Aggregate!=aggregate.Key)
                    inv.Blockers.Add("Physical interface "+p.Id+" is declared in more than one aggregate.");
                p.Aggregate=Clean(aggregate.Key,80);
                p.Notes.Add("Aggregation mode: FortiOS aggregate");
                p.Mode="Aggregate member";
            }
        }
        foreach(var (name,values) in logical)
        {
            int? vlan=VlanId(Value(values,"vlanid"));
            string type=Value(values,"type"),parent=Value(values,"interface");
            string alias=Value(values,"alias");
            string address=Value(values,"ip");
            if(vlan.HasValue)
            {
                var existing=inv.Vlans.FirstOrDefault(v=>v.Id==vlan.Value);
                if(existing==null)inv.Vlans.Add(new Vlan{Id=vlan.Value,Name=Clean(name,110)});
                else if(existing.Name!=name)inv.Assumptions.Add("VLAN "+vlan.Value+
                    " is used by more than one named FortiOS subinterface; identities stay separate in the logical inventory.");
                var parentPorts=physical.TryGetValue(parent,out var port)
                    ?new[]{port}
                    :aggregates.TryGetValue(parent,out var members)
                        ?members.Where(physical.ContainsKey).Select(member=>physical[member]).ToArray()
                        :Array.Empty<Port>();
                if(parentPorts.Length==0)
                    inv.Assumptions.Add("VLAN interface "+Clean(name)+" (ID "+vlan.Value+
                        ") is attached to "+Clean(parent)+", which is not a confirmed physical parent.");
                foreach(var p in parentPorts)
                {
                    if(!p.TaggedVlans.Contains(vlan.Value))p.TaggedVlans.Add(vlan.Value);
                    if(!p.RelatedInterfaces.Contains(name,StringComparer.OrdinalIgnoreCase))p.RelatedInterfaces.Add(Clean(name,120));
                    if(p.Mode is "Physical" or "Routed L3" or "Routed DHCP")
                        p.Mode=p.Mode=="Physical"?"802.1Q parent":"Routed L3 + 802.1Q";
                }
                inv.LogicalInterfaces.Add(new Port{Id=Clean(name),Media="Virtual",Mode="802.1Q VLAN",
                    UntaggedVlan=vlan.Value,Description=Clean(alias),Usage="Parent: "+Clean(parent),RelatedInterfaces=new(){Clean(parent)}});
            }
            else if(type.Equals("aggregate",StringComparison.OrdinalIgnoreCase))
                inv.LogicalInterfaces.Add(new Port{Id=Clean(name),Media="Virtual",Mode="FortiOS aggregate",
                    Usage="Logical aggregate; physical members are listed with the LAG outline"});
            if(address.Length>0)
                inv.SVIs.Add(new Svi{Interface=Clean(name),Address=Clean(address,100)});
        }
        if(!seenPhysical.Contains("ha")||!seenPhysical.Contains("mgmt"))
            inv.Assumptions.Add("HA and MGMT physical sockets are shown from the verified front panel even if omitted from the saved interface section.");
        inv.Vlans=inv.Vlans.OrderBy(v=>v.Id).ToList();
        foreach(var p in inv.Ports)p.TaggedVlans.Sort();
        // Optional supplemental routing and DHCP summaries are derived only
        // from the static configuration and cannot assert live routing state.
        foreach(var (name,values) in Entries(Section(source,"router static")).Take(256))
        {
            string dst=Value(values,"dst"),gateway=Value(values,"gateway"),device=Value(values,"device");
            if(dst.Length+gateway.Length+device.Length==0)continue;
            inv.Routes.Add("Static route "+Clean(name,20)+": "+
                (dst.Length>0?Clean(dst,85):"default")+
                (gateway.Length>0?" via "+Clean(gateway,85):"")+
                (device.Length>0?" on "+Clean(device,90):""));
        }
        foreach(var (name,values) in Entries(Section(source,"system dhcp server")).Take(128))
        {
            string iface=Value(values,"interface"),gateway=Value(values,"default-gateway");
            if(iface.Length>0)inv.Management.Add("Configured DHCP server "+Clean(name,20)+
                " on "+Clean(iface,90)+(gateway.Length>0?" | gateway "+Clean(gateway,85):""));
        }
        inv.Management.Add("Management: dedicated MGMT RJ45; actual access permissions and reachability depend on the running configuration.");
        return inv;
    }
}
