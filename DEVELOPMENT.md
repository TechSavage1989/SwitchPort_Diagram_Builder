# Development and release handoff

Canonical repository:
https://github.com/TechSavage1989/SwitchPort_Diagram_Builder

## Source layout

- Source/SwitchportDiagramBuilder: Windows host, embedded WebView2 interface,
  compiled icon and native update helper.
- Source/SwitchportGenerator: switch parsing, verified hardware layouts, adaptive
  chart generation, PDF reports and data exports.
- Source/SwitchportUpdater: shared feed, manifest, package and installation
  validation.
- Source/tests: generator, layout, updater and report regression checks.
- Source/Branding and Source/Licenses: editable icon resources and dependency
  notices.
- scripts: verified release packaging.
- .github/workflows: CI, Windows publication and GitHub Releases.

## Working on changes

Use a development branch for changes and open a pull request when review is
useful. Keep port-layout and report data checks when changing diagram geometry.
Preserve complete PDF VLAN values and independent device/module details.
Client configuration backups and generated reports are not committed.

Run the checks appropriate to the change. The GitHub workflow records the
complete required set. LayoutQualityChecks can use an optional original backup
path for the supplied MikroTik case; the normal CI fixtures do not require
private client input.

## Version and release rules

Use a new stable three-part version for a published update. The native app and
generator versions must match. UI and native-runtime cache directories derive
their version from the application assembly automatically.

Keep SwitchportDiagramBuilder.exe as the public stable download name.
The display name is exactly Switchport Diagram Builder. Version 1.12.1 starts
a clean application, storage and update identity; earlier releases remain
unchanged historical artifacts. The updater
respects the current installed path. Published portable ZIPs are immutable.
Package and executable hashes are calculated after all streams close, the ZIP
is verified, and only then are final filenames exposed to release upload.

The feed and manifest repository are fixed to this repository. Do not add
tokens, credentials, arbitrary executable URLs or downloaded update scripts.
If signing is introduced later, document and verify the publisher identity
without weakening the existing hash, archive and startup checks.

## Validation limits

CI exercises the shared updater validation, file replacement/recovery logic and
native helper test modes. Interactive WebView2 startup, Windows file locks and
real update/restart behavior should also be checked on a Windows workstation.
Do not describe a process spawn as a successful application startup.

