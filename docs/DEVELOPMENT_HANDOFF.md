# Cisco and 6200M validation update: 1.15.0

Current source adds CiscoHardware/CiscoParser and the R8Q71A/R8V12A profiles.
Read [Cisco support](CISCO_SUPPORT.md) for allowed-list semantics, full native
C9500 signature matching, 3650 alias handling and the explicit pending 3560-E
converter inventory. Do not infer X2/TwinGig sockets from unused interface stubs.
The private show-inventory evidence confirms C9500-48Y4C; its installed optics
do not change cage geometry. No private evidence is committed or uploaded.

Local standard Roslyn compilation against a public .NET 8 runtime supports
focused generator checks without retrying SDK/NuGet bootstrap. Complete existing
CI plus new Cisco actual-PDF/CSV tests remain publication gates. Test actual
private inputs only locally; never include their reports in CI artifacts.
The six confirmed inputs comprise the two 6100s, one 6200M, one two-member
3650 stack and two 9500s. Locally, all six individual exports and a combined
8-page diagram / 35-page booklet succeeded. All 340 physical interfaces appear
in the combined PDF inventory; an independent source comparison checked the
184 Cisco interfaces, VLAN/native/voice assignments and channel inheritance.
Representative original-input diagrams and combined sheets were visually reviewed. The five 3560-E inputs remain deliberately blocked
pending installed uplink modules. No interactive Windows dialogs are claimed.

# Reversible workspace edits / complete fresh-page blocks — 1.14.6

Removal is immediate with an in-memory undo stack. Exact item instances retain
parsed inventory, per-item device edits, dirty state and previous output metadata.
Undo restores positions/selection and pruned palette overrides; Clear all also
resets/restores workspace naming/title metadata. Newly added items are not silently
lost on Undo. Original files and generated reports are never deleted or modified.
Opening another workspace/closing ends undo history; no extra raw-source cache or
workspace format change is introduced. App-wide preferences/logo/base directory
remain outside this reset. Latest successful output is retained, stale and clearly
labeled as previous when the workspace is empty. Clear/Undo are near the list,
keyboard accessible and disabled while busy or unavailable.

Shared layout first measures complete compact components against a fresh page,
then tries the actual standalone side-key solver before detail continuation.
This lets a tall stack place its LAGs alongside the VLAN key instead of wrongly
forcing all details underneath. Dedicated pages preserve ordering, all member/
port/description data and shared palette colors; ordinary small configurations
still share sheets. The page reports its true standalone geometry scale. A tight
optional title can use the existing top header rather than splitting a fitting
configuration; an overlong title is rejected explicitly, never silently truncated.
Page grouping measures the merged side key as well as switch height: if keys
fit individually but not together, the next whole configuration moves to a new
page rather than repeating both across key continuation pages. Only the existing
genuine overflow path produces detail continuations. The side-key requirement remains: a configuration that only fits standalone with
a bottom VLAN list can still need side-key continuation; no hidden key relocation
or unreadable shrinking is introduced. Side-key pagination, minimum text/socket geometry and validation remain intact.

The supplied private eight-member regression passes standalone, after-small,
reordered and repeated-config tests; its complete 17-LAG key now stays with the
switch on one page. Original inputs/reports remain outside Git/CI. Synthetic
fixtures cover the same side-LAG fallback and genuine oversized descriptions.
Local verification passed 1,997 shared-sheet assertions, 122 workspace/state
assertions, actual PDF/PNG checks and 34 packaging/root/branding tests. The supplied
configuration passed 320 atomic-layout checks including the optional title.
Local compilation uses the standard Roslyn compiler with existing public runtime
references because SDK/NuGet bootstrap is unavailable; hosted normal builds remain
required. Review caught a 6 px overflow only at 600×430 with generated output
and a logo; compact 24 px reset/undo controls preserve the established layout
without relaxing the viewport assertion. Focused local workspace/geometry/PDF checks and all enforced review CI
are publication gates. No interactive native Windows dialogs were exercised.

# Detail-key continuation correction — 1.14.5

## Current focused update

The 1.14.4 shared-key pagination claim was incomplete: `BuildCompact` omitted
only the individual VLAN list, but still required all trunk/LAG memberships and
port descriptions to fit below a complete switch in one A4 block. A tall stack
with many LAGs could export individually yet fail before shared pagination.

Workspace-only fallback now validates the physical block separately, then keeps
full LAG modes/member IDs, outline colors and port descriptions in readable
7.5 pt rows. Overflow continues under a repeated switch-identity header, with
section continuation labels; even one oversized entry splits by wrapped lines.
Normal rows stay together. The side VLAN key and bottom symbol key are rendered
on every shared page, using the existing palette and output scene. Optional title
height is reserved before pagination. Original standalone export behavior remains.
Genuine indivisible physical chassis/port-marker capacity is still enforced;
configuration and generation-stage context improve remaining failure messages.

Regression coverage adds a fully synthetic eight-member/17-LAG stack, complete
port-description text, one exceptionally long entry, title clearance, scene bounds,
actual PDF text and matching PNG page counts. No uploaded customer configuration
or identifying content is included in repository fixtures or CI artifacts. A
compiled public-code-only `workspace-local-checker` artifact supports local
`WorkspaceSheetChecks.dll --workspace INPUT_DIRECTORY OUTPUT_DIRECTORY` validation
without requiring a local SDK bootstrap. It is not a public application asset.
Validation evidence: the compiled application from review commit `11ac508` ran
locally against all seven supplied configurations (not uploaded). Seven individual
packages and seven shared PNG/PDF pages succeeded; the legacy non-paginating
compact path failed on one input. All 29 distinct descriptions, 17 LAG identities
and 23 VLAN IDs were verified complete/consistent in the actual combined PDF.
The tall-stack and continuation/next-switch pages were visually inspected, with
clear side/bottom keys and no clipping. These private inputs/reports stay outside
Git and CI. The local SDK bootstrap cannot create its NuGet mutex, but running
the CI-compiled managed harness directly works normally within the sandbox.

The synthetic page-count assertion was corrected to allow efficient packing
(three sheets, not an arbitrary minimum of four), and its oversized single-entry
fixture enlarged until it actually requires continuation. Application code did
not change. The locally compiled focused checker passes 1,625 assertions plus
actual PDF text/bottom-band/PNG checks; packaging/root/branding passes 34 tests.
Windows native build/update and UI gates passed the initial review. The corrected
final review and main release must pass all enforced CI before publication.
Interactive native Windows dialogs/user update restart remain untested here.

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
