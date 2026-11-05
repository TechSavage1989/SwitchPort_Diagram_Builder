using System.IO.Compression;
using System.Text;
using SwitchportGenerator;

static class SupplementaryChecks
{
    static int assertions;
    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Entry(ZipArchive zip, string path, string text)
    {
        using var stream = zip.CreateEntry(path).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }

    static string Sheet(params string[][] rows)
    {
        var xml = new StringBuilder("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        for (int r = 0; r < rows.Length; r++)
        {
            xml.Append("<row r=\"").Append(r + 1).Append("\">");
            for (int c = 0; c < rows[r].Length; c++)
                xml.Append("<c r=\"").Append((char)('A' + c)).Append(r + 1)
                    .Append("\" t=\"inlineStr\"><is><t>")
                    .Append(System.Security.SecurityElement.Escape(rows[r][c]))
                    .Append("</t></is></c>");
            xml.Append("</row>");
        }
        return xml.Append("</sheetData></worksheet>").ToString();
    }

    static string CreateAuvik(string folder)
    {
        string file = Path.Combine(folder, "fictional-auvik.xlsx");
        using var archive = ZipFile.Open(file, ZipArchiveMode.Create);
        Entry(archive, "xl/workbook.xml", """
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Interfaces" sheetId="1" r:id="rId1"/><sheet name="Routes" sheetId="2" r:id="rId2"/></sheets>
            </workbook>
            """);
        Entry(archive, "xl/_rels/workbook.xml.rels", """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Target="worksheets/sheet1.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"/>
              <Relationship Id="rId2" Target="worksheets/sheet2.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"/>
            </Relationships>
            """);
        Entry(archive, "xl/worksheets/sheet1.xml", Sheet(
            new[] { "INTERFACES" },
            new[] { "Admin Status", "Oper Status", "Interface Name", "MAC Address", "Type", "Parent Device", "Negotiated Speed", "Connected To", "Configuration Details" },
            new[] { "Up", "Up", "GigabitEthernet1/0/1", "02:00:00:00:00:01", "Ethernet", "LAB-SW-01.example.com", "1.0 Gbit/s", "LAB-CORE through Gi1/0/48", "VLAN 20" },
            new[] { "Up", "Down", "GigabitEthernet1/0/2", "02:00:00:00:00:02", "Ethernet", "OTHER-SW", "1.0 Gbit/s", "OTHER-ENDPOINT", "VLAN 30" },
            new[] { "Up", "Up", "GigabitEthernet1/0/9", "02:00:00:00:00:09", "Ethernet", "LAB-SW-02.example.com", "1.0 Gbit/s", "LAB-WAP-02", "VLAN 50" }
        ));
        Entry(archive, "xl/worksheets/sheet2.xml", Sheet(
            new[] { "ROUTES" },
            new[] { "Device Name", "Destination", "Next Hop", "Metric", "Gateway Interface" },
            new[] { "LAB-SW-01", "192.0.2.0/24", "198.51.100.1", "10", "Vlan20" },
            new[] { "OTHER-SW", "203.0.113.0/24", "198.51.100.2", "10", "Vlan30" },
            new[] { "LAB-SW-02", "192.0.2.128/25", "198.51.100.3", "20", "Vlan50" }
        ));
        return file;
    }

    public static int Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "switchport-supplementary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string text = """
                LAB-SW-01#show interfaces
                GigabitEthernet1/0/1 is up, line protocol is up
                Internet address is 192.0.2.10/24
                LAB-SW-01#show lldp neighbors detail
                Local Intf: Gi1/0/1
                System Name: LAB-CORE
                Port id: Gi1/0/48

                LAB-SW-01#show ip route
                S* 0.0.0.0/0 [1/0] via 198.51.100.1
                LAB-SW-01#show ip dhcp relay
                ip helper-address 192.0.2.53
                secret password: fictional
                """;
            var show = SupplementaryData.FromText(text, "fictional-show.txt", "LAB-SW-01");
            Check(show.Observations.Any(r => r.Category == "Neighbor" && r.Value.Contains("LAB-CORE")), "LLDP neighbor was not identified.");
            Check(show.Observations.Any(r => r.Category == "Route" && r.Field == "0.0.0.0/0"), "Static route was not identified.");
            Check(show.Observations.Any(r => r.Category == "DHCP relay" && r.Value == "192.0.2.53"), "DHCP relay was not identified.");
            Check(show.Observations.Any(r => r.Category == "IP interface" && r.Value == "192.0.2.10/24"), "IP interface address was not identified.");
            Check(!show.Observations.Any(r => r.Value.Contains("fictional")), "Sensitive output should be redacted.");
            Check(SupplementaryData.InterfaceKey("GigabitEthernet1/0/1") == SupplementaryData.InterfaceKey("Gi1/0/1"), "Cisco interface aliases do not match.");

            string workbook = CreateAuvik(folder);
            var auvik = SupplementaryData.FromAuvik(workbook, "LAB-SW-01");
            Check(auvik.Observations.Any(r => r.Category == "Neighbor" && r.Value.Contains("LAB-CORE")), "Auvik connected device was not identified.");
            Check(auvik.Observations.Any(r => r.Category == "Route" && r.Field == "192.0.2.0/24"), "Auvik route was not identified.");
            Check(auvik.Observations.All(r => !r.Value.Contains("OTHER-ENDPOINT") && !r.Value.Contains("203.0.113.0")), "Another device's report rows were imported.");
            bool mismatchBlocked = false;
            try { SupplementaryData.FromAuvik(workbook, "NO-MATCH"); }
            catch (InvalidOperationException) { mismatchBlocked = true; }
            Check(mismatchBlocked, "Unmatched Auvik device was accepted.");

            var inv = new Inventory { Hostname = "LAB-SW-01", Ports = new()
            {
                new() { Id = "GigabitEthernet1/0/1", UntaggedVlan = 20, TaggedVlans = new() { 30 }, Mode = "Trunk" },
                new() { Id = "GigabitEthernet1/0/2", UntaggedVlan = 40, Description = "Configured port" }
            }};
            SupplementaryData.Apply(inv, new[] { auvik });
            Check(inv.Ports[0].ConnectedDevice.Contains("LAB-CORE"), "Observed connected device was not reflected in an empty port field.");
            Check(inv.Ports[0].LiveState.Contains("Up"), "Observed operational status was not added.");
            Check(inv.Ports[0].Speed.Contains("1.0 Gbit/s"), "Observed speed was not added.");
            Check(inv.Ports[0].UntaggedVlan == 20 && inv.Ports[0].TaggedVlans.SequenceEqual(new[] { 30 }) && inv.Ports[0].Mode == "Trunk", "Supplementary data changed configured port attributes.");
            Check(inv.Ports[1].UntaggedVlan == 40 && inv.Ports[1].ConnectedDevice == "Unknown/Confirm before use", "Unrelated port changed.");

            SupplementaryData.WriteExports(folder, new[] { auvik, show });
            Check(File.Exists(Path.Combine(folder, "supplementary-data.json")), "Supplementary JSON missing.");
            Check(File.ReadAllText(Path.Combine(folder, "supplementary-data.csv")).Contains("LAB-CORE"), "Supplementary CSV missing neighbor.");
            var ws = new ConfigurationWorkspace();
            var item = ws.AddText("hostname LAB-SW-01\ninterface 1/1/1\n no shutdown\n", "fictional.cfg");
            ws.AddSupplementary(item.Id, show);
            Check(ws.Selected?.SupplementarySources.Count == 1, "Workspace did not own supplementary source.");
            var loaded = ConfigurationWorkspace.Deserialize(ws.Serialize());
            Check(loaded.Selected?.SupplementarySources.Count == 1, "Supplementary sources were lost when reopening workspace.");
            loaded.RemoveSupplementary(loaded.SelectedId!, loaded.Selected!.SupplementarySources[0].Id);
            Check(loaded.Selected?.SupplementarySources.Count == 0, "Supplementary source removal failed.");
            Check(ws.Selected?.SupplementarySources.Count == 1, "Loaded workspace mutated its original instance.");

            // Automatic drag-and-drop: validate mixed order, multi-device Auvik
            // matching, show-output routing and backward-compatible configs.
            Check(SupplementaryData.LooksLikeCommandOutput(text), "Operational show output must auto-detect.");
            Check(!SupplementaryData.LooksLikeCommandOutput("hostname LAB-SW-01\ninterface 1/1/1\n show configuration\n"), "Primary config must not be interpreted as operational output.");
            Check(!SupplementaryData.LooksLikeCommandOutput("LAB-SW-01#show running-config\nhostname LAB-SW-01\n"), "show running-config remains a primary configuration.");
            Check(SupplementaryData.PromptHostname(text) == "LAB-SW-01", "Command prompt hostname must be recognized.");

            byte[] workbookBytes = File.ReadAllBytes(workbook);
            var multiple = SupplementaryData.FromAuvikBytes(workbookBytes, "fictional-auvik.xlsx",
                new[] { "LAB-SW-01", "LAB-SW-02", "Unknown" });
            Check(multiple.Count == 2, "Site-wide workbook should match both known hostnames.");
            Check(multiple["LAB-SW-02"].Observations.Any(o => o.Value.Contains("LAB-WAP-02")), "Second device observations were not isolated.");
            Check(multiple["LAB-SW-01"].Observations.All(o => !o.Value.Contains("LAB-WAP-02")), "Cross-device observations leaked.");

            var mixed = new ConfigurationWorkspace();
            var mixedFiles = new[]
            {
                new DroppedNetworkFile("fictional-auvik.xlsx", Convert.ToBase64String(workbookBytes), "xlsx-base64", null),
                new DroppedNetworkFile("show-neighbors.log", text, "text", null),
                new DroppedNetworkFile("access-1.cfg", "hostname LAB-SW-01\ninterface 1/1/1\n no shutdown\n", "text", null),
                new DroppedNetworkFile("access-2.cfg", "hostname LAB-SW-02\ninterface 1/1/9\n no shutdown\n", "text", null)
            };
            var imported = WorkspaceDropImporter.Import(mixed, mixedFiles, null);
            Check(imported.Errors.Count == 0, "Mixed Auvik/config/command import errors: " + string.Join("; ", imported.Errors));
            Check(imported.ConfigurationsAdded == 2, "Mixed drop lost primary configurations.");
            Check(imported.SupplementarySourcesAdded == 3 && imported.MatchedItemIds.Count == 2, "Mixed drop should attach two Auvik sources and one CLI source.");
            var first = mixed.Items.Single(i => i.Inventory.Hostname == "LAB-SW-01");
            var second = mixed.Items.Single(i => i.Inventory.Hostname == "LAB-SW-02");
            Check(first.SupplementarySources.Count == 2 && second.SupplementarySources.Count == 1, "Sources attached to wrong primary device.");
            Check(first.Text.Contains("no shutdown") && second.Text.Contains("no shutdown"), "Supplementary import changed source configuration.");
            Check(mixed.SelectedId == second.Id, "Existing configuration drop selection behavior changed.");
            var reopened = ConfigurationWorkspace.Deserialize(mixed.Serialize());
            Check(reopened.Items.Single(i => i.Inventory.Hostname == "LAB-SW-01").SupplementarySources.Count == 2, "Mixed-import sources did not persist.");

            var onlyWorkbook = WorkspaceDropImporter.Import(new ConfigurationWorkspace(),
                new[] { mixedFiles[0] }, null);
            Check(onlyWorkbook.ConfigurationsAdded == 0 && onlyWorkbook.SupplementarySourcesAdded == 0 &&
                onlyWorkbook.Errors.Any(e => e.Contains("matching switch config")), "Unmatched Auvik workbook was silently treated as a configuration.");

            var onlyCli = WorkspaceDropImporter.Import(new ConfigurationWorkspace(),
                new[] { mixedFiles[1] }, null);
            Check(onlyCli.ConfigurationsAdded == 0 && onlyCli.SupplementarySourcesAdded == 0 &&
                onlyCli.Errors.Count == 1, "Operational output without a matching config must not become a switch.");

            var wrongPrompt = WorkspaceDropImporter.Import(mixed,
                new[] { new DroppedNetworkFile("wrong.log", "OTHER-SW#show ip route\nS* 0.0.0.0/0 via 192.0.2.1\n", "text", null) },
                first.Id);
            Check(wrongPrompt.SupplementarySourcesAdded == 0 && wrongPrompt.Errors.Count == 1, "Mismatched CLI prompt must not attach to selected device.");
            var routePrompt = WorkspaceDropImporter.Import(mixed,
                new[] { new DroppedNetworkFile("route.log", "LAB-SW-02#show ip route\nS* 0.0.0.0/0 via 198.51.100.3\n", "text", null) },
                first.Id);
            Check(routePrompt.SupplementarySourcesAdded == 1 && second.SupplementarySources.Count == 2, "Prompt matching must supersede different selected switch.");

            var unknownCsv = WorkspaceDropImporter.Import(mixed,
                new[] { new DroppedNetworkFile("unknown.csv", "Header,Unrecognized\n1,2\n", "text", null) }, first.Id);
            Check(unknownCsv.ConfigurationsAdded == 0 && unknownCsv.Errors.Count == 1, "Unknown CSV must never be imported as a configuration.");
            var onlySupp = WorkspaceDropImporter.Import(mixed,
                new[] { new DroppedNetworkFile("new.cfg", "hostname TEST-SW-03\n", "text", null) }, first.Id, true);
            Check(onlySupp.ConfigurationsAdded == 0 && onlySupp.Errors.Count == 1, "Drop into supplementary dialog must reject primary configs.");

            var duplicates = new ConfigurationWorkspace();
            var duplicateA = duplicates.AddText("hostname LAB-SW-01\n", "snapshot-a.cfg");
            var duplicateB = duplicates.AddText("hostname LAB-SW-01\n", "snapshot-b.cfg");
            var ambiguous = WorkspaceDropImporter.Import(duplicates, new[] { mixedFiles[0] }, null);
            Check(ambiguous.SupplementarySourcesAdded == 0 && ambiguous.Errors.Count > 0, "Duplicate hostname must not receive workbook automatically.");
            var explicitlyTargeted = WorkspaceDropImporter.Import(duplicates, new[] { mixedFiles[0] }, duplicateA.Id);
            Check(explicitlyTargeted.SupplementarySourcesAdded == 1 && duplicateA.SupplementarySources.Count == 1 &&
                duplicateB.SupplementarySources.Count == 0, "Explicit device target should resolve duplicate hostname safely.");

            var invalidExcel = WorkspaceDropImporter.Import(mixed,
                new[] { new DroppedNetworkFile("not-an-excel.xlsx", Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }), "xlsx-base64", null) }, first.Id);
            Check(invalidExcel.ConfigurationsAdded == 0 && invalidExcel.SupplementarySourcesAdded == 0 &&
                invalidExcel.Errors.Count == 1, "Malformed Excel input must not enter configuration inventory.");

            Console.WriteLine("PASS SupplementaryChecks (" + assertions + " assertions)");
            return 0;
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
