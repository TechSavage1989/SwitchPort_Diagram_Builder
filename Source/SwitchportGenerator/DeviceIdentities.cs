using System.Text.Json;
using System.Text.Json.Serialization;
namespace SwitchportGenerator;

public sealed class DeviceIdentity
{
    public string Id {get;set;}="";
    public string Label {get;set;}="";
    public string Model {get;set;}="";
    public string DetectedModel {get;set;}="";
    public string ModelSelection {get;set;}="";
    public List<DeviceModuleSelection> ModuleSelections {get;set;}=new();
    public string Kind {get;set;}="";
    public string ParentId {get;set;}="";
    public string GroupId {get;set;}="";
    public string Name {get;set;}="";
    public string SerialNumber {get;set;}="";
    public string AssetTag {get;set;}="";
    public string Notes {get;set;}="";
    [JsonIgnore] public bool HasDetails=>Name.Length+SerialNumber.Length+AssetTag.Length+Notes.Length+ModelSelection.Length>0||ModuleSelections.Count>0;
}

public static class DeviceIdentities
{
    public static string ModuleId(Group group)=>group.Slot.Length==0?"":group.Device+"/"+group.Slot.ToUpperInvariant();
    public static List<DeviceIdentity> Describe(Inventory inv)
    {
        var result=new List<DeviceIdentity>();bool stack=ReportText.IsStack(inv);
        foreach(var device in inv.Groups.GroupBy(g=>g.Device,StringComparer.OrdinalIgnoreCase)
            .OrderBy(g=>g.Key.Length).ThenBy(g=>g.Key,StringComparer.OrdinalIgnoreCase))
        {
            var baseGroups=device.Where(g=>g.Slot.Length==0).ToList();
            string kind=baseGroups.Count==0?"Chassis":"Switch";
            string label=stack?kind+" / Member "+device.Key:kind;
            result.Add(new DeviceIdentity{Id=device.Key,Label=label,Kind=kind,
                Model=kind=="Chassis"?inv.ChassisModel:string.Join(" / ",baseGroups.Select(g=>ReportText.ModelName(g.Product)).Where(p=>p.Length>0).Distinct())});
            foreach(var module in device.Where(g=>g.Slot.Length>0).OrderBy(g=>g.Slot.Length).ThenBy(g=>g.Slot,StringComparer.OrdinalIgnoreCase))
                result.Add(new DeviceIdentity{Id=ModuleId(module),Label=(stack?"Member "+device.Key+" / ":"")+(module.Rear?"Rear module ":"Module ")+module.Slot,
                    Kind="Module",ParentId=device.Key,GroupId=module.Id,Model=ReportText.ModelName(module.Product)});
        }
        foreach(var device in result)device.DetectedModel=device.Model;
        return result;
    }

    public static List<DeviceIdentity> Read(string path)
    {
        if(!File.Exists(path)||new FileInfo(path).Length>128_000)throw new InvalidOperationException("Device details must be a JSON file under 128 KB.");
        try{return JsonSerializer.Deserialize<List<DeviceIdentity>>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidOperationException("Device details must contain an array.");}
        catch(JsonException){throw new InvalidOperationException("Device details must contain a valid JSON array.");}
    }
    public static void Apply(Inventory inv,IEnumerable<DeviceIdentity> details,List<DeviceIdentity>? inventoryDevices=null)
    {
        var devices=inventoryDevices??Describe(inv);var rows=details.ToList();
        if(rows.Count>128)throw new InvalidOperationException("Device details exceed the 128-device limit.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Clean(string? value,int limit,string field)
        {
            value??="";
            if(value.Length>limit)throw new InvalidOperationException(field+" must be "+limit+" characters or fewer.");
            return Parser.Safe(value.Replace('\r',' ').Replace('\n',' ').Replace('\t',' '));
        }
        foreach(var row in rows)
        {
            if(row==null||string.IsNullOrWhiteSpace(row.Id)||!seen.Add(row.Id))throw new InvalidOperationException("Device details contain an empty or duplicate device ID.");
            var device=devices.FirstOrDefault(d=>d.Id.Equals(row.Id,StringComparison.OrdinalIgnoreCase));
            if(device==null)throw new InvalidOperationException("Device details no longer match the inventory. Reopen Device details and review the current devices.");
            device.Name=Clean(row.Name,80,"Device name");device.SerialNumber=Clean(row.SerialNumber,80,"Serial number");
            device.AssetTag=Clean(row.AssetTag,80,"Asset tag");device.Notes=Clean(row.Notes,400,"Other details");
            device.ModelSelection=DeviceModels.Clean(row.ModelSelection);
            device.ModuleSelections=DeviceModules.Copy(row.ModuleSelections);
            if(device.ModelSelection.Length>0)device.Model=device.ModelSelection;
        }
        inv.Devices=devices;
    }
    public static DeviceIdentity? For(Inventory inv,string device)=>inv.Devices.FirstOrDefault(d=>d.Id.Equals(device,StringComparison.OrdinalIgnoreCase));
    public static string Heading(Inventory inv,string device)
    {
        var detail=For(inv,device);
        if(detail==null||!detail.HasDetails)return "";
        return string.Join(" | ",new[]{detail.Name.Length>0?detail.Name:detail.AssetTag,detail.SerialNumber.Length>0?"S/N "+detail.SerialNumber:""}.Where(s=>s.Length>0));
    }
    public static List<Scene> Pages(Inventory inv,int firstPage=2)
    {
        var pages=new List<Scene>();Scene? page=null;double y=0;
        List<string> Wrap(string value,double width)
        {
            var result=new List<string>();string line="";
            foreach(char c in value)
            {
                string next=line+c;
                if(line.Length>0&&ReportRenderer.Measure(new Label(0,0,next,13)).Width>width){result.Add(line);line="";}
                line+=c;
            }
            if(line.Length>0)result.Add(line);return result;
        }
        foreach(var device in inv.Devices.Where(d=>d.HasDetails))
        {
            var fields=new[]{("Model",device.ModelSelection),("Modules",string.Join("; ",device.ModuleSelections.Select(s=>s.Slot+" = "+s.Sku))),("Name",device.Name),("Serial number",device.SerialNumber),("Asset tag",device.AssetTag),("Other details",device.Notes)}
                .Where(f=>f.Item2.Length>0).Select(f=>(Name:f.Item1,Lines:Wrap(f.Item2,965))).ToList();
            double height=46+fields.Sum(f=>Math.Max(1,f.Lines.Count)*17+5);
            if(page==null||y+height>726)
            {
                page=new Scene{Width=1190,Height=842};pages.Add(page);y=104;
                page.Rect(0,0,1190,84,"#13283d","#13283d");
                page.Text(30,17,ReportText.DisplayName(inv)+" / Device details",23,"#ffffff",true,1130);
                page.Text(30,55,"Chassis, switch and module identification",12,"#c6d7e6");
                page.Text(30,777,ReportText.CableMoveDisclaimer,10,"#34516b");
                page.Text(1070,815,"Page "+(firstPage+pages.Count-1),9,"#718396");
            }
            page.Rect(30,y,1130,height,"#f5f8fb","#c3d2df",4);
            string context=device.ParentId.Length>0?(For(inv,device.ParentId)?.Label??device.ParentId)+" / ":"";
            page.Text(42,y+8,context+device.Label+" | "+device.Model,15,"#20374e",true,1100);
            double fieldY=y+34;
            foreach(var field in fields)
            {
                page.Text(42,fieldY,field.Name,12,"#52677c",true,115);
                foreach(string line in field.Lines){page.Text(170,fieldY,line,13,"#172333",false,965);fieldY+=17;}
                fieldY+=5;
            }
            y+=height+12;
        }
        return pages;
    }
}
