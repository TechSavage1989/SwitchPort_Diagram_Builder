using System.Text;
using System.Text.RegularExpressions;

namespace SwitchportGenerator;

/// <summary>An individual file dropped onto the WebView2 workspace.</summary>
public sealed record DroppedNetworkFile(string? Name, string? Text, string? Encoding, string? Error);

public sealed class WorkspaceDropResult
{
    public int ConfigurationsAdded { get; internal set; }
    public int SupplementarySourcesAdded { get; internal set; }
    public HashSet<string> MatchedItemIds { get; } = new(StringComparer.Ordinal);
    public List<string> Errors { get; } = new();
    public string Message =>
        $"Added {ConfigurationsAdded} configuration{(ConfigurationsAdded == 1 ? "" : "s")} and " +
        $"{SupplementarySourcesAdded} supplementary source{(SupplementarySourcesAdded == 1 ? "" : "s")}" +
        (SupplementarySourcesAdded > 0 ? $" to {MatchedItemIds.Count} device{(MatchedItemIds.Count == 1 ? "" : "s")}" : "") +
        "." + (Errors.Count > 0 ? $" {Errors.Count} file or device assignment{(Errors.Count == 1 ? "" : "s")} need review." : "");
}

/// <summary>
/// Classifies mixed drops without ever letting Auvik/CLI evidence enter the
/// physical-configuration parser. Existing config and RouterOS backup drops
/// retain their original semantics. Attach evidence only to identified devices.
/// </summary>
public static class WorkspaceDropImporter
{
    static string Name(DroppedNetworkFile file)
    {
        string candidate = file.Name?.Replace('\\', '/') ?? "Dropped file";
        string leaf = candidate.Split('/').LastOrDefault() ?? "Dropped file";
        leaf = leaf.Trim();
        return leaf.Length == 0 ? "Dropped file" : leaf[..Math.Min(leaf.Length, 200)];
    }

    static string PayloadText(DroppedNetworkFile file)
    {
        if (file.Encoding is "base64")
        {
            if (file.Text == null || file.Text.Length > 28_000_000)
                throw new InvalidOperationException("RouterOS backup exceeds the 20 MB limit.");
            return ConfigInput.Decode(Convert.FromBase64String(file.Text));
        }
        if (file.Encoding is not (null or "text"))
            throw new InvalidOperationException("Unsupported dropped file encoding.");
        string content = file.Text ?? "";
        if (Encoding.UTF8.GetByteCount(content) > ConfigurationWorkspace.MaxInputBytes)
            throw new InvalidOperationException("Text file exceeds the 20 MB limit.");
        if (content.Contains('\0') || string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("The dropped text file is empty or contains binary data.");
        return content;
    }

    // A partially supported configuration may still contain an unambiguous
    // hostname declaration. Use it for evidence attribution only, never for
    // hardware, port geometry, VLANs or render validation.
    public static string EvidenceHostname(ConfigurationItem item)
    {
        string parsed = item.Inventory.Hostname;
        if (!string.IsNullOrWhiteSpace(parsed) &&
            !parsed.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) return parsed;
        var match = Regex.Match(item.Text, @"(?im)^\s*hostname\s+""?([\w.-]{1,180})""?\s*$");
        return match.Success ? match.Groups[1].Value : "";
    }

    static ConfigurationItem? TargetForCommand(ConfigurationWorkspace workspace, string text, string? dropTargetId)
    {
        var candidates = workspace.Items;
        if (candidates.Count == 0) return null;
        string prompt = SupplementaryData.PromptHostname(text);
        string promptKey = SupplementaryData.HostKey(prompt);
        if (promptKey.Length > 0)
        {
            var matched = candidates.Where(item => SupplementaryData.HostKey(EvidenceHostname(item)) == promptKey).ToList();
            if (matched.Count == 1) return matched[0];
            if (matched.Count > 1)
                return matched.FirstOrDefault(item => item.Id == dropTargetId);
            return null; // Do not silently attach one device's CLI to a different hostname.
        }
        if (dropTargetId != null)
            return candidates.FirstOrDefault(item => item.Id == dropTargetId);
        return candidates.Count == 1 ? candidates[0] : null;
    }

    public static WorkspaceDropResult Import(
        ConfigurationWorkspace workspace,
        IEnumerable<DroppedNetworkFile> suppliedFiles,
        string? selectedIdBeforeDrop, bool supplementaryOnly = false)
    {
        var files = suppliedFiles.ToArray();
        var result = new WorkspaceDropResult();
        if (files.Length == 0) return result;

        var commands = new List<(string Name, string Text)>();
        var workbooks = new List<(string Name, string Base64)>();
        var configurations = new List<(string Name, string Text)>();

        // Classify every input before applying it. A site-wide workbook must
        // never be parsed as a switch configuration, even when unmatched.
        foreach (var file in files)
        {
            string name = Name(file);
            try
            {
                if (!string.IsNullOrWhiteSpace(file.Error))
                    throw new InvalidOperationException(file.Error);
                string ext = Path.GetExtension(name).ToLowerInvariant();
                if (file.Encoding == "xlsx-base64" || ext == ".xlsx")
                {
                    if (ext != ".xlsx" || file.Encoding != "xlsx-base64" ||
                        file.Text == null || file.Text.Length > 34_000_000)
                        throw new InvalidOperationException("Expected an unmodified Auvik .xlsx workbook smaller than 25 MB.");
                    workbooks.Add((name, file.Text));
                    continue;
                }
                string text = PayloadText(file);
                if (file.Encoding != "base64" && SupplementaryData.LooksLikeCommandOutput(text))
                    commands.Add((name, text));
                else if (ext == ".csv")
                    throw new InvalidOperationException("Unrecognized CSV evidence. Use a supported Auvik workbook or saved CLI output.");
                else if (supplementaryOnly)
                    throw new InvalidOperationException("This file looks like a primary configuration. Drop it on the workspace instead.");
                else
                    configurations.Add((name, text));
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException)
            { result.Errors.Add(name + ": " + ex.Message); }
        }

        // Pass 1: create primary configs before matching evidence. This permits
        // dropping a configuration and its supporting Auvik/CLI exports together.
        foreach (var (name, text) in configurations)
        {
            try { workspace.AddText(text, name); result.ConfigurationsAdded++; }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            { result.Errors.Add(name + ": " + ex.Message); }
        }

        // If no configuration was previously selected, exactly one newly added
        // configuration is a safe target for command output without a prompt.
        string? targetId = selectedIdBeforeDrop;
        if (targetId == null && result.ConfigurationsAdded == 1)
            targetId = workspace.SelectedId;

        // Pass 2: attach workbook rows to each existing matching hostname.
        foreach (var (name, base64) in workbooks)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                var inventoryHosts = workspace.Items.Select(EvidenceHostname).ToArray();
                var sources = SupplementaryData.FromAuvikBytes(bytes, name, inventoryHosts);
                if (sources.Count == 0)
                {
                    result.Errors.Add(name + ": No Auvik device names match the loaded configurations. Import the matching switch config first.");
                    continue;
                }
                foreach (var pair in sources)
                {
                    var matching = workspace.Items.Where(item => SupplementaryData.HostKey(EvidenceHostname(item)) == pair.Key).ToList();
                    ConfigurationItem? destination = matching.Count == 1 ? matching[0] :
                        matching.FirstOrDefault(item => item.Id == targetId);
                    if (destination == null)
                    {
                        result.Errors.Add(name + ": Device " + pair.Key + " has multiple matching configurations. Select the intended one before dropping.");
                        continue;
                    }
                    try
                    {
                        workspace.AddSupplementary(destination.Id, pair.Value);
                        result.SupplementarySourcesAdded++;
                        result.MatchedItemIds.Add(destination.Id);
                    }
                    catch (InvalidOperationException ex)
                    { result.Errors.Add(name + " / " + pair.Key + ": " + ex.Message); }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException
                or IOException or System.Xml.XmlException)
            { result.Errors.Add(name + ": " + ex.Message); }
        }

        // Pass 3: a recognized CLI prompt wins over the current selection.
        // Without a prompt, a row drop, explicit selection or sole configuration
        // is required; never guess among multiple devices.
        foreach (var (name, text) in commands)
        {
            var item = TargetForCommand(workspace, text, targetId);
            if (item == null)
            {
                result.Errors.Add(name + ": Could not uniquely match operational output to a switch. Select its configuration and import under Supplementary data.");
                continue;
            }
            try
            {
                var source = SupplementaryData.FromText(text, name, EvidenceHostname(item));
                workspace.AddSupplementary(item.Id, source);
                result.SupplementarySourcesAdded++;
                result.MatchedItemIds.Add(item.Id);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            { result.Errors.Add(name + ": " + ex.Message); }
        }
        return result;
    }
}
