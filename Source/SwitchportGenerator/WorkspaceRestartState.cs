using System.Text;
using System.Text.Json;

namespace SwitchportGenerator;

/// <summary>Pure update/rollback handoff; only the host reads or writes its protected session file.</summary>
public static class WorkspaceRestartState
{
    public const int MaxBytes = 256_000_000;
    public sealed record Snapshot(string Workspace, bool Dirty, ItemState[] Items, string SavedWorkspaceName="",GeneratedProject? LatestProject=null);
    public sealed record ItemState(string Id, string? LastOutput, string? LastChart, string Status, string StatusType, bool Dirty);
    public sealed record LegacySession(string Text, string Name, DeviceIdentity[]? Devices, string? LastOutput, string? LastChart);

    public static Snapshot Capture(ConfigurationWorkspace workspace,GeneratedProject? latestProject=null) => new(workspace.Serialize(), workspace.Dirty,
        workspace.Items.Select(item => new ItemState(item.Id, item.LastOutput, item.LastChart, item.Status, item.StatusType, item.Dirty)).ToArray(),workspace.SavedWorkspaceName,latestProject);

    public static ConfigurationWorkspace Restore(string json, Func<string, bool> directoryExists, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new InvalidOperationException("Saved restart workspace is empty or too large.");
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            if (document.RootElement.TryGetProperty("Workspace", out _))
            {
                var state = JsonSerializer.Deserialize<Snapshot>(json) ?? throw new JsonException();
                if (string.IsNullOrWhiteSpace(state.Workspace)) throw new InvalidOperationException("Saved restart workspace is empty.");
                var workspace = ConfigurationWorkspace.Deserialize(state.Workspace);
                if (state.Items == null || state.Items.Length != workspace.Items.Count)
                    throw new InvalidOperationException("Saved restart item state does not match the workspace.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var saved in state.Items)
                {
                    if (saved == null || string.IsNullOrEmpty(saved.Id) || !seen.Add(saved.Id))
                        throw new InvalidOperationException("Saved restart item state contains an empty or duplicate identifier.");
                    var item = workspace.Find(saved.Id);
                    RestorePaths(item, saved.LastOutput, saved.LastChart, directoryExists, fileExists);
                    item.Status = saved.Status ?? ""; item.StatusType = saved.StatusType ?? ""; item.Dirty = saved.Dirty;
                }
                workspace.SavedWorkspaceName=WorkspaceNaming.Safe(state.SavedWorkspaceName??"");
                if(string.IsNullOrEmpty(state.SavedWorkspaceName))workspace.SavedWorkspaceName="";
                workspace.Dirty = state.Dirty;
                return workspace;
            }
            // The previous stable release persisted a single configuration, not a workspace.
            var legacy = JsonSerializer.Deserialize<LegacySession>(json) ?? throw new JsonException();
            var migrated = new ConfigurationWorkspace();
            var selected = migrated.AddText(legacy.Text, legacy.Name);
            migrated.SetDevices(selected.Id, legacy.Devices ?? Array.Empty<DeviceIdentity>());
            RestorePaths(selected, legacy.LastOutput, legacy.LastChart, directoryExists, fileExists);
            selected.Status = selected.LastOutput == null ? "" : "Configuration and saved device details restored after update.\n\nOutput: " + selected.LastOutput;
            selected.StatusType = selected.LastOutput == null ? "" : "complete";
            return migrated;
        }
        catch (JsonException) { throw new InvalidOperationException("Saved restart workspace is not valid JSON."); }
    }

    public static GeneratedProject? RestoreProject(string json)
    {
        try{return JsonSerializer.Deserialize<Snapshot>(json)?.LatestProject;}
        catch(JsonException){return null;}
    }

    static void RestorePaths(ConfigurationItem item, string? output, string? chart, Func<string, bool> directoryExists, Func<string, bool> fileExists)
    {
        item.LastOutput = !string.IsNullOrWhiteSpace(output) && directoryExists(output) ? output : null;
        item.LastChart = !string.IsNullOrWhiteSpace(chart) && fileExists(chart) ? chart : null;
    }
}
