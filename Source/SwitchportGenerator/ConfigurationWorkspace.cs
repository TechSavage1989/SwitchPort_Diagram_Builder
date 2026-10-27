using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchportGenerator;

/// <summary>A source snapshot and its independent operator-supplied device/model details.</summary>
public sealed class ConfigurationItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Pasted configuration";
    public string SourcePath { get; set; } = "";
    public string Text { get; set; } = "";
    public List<DeviceIdentity> Devices { get; set; } = new();
    [JsonIgnore] public Inventory Inventory { get; private set; } = new();
    [JsonIgnore] public bool Dirty { get; set; } = true;
    [JsonIgnore] public string? LastOutput { get; set; }
    [JsonIgnore] public string? LastChart { get; set; }
    [JsonIgnore] public string Status { get; set; } = "";
    [JsonIgnore] public string StatusType { get; set; } = "";

    public void Refresh() => Inventory = DeviceModels.Parse(Text, Devices);
    public void InvalidateOutput()
    {
        LastOutput = null; LastChart = null; Status = ""; StatusType = "";
    }
}

/// <summary>
/// Versioned, explicit-save workspace. Config files and the generator's inventory schema
/// are unchanged. Saved source snapshots may contain credentials; callers must warn users.
/// </summary>
public sealed class ConfigurationWorkspace
{
    public const string FormatName = "SwitchportDiagramBuilder.Workspace";
    public const int FormatVersion = 1;
    public const int MaxInputBytes = 20_000_000;
    public const int MaxWorkspaceBytes = 100_000_000;
    public const int MaxItems = 100;
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public List<ConfigurationItem> Items { get; } = new();
    public string? SelectedId { get; set; }
    public ConfigurationItem? Selected => Items.FirstOrDefault(item => item.Id == SelectedId);
    public bool Dirty { get; set; }

    public ConfigurationItem AddText(string text, string name, string sourcePath = "")
    {
        ValidateText(text);
        if (Items.Count >= MaxItems) throw new InvalidOperationException("A workspace can contain up to 100 configurations.");
        if (Items.Sum(item => (long)Encoding.UTF8.GetByteCount(item.Text)) + Encoding.UTF8.GetByteCount(text) > MaxWorkspaceBytes)
            throw new InvalidOperationException("Workspace source snapshots exceed the 100 MB limit.");
        var item = new ConfigurationItem { Id = Guid.NewGuid().ToString("N"), Text = text, Name = CleanName(name), SourcePath = sourcePath ?? "" };
        item.Refresh(); // Validation blockers remain visible and can be resolved with model details.
        Items.Add(item); SelectedId = item.Id; Dirty = true;
        return item;
    }

    public ConfigurationItem Select(string id)
    {
        var item = Find(id);
        if (SelectedId != id) { SelectedId = id; Dirty = true; }
        return item;
    }

    public void Move(string id, int newIndex)
    {
        var item = Find(id);
        if (newIndex < 0 || newIndex >= Items.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        int previous = Items.IndexOf(item);
        if (previous == newIndex) return;
        Items.RemoveAt(previous); Items.Insert(newIndex, item); Dirty = true;
    }

    public void Remove(string id)
    {
        var item = Find(id); int index = Items.IndexOf(item);
        Items.RemoveAt(index);
        if (SelectedId == id) SelectedId = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)].Id;
        Dirty = true;
    }

    public void SetDevices(string id, IEnumerable<DeviceIdentity> devices)
    {
        var item = Find(id);
        var inventory = DeviceModels.Parse(item.Text, CloneDevices(devices));
        // Store canonical, independently owned details, never the caller's mutable rows.
        item.Devices = CloneDevices(inventory.Devices.Where(device => device.HasDetails));
        item.Refresh(); item.InvalidateOutput(); item.Dirty = true; Dirty = true;
    }

    public ConfigurationItem Find(string id) => Items.FirstOrDefault(item => item.Id == id)
        ?? throw new InvalidOperationException("The configuration is no longer in this workspace.");

    public string Serialize()
    {
        var document = new WorkspaceDocument { Format = FormatName, Version = FormatVersion, Items = Items, SelectedId = SelectedId };
        string json = JsonSerializer.Serialize(document, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaxWorkspaceBytes)
            throw new InvalidOperationException("The saved workspace exceeds the 100 MB limit.");
        return json;
    }

    public void Save(string path)
    {
        string json = Serialize();
        string fullPath = Path.GetFullPath(path);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            File.Move(temporary, fullPath, true);
            Dirty = false; foreach (var item in Items) item.Dirty = false;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static ConfigurationWorkspace Load(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > MaxWorkspaceBytes)
            throw new InvalidOperationException("Choose a workspace file under 100 MB.");
        return Deserialize(File.ReadAllText(path));
    }

    public static ConfigurationWorkspace Deserialize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxWorkspaceBytes)
            throw new InvalidOperationException("The saved workspace exceeds the 100 MB limit.");
        WorkspaceDocument document;
        try { document = JsonSerializer.Deserialize<WorkspaceDocument>(json) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidOperationException("The workspace is not valid JSON."); }
        if (document.Format != FormatName || document.Version != FormatVersion)
            throw new InvalidOperationException("This workspace format or version is not supported.");
        if (document.Items == null || document.Items.Count > MaxItems)
            throw new InvalidOperationException("A workspace can contain up to 100 configurations.");
        var workspace = new ConfigurationWorkspace();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in document.Items)
        {
            if (item == null || !Guid.TryParseExact(item.Id, "N", out _) || !ids.Add(item.Id))
                throw new InvalidOperationException("Workspace item identifiers are invalid or duplicated.");
            ValidateText(item.Text);
            if (item.Name == null || item.Name.Length > 200 || item.SourcePath == null || item.SourcePath.Length > 4096 || item.Devices == null)
                throw new InvalidOperationException("Workspace item metadata is invalid.");
            item.Name = CleanName(item.Name);
            item.Devices = CloneDevices(item.Devices);
            item.Refresh(); item.Dirty = false; item.InvalidateOutput();
            workspace.Items.Add(item);
        }
        if (document.SelectedId != null && !ids.Contains(document.SelectedId))
            throw new InvalidOperationException("The selected configuration is not in this workspace.");
        workspace.SelectedId = document.SelectedId ?? workspace.Items.FirstOrDefault()?.Id;
        return workspace;
    }

    static void ValidateText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("The configuration is empty.");
        if (Encoding.UTF8.GetByteCount(text) > MaxInputBytes) throw new InvalidOperationException("Configuration exceeds the 20 MB limit.");
        if (text.Contains('\0')) throw new InvalidOperationException("Unsupported binary configuration. Use a text export or an unencrypted RouterOS backup.");
    }
    static string CleanName(string? name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Pasted configuration" : name.Replace('\r', ' ').Replace('\n', ' ');
        return name[..Math.Min(name.Length, 200)];
    }
    static List<DeviceIdentity> CloneDevices(IEnumerable<DeviceIdentity> devices) => devices.Select(device => device == null
        ? throw new InvalidOperationException("Device details contain an empty row.")
        : new DeviceIdentity { Id = device.Id ?? "", Name = device.Name ?? "", SerialNumber = device.SerialNumber ?? "", AssetTag = device.AssetTag ?? "", Notes = device.Notes ?? "", ModelSelection = device.ModelSelection ?? "" }).ToList();

    sealed class WorkspaceDocument
    {
        public WorkspaceDocument() { }
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public string? SelectedId { get; set; }
        public List<ConfigurationItem> Items { get; set; } = new();
    }
}
