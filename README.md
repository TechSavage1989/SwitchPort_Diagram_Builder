<p align="center">
  <img src="Source/Branding/Switchport_Diagram_Builder_Icon.png" width="112" alt="Switchport Diagram Builder icon">
</p>

<h1 align="center">Switchport Diagram Builder</h1>
<p align="center">Turn supported switch configurations into physical port maps and readable VLAN reports.</p>

<p align="center">
  <a href="https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/latest">Download</a> ·
  <a href="#getting-started">Get started</a> ·
  <a href="#features">Features</a> ·
  <a href="#updates-and-settings">Updates</a> ·
  <a href="#development">Development</a>
</p>

## Features

- **Map physical ports:** generate device layouts with proportional RJ45 and optical connectors, member/chassis/module identity and VLAN keys.
- **Read VLAN membership at a glance:** native VLAN fills, tagged-VLAN circles and voice-VLAN diamonds use configuration-aware colors.
- **Keep the complete inventory:** PDF tables retain full tagged-VLAN lists, with wrapped cells and continuation pages for long rows.
- **Share one sheet:** place switches from different configurations together on readable A4 sheets in workspace order, with shared VLAN/LAG colors and exact-sheet previews.
- **Organize multiple configurations:** add files or pasted snapshots, drag to reorder (or use up/down), and keep each diagram's model/device details independent.
- **Export a report package:** A4 PDF, 300 DPI PNG, CSV, JSON, a technician summary and a validation report.

## Download

Get **`SwitchportDiagramBuilder.exe`** or the versioned **Windows x64 ZIP** from [the latest release](https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/latest). Extract the ZIP before launching.

## Requirements

| Requirement | Details |
| --- | --- |
| Operating system | Windows 10 or 11, 64-bit x64 |
| Runtime | Microsoft Edge WebView2 Runtime; no separate .NET installation for published builds |
| Configuration input | Supported HPE/Aruba configurations and documented MikroTik RouterOS text/binary inputs |
| Coverage | Model, syntax and export-format support are bounded; check the guides below before relying on a diagram |

> **Validate before sharing:** a researched model is not automatically a supported layout. Typing a model name does not bypass physical-layout validation. Unknown hardware, conflicting identity or layouts beyond readable A4 capacity can block export rather than silently omit information.

See [hardware coverage](Source/SUPPORT_COVERAGE.md), [MikroTik support](Source/MIKROTIK_SUPPORT.md), [layout rules](Source/LAYOUT_SIZING.md) and [color selection](Source/COLOR_SELECTION.md).

## Getting started

1. Open [the latest release](https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/latest). Download **`SwitchportDiagramBuilder.exe`** or extract the **Windows x64 ZIP**.
2. Launch the executable from a writable local folder. **Microsoft Edge WebView2 Runtime** must be installed.
3. Choose one or more saved configurations, drop files into the window, or use **Paste new → Add configuration**. Imports add items; duplicate names stay separate.
4. Select a configuration in the workspace. Open **Device details**, leave its model empty for automatic detection, or supply an exact supported SKU when hardware identity is missing. Each item retains its own member/module details.
5. Choose an output folder and select **Build selected** (or **Build all**). After a successful build, use **Open chart** or **Open output folder** for the selected item.

Reports are saved in a child folder named for the switch hostname or identified model. Repeated exports create numbered folders rather than replacing earlier results.

### Ordered workspaces

Drag list items or use their **Move up / Move down** controls. Selection follows the item, not its old position. Remove affects only that item; unsaved changes require confirmation. Unsupported configurations remain visible with validation warnings so you can supply missing model details without blocking the others.

**Save workspace** writes a separate versioned `.spworkspace.json` containing ordered configuration snapshots, selection and per-item details. Existing workspaces remain compatible. Saving is explicit; these files are not uploaded automatically. **Protect workspace files like the original configurations: they can contain credentials and network details.** Opening validates before replacing current items. Reopened workspaces rebuild exports rather than trusting external output paths.

The generated workspace folder defaults to the **saved workspace filename**. Before saving, it derives a compact label from switch names/models. Use **Name…** for an optional custom output label (clear it to return to automatic naming). A renamed workspace uses its new filename when reopened, unless you explicitly set a custom label. The **base output folder** remains separate; repeated exports use `Name (2)`, `Name (3)` rather than overwriting.

**Build all → Shared A4 sheets** is enabled by default. Multiple complete switch diagrams from separate configurations share each sheet in list order, with clear labels and boundaries. Common reference port geometry and readable labels are retained; another A4 page is added only when the next complete switch block cannot fit. Dense chassis/stack configurations can require their own sheet and are explicitly reported. **Preview sheets** shows the actual exported PNG for every page; the PDF uses the same vector scenes. The combined booklet begins with shared sheets, followed by complete per-switch inventory/detail pages. Individual eight-file packages and **Build selected** remain available.

**Shared colors:** matching VLAN ID plus exact trimmed/Unicode-normalized name receives the same color throughout the workspace. Different names for one ID remain distinct and generate warnings. Unnamed or ambiguous VLANs stay item-scoped rather than being silently merged. Matching trimmed, case-insensitive **LAG labels** share an outline color chosen against all member fills; matching labels do **not** imply a physical connection between switches. These mappings survive reorder, selection and save/reopen and apply to individual workspace exports too. Explicit saved palette overrides are retained when valid; unsafe/conflicting overrides are rejected clearly.

Invalid configurations are reported independently, while valid ones still export. Selected **Output / Copy / Open chart / Open output folder** stay separate from batch output. Output directory, report logo and update preferences remain app-wide.

See [workspace format and development handoff](docs/DEVELOPMENT_HANDOFF.md) and [1.14.0 release notes](docs/releases/1.14.0.md).

## How to use

| Output | Purpose |
| --- | --- |
| `wall-chart.png`, `a4-one-page.png` | A4 landscape port maps at 300 DPI |
| `a4-one-page.pdf` | Single-page visual diagram |
| `a4-booklet.pdf` | Diagram followed by detailed port inventory |
| `port-map.csv`, `technician-summary.txt` | Tabular data and a readable summary |
| `inventory.json`, `validation-report.txt` | Complete inventory and diagnostic records for internal review |

The validation report records assumptions and provenance. Review it alongside the diagram; configuration exports do not establish live device state.

## Updates and settings

Open **Updates** for manual checks, release notes, download progress, reminder delay and the automatic-check setting. Automatic checks are enabled by default and run shortly after startup and periodically while the app is open.

**Download and installation are separate actions.** Installation requires explicit confirmation and waits for report generation to finish. The updater verifies package/executable SHA-256 hashes and Windows executable identity. It retains the previous executable and attempts recovery if the new interface does not become ready after restart.

Current builds are not Authenticode signed. Checksums verify integrity through the trusted release repository; they are not a code-signing certificate.

Settings, your report logo and generated reports persist between sessions and future updates. **1.12.1 starts the current application identity:** install it from the release download. Earlier builds are historical and do not update into this identity.

See [UPDATING.md](docs/UPDATING.md) for writable-folder requirements, trust limitations and recovery instructions.

## Development

<details>
<summary>Build, checks and release safeguards</summary>

Read [DEVELOPMENT.md](DEVELOPMENT.md) for the source layout, checks and release safeguards. Editable application, generator, resources and regression checks are in [Source](Source/). Keep private configuration backups and generated client reports out of the repository.

The Windows host uses .NET 8, WinForms and WebView2. The generator and shared updater validation library also build on Linux.

Build with the .NET 8 SDK:

```sh
dotnet build Source/SwitchportDiagramBuilder/SwitchportDiagramBuilder.csproj -c Release
```

Publish a self-contained Windows executable:

```sh
dotnet publish Source/SwitchportDiagramBuilder/SwitchportDiagramBuilder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/windows
```

Package with [scripts/package_release.py](scripts/package_release.py). The [build workflow](.github/workflows/build-and-release.yml) defines the complete regression checks and publishes new versioned releases after a successful main build. Existing published releases are not overwritten; documentation updates must not bump the application version.

Source is available in this repository and GitHub's versioned source archives. Update ZIPs contain application/update files, not user configurations. Native helper tests do not replace interactive Windows/WebView2 and real restart testing.

</details>
