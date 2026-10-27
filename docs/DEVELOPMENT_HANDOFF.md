# Combined legend and unified results — 1.14.1

## Focused update

Combined scenes now start directly with each switch's own header; the generic
workspace title bar is absent. Switch blocks have a 6 pt gap and no per-switch VLAN
key. A complete, wrapped, deduplicated key is reserved once per A4 sheet. Matching
ID/name identities share one entry and exactly the same fill across switches;
conflicting/unnamed meanings remain separately labeled. Page grouping accounts for
both all switch blocks and the full key before accepting another configuration.
Dense single-item fallbacks retain one complete readable key and their established
geometry. The standalone renderer/export path remains unchanged.

The same scene drives the combined PDF and 300 DPI PNG preview. Logos appear once
in the first switch header on each page. Inventory appendices retain their full
per-switch values. Workspace palette planning has a pre-parsed inventory overload
that preserves applied color maps and rejects conflicting supplied maps without
mutating original inventories.

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
stale and summary. `openChart` opens the latest single PNG or emits
`openProjectPreview` for the latest multi-page project; existing preview requests
read that same result. Generation errors/status are separate from the last success
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

- VLAN identity: numeric ID plus exact **trimmed, NFC-normalized, case-sensitive
  name**. Equal pairs share color across configurations.
- Different names for one ID remain separate colors and produce an explicit
  conflict warning. An unnamed or internally ambiguous ID stays scoped to the
  stable configuration ID; it is not silently merged with a named network or
  another unnamed configuration. Colors do not establish network connectivity.
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
