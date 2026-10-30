using System.Text.Json;
using SwitchportGenerator;

static class CatalogChecks
{
    static int assertions;
    static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static string Header(string model)=>"version 16.9\nhostname CATALOG-LAB\nswitch 1 provision "+model+"\n";
    static DeviceIdentity Row(string id,string model)=>new(){Id=id,ModelSelection=model,Name="Member "+id,Notes="Independent notes "+id};
    static void Rejected(Action action,string message)
    {try{action();}catch(InvalidOperationException){Check(true,message);return;}throw new Exception(message);}
    static string PortState(Inventory inv)=>JsonSerializer.Serialize(inv.Ports.Select(p=>new{p.Id,p.Media,p.Description,p.Mode,p.UntaggedVlan,p.TaggedVlans,p.VoiceVlan,p.Aggregate,p.Disabled}));
    static string Stack()=>Header("WS-C3650-48PD")+"switch 2 provision ws-c3650-48pd\ninterface Port-channel7\n switchport mode trunk\n switchport trunk native vlan 10\n switchport trunk allowed vlan 10,20-59\n!\ninterface GigabitEthernet1/0/1\n switchport mode access\n switchport access vlan 10\n switchport voice vlan 13\n description Independent phone port\n shutdown\n!\ninterface TenGigabitEthernet2/1/4\n channel-group 7 mode active\n description Independent stack uplink\n!\n";
    static void Inspector(string root)
    {
        string source=Stack();var automatic=DeviceModels.Parse(source,Array.Empty<DeviceIdentity>());
        var rows=new[]{Row("1","WS-C3650-48FD-L"),Row("2","WS-C3650-48FD-L")};
        foreach(var inv in new[]{DeviceModels.Preview(source,rows),DeviceModels.Parse(source,rows)})
        {
            Check(inv.Blockers.Count==0,"FD-L inspector preview/save accepts same-layout provisioning.");
            Check(inv.Ports.Count==104&&inv.Groups.Count==2&&inv.Groups.All(g=>g.Product=="WS-C3650-48FD"),"Two FD members have 104 independent physical ports.");
            Check(PortState(inv)==PortState(automatic),"Exact bundle refinement retains every source port setting.");
            Check(inv.Devices.All(d=>DeviceModules.Slots(inv,d).Count==0),"3650 fixed uplinks never expose removable network modules.");
            Check(inv.Assumptions.Any(a=>a.Contains("refines provisioning")),"The distinction between operator PID and provisioning remains explicit.");
        }
        foreach(string suffix in new[]{"L","S","E"})
            Check(CiscoHardware.Find("ws-c3650-48fd-"+suffix)?.Code=="WS-C3650-48FD","All documented FD license suffixes normalize.");
        Check(DeviceModels.Choices.Any(m=>m.Sku=="WS-C3650-48FD"),"FD is available in the real inspector model choices.");
        foreach(string mismatch in new[]{"WS-C3650-24PD","WS-C3650-48FQ","WS-C3650-48TD"})
            Rejected(()=>DeviceModels.Parse(source,new[]{Row("1",mismatch)}),"Different hardware cannot override provisioning.");
        string exact="NAME: \"Switch 1\", DESCR: \"Synthetic switch\"\nPID: WS-C3650-48PD-L, VID: V01, SN: SYNTHETIC\n";
        Check(DeviceModels.Parse(source+exact,rows).Blockers.Count>0,"Exact inventory PID cannot be overwritten by a same-layout bundle.");
        Check(Parser.Parse(source+exact.Replace("48PD-L","48FD-L")).Blockers.Count==0,"Exact inventory can refine the same-layout provisioning bundle.");
        Check(Parser.Parse(Header("WS-C3650-48PD")+"license udi pid WS-C3650-48PD-L sn SYNTHETIC\n","WS-C3650-48FD-L").Blockers.Count>0,"License UDI is exact hardware evidence.");
        var workspace=new ConfigurationWorkspace();var item=workspace.AddText(source,"synthetic-3650.cfg");workspace.SetDevices(item.Id,rows);
        string path=Path.Combine(root,"inspector.spworkspace.json");workspace.Save(path);var reopened=ConfigurationWorkspace.Load(path);
        Check(reopened.Selected!.Text==source&&PortState(reopened.Selected.Inventory)==PortState(automatic),"Save/open preserves original source and all settings.");
        Check(reopened.Selected.Inventory.Devices.All(d=>d.ModelSelection=="WS-C3650-48FD-L"&&d.Notes=="Independent notes "+d.Id),"Independent model and other details survive save/open.");
        reopened.SetDevices(item.Id,new[]{Row("1",""),Row("2","")});
        Check(reopened.Selected.Inventory.Groups.All(g=>g.Product=="WS-C3650-48PD"),"Automatic returns to original provisioning.");
        string input=Path.Combine(root,"3650.cfg"),details=Path.Combine(root,"3650-details.json");File.WriteAllText(input,source);File.WriteAllText(details,JsonSerializer.Serialize(rows));
        Check(GeneratorEntry.Run(new[]{"--input",input,"--output",Path.Combine(root,"report-3650"),"--device-details",details})==0,"FD stack exports actual PDF/PNG/CSV.");
    }
    static void NewFamilies()
    {
        foreach(var model in CiscoHardware.Models.Where(m=>m.Code.StartsWith("WS-C3650-")))
        {
            var inv=DeviceModels.Parse(Header(model.Code),Array.Empty<DeviceIdentity>());
            int count=int.Parse(model.Code[9..11]);
            Check(inv.Blockers.Count==0&&inv.Ports.Count==count+4,"3650 exact model count: "+model.Code);
            Check(DeviceModules.Slots(inv,inv.Devices.Single()).Count==0,"Fixed 3650 has no module bays: "+model.Code);
            var uplinks=inv.Ports.Where(p=>p.Id.Contains("/1/")).ToList();
            int expected=model.Code.TrimEnd('M').EndsWith('S')?0:model.Code.TrimEnd('M').EndsWith('Q')?4:2;
            Check(uplinks.Count(p=>p.Media=="SFP+")==expected,"3650 variant-specific SFP/SFP+ capability: "+model.Code);
        }
        foreach(var model in CiscoHardware.Models.Where(m=>m.Code.StartsWith("C9300X-")))
        {
            string source=Header(model.Code).Replace("16.9","17.12");var inv=DeviceModels.Parse(source,Array.Empty<DeviceIdentity>());
            var options=DeviceModules.Slots(inv,inv.Devices.Single()).Single().Options;
            Check(options.All(o=>o.Sku.StartsWith("C9300X-")),"9300X rejects ordinary 9300/3850 modules.");
            Check(options.Any(o=>o.Sku=="C9300X-NM-4C")==new[]{"C9300X-24Y","C9300X-48HX","C9300X-48TX"}.Contains(model.Code),"4C is restricted to its documented parent models.");
            foreach(var option in options)
            {
                var selected=DeviceModels.Parse(source,new[]{new DeviceIdentity{Id="1",ModuleSelections=new(){new(){Slot="NM",Sku=option.Sku}}}});
                Check(selected.Blockers.Count==0,"New module parses: "+option.Sku+" on "+model.Code);
                Check(Charts.Validate(selected,Charts.Layout(selected)).Count==0,"New module geometry: "+option.Sku);
                if(option.Sku=="C9300X-NM-4C")Check(selected.Groups.Single(g=>g.Slot=="NM").SocketLayout.Select(s=>(s.Column,s.Row)).SequenceEqual(new[]{(0d,0),(0d,1),(1d,0),(1d,1)}),"4C native QSFP28 cages follow the photographed two-column, two-row arrangement.");
            }
            Check(Parser.Parse(source+"NAME: \"Switch 1\", DESCR: \"Synthetic switch\"\nPID: "+model.Code+"-E, VID: V01, SN: SYNTHETIC\n").Blockers.Count==0,"9300X inventory PID is recognized.");
        }
        foreach(string code in new[]{"R8N89B","S4R21A","R8Q72A","R8V13A"})
        {
            var inv=DeviceModels.Parse("! AOS-CX "+code+"\nhostname COMPACT-LAB\ninterface 1/1/13\n vlan access 19\ninterface 1/1/15\n vlan trunk native 19\n vlan trunk allowed 19,20,21\n",Array.Empty<DeviceIdentity>());
            Check(inv.Blockers.Count==0&&inv.Ports.Count==16,"Compact CX retains 14 copper + 2 optical interfaces: "+code);
            Check(inv.Ports.Single(p=>p.Number==13).Media=="RJ-45"&&inv.Ports.Single(p=>p.Number==15).Media==(code.StartsWith("R8Q")||code.StartsWith("R8V")?"SFP+":"SFP"),"Copper and optical uplinks remain independent.");
        }
        foreach(var model in MikroTikParser.Models)
        {
            var inv=DeviceModels.Parse("# RouterOS 7.16\n# model = "+model.Code+"\n/system identity\nset name=ROUTEROS-LAB\n",Array.Empty<DeviceIdentity>());
            Check(inv.Blockers.Count==0&&inv.Ports.Count==model.Copper+model.Fiber+model.Qsfp,"Every MikroTik catalog model parses: "+model.Code);
            Check(Charts.Validate(inv,Charts.Layout(inv)).Count==0,"MikroTik geometry: "+model.Code);
        }
        var binary=new MikroTikParser.Snapshot{Binary=true,Interfaces=Enumerable.Range(1,24).Select(n=>new MikroTikParser.Interface{Key="ether"+n,Name="ether"+n,DefaultName="ether"+n}).Concat(Enumerable.Range(1,2).Select(n=>new MikroTikParser.Interface{Key="sfp-sfpplus"+n,Name="sfp-sfpplus"+n,DefaultName="sfp-sfpplus"+n})).ToList()};
        var shared=Parser.Parse(MikroTikBackup.Prefix+JsonSerializer.Serialize(binary));
        Check(shared.Blockers.Count==0&&shared.Ports.Count==26&&shared.Groups.Single().Product.StartsWith("MIKROTIK-SHARED-"),"Identical rack/desktop signatures select shared geometry without inventing a SKU or throwing.");
    }
    static void AdditionalReports(string root)
    {
        var fixtures=new[]{
            (Name:"9300x",Source:Header("C9300X-24Y").Replace("16.9","17.12")+"interface HundredGigE1/1/4\n switchport mode trunk\n switchport trunk native vlan 10\n switchport trunk allowed vlan 10,20-59\n!\n",Details:new[]{new DeviceIdentity{Id="1",ModuleSelections=new(){new(){Slot="NM",Sku="C9300X-NM-4C"}}}}),
            (Name:"6200-compact",Source:Fixture("R8Q72A")+"interface 1/1/15\n vlan trunk native 10\n vlan trunk allowed 10,20-59\n",Details:Array.Empty<DeviceIdentity>()),
            (Name:"mikrotik",Source:Fixture("CRS305-1G-4S+IN"),Details:Array.Empty<DeviceIdentity>())};
        foreach(var f in fixtures)
        {
            string input=Path.Combine(root,f.Name+".cfg"),details=Path.Combine(root,f.Name+"-details.json");File.WriteAllText(input,f.Source);File.WriteAllText(details,JsonSerializer.Serialize(f.Details));
            Check(GeneratorEntry.Run(new[]{"--input",input,"--output",Path.Combine(root,"report-"+f.Name),"--device-details",details})==0,"New family actual report export: "+f.Name);
        }
    }
    static string Fixture(string sku)
    {
        if(HardwareProfiles.ChassisModels.ContainsKey(sku))return "; "+sku+" Configuration Editor\nhostname CATALOG-LAB\nmodule A type "+HardwareCatalog.Modules.First(m=>HardwareCatalog.IsCompatibleModule(m.Sku,sku)).Sku+"\n";
        var m=HardwareCatalog.Find(sku);
        return CiscoHardware.Find(sku)!=null?Header(sku):MikroTikParser.Models.Any(x=>x.Code==sku)?"# RouterOS 7.16\n# model = "+sku+"\n/system identity\nset name=CATALOG-LAB\n":m?.OS=="Comware"?"# "+sku+"\nversion 7.1.070\nsysname CATALOG-LAB\n":m?.OS=="Instant On 1960"?"config-file-header\nCATALOG-LAB\nvInstantOn_1960_3.3.0.0\nSKU Description \"Instant On 1960 "+sku+"\"\n":m?.OS=="AOS-CX"?"! AOS-CX "+sku+"\nhostname CATALOG-LAB\n":"; "+sku+" Configuration Editor\nhostname CATALOG-LAB\n";
    }
    static string Snapshot()
    {
        var records=new SortedDictionary<string,string[]>(StringComparer.Ordinal);
        foreach(var m in HardwareCatalog.All)records[m.Sku]=new[]{"HPE/Aruba",m.Sku,m.Name,m.ModuleFamilies.Length>0||m.Name.Contains("Module")?"Module":"Switch",m.Status,m.Status=="supported"?(m.Copper+m.Optical).ToString():"",string.Join(';',m.ModuleFamilies),m.Source,m.LayoutSource,m.Notes};
        var compatibility=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        foreach(var choice in DeviceModels.Choices)
        {
            var inv=DeviceModels.Parse(Fixture(choice.Sku),Array.Empty<DeviceIdentity>());
            Check(inv.Blockers.Count==0,"Every inspector choice parses: "+choice.Sku+": "+string.Join(';',inv.Blockers));
            var parent=inv.Devices.FirstOrDefault(d=>d.ParentId.Length==0);
            var modules=parent==null?new List<string>():DeviceModules.Slots(inv,parent).SelectMany(s=>s.Options).Select(o=>o.Sku).Distinct().Order(StringComparer.Ordinal).ToList();
            foreach(string module in modules){if(!compatibility.ContainsKey(module))compatibility[module]=new();compatibility[module].Add(choice.Sku);}
            string source=HardwareCatalog.Find(choice.Sku)?.Source??CiscoHardware.Find(choice.Sku)?.Source??(MikroTikParser.Models.Any(m=>m.Code==choice.Sku)?MikroTikParser.ModelSource(choice.Sku):"Source/HARDWARE_LAYOUT.md");
            if(!records.TryGetValue(choice.Sku,out var row))records[choice.Sku]=row=new[]{inv.Vendor,choice.Sku,choice.Name,choice.Kind,"supported",choice.Kind=="Chassis"?"":InventoryBasis.ConfirmedPhysicalPortCount(inv).ToString(),"",source,source,choice.Kind=="Chassis"?"Chassis capacity only; installed modules require evidence.":""};
            row[6]=string.Join(';',modules);
        }
        foreach(var module in CiscoModuleCatalog.Choices)records[module.Sku]=new[]{"Cisco",module.Sku,module.Name,"Module","supported","","",module.Source,module.LayoutSource,module.Notes};
        foreach(var pair in compatibility){Check(records.ContainsKey(pair.Key),"Every offered module appears in the complete catalog.");records[pair.Key][6]=string.Join(';',pair.Value.Order(StringComparer.Ordinal));}
        Check(DeviceModels.Choices.Select(c=>c.Sku).Distinct().Count()==DeviceModels.Choices.Count(),"Inspector switch and chassis choices are unique.");
        foreach(var module in HardwareCatalog.Modules.Select(m=>m.Sku).Concat(CiscoModuleCatalog.Choices.Select(m=>m.Sku)))Check(compatibility.ContainsKey(module),"Every enabled module has at least one compatible parent: "+module);
        string Quote(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
        Console.WriteLine("Complete catalog: "+records.Count+" records; "+DeviceModels.Choices.Count()+" switch/chassis choices; "+compatibility.Count+" module PIDs.");
        return "Vendor,Sku,Name,Kind,Status,Base physical interfaces,Compatible modules or parents,Source,LayoutSource,Notes\n"+string.Join('\n',records.Values.Select(r=>string.Join(',',r.Select(Quote))))+"\n";
    }
    static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args.Length>0?args[0]:"catalog-fixtures");Directory.CreateDirectory(root);
            Inspector(root);NewFamilies();AdditionalReports(root);string snapshot=Snapshot();
            string path="Source/ALL_MODELS_MODULES.csv";
            if(args.Contains("--refresh"))File.WriteAllText(path,snapshot);
            else Check(File.ReadAllText(path).Replace("\r\n","\n")==snapshot,"Published complete model/module catalog matches runtime data. Refresh with --refresh after review.");
            Console.WriteLine("PASS "+assertions+" catalog and inspector assertions.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
