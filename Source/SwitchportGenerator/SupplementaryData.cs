using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SwitchportGenerator;

// Supplementary observations are read-only evidence. They must never determine
// physical hardware, native/tagged VLANs, LAGs, or administrative configuration.
public sealed class SupplementaryObservation
{
    public string Category { get; set; } = "";
    public string Interface { get; set; } = "";
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class SupplementarySource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Device { get; set; } = "";
    public List<SupplementaryObservation> Observations { get; set; } = new();
}

public static class SupplementaryData
{
    public const int MaxSources = 20;
    public const int MaxObservations = 5000;
    public const int MaxTextBytes = 20_000_000;
    public const int MaxWorkbookBytes = 25_000_000;
    static readonly Regex Sensitive = new(@"(?i)\b(passw(?:or)?d|secret|credential|private.key|token|psk|encrypted|authorization|api.key|snmp.community|community.string|shared.secret|enable.secret|private.pem)\b", RegexOptions.Compiled);
    static readonly Regex IpAddress = new(@"\b(?:\d{1,3}\.){3}\d{1,3}(?:/\d{1,2})?\b", RegexOptions.Compiled);
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Clean(string? value, int max = 240)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string text = new(value.Trim().Where(c => !char.IsControl(c) || c == '\t').ToArray());
        if (Sensitive.IsMatch(text)) return "[REDACTED]";
        return text.Length <= max ? text : text[..max] + "...";
    }

    // A text capture must have an operational command or unmistakable show output
    // before auto-import. "show running-config" and saved configurations stay primary.
    public static bool LooksLikeCommandOutput(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (Regex.IsMatch(text, @"(?im)^\s*(?:[\w.-]+[#>$]\s*)?(?:show|display|get|diagnose)\s+(?!running-config\b|startup-config\b|current-configuration\b|configuration\b)(?:ip|ipv6|interfaces?|ports?|lldp|cdp|mac|dhcp|arp|routes?|routing|router|switch|system|version|inventory|vlan|neighbors?|ethernet|firewall)\b"))
            return true;
        return Regex.IsMatch(text, @"(?im)^[\w./-]+\s+is\s+(?:administratively down|up|down),?\s+line protocol is\s+(?:up|down)\b") ||
            (Regex.IsMatch(text, @"(?im)^\s*(?:Local (?:Intf|Interface|Port))\s*:", RegexOptions.IgnoreCase) &&
             Regex.IsMatch(text, @"(?im)^\s*(?:System Name|Device ID|Port id)\s*:", RegexOptions.IgnoreCase));
    }

    // Only a command-prompt identity is auto-matched to an existing workspace item.
    public static string PromptHostname(string text)
    {
        var matches = Regex.Matches(text, @"(?im)^\s*([\w.-]{1,180})[#>$]\s*(?:show|display|get|diagnose)\s+");
        var names = matches.Cast<Match>().Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        return names.Length == 1 ? names[0] : "";
    }

    public static string HostKey(string? name) => string.IsNullOrWhiteSpace(name) ? "" :
        name.Trim().TrimEnd('.').Split('.')[0].ToUpperInvariant();

    public static string InterfaceKey(string? value)
    {
        string s = (value ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
        s = Regex.Replace(s, @"^(?:gigabitethernet|gigeth|gig|gi)(?=\d)", "gi");
        s = Regex.Replace(s, @"^(?:tengigabitethernet|tengigeth|tengige|te)(?=\d)", "te");
        s = Regex.Replace(s, @"^(?:twentyfivegige|twentyfivegigabitethernet|twentyfiveg)(?=\d)", "twentyfiveg");
        s = Regex.Replace(s, @"^(?:fastethernet|fa)(?=\d)", "fa");
        s = Regex.Replace(s, @"^(?:ethernet|eth)(?=\d)", "eth");
        return s;
    }

    static void Add(SupplementarySource source, string category, string port, string field, string value)
    {
        string safe = Clean(value);
        if (safe.Length == 0 || safe == "0" || safe.Equals("No data available.", StringComparison.OrdinalIgnoreCase)) return;
        if (source.Observations.Count >= MaxObservations)
            throw new InvalidOperationException("Supplementary source exceeds 5,000 observations. Split the report by device or section.");
        source.Observations.Add(new SupplementaryObservation
        {
            Category = Clean(category, 40), Interface = Clean(port, 100),
            Field = Clean(field, 80), Value = safe
        });
    }

    public static SupplementarySource FromText(string text, string name, string device)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Supplementary command output is empty.");
        if (Encoding.UTF8.GetByteCount(text) > MaxTextBytes || text.Contains('\0'))
            throw new InvalidOperationException("Command output exceeds 20 MB or contains binary data.");
        var source = new SupplementarySource { Name = Clean(name, 180), Kind = "Command output", Device = Clean(device, 180) };
        string command = "Unlabeled output", local = "", remote = "", remotePort = "";
        int referenceLines = 0;
        void FlushNeighbor()
        {
            if (local.Length > 0 && remote.Length > 0)
                Add(source, "Neighbor", local, "Connected device", remote + (remotePort.Length > 0 ? " / " + remotePort : ""));
            remote = ""; remotePort = "";
        }
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) { FlushNeighbor(); continue; }
            var prompt = Regex.Match(line, @"^(?:(?:[\w.-]+)[#>$]\s*)?(?:show|display|get|diagnose)\s+(.+)$", RegexOptions.IgnoreCase);
            if (prompt.Success) { FlushNeighbor(); command = Clean(prompt.Groups[1].Value, 80); local = ""; continue; }
            var interfaceHeader = Regex.Match(line, @"^([\w./-]+)\s+is\s+(administratively down|up|down)\b", RegexOptions.IgnoreCase);
            if (interfaceHeader.Success)
            {
                FlushNeighbor(); local = interfaceHeader.Groups[1].Value;
                Add(source, "Interface", local, "Operational status", interfaceHeader.Groups[2].Value);
            }
            var brief = Regex.Match(line, @"^(?<port>(?:Gi|Te|Fa|Eth|GigabitEthernet|TenGigabitEthernet|FastEthernet)[\w./-]+)\s+(?<address>(?:\d{1,3}\.){3}\d{1,3}|unassigned)\s+\S+\s+\S+\s+(?<state>up|down|administratively down)\s+(?<protocol>up|down)$", RegexOptions.IgnoreCase);
            if (brief.Success && Regex.IsMatch(command, @"interface", RegexOptions.IgnoreCase))
            {
                Add(source, "IP interface", brief.Groups["port"].Value, "Address", brief.Groups["address"].Value);
                Add(source, "Interface", brief.Groups["port"].Value, "Operational status", brief.Groups["state"].Value + " / " + brief.Groups["protocol"].Value);
            }
            if (Regex.IsMatch(command, @"(?:lldp|cdp).*neighbor", RegexOptions.IgnoreCase))
            {
                var neighborLine = Regex.Match(line, @"^(?<device>\S+)\s+(?<local>(?:Gi|Te|Fa|Eth|Po|GigabitEthernet|TenGigabitEthernet)[\w./-]+)\s+\d+\s+.+?\s+(?<remote>(?:Gi|Te|Fa|Eth|Po|GigabitEthernet|TenGigabitEthernet)[\w./-]+)$", RegexOptions.IgnoreCase);
                if (neighborLine.Success)
                    Add(source, "Neighbor", neighborLine.Groups["local"].Value, "Connected device",
                        neighborLine.Groups["device"].Value + " / " + neighborLine.Groups["remote"].Value);
            }
            var localMatch = Regex.Match(line, @"^(?:Local (?:Intf|Interface|Port)|Interface)\s*:\s*([\w./-]+)", RegexOptions.IgnoreCase);
            if (localMatch.Success) { if (remote.Length == 0) FlushNeighbor(); local = localMatch.Groups[1].Value; }
            var remoteMatch = Regex.Match(line, @"^(?:System Name|Device ID|Neighbor)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
            if (remoteMatch.Success) remote = remoteMatch.Groups[1].Value.Trim();
            var remotePortMatch = Regex.Match(line, @"^(?:Port [iI][dD]|Port ID \(outgoing port\)|Remote Port)\s*:\s*(.+)$");
            if (remotePortMatch.Success) remotePort = remotePortMatch.Groups[1].Value.Trim();
            var cdpPair = Regex.Match(line, @"^Interface:\s*([\w./-]+).*?Port ID \(outgoing port\):\s*([\w./-]+)", RegexOptions.IgnoreCase);
            if (cdpPair.Success)
            {
                local = cdpPair.Groups[1].Value;
                remotePort = cdpPair.Groups[2].Value;
                FlushNeighbor();
            }
            var address = Regex.Match(line, @"^(?:Internet address is|IP(?:v4)? Address\s*[:=]|ip address)\s+(.+)$", RegexOptions.IgnoreCase);
            if (address.Success && IpAddress.IsMatch(address.Groups[1].Value))
                Add(source, "IP interface", local, "Address", address.Groups[1].Value);
            var relay = Regex.Match(line, @"\b(?:ip helper-address|dhcp-relay(?:\s+address|\s+server)?|relay-to|relay address|dhcp-relay-ip)\s*[:=]?\s*((?:\d{1,3}\.){3}\d{1,3})", RegexOptions.IgnoreCase);
            if (relay.Success) Add(source, "DHCP relay", local, "Relay target", relay.Groups[1].Value);
            if (Regex.IsMatch(command, @"dhcp", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(line, @"\b(?:pool|scope|lease|server|relay|excluded-address|range)\b", RegexOptions.IgnoreCase))
                Add(source, "DHCP", local, command, line);
            var route = Regex.Match(line, @"(?<dest>(?:\d{1,3}\.){3}\d{1,3}/\d{1,2}|default)\b.*?\b(?:via|gateway|next.hop)\s+(?<hop>(?:\d{1,3}\.){3}\d{1,3})", RegexOptions.IgnoreCase);
            if (route.Success && (Regex.IsMatch(command, @"route|routing", RegexOptions.IgnoreCase) || Regex.IsMatch(line, @"^\s*[A-Z* ]{1,5}(?:\s+|[0-9])")))
                Add(source, "Route", "", route.Groups["dest"].Value, "Next hop " + route.Groups["hop"].Value + " | " + line);
            var connectedRoute = Regex.Match(line, @"^[CL]\*?\s+(?<dest>(?:\d{1,3}\.){3}\d{1,3}/\d{1,2})\s+is\s+directly\s+connected,?\s*(?<iface>\S+)?", RegexOptions.IgnoreCase);
            if (connectedRoute.Success && Regex.IsMatch(command, @"route|routing", RegexOptions.IgnoreCase))
                Add(source, "Route", connectedRoute.Groups["iface"].Value, connectedRoute.Groups["dest"].Value, "Directly connected");
            if (Regex.IsMatch(command, @"lldp|cdp|neighbor|interface|route|dhcp|relay|arp|mac.address|vlan", RegexOptions.IgnoreCase)
                && referenceLines < 400 && line.Length > 3)
            {
                Add(source, "Reference", local, command, line);
                referenceLines++;
            }
        }
        FlushNeighbor();
        if (source.Observations.Count == 0)
            throw new InvalidOperationException("No supported supplementary data was found. Include the show/display command header or a recognized interface, route, DHCP or neighbor entry.");
        return source;
    }

    // Minimal, bounded Open XML reader; no Excel, external packages, macros or formulas executed.
    static XDocument ReadXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidOperationException("Workbook is missing " + path + ".");
        if (entry.Length > 30_000_000) throw new InvalidOperationException("Workbook section is too large.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 30_000_000, IgnoreWhitespace = true
        });
        return XDocument.Load(reader);
    }

    static int ColumnIndex(string cell)
    {
        int index = 0;
        foreach (char c in cell)
        {
            if (!char.IsLetter(c)) break;
            index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1;
        }
        return index - 1;
    }

    static Dictionary<string, List<string[]>> ReadTables(Stream file)
    {
        using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 400 || zip.Entries.Sum(e => e.Length) > 120_000_000)
            throw new InvalidOperationException("Workbook exceeds safe import limits.");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var workbook = ReadXml(zip, "xl/workbook.xml");
        var relationships = ReadXml(zip, "xl/_rels/workbook.xml.rels");
        var locations = relationships.Descendants().Where(x => x.Name.LocalName == "Relationship")
            .Where(x => (string?)x.Attribute("Id") != null && (string?)x.Attribute("Target") != null)
            .ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);
        List<string> shared = new();
        if (zip.GetEntry("xl/sharedStrings.xml") != null)
            shared = ReadXml(zip, "xl/sharedStrings.xml").Descendants(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))).ToList();
        var result = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in workbook.Descendants(ns + "sheet"))
        {
            string title = (string?)sheet.Attribute("name") ?? "";
            if (title is not ("Devices" or "Network Elements" or "Interfaces" or "Routes" or "ARP Entries" or "Forwarding Database (FDB)")) continue;
            string id = (string?)sheet.Attribute(rel + "id") ?? "";
            if (!locations.TryGetValue(id, out var target)) continue;
            string part = target.StartsWith("/xl/", StringComparison.OrdinalIgnoreCase) ? target[1..] :
                target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target.TrimStart('/');
            if (part.Contains("..", StringComparison.Ordinal) || !part.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)) continue;
            var rows = new List<string[]>();
            foreach (var row in ReadXml(zip, part).Descendants(ns + "sheetData").Descendants(ns + "row"))
            {
                var values = new string[16];
                foreach (var cell in row.Elements(ns + "c"))
                {
                    int index = ColumnIndex((string?)cell.Attribute("r") ?? "");
                    if (index < 0 || index >= values.Length) continue;
                    string type = (string?)cell.Attribute("t") ?? "";
                    string raw = type == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(t => t.Value)) :
                        (string?)cell.Element(ns + "v") ?? "";
                    if (type == "s" && int.TryParse(raw, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < shared.Count)
                        raw = shared[sharedIndex];
                    values[index] = Clean(raw);
                }
                rows.Add(values);
                if (rows.Count > 100_000) throw new InvalidOperationException("Workbook has too many rows.");
            }
            result[title] = rows;
        }
        return result;
    }

    static bool SameDevice(string? candidate, string target)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(target)) return false;
        string a = candidate.Trim().TrimEnd('.'), b = target.Trim().TrimEnd('.');
        return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
            a.Split('.')[0].Equals(b.Split('.')[0], StringComparison.OrdinalIgnoreCase);
    }

    public static SupplementarySource FromAuvik(string path, string expectedHostname)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > MaxWorkbookBytes)
            throw new InvalidOperationException("Choose an Auvik .xlsx report smaller than 25 MB.");
        using var input = File.OpenRead(path);
        var result = FromAuvikTables(ReadTables(input), Path.GetFileName(path), expectedHostname);
        return result ?? throw new InvalidOperationException("No Auvik rows matched \"" + Clean(expectedHostname, 90) +
            "\". Select the matching configuration or check the report's device name.");
    }

    // Read the workbook once even for a site-wide Auvik export containing many devices.
    // Entries are kept per matching hostname, never merged with configuration data.
    public static Dictionary<string, SupplementarySource> FromAuvikBytes(
        byte[] bytes, string name, IEnumerable<string> expectedHostnames)
    {
        if (bytes == null || bytes.Length == 0 || bytes.Length > MaxWorkbookBytes)
            throw new InvalidOperationException("Auvik workbook must be smaller than 25 MB.");
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4b)
            throw new InvalidOperationException("The dropped .xlsx file is not a valid Excel workbook.");
        using var input = new MemoryStream(bytes, writable: false);
        var tables = ReadTables(input);
        var results = new Dictionary<string, SupplementarySource>(StringComparer.OrdinalIgnoreCase);
        foreach (string hostname in expectedHostnames.Where(h => HostKey(h).Length > 0 && !h.Equals("Unknown", StringComparison.OrdinalIgnoreCase)))
        {
            string key = HostKey(hostname);
            if (results.ContainsKey(key)) continue;
            var source = FromAuvikTables(tables, name, hostname);
            if (source != null) results.Add(key, source);
        }
        return results;
    }

    static SupplementarySource? FromAuvikTables(
        Dictionary<string, List<string[]>> tables, string name, string expectedHostname)
    {
        if (string.IsNullOrWhiteSpace(expectedHostname) ||
            expectedHostname.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            return null;
        var source = new SupplementarySource
        {
            Name = Clean(name, 180), Kind = "Auvik workbook",
            Device = Clean(expectedHostname, 180)
        };
        foreach (var pair in tables)
        {
            var rows = pair.Value;
            if (rows.Count < 3) continue;
            var headers = rows[1].Select((value, index) => (value, index)).Where(entry => !string.IsNullOrWhiteSpace(entry.value))
                .ToDictionary(entry => entry.value, entry => entry.index, StringComparer.OrdinalIgnoreCase);
            string Get(string[] row, string key) => headers.TryGetValue(key, out int index) ? row[index] ?? "" : "";
            foreach (var row in rows.Skip(2))
            {
                string owner = pair.Key == "Interfaces" ? Get(row, "Parent Device") : Get(row, "Device Name");
                if (!SameDevice(owner, expectedHostname)) continue;
                string iface = pair.Key == "Interfaces" ? Get(row, "Interface Name") :
                    pair.Key == "Routes" ? Get(row, "Gateway Interface") : Get(row, "Interface");
                switch (pair.Key)
                {
                    case "Interfaces":
                        Add(source, "Interface", iface, "Admin status", Get(row, "Admin Status"));
                        Add(source, "Interface", iface, "Operational status", Get(row, "Oper Status"));
                        Add(source, "Interface", iface, "Speed", Get(row, "Negotiated Speed"));
                        Add(source, "Interface", iface, "MAC", Get(row, "MAC Address"));
                        Add(source, "Interface", iface, "Type", Get(row, "Type"));
                        Add(source, "Neighbor", iface, "Connected device", Get(row, "Connected To"));
                        Add(source, "Reference", iface, "Auvik configuration details", Get(row, "Configuration Details"));
                        break;
                    case "Routes":
                        Add(source, "Route", iface, Get(row, "Destination"),
                            "Next hop: " + Get(row, "Next Hop") + " | Metric: " + Get(row, "Metric"));
                        break;
                    case "ARP Entries":
                        Add(source, "ARP", iface, Get(row, "IP Address"), Get(row, "Hardware (MAC) Address"));
                        break;
                    case "Forwarding Database (FDB)":
                        Add(source, "MAC table", iface, Get(row, "Destination MAC Address"), "VLAN: " + Get(row, "VLAN"));
                        break;
                    case "Devices":
                    case "Network Elements":
                        Add(source, "Device", "", "Model", Get(row, "Make & Model"));
                        Add(source, "Device", "", "IP addresses", Get(row, "IP Address(es)"));
                        Add(source, "Device", "", "Connected to", Get(row, "Connected To"));
                        Add(source, "Device", "", "Software version", Get(row, "Software Version"));
                        break;
                }
            }
        }
        return source.Observations.Count == 0 ? null : source;
    }

    public static void Validate(IEnumerable<SupplementarySource>? sources)
    {
        if (sources == null) throw new InvalidOperationException("Supplementary source list is invalid.");
        var list = sources.ToList();
        if (list.Count > MaxSources) throw new InvalidOperationException("A configuration supports up to 20 supplementary sources.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in list)
        {
            if (source == null || !Guid.TryParseExact(source.Id, "N", out _) || !ids.Add(source.Id) ||
                source.Name == null || source.Kind == null || source.Device == null || source.Observations == null ||
                source.Name.Length > 200 || source.Device.Length > 200 || source.Observations.Count > MaxObservations)
                throw new InvalidOperationException("Supplementary source metadata is invalid.");
            foreach (var record in source.Observations)
                if (record == null || record.Category == null || record.Interface == null || record.Field == null ||
                    record.Value == null || record.Category.Length > 40 || record.Interface.Length > 100 ||
                    record.Field.Length > 80 || record.Value.Length > 260 ||
                    new[] { record.Category, record.Interface, record.Field, record.Value }.Any(s => s.Any(c => char.IsControl(c) && c != '\t')))
                    throw new InvalidOperationException("Supplementary observation data is invalid.");
        }
    }

    public static void Apply(Inventory inventory, IEnumerable<SupplementarySource> sources)
    {
        foreach (var source in sources)
        {
            foreach (var port in inventory.Ports)
            {
                string key = InterfaceKey(port.Id);
                var records = source.Observations.Where(r => r.Interface.Length > 0 && InterfaceKey(r.Interface) == key).ToList();
                if (records.Count == 0) continue;
                var neighbor = records.FirstOrDefault(r => r.Category == "Neighbor" && r.Field == "Connected device");
                if (neighbor != null && port.ConnectedDevice == "Unknown/Confirm before use")
                    port.ConnectedDevice = "Observed: " + Clean(neighbor.Value, 110);
                var state = records.FirstOrDefault(r => r.Category == "Interface" && r.Field == "Operational status");
                if (state != null && port.LiveState == "Unknown/Confirm before use")
                    port.LiveState = "Observed: " + Clean(state.Value, 65);
                var speed = records.FirstOrDefault(r => r.Category == "Interface" && r.Field == "Speed");
                if (speed != null && port.Speed == "Not specified")
                    port.Speed = "Observed: " + Clean(speed.Value, 65);
                foreach (var row in records.Where(r => r.Category is "Neighbor" or "DHCP relay" or "IP interface").Take(4))
                    port.Notes.Add("Supplementary (" + source.Name + "): " + row.Field + ": " + Clean(row.Value, 150));
            }
            inventory.Assumptions.Add("Supplementary observations from " + source.Name +
                " (" + source.Kind + ", " + source.Observations.Count + " records) are snapshots, not authoritative configuration or physical connectivity.");
        }
    }

    public static string Summary(IReadOnlyList<SupplementarySource> sources)
    {
        if (sources.Count == 0) return "";
        var output = new StringBuilder();
        output.AppendLine().AppendLine("SUPPLEMENTARY NETWORK OBSERVATIONS (NOT LIVE CONFIGURATION)");
        output.AppendLine(new string('=', 72));
        foreach (var source in sources)
        {
            output.AppendLine().AppendLine("Source: " + Clean(source.Name) + " | " + source.Kind + " | " +
                source.Observations.Count + " observations");
            output.AppendLine("Device: " + Clean(source.Device));
            foreach (var category in source.Observations.GroupBy(item => item.Category).OrderBy(group => group.Key))
            {
                output.AppendLine("  " + category.Key + " (" + category.Count() + ")");
                int shown = 0;
                int limit = category.Key == "Reference" ? 4 : 25;
                foreach (var record in category)
                {
                    if (shown++ >= limit) break;
                    output.AppendLine("    " + (record.Interface.Length > 0 ? record.Interface + " | " : "") +
                        record.Field + ": " + Clean(record.Value));
                }
                if (category.Count() > limit)
                    output.AppendLine("    ... " + (category.Count() - limit) + " more in supplementary-data.csv.");
            }
        }
        output.AppendLine().AppendLine("Observations are source-attributed snapshots. Confirm before production changes.");
        return output.ToString();
    }

    static string Csv(string? value)
    {
        string s = Clean(value, 1000).Replace("\"", "\"\"");
        if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
        return "\"" + s + "\"";
    }

    public static void WriteExports(string directory, IReadOnlyList<SupplementarySource> sources)
    {
        if (sources.Count == 0) return;
        Validate(sources);
        File.WriteAllText(Path.Combine(directory, "supplementary-data.json"), JsonSerializer.Serialize(sources, JsonOptions));
        var csv = new StringBuilder("Source,Type,Device,Category,Interface,Field,Observed value\r\n");
        foreach (var source in sources)
            foreach (var record in source.Observations)
                csv.AppendJoin(',', new[] { source.Name, source.Kind, source.Device, record.Category, record.Interface, record.Field, record.Value }.Select(Csv)).Append("\r\n");
        File.WriteAllText(Path.Combine(directory, "supplementary-data.csv"), csv.ToString(), new UTF8Encoding(true));
    }
}
