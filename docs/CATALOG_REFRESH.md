# Model and module audit, 1.17.0

Checked 2026-10-29. The complete [catalog snapshot](../Source/ALL_MODELS_MODULES.csv)
is generated from runtime data, including legacy profiles, chassis, Cisco and
MikroTik. It contains 451 identities: 279 selectable switches/chassis, 75 module
PIDs and 97 researched identities whose physical layouts remain pending.
CI rejects drift between the snapshot and the actual inspector choices.
Supported means the saved configuration and physical layout can be represented;
it does not establish installed optics, live link state, or firmware compatibility.

## Cisco 3650 inspector

The exact standard-port profiles are WS-C3650-24TS, 48TS, 24PS, 48PS, 48FS,
24TD, 48TD, 24PD, 48PD, 48FD, 48FQ, 48PQ, 48TQ, 24PDM and 48FQM.
The L/S/E license suffixes normalize to these base identities. Fourteen profiles
are new; 48PD remains supported. S variants have four SFP uplinks, D variants two
SFP plus two SFP+, and Q variants four SFP+. The fixed uplinks form one horizontal
row; they do not receive removable network-module dropdowns.

48PD/48FD, 48PS/48FS and 48PQ/48FQ are explicitly audited same-layout PoE bundle
pairs. An operator selection may refine a provisioning-only identity within a
pair. The assumption records both identities and does not infer a power budget.
Exact show-inventory, license-UDI or trusted model/PID evidence cannot be
overridden this way. Other count/media/family conflicts still block generation.
Each stack member retains independent model selection, notes and port settings.
Automatic restores source provisioning. The source file is never rewritten.

- [3650 model table, front panels, fixed-uplink numbering](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3650/hardware/installation/guide/Cat3650hig_book/HIGOVERV.html)
- [Fuji 16.9 supported hardware](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3650/software/release/16-9/release_notes/ol-16-9-3650.pdf)
- [3650 data sheet and power-bundle differences](https://www.cisco.com/c/en/us/products/collateral/switches/catalyst-3650-series-switches/data_sheet-c78-729449.html)

## Additional Cisco modules

Five new bases: C9300X-12Y, 24Y, 24HX, 48HX and 48TX. Fiber bases use
TwentyFiveGigE member/0/port, copper bases TenGigabitEthernet member/0/port.
The long TwentyFiveGigabitEthernet spelling is also accepted.

| Module | Physical layout | Native interface | Restriction |
| --- | --- | --- | --- |
| C9300X-NM-2C | Two QSFP28 cages, one row | HundredGigE member/1/1-2 | C9300X only |
| C9300X-NM-4C | Two columns, two rows; 1/2 then 3/4 | HundredGigE member/1/1-4 | Only 24Y, 48HX, 48TX |
| C9300X-NM-8M | Four columns, two rows | TenGigabitEthernet member/1/1-8 | C9300X only |
| C9300X-NM-8Y | Four columns, two rows | TwentyFiveGigE member/1/1-8 | C9300X only |
| C9300X-NM-BLANK | No sockets | None | C9300X only |

Existing module compatibility is retained. Other software uplink alternatives
remain independent qualified entries. Native cage layouts do not establish
breakout lanes. C9300X-48HXN remains pending because it has firmware-dependent
usable-module-port restrictions; it is not silently treated as a 48HX.

- [Base model tables and front panels](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9300/hardware/install/b_c9300_hig/Product-overview.html)
- [Module compatibility and native interface tables](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9300/hardware/install/b_c9300_hig/Installing-a-network-module.html)
- [Cisco data sheet, Figure 2 module photographs](https://www.cisco.com/c/en/us/products/collateral/switches/catalyst-9300-series-switches/nb-06-cat9300-ser-data-sheet-cte-en.html)
- [HXN firmware-dependent limits](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9300/software/release/26-x/configuration_guide/int_hw/b_26x_int_and_hw_9300_cg/configuring_interface_characteristics.html)

## HPE/Aruba audit

All 188 product IDs in the current [official AOS-CX port map](https://arubanetworking.hpe.com/techdocs/new-central/content/cfg/profiles/intf-prof-mod-port-map.htm)
were compared with the existing catalog; none were missing. Broad port-type
tables alone do not establish cage geometry. Four pending profiles are now
enabled after checking installation-guide drawings:

- R8N89B and S4R21A: 12 PoE copper plus independent copper 13/14 and SFP 15/16.
  Their optical bank is on the left. [6000/6100 guide, front-panel group 1](https://arubanetworking.hpe.com/techdocs/hardware/switches/6100/IGSG/igsg_6000-6100.pdf).
- R8Q72A and R8V13A: 12 PoE copper plus independent copper 13/14 and SFP+ 15/16.
  The SFP+ pair is horizontal on the right lower row. Management/stacking
  connectors are excluded. [6200 guide, Figure 3](https://arubanetworking.hpe.com/techdocs/hardware/switches/6200/IGSG/Aruba_6200_IGSG.pdf).

The embedded HPE catalog now has 193 enabled and 97 pending entries. Existing
41 module PIDs and their parent/slot restrictions are retained and exercised.
Legacy/chassis entries outside that JSON also appear in the complete snapshot.

## MikroTik

Three new RouterOS bridge-VLAN profiles are based on manufacturer front photographs:
[CRS305-1G-4S+IN](https://mikrotik.com/product/crs305_1g_4s_in),
[CRS326-24G-2S+RM](https://mikrotik.com/product/CRS326-24G-2SplusRM) and
[CRS326-24G-2S+IN](https://mikrotik.com/product/crs326_24g_2s_in).
CRS305 has one copper plus four SFP+ in a row; both CRS326 copper models have
24 copper ports and a horizontal two-cage SFP+ bank. Console is excluded.
Matching rack/desktop binary signatures select shared geometry while leaving
the exact SKU unconfirmed. SwOS backups are not newly supported.

## Maintenance

Run CatalogChecks with an empty output directory and --refresh after reviewing
runtime catalog edits. Commit Source/ALL_MODELS_MODULES.csv with the changes.
Normal CI runs without --refresh and verifies exact consistency, all inspector
choices, every enabled module's compatible parent, targeted identity boundaries,
save/open, and actual PDF/PNG/CSV output. Existing complete geometry and module
sweeps remain required. Private configs and generated reports stay outside Git.

The refresh does not claim every switch ever manufactured. Six multigigabit
3650 profiles, additional 9200/9300 variants and the explicitly pending HPE
families need further interface/layout verification before becoming selectable.
