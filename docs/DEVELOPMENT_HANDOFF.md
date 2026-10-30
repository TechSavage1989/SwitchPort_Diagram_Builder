# Physical port-position correction: 1.18.0

Read [the reference audit](PHYSICAL_POSITION_AUDIT.md) for the scope, primary
manufacturer drawings and diagram conventions. Hardware columns are fractional
port pitches. QuickSheet now consumes those coordinates directly, preserving
empty rows and leading fascia. It may condense short inter-bank gaps in the
most compact layout, but does not change socket proportions or numbering.
Installed Cisco modules are composed into their member face; software
alternatives remain separately qualified. Rear 2920 bays stay in a rear view.

PositionChecks in the existing CI workflow sweeps all inspector choices, all
75 module PIDs and ten parser-only 1930 SKUs, checks final rendered positions,
and exports representative reports plus position-audit.csv. The complete prior
CI remains required. Private input validation is local only. Windows interactive
WebView2 and real user update/restart have not been manually exercised here.

Local validation passed 409 finished diagrams and 1,012,014 physical-position
assertions, 120,439 adaptive layout assertions, 6,632 layout-quality assertions,
all existing geometry checks, Cisco/module/catalog PDF inspections, and 34
repository/packaging tests. The supplied 3650 stack retained all 104 port states
after generation; its PNG was visually reviewed. These local results supplement,
and do not replace, the complete hosted CI gate.

Release assets must be downloaded and verified against update-manifest.json,
SHA256SUMS.txt and the release receipt after publication. Promote only the exact
feature commit that passed every CI job, using the current main SHA as a lease.

# Catalog and 3650 inspector refresh: 1.17.0

Read [catalog sources and limits](CATALOG_REFRESH.md) and the runtime-derived
[complete snapshot](../Source/ALL_MODELS_MODULES.csv) before extending hardware.
There are 279 selectable switch/chassis models, 75 module PIDs and 97 pending
identities. This release adds 26 switch models and five C9300X modules; all 188
current official AOS-CX port-map IDs already have database records. The embedded
HPE JSON now contains 193 supported and 97 pending identities.

The standard-port 3650 table now includes all 15 documented base variants.
WS-C3650-48FD-L/S/E normalizes to 48FD. Only the explicit same-layout PoE bundle
pairs may refine provisioning-only identity. Exact inventory or license UDI
still wins, conflicting exact evidence blocks, and each stack member remains
independent. Fixed 3650 uplinks never get removable module selectors. Six
multigigabit 3650 variants and C9300X-48HXN remain outside enabled coverage.

C9300X module compatibility is separate from ordinary C9300. The 4C module is
limited to 24Y, 48HX and 48TX, with two columns and two rows. Module application
now orders retained and newly introduced sockets consistently even when a sparse
backup contains only the last module port. No interface settings are merged.
Optical group labels derive from actual connector types. Matching MikroTik
rack/desktop binary signatures use shared geometry without inventing an exact PID.

Local final verification passed 590 catalog/inspector assertions, 457 Cisco
assertions and 1,978 module assertions. Actual PDF/PNG/CSV verification passed
all four new-family fixtures and the existing Cisco/module fixtures, including
complete 40-ID VLAN lists. The supplied private stack exports all 104 interfaces
both automatically and with independent FD selections; every port setting is
unchanged and all tagged lists appear in both booklets. Private inputs/reports
remain outside Git. Representative charts were visually inspected.

CatalogChecks exercises every selectable model and every module's compatible
parent, verifies the committed snapshot against runtime data, tests identity
boundaries, save/open and Automatic restoration, and produces actual reports.
Use --refresh only after reviewing catalog changes. Browser regression also
exercises independent FD selections, suffix entry, notes and fixed-uplink controls.

All 34 packaging/root/branding tests and JavaScript syntax checks pass. Full
hosted browser/Linux/Windows checks are required on the exact integrated commit
before non-force main promotion. Release assets must pass the existing immutable
publication and downloaded hash checks. No interactive Windows/WebView2 session
or real user's update/restart is claimed tested here.

# Installed module selection: 1.16.0

Selected device details provides compatible per-slot module dropdowns for each
supported parent switch, stack member or chassis. Read [module support](MODULE_SUPPORT.md)
and [Cisco module references](CISCO_MODULE_REFERENCES.md) before extending the
catalogue. Manufacturer sources establish card compatibility, numbering and socket
geometry; a product-name match alone is not enough.

`DeviceIdentity.ModuleSelections` is an optional list of `{Slot,Sku}` values on
the parent device, limited to 12 slots per device. Old workspaces need no new field.
Empty selections use automatic configuration evidence. Choices
belong to their exact parent and slot, remain independent across stack members,
and survive explicit workspace saves and protected update restoration. Existing
module rows retain independent names, serials, asset tags and other details.
Saved module details remain when reverting to automatic removes a selected card,
provided its parent/slot still exists; those rows explicitly show hardware as
unconfirmed. Module selection must affect the parser's hardware inventory, not
just its label.

`DeviceModules.Slots(inv,device)` exposes slot labels, detected/selected SKUs and
compatible options with source references. The host includes these in the device
inspection bridge and previews changed model/module drafts without saving them.
Preview normalizes newly introduced module detail rows while retaining current
field values and focus. Save waits for the latest preview; stale replies cannot
replace a newer draft. Cancel leaves the saved workspace unchanged, and orphaned
selections remain visible for review instead of being dropped.
Model changes must refresh compatible module options. The backend validates all
operator choices again before replacing the selected workspace item's inventory.
Slot keys are opaque bridge values: HPE letters, Cisco `X2-1`/`X2-2` and `NM`.
2920 rear slots are A/B; 3810M has A, with B additionally available on JL075A.
Do not place power supplies, fans or generic pluggable optics into network-card
slot choices or use them to infer physical port counts.

Unknown Cisco uplink modes retain qualified configured-interface entries rather
than inventing sockets. Known selected modules do not retain an inaccurate
unconfirmed-module label; only saved software alternatives stay qualified. When a known module establishes physical interfaces,
independent settings on unused software alternatives must remain available and
clearly distinguished. Never merge independent VLAN, voice, LAG, description or
administrative state just because interfaces share an uplink bay. Complete PDF/
CSV values and physical/configured counts remain required.

HPE/Aruba coverage includes 41 module PIDs, 16 newly added: J4820B, J4821A,
J4821B, J4878B, J4852A, J8161A, J8707A, J8708A, J9546A, J9547A, J9637A,
J9988A, J9989A, J9991A, J9992A and J9996A. Family choice counts overlap because
some cards support more than one chassis family; use the explicit compatibility
metadata rather than adding those counts. Fourteen additional chassis/bundle
aliases establish chassis capacity without claiming installed card inventory.
Existing detected declarations keep
their prior parser semantics; compatibility checks constrain new operator choices.

Cisco coverage includes 29 module PIDs, four of which are blank inserts, and
46 added base profiles: 16 Catalyst 3560-X/3750-X, 11 Catalyst 3850, seven
Catalyst 9200 and 12 Catalyst 9300. 3560-E adds TwinGig and OneX converter
choices plus researched X2 transceiver inventory. Existing 3650 and 9500 fixed
uplinks remain unchanged. PXG multigigabit/9300X profiles and restricted modules
without verified interface numbering remain outside selectable coverage. Native
QSFP mode does not establish breakout inventory. See the Cisco reference for
exact per-model restrictions; never offer every family module indiscriminately.

New module regression coverage supplements all existing parser, report, layout,
workspace, browser, updater and Windows package checks. Do not relax existing gates.
Final integrated local verification passed 1,858 ModuleChecks assertions,
358 existing CiscoChecks assertions, all 34 packaging/root/branding tests and
JavaScript syntax checks. The module sweep exercised all 41 HPE cards, all 46
added Cisco base profiles and all 29 Cisco module PIDs, including nine 3560-E
choices in both bays and 20 other network-module PIDs. Actual PDF/PNG/CSV checks
passed for mixed TwinGig/X2, 5400R, dense 9300 SFP28 and 3850 blank-module exports.
Exact 40-ID tagged lists were complete in CSV and actual PDF text. Checks cover
physical/qualified counts, independent state, known-module headings, generic
alternative labels, compatible preview normalization, save/open, update/rollback,
Undo, old workspaces without module fields and invalid inventory boundaries.

The complete hosted browser, Linux and Windows review remains required on the
exact integrated commit before non-force main promotion. No interactive native
Windows/WebView2 dialogs or real user update/restart are claimed exercised here.

# Qualified 3560-E configuration views: 1.15.1

This update supersedes the blanket 3560-E block described in the 1.15.0 section
below. Read [Cisco support](CISCO_SUPPORT.md) for the precise scope. A complete
IOS 15 / FastEthernet0 / Gi0/1-52 / Te0/1-2 signature can establish a shared
48-port 3560-E base layout. Exact WS-C3560E-48PD-SF selection is also recognized.
Neither path establishes installed X2/TwinGig modules or the active uplink mode.

The qualified diagram retains 48 base RJ-45 ports and a separate group of six
independent configured uplink alternatives. The upper bay relates Gi49/Gi50 to
Te1; the lower relates Gi51/Gi52 to Te2. Neutral software entries and a visible
module-uncertainty label prevent the view from claiming six physical uplinks.
Do not reuse the 3650 alias merge: the two Gi interfaces in each bay are independent
and may retain different VLANs, voice settings, LAGs, descriptions and admin state.

Group.ConfiguredInterfaces and InventoryBasis separate configured alternatives
from confirmed physical-base counts. Keep that distinction in UI, diagram headers,
PDF inventories, CSV, JSON, summaries and validation. Never advertise the 54
configured entries as 54 confirmed physical sockets. Automatic family inference
requires the complete signature; conflicting explicit identity and invalid
syntax/ranges remain genuine blockers.

Focused local checks passed 132 C# assertions and the synthetic PDF/PNG/CSV/JSON
verifier. Each of the five supplied original backups produced eight deliverables
without blockers: 48 confirmed base ports plus six configured uplink alternatives.
Together, those individual reports retain 270 interface entries, comprising 240
confirmed base ports and 30 alternatives. Booklets span four to eight pages;
complete interface IDs, tagged VLAN/policy text and qualification were checked in
every PDF. Representative original-input charts were visually reviewed without
ellipsis or clipping. All five also exported together successfully on three
shared A4 diagram sheets with 22 combined booklet pages; qualification remains
visible on the shared sheets.

Local packaging/root/branding passed all 34 tests, and JavaScript syntax checks
passed. The local headless-browser bootstrap could not download its browser, so
no local UI run is claimed. Required hosted browser, Linux and Windows checks,
native updater checks and compiled icon checks remain publication gates. No
interactive Windows UI or real user update/restart is claimed here. Private
backups, model screenshots, inventory output and generated reports remain outside
Git and CI.

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
