using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public sealed class HardwareModel
{
    public string Sku {get;set;}="";
    public string Name {get;set;}="";
    public string Family {get;set;}="";
    public string OS {get;set;}="";
    public string Status {get;set;}="research-pending";
    public string Source {get;set;}="";
    public string LayoutSource {get;set;}="";
    public string Notes {get;set;}="";
    public string[] ModuleFamilies {get;set;}=Array.Empty<string>();
    public int Copper {get;set;}
    public int Optical {get;set;}
    public string Side {get;set;}="right";
    public string OpticalType {get;set;}="Optical";
    public bool SingleRow {get;set;}
    public bool BlankLeft {get;set;}
    public List<HardwareSocket> Sockets {get;set;}=new();
}

public static class HardwareCatalog
{
    static readonly List<HardwareModel> all=Read();
    public static IReadOnlyList<HardwareModel> All=>all;
    public static IEnumerable<HardwareModel> Models=>all.Where(m=>m.Status=="supported");
    public static HardwareModel? Find(string sku)=>Models.FirstOrDefault(m=>m.Sku.Equals(sku,StringComparison.OrdinalIgnoreCase));
    public static IEnumerable<HardwareModel> Modules=>Models.Where(m=>m.ModuleFamilies.Length>0);
    public static bool IsModule(string sku)=>Find(sku)?.ModuleFamilies.Length>0;
    public static string ModuleFamily(string parentSku,string chassisModel="")
    {
        string code=parentSku.Trim().ToUpperInvariant();
        if(new[]{"JL071A","JL072A","JL073A","JL074A","JL075A","JL076A"}.Contains(code))return "3810M";
        if(HardwareProfiles.Is2920(code))return "2920";
        string name=HardwareProfiles.ChassisModels.TryGetValue(code,out var profile)?profile.Name:chassisModel;
        name=Regex.Replace(name.ToUpperInvariant(),@"[\s-]","");
        if(Regex.IsMatch(name,@"^(?:5400R|5406R|5412R)(?:ZL2)?$"))return "5400R";
        if(Regex.IsMatch(name,@"^(?:5400|5406|5412)(?:ZL)?$"))return "5400zl";
        if(Regex.IsMatch(name,@"^(?:8200|8206|8212)(?:ZL)?$"))return "8200zl";
        if(Regex.IsMatch(name,@"^(?:5300|5304|5308)(?:XL)?$"))return "5300xl";
        return "";
    }
    public static bool IsCompatibleModule(string moduleSku,string parentSku,string chassisModel="")
    {
        string family=ModuleFamily(parentSku,chassisModel);
        if(moduleSku.Equals("JL079A",StringComparison.OrdinalIgnoreCase)
            &&!new[]{"JL072A","JL074A","JL076A"}.Contains(parentSku.Trim(),StringComparer.OrdinalIgnoreCase))return false;
        return family.Length>0&&Find(moduleSku)?.ModuleFamilies.Contains(family,StringComparer.OrdinalIgnoreCase)==true;
    }
    public static bool IsSelection(string value)=>value.Contains('=')||Regex.IsMatch(value.Trim(),@"^(?:J(?:[A-Z]\d{3}|\d{4})|[RS]\d[A-Z]\d{2})[A-Z]$",RegexOptions.IgnoreCase);
    static List<HardwareModel> Read()
    {
        using var data=Assembly.GetExecutingAssembly().GetManifestResourceStream("SwitchportGenerator.Data.hardware-catalog.json")??throw new InvalidOperationException("Embedded hardware catalogue is missing.");
        var models=JsonSerializer.Deserialize<List<HardwareModel>>(data)??new();
        if(models.Select(m=>m.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=models.Count)throw new InvalidOperationException("Duplicate hardware catalogue identifiers.");
        foreach(var m in models.Where(m=>m.Status=="supported"))
        {
            var ids=m.Sockets.Select(s=>s.Number).Distinct().Order().ToList();
            if(m.Source.Length==0||m.LayoutSource.Length==0||!ids.SequenceEqual(Enumerable.Range(1,m.Copper+m.Optical))||m.Sockets.Any(s=>s.Column<0||s.Row<0||s.Row>15)||m.Sockets.GroupBy(s=>(s.Number,s.Suffix)).Any(g=>g.Count()>1))throw new InvalidOperationException("Invalid verified hardware catalogue entry: "+m.Sku);
        }
        return models;
    }
    public static List<string> ProductCodes(string text)=>Regex.Matches(string.Join('\n',text.Split('\n').Where(l=>Regex.IsMatch(l,@"(?i)^\s*(?:[;!#]|(?:product|model|chassis|system\s+model)\b)"))),@"\b(?:J(?:[A-Z]\d{3}|\d{4})|[RS]\d[A-Z]\d{2})[A-Z]\b",RegexOptions.IgnoreCase).Select(m=>m.Value.ToUpperInvariant()).Distinct().ToList();
    public static void ApplySelection(Inventory inv,string text,string? selected,Dictionary<string,string> declared)
    {
        if(string.IsNullOrWhiteSpace(selected))return;
        var choices=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string token in selected.Split(','))
        {
            var match=Regex.Match(token.Trim(),@"^(?:(\d+(?:/[A-L])?|[A-L])\s*=\s*)?([A-Z0-9-]+)$",RegexOptions.IgnoreCase);
            if(!match.Success){inv.Blockers.Add("Hardware selection must be a supported SKU, or member=SKU pairs separated by commas.");return;}
            string id=match.Groups[1].Success?match.Groups[1].Value.ToUpperInvariant():"1",sku=match.Groups[2].Value.ToUpperInvariant();
            if(!Parser.SupportedModelCodes.Contains(sku,StringComparer.OrdinalIgnoreCase)){inv.Blockers.Add("Hardware model has no verified port layout: "+sku);return;}
            bool slot=Regex.IsMatch(id,@"^(?:\d+/)?[A-L]$",RegexOptions.IgnoreCase);
            if(IsModule(sku)!=slot){inv.Blockers.Add(IsModule(sku)?"Select an explicit module slot, for example A="+sku+".":"Selected SKU is a fixed switch, not a module for slot "+id+".");return;}
            if(!choices.TryAdd(id,sku)){inv.Blockers.Add("Duplicate selected hardware member/slot: "+id);return;}
        }
        var explicitCodes=ProductCodes(text);
        if(declared.Count==0&&explicitCodes.Count>0)
        {
            var bases=choices.Where(c=>!IsModule(c.Value)).ToList();
            // A known fixed base or chassis header may accompany additional
            // operator-supplied module bays. Modules never replace that identity.
            if(explicitCodes.Count!=1||bases.Any(c=>c.Key!="1"||!c.Value.Equals(explicitCodes[0],StringComparison.OrdinalIgnoreCase)))
            {inv.Blockers.Add("Selected hardware conflicts with product identity in the configuration.");return;}
            string identity=explicitCodes[0];
            if(!HardwareProfiles.ChassisModels.ContainsKey(identity))
            {
                if(!Parser.SupportedModelCodes.Contains(identity,StringComparer.OrdinalIgnoreCase)||IsModule(identity))
                {inv.Blockers.Add("Selected hardware conflicts with product identity in the configuration.");return;}
                declared["1"]=identity;
            }
        }
        foreach(var choice in choices)
        {
            if(declared.TryGetValue(choice.Key,out var existing)&&!existing.Equals(choice.Value,StringComparison.OrdinalIgnoreCase))inv.Blockers.Add("Selected hardware conflicts with the configured member/slot: "+choice.Key);
            else declared[choice.Key]=choice.Value;
        }
        inv.Assumptions.Add("Hardware inventory supplied by the operator: "+string.Join(", ",choices.Select(c=>c.Key+"="+c.Value))+". Confirm member/slot assignments against chassis labels.");
    }
}
