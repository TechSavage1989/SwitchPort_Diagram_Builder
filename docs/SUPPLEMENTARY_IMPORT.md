# Supplementary network data import

This feature adds **source-attributed operational observations** alongside an existing switch configuration. Supplementary input does not replace the primary configuration, prove current physical connections or determine VLAN/port geometry.

## Use

1. Import a supported primary configuration and select its workspace item.
2. Click **Supplementary data** in Diagram settings.
3. Choose **Add report or command file** for Auvik Excel reports (.xlsx) or switch/firewall CLI output saved as .txt/.log/.csv. Alternatively use **Paste show output** and paste one or more commands plus their output.
4. Review the source list and categories. Remove an incorrectly attached source before building.
5. Build selected or Build all. The ordinary report package contains **supplementary-data.csv** and **supplementary-data.json** when evidence is present, and a limited source-attributed overview in **technician-summary.txt**.

Attach multiple sources to the same configuration, including an Auvik report and current CLI output. The application does not fetch directly from Auvik, connect to switches, log in to firewalls or run commands. Files stay local.

## Automatic drag and drop (unreleased development feature)

Drop one or more network files anywhere in the main workspace. The same file-drop area now classifies by format and content:

- **Primary configurations:** ordinary text configurations and unencrypted RouterOS backups use the original configuration workflow.
- **Auvik reports:** .xlsx workbooks with valid Excel ZIP structure are recognized as supplementary. Each matching device in the loaded workspace receives only its own report rows. A site-wide workbook may populate several existing configurations. No matching hostname means the workbook is rejected without creating a dummy configuration.
- **CLI output:** recognized show/display/get/diagnose command captures or distinctive operational interface/LLDP fields are supplementary, not new switches. CLI prompts identify a matching hostname; otherwise the explicitly dropped-on or selected device is used. Conflicting or ambiguous host identities are reported.
- **Mixed drops:** switch configs are added first even when supplementary files appear earlier in the drop batch. Errors do not erase valid file imports or existing generated reports.
- **Supplementary dialog:** drop report or operational text directly into the open dialog; a primary configuration is rejected there, not reclassified.

A file with an unrecognized CSV schema is explicitly rejected rather than treated as a configuration. Unencrypted MikroTik backup byte fidelity and ordinary single-configuration text drop behavior are preserved. The app never guesses a device when hostname matching is ambiguous. Auvik workbooks are parsed in memory only, with ZIP/XML input limits and no macro, script or external reference execution.

## Input coverage

| Input | Supported interpretation |
| --- | --- |
| Auvik workbook, Interfaces | Interface admin/operational status, negotiated speed, MAC/type, connected-to and reference details |
| Auvik workbook, Devices / Network Elements | Matching device model, IP addresses, connected-to and software version |
| Auvik workbook, Routes | Destination, next hop, metric, gateway interface |
| Auvik workbook, ARP Entries | IP address, MAC address and interface |
| Auvik workbook, Forwarding Database (FDB) | Destination MAC address, VLAN and interface |
| CLI: show/display/get interfaces, show ip interface brief | Interface operational status, recognized IP addresses and source-labeled reference lines |
| CLI: show/display/get ip route, router routing table | Recognized prefixes, next hops and direct routes plus reference lines |
| CLI: show lldp neighbors detail, show cdp neighbors detail, LLDP/CDP summaries | Local/remote interface and device where recognized, plus source-labeled reference lines |
| CLI: show DHCP, DHCP relay, ip helper-address | Recognized relay IP targets, pool/server/scope/lease reference lines |
| Other saved text/CSV CLI captures | Conservatively imported reference observations when a recognized command heading is present |

Importing is deliberately conservative. Vendor firmware changes column layouts and command formats; an unparsed row is **not** a missing or disabled interface. Only recognized fields are normalized. A saved CSV is read as a text command capture, not as an arbitrary Auvik-exported CSV schema.

## Device attribution and precedence

Manual Auvik imports under Supplementary data use the **selected configuration's hostname**. Automatic drag-and-drop instead checks hostnames across all loaded workspace configurations and attaches only matching rows for each device. Full and short hostnames compare case-insensitively. Unmatched workbooks are rejected, and duplicate hostname snapshots require an explicit target.

CLI text is attributed to the *currently selected* configuration by operator choice; a switch's own CLI output may have a different prompt name, so confirm the correct item before importing.

- Native/untagged VLAN, tagged VLANs, voice VLAN, trunk/LAG, port numbering, physical sockets, module choices, configuration-derived descriptions and administration state come from the primary configuration only.
- When primary inventory fields are still unknown, identified connected device, observed operational link state and observed speed can be filled with **Observed** attribution.
- Observed IP interface details, DHCP/relay data, routing and ARP/MAC information are saved in supplementary exports, and a condensed view is included in the technician summary.
- Conflicting observations from different sources are preserved independently in the sidecars; source order can determine the first value used to fill an otherwise unknown field. Do not treat an inferred relationship as a confirmed physical connection.
- A4 geometry, VLAN legend/markers, booklet continuation and shared-sheet composition are unchanged.

## Security, limits and compatibility

- Imports are local, read only and bounded. The workbook reader uses Open XML ZIP and XML documents without executing formulas, macros, scripts, external links or commands. It prohibits XML DTD processing and remote resolution.
- A source can contain network identifiers and credentials. Obvious secret-bearing lines are redacted, but this is **not a complete secret scrubber**. Do not paste privileged configuration output or share a resulting workspace/report without reviewing it. Keep original files and generated reports protected.
- Maximum 20 supplementary sources per configuration, 5,000 normalized observations per source, 20 MB per text capture and 25 MB per Auvik workbook. The existing workspace size cap of 100 MB remains.
- Only parsed evidence is embedded in the saved workspace. Original workbook bytes, formulas and complete raw CLI transcripts are not retained. Removing a source removes its observations from future builds without deleting its original file.
- The existing version-1 workspace schema remains compatible: older saves with no supplementary section load normally. Unsupported or malformed supplemental data never changes a device's hardware identity.
- Original source files and customer reports are never committed or uploaded to the application's public repository or CI tests.

## Development regression requirements

Run **SupplementaryChecks**, **verify-supplementary-ui.cjs**, **verify-supplementary-drop-ui.cjs**, all legacy CI report/parser/browser/update checks and the Windows packaging/updater tests. Run actual Auvik samples only on a trusted workstation, not in public GitHub CI. Capture real UI screenshots with fictional data, keep any stale README images synchronized, and validate physical socket/legend invariants. Do not claim an untested Windows/WebView2 session or live network discovery.

