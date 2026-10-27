using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SwitchportGenerator;

/// <summary>Optional operator choices. Keys are semantic identities, never list positions.</summary>
public sealed class WorkspacePaletteOverrides
{
    public Dictionary<string,string> Vlans { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string,string> Aggregates { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Complete, portable rendering context for one configuration.</summary>
public sealed class RenderPalette
{
    public Dictionary<int,string> Vlans { get; set; } = new();
    public Dictionary<string,string> Aggregates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; set; } = new();

    public static void ValidateColor(string? color)
    {
        if(color==null||!Regex.IsMatch(color,@"^#[0-9a-fA-F]{6}$"))
            throw new InvalidOperationException("Palette colors must be six-digit #RRGGBB values.");
    }
    public static void ValidateVlanColor(string? color)
    {
        ValidateColor(color);
        var rgb=SkiaSharp.SKColor.Parse(color!);var lab=AggregatePalette.Lab(color!);
        if(lab.L<60||lab.L>91||Math.Min(rgb.Red,Math.Min(rgb.Green,rgb.Blue))<95
            ||new[]{"#ffffff","#e6e6e6","#000000"}.Any(reference=>PerceptualColor.Difference(color!,reference)<=7))
            throw new InvalidOperationException("Saved VLAN colors must remain soft, readable and distinct from the reserved None, Unknown and Stack colors.");
    }
    void Validate()
    {
        if(Vlans==null||Aggregates==null||Warnings==null||Vlans.Count>65536||Aggregates.Count>4096||Warnings.Any(warning=>warning==null))
            throw new InvalidOperationException("Render palette metadata is invalid.");
        foreach(string color in Vlans.Values)ValidateVlanColor(color);
        foreach(string color in Aggregates.Values)ValidateColor(color);
        if(Vlans.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Vlans.Count)
            throw new InvalidOperationException("Distinct VLAN identities cannot share an overridden color.");
        if(Aggregates.Keys.Any(string.IsNullOrWhiteSpace)||Aggregates.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Aggregates.Count)
            throw new InvalidOperationException("Render palette has empty or duplicate LAG labels.");
    }
    public void Apply(Inventory inventory)
    {
        Validate();
        var ids=WorkspacePalette.VlanIds(inventory);
        var aggregates=inventory.Ports.Where(port=>port.Aggregate.Length>0).Select(port=>port.Aggregate).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(!ids.Order().SequenceEqual(Vlans.Keys.Order())||aggregates.Length!=Aggregates.Count||aggregates.Any(label=>!Aggregates.Keys.Contains(label,StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Render palette does not cover this configuration exactly. Rebuild the workspace palette.");
        var previousVlans=inventory.VlanColorOverrides;var previousAggregates=inventory.AggregateColorOverrides;
        try
        {
            inventory.VlanColorOverrides=Vlans.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToLowerInvariant());
            inventory.AggregateColorOverrides=new Dictionary<string,string>(Aggregates,StringComparer.OrdinalIgnoreCase);
            var colors=VlanPalette.Assign(inventory);
            AggregatePalette.Assign(inventory,port=>VlanPalette.NativeFill(port,colors),colors.Values);
        }
        catch {inventory.VlanColorOverrides=previousVlans;inventory.AggregateColorOverrides=previousAggregates;throw;}
        foreach(string warning in Warnings)if(!inventory.Assumptions.Contains(warning))inventory.Assumptions.Add(warning);
    }
    public string Serialize() { Validate();return JsonSerializer.Serialize(this); }
    public static RenderPalette Deserialize(string json)
    {
        if(string.IsNullOrWhiteSpace(json)||Encoding.UTF8.GetByteCount(json)>2_000_000)
            throw new InvalidOperationException("Render palette is empty or exceeds the 2 MB limit.");
        try
        {
            var palette=JsonSerializer.Deserialize<RenderPalette>(json)??throw new JsonException();
            palette.Validate();return palette;
        }
        catch(JsonException){throw new InvalidOperationException("Render palette is not valid JSON.");}
    }
}

public sealed class WorkspacePalettePlan
{
    public List<string> Warnings { get; } = new();
    public Dictionary<string,RenderPalette> Items { get; } = new(StringComparer.Ordinal);
    public Dictionary<string,Dictionary<int,string>> VlanIdentities { get; } = new(StringComparer.Ordinal);
    public RenderPalette ForItem(string id)=>Items.TryGetValue(id,out var palette)?palette:throw new InvalidOperationException("Configuration is absent from this workspace palette.");
    public void Apply(string id,Inventory inventory)=>ForItem(id).Apply(inventory);
}

public static class WorkspacePalette
{
    // VLAN IDs alone define shared colors. Names are display aliases, never identities.
    // Shared colors do not prove physical network connectivity.
    static string Name(string? value)=>(value??"").Trim().Normalize(NormalizationForm.FormC);
    public static string AggregateKey(string label)=>Name(label).ToUpperInvariant();
    public static List<int> VlanIds(Inventory inventory)=>inventory.Vlans.Select(vlan=>vlan.Id)
        .Concat(inventory.Ports.SelectMany(port=>port.TaggedVlans
            .Concat(port.UntaggedVlan.HasValue?new[]{port.UntaggedVlan.Value}:Array.Empty<int>())
            .Concat(port.VoiceVlan.HasValue?new[]{port.VoiceVlan.Value}:Array.Empty<int>()))).Distinct().Order().ToList();
    static string[] Names(Inventory inventory,int id)=>inventory.Vlans.Where(vlan=>vlan.Id==id).Select(vlan=>Name(vlan.Name)).Where(name=>name.Length>0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    public static string VlanKey(string itemId,Inventory inventory,int id)=>"vlan:"+id;

    // Read old ID/name and item-scoped keys without changing the version-1 file format.
    // Equal legacy choices coalesce; contradictory choices must be resolved explicitly.
    public static WorkspacePaletteOverrides NormalizeOverrides(IReadOnlyList<(string Id,Inventory Inventory)> inputs,WorkspacePaletteOverrides overrides)
    {
        if(overrides.Vlans==null||overrides.Aggregates==null)throw new InvalidOperationException("Workspace palette overrides must contain VLAN and LAG maps.");
        var legacy=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var item in inputs)foreach(int id in VlanIds(item.Inventory))
        {
            string key=VlanKey(item.Id,item.Inventory,id);legacy[key]=key;
            var names=Names(item.Inventory,id);
            legacy[names.Length==1?"vlan:"+id+":name:"+names[0]:"vlan:"+id+":item:"+item.Id]=key;
        }
        var normalized=new WorkspacePaletteOverrides{Aggregates=new(overrides.Aggregates,StringComparer.Ordinal)};
        foreach(var pair in overrides.Vlans.OrderBy(pair=>pair.Key,StringComparer.Ordinal))
        {
            if(!legacy.TryGetValue(pair.Key,out var key))throw new InvalidOperationException("A saved VLAN color override has no matching identity in this workspace: "+pair.Key);
            RenderPalette.ValidateVlanColor(pair.Value);
            if(normalized.Vlans.TryGetValue(key,out string? color)&&!color.Equals(pair.Value,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Conflicting saved colors for "+key+". VLAN IDs now share one color regardless of name. Replace its legacy overrides with one "+key+" color choice.");
            normalized.Vlans[key]=pair.Value.ToLowerInvariant();
        }
        return normalized;
    }

    public static WorkspacePalettePlan Create(ConfigurationWorkspace workspace,WorkspacePaletteOverrides? overrides=null)
        =>CreateCore(workspace.Items.Select(item=>(item.Id,item.Inventory)).ToArray(),overrides??workspace.PaletteOverrides??new(),false);

    /// <summary>Plan from parsed inventories without reparsing source or changing input objects.
    /// Existing applied contexts are pinned, including when rendering a subset of a workspace.</summary>
    public static WorkspacePalettePlan Create(IReadOnlyList<(string Id,Inventory Inventory)> inputs,WorkspacePaletteOverrides? overrides=null)
        =>CreateCore(inputs,overrides??new(),true);

    static WorkspacePalettePlan CreateCore(IReadOnlyList<(string Id,Inventory Inventory)> inputs,WorkspacePaletteOverrides overrides,bool preserveApplied)
    {
        if(inputs==null||inputs.Any(item=>string.IsNullOrWhiteSpace(item.Id)||item.Inventory==null)||inputs.Select(item=>item.Id).Distinct(StringComparer.Ordinal).Count()!=inputs.Count)
            throw new InvalidOperationException("Workspace palette inputs require distinct nonempty stable identifiers and parsed inventories.");
        if(overrides.Vlans==null||overrides.Aggregates==null)throw new InvalidOperationException("Workspace palette overrides must contain VLAN and LAG maps.");
        foreach(string color in overrides.Vlans.Values)RenderPalette.ValidateVlanColor(color);
        foreach(string color in overrides.Aggregates.Values)RenderPalette.ValidateColor(color);
        overrides=NormalizeOverrides(inputs,overrides);
        var plan=new WorkspacePalettePlan();
        // Stable identity order, never visible list order, controls palette optimization.
        var allItems=inputs.OrderBy(item=>item.Id,StringComparer.Ordinal).ToArray();
        var items=allItems.Where(item=>item.Inventory.Blockers.Count==0).ToArray();
        if(preserveApplied)
        {
            overrides=new WorkspacePaletteOverrides{Vlans=new(overrides.Vlans,StringComparer.Ordinal),Aggregates=new(overrides.Aggregates,StringComparer.Ordinal)};
            void Pin(Dictionary<string,string> target,string key,string color,string kind)
            {
                if(target.TryGetValue(key,out string? existing)&&!existing.Equals(color,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Conflicting applied "+kind+" colors for shared identity "+key+". Rebuild the complete workspace palette.");
                target[key]=color.ToLowerInvariant();
            }
            foreach(var item in items)
            {
                var ids=VlanIds(item.Inventory).ToHashSet();
                foreach(var pair in item.Inventory.VlanColorOverrides??new Dictionary<int,string>())
                {
                    if(!ids.Contains(pair.Key))throw new InvalidOperationException("Applied VLAN palette contains an identity absent from its configuration.");
                    RenderPalette.ValidateVlanColor(pair.Value);
                    Pin(overrides.Vlans,VlanKey(item.Id,item.Inventory,pair.Key),pair.Value,"VLAN");
                }
                var labels=item.Inventory.Ports.Where(port=>port.Aggregate.Length>0).Select(port=>port.Aggregate).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach(var pair in item.Inventory.AggregateColorOverrides??new Dictionary<string,string>())
                {
                    if(!labels.Contains(pair.Key))throw new InvalidOperationException("Applied LAG palette contains a label absent from its configuration.");
                    RenderPalette.ValidateColor(pair.Value);
                    Pin(overrides.Aggregates,AggregateKey(pair.Key),pair.Value,"LAG");
                }
            }
        }
        var knownVlanKeys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in allItems)
        {
            var map=VlanIds(item.Inventory).ToDictionary(id=>id,id=>VlanKey(item.Id,item.Inventory,id));
            plan.VlanIdentities.Add(item.Id,map);knownVlanKeys.UnionWith(map.Values);
        }
        if(items.Length!=allItems.Length)plan.Warnings.Add("Configurations with validation blockers are excluded from the shared palette until corrected.");
        var identities=new Dictionary<string,int>(StringComparer.Ordinal);
        var namesById=new Dictionary<int,HashSet<string>>();
        foreach(var item in items)
        {
            foreach(int id in VlanIds(item.Inventory))
            {
                string key=plan.VlanIdentities[item.Id][id];identities.TryAdd(key,id);
                if(!namesById.TryGetValue(id,out var names))namesById[id]=names=new(StringComparer.Ordinal);
                var supplied=Names(item.Inventory,id);foreach(string name in supplied)names.Add(name);
            }
        }
        foreach(var pair in namesById.OrderBy(pair=>pair.Key))
            if(pair.Value.Count>1)plan.Warnings.Add("VLAN "+pair.Key+" uses name aliases ("+string.Join(" / ",pair.Value.Order(StringComparer.Ordinal))+"). One shared color/key is used by VLAN ID; connectivity is not inferred.");
        var syntheticIds=new Dictionary<string,int>(StringComparer.Ordinal);int next=100_000;
        foreach(var group in identities.OrderBy(pair=>pair.Value).ThenBy(pair=>pair.Key,StringComparer.Ordinal).GroupBy(pair=>pair.Value))
        {
            foreach(var pair in group)syntheticIds.Add(pair.Key,group.Count()==1?pair.Value:next++);
        }
        var synthetic=new Inventory{Vlans=syntheticIds.Select(pair=>new Vlan{Id=pair.Value}).ToList()};
        foreach(var item in items)
        {
            int Map(int id)=>syntheticIds[plan.VlanIdentities[item.Id][id]];
            foreach(var port in item.Inventory.Ports)
                synthetic.Ports.Add(new Port{Id=item.Id+"/"+port.Id,Group=item.Id+"/"+port.Group,
                    UntaggedVlan=port.UntaggedVlan.HasValue?Map(port.UntaggedVlan.Value):null,UntaggedState=port.UntaggedState,
                    TaggedVlans=port.TaggedVlans.Select(Map).ToList(),VoiceVlan=port.VoiceVlan.HasValue?Map(port.VoiceVlan.Value):null,
                    StackLink=port.StackLink,Aggregate=AggregateKey(port.Aggregate)});
        }
        if(overrides.Vlans.Keys.Any(key=>!knownVlanKeys.Contains(key)))
            throw new InvalidOperationException("A saved VLAN color override has no matching identity in this workspace.");
        if(overrides.Vlans.Count>0)synthetic.VlanColorOverrides=overrides.Vlans.Where(pair=>syntheticIds.ContainsKey(pair.Key)).ToDictionary(pair=>syntheticIds[pair.Key],pair=>pair.Value);
        var vlans=VlanPalette.Assign(synthetic);
        var lagKeys=synthetic.Ports.Where(port=>port.Aggregate.Length>0).Select(port=>port.Aggregate).ToHashSet(StringComparer.Ordinal);
        var knownLagKeys=allItems.SelectMany(item=>item.Inventory.Ports.Where(port=>port.Aggregate.Length>0).Select(port=>AggregateKey(port.Aggregate))).ToHashSet(StringComparer.Ordinal);
        if(overrides.Aggregates.Keys.Any(key=>key!=AggregateKey(key)||!knownLagKeys.Contains(key)))
            throw new InvalidOperationException("A saved LAG color override has no matching normalized label in this workspace.");
        if(overrides.Aggregates.Count>0)synthetic.AggregateColorOverrides=overrides.Aggregates.Where(pair=>lagKeys.Contains(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.OrdinalIgnoreCase);
        var aggregates=AggregatePalette.Assign(synthetic,port=>VlanPalette.NativeFill(port,vlans),vlans.Values);
        if(lagKeys.Count>0&&items.Length>1)plan.Warnings.Add("Matching LAG labels share outline colors only; this does not establish a physical link or port-channel relationship between devices.");
        foreach(var item in items)
        {
            var palette=new RenderPalette{Warnings=plan.Warnings.ToList()};
            foreach(var pair in plan.VlanIdentities[item.Id])palette.Vlans.Add(pair.Key,vlans[syntheticIds[pair.Value]]);
            foreach(string label in item.Inventory.Ports.Where(port=>port.Aggregate.Length>0).Select(port=>port.Aggregate).Distinct(StringComparer.OrdinalIgnoreCase))
                palette.Aggregates.Add(label,aggregates[AggregateKey(label)]);
            plan.Items.Add(item.Id,palette);
        }
        return plan;
    }
}
