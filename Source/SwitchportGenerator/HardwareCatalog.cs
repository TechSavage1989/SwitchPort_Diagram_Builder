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
    static readonly HashSet<string> moduleCodes=new(new[]{"J4820A","J4878A","J4907A","J9534A","J9535A","J9536A","J9537A","J9538A","J9548A","J9549A","J9550A","J9986A","J9987A","J9990A","J9993A","J9995A","J9731A","J9732A","J8702A","J8705A","J8706A","JL083A","JL078A","JL079A","JL081A"},StringComparer.OrdinalIgnoreCase);
    static readonly List<HardwareModel> all=Read();
    public static IReadOnlyList<HardwareModel> All=>all;
    public static IEnumerable<HardwareModel> Models=>all.Where(m=>m.Status=="supported");
    public static HardwareModel? Find(string sku)=>Models.FirstOrDefault(m=>m.Sku.Equals(sku,StringComparison.OrdinalIgnoreCase));
    public static bool IsModule(string sku)=>moduleCodes.Contains(sku);
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
            if(moduleCodes.Contains(sku)!=slot){inv.Blockers.Add(moduleCodes.Contains(sku)?"Select an explicit module slot, for example A="+sku+".":"Selected SKU is a fixed switch, not a module for slot "+id+".");return;}
            if(!choices.TryAdd(id,sku)){inv.Blockers.Add("Duplicate selected hardware member/slot: "+id);return;}
        }
        var explicitCodes=ProductCodes(text);
        if(declared.Count==0&&explicitCodes.Count>0&&(choices.Count!=1||explicitCodes.Count!=1||!explicitCodes.Contains(choices.Values.Single(),StringComparer.OrdinalIgnoreCase)))
        {inv.Blockers.Add("Selected hardware conflicts with product identity in the configuration.");return;}
        foreach(var choice in choices)
        {
            if(declared.TryGetValue(choice.Key,out var existing)&&!existing.Equals(choice.Value,StringComparison.OrdinalIgnoreCase))inv.Blockers.Add("Selected hardware conflicts with the configured member/slot: "+choice.Key);
            else declared[choice.Key]=choice.Value;
        }
        inv.Assumptions.Add("Hardware inventory supplied by the operator: "+string.Join(", ",choices.Select(c=>c.Key+"="+c.Value))+". Confirm member/slot assignments against chassis labels.");
    }
}
