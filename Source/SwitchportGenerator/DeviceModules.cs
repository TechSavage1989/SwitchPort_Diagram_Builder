using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public sealed class DeviceModuleSelection
{
    public string Slot {get;set;}="";
    public string Sku {get;set;}="";
}

public sealed class ModuleChoice
{
    public string Sku {get;set;}="";
    public string Name {get;set;}="";
    public string Source {get;set;}="";
    public string LayoutSource {get;set;}="";
    public string Notes {get;set;}="";
}

public sealed class ModuleSlotChoice
{
    public string Slot {get;set;}="";
    public string Label {get;set;}="";
    public string DetectedSku {get;set;}="";
    public string SelectedSku {get;set;}="";
    public List<ModuleChoice> Options {get;set;}=new();
}

/// <summary>Compatible, per-member module selections; the original configuration is never changed.</summary>
public static class DeviceModules
{
    public static List<DeviceModuleSelection> Copy(IEnumerable<DeviceModuleSelection>? selections)
    {
        if(selections==null)throw new InvalidOperationException("Module selections must contain a list.");
        var result=new List<DeviceModuleSelection>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var selection in selections)
        {
            if(selection==null)throw new InvalidOperationException("Module selections contain an empty row.");
            string slot=Clean(selection.Slot,24,"Module slot").ToUpperInvariant(),sku=Clean(selection.Sku,80,"Module model").ToUpperInvariant();
            if(slot.Length==0||!seen.Add(slot))throw new InvalidOperationException("Module selections contain an empty or duplicate slot.");
            if(sku.Length>0)result.Add(new(){Slot=slot,Sku=sku});
            if(seen.Count>12)throw new InvalidOperationException("A device can have at most twelve module selections.");
        }
        return result;
    }
    static string Clean(string? value,int limit,string label)
    {
        if(value==null||value.Length>limit||value.Any(char.IsControl))throw new InvalidOperationException(label+" is invalid.");
        return value.Trim();
    }
    public static List<ModuleSlotChoice> Slots(Inventory inv,DeviceIdentity device)
    {
        if(device.Kind=="Module")return new();
        if(inv.Vendor=="Cisco")return CiscoModuleCatalog.Slots(inv,device).ToList();
        if(inv.OS!="AOS-Switch")return new();
        var baseGroup=inv.Groups.FirstOrDefault(g=>g.Device.Equals(device.Id,StringComparison.OrdinalIgnoreCase)&&g.Slot.Length==0);
        string parentSku=baseGroup?.Product??"";
        if(HardwareProfiles.ChassisModels.ContainsKey(device.ModelSelection??""))parentSku=device.ModelSelection??"";
        string chassis=device.Kind=="Chassis"?inv.ChassisModel:"";
        int count=HardwareProfiles.Is2920(parentSku)?2:inv.FlexibleUplinks&&baseGroup!=null?(parentSku.Equals("JL075A",StringComparison.OrdinalIgnoreCase)?2:1):device.Kind=="Chassis"?inv.ChassisSlots:0;
        var options=HardwareCatalog.Modules.Where(m=>HardwareCatalog.IsCompatibleModule(m.Sku,parentSku,chassis))
            .Select(m=>new ModuleChoice{Sku=m.Sku,Name=m.Name,Source=m.Source,LayoutSource=m.LayoutSource,Notes=m.Notes}).OrderBy(m=>m.Sku,StringComparer.OrdinalIgnoreCase).ToList();
        var result=new List<ModuleSlotChoice>();
        if(options.Count==0)return result;
        for(int n=0;n<count;n++)
        {
            string slot=((char)('A'+n)).ToString();
            string selected=(device.ModuleSelections??new()).FirstOrDefault(s=>s.Slot.Equals(slot,StringComparison.OrdinalIgnoreCase))?.Sku??"";
            string detected=inv.Groups.FirstOrDefault(g=>g.Device.Equals(device.Id,StringComparison.OrdinalIgnoreCase)&&g.Slot.Equals(slot,StringComparison.OrdinalIgnoreCase))?.Product??"";
            var evidence=inv.ModuleInventory?.FirstOrDefault(m=>m.Device.Equals(device.Id,StringComparison.OrdinalIgnoreCase)&&m.Slot.Equals(slot,StringComparison.OrdinalIgnoreCase));
            result.Add(new(){Slot=slot,Label=(HardwareProfiles.Is2920(parentSku)?"Rear slot ":"Slot ")+slot,DetectedSku=evidence is {OperatorSupplied:false}?evidence.Sku:selected.Length>0?"":detected,SelectedSku=selected,Options=options});
        }
        return result;
    }
    public static Inventory Apply(string source,Inventory inv,List<DeviceIdentity> rows,string? selectedModels)
    {
        // Canonicalization also validates absent, duplicate and overlong persisted rows.
        var choices=rows.ToDictionary(row=>row.Id,row=>Copy(row.ModuleSelections),StringComparer.OrdinalIgnoreCase);
        if(inv.Vendor=="Cisco")
        {
            CiscoModuleCatalog.Apply(inv,rows.Select(row=>new DeviceIdentity{Id=row.Id,ModuleSelections=choices[row.Id]}).ToList());
            return inv;
        }
        var devices=DeviceModels.Describe(source,inv);var selected=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string token in (selectedModels??"").Split(',',StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair=token.Split('=',2);string key=pair.Length==2?pair[0].Trim():"1",sku=pair[^1].Trim();
            selected[key]=sku;
        }
        bool changed=false;var capabilityNotes=new List<string>();
        var moduleEvidence=inv.Groups.Where(g=>g.Slot.Length>0&&g.Product.Length>0).Select(g=>new InstalledModule{Device=g.Device,Slot=g.Slot,Sku=g.Product}).ToList();
        foreach(var row in rows)
        {
            if(choices[row.Id].Count==0)continue;
            var parent=devices.FirstOrDefault(d=>d.Id.Equals(row.Id,StringComparison.OrdinalIgnoreCase)&&d.Kind!="Module")
                ??throw new InvalidOperationException("Module selection must belong to a switch, member or chassis.");
            parent.ModelSelection=row.ModelSelection;
            var slots=Slots(inv,parent);
            foreach(var choice in choices[row.Id])
            {
                var slot=slots.FirstOrDefault(s=>s.Slot.Equals(choice.Slot,StringComparison.OrdinalIgnoreCase));
                if(slot==null||!slot.Options.Any(o=>o.Sku.Equals(choice.Sku,StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Module "+choice.Sku+" is not compatible with "+parent.Label+" slot "+choice.Slot+".");
                var declared=inv.Groups.FirstOrDefault(g=>g.Device.Equals(parent.Id,StringComparison.OrdinalIgnoreCase)&&g.Slot.Equals(choice.Slot,StringComparison.OrdinalIgnoreCase));
                if(declared!=null&&declared.Product.Length>0&&!declared.Product.Equals(choice.Sku,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Selected module conflicts with configured hardware for "+parent.Label+" slot "+choice.Slot+".");
                if(declared!=null&&declared.Product.Length>0)continue;
                var module=HardwareCatalog.Find(choice.Sku)!;
                if(module.Family=="zl v2 module"&&HardwareCatalog.ModuleFamily("",inv.ChassisModel)=="5400R")
                {
                    if(Regex.IsMatch(source,@"(?im)^\s*no\s+allow-v2-modules\b|^\s*vsf(?:\s|$)"))
                        throw new InvalidOperationException("Module "+choice.Sku+" requires v2 compatibility mode and cannot be newly selected for a 5400R configuration that disables v2 modules or enables VSF.");
                    capabilityNotes.Add("5400R v2 module "+choice.Sku+" requires allow-v2-modules compatibility mode. This saved inventory does not establish the live module mode.");
                }
                if(module.Family=="zl v1 module"&&Regex.IsMatch(source,@"(?im)^\s*no\s+allow-v1-modules\b"))
                    throw new InvalidOperationException("Module "+choice.Sku+" conflicts with no allow-v1-modules in this configuration.");
                if(choice.Sku=="JL081A")
                {
                    var version=Regex.Match(inv.Firmware,@"^KB\.(\d+)\.(\d+)\.(\d+)",RegexOptions.IgnoreCase);
                    if(version.Success&&Version.TryParse(string.Join('.',Enumerable.Range(1,3).Select(i=>version.Groups[i].Value)),out var actual)&&actual<new Version(16,4,8))
                        throw new InvalidOperationException("JL081A requires KB.16.04.0008 or later; the saved configuration identifies "+inv.Firmware+".");
                }
                if(module.Notes.Length>0)capabilityNotes.Add(choice.Sku+": "+module.Notes);
                string key=declared?.Prefix??(ReportText.IsStack(inv)||parent.Id!="1"?parent.Id+"/":"")+choice.Slot;
                if(selected.TryGetValue(key,out var old)&&!old.Equals(choice.Sku,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Conflicting module selections for slot "+key+".");
                selected[key]=choice.Sku;changed=true;
                moduleEvidence.Add(new(){Device=parent.Id,Slot=choice.Slot,Sku=choice.Sku,OperatorSupplied=true});
            }
        }
        bool supplied=choices.Values.Any(c=>c.Count>0);
        if(!changed)
        {
            if(supplied&&moduleEvidence.Count>0)inv.ModuleInventory=moduleEvidence;
            return inv;
        }
        int capacity=inv.ChassisSlots;string chassisName=inv.ChassisModel;
        inv=Parser.Parse(source,string.Join(',',selected.Select(p=>p.Key+"="+p.Value)));
        if(capacity>0&&inv.ChassisSlots==0)
        {
            inv.ChassisSlots=capacity;inv.ChassisModel=chassisName;
            inv.Assumptions.RemoveAll(s=>s.StartsWith("Chassis identity is not supplied.",StringComparison.Ordinal));
            HardwareProfiles.Validate(inv);
        }
        inv.Assumptions.AddRange(capabilityNotes.Distinct());
        inv.ModuleInventory=moduleEvidence;
        return inv;
    }
    public static void RetainDetails(Inventory inv,List<DeviceIdentity> devices,IEnumerable<DeviceIdentity> rows)
    {
        foreach(var row in rows.Where(r=>!devices.Any(d=>d.Id.Equals(r.Id,StringComparison.OrdinalIgnoreCase))))
        {
            int slash=row.Id.LastIndexOf('/');
            if(slash<=0)continue;
            string parentId=row.Id[..slash],slotId=row.Id[(slash+1)..];
            var parent=devices.FirstOrDefault(d=>d.Id.Equals(parentId,StringComparison.OrdinalIgnoreCase)&&d.Kind!="Module");
            if(parent==null||!Slots(inv,parent).Any(s=>s.Slot.Equals(slotId,StringComparison.OrdinalIgnoreCase)))continue;
            if(!string.IsNullOrEmpty(row.ModelSelection))throw new InvalidOperationException("Use the parent device's module selector to choose installed hardware.");
            devices.Add(new(){Id=row.Id,ParentId=parent.Id,Kind="Module",Label="Module "+slotId+" (hardware unconfirmed)",Model="Installed hardware unconfirmed"});
        }
    }
}
