using System.Text.Json;
using SwitchportGenerator;

static class CiscoChecks
{
    static int assertions;
    static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    static string Header="version 16.9\nhostname CISCO-LAB\nswitch 1 provision ws-c3650-48pd\n!\n";
    static Inventory Parse(string commands)=>Parser.Parse(Header+commands);
    static Port Port(Inventory inv,string id)=>inv.Ports.Single(p=>p.Id==id);
    static string Configured3560()
    {
        // Synthetic complete software signature. The simultaneous Gi/Te settings
        // below deliberately cannot establish the installed per-bay hardware.
        var commands=new Dictionary<string,string>
        {
            ["GigabitEthernet0/1"]=" switchport mode access\n switchport access vlan 100\n switchport voice vlan 101\n description First fixed port\n",
            ["GigabitEthernet0/48"]=" switchport mode access\n switchport access vlan 200\n description Last fixed port\n no shutdown\n",
            ["GigabitEthernet0/49"]=" switchport mode access\n switchport access vlan 10\n switchport voice vlan 20\n description Upper bay SFP first alternative\n no shutdown\n",
            ["GigabitEthernet0/50"]=" switchport mode trunk\n switchport trunk native vlan 30\n switchport trunk allowed vlan 30-32\n description Upper bay SFP second alternative\n shutdown\n",
            ["GigabitEthernet0/51"]=" switchport mode trunk\n switchport trunk native vlan 50\n switchport trunk allowed vlan 50-52\n channel-group 8 mode passive\n description Lower bay SFP first alternative\n no shutdown\n",
            ["GigabitEthernet0/52"]=" switchport mode access\n switchport access vlan 60\n switchport voice vlan 61\n description Lower bay SFP second alternative\n no shutdown\n",
            ["TenGigabitEthernet0/1"]=" switchport mode trunk\n switchport trunk native vlan 40\n switchport trunk allowed vlan 40-42\n channel-group 7 mode active\n description Upper bay X2 alternative\n shutdown\n",
            ["TenGigabitEthernet0/2"]=" switchport mode trunk\n switchport trunk native vlan 70\n switchport trunk allowed vlan 70-75\n description Lower bay X2 alternative\n shutdown\n"
        };
        string text="version 15.0\nhostname CONFIGURED-UPLINK-LAB\ninterface FastEthernet0\n shutdown\n!\n";
        text+="interface Port-channel7\n switchport mode trunk\n switchport trunk native vlan 140\n switchport trunk allowed vlan 140-142\n!\ninterface Port-channel8\n switchport mode trunk\n switchport trunk native vlan 150\n switchport trunk allowed vlan 150-152\n!\n";
        foreach(string id in Enumerable.Range(1,52).Select(n=>"GigabitEthernet0/"+n).Concat(new[]{"TenGigabitEthernet0/1","TenGigabitEthernet0/2"}))
            text+="interface "+id+"\n"+(commands.TryGetValue(id,out var settings)?settings:"")+"!\n";
        return text;
    }
    static object[] Expected3560()=>new object[]
    {
        new {Port="GigabitEthernet0/1",Native=100,Tagged=new[]{101},Voice=(int?)101,Aggregate="",Disabled=(bool?)null,Description="First fixed port",Configured=false},
        new {Port="GigabitEthernet0/48",Native=200,Tagged=Array.Empty<int>(),Voice=(int?)null,Aggregate="",Disabled=(bool?)false,Description="Last fixed port",Configured=false},
        new {Port="GigabitEthernet0/49",Native=10,Tagged=new[]{20},Voice=(int?)20,Aggregate="",Disabled=(bool?)false,Description="Upper bay SFP first alternative",Configured=true},
        new {Port="GigabitEthernet0/50",Native=30,Tagged=new[]{31,32},Voice=(int?)null,Aggregate="",Disabled=(bool?)true,Description="Upper bay SFP second alternative",Configured=true},
        new {Port="GigabitEthernet0/51",Native=150,Tagged=new[]{151,152},Voice=(int?)null,Aggregate="Po8",Disabled=(bool?)false,Description="Lower bay SFP first alternative",Configured=true},
        new {Port="GigabitEthernet0/52",Native=60,Tagged=new[]{61},Voice=(int?)61,Aggregate="",Disabled=(bool?)false,Description="Lower bay SFP second alternative",Configured=true},
        new {Port="TenGigabitEthernet0/1",Native=140,Tagged=new[]{141,142},Voice=(int?)null,Aggregate="Po7",Disabled=(bool?)true,Description="Upper bay X2 alternative",Configured=true},
        new {Port="TenGigabitEthernet0/2",Native=70,Tagged=new[]{71,72,73,74,75},Voice=(int?)null,Aggregate="",Disabled=(bool?)true,Description="Lower bay X2 alternative",Configured=true}
    };
    static void Configured3560Checks()
    {
        string text=Configured3560();var inv=Parser.Parse(text);
        Check(inv.Blockers.Count==0,"Complete 3560-E software signature can render a qualified configuration view.");
        Check(inv.Groups.Count==2&&inv.Ports.Count==54,"48 fixed ports and six independent configured uplink entries are retained.");
        Check(InventoryBasis.HasConfiguredInterfaces(inv)&&InventoryBasis.ConfirmedPhysicalPortCount(inv)==48&&InventoryBasis.ConfiguredInterfaceCount(inv)==6,"Confirmed physical counts exclude the six unconfirmed alternatives.");
        Check(inv.Groups.Where(g=>!g.ConfiguredInterfaces).Single().Product==CiscoHardware.Shared3560E48Profile,"Automatic signature matching does not invent an exact SKU.");
        Check(!ReportText.SwitchModel(inv).Contains("WS-C3560E-48PD-SF",StringComparison.OrdinalIgnoreCase),"Automatic report identity does not invent PoE or license identity.");
        var logical=inv.Groups.Single(g=>g.ConfiguredInterfaces);
        Check(logical.Id.Contains("unconfirmed",StringComparison.OrdinalIgnoreCase),"Uplink qualification is in its group identity, not only optional media text.");
        Check(inv.Ports.Where(p=>p.Group==logical.Id).All(p=>p.Media=="Unspecified"),"Unconfirmed alternatives use neutral media rather than fabricated SFP/X2 sockets.");
        foreach(var expected in JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(Expected3560())).EnumerateArray())
        {
            var p=Port(inv,expected.GetProperty("Port").GetString()!);
            Check(p.UntaggedVlan==expected.GetProperty("Native").GetInt32()&&p.TaggedVlans.SequenceEqual(expected.GetProperty("Tagged").EnumerateArray().Select(v=>v.GetInt32())),"Each 3560-E software alternative retains its own effective native/tagged values: "+p.Id);
            Check(p.VoiceVlan==(expected.GetProperty("Voice").ValueKind==JsonValueKind.Null?(int?)null:expected.GetProperty("Voice").GetInt32())&&p.Aggregate==expected.GetProperty("Aggregate").GetString(),"Each alternative retains independent voice and channel membership: "+p.Id);
            Check(p.Disabled==(expected.GetProperty("Disabled").ValueKind==JsonValueKind.Null?(bool?)null:expected.GetProperty("Disabled").GetBoolean())&&p.Description==expected.GetProperty("Description").GetString(),"Each alternative retains administrative state and description: "+p.Id);
        }
        Check(!inv.Ports.Any(p=>p.Id=="FastEthernet0")&&!inv.Ports.Where(p=>p.Group==logical.Id).Any(p=>p.RelatedInterfaces.Count>0),"Management is excluded and independent alternatives are not merged as aliases.");
        var map=Charts.Layout(inv);Check(Charts.Validate(inv,map).Count==0,"Qualified 3560-E layout validates as interface geometry.");
        Placement P(string id)=>map.Single(p=>p.Port==id);
        Check(P("GigabitEthernet0/49").Tile.Y==P("GigabitEthernet0/50").Tile.Y&&P("GigabitEthernet0/50").Tile.Y==P("TenGigabitEthernet0/1").Tile.Y,"Upper bay software alternatives share a table row.");
        Check(P("GigabitEthernet0/51").Tile.Y==P("GigabitEthernet0/52").Tile.Y&&P("GigabitEthernet0/52").Tile.Y==P("TenGigabitEthernet0/2").Tile.Y&&P("GigabitEthernet0/51").Tile.Y>P("GigabitEthernet0/49").Tile.Y,"Lower bay alternatives occupy a separate row.");
        Check(P("GigabitEthernet0/49").Tile.X==P("GigabitEthernet0/51").Tile.X&&P("GigabitEthernet0/50").Tile.X==P("GigabitEthernet0/52").Tile.X&&P("TenGigabitEthernet0/1").Tile.X==P("TenGigabitEthernet0/2").Tile.X,"Bay rows use corresponding software columns without claiming six installed cages.");
        var sheet=QuickSheet.Build(inv,map,"CONFIGURED-CHECK");Check(Charts.ValidateScene(sheet.Scene).Count==0&&sheet.Sockets.Count==54,"Qualified 3560-E chart preserves every entry without collisions.");
        Check(sheet.Scene.Shapes.Where(s=>inv.Ports.Any(p=>p.Id==s.Owner&&p.Group==logical.Id)).All(s=>s.Points.Length==4),"Unconfirmed uplink entries have neutral rectangular outlines.");
        string labels=string.Join(' ',sheet.Scene.Labels.Select(l=>l.Text));
        Check(labels.Contains("configured",StringComparison.OrdinalIgnoreCase)&&labels.Contains("unconfirmed",StringComparison.OrdinalIgnoreCase),"Chart visibly qualifies the interface count and unknown installed modules.");
        var selected=DeviceModels.Parse(text,new[]{new DeviceIdentity{Id="1",ModelSelection="WS-C3560E-48PD-SF"}});
        Check(selected.Blockers.Count==0&&InventoryBasis.ConfirmedPhysicalPortCount(selected)==48&&InventoryBasis.ConfiguredInterfaceCount(selected)==6,"An exact manual switch PID preserves the qualified module representation.");
        Check(selected.Groups.Single(g=>!g.ConfiguredInterfaces).Product=="WS-C3560E-48PD-SF"&&selected.Groups.Single(g=>g.ConfiguredInterfaces).Product.Length==0,"Manual model selection identifies the base switch without assigning a fictitious product to configured alternatives.");
        Check(DeviceModels.Choices.Any(c=>c.Sku=="WS-C3560E-48PD-SF"),"Confirmed 3560-E model is selectable in device details.");
        foreach(string partial in new[]{text.Replace("interface TenGigabitEthernet0/2\n", "interface TenGigabitEthernet0/3\n"),text.Replace("interface FastEthernet0\n shutdown\n!\n",""),text.Replace("version 15.0","version 16.9"),text+"interface GigabitEthernet0/53\n!\n"})
            Check(Parser.Parse(partial).Blockers.Count>0,"An incomplete or incompatible signature cannot silently select a configured family view.");
        Check(Parser.Parse(text+"switch 1 provision ws-c3650-48pd\n").Blockers.Count>0&&Parser.Parse(text+"! model C9500-24Y4C\n").Blockers.Count>0,"Contradictory explicit hardware identity remains blocked.");
        Check(Parser.Parse(text.Replace("switchport access vlan 10\n","switchport access vlan 4095\n")).Blockers.Count>0,"Qualified hardware representation does not bypass VLAN validation.");
    }
    static void Configured3560PersistenceChecks()
    {
        var workspace=new ConfigurationWorkspace();var item=workspace.AddText(Configured3560(),"synthetic-3560.cfg","/missing/original/source.cfg");
        workspace.AddText(Header,"physical-3650.cfg");workspace.Select(item.Id);
        workspace.SetDevices(item.Id,new[]{new DeviceIdentity{Id="1",Name="Edited 3560-E",SerialNumber="SYNTHETIC-3560",ModelSelection="WS-C3560E-48PD-SF",Notes="Installed uplink modules unconfirmed"}});
        workspace.OutputLabel="Configured uplink workspace";workspace.ShowWorkspaceTitle=true;
        var exported=JsonSerializer.Deserialize<Inventory>(JsonSerializer.Serialize(item.Inventory))!;
        Check(InventoryBasis.ConfirmedPhysicalPortCount(exported)==48&&InventoryBasis.ConfiguredInterfaceCount(exported)==6&&exported.Groups.Count(g=>g.ConfiguredInterfaces)==1,"Exported inventory JSON retains group basis instead of promoting alternatives to physical sockets.");
        string directory=Path.Combine(Path.GetTempPath(),"configured-3560-persistence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string path=Path.Combine(directory,"qualified.spworkspace.json");workspace.Save(path);
            Check(!File.ReadAllText(path).Contains("ConfiguredInterfaces",StringComparison.Ordinal),"Workspace save persists original configuration and details, not a second derived-inventory cache.");
            var reopened=ConfigurationWorkspace.Load(path);var selected=reopened.Selected!;
            Check(selected.Id==item.Id&&reopened.Items.Count==2&&selected.Text==Configured3560(),"Explicit save/open reconstructs the selected qualified configuration even when its original source path is missing.");
            Check(selected.Inventory.Blockers.Count==0&&InventoryBasis.ConfirmedPhysicalPortCount(selected.Inventory)==48&&InventoryBasis.ConfiguredInterfaceCount(selected.Inventory)==6,"Reopened workspace retains truthful 48+6 qualification.");
            Check(selected.Devices.Single().ModelSelection=="WS-C3560E-48PD-SF"&&selected.Inventory.Devices.Single().SerialNumber=="SYNTHETIC-3560"&&selected.Inventory.Devices.Single().Notes=="Installed uplink modules unconfirmed","Independent operator model, serial and detail edits survive reopening without removing module uncertainty.");
            Check(Port(selected.Inventory,"GigabitEthernet0/51").UntaggedVlan==150&&Port(selected.Inventory,"TenGigabitEthernet0/1").UntaggedVlan==140,"Save/open does not merge alternative-interface VLAN or LAG states.");
            selected.LastOutput=directory;selected.LastChart=Path.Combine(directory,"previous.png");selected.Status="Qualified previous output";selected.StatusType="complete";selected.Dirty=true;reopened.Dirty=true;
            string handoff=JsonSerializer.Serialize(WorkspaceRestartState.Capture(reopened));
            var restored=WorkspaceRestartState.Restore(handoff,p=>p==directory,p=>p==selected.LastChart);var after=restored.Selected!;
            Check(after.Id==selected.Id&&after.LastOutput==directory&&after.LastChart==selected.LastChart&&after.Dirty&&restored.Dirty,"Update/rollback restoration preserves selection, existing output links and unsaved state for the qualified view.");
            Check(after.Inventory.Blockers.Count==0&&InventoryBasis.ConfirmedPhysicalPortCount(after.Inventory)==48&&InventoryBasis.ConfiguredInterfaceCount(after.Inventory)==6&&after.Inventory.Groups.Single(g=>g.ConfiguredInterfaces).Id.Contains("unconfirmed",StringComparison.OrdinalIgnoreCase),"Update handoff reparses and restores the unconfirmed module basis.");
            Check(Port(after.Inventory,"GigabitEthernet0/49").VoiceVlan==20&&Port(after.Inventory,"TenGigabitEthernet0/1").Aggregate=="Po7"&&Port(after.Inventory,"GigabitEthernet0/51").Aggregate=="Po8","Restart restoration preserves each independent alternative's voice/channel settings.");
            var legacy=new WorkspaceRestartState.LegacySession(Configured3560(),"legacy-3560.cfg",selected.Devices.ToArray(),null,null);
            var migrated=WorkspaceRestartState.Restore(JsonSerializer.Serialize(legacy),_=>false,_=>false);
            Check(migrated.Selected!.Inventory.Blockers.Count==0&&InventoryBasis.ConfirmedPhysicalPortCount(migrated.Selected.Inventory)==48&&InventoryBasis.ConfiguredInterfaceCount(migrated.Selected.Inventory)==6,"A legacy one-configuration update session also restores qualified 3560-E rendering.");
        }
        finally {Directory.Delete(directory,true);}
    }
    static void Main(string[] args)
    {
        foreach(var model in CiscoHardware.Models)
        {
            string version=model.Code.StartsWith("WS-C3560E-",StringComparison.Ordinal)?"15.0":"17.3";
            var inv=Parser.Parse("version "+version+"\nhostname MODEL-LAB\ninterface "+(version=="15.0"?"FastEthernet0":"GigabitEthernet0/0")+"\n shutdown\n!\n",model.Code);
            Check(inv.Blockers.Count==0,model.Code+" imports by explicit model.");
            Check(inv.Ports.Count==model.Sockets.Count,model.Code+" represented interface count.");
            var map=Charts.Layout(inv);Check(Charts.Validate(inv,map).Count==0,model.Code+" base geometry.");
            var sheet=QuickSheet.Build(inv,map,"CISCO-CHECK");Check(Charts.ValidateScene(sheet.Scene).Count==0,model.Code+" A4 scene.");
            Check(sheet.Sockets.Count==inv.Ports.Count,model.Code+" each represented entry appears once.");
        }
        Configured3560Checks();
        Configured3560PersistenceChecks();
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
        var staleVoice=Parse("interface GigabitEthernet1/0/1\n switchport mode trunk\n switchport trunk allowed vlan 10,20\n switchport voice vlan 30\n!\n");
        Check(staleVoice.Ports[0].VoiceVlan==null&&staleVoice.Ports[0].TaggedVlans.SequenceEqual(new[]{10,20}),"Inactive access voice settings cannot bypass a trunk allowed list.");
        foreach(bool reverse in new[]{false,true})
        {
            string stub="interface GigabitEthernet1/1/3\n!\n",configured="interface TenGigabitEthernet1/1/3\n switchport mode access\n switchport access vlan 10\n!\n";
            var alias=Parse(reverse?configured+stub:stub+configured);
            Check(alias.Blockers.Count==0&&alias.Ports.Count==52&&Port(alias,"TenGigabitEthernet1/1/3").UntaggedVlan==10,"Unused alias stubs do not double count or override a configured cage.");
        }
        Check(Parse("interface GigabitEthernet1/1/3\n switchport access vlan 20\n!\ninterface TenGigabitEthernet1/1/3\n switchport access vlan 10\n!\n").Blockers.Any(b=>b.Contains("aliases")),"Conflicting uplink aliases require active-interface confirmation.");
        foreach(string bad in new[]{"switchport access vlan 4095","switchport trunk allowed vlan 20-10","switchport trunk allowed vlan remove all","switchport voice vlan dot1p","switchport trunk encapsulation isl","switchport private-vlan host-association 10 20"})
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
        Check(DeviceModels.Describe("version 17.3\nhostname PARTIAL\ninterface TwentyFiveGigE2/0/1\n",new Inventory()).Any(d=>d.Id=="2"),"Unidentified Cisco members remain selectable in device details.");
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
            string[] fixtures={Header+"switch 2 provision ws-c3650-48pd\ninterface GigabitEthernet1/0/1\n switchport mode access\n switchport access vlan 10\n switchport voice vlan 20\n!\ninterface Port-channel1\n switchport mode trunk\n switchport trunk allowed vlan 10,20,30-35\n!\ninterface TenGigabitEthernet2/1/4\n channel-group 1 mode active\n!\n", "version 17.3\nhostname POLICY-LAB\n! model C9500-48Y4C\nvlan 10,40,200\n!\ninterface TwentyFiveGigE1/0/1\n switchport mode trunk\n switchport trunk allowed vlan except "+exclusions+"\n!\n",Configured3560(),Configured3560()+"NAME: \"Switch 1\", DESCR: \"3560-E test switch\"\nPID: WS-C3560E-48PD-SF, VID: V01, SN: SYNTHETIC\n"};
            for(int index=0;index<fixtures.Length;index++)
            {
                string input=Path.Combine(root,"fixture-"+index+".txt"),output=Path.Combine(root,"report-"+index);File.WriteAllText(input,fixtures[index]);
                Check(GeneratorEntry.Run(new[]{"--input",input,"--output",output})==0,"Cisco synthetic actual PDF/PNG/CSV export.");
            }
            File.WriteAllText(Path.Combine(root,"expected-policy.txt"),"All except "+exclusions+"; native VLAN 1 is untagged if allowed");
            File.WriteAllText(Path.Combine(root,"expected-3560.json"),JsonSerializer.Serialize(Expected3560()));
        }
        Console.WriteLine("PASS "+assertions+" Cisco and 6200M inventory, VLAN, alias, aggregation, hardware-identity and geometry assertions.");
    }
}
