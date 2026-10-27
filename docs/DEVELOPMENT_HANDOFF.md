# Bottom icon key on all diagrams — 1.14.4

## Current focused update

The appearance key is now once at the bottom of every single/combined diagram,
not at the top. `QuickSheet.IconKeyTop` reserves a fixed 7 pt symbol row above the
safety footer; `DiagramBottom` bounds all switch and legend layout before that row.
Standalone geometry moves upward by the former top-key height, retaining the same
usable body height and minimum sizes. Compact workspace components omit a key;
WorkspaceSheet adds one common bottom key per page. Stack/disabled indicators are
preserved. Optional workspace titles, first-switch logos and side VLAN-ID keys
remain. PNG and PDF share one scene. This supersedes the top-key placement below.

Initial 1.14.4 review passed 111 workspace, 70 palette and 523 shared-sheet
assertions plus actual PDF key-band checks and Windows/UI gates. The legacy
complete-VLAN-label test now bounds its last row at `IconKeyTop`, excluding the
separate symbol key without relaxing any label completeness assertion. Final CI
must pass before promotion.

Focused regressions verify single, combined and multi-page key position/count,
physical geometry clearance, footer separation and actual PDF coordinates. All
enforced CI stays enabled; no optional expanded testing sweep is added.

## Previous 1.14.3 update

Combined pages have one top symbol/appearance key, reused from the standalone
renderer, including stack/disabled symbols when applicable. Per-switch copies and
source filenames are removed. Switch hostnames/models/member identities remain.
The shared side VLAN-ID key remains separate. The scene reserves top space before
packing complete switch blocks; the same scene drives PNG and vector PDF.

`ShowWorkspaceTitle` is an optional version-1 workspace boolean, default false.
Name… exposes it with explicit Apply/Cancel. `WorkspaceNaming.DocumentTitle` uses
`Suggested`, not the custom output `Effective` label: saved workspace basename,
otherwise intelligible switch identities. Reopen after file rename and protected
update handoff retain the intended name. Save-as invalidates a titled previous
result even if a custom output label stayed unchanged. Optional titles wrap at
readable size; no generic title is introduced. Logos stay in the first switch's
header. Individual QuickSheet exports retain their existing key and header.

Targeted coverage: one top symbol key/page, filename omission and switch identity,
opt-in title on/off/wrapping, persistence/rename/update handoff, separate output
label and actual PNG/PDF layout. Existing required CI remains intact; avoid extra
manual sweeps. Local packaging/root/branding and JS checks precede review; hosted
runtime and public asset verification remain release gates.

## Focused update

VLAN identity is now numeric ID alone (`vlan:<id>`), superseding the earlier
ID/name rule. Differing names are sorted aliases in one shared side-key entry;
blank names do not split an ID. Combined sheets reserve a right sidebar and retain
switch headers and compact gaps. Long keys wrap and continue across pages rather
than shrinking or dropping entries. Standalone rendering defaults are unchanged.
The same scene drives vector PDF and 300 DPI PNG output.

Consistent legacy name/item-scoped overrides normalize to ID-only keys on load.
Contradictory old overrides for one ID fail explicitly with a request to replace
those keys with one `vlan:<id>` choice; no order-dependent winner is chosen.
Existing applied maps with conflicting colors are likewise rejected. LAG matching,
contrast and connectivity-disclaimer policy is unchanged.

## Latest successful project and intuitive controls

The UI groups Configuration/workspace inputs, Diagram settings and Build & results.
Output-folder/logo preferences are shared; device/model details belong only to the
selected configuration. Build selected and Build all retain explicit scope.
One Open chart/Open output folder pair follows `GeneratedProjectHistory.Current`,
not whichever item happens to be selected. Successful single, combined and
individual-only batch builds replace this result atomically; failed/cancelled
builds or failed requested combination do not. Partial-valid successful combined
projects retain their warnings. Individual-only batches preview their successful
charts in workspace order without offering a nonexistent combined PDF.

Source/device/logo/order/workspace-name changes and opening another workspace mark
the saved result stale but leave existing output accessible. Selection does not
make output stale. Paths are checked before opening; missing output is not silently
recreated or redirected to the base output directory. Project metadata is retained
only in memory and the existing protected update/restart handoff, not in explicitly
saved configuration workspace files. Old handoffs still migrate selected output.

The authoritative `project` bridge event publishes available/chartAvailable/
folderAvailable, combined (paged preview), pageCount, pdfAvailable, label, output,
stale and summary. `openChart` always opens the latest PNG (first available page, or explicit preview
page index), never a PDF fallback. Open PDF is separate for single or combined
projects. Sheets… opens the PNG preview and its Open PNG uses the displayed index.
Existing preview requests read the same latest result. Generation errors/status are separate from the last success
summary, so Copy and opening controls keep a consistent target.

## Names and persistence

`WorkspaceNaming` chooses the custom `OutputLabel` if supplied; otherwise it uses
the actual saved workspace filename minus `.spworkspace.json`/`.json`. Unsaved
workspaces derive a compact name from switch display names/hostnames/models.
Filesystem separators, reserved Windows names, trailing dots and length are handled
by the existing safe naming rules. The **base output directory** is not renamed.
Fresh generated directories are atomically renamed into the selected label;
existing files/directories (case-insensitive names) cause ` (2)`, ` (3)` suffixes,
never silent replacement.

Optional `OutputLabel` and `PaletteOverrides` extend the existing version-1
`.spworkspace.json` document compatibly. Old workspaces need neither field.
`SavedWorkspaceName` is runtime provenance derived from the path on explicit
Save/Open, so externally renaming a file produces the right default when reopened.
The update handoff also preserves this naming context. Save remains explicit,
atomic, and local. Saved workspaces contain original configuration snapshots and
can contain credentials: protect them like original backups; never commit/upload
private workspaces or client reports. The usual extension remains gitignored.

## Shared palette policy

`WorkspacePalette.Create` constructs one semantic palette across the full
renderable workspace, then gives each valid item a complete `RenderPalette` context. Already-blocked inventories do not poison valid-item color planning; their normal validation reports are retained. Native selected
builds send this context to the child generator using `--palette`; batch builds
call the same generator with it directly. Inventory JSON includes optional rendering
overlay maps only when supplied. Combined regeneration therefore retains exactly
the same colors. `workspace-palette.json` records per-item maps and policy warnings.

- VLAN identity: numeric ID alone, regardless of names or blank metadata.
  Distinct trimmed/NFC names are displayed as aliases under one ID and color.
  Matching colors do not establish network connectivity.
- LAG identity: trimmed/NFC label matched case-insensitively. Matching labels share
  a contrast-checked outline across every member fill. Matching numeric suffixes
  with different labels are not guessed to match. **Matching LAG colors do not
  establish a physical link or common port channel between switches.**
- Identity/context sorting—not list order or selection—drives assignment, keeping
  colors stable through reordering, selection, save/open and update restoration.
- Optional explicit saved overrides use canonical identity keys. Valid choices
  are pinned; removing an item prunes only its orphaned keys, while shared keys survive. Unknown identities, duplicate meanings/colors, unsafe reserved-state
  colors or poor LAG contrast are rejected clearly rather than silently changed.

No parser semantics or memberships are changed. Voice diamonds, tagged circles,
native fills, stack flags, LAG outlines, port labels, per-switch LAG/description sections and logos
are retained. Adding/removing configs or editing device/model details invalidates
per-item render caches because it can change the shared palette; the latest successful project is marked stale instead of losing its open links. Old generated files are never deleted by these edits.

## Validation and release procedure

1.14.2 focused checks cover same-ID/different/blank names, alias-key deduplication,
side-key geometry/continuation, legacy override migration/conflicts and PNG/PDF
path selection across single→combined→single results. Local Python and JS checks
run before review. Required hosted CI remains unchanged and must pass on the exact
reviewed commit before non-force main promotion. No repeated broad manual sweep;
inspect representative side-key artifacts and essential public package/feed proofs.

Initial 1.14.1 review `bdc7a2e` passed the full Linux parser/render/report suite
and Windows build/updater checks, including 102 workspace/project-state, 66 palette
and 309 shared-sheet assertions:
https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/actions/runs/40468197574
Downloaded sheet artifacts matched their SHA-256 and were visually reviewed:
header-free mixed-vendor sheets, one shared key, matching fills, conflict/unnamed
labels and 40 long wrapped names. The unified-project UI flows passed; a final
600×430 logo overflow was corrected with compact spacing, retaining and expanding
the no-scroll/control-bounds regression. The exact final commit must pass all
hosted jobs before promotion; no failure is waived.


Historical 1.14.0 review commit `1bbbf55415502c9eba0492d2beded7e222bde05e` passed the complete
hosted workflow on 2026-10-27:
https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/actions/runs/40464057384
New checks passed 83 workspace/state/batch, 58 shared-palette and 189 shared-sheet
assertions, plus actual PDF/raster inspection and all prior required suites.
Downloaded synthetic artifacts were digest-verified and visually inspected: two
configurations share one sheet, the three-item mixed-vendor export shares one
sheet, and five switches paginate 3+2 with complete labels/keys and stable scale.
Normal and compact-window UI screenshots were also inspected. The final reviewed
commit must pass the same full gate before non-force main promotion.


Local Python packaging/branding/publication/root checks and JavaScript syntax run
before publication. Local .NET execution remains blocked by the executor's NuGet
mutex/runtime bootstrap restriction; do not retry sandbox bypasses. Actual .NET
and Windows tests run in hosted CI on an isolated branch before main promotion.

Added/extended checks cover meaningful saved/renamed/custom/unsaved names, reserved
paths and collision isolation; shared/mismatched/unnamed VLANs, LAG context and
reorder/save/load/override stability; real two-switch one-page output, 3–5-switch
capacity pagination, mixed vendors, dense/tall fallback, port/marker preservation,
fonts/bounds/logo, matching PNG/PDF geometry, preview navigation and stale responses.
All existing CI parser/report/hardware/adaptive/inventory/updater checks remain.
Generated synthetic sheet fixtures and UI screenshots are uploaded for visual QA.

Do not claim interactive native Windows dialogs or a real user update/restart were
run here; hosted Windows helper tests and pure restart-migration checks supplement
but do not replace workstation testing. Historical private-client-only fixture
scripts remain unrun unless their inputs are explicitly supplied.

Promote only the exact green reviewed commit by non-force fast-forward after
checking concurrent main changes. Existing main release gates verify all jobs,
package assets and publish a new immutable version. After release, confirm tag/main
commit, latest feed, all downloaded public asset digests, package contents and
Windows executable identity. Do not replace old releases or change access.
