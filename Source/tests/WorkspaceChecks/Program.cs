using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SwitchportGenerator;

static class WorkspaceChecks
{
    static int checks;
    const string Aruba = "; JL256A Configuration Editor\nhostname SAME-HOST\ntrunk 49-50 trk1 lacp\nvlan 10\n name Data\n untagged 1-48\n tagged trk1\n exit\nvlan 150\n name Voice\n tagged 1-48,trk1\n voice\n exit\n";
    const string RouterOs = "# by RouterOS 7.16\n# model = CRS309-1G-8S+IN\n/system identity\nset name=FIBER-LAB\n";
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action, string message)
    {
        bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    static int Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "Switchport-workspace-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(root); Console.WriteLine("PASS " + checks + " workspace/state/batch assertions. Output: " + root); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if (args.Length == 0) Directory.Delete(root, true); }
    }
    static void Run(string root)
    {
        var workspace = new ConfigurationWorkspace();
        var first = workspace.AddText(Aruba, "switch.cfg", "/no-longer-present/switch.cfg");
        var duplicate = workspace.AddText(Aruba, "switch.cfg");
        var router = workspace.AddText(RouterOs, "fiber.rsc");
        var invalid = workspace.AddText("this is not a supported configuration", "invalid.txt");
        Check(workspace.Items.Count == 4 && first.Id != duplicate.Id, "Duplicate imports must be additive with distinct stable identities.");
        Check(!ReferenceEquals(first.Inventory, duplicate.Inventory), "Each item owns a distinct parsed inventory.");
        Check(first.Inventory.Blockers.Count == 0 && router.Inventory.Blockers.Count == 0 && invalid.Inventory.Blockers.Count > 0, "Mixed vendor inputs and invalid input retain independent validation.");
        Check(first.Inventory.Ports.Any(port => port.Aggregate.Length > 0) && first.Inventory.Vlans.Any(vlan => vlan.Voice), "Existing VLAN, voice and aggregation semantics survive workspace parsing.");
        Reject(() => workspace.AddText("  ", "empty.cfg"), "Empty import must fail without replacing existing items.");
        Reject(() => workspace.AddText("a\0b", "binary.bin"), "Unsupported binary input must be rejected independently.");
        Reject(() => workspace.AddText(new string('x', ConfigurationWorkspace.MaxInputBytes + 1), "large.cfg"), "The existing 20 MB input limit must be retained.");
        Check(workspace.Items.Count == 4, "Rejected inputs do not alter valid configurations.");

        workspace.Select(first.Id);
        first.LastOutput = "previous output"; first.LastChart = "previous chart"; first.Status = "previous status";
        workspace.Move(router.Id, 0);
        Check(workspace.Selected == first && workspace.Items.Select(item => item.Id).SequenceEqual(new[] { router.Id, first.Id, duplicate.Id, invalid.Id }), "Reordering must preserve selection by identity.");
        workspace.Select(router.Id); workspace.Select(first.Id);
        Check(first.LastChart == "previous chart" && first.Status == "previous status", "Switching must restore each item's prior output state.");
        var details = new DeviceIdentity { Id = "1", Name = "FIRST-EDITED", SerialNumber = "FIRST-ONLY", ModelSelection = "Custom report label" };
        workspace.SetDevices(first.Id, new[] { details });
        details.Name = "UNEXPECTED-EXTERNAL-MUTATION";
        Check(first.Inventory.Devices.Single().Name == "FIRST-EDITED" && duplicate.Inventory.Devices.All(device => device.Name == ""), "Device details must remain independent, even across duplicate inputs.");
        Check(first.Devices[0].Name == "FIRST-EDITED" && first.Inventory.Devices.Single().Model == "Custom report label", "Mutable caller rows cannot leak into stored model state.");
        Check(first.LastOutput == null && first.LastChart == null && first.Dirty, "Editing invalidates only this item's generated outputs.");
        Reject(() => workspace.SetDevices(first.Id, new[] { new DeviceIdentity { Id = "not-a-device", Name = "bad" } }), "Invalid device edits must be rejected transactionally.");
        Check(first.Inventory.Devices.Single().Name == "FIRST-EDITED", "Rejected edits must preserve valid prior state.");
        var unidentified = workspace.AddText(RouterOs.Replace("# model = CRS309-1G-8S+IN", "# model not supplied"), "manual-model.rsc");
        Check(unidentified.Inventory.Blockers.Count > 0, "Missing hardware identity must not be invented on import.");
        workspace.SetDevices(unidentified.Id, new[] { new DeviceIdentity { Id = "1", ModelSelection = "CRS309-1G-8S+IN" } });
        Check(unidentified.Inventory.Blockers.Count == 0 && unidentified.Inventory.Ports.Count == 9, "Independent manual model selection can resolve an imported configuration.");
        workspace.Select(first.Id); workspace.Remove(unidentified.Id);
        Check(workspace.Selected == first, "Removing another item does not change the selected item.");

        string saved = Path.Combine(root, "roundtrip.spworkspace.json");
        workspace.Save(saved);
        Check(!workspace.Dirty && workspace.Items.All(item => !item.Dirty), "Only explicit workspace save resets unsaved state.");
        string json = File.ReadAllText(saved);
        Check(!json.Contains("LastChart") && !json.Contains("LastOutput") && !json.Contains("Inventory\""), "Workspace does not serialize derived inventory or executable/output paths.");
        var restored = ConfigurationWorkspace.Load(saved);
        Check(restored.Items.Select(item => item.Id).SequenceEqual(workspace.Items.Select(item => item.Id)) && restored.SelectedId == first.Id, "Workspace roundtrip preserves order and selection.");
        Check(restored.Selected!.Text == Aruba && restored.Selected.Devices[0].SerialNumber == "FIRST-ONLY", "Source snapshots and device/model state survive without reading original paths.");
        Check(restored.Selected.LastChart == null && !restored.Dirty && restored.Items.All(item => !item.Dirty), "Reopening requires output rebuild and starts clean.");
        var bad = JsonNode.Parse(json)!;
        bad["Version"] = 2; Reject(() => ConfigurationWorkspace.Deserialize(bad.ToJsonString()), "Future format versions must fail clearly.");
        bad = JsonNode.Parse(json)!; bad["Items"]![1]!["Id"] = bad["Items"]![0]!["Id"]!.GetValue<string>();
        Reject(() => ConfigurationWorkspace.Deserialize(bad.ToJsonString()), "Duplicate persisted IDs must not alias state.");
        bad = JsonNode.Parse(json)!; bad["SelectedId"] = Guid.NewGuid().ToString("N");
        Reject(() => ConfigurationWorkspace.Deserialize(bad.ToJsonString()), "Unknown selected item must not be silently substituted.");
        Reject(() => ConfigurationWorkspace.Deserialize("{}"), "An unrelated JSON document must not be accepted as a workspace.");
        Reject(() => ConfigurationWorkspace.Deserialize("[oops"), "Malformed JSON must fail without touching the current workspace.");
        bad = JsonNode.Parse(json)!; bad["Items"]![1]!["Devices"]![0]!["Name"] = null;
        Check(ConfigurationWorkspace.Deserialize(bad.ToJsonString()).Selected!.Devices[0].Name == "", "Null optional device fields must normalize without a null-reference failure.");
        bad = JsonNode.Parse(json)!; bad["Items"]![1]!["Devices"]![0]!["Id"] = null;
        Reject(() => ConfigurationWorkspace.Deserialize(bad.ToJsonString()), "Null device identifiers must produce an actionable validation error.");
        Check(workspace.Items.Count == 4 && workspace.SelectedId == first.Id, "Failed workspace loads leave the original workspace intact.");

        string oldSelection = workspace.SelectedId!;
        var exported = WorkspaceExport.Export(workspace, root);
        Check(exported.SuccessCount == 3 && exported.FailureCount == 1, "Batch continues after invalid items and exports every valid item.");
        Check(exported.Items.Select(item => item.Id).SequenceEqual(workspace.Items.Select(item => item.Id)), "Batch manifest follows visible workspace order.");
        Check(workspace.SelectedId == oldSelection && !workspace.Dirty && workspace.Items.All(item => !item.Dirty), "Export does not change selection or workspace saved state.");
        Check(exported.Items.Select(item => item.OutputDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4, "Duplicate hostnames and filenames never share output directories.");
        Check(Path.GetFileName(exported.Items[0].OutputDirectory).StartsWith("001-FIBER-LAB") && Path.GetFileName(exported.Items[1].OutputDirectory).StartsWith("002-SAME-HOST"), "Ordinal and hostname/model names make export order visible.");
        string[] expected = { "wall-chart.png", "a4-one-page.png", "a4-one-page.pdf", "a4-booklet.pdf", "port-map.csv", "technician-summary.txt", "validation-report.txt", "inventory.json" };
        foreach (var item in exported.Items.Where(item => item.Success))
            Check(Directory.GetFiles(item.OutputDirectory).Select(Path.GetFileName).Order().SequenceEqual(expected.Order()), "Each successful item retains all eight existing output formats.");
        Check(invalid.LastChart == null && Directory.GetFiles(exported.Items.Last().OutputDirectory, "*.pdf").Length == 0, "Invalid configurations cannot retain a stale chart or PDF.");
        Check(first.LastOutput != duplicate.LastOutput && first.LastChart != duplicate.LastChart, "Per-item selected export and open-chart paths stay isolated.");
        var firstInventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(Path.Combine(first.LastOutput!, "inventory.json")))!;
        var duplicateInventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(Path.Combine(duplicate.LastOutput!, "inventory.json")))!;
        Check(firstInventory.Devices[0].SerialNumber == "FIRST-ONLY" && duplicateInventory.Devices[0].SerialNumber == "", "Saved reports do not leak manual details between duplicates.");
        Check(exported.CombinedError.Length == 0 && File.Exists(exported.CombinedDiagramsPath) && File.Exists(exported.CombinedBookletPath), "Combined diagram and booklet PDFs must be produced for successful items.");
        string pdf = File.ReadAllText(exported.CombinedDiagramsPath!, System.Text.Encoding.Latin1);
        Check(Regex.Matches(pdf, @"/Type /Page\b").Count == 3, "Combined diagram PDF has exactly one separate A4 page per successful configuration.");
        Check(File.ReadAllText(Path.Combine(exported.OutputDirectory, "workspace-export.json")).Contains("\"FailureCount\": 1"), "Partial failure remains explicit in the batch manifest.");

        var small = new ConfigurationWorkspace(); small.AddText(RouterOs, "same.rsc");
        var again = WorkspaceExport.Export(small, root, combinedPdf: false);
        Check(again.OutputDirectory != exported.OutputDirectory && File.Exists(exported.CombinedDiagramsPath), "Repeated builds create new batch output without overwriting previous exports.");
        Check(again.CombinedDiagramsPath == null && again.CombinedBookletPath == null, "Combined PDFs are an explicit batch option.");
        File.WriteAllText(Path.Combine(root, "batch-to-inspect.txt"), exported.OutputDirectory);
        workspace.Remove(first.Id);
        Check(workspace.SelectedId == duplicate.Id && workspace.Dirty, "Removing selected item selects the nearest remaining item.");
        foreach (var item in workspace.Items.ToArray()) workspace.Remove(item.Id);
        Check(workspace.Selected == null && workspace.Items.Count == 0, "Removing all items leaves a coherent empty workspace.");
    }
}
