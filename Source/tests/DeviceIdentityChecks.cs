using HUBTechGenerator;
static class DeviceIdentityChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static DeviceIdentity Detail(string id)=>new(){Id=id,Name="West Wing "+id,SerialNumber="SN-"+id+"-001",AssetTag="AT-"+id,Notes="Rack 2, upper shelf"};
    static void Reject(Inventory inv,params DeviceIdentity[] details)
    {
        bool rejected=false;try{DeviceIdentities.Apply(inv,details);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Invalid device identity data must be rejected");
    }
    static int Main()
    {
        try{Run();return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void Run()
    {
        var fixedSwitch=Parser.Parse("; JL256A Configuration Editor\nhostname IDENTITY-TEST\n");
        var original=Charts.Layout(fixedSwitch);
        DeviceIdentities.Apply(fixedSwitch,new[]{Detail("1")});
        Check(fixedSwitch.Devices.Count==1&&fixedSwitch.Devices[0].Model=="JL256A"&&fixedSwitch.Devices[0].Label=="Switch","Fixed switch has one explicit identity");
        Check(ReportText.GroupLabel(fixedSwitch,fixedSwitch.Groups[0]).Contains("West Wing 1 | S/N SN-1-001"),"Name and serial appear in the port heading");
        Check(Charts.Layout(fixedSwitch).SequenceEqual(original),"Metadata cannot alter physical port placements");
        var chart=Charts.Create(fixedSwitch,original,"TEST");
        Check(chart.Labels.Single(l=>l.X==12&&l.Y==17).Text=="West Wing 1"&&fixedSwitch.Hostname=="IDENTITY-TEST","Supplied switch name replaces upper-left report name without changing configured hostname");
        Check(Charts.ValidateScene(chart).Count==0,"Named switch scene has no label collisions");
        var pages=Charts.Booklet(fixedSwitch,chart,"TEST");
        Check(pages.Count==5&&pages[1].Labels.Any(l=>l.Text=="AT-1")&&pages[1].Labels.Any(l=>l.Text=="Rack 2, upper shelf"),"Booklet preserves asset and other details on an identification page");
        Check(pages.All(p=>Charts.ValidateScene(p).Count==0),"All identification and port-inventory pages have valid geometry");
        Check(pages.Skip(1).All(p=>p.Labels.Any(l=>l.Y==17&&l.Text.StartsWith("West Wing 1"))),"All booklet page headers use the supplied device name");

        var chassis=Parser.Parse("; J9851A Configuration Editor\nmodule A type J9993A\nmodule C type J9986A\nmodule G type J9986A\n");
        DeviceIdentities.Apply(chassis,new[]{Detail("1"),Detail("1/A"),Detail("1/C")});
        Check(chassis.Devices.Select(d=>d.Id).SequenceEqual(new[]{"1","1/A","1/C","1/G"})&&chassis.Devices[0].Label=="Chassis","Chassis and each detected module have separate ordered entries");
        Check(chassis.Devices[1].Kind=="Module"&&chassis.Devices[1].ParentId=="1"&&chassis.Devices[1].GroupId=="Module A"&&chassis.Devices[1].Model=="J9993A","Module hierarchy and model follow physical inventory");
        Check(chassis.Devices[3].SerialNumber=="","Untouched module does not inherit a serial number");
        Check(ReportText.GroupLabel(chassis,chassis.Groups.Single(g=>g.Slot=="G"))=="Slot G","Blank module heading does not show parent metadata");
        Check(Charts.Create(chassis,Charts.Layout(chassis),"TEST").Labels.Single(l=>l.X==12&&l.Y==17).Text=="West Wing 1","Supplied chassis name replaces upper-left report name");
        Check(Charts.Create(chassis,Charts.Layout(chassis),"TEST").Labels.Any(l=>l.Text.Contains("SN-1-001")),"Chassis details appear in the main diagram header");
        Check(ReportText.GroupLabel(chassis,chassis.Groups.Single(g=>g.Slot=="A")).Contains("SN-1/A-001")&&!ReportText.GroupLabel(chassis,chassis.Groups.Single(g=>g.Slot=="G")).Contains("SN-1/A-001"),"Module serials label only their own slots");
        var chassisPages=DeviceIdentities.Pages(chassis);
        Check(chassisPages.SelectMany(p=>p.Labels).Count(l=>l.Text=="Serial number")==3&&chassisPages.All(p=>Charts.ValidateScene(p).Count==0),"Parent and module full details appear independently in the booklet");
        Check(Charts.ValidateScene(Charts.Create(chassis,Charts.Layout(chassis),"TEST")).Count==0,"Named modular chassis fits existing panel geometry");

        string source="; Aruba Configuration Editor\nhostname STACK-IDENTITY-TEST\n"+string.Join('\n',Enumerable.Range(1,8).Select(n=>$"vsf member {n} type JL558A"));
        var stack=Parser.Parse(source);DeviceIdentities.Apply(stack,new[]{Detail("1"),Detail("8")});
        Check(ReportText.DisplayName(stack)=="West Wing 1 / West Wing 8","Multiple supplied switch names use inventory order in the report heading");
        Check(stack.Devices.Count==8&&stack.Devices.Count(d=>d.HasDetails)==2,"Only explicitly edited members receive details");
        Check(stack.Devices.Single(d=>d.Id=="2").SerialNumber=="","No serial numbers are inferred for other members");
        Check(!ReportText.GroupLabel(stack,stack.Groups.First(g=>g.Device=="2")).Contains("SN-1-001"),"Member metadata cannot bleed into another member");
        DeviceIdentities.Apply(stack,Enumerable.Range(1,8).Select(n=>new DeviceIdentity{Id=n.ToString(),Name=new string('W',80),SerialNumber=new string('S',80),AssetTag=new string('A',80),Notes=new string('N',400)}));
        var identityPages=DeviceIdentities.Pages(stack);
        Check(identityPages.Count>=2&&identityPages.All(p=>Charts.ValidateScene(p).Count==0),"Maximum-size details paginate without clipping or collisions");
        Check(identityPages.Sum(p=>p.Labels.Count(l=>l.Text=="Serial number"))==8,"Every member and serial field survives pagination");
        Check(Charts.ValidateScene(Charts.Create(stack,Charts.Layout(stack),"TEST")).Count==0,"Long headings retain eight-member A4 port geometry");

        var flex=Parser.Parse("; JL071A Configuration Editor\nmodule 1 type JL071A\nmodule A type JL083A\n");
        DeviceIdentities.Apply(flex,new[]{Detail("1/A")});
        Check(flex.Devices.Count==2&&flex.Devices[0].Model=="JL071A"&&flex.Devices[1].Model=="JL083A"&&flex.Devices[1].ParentId=="1","Flexible uplink module has a separate entry with its own model");
        Check(!flex.Devices[0].HasDetails&&flex.Devices[1].SerialNumber=="SN-1/A-001","Module-only edits leave the parent blank");
        Check(ReportText.DisplayName(flex)==flex.Hostname,"Module name cannot replace the switch/chassis report name");
        DeviceIdentities.Apply(flex,new[]{Detail("1")});
        Check(ReportText.GroupLabel(flex,flex.Groups.Single(g=>g.Slot=="A"))=="Slot A","Flexible module does not inherit parent metadata in its heading");
        var rear=Parser.Parse("; hpStack_WB Configuration Editor\nstacking\n member 1 type J9729A\n exit\ninterface 1/B1\n exit\n");
        DeviceIdentities.Apply(rear,new[]{Detail("1"),Detail("1/B")});
        Check(rear.Devices.Count==2&&rear.Devices[1].Label.Contains("Rear module B")&&rear.Devices[1].SerialNumber=="SN-1/B-001","Rear uplink module has its own editable identity");
        DeviceIdentities.Apply(rear,new[]{Detail("1")});
        Check(ReportText.GroupLabel(rear,rear.Groups.Single(g=>g.Slot=="B"))=="Member 1 | Rear module B","Blank rear module does not show switch metadata");
        var modularStack=new Inventory{ChassisModel="J9851A",ChassisSlots=12,Topology="Stack",Groups=new(){new(){Id="Module 1/A",Device="1",Slot="A",Product="J9993A"},new(){Id="Module 2/A",Device="2",Slot="A",Product="J9986A"}}};
        DeviceIdentities.Apply(modularStack,new[]{Detail("1/A"),Detail("2/A")});
        Check(modularStack.Devices.Select(d=>d.Id).SequenceEqual(new[]{"1","1/A","2","2/A"}),"Identical slot letters in different chassis stay separate");
        Check(ReportText.GroupLabel(modularStack,modularStack.Groups[1]).Contains("SN-2/A-001")&&!ReportText.GroupLabel(modularStack,modularStack.Groups[1]).Contains("SN-1/A-001"),"Module metadata cannot bleed between parents");
        DeviceIdentities.Apply(modularStack,new[]{new DeviceIdentity{Id="1/A",Kind="FORGED",ParentId="2",GroupId="FORGED",Model="FORGED",Name="Linecard"}});
        Check(modularStack.Devices[1].Kind=="Module"&&modularStack.Devices[1].ParentId=="1"&&modularStack.Devices[1].GroupId=="Module 1/A"&&modularStack.Devices[1].Model=="J9993A","Submitted fields cannot override module hierarchy or hardware");
        Reject(chassis,Detail("1/Z"));Reject(chassis,Detail("1/a"),Detail("1/A"));
        DeviceIdentities.Apply(chassis,Array.Empty<DeviceIdentity>());
        Check(chassis.Devices.Count==4&&chassis.Devices.All(d=>!d.HasDetails),"Clearing preserves chassis/module entries and clears their details");
        var irf=Parser.Parse("version 7.1.070\nsysname IRF-IDENTITY\ninterface GigabitEthernet1/0/1\n port access vlan 10\n#\ninterface GigabitEthernet2/0/48\n port access vlan 20\n","1=JG932A,2=JG934A");
        DeviceIdentities.Apply(irf,new[]{Detail("2")});
        Check(irf.Blockers.Count==0&&irf.Devices.Count==2&&irf.Devices.Single(d=>d.Id=="2").SerialNumber=="SN-2-001","IRF member identities follow physical member IDs");

        Reject(fixedSwitch,Detail("1"),Detail("1"));Reject(fixedSwitch,Detail("2"));
        Reject(fixedSwitch,new DeviceIdentity{Id="1",Name=new string('X',81)});
        Reject(fixedSwitch,new DeviceIdentity{Id="1",Notes=new string('X',401)});
        Check(fixedSwitch.Devices[0].Name=="West Wing 1","Invalid edits do not partially replace existing details");
        DeviceIdentities.Apply(fixedSwitch,new[]{new DeviceIdentity{Id="1",Name="Review\r\nSwitch",Model="FORGED",Label="FORGED"}});
        Check(fixedSwitch.Devices[0].Name=="Review  Switch"&&fixedSwitch.Devices[0].Model=="JL256A"&&fixedSwitch.Devices[0].Label=="Switch","Model and label remain authoritative; control characters are normalized");
        DeviceIdentities.Apply(fixedSwitch,new[]{new DeviceIdentity{Id="1",SerialNumber="SERIAL-ONLY",AssetTag="ASSET-ONLY"}});
        Check(ReportText.DisplayName(fixedSwitch)=="IDENTITY-TEST","Serial and asset fields do not override the configured report name");
        DeviceIdentities.Apply(fixedSwitch,Array.Empty<DeviceIdentity>());
        Check(!fixedSwitch.Devices.Any(d=>d.HasDetails)&&DeviceIdentities.Pages(fixedSwitch).Count==0,"Clearing details restores the original booklet page count");
        Check(Charts.Create(fixedSwitch,original,"TEST").Labels.Single(l=>l.X==12&&l.Y==17).Text=="IDENTITY-TEST","Clearing supplied name restores configured hostname in upper-left corner");
        string mixed="; Aruba Configuration Editor\nhostname MODELS-TEST\nvsf member 1 type JL558A\nvsf member 2 type JL256A\n";
        var namedModels=DeviceModels.Parse(mixed,new[]{new DeviceIdentity{Id="1",ModelSelection="North access"},new DeviceIdentity{Id="2",ModelSelection="South access"}});
        Check(namedModels.Blockers.Count==0&&namedModels.Devices[0].Model=="North access"&&namedModels.Devices[1].Model=="South access","Each stack member has a separate report model label");
        Check(namedModels.Devices[0].DetectedModel=="JL558A"&&namedModels.Devices[1].DetectedModel=="JL256A"&&namedModels.Groups[0].Product=="JL558A","Typed labels preserve detected SKU and physical layout");
        Check(ReportText.SwitchModel(namedModels)=="North access / South access"&&DeviceModels.GroupModel(namedModels,namedModels.Groups[1])=="South access","Member model labels map to their own diagram panels");
        Check(Charts.ValidateScene(Charts.Create(namedModels,Charts.Layout(namedModels),"TEST")).Count==0,"Independent model labels fit the report geometry");
        var resetModels=DeviceModels.Parse(mixed,Array.Empty<DeviceIdentity>());
        Check(resetModels.Devices[0].Model=="JL558A"&&!resetModels.Devices.Any(d=>d.HasDetails),"Blank model selections restore automatic detection");
        var unnamedModel=DeviceModels.Parse("; JL256A Configuration Editor\n",new[]{new DeviceIdentity{Id="1",ModelSelection="West access model"}});
        Check(ReportNaming.FolderName(unnamedModel)=="West access model","Per-device model name remains the folder-name fallback when hostname is absent");
        string unidentified="version 7.1.070\nsysname MISSING-MODELS\nirf member 1 priority 32\nirf member 2 priority 30\ninterface GigabitEthernet1/0/1\n#\ninterface GigabitEthernet2/0/1\n#\n";
        var unknown=DeviceModels.Parse(unidentified,Array.Empty<DeviceIdentity>());
        Check(unknown.Devices.Select(d=>d.Id).SequenceEqual(new[]{"1","2"})&&unknown.Devices.All(d=>d.Model==""),"Unidentified IRF members remain available for individual model input");
        var selectedModels=DeviceModels.Parse(unidentified,new[]{new DeviceIdentity{Id="1",ModelSelection="JG932A"},new DeviceIdentity{Id="2",ModelSelection="JG934A"}});
        Check(selectedModels.Blockers.Count==0&&selectedModels.Groups.Single(g=>g.Device=="1").Product=="JG932A"&&selectedModels.Groups.Single(g=>g.Device=="2").Product=="JG934A","Independent exact SKU selections establish mixed IRF hardware");
        var legacyAndDetails=DeviceModels.Parse(unidentified,new[]{new DeviceIdentity{Id="2",ModelSelection="JG934A"}},"1=JG932A");
        Check(legacyAndDetails.Blockers.Count==0&&legacyAndDetails.Groups.Count==2,"CLI legacy selections and per-device selections combine without duplicate member IDs");
        var instantModels=DeviceModels.Parse("vInstantOn_1960_3.1.1.0\nSKU Description JL805A\nunit-type unit 1 network te uplink none\nunit-type unit 2 network ge uplink te\ninterface 1/1\nexit\ninterface 2/1\nexit\n",new[]{new DeviceIdentity{Id="2",ModelSelection="JL806A"}});
        Check(instantModels.Blockers.Count==0&&instantModels.Groups.Single(g=>g.Device=="2").Product=="JL806A","Member model selection can identify an ambiguous mixed Instant On stack");
        var selectedChassis=DeviceModels.Parse("; Aruba Configuration Editor\nmodule A type J9993A\nmodule G type J9986A\n",new[]{new DeviceIdentity{Id="1",ModelSelection="J9851A"}});
        Check(selectedChassis.Blockers.Count==0&&selectedChassis.ChassisSlots==12&&selectedChassis.Devices[0].Model=="J9851A","Chassis model field can establish missing verified chassis capacity");
        var chassisLabel=DeviceModels.Parse("; J9851A Configuration Editor\nmodule A type J9993A\n",new[]{new DeviceIdentity{Id="1",ModelSelection="Core chassis model"}});
        Check(chassisLabel.Devices[0].Kind=="Chassis"&&chassisLabel.Devices[0].Model=="Core chassis model"&&chassisLabel.ChassisSlots==12,"Chassis has its own report model input with retained physical capacity");
        foreach(var bad in new[]{new DeviceIdentity{Id="1",ModelSelection="JL256A"},new DeviceIdentity{Id="2",ModelSelection="JG999A"},new DeviceIdentity{Id="1",ModelSelection=new string('M',101)}})
        {
            bool rejected=false;try{DeviceModels.Parse(mixed,new[]{bad});}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Conflicting, unsupported and oversized model selections must be rejected");
        }
        Check(DeviceModels.Choices.Any(m=>m.Sku=="J9851A"&&m.Kind=="Chassis")&&DeviceModels.Choices.Any(m=>m.Sku=="JG932A"&&m.Kind=="Switch"),"Model dropdown includes switch and chassis SKUs");
        Console.WriteLine("PASS independent switch/member/chassis model selections; missing mixed-IRF and chassis hardware; manual report labels preserve detected SKU/layout; automatic reset; model conflict, invalid SKU and size rejection; per-device diagram labels and catalogue options.");
        Console.WriteLine("PASS supplied switch/chassis names replace upper-left report/booklet headings; configured-hostname fallback and clear; module-only names cannot override device title; blank slot/flex/rear modules show no parent metadata; independent identities, hierarchy, pagination, unchanged geometry and invalid-edit checks.");
    }
}
