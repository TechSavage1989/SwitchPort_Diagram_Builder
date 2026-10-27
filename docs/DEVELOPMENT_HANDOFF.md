# Shared multi-switch workspace sheets — 1.14.0

## Corrected intent

Version 1.14.0 corrects the 1.13.0 interpretation: switches from different
configurations must share a report sheet, not automatically receive separate
pages. Selected/individual eight-file exports remain available. Existing vendor
parsers and standalone layout rules are retained.

`QuickSheet.BuildCompact` reuses the same hardware/socket/key renderer at readable
reference geometry, removing only unused bottom space and duplicate footer.
`WorkspaceSheet.Build` stacks complete labeled switch sections in workspace order,
using a common physical scale and marker size. Another A4 page starts only when the
next complete section cannot fit. Dense/chassis/stack configurations that need the
original standalone solver retain their own page with an explicit warning; none are
silently shrunk below readability or omitted. Shared page plans record placements,
scale/fallback decisions and warnings in `workspace-layout.json`.

The combined diagram PDF and every `workspace-sheet-NNN.png` use the same scenes.
The preview modal reads those actual PNGs, not a separate approximation. The
combined booklet starts with shared sheets and then retains every item's original
inventory/device/module appendix pages without duplicating standalone diagrams.
Per-item PNGs, PDFs, CSV, JSON, summary and validation reports remain intact.

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
native fills, stack flags, LAG outlines, port labels, per-switch legends and logos
are retained. Adding/removing configs or editing device/model details invalidates
cached render links because it can change the shared palette; reordering only
invalidates combined output. Old generated files are never deleted by these edits.

## Bridge and UI

Existing workspace selection/order/import/delete/Save/Open and updater actions
remain. New `setWorkspaceLabel` applies a custom label (empty means automatic).
`workspace` publishes `outputLabel`, `suggestedLabel`, `effectiveLabel`,
`workspaceName`, `combinedAvailable` and `pageCount`. Shared sheets are enabled by
default. Batch status keeps policy/capacity warnings separate from selected output.
`previewCombined {index,requestId}` returns the actual exported image plus page
metadata; request IDs reject stale modal responses. `openCombined` opens the vector
PDF. Label/preview modals use existing update-editing deferral. Small-window layout
and selected-item scroll visibility remain regression-tested.

## Validation and release procedure

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
