using System.Text;
using SwitchportGenerator;

namespace SwitchportDiagramBuilder;

public sealed partial class MainForm
{
    void ChooseSupplementary()
    {
        var selected = workspace.Selected ?? throw new InvalidOperationException("Select a configuration before importing supplementary data.");
        using var dialog = new OpenFileDialog
        {
            Title = "Add supplementary data to " + selected.Name,
            Filter = "Auvik reports and command output|*.xlsx;*.txt;*.log;*.csv|Auvik Excel reports|*.xlsx|Command output|*.txt;*.log;*.csv",
            CheckFileExists = true, Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int added = 0;
        var errors = new List<string>();
        foreach (var path in dialog.FileNames)
        {
            try
            {
                SupplementarySource source;
                if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                    source = SupplementaryData.FromAuvik(path, selected.Inventory.Hostname);
                else
                {
                    if (new FileInfo(path).Length > SupplementaryData.MaxTextBytes)
                        throw new InvalidOperationException("Command output exceeds the 20 MB limit.");
                    source = SupplementaryData.FromText(File.ReadAllText(path), Path.GetFileName(path), selected.Inventory.Hostname);
                }
                workspace.AddSupplementary(selected.Id, source);
                added++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException
                or ArgumentException or System.Xml.XmlException or InvalidDataException or System.Text.Json.JsonException)
            {
                errors.Add(Path.GetFileName(path) + ": " + ex.Message);
            }
        }
        if (added > 0) InvalidateBatch();
        SendSelection();
        Send(new { type = "supplementaryResult", success = added > 0, message =
            $"Added {added} supplementary source{(added == 1 ? "" : "s")} to the selected configuration.",
            errors });
    }

    void PasteSupplementary(string text, string name)
    {
        var selected = workspace.Selected ?? throw new InvalidOperationException("Select a configuration first.");
        try
        {
            var source = SupplementaryData.FromText(text, string.IsNullOrWhiteSpace(name) ? "Pasted show output" : name,
                selected.Inventory.Hostname);
            workspace.AddSupplementary(selected.Id, source);
            InvalidateBatch();
            SendSelection();
            Send(new { type = "supplementaryResult", success = true,
                message = $"Added {source.Observations.Count} observations from pasted command output.",
                errors = Array.Empty<string>() });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Send(new { type = "supplementaryResult", success = false,
                message = ex.Message, errors = Array.Empty<string>() });
        }
    }

    void RemoveSupplementary(string id)
    {
        if (workspace.Selected is not { } selected) return;
        workspace.RemoveSupplementary(selected.Id, id);
        InvalidateBatch(); SendSelection();
        Send(new { type = "supplementaryResult", success = true,
            message = "Supplementary source removed.", errors = Array.Empty<string>() });
    }
}
