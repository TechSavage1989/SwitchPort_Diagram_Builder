using System.Text.Json;
using SwitchportGenerator;

static class ModuleChecks
{
    static int assertions;
    static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    static void Reject(Action action,string message)
    {
        try{action();}catch(InvalidOperationException){Check(true,message);return;}
        throw new Exception(message);
    }
    static DeviceIdentity Row(string id,params (string Slot,string Sku)[] modules)=>new()
    {Id=id,ModuleSelections=modules.Select(m=>new DeviceModuleSelection{Slot=m.Slot,Sku=m.Sku}).ToList()};
    static Inventory Selected(string text,params DeviceIdentity[] devices)=>DeviceModels.Parse(text,devices);
    static DeviceIdentity Parent(Inventory inventory,string id="1")=>DeviceModels.Describe("",inventory).Single(d=>d.Id==id);
    static Port Port(Inventory inventory,string id)=>inventory.Ports.Single(p=>p.Id==id);
    static string DenseVlans=>string.Join(',',Enumerable.Range(0,40).Select(n=>101+2*n));
    const string Chassis="; J9821A Configuration Editor\nhostname MODULE-CHASSIS-LAB\n";
    const string Rear="; hpStack_WB Configuration Editor\nhostname MODULE-REAR-LAB\nstacking\n member 1 type \"J9726A\"\n exit\ninterface 1/A1\n name \"Configured rear interface\"\n exit\nvlan 10\n untagged 1/A1\n exit\n";
    static string Cisco()
    {
        var settings=new Dictionary<string,string>
        {
            ["GigabitEthernet0/49"]=" switchport mode trunk\n switchport trunk native vlan 10\n switchport trunk allowed vlan 10,"+DenseVlans+"\n description Selected converter first port retains every tagged VLAN\n no shutdown\n",
            ["GigabitEthernet0/50"]=" switchport mode access\n switchport access vlan 20\n switchport voice vlan 21\n description Selected converter second independent port\n shutdown\n",
            ["GigabitEthernet0/51"]=" switchport mode access\n switchport access vlan 30\n switchport voice vlan 31\n description Inactive lower converter first alternative\n",
            ["GigabitEthernet0/52"]=" switchport mode access\n switchport access vlan 40\n description Inactive lower converter second alternative\n",
            ["TenGigabitEthernet0/1"]=" switchport mode trunk\n switchport trunk native vlan 50\n switchport trunk allowed vlan 50,211,223,227,229,233\n description Inactive upper X2 alternative remains visible\n shutdown\n",
            ["TenGigabitEthernet0/2"]=" channel-group 7 mode active\n description Selected lower X2 interface\n no shutdown\n"
        };
        string text="version 15.0\nhostname MODULE-CISCO-LAB\ninterface FastEthernet0\n shutdown\n!\ninterface Port-channel7\n switchport mode trunk\n switchport trunk native vlan 60\n switchport trunk allowed vlan 60,71,73,79,83,89\n!\n";
        foreach(string id in Enumerable.Range(1,52).Select(n=>"GigabitEthernet0/"+n).Concat(new[]{"TenGigabitEthernet0/1","TenGigabitEthernet0/2"}))
            text+="interface "+id+"\n"+(settings.TryGetValue(id,out string? value)?value:"")+"!\n";
        return text;
    }
    static void SlotsAndCompatibility()
    {
        var chassis=Selected(Chassis);
        var slots=DeviceModules.Slots(chassis,Parent(chassis));
        Check(slots.Select(s=>s.Slot).SequenceEqual(new[]{"A","B","C","D","E","F"}),"A six-slot chassis offers every real bay, including unpopulated slots.");
        Check(slots.All(s=>s.Options.Any(o=>o.Sku=="J9986A")),"Compatible zl2 copper modules are offered in all chassis slots.");
        Check(slots.SelectMany(s=>s.Options).All(o=>o.Source.StartsWith("https://")&&o.LayoutSource.StartsWith("https://")),"Every selectable module carries manufacturer identity and geometry evidence.");
        Check(!slots.SelectMany(s=>s.Options).Any(o=>o.Sku=="J9731A"),"Rear 2920 cards are excluded from chassis module choices.");
        foreach(var invalid in new[]{Row("1",("G","J9986A")),Row("1",("A","J9731A")),Row("1",("A","JL256A")),Row("1",("A","UNVERIFIED-MODULE")),Row("1",("A","J9986A"),("a","J9993A"))})
            Reject(()=>Selected(Chassis,invalid),"Invalid slot, incompatible/fixed/unknown model and duplicate slot choices are rejected.");
        var declared=Selected(Chassis+"module A type J9986A\n");
        Check(DeviceModules.Slots(declared,Parent(declared)).Single(s=>s.Slot=="A").DetectedSku=="J9986A","Detected slot identity is exposed separately from an operator selection.");
        Check(Selected(Chassis+"module A type J9986A\n",Row("1",("A","J9986A"))).Blockers.Count==0,"Confirming a detected module is allowed.");
        Reject(()=>Selected(Chassis+"module A type J9986A\n",Row("1",("A","J9993A"))),"Operator choices cannot replace a contradictory module PID in the source.");
        foreach(string text in new[]{"version 16.9\nhostname FIXED-LAB\nswitch 1 provision ws-c3650-48pd\n","version 17.3\nhostname FIXED-LAB\n! model C9500-48Y4C\n","; JL256A Configuration Editor\n"})
        {
            var inv=Selected(text);
            Check(DeviceModules.Slots(inv,Parent(inv)).Count==0,"Fixed-uplink switches do not acquire invented network-module bays.");
            Reject(()=>Selected(text,Row("1",("A","J9986A"))),"Module selections are rejected on switches without that bay.");
        }
        Reject(()=>Selected(Chassis,Row("2",("A","J9986A"))),"Module choices are restricted to the device they belong to.");
        var canonical=Selected(Chassis,Row("1",(" a "," j9986a ")));
        Check(canonical.Blockers.Count==0&&canonical.Devices.Single(d=>d.Id=="1").ModuleSelections.Single().Slot=="A"&&canonical.Devices.Single(d=>d.Id=="1").ModuleSelections.Single().Sku=="J9986A","Persisted slot and SKU values are normalized consistently.");
        foreach(var invalid in new[]{new DeviceIdentity{Id="1",ModuleSelections=null!},new DeviceIdentity{Id="1",ModuleSelections=new(){null!}},Row("1",("A\n","J9986A")),Row("1",("A",new string('X',81))),Row("1",("","J9986A"))})
            Reject(()=>Selected(Chassis,invalid),"Malformed persisted module metadata is rejected with a clear validation error.");
    }
    static void CatalogSweep()
    {
        var covered=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string fixture in new[]{"; J4850A Configuration Editor\n","; J4819A Configuration Editor\n","; J8697A Configuration Editor\n",Chassis,"; J8715A Configuration Editor\n","; JL075A Configuration Editor\nmodule 1 type JL075A\n","; JL076A Configuration Editor\nmodule 1 type JL076A\n","; J9726A Configuration Editor\nmodule 1 type J9726A\n"})
        {
            var baseline=Selected(fixture);var parent=Parent(baseline);var slot=DeviceModules.Slots(baseline,parent).FirstOrDefault();
            Check(slot!=null,"Each researched modular family exposes its physical slot choices.");
            foreach(var option in slot!.Options)
            {
                var inv=Selected(fixture,Row("1",(slot.Slot,option.Sku)));
                Check(inv.Blockers.Count==0,"Every offered compatible module can establish hardware: "+option.Sku+" on "+fixture.Split('\n')[0]);
                Check(inv.Groups.Any(g=>g.Slot==slot.Slot&&g.Product==option.Sku)&&inv.Ports.Count>0,"Every module choice supplies real sockets in the requested bay: "+option.Sku);
                var map=Charts.Layout(inv);
                Check(Charts.Validate(inv,map).Count==0,"Every selectable module has valid port geometry: "+option.Sku);
                covered.Add(option.Sku);
            }
        }
        Check(covered.Count>=41,"The module catalog covers all researched HPE/Aruba interface cards across supported families.");
        Console.WriteLine("Verified selectable module catalog sweep: "+covered.Count+" distinct HPE/Aruba cards.");
    }
    static void HpeSelection()
    {
        string text=Chassis+"vlan 10\n untagged A1\n exit\nvlan 20\n tagged A1,B1\n exit\n";
        var inv=Selected(text,Row("1",("A","J9986A"),("B","J9993A")));
        Check(inv.Blockers.Count==0&&inv.Groups.Count==2,"An operator can establish multiple previously unreported installed chassis cards.");
        Check(inv.Groups.Single(g=>g.Slot=="A").Product=="J9986A"&&inv.Groups.Single(g=>g.Slot=="B").Product=="J9993A","Every selected module retains its slot and exact PID.");
        Check(inv.Ports.Count==32&&Port(inv,"A1").Media=="RJ-45"&&Port(inv,"B1").Media=="SFP+","Selected chassis cards control actual socket counts and media.");
        Check(Port(inv,"A1").UntaggedVlan==10&&Port(inv,"A1").TaggedVlans.SequenceEqual(new[]{20})&&Port(inv,"B1").TaggedVlans.SequenceEqual(new[]{20}),"Module selection reparses VLAN membership against the established physical inventory.");
        Check(DeviceModules.Slots(inv,inv.Devices.Single(d=>d.Id=="1")).Single(s=>s.Slot=="A").SelectedSku=="J9986A","Applied choices remain selected when device details is reopened.");
        var automatic=Selected(Chassis+"module A type J9986A\n",Row("1",("A","")));
        Check(automatic.Blockers.Count==0&&automatic.Groups.Single().Product=="J9986A","An empty choice reverts to source-detected inventory.");
        var numeric=Selected(Chassis+"module 1 type J9986A\nvlan 10\n untagged A1\n exit\n",Row("1",("A","J9986A")));
        Check(numeric.Blockers.Count==0&&numeric.Groups.Count==1&&numeric.Ports.Count==24&&numeric.Ports.Select(p=>p.Id).Distinct().Count()==24&&Port(numeric,"A1").UntaggedVlan==10,"Confirming a numeric source slot uses the existing canonical bay without duplicating its interfaces.");
        var manualRow=Row("1",("B","J9993A"));manualRow.ModelSelection="J9821A";
        var manualChassis=Selected("; Aruba Configuration Editor\nmodule A type J9986A\n",manualRow);
        Check(manualChassis.Blockers.Count==0&&manualChassis.ChassisSlots==6&&manualChassis.Groups.Count==2&&manualChassis.Ports.Count==32,"An operator chassis model and newly supplied module bay work together without losing capacity or detected modules.");
        var rear=Selected(Rear,Row("1",("A","J9731A"),("B","J9732A")));
        Check(rear.Blockers.Count==0&&rear.Groups.Count(g=>g.Rear)==2,"Independent 2920 rear bays can be selected alongside a fixed switch header.");
        Check(Port(rear,"1/A1").Media=="SFP+"&&Port(rear,"1/B1").Media=="RJ-45"&&Port(rear,"1/A1").UntaggedVlan==10,"Rear module choices set media and preserve existing rear-port settings.");
        var flex=Selected("; JL071A Configuration Editor\nmodule 1 type JL071A\n",Row("1",("A","JL083A")));
        Check(flex.Blockers.Count==0&&flex.Groups.Any(g=>g.Slot=="A"&&g.Product=="JL083A"),"3810M flexible uplink inventory accepts its compatible installed module.");
        Reject(()=>Selected("; JL071A Configuration Editor\nmodule 1 type JL071A\n",Row("1",("B","JL083A"))),"3810M single-bay bases do not offer a second bay.");
        string stack="; hpStack_KB Configuration Editor\nstacking\n member 1 type JL075A\n member 2 type JL075A\n exit\n";
        var stacked=Selected(stack,Row("1",("A","JL083A")),Row("2",("B","JL078A")));
        Check(stacked.Blockers.Count==0&&stacked.Groups.Any(g=>g.Device=="1"&&g.Slot=="A"&&g.Product=="JL083A")&&stacked.Groups.Any(g=>g.Device=="2"&&g.Slot=="B"&&g.Product=="JL078A"),"Stack member module choices remain independent and use their real member/slot identity.");
        Reject(()=>Selected("; JL075A Configuration Editor\nmodule 1 type JL075A\n",Row("1",("A","JL079A"))),"The dual-QSFP card is not offered on the unsupported 16-SFP+ 3810M base.");
        var qsfp=Selected("; JL076A Configuration Editor\nmodule 1 type JL076A\n",Row("1",("A","JL079A")));
        Check(qsfp.Blockers.Count==0&&qsfp.Groups.Any(g=>g.Slot=="A"&&g.Product=="JL079A"),"The dual-QSFP card is available on its explicitly supported 3810M base.");
        Reject(()=>Selected(Chassis+"no allow-v2-modules\n",Row("1",("A","J9538A"))),"A new v2 card cannot bypass an explicit chassis compatibility prohibition.");
        Reject(()=>Selected(Chassis+"vsf\n",Row("1",("A","J9538A"))),"New v2 cards are rejected in a 5400R VSF configuration.");
        Check(Selected(Chassis+"no allow-v2-modules\nmodule A type J9538A\n",Row("1",("A","J9538A"))).Blockers.Count==0,"Confirming a source-detected v2 card preserves existing inventory semantics.");
        Reject(()=>Selected("; J8697A Configuration Editor\nno allow-v1-modules\n",Row("1",("A","J8705A"))),"A new v1 card cannot bypass an explicit old-chassis compatibility prohibition.");
        Check(Selected("; J8697A Configuration Editor\nno allow-v1-modules\nmodule A type J8705A\n",Row("1",("A","J8705A"))).Blockers.Count==0,"Confirming an existing v1 source card retains its established physical inventory.");
        Reject(()=>Selected("; JL071A Configuration Editor\n; Created on release #KB.16.04.0007\nmodule 1 type JL071A\n",Row("1",("A","JL081A"))),"An explicitly older 3810M firmware cannot silently accept a module whose minimum release is newer.");
        Check(Selected("; JL071A Configuration Editor\n; Created on release #KB.16.04.0008\nmodule 1 type JL071A\n",Row("1",("A","JL081A"))).Blockers.Count==0,"The minimum supported firmware accepts the multigigabit 3810M card.");
        var map=Charts.Layout(inv);Check(Charts.Validate(inv,map).Count==0&&Charts.ValidateScene(QuickSheet.Build(inv,map,"MODULE-CHECK").Scene).Count==0,"Selected modules retain validated chassis geometry and printable A4 layout.");
    }
    static void CiscoSelection()
    {
        string text=Cisco();var baseline=Selected(text);
        Check(baseline.Blockers.Count==0&&InventoryBasis.ConfirmedPhysicalPortCount(baseline)==48&&InventoryBasis.ConfiguredInterfaceCount(baseline)==6,"Existing unselected 3560-E views keep the 48-base-plus-six-alternative basis.");
        var options=DeviceModules.Slots(baseline,Parent(baseline));
        Check(options.Select(s=>s.Slot).SequenceEqual(new[]{"X2-1","X2-2"})&&options.All(s=>s.Options.Any(o=>o.Sku=="CVR-X2-SFP")&&s.Options.Any(o=>o.Sku=="X2-10GB-SR")),"Both 3560-E bays offer verified TwinGig and native X2 choices.");
        Check(options.All(s=>s.Options.Count>=9&&s.Options.Any(o=>o.Sku=="CVR-X2-SFP10G")),"3560-E choices include verified TwinGig, OneX and X2 optic PIDs.");
        foreach(var slot in options)foreach(var option in slot.Options)
        {
            var chosen=Selected(text,Row("1",(slot.Slot,option.Sku)));bool twin=option.Sku=="CVR-X2-SFP";
            Check(chosen.Blockers.Count==0&&chosen.Ports.Count==54&&InventoryBasis.ConfirmedPhysicalPortCount(chosen)==(twin?50:49)&&InventoryBasis.ConfiguredInterfaceCount(chosen)==(twin?4:5),"Every verified 3560-E bay choice retains exact physical and alternative counts: "+slot.Slot+" / "+option.Sku);
            if(option.Sku=="CVR-X2-SFP10G")Check(Port(chosen,slot.Slot=="X2-1"?"TenGigabitEthernet0/1":"TenGigabitEthernet0/2").Media=="SFP+","The OneX converter establishes an SFP+ cage rather than an X2 optical connector.");
            Check(Charts.Validate(chosen,Charts.Layout(chosen)).Count==0,"Each 3560-E module choice has valid installed and configured geometry.");
        }
        var row=Row("1",("X2-1","CVR-X2-SFP"),("X2-2","X2-10GB-SR"));
        var inv=Selected(text,row);
        Check(inv.Blockers.Count==0&&inv.Ports.Count==54,"Selecting mixed bay modes preserves every independent software interface.");
        Check(InventoryBasis.ConfirmedPhysicalPortCount(inv)==51&&InventoryBasis.ConfiguredInterfaceCount(inv)==3,"Two selected SFPs plus one selected X2 count as three installed physical uplinks, with three inactive alternatives kept separate.");
        foreach(string id in new[]{"GigabitEthernet0/49","GigabitEthernet0/50","TenGigabitEthernet0/2"})
            Check(!InventoryBasis.IsConfiguredInterface(inv,Port(inv,id)),"The selected mode establishes installed sockets for "+id);
        foreach(string id in new[]{"GigabitEthernet0/51","GigabitEthernet0/52","TenGigabitEthernet0/1"})
            Check(InventoryBasis.IsConfiguredInterface(inv,Port(inv,id)),"Inactive mode settings stay qualified as configured alternatives for "+id);
        Check(Port(inv,"GigabitEthernet0/49").Media=="SFP"&&Port(inv,"GigabitEthernet0/50").Media=="SFP"&&Port(inv,"TenGigabitEthernet0/2").Media=="X2","Module choice controls installed connector geometry and media.");
        foreach(var original in baseline.Ports)
        {
            var p=Port(inv,original.Id);
            Check(p.UntaggedVlan==original.UntaggedVlan&&p.TaggedVlans.SequenceEqual(original.TaggedVlans)&&p.VoiceVlan==original.VoiceVlan&&p.Aggregate==original.Aggregate&&p.Disabled==original.Disabled&&p.Description==original.Description,"Module choice never merges or drops interface settings: "+p.Id);
        }
        Check(Port(inv,"GigabitEthernet0/50").VoiceVlan==21&&Port(inv,"TenGigabitEthernet0/2").Aggregate=="Po7"&&Port(inv,"TenGigabitEthernet0/1").UntaggedVlan==50,"Voice, LAG inheritance and inactive native VLANs remain independent.");
        Check(Port(inv,"GigabitEthernet0/49").TaggedVlans.SequenceEqual(Enumerable.Range(0,40).Select(n=>101+2*n)),"A selected converter retains the complete exact tagged VLAN set from the source.");
        var partial=Selected(text,Row("1",("X2-1","CVR-X2-SFP")));
        Check(InventoryBasis.ConfirmedPhysicalPortCount(partial)==50&&InventoryBasis.ConfiguredInterfaceCount(partial)==4,"Selecting one bay leaves only the other bay unconfirmed plus the inactive selected-bay interface.");
        var restored=Selected(text,Row("1",("X2-1",""),("X2-2","")));
        Check(InventoryBasis.ConfirmedPhysicalPortCount(restored)==48&&InventoryBasis.ConfiguredInterfaceCount(restored)==6,"Reverting module choices restores the original qualified view.");
        foreach(var invalid in new[]{Row("1",("3","CVR-X2-SFP")),Row("1",("X2-1","J9731A")),Row("1",("X2-1","CVR-X2-SFP"),("X2-1","X2-10GB-SR")),Row("1",("X2-1","SFP-10G-SR"))})
            Reject(()=>Selected(text,invalid),"Invalid Cisco bay, foreign card, duplicate bay and incompatible transceiver choices are rejected.");
        var bad=Selected(text.Replace("switchport access vlan 20\n","switchport access vlan 4095\n"),row);
        Check(bad.Blockers.Count>0,"Installed module selections never bypass invalid VLAN validation.");
        string detectedSource=text+"NAME: \"TenGigabitEthernet0/1\", DESCR: \"Synthetic TwinGig converter\"\nPID: CVR-X2-SFP, VID: V01, SN: SYNTHETIC\n";
        var detected=Selected(detectedSource,Row("1",("X2-1","CVR-X2-SFP")));
        Check(detected.Blockers.Count==0&&detected.Ports.Count==54&&InventoryBasis.ConfirmedPhysicalPortCount(detected)==50&&detected.Ports.Select(p=>p.Id).Distinct().Count()==54,"Confirming a show-inventory converter is idempotent and cannot duplicate its installed sockets.");
        var detectedSlot=DeviceModules.Slots(detected,detected.Devices.Single(d=>d.Id=="1")).Single(s=>s.Slot=="X2-1");
        Check(detectedSlot.DetectedSku=="CVR-X2-SFP"&&detectedSlot.SelectedSku=="CVR-X2-SFP","A confirmed detected converter retains both source and operator provenance.");
        Reject(()=>Selected(detectedSource,Row("1",("X2-1","X2-10GB-SR"))),"An operator converter choice cannot contradict its bay's show-inventory PID.");
        var map=Charts.Layout(inv);Check(Charts.Validate(inv,map).Count==0&&Charts.ValidateScene(QuickSheet.Build(inv,map,"MODULE-CHECK").Scene).Count==0,"Mixed known physical uplinks and qualified alternatives retain readable geometry.");
    }
    static string CiscoHeader(string model)=>"version 16.9\nhostname MODULE-FAMILY-LAB\nswitch 1 provision "+model.ToLowerInvariant()+"\n!\n";
    static void CiscoCatalogSweep()
    {
        var modules=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var model in CiscoModuleCatalog.BaseModels)
        {
            string text=CiscoHeader(model.Code);var baseline=Selected(text);
            int fixedPorts=model.Sockets.Count(s=>!s.ConfiguredInterface);
            Check(baseline.Blockers.Count==0&&baseline.Ports.Count==fixedPorts,"An explicit base with no optional-module evidence draws only its confirmed base sockets: "+model.Code);
            var parent=Parent(baseline);var slot=DeviceModules.Slots(baseline,parent).Single();
            foreach(var option in slot.Options)
            {
                var inv=Selected(text,Row("1",(slot.Slot,option.Sku)));
                Check(inv.Blockers.Count==0,"Every offered Cisco card can be selected on its verified base: "+model.Code+" / "+option.Sku);
                Check(inv.ModuleInventory!.Single().Sku==option.Sku&&inv.ModuleInventory!.Single().OperatorSupplied,"Explicit Cisco module evidence retains operator provenance.");
                Check(inv.Ports.Select(p=>p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==inv.Ports.Count,"Selected Cisco card never creates duplicate interface identities: "+model.Code+" / "+option.Sku);
                Check(InventoryBasis.ConfiguredInterfaceCount(inv)==0,"A header-only backup has no invented configured alternatives: "+model.Code+" / "+option.Sku);
                var map=Charts.Layout(inv);Check(Charts.Validate(inv,map).Count==0,"Every compatible Cisco module has valid installed geometry: "+model.Code+" / "+option.Sku);
                modules.Add(option.Sku);
            }
        }
        Check(modules.Count>=20,"Network module selection covers the researched 3KX, 3850, 9200 and 9300 card families.");
        Console.WriteLine("Verified Cisco module catalog sweep: "+CiscoModuleCatalog.BaseModels.Count+" bases and "+modules.Count+" distinct network module PIDs.");
        string source=CiscoHeader("WS-C3850-48P")+"interface GigabitEthernet1/1/1\n switchport mode access\n switchport access vlan 10\n!\ninterface TenGigabitEthernet1/1/1\n switchport mode access\n switchport access vlan 20\n!\n";
        var selected=Selected(source,Row("1",("NM","C3850-NM-4-10G")));
        Check(selected.Blockers.Count==0&&Port(selected,"GigabitEthernet1/1/1").UntaggedVlan==10&&Port(selected,"TenGigabitEthernet1/1/1").UntaggedVlan==20,"Different-speed alternatives on a modular 3850 retain independent saved VLAN settings.");
        Check(InventoryBasis.IsConfiguredInterface(selected,Port(selected,"GigabitEthernet1/1/1"))&&!InventoryBasis.IsConfiguredInterface(selected,Port(selected,"TenGigabitEthernet1/1/1")),"Only the selected network card's interface becomes an installed socket.");
        var blank=Selected(source,Row("1",("NM","C3850-NM-BLANK")));
        Check(blank.Blockers.Count==0&&InventoryBasis.ConfirmedPhysicalPortCount(blank)==48&&InventoryBasis.ConfiguredInterfaceCount(blank)==2,"A blank module establishes no uplink sockets while preserving every saved alternative.");
        Reject(()=>Selected(CiscoHeader("WS-C3850-24T"),Row("1",("NM","C3850-NM-4-10G"))),"A 24-port copper 3850 cannot accept a module reserved for larger compatible bases.");
        string all3850Stubs=CiscoHeader("WS-C3850-24T")+string.Concat(new[]{"GigabitEthernet","TenGigabitEthernet"}.SelectMany(kind=>Enumerable.Range(1,4).Select(n=>"interface "+kind+"1/1/"+n+"\n!\n")));
        var unknown3850=Selected(all3850Stubs);
        Check(unknown3850.Blockers.Count==0&&unknown3850.Ports.Count==32&&InventoryBasis.ConfirmedPhysicalPortCount(unknown3850)==24&&InventoryBasis.ConfiguredInterfaceCount(unknown3850)==8,"All eight documented 24-port 3850 CLI uplink stubs remain configured alternatives rather than causing a false hardware block.");
        var known3850=Selected(all3850Stubs,Row("1",("NM","C3850-NM-2-10G")));
        Check(known3850.Blockers.Count==0&&known3850.Ports.Count==32&&InventoryBasis.ConfirmedPhysicalPortCount(known3850)==28&&InventoryBasis.ConfiguredInterfaceCount(known3850)==4,"A selected 2-10G card establishes only its four sockets while retaining the other 3850 CLI stubs.");
        Check(Port(known3850,"GigabitEthernet1/1/1").Media=="SFP"&&Port(known3850,"TenGigabitEthernet1/1/3").Media=="SFP+"&&InventoryBasis.IsConfiguredInterface(known3850,Port(known3850,"TenGigabitEthernet1/1/1")),"The 3850 mixed network module uses the documented SFP/SFP+ interface numbering.");
        Reject(()=>Selected(CiscoHeader("WS-C3850-48P"),Row("1",("NM","C3850-NM-8-10G"))),"An eight-port module is limited to its verified 24XS base.");
        Reject(()=>Selected(CiscoHeader("C9200-48P"),Row("1",("NM","C9300-NM-8X"))),"Similar-looking uplink module families are not treated as interchangeable.");
        string inventory="NAME: \"Switch 1 Uplink Module\", DESCR: \"Synthetic 3850 network module\"\nPID: C3850-NM-2-10G, VID: V01, SN: SYNTHETIC\n";
        var detected=Selected(CiscoHeader("WS-C3850-48P")+inventory);
        Check(detected.Blockers.Count==0&&detected.ModuleInventory!.Single().Sku=="C3850-NM-2-10G"&&!detected.ModuleInventory!.Single().OperatorSupplied,"Show inventory establishes a Cisco network card independently of operator edits.");
        Check(DeviceModules.Slots(detected,Parent(detected)).Single().DetectedSku=="C3850-NM-2-10G","Source-detected Cisco module identity is available to the dropdown.");
        Reject(()=>Selected(CiscoHeader("WS-C3850-48P")+inventory,Row("1",("NM","C3850-NM-4-10G"))),"A selected Cisco card cannot contradict show inventory.");
        string unassigned="NAME: \"Uplink Module\", DESCR: \"Synthetic unassigned network module\"\nPID: C3850-NM-2-10G, VID: V01, SN: SYNTHETIC\n";
        Check(Selected(CiscoHeader("WS-C3850-48P")+"switch 2 provision ws-c3850-48p\n"+unassigned).Blockers.Count>0,"An unassigned module PID in a stack cannot silently attach to an arbitrary member.");
        foreach(var evidence in new[]{(Base:"C9200-48P",Card:"C9200-NM-4X",Count:52),(Base:"C9300-48T",Card:"C9300-NM-2Y",Count:50)})
        {
            string raw="version 17.3\nhostname RAW-INVENTORY-LAB\ninterface GigabitEthernet1/0/1\n switchport mode access\n switchport access vlan 10\n!\n  NAME: \"Switch 1\", DESCR: \"Synthetic Catalyst chassis\"\n  PID: "+evidence.Base+", VID: V01, SN: SYNTHETIC-BASE\n  NAME: \"Switch 1 Uplink Module\", DESCR: \"Synthetic network module\"\n  PID: "+evidence.Card+", VID: V01, SN: SYNTHETIC-MODULE\n";
            var inv=Selected(raw);
            Check(inv.Blockers.Count==0&&inv.Ports.Count==evidence.Count&&inv.Groups.Any(g=>g.Slot.Length==0&&g.Product==evidence.Base)&&inv.ModuleInventory!.Single().Sku==evidence.Card,"Raw indented show inventory distinguishes the base PID from the similarly prefixed network module PID: "+evidence.Base);
        }
        string unknown="NAME: \"Switch 1 Uplink Module\", DESCR: \"Unverified network module\"\nPID: C9300-NM-99X, VID: V01, SN: SYNTHETIC\n";
        Check(Selected(CiscoHeader("C9300-48T")+unknown,Row("1",("NM","C9300-NM-2Y"))).Blockers.Count>0,"A manual card selection cannot override an explicit unsupported source module PID.");
        string payload="NAME: \"Switch 1\", DESCR: \"Not actual inventory\"\nPID: C9300-48T, VID: V01, SN: SYNTHETIC\n"+unknown;
        foreach(string wrapped in new[]{"banner motd #\n"+payload+"#\n","crypto pki certificate chain SYNTHETIC\n"+payload+"quit\n"})
        {
            var inv=Selected(CiscoHeader("WS-C3850-48P")+wrapped);
            Check(inv.Blockers.Count==0&&inv.Ports.Count==48&&!(inv.ModuleInventory?.Any()??false),"Banner and certificate payloads cannot establish or contradict installed module inventory.");
        }
    }
    static void Persistence(string root)
    {
        var workspace=new ConfigurationWorkspace();var item=workspace.AddText(Cisco(),"synthetic-modules.cfg","/missing/original.cfg");
        var row=Row("1",("X2-1","CVR-X2-SFP"),("X2-2","X2-10GB-SR"));row.Name="Module persistence lab";row.Notes="Independent device details";
        workspace.SetDevices(item.Id,new[]{row});
        row.ModuleSelections[0].Sku="X2-10GB-SR";row.ModuleSelections.Clear();
        Check(item.Devices.Single(d=>d.Id=="1").ModuleSelections.Count==2&&Port(item.Inventory,"GigabitEthernet0/49").Media=="SFP","Stored module selections are deep copies of mutable caller data.");
        string path=Path.Combine(root,"modules.spworkspace.json");workspace.Save(path);
        var reopened=ConfigurationWorkspace.Load(path);var saved=reopened.Selected!;
        Check(saved.Id==item.Id&&saved.Text==Cisco()&&saved.Devices.Single(d=>d.Id=="1").ModuleSelections.Select(m=>m.Sku).SequenceEqual(new[]{"CVR-X2-SFP","X2-10GB-SR"}),"Explicit save/open preserves slot choices and original configuration without relying on a source file.");
        Check(InventoryBasis.ConfirmedPhysicalPortCount(saved.Inventory)==51&&InventoryBasis.ConfiguredInterfaceCount(saved.Inventory)==3&&Port(saved.Inventory,"GigabitEthernet0/49").TaggedVlans.Count==40,"Save/open reconstructs actual module media and complete settings.");
        saved.LastOutput=root;saved.LastChart=Path.Combine(root,"previous.png");saved.Status="Previous module build";saved.StatusType="complete";saved.Dirty=true;reopened.Dirty=true;
        string handoff=JsonSerializer.Serialize(WorkspaceRestartState.Capture(reopened));
        var restarted=WorkspaceRestartState.Restore(handoff,p=>p==root,p=>p==saved.LastChart);
        Check(restarted.Selected!.Devices.Single(d=>d.Id=="1").ModuleSelections.Count==2&&InventoryBasis.ConfirmedPhysicalPortCount(restarted.Selected.Inventory)==51,"Update and rollback handoff retain explicit installed module choices.");
        Check(restarted.Selected.LastOutput==root&&restarted.Selected.LastChart==saved.LastChart&&restarted.Selected.Dirty&&restarted.Dirty,"Restart also preserves result links and unsaved state.");
        restarted.Remove(saved.Id);Check(restarted.UndoRemoval()&&InventoryBasis.ConfirmedPhysicalPortCount(restarted.Selected!.Inventory)==51,"Undo removal restores the same module configuration.");
        restarted.Clear();Check(restarted.UndoRemoval()&&restarted.Selected!.Devices.Single(d=>d.Id=="1").ModuleSelections.Count==2,"Undo clear restores installed module choices.");
        var legacy=new WorkspaceRestartState.LegacySession(Cisco(),"legacy-modules.cfg",saved.Devices.ToArray(),null,null);
        Check(InventoryBasis.ConfirmedPhysicalPortCount(WorkspaceRestartState.Restore(JsonSerializer.Serialize(legacy),_=>false,_=>false).Selected!.Inventory)==51,"Legacy single-configuration update sessions migrate module details without loss.");
        var noModules=new ConfigurationWorkspace();var oldItem=noModules.AddText(Cisco(),"old-workspace.cfg");
        noModules.SetDevices(oldItem.Id,new[]{new DeviceIdentity{Id="1",Name="Legacy device detail"}});
        var legacyDocument=System.Text.Json.Nodes.JsonNode.Parse(noModules.Serialize())!;
        legacyDocument["Items"]![0]!["Devices"]![0]!.AsObject().Remove("ModuleSelections");
        string legacyJson=legacyDocument.ToJsonString();
        Check(!legacyJson.Contains("ModuleSelections",StringComparison.Ordinal)&&InventoryBasis.ConfirmedPhysicalPortCount(ConfigurationWorkspace.Deserialize(legacyJson).Selected!.Inventory)==48,"Old workspaces without a module-selection field retain their original uncertainty.");
        Check(ConfigurationWorkspace.Deserialize(legacyJson).Selected!.Inventory.Devices.Single().Name=="Legacy device detail","Old device details also remain intact when the new optional module field is absent.");
        var reset=ConfigurationWorkspace.Load(path);reset.SetDevices(reset.Selected!.Id,new[]{new DeviceIdentity{Id="1",Name="Module persistence lab"}});
        Check(InventoryBasis.ConfirmedPhysicalPortCount(reset.Selected.Inventory)==48&&reset.Selected.Devices.Single().ModuleSelections.Count==0,"Removing choices and saving device details fully reverts the selected hardware.");
        var detailWorkspace=new ConfigurationWorkspace();var withDetails=detailWorkspace.AddText(Cisco(),"module-details.cfg");
        detailWorkspace.SetDevices(withDetails.Id,new[]{Row("1",("X2-1","CVR-X2-SFP")),new DeviceIdentity{Id="1/X2-1",Name="Upper converter",SerialNumber="SYNTHETIC-MODULE-SERIAL",Notes="Keep this module note"}});
        detailWorkspace.SetDevices(withDetails.Id,new[]{new DeviceIdentity{Id="1"},new DeviceIdentity{Id="1/X2-1",Name="Upper converter",SerialNumber="SYNTHETIC-MODULE-SERIAL",Notes="Keep this module note"}});
        Check(InventoryBasis.ConfirmedPhysicalPortCount(withDetails.Inventory)==48&&withDetails.Inventory.Devices.Single(d=>d.Id=="1/X2-1").Notes=="Keep this module note","Reverting a card retains separately entered module identification without treating it as installed hardware.");
        Check(ConfigurationWorkspace.Deserialize(detailWorkspace.Serialize()).Selected!.Inventory.Devices.Single(d=>d.Id=="1/X2-1").SerialNumber=="SYNTHETIC-MODULE-SERIAL","Unconfirmed module notes and serials also survive explicit workspace persistence.");
    }
    static void Preview()
    {
        var chassisRow=Row("1",("A","J9986A"),("B","J9993A"));
        string source=Chassis+"module A type J9986A\n";
        var current=DeviceModels.Preview(source,new[]{chassisRow});
        var a=DeviceModules.Slots(current,current.Devices.Single(d=>d.Id=="1")).Single(s=>s.Slot=="A");
        Check(a.DetectedSku=="J9986A"&&a.SelectedSku=="J9986A","Preview retains both source provenance and explicit confirmation of the same module PID.");
        Check(current.Ports.Count==32&&current.Devices.Single(d=>d.Id=="1").ModuleSelections.Count==2,"Refreshing compatible module options preserves the selected installed modules.");
        chassisRow.ModelSelection="Friendly chassis report label";
        var labeled=DeviceModels.Preview(source,new[]{chassisRow});
        Check(labeled.Devices.Single(d=>d.Id=="1").Model=="Friendly chassis report label"&&labeled.Devices.Single(d=>d.Id=="1").ModuleSelections.Count==2&&labeled.Ports.Count==32,"Typing a friendly report label preserves compatible modules and confirmed geometry.");
        string partial="version 16.9\nhostname PREVIEW-LAB\ninterface GigabitEthernet1/0/1\n switchport mode access\n switchport access vlan 10\n!\n";
        var row=Row("1",("NM","C3850-NM-4-10G"));row.ModelSelection="WS-C3850-48P";
        var child=new DeviceIdentity{Id="1/NM",Name="Module note stays local",SerialNumber="PREVIEW-SYNTHETIC",Notes="Keep pending child edits"};
        var original=DeviceModels.Preview(partial,new[]{row,child});
        Check(original.Blockers.Count==0&&original.Devices.Single(d=>d.Id=="1").ModuleSelections.Single().Sku=="C3850-NM-4-10G","A valid manual model preview retains its compatible network card.");
        row.ModelSelection="C9300-48T";
        var compatible=DeviceModels.Preview(partial,new[]{row,child});
        Check(compatible.Blockers.Count==0&&compatible.Devices.Single(d=>d.Id=="1").ModuleSelections.Single().Sku=="C3850-NM-4-10G","A cross-generation card remains selected when the new base explicitly supports it.");
        row.ModelSelection="C9200-48T";
        var changed=DeviceModels.Preview(partial,new[]{row,child});
        Check(changed.Blockers.Count==0&&changed.Devices.Single(d=>d.Id=="1").ModuleSelections.Count==0&&DeviceModules.Slots(changed,changed.Devices.Single(d=>d.Id=="1")).Single().Options.Any(o=>o.Sku=="C9200-NM-4X"),"Changing an unidentified switch's model removes incompatible old choices and returns the new model's modules.");
        Check(row.ModuleSelections.Single().Sku=="C3850-NM-4-10G"&&child.Notes=="Keep pending child edits"&&child.SerialNumber=="PREVIEW-SYNTHETIC","Preview never mutates the caller's pending module selections or child identification edits.");
        row.ModelSelection="";
        var automatic=DeviceModels.Preview(partial,new[]{row,child});
        Check(automatic.Devices.Single(d=>d.Id=="1").ModuleSelections.Count==0&&DeviceModules.Slots(automatic,automatic.Devices.Single(d=>d.Id=="1")).Count==0,"Returning the model selector to automatic clears module choices whose compatible base is no longer established.");
    }
    static void Export(string root,string name,string source,DeviceIdentity[] details)
    {
        string input=Path.Combine(root,name+".cfg"),deviceFile=Path.Combine(root,name+"-details.json"),output=Path.Combine(root,"report-"+name);
        File.WriteAllText(input,source);File.WriteAllText(deviceFile,JsonSerializer.Serialize(details));
        Check(GeneratorEntry.Run(new[]{"--input",input,"--output",output,"--device-details",deviceFile})==0,"Selected module actual PDF/PNG/CSV export: "+name);
        Check(File.ReadAllText(input)==source,"Module selection and generation leave the source backup unchanged: "+name);
        var inventory=JsonSerializer.Deserialize<Inventory>(File.ReadAllText(Path.Combine(output,"inventory.json")))!;
        File.WriteAllText(Path.Combine(root,name+"-expectations.json"),JsonSerializer.Serialize(inventory.Ports.Select(p=>new{p.Id,p.Description,p.UntaggedVlan,Tagged=ReportText.TaggedVlans(p),p.VoiceVlan,p.Aggregate,p.Media,Configured=InventoryBasis.IsConfiguredInterface(inventory,p)})));
    }
    static int Main(string[] args)
    {
        string root=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"module-checks-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
        try
        {
            SlotsAndCompatibility();HpeSelection();CatalogSweep();CiscoSelection();CiscoCatalogSweep();Persistence(root);Preview();
            if(args.Length>0)
            {
                Export(root,"cisco",Cisco(),new[]{Row("1",("X2-1","CVR-X2-SFP"),("X2-2","X2-10GB-SR"))});
                Export(root,"hpe",Chassis+"vlan 10\n untagged A1\n exit\nvlan 20\n tagged A1,B1\n exit\n",new[]{Row("1",("A","J9986A"),("B","J9993A"))});
                Export(root,"9300",CiscoHeader("C9300-48T")+"interface TwentyFiveGigE1/1/1\n switchport mode trunk\n switchport trunk native vlan 10\n switchport trunk allowed vlan 10,"+DenseVlans+"\n description Selected SFP28 module keeps every tagged VLAN\n!\ninterface TenGigabitEthernet1/1/1\n switchport mode access\n switchport access vlan 20\n description Independent inactive SFP+ alternative\n!\n",new[]{Row("1",("NM","C9300-NM-2Y"))});
                Export(root,"3850-blank",CiscoHeader("WS-C3850-48P")+"interface GigabitEthernet1/1/1\n switchport mode access\n switchport access vlan 10\n description Inactive SFP interface retained with blank module\n!\ninterface TenGigabitEthernet1/1/1\n switchport mode access\n switchport access vlan 20\n description Inactive SFP+ interface retained with blank module\n!\n",new[]{Row("1",("NM","C3850-NM-BLANK"))});
            }
            Console.WriteLine("PASS "+assertions+" module compatibility, installed socket, independent interface, persistence and export assertions.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{if(args.Length==0)Directory.Delete(root,true);}
    }
}
