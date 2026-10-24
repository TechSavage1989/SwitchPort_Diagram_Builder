Hardware layout notes
=====================

The diagram is a front-view patching schematic. Slot placement and interface
identities follow the selected hardware profile; spacing grows to accommodate
VLAN markers. It is not a dimensioned mechanical drawing.

5406 zl / 5406R zl2: three rows of paired slots A/B, C/D, E/F.
5412 zl / 5412R zl2: six rows, ending K/L.
8212 zl: six paired rows, with the fabric region between E/F and G/H.
8206 zl: three paired rows, with the fabric region below E/F.
3810M: fixed interface bank, followed by front uplink bay A; JL075A has A/B.
24-port copper 3810M models retain the unused left fascia. The 16-port SFP+
3810M fixed bank and its two uplink bays use a single horizontal port row.

Declared module letters and member prefixes determine placement, regardless
of configuration ordering. A missing module declaration produces an
unreported slot placeholder, not an assertion that a physical slot is empty.
Management, system support and fabric bands indicate structural regions only.
They do not claim that any particular service module is installed.
Unknown chassis capacity is labeled and never inferred from a hostname.
Other models require a verified profile before physical geometry is supported.
Separate stack members are ordered by member ID; this is not physical rack order.

Reference manuals
-----------------
HPE 5400R zl2 Installation and Getting Started Guide, front views, printed
pages 1-5 and 1-6 (PDF pages 15-16):
https://support.hpe.com/hpesc/public/api/document/emr_na-c04655001?docLocale=en_US

HP 8200 zl Installation and Getting Started Guide, publication 5998-2999,
June 2013, front views, printed pages 1-5 and 1-6 (PDF pages 16-17).
HPE 8200 zl product reference:
https://www.hpe.com/psnow/doc/c04111638

HPE Aruba Networking 3810M Installation and Getting Started Guide, part
5998-8461b, front views and flex modules, PDF pages 8-9:
https://www.arubanetworks.com/techdocs/hardware/switches/3810/IGSG/3810_igsg.pdf

HP Switch zl2 Modules Installation Guide, part 5998-5076a:
https://support.hpe.com/hpesc/public/api/document/emr_na-c04344577?docLocale=en_US

Modular VSF interface naming:
https://arubanetworking.hpe.com/techdocs/AOS-S/16.10/MCG/KB/content/kb/int-nam-con.htm

Regression checks cover chassis capacities, independent A/B and C/A relative
position assertions, undeclared bays, member/slot identities, service-region
placement, flex bay positions, port bounds, text collisions, A4 PNG/PDF export,
and rejection of a slot outside the chassis capacity.

Instant On 1930
---------------
JL686A/B has 48 RJ-45 ports and a right-hand 2x2 SFP+ bank numbered 49-52.
The same bank pattern is used for the validated 24-port 1930 models; the
8-port models have a single horizontal RJ-45 and SFP row. Reference:
https://arubanetworking.hpe.com/techdocs/InstantOn_1930_Switch/InstantOn_1930_IGSG.pdf

VLAN egress untagged membership and ingress PVID are separate fields. The
parser preserves PVID in port notes and does not infer egress membership
from it. Reference (VLAN Interface Configuration and Voice VLAN Configuration):
https://arubanetworking.hpe.com/techdocs/hardware/switches/1930/IGSG/1930_MCG.pdf

Instant On 1960
---------------
JL805A has a blank left fascia and a single horizontal row of twelve 10GBASE-T
ports followed by four SFP+ ports. Stack members retain member/port numbering.
The supplied backup declares four units with network te/uplink none, matching
the validated JL805A profile. It does not identify physical stacking links.
Other 1960 SKUs and ambiguous/mixed member signatures require separate profiles.
Hardware reference, Figure 1 (PDF page 6):
https://arubanetworking.hpe.com/techdocs/InstantOn_1960_Switch/igsg_1960.pdf

TRK membership is supplied by channel-group definitions. Effective TRK VLAN
settings replace per-port shadow VLAN settings. Configured AUTO/ON mode text
is retained without inferring a live protocol state. Reference, Trunk Configuration:
https://arubanetworking.hpe.com/techdocs/InstantOn_1960_Switch/InstantOn-1960-MCG.pdf


Legacy XL and additional CX profiles (1.10.0)
------------------------------------------
5304xl J4850A has paired A/B and C/D slots; 5308xl J4819A continues E/F, G/H.
Numeric module declarations on an identified chassis map 1 to A, 2 to B, etc.
The 8206 J9477A chassis identity is recognized alongside J9475A.
J4820A uses six-port row blocks: top 1-6,13-18; bottom 7-12,19-24.
J4878A uses one row of four mini-GBIC sockets.
J4907A uses top 1-7 and bottom 8-14, then bottom-row 15T/15M and 16T/16M.
Both physical alternatives are drawn, but inventory/CSV retain 16 interfaces.
Only one socket per combo interface may be used. Active medium is unconfirmed.

HP ProCurve 5300xl Installation and Getting Started Guide, 5991-4750,
PDF pages 9-12 (chassis and slot illustrations):
https://ftp.hp.com/pub/networking/software/5300xl-Install-May2006-59914750.pdf
HP ProCurve Switch XL Modules Installation Guide, 5990-3019, PDF page 7
(J4820A numbering and J4878A single-row positions):
https://ftp.hp.com/pub/networking/software/59903019.pdf
Updated XL Module guide, 5990-6076, PDF page 8 (J4907A physical sockets):
https://ftp.hp.com/pub/networking/software/59906076.pdf

3800 J9573A retains the large blank fascia before the 24 copper ports and
right-hand vertical 25/26 SFP+ cages. Corroborated legacy J9573x spelling is
normalized only when the Configuration Editor header explicitly says J9573A.
HP E3800 guide, 5998-0566, printed 1-2 / PDF page 10 (HP-authored mirror):
https://server-shop.ua/assets/images/resources/11027/hp-3800-installation-and-getting-started-guide.pdf
HP official 3800 specifications (J9573A port count):
https://www.hpe.com/psnow/downloadDoc/c04111485.pdf?id=c04111485

CX 6200F JL727B has right-hand SFP+ 49/51 above 50/52, beside 48 RJ-45.
Front view and model table, PDF page 9:
https://arubanetworking.hpe.com/techdocs/hardware/switches/6200/IGSG/Aruba_6200_IGSG.pdf
The PL shared 48G profile has a left-hand 2x2 optical bank, 49/51 above
50/52, followed by two rows of copper sockets, odd above even. Both 6000 and
6100 48G variants share these socket positions. The firmware does not identify
which family or PoE variant is installed. Client headings therefore omit the
model/family name. Exact model, PoE and SFP versus SFP+ uncertainty is retained
in internal diagnostics.
No hostname or connected-device description is used to establish identity.
The shared profile requires PL.10 firmware and exactly the complete single-switch
interface inventory 1/1/1 through 1/1/52. It does not override explicit product
identity, member/stack declarations, VSX context, or an incompatible inventory.
Partial, duplicate, additional or range-only physical declarations are rejected.

Official 6000/6100 front views and model tables, PDF pages 7-10:
https://arubanetworking.hpe.com/techdocs/hardware/switches/6100/IGSG/igsg_6000-6100.pdf
Official release notes for the supplied PL.10.10.1070 firmware, supported
6000/6100 families and PL image example, PDF pages 3-5:
https://arubanetworking.hpe.com/techdocs/AOS-CX/10.10/RN/rn_6000-6100_10-10-1070.pdf

Cross-check: J9537A remains 24 SFP, odd above even. The v2 module guide
5998-5076a has an inconsistent illustration alongside its J9537A description
(the illustration shows the mixed J9637A). The written 24-SFP specification
and the official 5400zl QuickSpecs c04111646 establish the cage count/type;
the mixed illustration is not used to change this profile.
https://support.hpe.com/hpesc/public/api/document/c04111646


Visual working page (1.10.5)
--------------------------
All socket symbols use fixed printed dimensions across diagrams: 25.2 pt
wide, 23.2 pt high for RJ45 and 16.2 pt for SFP/SFP+. Horizontal
socket columns have 1 pt gaps; the 24.2 pt physical row pitch includes the
external optical number row. Optical cages are wide, shallow rectangles with small
0.7 pt corner bevels. RJ45 tabs remain narrow at 42 percent of the body width
and 5.4 pt deep. Numbers use fixed 4.5 pt bold text inside RJ45 tabs and
below optical cages, aligned with the RJ45 tab number line on each row.
One/two-row diagrams use 57.2 pt active panels, with 0.5 pt gaps between
panel rows. Verified three-row diagrams use a common 81.4 pt active height.
Single-row banks are centered within that common height. Title, legend and
description spacing is compact; undeclared slot rows retain small placeholders.

Local numbers follow faceplate numbering; panel headings retain slot/member
context. Full interface IDs remain in keys and inventories. Combo RJ45/SFP
pairs share a local number. Shared-interface details are retained in diagnostics.
Every port has a clipped 0.8 pt dark edge, giving a visible 0.4 pt inside line.
The 2.4 pt LAG stroke follows the entire silhouette, including the RJ45 tab
and optical bottom edge. A 0.3 pt outward offset preserves the native face
and number clearance. There is no white separator; the remaining minimum
gap between adjacent aggregate frames is 0.4 pt.

Pastel VLAN colors are assigned without near duplicates. LAG colors are
selected against actual fills and the full VLAN palette, including hue
separation preference. Color distances guard both member contrast and
aggregate separation. A capacity error prevents indistinguishable palettes.
All circle and diamond cores retain a fixed 3 pt colored extent. Symbol keys
use identical markers. Number and border areas are reserved independently.

The side VLAN key preserves names and IDs. The top reading guide pairs real
symbols with short labels. Headers retain clearance from all port geometry.
Actual slot positions, bank sides, port order and flexible bays are preserved.
Unconfirmed model/family names are omitted from client headings. A manual
model name changes the printed header without changing socket placement.
Panel/legend widths can adjust to wide hardware while socket dimensions stay
fixed. Unusually dense keys or inventories fail rather than shrink the map.

Catalogue expansion (1.10.0)
---------------------------
HardwareCatalog.cs loads the embedded Data/hardware-catalog.json. Each enabled
entry carries explicit socket number, column, row, media, optional Comware
interface kind and optional T/M shared-interface suffix. Source and layout
references remain with the profile. Research-pending identities do not enable
geometry. HARDWARE_CATALOG.csv and SUPPORT_COVERAGE.md expose the precise scope.
The existing legacy model layout paths remain unchanged.

Custom bank positions retain their physical rows. All active panels within
one diagram share the height needed by the largest bank. Existing one/two-row
profiles stay at 57.2 pt; the verified three-row 8320 uplink bank uses 81.4 pt.
Socket dimensions, labels, marker size and gaps stay fixed across diagrams.
SFP/SFP+/SFP56/QSFP capability is labelled in profile/port notes; installed
transceivers and live speeds remain unknown. The optical symbol is a schematic
cage and is not a dimensioned mechanical drawing of every optical form factor.

The Trunks/LAGs key and top guide use plain colored rectangle outlines. Key
strokes are clipped within each rectangle. Aggregate borders on physical ports
retain the complete RJ45/optical silhouette.


Additional 2920 and 5400R support (1.10.7)
-----------------------------------------
Added J9726A/J9727A 24-port and J9728A/J9729A/J9836A 48-port 2920 bases,
including four paired copper/SFP combo interfaces. Each pair shares one
logical interface and its VLANs, aggregate membership and administrative state.
Explicit stack members are retained, including members without descriptions.
2920 stack capacity is four members. Backplane links are not invented.
Referenced rear A/B bays contain two ports. Without a module SKU, they use
neutral rectangles, blank product identity and Unspecified media. No SFP+ or
RJ45 card is guessed, and unreferenced rear bays are not invented. Explicit
J9731A or J9732A module identity establishes SFP+ or copper respectively.
For declared stacks, operator selections can supply 1/B=J9731A, for example.
Rear panels are separate from the front panels and retain equal active height.
Hardware source: https://support.hpe.com/hpesc/public/api/document/c03898362

Added the J9995A 5400R v3 zl2 module with eight RJ45 ports in one horizontal
row. A configured speed remains separate from port hardware capability.
Hardware source: https://support.hpe.com/hpesc/public/api/document/c04654886

Terminal-wrapped VLAN port lists now preserve a slash or range continuation
when an interface identifier is split between lines. Source hostname and SKU
remain authoritative over the uploaded filename. These changes add hardware
profiles and syntax coverage; they do not claim universal HPE support.


Application icon and report logos (1.10.10)
------------------------------------------
The flat albedo icon is an editable SVG with named vector groups and solid
navy, cyan, mint, amber, slate, magenta and off-white colors. The supplied
updated SVG is preserved unchanged as the editable master and app-header
image; the PNG and Windows ICO frames are regenerated from it. Branding/ also contains its
1024px PNG, multi-resolution ICO and color palette. The Windows icon is
embedded through ApplicationIcon during compilation, with the same image
used for the window/taskbar and the app header. No compiled EXE is modified
after the build. Edit the SVG in Inkscape, Illustrator or another SVG editor;
regenerate PNG/ICO exports when changing the artwork, then rebuild the app.

Select Report logo (optional) to choose a PNG, JPG or WebP image. The app
shows a preview and remembers a normalized copy for later sessions. Remove
clears it. Logo changes require a new report build. Logos appear in the
header of both PNG charts and every PDF booklet page, on a small white
backplate. Proportions and transparency are preserved. Port, VLAN and A4
geometry do not change. Text exports and inventory data remain text-only.
The original image is not modified. Input limit: 10 MB and 32 megapixels;
logos are normalized to a maximum dimension of 1024px for consistent output.
CLI: --logo path/to/logo.png. PNG/JPG/WebP are accepted; SVG source artwork
can be exported to PNG first. Native Windows taskbar and dialog behavior
still needs Windows testing; build/resource and browser checks ran on Linux.

Eight-member wrapped VSF validation (1.10.9)
------------------------------------------
Trunk-name fragments in wrapped AOS-Switch VLAN lists are rejoined without
changing port ranges or VLAN memberships. A supplied mixed JL558A/JL256A
eight-member stack exports 416 interfaces, 16 explicit VSF links and 17 LACP
trunks on one A4 page. Panel headings use seven-point text and eight-point
header bands. Port and marker dimensions are retained. The expanded outline
candidate fallback and maximin color selection retain the existing separation
guards. All 149 hardware profile layouts and geometry checks pass.

Per-device identification (1.10.13)
---------------------------------
Optional names, serial numbers, asset tags and other details are keyed by
Group.Device, so slot/flexible/rear modules retain their parent identity.
Fixed switches have one entry; VSF/IRF members have separate entries. The
validated model and member IDs are read-only. Diagram headings show the
name (or asset tag if no name is supplied) and serial number. Full fields
are retained on optional booklet identity pages and in JSON, CSV and summary
exports. Identification does not change physical port placements or marker
sizes. Long headings may be compacted; full values remain in the booklet.


Independent module identification (1.10.14)
-----------------------------------------
Every Group.Device retains a separate switch/member or chassis entry. Every
nonempty Group.Slot adds a module entry, identified by Device/Slot, with its
own Name, SerialNumber, AssetTag and Notes. Kind, ParentId and GroupId are
validated from inventory and cannot be replaced through entered metadata.
Module cards follow their parent; matching slot letters in different chassis
have distinct IDs. Flexible and rear modules are included independently.
This supersedes the shared module identity behavior in 1.10.13.

Parent models describe the chassis or base switch; module models describe
that module. CSV preserves existing parent identity columns and appends
separate module columns per port. JSON schema 1.3 retains both identities and
the hierarchy. The booklet and summary include each edited entry separately.
Module diagram headings prioritize their own name/serial, with the chassis
identity in its chart/member heading. Parent-only panel annotations are marked
Parent. Long text may shorten in compact headings; booklet/data values remain
complete. No port, socket, marker, panel height or A4 dimensions are changed.


Report naming and empty module headings (1.10.15)
------------------------------------------------
GroupLabel resolves metadata only from the group itself: the module ID for a
slot group, or the parent device ID for a base-switch group. Empty module
fields never fall back to parent metadata. This supersedes the Parent
annotation behavior described for 1.10.14.

DisplayName resolves entered switch/chassis names in inventory order, using
the configured hostname only when none are supplied. Module-only names and
serial/asset fields do not override the report name. This display name is
used in the main diagram upper-left heading and booklet page headers without
changing Inventory.Hostname or output-folder naming. Model, serial and asset
identifiers remain in their appropriate device context. Geometry is unchanged.


Per-device model selection (1.10.16)
----------------------------------
Model controls are part of switch/member/chassis cards in Device details.
DeviceModels resolves ModelSelection for each authoritative parent ID. Exact
supported switch SKUs pass through existing parser hardware validation. Typed
names are per-device report labels and retain DetectedModel and physical SKU.
Unidentified source members remain editable without inventing ports. Chassis
SKUs use verified chassis profiles and validate slot capacity/family. Mixed
unidentified chassis layouts cannot share an unverified geometry.

Selected/typed values populate Devices.Model in schema 1.4 while preserving
DetectedModel and ModelSelection. Base panel titles use their own parent model;
module panel models remain module SKUs. CSV appends separate Device Model and
Module Model columns. Complete model text wraps on booklet identity cards.
The generator and host use the same resolution path; legacy CLI selections
remain available. Save/Cancel/reset and per-configuration lifecycle apply to
models and identity fields together. No fixed drawing dimensions change.

Version 1.11.0 supersedes fixed 3 pt marker geometry with a 4.2 pt minimum,
spaced packing and taller dense faces. Uniform diagram-body enlargement uses
available A4 space. Hardware slot/bank ordering remains unchanged. The four
MikroTik profiles and source references are documented in MIKROTIK_SUPPORT.md.
