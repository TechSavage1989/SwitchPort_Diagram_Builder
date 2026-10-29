using System.Text.Json;
using SwitchportGenerator;

static class CiscoChecks
{
    static int assertions;
    static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    static string Header="version 16.9\nhostname CISCO-LAB\nswitch 1 provision ws-c3650-48pd\n!\n";
    static Inventory Parse(string commands)=>Parser.Parse(Header+commands);
    static Port Port(Inventory inv,string id)=>inv.Ports.Single(p=>p.Id==id);
    static void Main(string[] args)
    {
        foreach(var model in CiscoHardware.Models)
        {
            var inv=Parser.Parse("version 17.3\nhostname MODEL-LAB\ninterface GigabitEthernet0/0\n shutdown\n!\n",model.Code);
            Check(inv.Blockers.Count==0,model.Code+" imports by explicit model.");
            Check(inv.Ports.Count==model.Sockets.Count,model.Code+" physical count.");
            var map=Charts.Layout(inv);Check(Charts.Validate(inv,map).Count==0,model.Code+" base geometry.");
            var sheet=QuickSheet.Build(inv,map,"CISCO-CHECK");Check(Charts.ValidateScene(sheet.Scene).Count==0,model.Code+" A4 scene.");
            Check(sheet.Sockets.Count==inv.Ports.Count,model.Code+" each cage appears once.");
        }
        var stack=Parser.Parse(Header+"switch 2 provision ws-c3650-48pd\ninterface Port-channel7\n switchport mode trunk\n switchport trunk native vlan 10\n switchport trunk allowed vlan 10,20-22\n!\ninterface GigabitEthernet1/0/1\n switchport access vlan 99\n channel-group 7 mode active\n!\ninterface TenGigabitEthernet2/1/4\n channel-group 7 mode passive\n!\nvlan 20\n name Voice\n!\n");
        Check(stack.Blockers.Count==0&&stack.Ports.Count==104&&stack.Groups.Count==2,"Two provisioned members have 104 cages.");
        foreach(var p in stack.Ports.Where(p=>p.Aggregate=="Po7"))Check(p.UntaggedVlan==10&&p.TaggedVlans.SequenceEqual(new[]{20,21,22})&&p.Mode=="Trunk"&&p.Notes.Any(n=>n.Contains("LACP")),"Cross-stack channel inherits VLANs and LACP.");
        Check(!stack.Ports.Any(p=>p.StackLink),"Rear StackWise cables are not inferred from provisioning.");
        Check(Port(stack,"GigabitEthernet2/0/48").UntaggedVlan==null,"Unconfigured ports remain unknown.");
        Check(Port(stack,"TenGigabitEthernet2/1/4").Media=="SFP+"&&Port(stack,"GigabitEthernet2/1/1").Media=="SFP","3650 mixed media capability.");
        var map3650=Charts.Layout(stack);var uplinks=map3650.Where(p=>p.Group=="Member 1"&&stack.Ports.Single(q=>q.Id==p.Port).Number>48).ToList();
        Check(uplinks.Select(p=>p.Tile.Y).Distinct().Count()==1&&uplinks.Select(p=>p.Tile.X).SequenceEqual(uplinks.Select(p=>p.Tile.X).Order()),"3650 four uplinks form one horizontal bank.");
        var access=Parse("interface GigabitEthernet1/0/1\n switchport mode access\n switchport access vlan 10\n switchport voice vlan 20\n description Lab phone\n shutdown\n!\ninterface GigabitEthernet1/0/2\n switchport access vlan 30\n!\ninterface GigabitEthernet1/0/3\n no switchport\n ip address 192.0.2.1 255.255.255.0\n!\n");
        var a=access.Ports[0];Check(a.UntaggedVlan==10&&a.TaggedVlans.SequenceEqual(new[]{20})&&a.VoiceVlan==20&&a.Disabled==true&&a.Description=="Lab phone","Access, voice, description and shutdown survive.");
        Check(access.Ports[1].Mode.StartsWith("Dynamic")&&access.Ports[1].Notes.Any(n=>n.Contains("DTP")),"Configured access VLAN without static mode retains DTP uncertainty.");
        Check(access.Ports[2].Mode=="Routed"&&access.Ports[2].UntaggedVlan==null,"Routed interfaces have no fabricated native VLAN.");
        var all=Parse("vlan 10,20-22\n!\ninterface GigabitEthernet1/0/1\n switchport mode trunk\n switchport trunk allowed vlan except 1,21\n!\n");
        a=all.Ports[0];Check(a.UntaggedVlan==null&&a.UntaggedState=="None"&&a.TaggedVlans.SequenceEqual(new[]{10,20,22}),"All-except omits excluded native and tagged IDs.");
        Check(ReportText.TaggedVlans(a).Contains("except 1,21")&&a.TaggedVlanPolicy!=null,"Symbolic policy retains every exclusion for CSV/PDF.");
        var native=Parse("vlan dot1q tag native\ninterface GigabitEthernet1/0/1\n switchport mode trunk\n switchport trunk native vlan 10\n switchport trunk allowed vlan 10,20\n!\n");
        Check(native.Ports[0].UntaggedVlan==null&&native.Ports[0].TaggedVlans.SequenceEqual(new[]{10,20}),"Global native tagging removes native fill and retains tagged native marker.");
        var modified=Parse("interface GigabitEthernet1/0/1\n switchport mode trunk\n switchport trunk allowed vlan 10,20-22\n switchport trunk allowed vlan remove 21\n switchport trunk allowed vlan add 30\n!\n");
        Check(modified.Ports[0].TaggedVlans.SequenceEqual(new[]{10,20,22,30}),"Allowed set replacement/add/remove are sequential.");
        foreach(bool reverse in new[]{false,true})
        {
            string stub="interface GigabitEthernet1/1/3\n!\n",configured="interface TenGigabitEthernet1/1/3\n switchport mode access\n switchport access vlan 10\n!\n";
            var alias=Parse(reverse?configured+stub:stub+configured);
            Check(alias.Blockers.Count==0&&alias.Ports.Count==52&&Port(alias,"TenGigabitEthernet1/1/3").UntaggedVlan==10,"Unused alias stubs do not double count or override a configured cage.");
        }
        Check(Parse("interface GigabitEthernet1/1/3\n switchport access vlan 20\n!\ninterface TenGigabitEthernet1/1/3\n switchport access vlan 10\n!\n").Blockers.Any(b=>b.Contains("aliases")),"Conflicting uplink aliases require active-interface confirmation.");
        foreach(string bad in new[]{"switchport access vlan 4095","switchport trunk allowed vlan 20-10","switchport voice vlan dot1p","switchport trunk encapsulation isl","switchport private-vlan host-association 10 20"})
            Check(Parse("interface GigabitEthernet1/0/1\n "+bad+"\n!\n").Blockers.Count>0,"Unsafe VLAN syntax is rejected: "+bad);
        foreach(string bad in new[]{"GigabitEthernet1/0/49","HundredGigE1/0/1/1","GigabitEthernet1/0/1.10","range GigabitEthernet1/0/1 - 4"})
            Check(Parse("interface "+bad+"\n switchport mode access\n!\n").Blockers.Count>0,"Unsupported physical inventory is rejected: "+bad);
        foreach(int size in new[]{24,48})
        {
            string signature="version 17.3\nhostname SIGNATURE-LAB\n"+string.Concat(CiscoHardware.Find("C9500-"+size+"Y4C")!.Sockets.Select(s=>"interface "+CiscoHardware.Interface(s,"1")+"\n!\n"));
            var inv=Parser.Parse(signature);Check(inv.Blockers.Count==0&&inv.Groups.Single().Product=="C9500-"+size+"Y4C","Complete native Y4C signature establishes layout.");
            Check(Parser.Parse(signature.Replace("interface HundredGigE1/0/"+(size+4)+"\n!\n","")).Blockers.Count>0,"Partial interface inventory cannot establish a Y4C model.");
            Check(Parser.Parse(signature+"switch 1 provision unknown-model\n").Blockers.Count>0,"Explicit unknown model cannot be replaced by signature inference.");
            var invMap=Charts.Layout(inv);var q=invMap.Where(p=>inv.Ports.Single(q=>q.Id==p.Port).Number>size).ToList();
            Check(q.Count==4&&q[0].Tile.X==q[1].Tile.X&&q[0].Tile.Y<q[1].Tile.Y&&q[2].Tile.X>q[0].Tile.X,"Y4C uplinks remain a right-hand 2x2 bank.");
        }
        Check(Parser.Parse(Header,"C9500-48Y4C").Blockers.Count>0,"Selected model cannot override provisioning.");
        var inventory=Parser.Parse("version 17.3\nhostname INVENTORY-LAB\ninterface TwentyFiveGigE1/0/1\n!\nNAME: \"Power Supply Module 1\", DESCR: \"Power supply\"\nPID: C9K-PWR-650WAC-R, VID: V01, SN: LAB\nNAME: \"Slot 1 Supervisor\", DESCR: \"Catalyst switch\"\nPID: C9500-48Y4C, VID: V03, SN: LAB\nNAME: \"TwentyFiveGigE1/0/1\", DESCR: \"10G module\"\nPID: SFP-10G-SR, VID: V01, SN: LAB\n");
        Check(inventory.Blockers.Count==0&&inventory.Ports.Count==52,"Appended show inventory uses chassis PID and ignores PSU/transceiver PIDs.");
        var manual=DeviceModels.Parse("version 17.3\nhostname MANUAL-LAB\ninterface TwentyFiveGigE1/0/1\n switchport mode access\n!\n",new[]{new DeviceIdentity{Id="1",ModelSelection="C9500-24Y4C-A"}});
        Check(manual.Blockers.Count==0&&manual.Ports.Count==28,"Per-device Cisco SKU and license suffix select the physical profile.");
        var unknown=Parser.Parse("version 15.0\nhostname C3560-NAME-IS-NOT-INVENTORY\ninterface GigabitEthernet0/1\n switchport access vlan 10\n!\ninterface TenGigabitEthernet0/1\n switchport mode trunk\n!\n");
        Check(unknown.Blockers.Any(b=>b.Contains("uplink"))&&unknown.Ports.Count==0&&unknown.LogicalInterfaces.Any(p=>p.Id=="GigabitEthernet0/1"&&p.UntaggedVlan==10),"3560 unknown converters retain configured interfaces but do not fabricate physical sockets.");
        foreach(string sku in new[]{"r8q71a","R8V12A"})
        {
            var inv=Parser.Parse("!Version AOS-CX ML.10.18.1010\nhostname ARUBA-LAB\nvsf member 1\n    type "+sku+"\nvlan 4\n voice\ninterface 1/1/37\n vlan trunk native 19\n vlan trunk allowed 4,19,21-26\ninterface 1/1/49\n vlan access 19\n");
            Check(inv.Blockers.Count==0&&inv.Ports.Count==52&&inv.Groups.Single().Product==sku.ToUpperInvariant(),"6200M explicit lower/upper-case member is enabled.");
            Check(inv.Ports.Count(p=>p.Media=="RJ-45")==48&&inv.Ports.Count(p=>p.Media=="SFP+")==4,"6200M Smart Rate remains copper; optical ports start at 49.");
            Check(Port(inv,"1/1/37").TaggedVlans.SequenceEqual(new[]{4,21,22,23,24,25,26})&&Port(inv,"1/1/37").VoiceVlan==4,"6200M native/tagged ranges and voice survive.");
            var placements=Charts.Layout(inv);var p49=placements.Single(p=>p.Port=="1/1/49");var p50=placements.Single(p=>p.Port=="1/1/50");var p51=placements.Single(p=>p.Port=="1/1/51");
            Check(p49.Tile.X==p50.Tile.X&&p49.Tile.Y<p50.Tile.Y&&p51.Tile.X>p49.Tile.X,"6200M uplink numbering and odd/even rows.");
        }
        var banner=Parse("banner motd #\ninterface GigabitEthernet1/0/1\n switchport access vlan 777\n#\ninterface GigabitEthernet1/0/1\n switchport mode access\n switchport access vlan 10\n!\n");
        Check(banner.Blockers.Count==0&&banner.Ports[0].UntaggedVlan==10&&!banner.Vlans.Any(v=>v.Id==777),"Banner payload is not interpreted as configuration.");
        if(args.Length>0)
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            string exclusions=string.Join(',',Enumerable.Range(11,25));
            string[] fixtures={Header+"switch 2 provision ws-c3650-48pd\ninterface GigabitEthernet1/0/1\n switchport mode access\n switchport access vlan 10\n switchport voice vlan 20\n!\ninterface Port-channel1\n switchport mode trunk\n switchport trunk allowed vlan 10,20,30-35\n!\ninterface TenGigabitEthernet2/1/4\n channel-group 1 mode active\n!\n", "version 17.3\nhostname POLICY-LAB\n! model C9500-48Y4C\nvlan 10,40,200\n!\ninterface TwentyFiveGigE1/0/1\n switchport mode trunk\n switchport trunk allowed vlan except "+exclusions+"\n!\n"};
            for(int index=0;index<fixtures.Length;index++)
            {
                string input=Path.Combine(root,"fixture-"+index+".txt"),output=Path.Combine(root,"report-"+index);File.WriteAllText(input,fixtures[index]);
                Check(GeneratorEntry.Run(new[]{"--input",input,"--output",output})==0,"Cisco synthetic actual PDF/PNG/CSV export.");
            }
            File.WriteAllText(Path.Combine(root,"expected-policy.txt"),"All except "+exclusions+"; native VLAN 1 is untagged if allowed");
        }
        Console.WriteLine("PASS "+assertions+" Cisco and 6200M inventory, VLAN, alias, aggregation, hardware-identity and geometry assertions.");
    }
}
