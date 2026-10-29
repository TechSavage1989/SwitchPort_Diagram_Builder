# Cisco network module catalogue

Checked against Cisco documentation on 2026-10-29. The selector is a declaration
of installed hardware, not a transceiver compatibility checker or a live-link
detector. The implementation keeps the original configuration unchanged.

## Selectable coverage

The catalogue contains 29 Cisco module identifiers, including four blank modules.
They are filtered by the selected/detected base switch and member. Forty-six
additional base SKU layouts make the modular families available in the model
selector; existing supported Cisco layouts remain available.

| Base family | Selectable modules | Compatibility limits |
| --- | --- | --- |
| Existing 48-port 3560-E shared/exact profile | CVR-X2-SFP, CVR-X2-SFP10G; X2-10GB-SR, LR, ER, CX4, LX4, LRM, ZR | Two independent X2 bays. TwinGig provides two SFP sockets, OneX one SFP+ socket, and an X2 transceiver one 10G connection. Hardware revisions and IOS support must be checked against the Cisco release notes. |
| WS-C3560X and WS-C3750X | C3KX-NM-1G, C3KX-NM-10G, C3KX-NM-10GT, C3KX-SM-10G, C3KX-NM-BLANK | One network module slot per switch/member. The 10G module has four physical sockets with paired operating modes; the service module has two SFP+ sockets. |
| WS-C3850 | C3850-NM-4-1G, C3850-NM-2-10G, C3850-NM-4-10G, C3850-NM-8-10G, C3850-NM-2-40G, C3850-NM-BLANK | The 4-10G option is limited to 48-port copper and 12/24 XS bases. The 8-10G and 2-40G options are exposed on the supported 24XS base. The 4-1G and 2-10G are excluded from XS bases. |
| C9200 modular, non-multigigabit | C9200-NM-4G, C9200-NM-4X, C9200-NM-BLANK | C9200L fixed uplinks are not interchangeable network modules. |
| C9300 modular, non-multigigabit | C9300-NM-4G, C9300-NM-4M, C9300-NM-2Q, C9300-NM-8X, C9300-NM-2Y, C9300-NM-BLANK | Compatible C3850 module PIDs are also offered on C9300, as confirmed by the 9300 guide. C9300L and C9300LM fixed uplinks do not receive these options. C9300X modules are a different compatibility family. |

## Base SKU audit

Only the following exact base variants were taken from the model tables in the
linked Cisco hardware guides/data sheets. License suffixes normalize to these
base SKUs; port counts are never inferred from the highest interface number.

| Source model table | Added base variants |
| --- | --- |
| 3750-X/3560-X hardware overview, tables 1-1 and 1-2 | WS-C3560X-24T, 24P, 24U, 48T, 48P, 48PF, 48U; WS-C3750X-24T, 24P, 24U, 48T, 48P, 48PF, 48U, 12S, 24S |
| 3850 data sheet, table 1 | WS-C3850-24T, 24P, 24U, 48T, 48P, 48F, 48U, 12S, 24S, 12XS, 24XS |
| 9200 hardware overview, table 2 | C9200-24T, 24P, 24PB, 48T, 48P, 48PB, 48PL |
| 9300 hardware overview, table 1 | C9300-24T, 24P, 24U, 24UB, 24H, 24S, 48T, 48P, 48U, 48UB, 48H, 48S |

## Numbering and retained alternatives

- 3560-E upper bay: Te0/1 or TwinGig Gi0/49 and Gi0/50. Lower bay:
  Te0/2 or Gi0/51 and Gi0/52. The six saved interface settings stay independent.
- C3KX-NM-10G socket order is Gi1, Te1/Gi2, Gi3, Te2/Gi4. Selecting the
  module establishes four sockets, not six. Canonical physical entries use the
  Te interface on the SFP+ sockets; alternative Gi settings remain separately
  identified and are never merged or silently discarded. The C3KX service module
  similarly preserves its alternative interface settings.
- 3850 2-10G uses Gi member/1/1-2 and Te member/1/3-4. The 4-10G and
  8-10G modules use Te interfaces even with 1G SFPs. Native QSFP mode uses two
  FortyGigabitEthernet interfaces; configured breakout alternatives remain
  qualified software entries.
- 9300 4G uses Gi member/1/1-4, 4M uses Te member/1/1-4, 8X uses
  Te member/1/1-8, 2Y uses TwentyFiveGigE member/1/1-2, and 2Q native mode
  uses FortyGigabitEthernet member/1/1-2. Both Cisco long and short 25G interface
  spellings are recognized.

An unknown module retains a qualified configuration view. Choosing a module
establishes only its physical socket count/media. Saved alternatives remain
qualified entries and do not add to the physical count. Selecting a named BLANK insert adds no ports; Automatic returns to configuration
evidence. Show-inventory evidence cannot be contradicted by an operator selection.

## Primary references

1. [Cisco IOS 15.0(2)SE supported hardware and X2/TwinGig list](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/software/release/15-0_2a_se6/release/notes/OL25302.html)
2. [3750-X/3560-X product overview, switch models and module table](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/hardware/installation/guide/3750x_3560x_HIG/HIGOVERV.html)
3. [3750-X/3560-X module installation and pair restrictions](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/hardware/install/notes/net_mod_note.html)
4. [IOS interface characteristics and C3KX interface aliases](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/software/release/15-2_1_e/configuration/guide/scg3750x/swint.html)
5. [3850 module compatibility, native interface names and breakout alternatives](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3850/hardware/installation/guide/b_c3850_hig/m_install_netmod.html)
6. [3850 base switch and module data sheet](https://www.cisco.com/c/en/us/products/collateral/switches/catalyst-3850-series-switches/data_sheet_c78-720918.pdf)
7. [9200 base switch and module compatibility](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9200/hardware/install/b-c9200-hig/product_overview.html)
8. [9300 base model inventory](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9300/hardware/install/b_c9300_hig/Product-overview.html)
9. [9300/9300X module tables and interface numbering](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9300/hardware/install/b_c9300_hig/Installing-a-network-module.html)

## Researched, not exposed as verified layouts

C9200-NM-2Y and C9200-NM-2Q require C9200 multigigabit bases. C9300X-NM-2C,
4C, 8M and 8Y require compatible C9300X bases, with 4C restricted to
C9300X-24Y/48HX/48TX. These are documented by the references above, but are not
offered for other switch families. Their additional base interface layouts need
separate verification before they become selectable. Power supplies, fans,
StackWise cables and unrelated SFP optics are not network expansion cards.

10. [3560-E OneX converter support and exact PID](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750e_3560e/software/release/12-2_53_se/release/notes/OL21139.html)
