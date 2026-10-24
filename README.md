# HUBTech Network Diagram Builder

Windows desktop application for building physical switch port maps and complete
VLAN inventory reports from supported switch configurations.

Development and updates use this repository:
https://github.com/TechSavage1989/SwitchPort_Diagram_Builder

## Download

Get the current Windows x64 executable or portable package from
[Releases](https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/latest).
Run HUBTechDiagramBuilder.exe. Microsoft Edge WebView2 Runtime is required;
a separate .NET installation is not required for published builds.

Version 1.12.0 introduces the built-in updater. Earlier builds need this first
updater-enabled release downloaded manually. Later releases can be installed
through Updates in the app.

## Updates

Automatic checks are enabled by default. The app checks shortly after startup
and periodically while it remains open. Updates provides manual checking,
download progress, release notes, a reminder delay and an automatic-check setting.

Download and installation are separate actions. Installation requires explicit
confirmation and waits for report generation to finish. The updater verifies
the package and executable SHA-256 hashes and validates the Windows executable
before installation. A previous executable is retained for recovery. The new
interface must become ready after restart; failed startup restores the previous
version. App settings, report logo and generated reports stay in place.

See [UPDATING.md](UPDATING.md) for installation, recovery and release details.

## Diagram and report features

- Proportional RJ45 and optical port geometry with physical device layouts.
- Readable VLAN keys beside or below wide port banks, bounded text sizes and
  consistent tagged-VLAN circles and voice-VLAN diamonds.
- Configuration-aware soft colors selected for perceptual separation.
- Complete tagged VLAN lists in PDF inventory tables, with wrapped cells and
  continuation pages for unusually long rows.
- A4 PDF, 300 DPI PNG, CSV, JSON, network summary and validation report exports.
- Independent switch, member, chassis and module identity fields and models.
- Supported HPE/Aruba platforms and documented MikroTik RouterOS text/binary
  input. Hardware and syntax limits are documented in Source/.

See [hardware coverage](Source/SUPPORT_COVERAGE.md),
[MikroTik support](Source/MIKROTIK_SUPPORT.md),
[layout rules](Source/LAYOUT_SIZING.md) and
[color selection](Source/COLOR_SELECTION.md).

## Development

Editable application, generator, resources and regression checks are in Source/.
The native host uses .NET 8, WinForms and WebView2. The generator and shared
updater validation library also build on Linux.

Build the application with the .NET 8 SDK:

    dotnet build Source/HUBTechDiagramBuilder/HUBTechDiagramBuilder.csproj -c Release

Publish the portable Windows executable:

    dotnet publish Source/HUBTechDiagramBuilder/HUBTechDiagramBuilder.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows

Package the release with scripts/package_release.py. GitHub Actions runs the
regression checks and publishes a new versioned release after a successful main
branch build. Existing releases are not overwritten.

Source code is available directly from this repository and GitHub's source
archives. Update ZIPs contain the application and update documentation; user
configurations and generated reports are kept outside the source repository.

See [DEVELOPMENT.md](DEVELOPMENT.md) for the development and release process.

