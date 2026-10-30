# Physical port-position audit, 1.18.0

This audit covers the enabled hardware catalog, the built-in Cisco and MikroTik
profiles, all six 3810M bases, the 3800 J9573A, ten Instant On 1930 SKUs, shared
identity profiles, chassis slot placement, and every selectable network module.
The 97 research-pending catalog entries remain disabled. A family reference
applies to its documented variants; it is not evidence that a particular module
is installed in a user's switch.

The diagrams are readable front/rear port maps, not dimensioned mechanical
faceplate drawings. Rows, left-to-right order, bank boundaries, blank fascia,
combo alternatives and module bays are retained. Port silhouettes use a common
scale. Dense reports can reduce inter-bank whitespace to 45% of its normal
allowance, while preserving each boundary, the occupied rows, leading fascia
and large interior blank areas. Neither port count nor an unused CLI interface
is treated as proof of an installed module.

## Corrected behavior

- 3650 copper ports are grouped in banks of twelve; all four fixed uplinks stay
  on the lower right. 24-port models retain their unused left fascia.
- 3750-X fiber, 3850 S/XS fiber and 9300X Y fiber bases use a single lower row.
  9300 SFP bases retain their two-row arrangement. Copper models retain bank
  gaps and the blank left half on their 24-port variants.
- 9500-24Y4C retains the large blank area between its 24 SFP28 ports and four
  right-hand QSFP28 uplinks. The 48-port model keeps its central bank boundary.
- Installed Cisco network modules occupy the right side of their own member.
  3560-E X2/TwinGig bays retain upper/lower positions. Unconfirmed software
  alternatives remain a separate, qualified section. Copper module faces use
  local socket numbers; full interface names remain in the inventory.
- MikroTik two-row switches count odd ports along the bottom. CRS326-24S+2Q+RM
  also has Q1 below Q2. CRS309/317 Ethernet is to the right of the fiber bank;
  CRS305 Ethernet stays on the left. Copper and fiber bank gaps are retained.
- Aruba/HPE profiles retain left-side optics, leading/interior fascia and bank
  gaps. 2530 24-port Fast Ethernet uplinks occupy one lower row. Compact 2530
  combo models preserve separate 9T, 10T, 9M, 10M physical positions.
- 3810M flex modules align with the lower row; JL075A keeps its four fiber
  banks. JL076A separates its eight Smart Rate ports from its two 20-port banks.
- 2920 rear A/B expansion bays are shown side-by-side beneath the front view.
  The rear view is explicitly labeled; unreported bays do not acquire sockets.
- XL modules retain their row-block numbering; zl/zl2 single-row modules retain
  their lower positions and documented gaps. Chassis A/B, C/D and subsequent
  slot pairs, empty bays, and 8200 fabric regions are preserved.
- Instant On 1930 and 1960 now use their explicit socket coordinates in the
  finished diagram, including JL805A's single row and blank left fascia.

## Manufacturer references reviewed

Page numbers below are PDF pages, unless a figure or section is named. Some
manufacturer-authored documents are linked through public mirrors when the
manufacturer's download endpoint was unavailable. Product identity and layout
sources for individual catalog SKUs remain in the two catalog CSV files.

| Hardware | Reference and relevant view |
|---|---|
| Cisco 3650 | [Hardware installation guide](https://data.nag.wiki/Cisco/Datasheets/C3650/C3650_hig.pdf), pp. 21-23, figures 1-1 through 1-4 |
| Cisco 3560-X / 3750-X and C3KX modules | [Cisco data sheet](https://data.nag.wiki/Cisco/Datasheets/C3750X/Cisco_Catalyst_3750-X_and_3560-X_Series_Switches_Data_Sheet.pdf), pp. 3-4 and 7, front panels and module photographs |
| Cisco 3850 and modules | [Hardware guide](https://data.nag.wiki/Cisco/Datasheets/C3850/c3850_hig.pdf), pp. 21-22, 29, 69-71; front panels, fiber numbering, network modules |
| Cisco 9200 | [Cisco data sheet](https://data.nag.wiki/Cisco/Datasheets/Catalyst_9200_data_sheet.pdf), p. 4, figure 1; [hardware guide](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9200/hardware/install/b-c9200-hig/product_overview.html), network modules |
| Cisco 9300 / 9300X and modules | [Hardware guide](https://data.nag.wiki/Cisco/Datasheets/C9300/C9300_Hardware_Installation_Guide.pdf), pp. 17-23 and 83-86; [Cisco data sheet](https://www.cisco.com/c/en/us/products/collateral/switches/catalyst-9300-series-switches/nb-06-cat9300-ser-data-sheet-cte-en.html), figures 2-3, network module photographs |
| Cisco 9500 Y4C | [Hardware guide](https://data.nag.wiki/Cisco/Datasheets/C9500/C9500_hig.pdf), p. 24, figures 10-11, native port numbering |
| Cisco 3560-E X2 / TwinGig | [Existing verified module references](CISCO_MODULE_REFERENCES.md), upper/lower bays and independent converter interfaces |
| Aruba CX 6000 / 6100 | [Installation guide](https://arubanetworking.hpe.com/techdocs/hardware/switches/6100/IGSG/igsg_6000-6100.pdf), pp. 7-8 |
| Aruba CX 6200 | [Installation guide](https://arubanetworking.hpe.com/techdocs/hardware/switches/6200/IGSG/Aruba_6200_IGSG.pdf), pp. 8-10, including compact fixed uplinks |
| Aruba CX 6300 | [Installation guide](https://arubanetworking.hpe.com/techdocs/hardware/switches/6300/IGSG/igsg_6300.pdf), pp. 10-11 |
| Aruba 8320 | [Installation guide](https://arubanetworking.hpe.com/techdocs/hardware/switches/8320/IGSG/Aruba_8320_IGSG_en_us.pdf), pp. 6-8, including six QSFP ports in three rows |
| Aruba 2530 | [Installation guide](https://support.hpe.com/hpesc/public/api/document/c04623600), pp. 10, 13-14 |
| Aruba 2540 | [Installation guide](https://support.hpe.com/hpesc/public/api/document/c05325081), p. 6 |
| Aruba 2930F | [Installation guide](https://arubanetworking.hpe.com/techdocs/hardware/switches/2930/IGSG/Aruba_2930_IGSG.pdf), p. 6 |
| HPE 1920 | [Getting started guide](https://support.hpe.com/hpesc/public/api/document/c04463762), pp. 25-29 |
| HPE 1950 | [Getting started guide](https://itt.com.au/wp-content/uploads/2021/05/HPE-OfficeConnect-1950-Switch-Series-Getting-Started-Guide.pdf), pp. 36-38 |
| HPE 5130 EI | [Getting started guide](https://fccid.io/m/1c78a7ef4c3ca09f7794a48b674539171b4d75dc6fa2490054b12e2addc98030.pdf), pp. 39-42 |
| Instant On 1930 | [Installation guide](https://arubanetworking.hpe.com/techdocs/InstantOn_1930_Switch/InstantOn_1930_IGSG.pdf), pp. 5-8 |
| Instant On 1960 | [Installation guide](https://arubanetworking.hpe.com/techdocs/InstantOn_1960_Switch/igsg_1960.pdf), pp. 6-8 |
| Aruba 2920 | [Installation guide](https://support.hpe.com/hpesc/public/api/document/c03898362), pp. 10, 21 and 24, front, rear and expansion modules |
| HPE 3800 J9573A | [Installation guide](https://server-shop.ua/assets/images/resources/11027/hp-3800-installation-and-getting-started-guide.pdf), p. 10, figure 1-1 |
| Aruba 3810M | [Installation guide](https://support.hpe.com/hpesc/public/api/document/c04948676), pp. 7-9, all six bases and flex modules |
| zl v1 modules | [Module installation guide](https://ftp.hp.com/pub/networking/software/5400zl-ModuleInstall-Apr2006-59914714.pdf), module front-panel illustrations |
| zl v2 modules | [Module installation guide](https://support.hpe.com/hpesc/public/api/document/emr_na-c04344577?docLocale=en_US), pp. 5-6; use written SKU specification where the generic illustration is shared |
| zl2 v3 modules | [Module installation guide](https://support.hpe.com/hpesc/public/api/document/c04654886), pp. 5-6 |
| 5300xl modules | [Module installation guide](https://ftp.hp.com/pub/networking/software/5300-Module-Install-59912130-0305.PDF), pp. 6-7 |
| 5300xl chassis | [Installation guide](https://ftp.hp.com/pub/networking/software/5300xl-Install-May2006-59914750.pdf), p. 12 |
| 5400zl chassis | [Installation guide](https://ftp.hp.com/pub/networking/software/5400zl-Install-Feb2007-59914741.pdf), p. 15 |
| 5400R chassis | [Installation guide](https://support.hpe.com/hpesc/public/api/document/emr_na-c04655001?docLocale=en_US), p. 18 and chassis front views |
| 8200zl chassis | [Installation guide](https://gzhls.at/blob/ldb/c/a/4/1/3648723de60744c6dabfb663b8d999a0dc9b.pdf), pp. 16-17, 8212/8206 front views |

MikroTik's own product photographs establish physical numbering and port sides:
[CRS326 fiber](https://mikrotik.com/product/crs326_24s_2q_rm),
[CRS328](https://mikrotik.com/product/crs328_24p_4s_rm),
[CRS317](https://mikrotik.com/product/crs317_1g_16s_rm),
[CRS309](https://mikrotik.com/product/crs309_1g_8s_in),
[CRS305](https://mikrotik.com/product/crs305_1g_4s_in),
[CRS326 rack](https://mikrotik.com/product/CRS326-24G-2SplusRM), and
[CRS326 desktop](https://mikrotik.com/product/crs326_24g_2s_in).

## Regression evidence

PositionChecks exercises every inspector choice and all 75 module PIDs in a
finished QuickSheet scene, plus the ten parser-supported 1930 variants. It checks
physical identities, count, upper/lower and left/right ordering, visible bank
boundaries, same-member Cisco module placement, readable copper module labels,
and the literal manufacturer relationships above. It also exports actual
PNG/PDF/CSV files and a per-model position-audit.csv into the CI artifact.

GeometryChecks retains its complete catalog sweep, chassis and combo checks,
and dense eight-member 416-port/17-trunk case. Its former uniform-column
assertion now checks the specific 2930F bank boundaries and uniform spacing
inside each bank. AdaptiveLayoutChecks and LayoutQualityChecks retain their
font, proportional-shape, marker-count and dense VLAN guards. Existing parser,
PDF inventory, metadata, workspace and updater checks remain mandatory.

No private configuration is required by or committed with these checks.
