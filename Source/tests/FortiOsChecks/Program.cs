using SwitchportGenerator;

static class FortiOsChecks
{
    static int assertions;
    static void Check(bool condition,string message)
    {
        assertions++;
        if(!condition)throw new InvalidOperationException(message);
    }

    // Fictional-only fixture. Never copy any customer configuration, address,
    // serial number, account, password or encrypted secret into this repository.
    static string Sample(string platform="FG2H0G") => $"""
        #config-version={platform}-7.6.8-FW-build3744-260929:opmode=0:vdom=0:user=example
        config system global
            set hostname "LAB-FG200G"
            set alias "FortiGate-200G"
        end
        config system interface
            edit "ha"
                set type physical
            next
            edit "mgmt"
                set ip 192.0.2.99 255.255.255.0
                set type physical
                set dedicated-to management
            next
            edit "port1"
                set type physical
                set role wan
            next
            edit "port2"
                set ip 192.0.2.1 255.255.255.0
                set type physical
                set alias "Trusted link"
                set role lan
            next
            edit "port6"
                set status down
                set type physical
                set alias "Unused"
            next
            edit "port9"
                set mode dhcp
                set type physical
                set speed 5000auto
            next
            edit "port11"
                set type physical
                set speed 5000auto
                set alias "Multigig port"
            next
            edit "port17"
                set type physical
            next
            edit "x1"
                set type physical
                set mediatype sr-lr
                set speed 10000full
            next
            edit "x2"
                set type physical
                set speed 10000full
            next
            edit "Lab VLAN 140"
                set alias "HVAC"
                set interface "port2"
                set vlanid 140
                set ip 198.51.100.1 255.255.255.0
            next
            edit "Lab VLAN 160"
                set interface "port2"
                set vlanid 160
            next
            edit "Lab VLAN 250"
                set interface "x2"
                set vlanid 250
            next
            edit "agg1"
                set type aggregate
                set member "port1" "x1"
            next
            edit "agged-vlan"
                set interface "agg1"
                set vlanid 301
            next
            edit "ssl.root"
                set type tunnel
            next
            edit "modem"
                set mode pppoe
                set status down
                set type physical
            next
        end
        config system ha
            set mode a-p
        end
        config router static
            edit 1
                set dst 203.0.113.0 255.255.255.0
                set gateway 192.0.2.254
                set device "port2"
            next
        end
        config system dhcp server
            edit 1
                set interface "port2"
                set default-gateway 192.0.2.1
            next
        end
        config system admin
            edit "admin"
                set password ENC FAKE-NO-SECRETS-FOR-TESTING
            next
        end
        """;

    static Port Port(Inventory inv,string id) =>
        inv.Ports.Single(port=>port.Id.Equals(id,StringComparison.OrdinalIgnoreCase));

    static void Examine(string code, string expectedSku)
    {
        string cfg=Sample(code);
        var inv=DeviceModels.Parse(cfg,Array.Empty<DeviceIdentity>());
        Check(inv.Blockers.Count==0,"Expected verified FortiGate inventory: "+string.Join("; ",inv.Blockers));
        Check(inv.Vendor=="Fortinet"&&inv.OS=="FortiOS","FortiOS recognition");
        Check(inv.Hostname=="LAB-FG200G","Saved hostname");
        Check(inv.Firmware=="7.6.8","FortiOS firmware");
        Check(inv.Topology.Contains("active-passive"),"HA mode");
        Check(inv.Groups.Count==1&&inv.Groups[0].Product==expectedSku,"Model from product identifier");
        Check(inv.Ports.Count==30&&inv.Groups[0].Expected==30&&inv.Groups[0].SocketLayout.Count==30,"30 front-panel sockets");
        Check(inv.Ports.Select(p=>p.Number).SequenceEqual(Enumerable.Range(1,30)),"physical positions ordered");
        Check(inv.Ports.Select(p=>p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==30,"all port IDs unique");
        Check(inv.Ports.Count(p=>p.Media=="RJ-45")==18,"16 copper + HA + MGMT");
        Check(inv.Ports.Count(p=>p.Media=="SFP+")==8&&inv.Ports.Count(p=>p.Media=="SFP")==4,"separate optical media");
        Check(Port(inv,"x8").Media=="SFP+"&&Port(inv,"port20").Media=="SFP","Optical labels and media");
        Check(Port(inv,"ha").DisplayNumber=="HA"&&Port(inv,"mgmt").DisplayNumber=="MGMT","HA and MGMT printed labels");
        Check(Port(inv,"port9").Speed=="5000auto"&&Port(inv,"x1").Speed=="10000full","Speed configured");
        Check(Port(inv,"port6").Disabled==true&&!Port(inv,"port2").Disabled.GetValueOrDefault(),"Administrative state");
        Check(Port(inv,"port2").Description=="Trusted link"&&Port(inv,"port11").Description=="Multigig port","Interface aliases");
        Check(Port(inv,"port2").TaggedVlans.SequenceEqual(new[]{140,160}),"Native routed port receives configured VLAN subinterfaces as tags");
        Check(Port(inv,"x2").TaggedVlans.SequenceEqual(new[]{250}),"SFP+ parent VLAN tags");
        Check(Port(inv,"port1").TaggedVlans.SequenceEqual(new[]{301})&&Port(inv,"x1").TaggedVlans.SequenceEqual(new[]{301}),"Aggregate child VLAN tags propagate to both members");
        Check(Port(inv,"port1").Aggregate=="agg1"&&Port(inv,"x1").Aggregate=="agg1","FortiOS aggregate members outlined");
        Check(inv.Ports.All(p=>p.UntaggedVlan==null&&p.UntaggedState=="Unknown"),"Never invent native VLAN on FortiOS routed interfaces");
        Check(inv.LogicalInterfaces.Any(p=>p.Id=="Lab VLAN 140"&&p.UntaggedVlan==140)&&inv.LogicalInterfaces.Any(p=>p.Id=="agg1"),"Logical VLAN/aggregate interfaces retained separately");
        Check(inv.SVIs.Any(s=>s.Interface=="port2")&&inv.SVIs.Any(s=>s.Interface=="Lab VLAN 140"),"IP interface summary");
        Check(inv.Vlans.Select(v=>v.Id).SequenceEqual(new[]{140,160,250,301}),"Configured VLAN identity");
        Check(inv.Routes.Count==1&&inv.Routes[0].Contains("203.0.113.0"),"Saved static routing");
        Check(inv.Management.Any(s=>s.Contains("DHCP server")),"DHCP server summary");
        Check(!System.Text.Json.JsonSerializer.Serialize(inv).Contains("FAKE-NO-SECRETS",StringComparison.OrdinalIgnoreCase),"No admin password leak");
        Check(inv.Ports.All(p=>p.Id!="modem"&&p.Id!="ssl.root"),"Virtual/modem are not physical sockets");
        var p=Charts.Layout(inv);
        Check(p.Count==30,"One diagram socket per physical port");
        Check(Charts.Validate(inv,p).Count==0,"Physical front-panel layout: "+string.Join(" | ",Charts.Validate(inv,p)));
        var sockets=p.ToDictionary(socket=>socket.Port);
        Check(sockets["ha"].Tile.Y<sockets["mgmt"].Tile.Y,"HA above MGMT");
        Check(sockets["port1"].Tile.X<sockets["port9"].Tile.X&&sockets["port9"].Tile.X<sockets["x1"].Tile.X&&sockets["x1"].Tile.X<sockets["port17"].Tile.X,"Left-to-right bank order");
        Check(sockets["port1"].Tile.Y<sockets["port2"].Tile.Y&&sockets["x1"].Tile.Y<sockets["x2"].Tile.Y,"Top odd, bottom even ports");
        var output=QuickSheet.Build(inv,p,"FORTI-FIXTURE");
        Check(output.Sockets.Count==30,"A4 sheet must render all sockets");
        Check(output.Sockets.Count(x=>x.Optical)==12,"A4 optical shapes");
        Check(Charts.ValidateScene(output.Scene).Count==0,"A4 scene collision validation: "+string.Join(" | ",Charts.ValidateScene(output.Scene)));
        Check(ReportText.ReadHostname(cfg)=="LAB-FG200G","FortiOS hostname retrieval in workspace");
        Check(DeviceModels.Choices.Any(x=>x.Sku==expectedSku&&x.Kind=="Firewall"),"Device model selector");
        var selected=DeviceModels.Parse(cfg,new[]{new DeviceIdentity{Id="1",ModelSelection=expectedSku}});
        Check(selected.Blockers.Count==0&&selected.Devices.Any(d=>d.ModelSelection==expectedSku),"Compatible selected FortiGate model");
        var other=expectedSku=="FG-200G"?"FG-201G":"FG-200G";
        bool rejected=false;
        try{DeviceModels.Parse(cfg,new[]{new DeviceIdentity{Id="1",ModelSelection=other}});}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Contradictory model choice must fail");
    }

    public static int Main()
    {
        Examine("FG2H0G","FG-200G");
        Examine("FG2H1G","FG-201G");
        var unsupported=DeviceModels.Parse(Sample("FG999G"),Array.Empty<DeviceIdentity>());
        Check(unsupported.Blockers.Any(s=>s.Contains("unconfirmed")),"Unknown FortiGate variant cannot be mapped to known faceplate");
        Check(unsupported.Groups.Count==0&&unsupported.Ports.Count==0,"Unknown FG hardware cannot create invented physical ports");
        Check(!FortiOsParser.IsConfiguration("; JL256A Configuration Editor\nhostname ARUBA-LAB\n"),"Aruba configs retain their own parser");
        Check(!FortiOsParser.IsConfiguration("# by RouterOS 7.16\n/system identity\n"),"RouterOS configs retain their own parser");
        Console.WriteLine("PASS FortiOsChecks ("+assertions+" assertions) without any customer data.");
        return 0;
    }
}
