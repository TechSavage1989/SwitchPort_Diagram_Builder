# Selectable switch modules

Version 1.17.0 retains all 41 HPE/Aruba module PIDs and expands Cisco from 29 to 34, for 75 distinct module choices. See [the catalog audit](CATALOG_REFRESH.md).

Device details provides a module choice for each supported hardware bay. Choices
are filtered by the selected or established parent switch/chassis family and by
the bay. Switch members have independent choices. Automatic detection is the
default; selecting a module supplies operator hardware evidence and changes the
rendered inventory rather than merely changing a report label. Returning a bay
to automatic uses the original configuration again.

## HPE and Aruba coverage

The catalog contains 41 network-module PIDs across these six parent families.
Sixteen PIDs add newly enabled socket layouts; the remaining 25 retain their
existing layout behavior and gain sourced, compatible dropdown choices.

| Parent family | Compatible module PIDs | Bay scope |
| --- | --- | --- |
| ProCurve 5300xl | J4820A, J4820B, J4821A, J4821B, J4878A, J4878B, J4852A, J4907A, J8161A | A-D on 5304xl; A-H on 5308xl |
| ProCurve 5400zl | J8702A, J8705A, J8706A, J8707A, J8708A; all 11 v2 PIDs below | A-F on 5406zl; A-L on 5412zl |
| ProCurve 8200zl | J8702A, J8705A, J8706A, J8707A, J8708A; all 11 v2 PIDs below | A-F on 8206zl; A-L on 8212zl |
| Aruba 5400R zl2 | All 11 v2 PIDs and all 10 v3 PIDs below | A-F on 5406R; A-L on 5412R |
| Aruba 3810M | JL078A, JL079A, JL081A, JL083A | Front A; front A/B on JL075A. JL079A only on JL072A/JL074A/JL076A |
| Aruba 2920 | J9731A, J9732A | Rear A/B, independently per member |

v2 zl: J9534A, J9535A, J9536A, J9537A, J9538A, J9546A, J9547A,
J9548A, J9549A, J9550A and J9637A.

v3 zl2: J9986A, J9987A, J9988A, J9989A, J9990A, J9991A, J9992A,
J9993A, J9995A and J9996A. These are not offered for older 5400zl or 8200zl
chassis. v1 zl cards are not offered for 5400R zl2.

The added layouts are J4820B, J4821A, J4821B, J4878B, J4852A, J8161A,
J8707A, J8708A, J9546A, J9547A, J9637A, J9988A, J9989A, J9991A,
J9992A and J9996A. They include MT-RJ, X2, CX4, SFP, SFP+, QSFP+ and
RJ45 modules. Port counts represent interfaces. J4907A retains two shared
RJ45/SFP interface pairs rather than counting both media sockets as separate
interfaces. Slot-prefixed numbering and VLAN/voice/LAG state remain independent.

Fourteen additional chassis/bundle aliases use the established layouts:
J4848A/B, J4849A/B, J8166A, J8167A, J9532A, J9533A, J9539A, J9540A,
J9641A, J9642A, J9643A and J9866A. A bundle identity establishes chassis
capacity, not which modules remain installed; modules still require configured
inventory or explicit operator selection.

## Research and layout evidence

Catalog entries retain manufacturer source and layout-source URLs. Network-port
counts, connector types, numbering and row arrangement were checked against the
manufacturer module illustrations. Existing legacy geometries remain unchanged.

| Evidence | Use |
| --- | --- |
| [5300xl A-revision module installation guide](https://ftp.hp.com/pub/networking/software/5300-Module-Install-59912130-0305.PDF), printed page 1 | Original A-revision RJ45/SFP options |
| [5300xl module installation guide, 5991-2130](https://ftp.hp.com/pub/networking/software/5300xl-ModulesInstall-May2006-59912130.pdf), printed pages 1-2 | xl modules, banked 24-port ordering, 12-port MT-RJ row and shared J4907A pairs |
| [5300xl module read-me, 5991-2150](https://ftp.hp.com/pub/networking/software/5300xl-ModulesReadMe-Apr2005-59912150.pdf) | B-revision module identity and firmware scope |
| [5300xl QuickSpecs](https://www.hpe.com/psnow/doc/c04283168) | xl A-revision counts, parent chassis and bundles |
| [zl module installation guide, 5991-4714](https://ftp.hp.com/pub/networking/software/5400zl-ModuleInstall-Apr2006-59914714.pdf), printed pages 1-2 | Original zl network cards, including single-row X2/CX4 |
| [zl module read-me](https://ftp.hp.com/pub/networking/software/zl-ModulesReadMe-Aug2007-59918547.pdf) | 5400zl/8200zl compatibility |
| [v2 zl module installation guide, 5998-5076a](https://support.hpe.com/hpesc/public/api/document/emr_na-c04344577?docLocale=en_US), printed pages 6-7 | Eleven v2 cards, including 8 RJ45 and mixed 12 RJ45/12 SFP |
| [5400zl QuickSpecs](https://www.hpe.com/psnow/doc/c04111646) | Legacy chassis aliases and v2 module compatibility |
| [8200zl QuickSpecs](https://www.hpe.com/psnow/doc/c04111638) | 8200zl v1/v2 compatibility and J9641A chassis |
| [v3 zl2 module installation guide](https://support.hpe.com/hpesc/public/api/document/c04654886), printed pages 6-7 | Ten v3 modules, including QSFP+ row placement and Smart Rate copper |
| [5400R zl2 module reference](https://support.hpe.com/hpesc/public/docDisplay?docId=c04334041&docLocale=en_US&page=GUID-11226C8F-22DD-49A6-9C18-64BC9842ED6D.html) and [QuickSpecs](https://www.hpe.com/psnow/doc/c04293383) | 5400R v2/v3 compatibility |
| [3810M installation guide](https://support.hpe.com/hpesc/public/api/document/c04948676) and [Flexible uplink modules](https://arubanetworking.hpe.com/techdocs/AOS-S/16.10/ATMG/KB/content/kb/fle-upl-mod-kb-16..htm) | Flexible module options and one-/two-bay switch variants |
| [Module compatibility modes](https://arubanetworking.hpe.com/techdocs/AOS-S/16.11/MCG/KB/content/wc/com-mod-v2-zl-zl-mod.htm) and [5400R v2 support](https://arubanetworking.hpe.com/techdocs/AOS-S/16.11/MCG/KB/content/kb/all-v2-mod.htm) | Explicit module-generation restrictions |
| [2920 installation guide](https://support.hpe.com/hpesc/public/api/document/c03898362), printed pages 23-25 | Two-port rear SFP+/10GBASE-T cards and A/B positions |

## Scope and constraints

The selector is a physical inventory tool, not a firmware or live-link test.
Required switch software, compatibility modes, PoE budgets and supported optics
still depend on the installed equipment. 5400R v2 cards require v2 compatibility
mode and cannot be selected for an explicitly v3-only or VSF configuration.
Original zl v1 cards cannot be selected when v1 modules are explicitly disabled.
JL081A provides data ports on non-PoE bases; PoE requires a PoE base and supply.
Module speeds are capabilities, not
observed link rates. Breakout is not inferred from a QSFP cage.

Power supplies, fans, supervisors, service/controller cards without external
switch interfaces and dedicated stacking cards are not offered as network line
cards. Unsupported or unidentified parent hardware does not receive a guessed
module list. Existing configuration declarations remain authoritative; selections
cannot silently replace contradictory installed-module evidence.
