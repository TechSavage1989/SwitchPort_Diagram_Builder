using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SwitchportGenerator;

public sealed record WorkspaceItemExport(string Id, string Name, string OutputDirectory, bool Success, string Error);
public sealed class WorkspaceExportResult
{
    public string OutputDirectory { get; init; } = "";
    public List<WorkspaceItemExport> Items { get; } = new();
    public string? CombinedDiagramsPath { get; internal set; }
    public string? CombinedBookletPath { get; internal set; }
    public string CombinedError { get; internal set; } = "";
    public List<string> CombinedImages { get; } = new();
    public List<string> Warnings { get; } = new();
    public int PageCount => CombinedImages.Count;
    public int SuccessCount => Items.Count(item => item.Success);
    public int FailureCount => Items.Count - SuccessCount;
}

/// <summary>Batch orchestration deliberately reuses the existing eight-format generator.</summary>
public static class WorkspaceExport
{
    public static WorkspaceExportResult Export(ConfigurationWorkspace workspace, string outputRoot, string? logoPath = null, bool combinedPdf = true)
    {
        if (workspace.Items.Count == 0) throw new InvalidOperationException("Add a configuration before building reports.");
        outputRoot = Path.GetFullPath(outputRoot); Directory.CreateDirectory(outputRoot);
        var palette=WorkspacePalette.Create(workspace,workspace.PaletteOverrides);
        string batchDirectory=WorkspaceNaming.CreateOutputDirectory(outputRoot,WorkspaceNaming.Effective(workspace));
        var result = new WorkspaceExportResult { OutputDirectory = batchDirectory };
        result.Warnings.AddRange(palette.Warnings);
        File.WriteAllText(Path.Combine(batchDirectory,"workspace-palette.json"),JsonSerializer.Serialize(new{palette.Items,palette.Warnings},new JsonSerializerOptions{WriteIndented=true}));
        string temporary = Path.Combine(Path.GetTempPath(), "SwitchportDiagramBuilder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        var items = workspace.Items.ToArray();
        try
        {
            for (int index = 0; index < items.Length; index++)
            {
                var item = items[index]; item.InvalidateOutput();
                string output = Path.Combine(batchDirectory, (index + 1).ToString("D3") + "-" + ItemName(item));
                try
                {
                    string input = Path.Combine(temporary, "configuration.txt");
                    string details = Path.Combine(temporary, "devices.json");
                    File.WriteAllText(input, item.Text, new UTF8Encoding(false));
                    File.WriteAllText(details, JsonSerializer.Serialize(item.Devices));
                    var arguments = new List<string> { "--input", input, "--output", output, "--device-details", details };
                    if (logoPath != null) { arguments.Add("--logo"); arguments.Add(logoPath); }
                    int code = GeneratorEntry.Run(arguments.ToArray(),item.Inventory.Blockers.Count==0?palette.ForItem(item.Id):null);
                    bool success = code == 0;
                    string error = success ? "" : ReadFailure(output);
                    item.LastOutput = Directory.Exists(output) ? output : null;
                    item.LastChart = success ? Path.Combine(output, "wall-chart.png") : null;
                    item.Status = (success ? "Reports created." : error) + (item.LastOutput == null ? "" : "\n\nOutput: " + item.LastOutput);
                    item.StatusType = success ? "complete" : "blocked";
                    result.Items.Add(new(item.Id, item.Name, output, success, error));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                {
                    string error = "Reports could not be created (" + ex.GetType().Name + "). Check configuration and output permissions.";
                    item.Status = error; item.StatusType = "error";
                    result.Items.Add(new(item.Id, item.Name, output, false, error));
                }
            }
            if (combinedPdf && result.SuccessCount > 0) WriteCombined(result, logoPath, WorkspaceNaming.Effective(workspace));
            File.WriteAllText(Path.Combine(batchDirectory, "workspace-export.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return result;
        }
        finally
        {
            try { Directory.Delete(temporary, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    static string ItemName(ConfigurationItem item)
    {
        string hostname = ReportNaming.FolderName(item.Inventory);
        string model = ReportText.SwitchModel(item.Inventory);
        // Reuse exactly the established Windows-safe name rules for combined identifiers.
        return ReportNaming.FolderName(new Inventory { Hostname = model.Length > 0 && !hostname.Equals(model, StringComparison.OrdinalIgnoreCase) ? hostname + "-" + model : hostname });
    }

    static string ReadFailure(string output)
    {
        string report = Path.Combine(output, "validation-report.txt");
        if (!File.Exists(report)) return "Generation blocked. Check configuration syntax and output permissions.";
        var blockers = File.ReadLines(report).Where(line => line.StartsWith("BLOCKER:", StringComparison.Ordinal)).ToArray();
        return blockers.Length > 0 ? string.Join("\n", blockers) : "Generation blocked. Review the validation report in this item's output folder.";
    }

    static void WriteCombined(WorkspaceExportResult result, string? logoPath,string title)
    {
        string diagramsPath = Path.Combine(result.OutputDirectory, "workspace-a4-diagrams.pdf");
        string bookletPath = Path.Combine(result.OutputDirectory, "workspace-a4-booklets.pdf");
        try
        {
            ReportLogo? logo = logoPath == null ? null : ReportLogo.Load(logoPath);
            var inputs=new List<WorkspaceSheetItem>();var details = new List<Scene>();
            foreach (var item in result.Items.Where(item => item.Success))
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(item.OutputDirectory, "inventory.json"));
                var inventory = JsonSerializer.Deserialize<Inventory>(bytes) ?? throw new InvalidOperationException("Generated inventory is missing.");
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var placements = Charts.Layout(inventory);
                var errors = Charts.Validate(inventory, placements);
                if (inventory.Blockers.Count > 0 || errors.Count > 0) throw new InvalidOperationException("A generated inventory failed validation.");
                var scene = Charts.Create(inventory, placements, hash);
                QuickSheet.FitToPage(scene);
                var booklet = Charts.Booklet(inventory, scene, hash);
                if (logo != null) foreach (var page in booklet) logo.Apply(page);
                if (booklet.SelectMany(Charts.ValidateScene).Any()) throw new InvalidOperationException("A combined page failed layout validation.");
                inputs.Add(new(item.Id,item.Name,inventory,hash));details.AddRange(booklet.Skip(1));
            }
            var sheets=WorkspaceSheet.Build(inputs,logo,title);
            result.Warnings.AddRange(sheets.Warnings);
            Pdf.Save(sheets.Pages,diagramsPath);Pdf.Save(sheets.Pages.Concat(details).ToList(),bookletPath);
            for(int index=0;index<sheets.Pages.Count;index++)
            {
                string image=Path.Combine(result.OutputDirectory,$"workspace-sheet-{index+1:D3}.png");
                Png.Save(sheets.Pages[index],image,3508,2480,true);result.CombinedImages.Add(image);
            }
            File.WriteAllText(Path.Combine(result.OutputDirectory,"workspace-layout.json"),JsonSerializer.Serialize(new{PageCount=sheets.Pages.Count,sheets.GeometryScale,sheets.Placements,sheets.LegendEntries,sheets.Warnings},new JsonSerializerOptions{WriteIndented=true}));
            result.CombinedDiagramsPath = diagramsPath; result.CombinedBookletPath = bookletPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or OnePageCapacityException)
        {
            if (File.Exists(diagramsPath)) File.Delete(diagramsPath);
            if (File.Exists(bookletPath)) File.Delete(bookletPath);
            foreach(string image in result.CombinedImages)if(File.Exists(image))File.Delete(image);
            result.CombinedImages.Clear();
            result.CombinedError = "Shared sheets could not be created: " + ex.Message + " Individual report packages are retained.";
        }
    }
}
