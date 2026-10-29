HPE / Aruba coverage and research status
=======================================

Current hardware catalogue: 1.16.0.

This release does not claim universal support for every HPE/Aruba product or
backup format. It enables verified socket layouts and supported configuration
fields. A catalogue identity with an unverified layout remains blocked.
HARDWARE_CATALOG.csv lists 290 product identifiers, 189 enabled profiles and
101 research-pending entries. The earlier legacy profiles remain available.
The JSON embedded in the executable is the authoritative enabled catalogue;
no online service or external catalogue file is required at runtime.

Enabled catalogue families
--------------------------

| Family | Enabled catalogue entries | Configuration format |
| --- | ---: | --- |
| Aruba 2530 | 12 | AOS-Switch |
| Aruba 2540 | 4 | AOS-Switch |
| Aruba 2930F | 15 | AOS-Switch |
| CX 6000 | 20 | AOS-CX |
| CX 6100 | 6 | AOS-CX |
| CX 6200F/M | 35 | AOS-CX |
| CX 6300F/M and documented variants | 24 | AOS-CX |
| CX 8320 | 3 | AOS-CX |
| OfficeConnect 1920 | 9 | Comware text |
| OfficeConnect 1950 | 5 | Comware text |
| FlexNetwork 5130 EI | 5 | Comware text |
| Instant On 1960 JL805A-JL809A | 5 | Instant On local text |
| Aruba 2920 base switches | 5 | AOS-Switch |
| 5300xl modules | 9 | AOS-Switch |
| Original zl modules | 5 | AOS-Switch |
| v2 zl modules | 11 | AOS-Switch |
| v3 zl2 modules | 10 | AOS-Switch |
| 3810M flexible modules | 4 | AOS-Switch |
| 2920 rear modules | 2 | AOS-Switch |

Existing profiles also cover the documented 3800, all six 3810M base variants
and supported flex modules, 5304xl/5308xl, 5406/5412 zl/zl2, 8206/8212 chassis,
validated XL/zl/zl2 line cards and all ten previously validated Instant On 1930
SKUs. See HARDWARE_LAYOUT.md and README.md for their precise identities.

Selected device details offers 41 compatible HPE/Aruba module PIDs, including
16 newly enabled layouts. Options depend on the identified or selected parent
and exact bay. Fourteen additional chassis/bundle aliases establish chassis
capacity without establishing installed module inventory. Returning a bay to
automatic restores configuration evidence. See [module support](../docs/MODULE_SUPPORT.md)
for exact compatibility, numbering, layout sources and software-mode restrictions.

Physical details preserved
--------------------------

- 2530 8-port switches: interfaces 9/10 each have separate RJ45 and SFP
  alternatives. Both sockets are drawn, with one logical interface per pair.
- 2530 Fast Ethernet uplinks: the extra copper pair is above the optical pair.
- Compact CX 6100 JL679A: copper 13/14 and optical 15/16 are independent
  interfaces. They are not incorrectly collapsed into combo ports.
- 1920/5130 smaller variants have horizontal lower optical uplink rows;
  1920 48-port variants use the verified right-hand 2x2 bank.
- 1950: optical and 10G copper uplinks have variant-specific numbering and
  physical positions. Copper and optical types are not inferred from number alone.
- 5130 JG933A: interfaces 17-24 have SFP/RJ45 alternatives; 28 logical
  interfaces produce 36 physical sockets. Active medium is unconfirmed.
- 8320 48-port variants: right QSFP columns contain 49/50/51 and 52/53/54,
  three physical rows. All active panels in that diagram use the same height.
- Instant On 1960 24/48-port variants retain their separate two copper and two
  SFP+ uplinks; the 12XGT model keeps its single horizontal bank.

Configuration identity and limits
---------------------------------

A selected SKU or member/slot inventory is recorded as operator-supplied
information. Conflicts with explicit source product identity block generation.
A hostname, connected-device description, last configured port or lead switch
SKU does not establish arbitrary hardware inventory. The qualified complete
PL-family inference is visibly labelled 6000/6100 with exact SKU, PoE and optical
capability unconfirmed.

Comware parser fixtures cover access, trunk and hybrid membership, PVID,
space-separated/to VLAN ranges, voice configuration, bridge-aggregation/LACP
inheritance, configured IRF link ports, descriptions, admin state, speed,
SVIs and routes. They are synthetic fixtures, not a claim that live hardware
or production backups for every catalogue SKU were tested. Unsupported VLAN,
port-group, breakout or interface/media forms block the relevant output.
Multiple egress-untagged VLANs cannot use a single native fill and are blocked.
Omitted defaults and dynamic VLAN/phone admission are not inferred.

The application retains a fixed socket/marker scale and a single A4 working
page. Larger inventories or legends can exceed that page. A model profile is
not a guarantee that every configuration will fit. The capacity guard blocks
unreadable output; it does not shrink dots, silently omit ports or rearrange
physical rows. Diagram panels share a height within each output, allowing
three-row hardware without changing the existing one/two-row map scale.

Remaining portfolio work
------------------------

Research-pending catalogue rows include other 4100i, 5420, 6300 variants,
6400 line cards, 8100, 8325/H/P, 8360, 8400, 9300/S and 10000/10040 products.
These require exact per-variant socket/media validation and, where relevant,
new chassis/member/breakout handling. A count in a product table alone is not
used as proof of a complete physical layout. Instant On S0F35A has no usable
front illustration in the referenced public guide and remains pending.

The wider historical HPE portfolio is not yet exhaustively catalogued. This
includes additional ProCurve/ProVision 25xx/26xx/28xx/29xx/35xx products, older
Aruba Mobility Access switches, additional OfficeConnect/smart-managed backup
formats, Comware 3100/3600/5120/5130 HI/5140/5150/5500/5510/5520/5550/5600,
5700/5710/5720/5800/5820/5900/5901/5920/5940/5945/5950/5960/5980 and modular
7500/7500X/10500/11900/12500/12900 generations. Binary, encrypted and cloud-only
exports require separate format support. Unmanaged switches have no saved
VLAN configuration from which to produce this report. These are explicit
coverage gaps, not enabled generic guesses.

Primary research references
---------------------------

Official CX model/port catalogue:
https://arubanetworking.hpe.com/techdocs/new-central/content/cfg/profiles/intf-prof-mod-port-map.htm

Official HPE CX portfolio:
https://www.hpe.com/us/en/products/networking/cx-switches.html

Official HPE Comware portfolio, including Comware 9 generations:
https://www.hpe.com/lamerica/en/products/networking/comware.html

Official legacy HPE model/software support reference:
https://arubanetworking.hpe.com/techdocs/AirWave/830/webhelp/Content/SupportedDevices/HP.htm

2530 hardware guide, front views and network port definitions:
https://support.hpe.com/hpesc/public/api/document/c04623600

2540 hardware guide:
https://support.hpe.com/hpesc/public/api/document/c05325081

2930F hardware guide:
https://arubanetworking.hpe.com/techdocs/hardware/switches/2930/IGSG/Aruba_2930_IGSG.pdf

6000/6100 hardware guide:
https://arubanetworking.hpe.com/techdocs/hardware/switches/6100/IGSG/igsg_6000-6100.pdf

6200 hardware guide:
https://arubanetworking.hpe.com/techdocs/hardware/switches/6200/IGSG/Aruba_6200_IGSG.pdf

6300 hardware guide:
https://arubanetworking.hpe.com/techdocs/hardware/switches/6300/IGSG/igsg_6300.pdf

8320 hardware guide, printed pages 5-7:
https://arubanetworking.hpe.com/techdocs/hardware/switches/8320/IGSG/Aruba_8320_IGSG_en_us.pdf

1920/1950/5130 official product specifications with front photographs:
https://www.hpe.com/psnow/doc/c04394247
https://www.hpe.com/psnow/doc/c04545486
https://www.hpe.com/psnow/doc/c04394228

Comware 5130 EI Layer 2 command reference, including hybrid egress, trunk
PVID, VLAN ranges, voice-vlan and aggregation commands:
https://support.hpe.com/hpesc/public/api/document/c05062946

Instant On 1960 hardware guide, figures 1-5:
https://arubanetworking.hpe.com/techdocs/InstantOn_1960_Switch/igsg_1960.pdf


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

MikroTik RouterOS support in 1.11.0
--------------------------------
Four additional verified CRS profiles and unencrypted backup/text import are
implemented outside the HPE catalogue. See MIKROTIK_SUPPORT.md for exact scope
and binary, SwOS, switch-chip and QSFP breakout limitations.
