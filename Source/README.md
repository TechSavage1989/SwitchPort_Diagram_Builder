Switchport Diagram Builder 1.15.1 - Windows 10/11 x64

1.15.1: Qualified Catalyst 3560-E configuration views
- Complete 48-port 3560-E interface signatures can export without guessing X2/TwinGig modules.
- Draw 48 base RJ-45 ports separately from six configured uplink alternatives.
- Retain each alternative's VLANs, voice, LAG, description and administrative settings.
- Reports, JSON, CSV and validation distinguish physical base ports from software alternatives.
- Module uncertainty remains visible; conflicting or insufficient identity still blocks.
- See ../docs/CISCO_SUPPORT.md for the exact qualified-view scope.

Historical release notes
========================

1.15.0: Cisco support and Aruba 6200M inventory
- R8Q71A/R8V12A verified copper/Smart Rate/SFP+ layouts.
- Dedicated IOS/IOS XE parser with 3650 StackWise and C9500 Y4C profiles.
- Full all/except policies in PDF/CSV; no duplicate alternate uplink cages.
- See ../docs/CISCO_SUPPORT.md for precise inventory and 3560-E module limits.

Switchport Diagram Builder 1.14.6 - Windows 10/11 x64

1.14.6: Reversible workspace reset and complete switch blocks
- Remove configurations without repetitive prompts; Undo restores edits/order.
- Clear all resets workspace state, with Undo and no file/report deletion.
- Standalone-fitting switches keep their LAGs/descriptions on one fresh page
  when compact shared layout cannot fit them. Genuine oversize still continues.

1.14.5: Readable trunk and port-description continuation sheets
- Tall stacks no longer fail shared generation just because their LAG/member or
  port-description key exceeds one page. Complete details continue onto extra
  sheets, preserving side VLAN keys, bottom icons and switch identity.
- Physical geometry capacity remains validated; failure messages identify context.

1.14.4: Bottom icon key on every diagram page
- Native/tagged/voice/LAG appearance symbols now appear once at the bottom of
  individual and combined diagrams, with applicable stack/disabled symbols.
- The reserved key band stays above the safety footer and clear of all switches.
- Side VLAN keys, optional workspace titles, logos and PNG/PDF choices remain.


1.14.3: Shared top symbol key and optional workspace title
- Combined sheets show one native/tagged/voice/LAG appearance key at the top.
- Switch headers retain switch identity/model, not source configuration filenames.
- Name… offers an opt-in workspace-name document title. It is off by default,
  persists in the workspace and is separate from a custom output folder label.
- Side VLAN-ID key, PNG/PDF opening and individual-switch exports are unchanged.


1.14.2: VLAN-ID matching, side key and explicit PNG/PDF opening
- One shared key/color per VLAN ID regardless of name; aliases are retained.
- Combined keys stay beside switch diagrams with readable continuation pages.
- Open chart opens PNG; Open PDF is separate. Sheets… previews multiple pages,
  and Open PNG opens the displayed page. Latest-successful-project behavior stays.
- Consistent old palette overrides migrate; contradictory choices fail explicitly.


1.14.1: Clean combined sheets with one shared VLAN legend
- Combined diagrams start directly with switch headers, without a generic workspace title bar.
- Tight inter-switch spacing and one complete, deduplicated VLAN legend per sheet replace repeated per-switch VLAN lists.
- Shared VLAN IDs keep exactly the same colors in switches and legend; differing names appear as aliases.
- Configuration, diagram settings and build/results controls are grouped by purpose and scope. Unified Open chart/Open output folder follow the latest successful single or multiple-switch project; changes and failed builds do not remove access to previous output.
- Individual switch diagrams, exports, ordering, output naming and updater behavior remain unchanged.

Historical 1.14.0: Shared multi-switch A4 sheets and consistent workspace colors
- Shared A4 sheets now place multiple switches from separate configurations together in workspace order; pagination occurs only when another complete readable switch block will not fit.
- Exact-sheet PNG previews and vector PDFs use the same geometry; combined booklets retain full per-switch inventory appendices. Selected exports remain available.
- Named workspace folders default to the saved filename, support custom labels, and derive switch names before saving. Repeated names receive safe numeric suffixes.
- VLAN IDs alone now share one key/color, including differing or blank names. Normalized LAG labels retain their shared color mapping.
- This corrects the earlier separate-diagram-per-page behavior; see ../docs/DEVELOPMENT_HANDOFF.md for policies and validation.

Historical 1.13.0: Ordered multi-configuration workspaces
- Add multiple files or paste a new configuration without replacing existing items.
- Drag or use accessible up/down controls to organize export order; each item keeps independent device/model details and output state.
- Explicit .spworkspace.json save/open preserves source snapshots, order and selection; protect saved files like original configurations.
- Build selected reports or all items, with collision-safe batch folders and optional combined A4 diagrams/booklets (one complete diagram per page).
- Existing parsers, layout rules, formats, branding and updater safeguards remain unchanged.
- Full workflow and validation limits: ../docs/DEVELOPMENT_HANDOFF.md.

Run SwitchportDiagramBuilder.exe. The ZIP contains it at its root; it can also be copied
and run by itself. No companion EXE, wwwroot directory, .NET installation or DLL
beside the executable is required. Microsoft Edge WebView2 Runtime must still
be installed on Windows. The app restores its embedded interface and native
libraries into its per-user cache automatically.

Hardware coverage in 1.10.0
---------------------------
HARDWARE_CATALOG.csv contains 252 researched product identifiers and official
source references. 149 catalogue profiles are enabled, alongside the existing
legacy fixed/module/chassis and Instant On 1930 profiles. A researched entry
is not automatically a supported layout. Rows marked research-pending are
blocked; SUPPORT_COVERAGE.md explains the remaining portfolio coverage.
This is an expansion of verified support, not a claim that every HPE switch,
configuration syntax or export format is validated.

Enabled additions cover 2530, 2540, 2930F, CX 6000/6100/6200/6300/8320,
Comware 1920/1950/5130 EI, and Instant On 1960 JL805A-JL809A. Variants differ:
compact independent ports, 2530 and 5130 combo alternatives, horizontal
Comware uplinks, mixed 1950 media, and the 8320 three-row QSFP bank have their
own socket coordinates. The default socket scale and marker size are unchanged.
All active panels within a diagram share a height that accommodates the most
physical rows; existing one/two-row maps retain the compact 57.2 pt panels.

Comware text backups support explicit access/trunk/hybrid VLAN membership,
ingress PVID, VLAN names, voice configuration, bridge aggregates/LACP,
configured IRF links, admin state, descriptions, speed, SVIs and static routes.
Multiple untagged VLANs cannot use one native fill and block the diagram.
Unresolved port groups, unknown media/interface names and breakout syntax block.
Omitted defaults and dynamic voice admission remain unconfirmed.

Leave Model empty for automatic detection. A model is printed only when an
exact identity is supplied by the configuration or entered manually. The shared
PL layout profile does not print a model or family name. Type a model name to
use it in the report header, or choose an exact supported SKU to provide missing
hardware inventory. A typed name does not bypass physical-layout validation.
For member/module inventory,
enter pairs such as 1=JG932A,2=JG934A or A=J9986A,B=J9538A. The app records
operator-supplied identity and blocks conflicts with explicit source identity.
The shared PL family profile is an inference rule, not a selectable exact SKU.
Instant On 1960 automatic stacks retain the verified JL805A signature; other
stack members need explicit member=SKU pairs. A lead model cannot identify
unreported member variants. Bare 1930 backup identity remains automatic.

CLI: SwitchportGenerator.exe --input config.txt --output chosen-directory
Optional: --models JG932A or --models 1=JG932A,2=JG934A
Optional printed model name: --model-name "Aruba CX 6100"
Use --named-folder to put exports in the switch hostname/model child directory.

Launch view
-----------
Choose a saved file, select Paste configuration and paste the complete text,
or drop one configuration file anywhere in the window. In the paste dialog,
select Use configuration to load it, then Build reports. File and text drops
use the same hostname detection and optional model selection as saved files.
UTF-8 and BOM-marked UTF-16 files are supported. Inputs must contain text and
stay within the existing 20 MB limit. Multiple dropped files are rejected.

The compact interface contains configuration selection, output-folder selection,
generation, Open chart and Open output folder. The loaded configuration shows its
hostname when a hostname/sysname command exists. Open chart opens the latest
successful wall-chart.png in the default Windows image viewer. It is disabled
before success, while building, after a blocked build, and when the config,
model or output folder changes. A deleted chart prompts a new build.
Product branding does not mention its
browser engine. Primary controls fit at the minimum supported 900 x 650 window
size. Long paths use an ellipsis. Result details scroll within their own panel.

Output folders
--------------
Choose, paste or drop a switch configuration, optionally select Choose output folder,
then Build reports. The parent output location is remembered.
Reports are stored in a child folder named after the configured switch hostname.
If no hostname is supplied, the identified product/model is used. Unsupported
inputs with no hostname or confirmed/manually entered model use Unknown-Switch.
Windows-invalid filename characters are replaced with underscores.
Repeated exports create NAME (2), NAME (3), etc. Existing results are not replaced.
Open output folder opens the latest generated package in the selected parent.

Reports
-------
The main diagram is a visual port map on one A4 landscape page. Port fill shows
native VLAN membership; colored dots inside each socket show tagged VLANs.
A filled diamond identifies voice membership. The VLAN key includes matching
color blocks, dots or diamonds, VLAN IDs and configured VLAN names.

Every diagram footer and technician/validation report includes:
Confirm live link, LLDP/CDP, and MAC address table before moving production cables.

Client presentation in 1.10.1
---------------------------
Diagrams show configured port descriptions, VLAN and aggregate keys, and the
cable-move disclaimer. The Port notes section and generated explanatory
commentary are omitted. Blank VLAN names show only their VLAN ID. The shared
6000/6100 profile omits the model label. Manually entered names are printed
without changing the physical layout or the configured hostname.
Booklet pages omit export instructions and technical fingerprints. The text
summary contains switch data without reading instructions or assumptions.
The CSV contains structured configuration fields, with no free-text Notes or
Recommended Usage columns. Validation details, assumptions, provenance and
inventory fingerprints remain in validation-report.txt and inventory.json
for internal review. These two files are diagnostic records, not client reports.

Socket dimensions stay fixed by media type across every diagram. Ports are
25.2 pt wide, with a 23.2 pt simple RJ45 silhouette and a 16.2 pt optical
silhouette. Horizontal socket gaps are 1 pt; the 24.2 pt row pitch also
reserves the number line beneath optical cages.
Every port has a thin dark boundary. SFP/SFP+ cages use a wider, shallower
rectangle with small corner bevels and no internal contact/cage geometry.

RJ45 ports retain a narrow centered tab, 42 percent of the width and 5.4 pt
deep. Local numbers sit inside the tabs in fixed 4.5 pt bold text. Optical
numbers sit below the cages, horizontally aligned with the RJ45 tab numbers
on each physical row, in the same bold size. Slot/member context
appears in the panel heading; the key, CSV and inventory retain complete IDs.
The paired RJ45/SFP sockets on combo modules share the same local number.

VLAN markers retain their enlarged size from 1.9.9.
Every tagged circle and voice diamond uses the same fixed 3 pt size, including
markers in both keys and the visual reading guide. Centered rows start from
the top, with reserved clearance around numbers, halos and borders. Explicit
capacity limits block export rather than shrinking, overlapping or omitting
markers or required key entries. Numeric VLAN data is retained.

The symbol-based reading guide shows a filled block for native VLAN, a circle
for tagged VLAN, a diamond for voice, a rectangle outline for LAG/trunk,
a black block for stack/VSF and an exclamation mark for disabled ports.
The side VLAN key pairs the same symbols with full VLAN IDs and names.
A navy header and dark module title bands distinguish members and slots.
Stacks use Member 1, Member 2, etc., without redundant numeric or interface-prefix
suffixes. Modular stacks keep separate member and slot identities.
All active switch/module panels within a diagram share a common height.
One/two-row layouts use 57.2 pt, including optical-only modules and flexible
uplink bays; verified three-row layouts use 81.4 pt. Single-row port banks and their
number rows are centered within that height. Undeclared rows remain compact.
Panel gaps are 0.5 pt, with tighter title, key and description spacing.
Modular chassis use available sidebar space for the aggregate key to keep
more room below the map. Other keys sit below the ports, with a sidebar
fallback for dense layouts. Physical placement is retained.

Pastel VLAN palettes are chosen per configuration, with measured separation
between every used color and reserved white/grey states. Approximate familiar
hues are preferred only after choosing a separated palette. Colors may vary between configurations when needed to
keep the whole palette distinct. LAG/LACP/TRK borders use stronger colors
chosen against the actual VLAN palette and member fills, with a minimum
member color difference and separate colors for different aggregates. The
outline selector maximizes its weakest color separation before hue preference.
A denser candidate set is retried when necessary, retaining the same distance
guards. Similar colors are avoided when better separated options are available.
Continuous colored frames follow the entire socket, including the RJ45 tab
and the optical bottom edge, without a white separator. The frame is slightly
outset so numbers remain clear of the border. The key uses matching colored rectangle outlines and lists
aggregate names, modes and complete member IDs. Validation reports record
the exact palette. If sufficiently separated colors cannot be assigned,
export stops with a capacity message instead of reusing similar colors.

! after a number means explicitly disabled. Black fill identifies an explicit
stack/VSF link. The key also identifies unspecified/no native membership.
Port descriptions remain below the visual keys. There are no tagged-set
tables, letter references or native VLAN text footers.

The diagram is wall-chart.png, a4-one-page.png and a4-one-page.pdf. Both PNGs
are A4 landscape at 3508 x 2480 / 300 DPI, with physical margins matching PDF.
a4-booklet.pdf starts with the same visual page followed by port inventory
tables. Detailed numeric VLAN membership and supplied speed/neighbor fields
remain available in the report data and inventory rather than crowding the map.

Output files: wall-chart.png, a4-one-page.png, a4-one-page.pdf, a4-booklet.pdf,
port-map.csv, technician-summary.txt, validation-report.txt, inventory.json.
Keys wrap full values. Finite A4 capacity is checked explicitly; overflow fails
without silently shrinking sockets or dropping key content.

Physical chassis views
----------------------
Recognized 5406/5406R and 5412/5412R chassis show paired front-view slots
A/B, C/D, E/F, continuing G/H, I/J, K/L on twelve-slot chassis. Undeclared
slots retain their positions and are labeled "No module declared in configuration".
This does not assert that the physical bay is empty. 8212 zl diagrams retain
the central fabric region between E/F and G/H. 8206 zl uses a bottom fabric
region. Service regions are structural references, not installed inventory.
Modular member/slot identities such as 1/A and 2/B remain separate chassis.

3810M JL071A-JL076A bases keep fixed ports on the left and the declared front
uplink module(s) on the right. JL075A supports two separate A/B uplink bays.
Other bases have one A bay. Unreported bays retain their position. Rear stacking
module ports are not inferred or represented as front ports.
Unknown chassis capacity is explicitly labeled; only slot rows through the
highest declared letter are shown. Unsupported products and impossible slots
block final diagrams. Rack order of separate stack members is not inferred.

Instant On 1930 configuration support
-------------------------------------
Automatically recognizes vInstantOn_1930 firmware headers and explicit SKU
Description identity. Reads hostname, firmware, VLAN database/names, general
port tagged and untagged memberships, ingress PVID, configured global voice
VLAN/state, descriptions, speed, shutdown state and supplied SVI/route fields.
1930 profiles: JL680A/JL681A (8 RJ-45 + 2 SFP), JL682A/JL683A/B/JL684A/B
(24 RJ-45 + 4 SFP+), JL685A/JL686A/B (48 RJ-45 + 4 SFP+).

Native fills use explicit untagged egress membership. PVID is retained in the
structured CSV field and internal inventory; a differing ingress PVID and egress native VLAN blocks final diagrams
because the current visual cannot accurately combine those separate roles.
Omitted native settings remain Unknown. Dynamic OUI-based voice membership
is not inferred. The voice diamond marks explicit membership in the configured
voice VLAN. Disabled global voice state removes the voice designation.

The three supplied JL686B backups contain no aggregation definitions. Instant
On 1930 aggregation syntax remains unsupported and blocks final diagrams when
encountered. The source ZIP does not contain supplied configuration files.

Instant On 1960 configuration support
-------------------------------------
Supports JL805A (12 10GBASE-T RJ-45 + 4 SFP+) standalone switches and declared
stacks of up to four JL805A units. The complete port bank is a single horizontal
row with a blank left fascia. Member/port identities such as 4/16 are preserved.

The unit-type network te/uplink none signature is validated against the JL805A
profile. Declared units indicate configured inventory, not proven live members.
Other 1960 SKUs or ambiguous/mixed stack signatures block diagrams rather than
receiving copied chassis geometry. Physical stack-link ports are not identified
by the supplied backup and are not inferred from unused ports or unit numbers.

Reads interface TRK definitions and channel-group membership across members.
TRK VLAN settings replace inactive per-port shadow settings. AUTO/ON are retained
as configured mode labels, without claiming a live LACP negotiation state.
Each TRK has its own outline color, matching key, member list and inherited VLAN
markers. Trunk descriptions are retained in member notes. Missing TRK definitions,
orphan TRKs, invalid members and conflicting modes block final diagrams.

Additional local-backup support in 1.10.0
---------------------------------------
23 supplied AOS-Switch/AOS-CX backups verified, including legacy chassis,
3810M flexible-module declarations and CX 6100/6200F files. Wrapped VLAN
membership lists, including split interface tokens, retain their full membership.
CX comma/range VLAN database declarations and complete firmware labels are read.
VLAN names such as Community Center are retained while SNMP credential directives
remain outside the exported fields.

On an identified modular chassis, numeric module positions 1-12 map to A-L.
J9573x is accepted only with a corroborating J9573A product header.
J4907A has 16 logical interfaces and 18 physical sockets; T/M alternatives
share interface 15 or 16. Saved configuration does not establish the active medium.
PL firmware is shared by the CX 6000 and 6100 families. When no product
identifier is supplied, exactly one declaration for each interface 1/1/1-1/1/52
selects their common 48 RJ45 / 4 optical layout. Hostnames and descriptions do
not select hardware. The diagram omits the model and family name and uses
the SFP/SFP+ media label. Exact SKU, PoE and optical capability limitations
remain in the diagnostic validation report.
Explicit supported product identity takes precedence. Partial inventories,
duplicate or extra physical declarations, undeclared members, VSF/VSX context
and unsupported/conflicting product identity remain blocked. When no physical
inventory is established, the report omits secondary errors for each port.

Supported products
------------------
J9573A: left blank fascia, 24 RJ-45, right-side vertical SFP+ 25/26.
J4820A: top 1-6 / 13-18, bottom 7-12 / 19-24.
J4878A: four SFP sockets in one row.
J4907A: top 1-7, bottom 8-14, followed by 15T/15M and 16T/16M.
JL727B: 48 RJ-45, right-side 2x2 SFP+ 49/51 above 50/52.
JL675A/JL676A: 48 RJ-45, left SFP+ bank.
Qualified CX6000-6100-48G-4OPT shared profile: 48 RJ-45, left optical bank;
SFP versus SFP+ capability and exact SKU/PoE variant unconfirmed.
5304xl J4850A and 5308xl J4819A: paired slot rows A/B onward, with gaps preserved.
JL558A/JL256A: 48 RJ-45, left-side 2x2 SFP+ 49-52.
JL661A: 48 RJ-45, right-side 2x2 SFP56 49-52.
JL662A: blank left fascia, 24 RJ-45, right-side 2x2 SFP56 25-28.
J9536A: 20 RJ-45, SFP+ 21 above 22.
J9538A/J9993A: eight SFP+ in one horizontal row.
J9990A: 20 RJ-45, right-side SFP+ 21/23 above 22/24.
J9534A/J9986A/J9987A/J8702A/J9550A: 24 RJ-45.
J8705A/J9535A/J9549A: 20 RJ-45 + 4 SFP.
J9537A/J8706A: 24 SFP, two rows.
J9548A: 20 RJ-45 + 2 SFP+.
3810M bases JL071A-JL076A; uplinks JL083A (4 SFP+), JL078A (1 QSFP+),
JL079A (2 QSFP+), JL081A (4 RJ-45). Module identities must be explicit.
Chassis identities include J9821A/J9822A, J9850A/J9851A, J8697A/J8698A,
J8715A/B, J9091A/B, J9475A/J9477A and supported bundle identifiers in HardwareProfiles.cs.

Supported AOS-Switch, AOS-CX, Comware and validated Instant On 1930/1960 text backups only. Explicit model/module/member identity is required except for the qualified shared profile described above. Its model uncertainty remains in internal diagnostics.
Unknown geometry blocks final PNG/PDF rendering. Unconfigured ports remain in
known model inventory. VLAN defaults, installed transceivers, live status and
unprovided stack links are not inferred. Credentials/raw configuration are
not copied to reports. Hostnames, addresses and descriptions are still sensitive.
The parser supports the documented VLAN, aggregation, port, SVI, route,
management, mirror and neighbor fields, and does not interpret every possible
vendor directive. Operational neighbor input uses Local Port/Interface and
System Name detail fields. This is not universal configuration support.

Build and verification
----------------------
Use the .NET 8 SDK x64 and PowerShell -File .\build.ps1. The host embeds the
managed report generator, fonts, native libraries and interface. Its --generate
command accepts the generator arguments, including --named-folder to treat
--output as the parent directory. Without --named-folder, --output remains an
exact empty output directory for CLI compatibility.

The Windows CI workflow tests a copy of the EXE in an otherwise empty folder.
That workflow has not been executed here. The distributed release was built
on Linux; see TEST_REPORT.txt for actual and pending validation.
Startup diagnostics: %LOCALAPPDATA%\SwitchportDiagramBuilder\startup-error.txt.
Renderer/font notices are in Licenses. Source and documentation are optional
for running the app.

Confirm live link, LLDP/CDP, and MAC address table before moving production cables.

Trunk/LAG key symbols use simple colored rectangle outlines. The top visual
guide uses the same rectangular symbol. Physical port outlines retain their
RJ45/optical silhouettes and aggregate colors.

Pasted and dropped text is held in memory while the app is open. It is written
to a uniquely named temporary input only during report generation and removed
after that run, including blocked/error runs. The clipboard is not read or
changed automatically. Native shell drag/drop still needs Windows validation.


Additional 2920 and 5400R support (1.10.9)
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
updated SVG is preserved without changes and used for the app header and
regenerated Windows icon. Branding/ retains its editable Illustrator data,
port panels, VLAN markers, side key and matching magenta trunk outline. Branding/ also contains its
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

Changes in 1.10.9
----------------
Output now has a Copy button for the full displayed result, including multiline
validation errors and the output path. Success and clipboard-busy feedback do
not replace the result. The copied text uses the Windows Unicode clipboard.

AOS-Switch VLAN lists now join line wraps inside trunk names as well as physical
interface ranges, including Trk1-T followed by rk17 on the next line. Compact
seven-point member headings and reduced panel gaps support the supplied
eight-member, 416-interface configuration with 17 trunks on one A4 diagram.
Port shapes, printed port sizes, identifier size and VLAN marker sizes are
retained. Rectangle trunk keys match their outlines. The new diagram-style
icon has larger, more widely spaced VLAN dots and remains editable as SVG.

Changes in 1.10.10
-----------------
Applies the supplied updated Switchport Diagram Builder.svg artwork to the app
header, executable and window/taskbar icons. The original SVG bytes, including
Illustrator editing data, are retained as the editable master in Branding/.
PNG and all ten ICO sizes are regenerated. Icons are embedded during the
normal build; the compiled EXE is not modified afterward.

Changes in 1.10.11
-----------------
Fixes an AOS-Switch line-wrap regression where a command-only tagged line was
joined to the preceding untagged list. Membership commands now end a port
list before continuation fragments are joined. Wrapped physical interfaces
and trunk names remain supported. True native VLAN conflicts and invalid
cross-module ranges still block export. The supplied NHS modular
configuration exports all 184 interfaces with the correct VLAN memberships.

Changes in 1.10.12
-----------------
Removes automatic Switchport branding from generated diagrams, all PDF pages,
network summaries and validation reports. The chart heading is NETWORK PORT
MAP. The cable-move disclaimer and optional report-logo support are retained.

Changes in 1.10.13
-----------------
Load a configuration, then select Device details beside the Model field.
Each physical switch/member or chassis has optional Name, Serial number,
Asset tag and Other details fields. Modules, flexible uplinks and rear
modules share the identity of their parent switch/chassis. Hardware models
and member IDs come from the validated inventory and are not changed by
identity fields. Model overrides are considered when identifying devices.

Name and serial number label the port-panel headings. Full values, asset
tags and other details appear on Device details pages in the PDF booklet,
in the network summary, in inventory.json (Devices) and in five appended
port-map.csv columns. Existing port/VLAN geometry and the single A4 diagram
are preserved. Compact diagram headings may shorten long values; the booklet
and data exports retain the complete text. Empty fields do not invent names
or serial numbers, and no identity pages are added when all fields are blank.

Save details applies edits to the current loaded configuration and makes a
new report build necessary. Cancel discards pending edits. Loading or reloading a
configuration clears prior device details so they do not carry between clients.
Details remain in memory for the current selection and are retained in the
reports after generation. Temporary generator input is removed after the run.
Name, serial and asset fields accept 80 characters each; Other details accepts
400. All are optional.

CLI: --device-details device-details.json accepts an array containing Id,
Name, SerialNumber, AssetTag and Notes. Use physical device IDs from the
inventory Devices list. Duplicate/missing device IDs and oversized fields
block export. Input metadata cannot override detected hardware or member IDs.


Changes in 1.10.14
-----------------
Device details now provides separate Name, Serial number, Asset tag and
Other details inputs for every detected chassis, switch/member and module.
Module cards follow their parent and identify where they are installed.
Slot modules, flexible uplinks and rear modules each retain their own values;
entering a chassis serial does not populate a module serial. This replaces
the shared module identity behavior described for 1.10.13.

Names and serials label the relevant diagram panels. A single chassis is
identified in the chart header; modular stack chassis use their member heading.
Module details take priority over media/model text in narrow panel headings.
Compact headings can shorten long text, especially in flexible uplink bays.
Complete separate values appear in the PDF booklet, summary, CSV and JSON.
The fixed port/marker geometry and single A4 main diagram are preserved.

inventory.json schema 1.3 Devices includes authoritative Kind, ParentId and
GroupId fields. Parent IDs remain unchanged (for example 1); module IDs use
parent/slot (for example 1/A). Use these IDs with CLI --device-details.
Module models and hierarchy come from the validated inventory. The existing
five device CSV columns retain the parent values; five additional columns
Module ID, Module Name, Module Serial Number, Module Asset Tag and Module
Details map module values to the correct ports. Base-switch ports leave the
module columns empty. Empty module fields remain empty in data exports.

Save, Cancel, field limits and current-selection lifecycle remain as above.
Only modules present in the parsed inventory receive entries; undeclared empty
slots are not given invented hardware or serial numbers.


Changes in 1.10.15
-----------------
Module headings use only the details entered for that module. An empty module
keeps its slot/hardware label without copying the parent name, serial number
or asset tag. This removes the parent fallback described in 1.10.14.

An entered switch or chassis Name replaces the default name in the upper-left
corner of the main diagram and in booklet page headings. With no entered
switch/chassis name, the configured hostname is used. Module names, serial
numbers and asset tags do not replace this report name. If several stack
members have names, those names appear in inventory order separated by /.
Long headings may shorten visually; full entered values remain in the booklet
and data exports. Clearing a name restores the configured-name fallback.

The original configuration hostname remains in inventory data, the network
summary and output-folder naming. No source configuration is modified. The
chassis header retains its serial/asset identifier alongside the model without
repeating the entered chassis name on the right. Independent parent/module
CSV fields, module details, optional report logos and A4 geometry are retained.


Changes in 1.10.16
-----------------
The Model input/dropdown has moved into Device details. After loading a
configuration, open Device details and use the separate Model (optional)
field on each switch, stack member or chassis card. Each field offers a
searchable SKU list and accepts a typed model name. Switch and chassis lists
are separate. Detected hardware is shown for reference. Module identity
cards retain their own fields; module SKUs still come from the configuration.

Leave Model blank for automatic detection. Choosing an exact supported SKU
can establish missing hardware for that member/chassis, subject to the same
port-layout and configuration validation. A typed model name changes only
that device's printed model label. It does not change port counts, media,
source configuration or the original detected SKU. Conflicting or unsupported
SKU choices are rejected. A report label accepts 100 characters on one line.
Missing member models can be entered on their own cards, without writing
member=SKU strings. Source member IDs remain authoritative.

Model edits share Save details and Cancel with the identity fields. Save
invalidates the prior chart. Cancel discards unsaved model changes. Clearing
an entry returns that device to automatic detection. Loading another
configuration clears all prior selections. Selected models remain scoped to
the current configuration. Reports retain the entered values.

Per-device model labels appear in the diagram, booklet and network summary.
JSON schema 1.4 Devices records ModelSelection, DetectedModel and Model.
CSV appends Device Model and Module Model while retaining the existing columns.
Full selected/typed model values are wrapped on booklet identification pages;
compact diagram headings can shorten long labels. Chassis SKU selection can
establish an unidentified chassis capacity. Mixed unidentified chassis layouts
still require a common verified layout for one diagram; typed labels can be
independent without changing geometry. Earlier CLI --models and --model-name
options remain supported. CLI --device-details accepts ModelSelection per
parent ID in addition to Name, SerialNumber, AssetTag and Notes.

The supplied name override, blank module headings, logo support, editable
icon assets and fixed A4 port/marker geometry from prior versions are retained.

Changes in 1.11.0
-----------------
VLAN circles and voice diamonds have a minimum 4.2 pt diameter, 40 percent
larger than 1.10.16. Markers retain clear spacing, numbers and outlines.
Ports containing unusually many VLANs receive taller faces rather than smaller
markers or omitted membership. A readable per-port limit remains enforced.

The diagram body, including its panels, labels, port shapes, VLAN markers and
keys, scales uniformly to fill available A4 space. Small maps use compact panel
widths and can enlarge substantially. Dense maps retain their normal scale;
headers, reading guide, client logo and disclaimer keep their reserved areas.
The existing single-page A4 and 300 DPI PNG export formats are retained.
These rules supersede earlier fixed marker/port size descriptions above.

MikroTik RouterOS .rsc input and unencrypted .backup loading are now available
through Choose and file drag/drop. Text exports can also be pasted. Four verified
CRS profiles are included; see MIKROTIK_SUPPORT.md for models, semantics,
references and the explicit binary/SwOS/breakout limits. The supplied binary
backup generates a physical chart while preserving all logical lane identities
in the JSON data. No exact model name is inferred from an interface signature.


Changes in 1.11.1
-----------------
The PDF booklet now prints complete tagged VLAN lists. All port-inventory cells
wrap at the existing readable text size, rows grow to fit their values, and page
breaks keep ordinary rows together. A row longer than a page continues on later
pages with its original port identity and a Continued label. Table headings,
page numbers, client logos and the disclaimer remain on every applicable page.
The single-page port diagram is retained. Explicit all-tagged policy is printed
as All. Listed VLAN IDs are not shortened, silently omitted or range-compacted.
Native fill samples in the inventory table now match the chosen diagram colors.

VLAN colors retain the soft/pastel style while prioritizing perceptual separation.
Several deterministic candidate searches and weakest-pair refinement compare
hue, lightness and chroma with CIEDE2000. Color assignment also separates VLANs
sharing a port or neighboring native fills. Familiar hue preferences yield to
clearer separation. Crowded palettes broaden the soft-color selection and relax
the old fixed-distance floor when necessary, rather than failing solely on
color capacity. Different source configurations can have different palettes;
the same configuration produces the same assignment. This is a best-effort
color selection, not a guarantee that unlimited pastel categories will look
unique on every display or printer, or a color-vision accessibility claim.

CIEDE2000 implementation and official reference test data:
https://hajim.rochester.edu/ece/sites/gsharma/ciede2000/


Changes in 1.11.2
-----------------
Adaptive layout preserves the original RJ45 and optical interface proportions.
It first uses available page space to enlarge the diagram uniformly. Crowded
layouts try tighter padding, gaps and readable role-wide typography before any
marker reduction. Interfaces no longer become taller to hold more VLAN markers.
Port labels retain at least 4.5 pt printed text; compact panel and key text
retains at least 7 pt. Headers, client logos and the cable-move disclaimer retain
their reserved page regions.

Tagged VLAN circles and voice diamonds prefer a 4.2 pt printed diameter. After
all supported compact layouts are considered, a single shared marker diameter
is reduced only enough to fit every membership in its actual port face. Guide
and key markers use that same size. The minimum accepted diameter is 2.2 pt;
configurations exceeding the readable single-page capacity still block export
rather than omit markers. Voice designations scoped to a port remain local to
that port. See LAYOUT_SIZING.md for the layout rules and regression coverage.

Complete paginated PDF VLAN lists, contrasting pastel color selection, MikroTik
support and all other exports from 1.11.1 are retained.


Changes in 1.11.3
-----------------
A readable VLAN key is reserved before enlarging the port diagram. Its full
heading and numeric VLAN IDs remain intact, and every text column has at least
78 printed points of usable width beyond its symbols and gutters. Longer words
receive extra space when available; unusually long names can wrap into readable
fragments. Wide banks can place the key below the diagram in up to five columns.

Printed text has upper bounds as well as readable lower bounds: body/key text
at most 9 pt, ordinary panel titles at most 8.5 pt, key headings at most 10 pt,
and port numbers 4.5-9 pt. Heading ink stays inside its backing. Port faces
retain their original proportions at one uniform geometry scale.

Preferred marker radius now follows uniform growth, min(4.2 pt, 2.1 pt * scale),
so sparse enlarged diagrams retain visible dots and voice diamonds. All guide,
key and per-port symbols share the same size. Only dense diagrams use the
largest-fitting global reduction after compact side and below-bank layouts.
Complete PDF tagged VLAN lists, contrasting pastel colors and MikroTik support
are retained. See LAYOUT_SIZING.md for sizing and regression coverage.


Changes in 1.12.0
-----------------
Adds the built-in GitHub updater. Automatic checks report available stable
releases; download and installation remain separate explicit actions. Open
Updates to check, review notes, download, delay the reminder or install and
restart. Report generation blocks installation until it finishes. Packages
and executables are validated before replacement, and failed visible startup
restores the previous executable. Settings, report logos, outputs and loaded
configuration details are retained across an update restart.

Canonical development source and release feed:
https://github.com/TechSavage1989/SwitchPort_Diagram_Builder

See the root README, docs/UPDATING.md and DEVELOPMENT.md for the current workflow,
package contract and recovery details. All 1.11.3 layout fixes remain.
