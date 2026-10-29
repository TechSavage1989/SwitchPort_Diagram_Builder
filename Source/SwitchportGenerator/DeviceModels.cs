using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public static class DeviceModels
{
    public static IEnumerable<(string Sku,string Name,string Kind)> Choices=>Parser.SupportedModelCodes.Select(s=>(Sku:s,Name:HardwareCatalog.Find(s)?.Name??CiscoHardware.Find(s)?.Name??s,Kind:"Switch"))
        .Where(m=>!HardwareCatalog.IsModule(m.Sku))
        .Concat(MikroTikParser.Models.Select(m=>(Sku:m.Code,Name:m.Code,Kind:"Switch")))
        .Concat(HardwareProfiles.ChassisModels.Select(p=>(Sku:p.Key,Name:p.Value.Name,Kind:"Chassis"))).OrderBy(m=>m.Sku,StringComparer.OrdinalIgnoreCase);
    public static string Clean(string? value)
    {
        value??="";
        if(value.Length>100||value.Any(char.IsControl))throw new InvalidOperationException("Model must be 100 characters or fewer on one line.");
        return Parser.Safe(value);
    }
    public static bool IsSku(string value)=>CiscoHardware.IsModel(value)||Regex.IsMatch(value,@"^(?:J(?:[A-Z]\d{3}|\d{4})|[RS]\d[A-Z]\d{2})[A-Z]$",RegexOptions.IgnoreCase);
    public static List<DeviceIdentity> Describe(string source,Inventory inv)
    {
        var devices=DeviceIdentities.Describe(inv);
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(Match m in Regex.Matches(source,@"(?im)^\s*(?:(?:vsf\s+|irf\s+)?member|irf\s+member|unit-type\s+unit)\s+(\d+)\b"))ids.Add(m.Groups[1].Value);
        foreach(Match m in Regex.Matches(source,@"(?im)^\s*interface\s+(?:(?:GigabitEthernet|Ten-?GigabitEthernet|Twenty-?FiveGigE|FortyGigE|HundredGigE))?(\d+)/\d+/\d+\b"))ids.Add(m.Groups[1].Value);
        if(devices.Count==0&&ids.Count==0)ids.Add("1");
        bool chassis=inv.ChassisSlots>0||inv.Topology=="Modular";
        foreach(string id in ids.Where(id=>!devices.Any(d=>d.Id==id)))
        {
            string kind=chassis?"Chassis":"Switch";
            devices.Add(new(){Id=id,Kind=kind,Label=ids.Count>1||ReportText.IsStack(inv)?kind+" / Member "+id:kind,Model=chassis?inv.ChassisModel:"",DetectedModel=chassis?inv.ChassisModel:""});
        }
        return devices.OrderBy(d=>d.ParentId.Length>0?d.ParentId.Length:d.Id.Length).ThenBy(d=>d.ParentId.Length>0?d.ParentId:d.Id,StringComparer.OrdinalIgnoreCase)
            .ThenBy(d=>d.ParentId.Length>0?1:0).ThenBy(d=>d.Id,StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static Inventory Parse(string source,IEnumerable<DeviceIdentity> details,string? legacyModels=null)
    {
        var rows=details.ToList();
        if(rows.Count>128)throw new InvalidOperationException("Device details exceed the 128-device limit.");
        var inv=Parser.Parse(source,legacyModels);var devices=Describe(source,inv);
        var selections=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var chassisSelections=new Dictionary<string,(string Sku,string Name,int Slots)>(StringComparer.OrdinalIgnoreCase);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows)
        {
            if(row==null||string.IsNullOrWhiteSpace(row.Id)||!seen.Add(row.Id))throw new InvalidOperationException("Device details contain an empty or duplicate device ID.");
            var device=devices.FirstOrDefault(d=>d.Id.Equals(row.Id,StringComparison.OrdinalIgnoreCase))??throw new InvalidOperationException("Device details no longer match the inventory. Reopen Device details and review the current devices.");
            string model=Clean(row.ModelSelection);
            if(device.Kind=="Module"&&model.Length>0)throw new InvalidOperationException("Model selection is available for switches, members and chassis. Module models come from the configuration.");
            if(inv.OS=="RouterOS"&&MikroTikParser.Models.Any(m=>m.Code.Equals(model,StringComparison.OrdinalIgnoreCase))){selections.Add(device.Id,model);continue;}
            if(!IsSku(model))continue;
            string sku=CiscoHardware.IsModel(model)?CiscoHardware.CanonicalModel(model):model.ToUpperInvariant();
            if(device.Kind=="Chassis")
            {
                if(!HardwareProfiles.ChassisModels.TryGetValue(sku,out var profile))throw new InvalidOperationException("Choose a supported chassis SKU for "+device.Label+", or type a model name for its report label.");
                string Family(string name)=>Regex.Match(name,@"^\d+[Rr]?").Value.ToUpperInvariant();
                if(inv.ChassisSlots>0&&(profile.Slots!=inv.ChassisSlots||Family(profile.Name)!=Family(inv.ChassisModel)))throw new InvalidOperationException("Selected chassis model conflicts with the configured hardware for "+device.Label+".");
                chassisSelections.Add(device.Id,(sku,profile.Name,profile.Slots));
            }
            else
            {
                if(!Parser.SupportedModelCodes.Contains(sku,StringComparer.OrdinalIgnoreCase)||HardwareCatalog.IsModule(sku))throw new InvalidOperationException("Hardware model has no verified switch layout: "+sku+".");
                var group=inv.Groups.FirstOrDefault(g=>g.Device.Equals(device.Id,StringComparison.OrdinalIgnoreCase)&&g.Slot.Length==0);
                if(group!=null&&!inv.OS.StartsWith("Instant On",StringComparison.Ordinal)&&group.Product!=Parser.SharedPl48Profile&&!group.Product.Equals(sku,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Selected model conflicts with configured hardware for "+device.Label+". Type a model name to change only its report label.");
                var catalogue=HardwareCatalog.Find(sku);
                if(catalogue!=null&&inv.OS!="Unknown"&&catalogue.OS!=inv.OS)throw new InvalidOperationException("Selected model uses a different operating system for "+device.Label+".");
                if(CiscoHardware.Find(sku)!=null&&!inv.OS.StartsWith("Cisco IOS",StringComparison.Ordinal))throw new InvalidOperationException("Selected Cisco model uses a different operating system for "+device.Label+".");
                selections.Add(device.Id,sku);
            }
        }
        if(selections.Count>0)
        {
            // Instant On mixed-member selection needs the complete known base inventory.
            foreach(var g in inv.Groups.Where(g=>g.Slot.Length==0&&g.Product!=Parser.SharedPl48Profile&&!g.Product.StartsWith("MIKROTIK-SHARED-")))selections.TryAdd(g.Device,g.Product);
            foreach(string token in (legacyModels??"").Split(',',StringSplitOptions.RemoveEmptyEntries))
            {
                var pair=token.Split('=',2);string id=pair.Length==2?pair[0].Trim():"1",sku=pair[^1].Trim();
                if(selections.TryGetValue(id,out string? existing)&&!existing.Equals(sku,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Conflicting model selections for member/slot "+id+".");
                selections.TryAdd(id,sku);
            }
            string selected=string.Join(',',selections.Select(p=>p.Key+"="+p.Value));
            inv=Parser.Parse(source,selected);
        }
        if(chassisSelections.Count>0&&inv.ChassisSlots==0)
        {
            var parents=devices.Where(d=>d.Kind=="Chassis").ToList();
            if(parents.Any(d=>!chassisSelections.ContainsKey(d.Id))||chassisSelections.Values.Select(p=>p.Name).Distinct().Count()!=1)
                throw new InvalidOperationException("Select the same verified chassis layout for every unidentified chassis. Mixed chassis layouts are not supported in one diagram.");
            var profile=chassisSelections.Values.First();inv.ChassisModel=profile.Name;inv.ChassisSlots=profile.Slots;
            inv.Assumptions.RemoveAll(s=>s.StartsWith("Chassis identity is not supplied.",StringComparison.Ordinal));
            HardwareProfiles.Validate(inv);
            inv.Assumptions.Add("Chassis hardware supplied by the operator: "+string.Join(", ",chassisSelections.Select(p=>p.Key+"="+p.Value.Sku))+". Confirm against chassis labels.");
        }
        DeviceIdentities.Apply(inv,rows,Describe(source,inv));
        foreach(var device in inv.Devices.Where(d=>d.ModelSelection.Length>0))
            inv.Assumptions.Add("Model supplied for "+device.Label+": "+device.ModelSelection+(IsSku(device.ModelSelection)?" (verified hardware selection).":" (report label; validated physical layout is unchanged)."));
        return inv;
    }
    public static string GroupModel(Inventory inv,Group group)=>group.Slot.Length==0?DeviceIdentities.For(inv,group.Device)?.Model??ReportText.ModelName(group.Product):ReportText.ModelName(group.Product);
}
