# Multi-configuration workspace — 1.13.0

## Implementation and compatibility

The Windows/WebView2 host remains in `Source/SwitchportDiagramBuilder`; the existing
parsers and A4 renderer are unchanged. `ConfigurationWorkspace.cs` owns ordered
source snapshots with stable IDs, independent cached inventories and operator
`DeviceIdentity` details (including manual models). `WorkspaceExport.cs` orchestrates
the existing eight-deliverable generator, then creates combined PDFs from the same
validated inventories. Every complete diagram keeps its own A4 page; no cross-item
squeezing or parser/validation bypass is introduced.

Imports are additive, including duplicate files/names/content. Per-file read/size/
empty/binary errors do not discard other valid items. Parsed configurations with
validation blockers stay in the list so missing hardware can be supplied through
Device details. Reordering moves identities, not copies; selected output state
stays attached to the item. Changing one item's details invalidates only its outputs.
Output folder and report logo retain their existing application-wide meaning and
invalidate all cached output links when changed. Update preferences remain global.

## Explicit-save format

No workspace format existed before this change. The separate `.spworkspace.json`
format uses `Format: SwitchportDiagramBuilder.Workspace`, `Version: 1`, `SelectedId`
and ordered `Items`. Each item holds `Id`, `Name`, `SourcePath` (display provenance),
`Text` (snapshot), and `Devices`. Existing configuration, inventory JSON and export
formats are unchanged. Sources are not reread from provenance paths on reopen.
Derived inventories, output/chart paths and runtime status are not loaded from a
workspace file. Only explicit Save writes this file; Save resets dirty state after
atomic replacement. Limits: 20 MB/configuration, 100 items and 100 MB serialized
workspace. Workspace files can contain credentials: do not commit/upload them, and
protect them like original backups. `.gitignore` excludes their usual extension.

The update restart handoff now retains the complete ordered workspace plus runtime
output/status/dirty state. The old single-selection handoff is still accepted for
upgrades. The original updater's protected session-path checks, hashes, extraction,
replacement and health/rollback logic are unchanged. This handoff is the existing
explicit update-install preservation mechanism, not new automatic workspace saving.

## Host/UI behavior

`workspace` events publish ordered metadata and selection. `selected` restores its
hostname, file, chart availability and status. `selectItem`, `moveItem`, `removeItem`,
`saveWorkspace`, `openWorkspace`, `loadFiles` and `runAll` extend the existing bridge.
Legacy `loadText`, `loadBackup`, `run`, copy/open and updater actions remain.
Batch results use a separate summary and folder action; selected Output/Copy and
Open chart/folder never silently switch to the batch root. Native file picker uses
multiselect; browser drop decoding preserves UTF-16 and RouterOS binary handling.
Opening a workspace validates before confirming replacement. Unsaved removal/close
and workspace replacement ask before discarding. Report generation locks mutation.

Batch directories have fresh identities; ordinal hostname/model child directories
preserve list order and avoid duplicate/case-insensitive collisions. Each item has
an independent success/failure result. Optional combined diagrams/booklets contain
successful items only, in the same order, with failures recorded in
`workspace-export.json`. Individual reports remain available if combining fails.

## Checks and release evidence

- Local Python packaging/publication/branding/root-layout checks: 34 passed.
- Local JavaScript syntax checks are run before branch publication.
- New `WorkspaceChecks` and `verify-pdfs.py` cover ordered mixed-vendor/invalid and
  duplicate imports, state isolation, model recovery, save/open validation, batch
  collisions, all eight existing formats, combined page order, A4 and 300 DPI.
- New browser workspace checks cover additive imports, selection/restore, movement,
  deletion, independent editing bridge, batch vs selected output, generation locks,
  normal/small windows and accessible controls. Existing CI UI checks are retained.
- Hosted CI also runs existing parser/report/geometry/adaptive/layout/inventory and
  updater checks, supplemental device/palette/RouterOS/logo regressions, the Windows
  self-contained build, compiled icon/display metadata and real updater helper tests.
- Local .NET execution is unavailable: runtime startup fails creating its NuGet
  mutex; authorized execution escalation also failed at environment bootstrap. No
  sandbox workaround was used. Compilation and .NET execution use hosted CI.
- Interactive native Windows/WebView2 dialogs, a real user update/restart and
  fixture-specific historical private client input scripts cannot be fully exercised
  in this cloud workspace. Do not claim those as tested.

Release procedure: isolated feature branch first; inspect every CI result, download
and verify packaged assets and interface screenshots, then non-force fast-forward
main only if its head is unchanged. Main's existing release gate requires all three
jobs. Confirm the stable tag, exact commit, latest release/feed and downloaded asset
hashes before delivery. Do not replace old tags/assets or change repository access.
