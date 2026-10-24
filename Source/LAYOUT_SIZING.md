# Adaptive layout sizing

The main diagram remains one A4 landscape page. Its body uses one geometry
scale, preserving every RJ45 and optical interface aspect ratio. A reference
RJ45 box is 25.2 by 23.2 points with a 17.8-point main face and narrow number
tab. Optical cages retain their 16.2-point reference height. VLAN density never
changes those proportions.

## Readable content before enlargement

Each candidate reserves its VLAN key before fitting the physical bank. Every
column has at least 78 printed points of usable text width, beyond the native
swatch, marker and gutters. The solver prefers enough room for the longest
word up to 140 points and can use the 78-point floor for wide hardware. Long
single-token names may wrap into meaningful fragments. Numeric VLAN IDs and
optional Voice prefixes must fit as complete units. The full VLAN key heading
fits without ellipsis. Wide banks can place the key below in up to five columns.

The solver considers normal spacing, tighter gaps and readable compact text
for side and below-bank layouts before marker reduction. Printed typography
is capped: standard panel titles 7-8.5 points, standard key/body text 7-9 points,
key headings at most 10 points, port numbers 4.5-9 points. Role-specific compact
floors remain. Actual glyph ink is anchored inside heading backdrops. Main
report title, optional client logo, reading guide and disclaimer keep reserved
regions. Interface silhouettes always retain their shared geometry scale.

## Visible and consistent markers

The preferred printed marker radius is the 2.1-point reference radius times
geometry scale, capped at 4.2 points. Sparse diagrams retain visible circles
and voice diamonds when ports enlarge. The maximum 8.4-point diameter fits
the guide band. Key gutters account for the preferred marker diameter.

Packing uses final face polygons and keeps complete halos clear of edges,
port numbers and adjacent markers. If preferred markers do not fit after
readable layout alternatives, a search selects the largest globally fitting
radius down to 1.1 points. All port, guide and key circles/diamonds share that
size. Inputs beyond readable capacity block instead of omitting memberships.

Per-port voice designation remains local. Global voice VLANs keep global
meaning. PDF inventory is independently paginated and preserves complete VLAN
lists even when one row needs multiple continuation pages.

## Verification

AdaptiveLayoutChecks independently verifies dense packing, consistent geometry,
marker counts and containment, font floors, heading ink and complete PDF data.
GeometryChecks covers all 149 enabled hardware profiles, mixed 416-interface
stacks, 17 trunk keys, flexible bays and injected collision/data failures.
LayoutQualityChecks covers readable keys, bounded typography, visible sparse
markers, long names, dense memberships and side/below-bank key placement.
