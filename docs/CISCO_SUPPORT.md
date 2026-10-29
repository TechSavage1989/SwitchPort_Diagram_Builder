# Cisco IOS and IOS XE support

Version 1.15.0 adds a dedicated Cisco parser. It keeps Cisco interface names,
physical member boundaries, native VLANs, voice VLANs and port-channel membership.
It does not send the backup to an online service.

## Verified layouts

| Model | Physical data sockets | Identification |
| --- | --- | --- |
| WS-C3650-48PD | 48 RJ-45, two SFP and two SFP+ per member | Explicit switch provisioning, chassis PID or per-device model selection |
| C9500-24Y4C | 24 SFP28 and four QSFP28 | Explicit chassis PID/model selection, or an exact complete native interface signature |
| C9500-48Y4C | 48 SFP28 and four QSFP28 | Explicit chassis PID/model selection, or an exact complete native interface signature |

License suffixes do not change these socket layouts. A hostname, boot filename,
partial interface list or last configured interface cannot identify hardware.
Conflicts with explicit provisioning/PIDs block generation. Management interfaces,
SVIs, loopbacks and logical port channels do not become physical front-panel ports.
Rear StackWise connections and live adjacency are not invented from provisioning.

The 3650 has four horizontally arranged uplink cages. G3/TE3 and G4/TE4 are
alternative software names for the same cages. Empty alternative stubs are allowed;
multiple configured aliases require confirmation rather than a guessed winner.
Full interface IDs and aliases are retained in exported inventory and CSV.

## VLAN and aggregation behavior

Access, static trunk, native/voice VLANs, allowed-list replacement/add/remove/
except/all/none, descriptions, shutdown, configured speed, routed ports, SVIs,
routes, and LACP/PAgP/static port channels are normalized. Explicit port-channel
VLAN fields apply to members, including cross-stack members; missing channel
fields retain explicit member settings. Channels without physical members remain
logical inventory and are reported as a limitation rather than drawn as sockets.

All/except policies stay symbolic in CSV and the PDF inventory, including every
exclusion and native treatment. Diagram dots show the VLANs identified by the
backup, with a complete policy key below the switch. These policies do not claim
that every permitted VLAN exists or forwards live. Global native tagging is
respected. A configured access VLAN without static access mode is marked dynamic
and its DTP operational mode remains unconfirmed. Completely unconfigured ports
remain unknown; explicit trunk/access mode uses the documented VLAN 1 default.

Unsupported ranges, breakout, subinterfaces, private VLAN modes, ISL encapsulation
and unrecognized VLAN-changing commands fail explicitly. These are coverage limits,
not invitations to infer a compatible layout.

## Appended hardware inventory

Append the switch's `show inventory` output after its running configuration in
the same text input when the running configuration omits its chassis PID. Named
switch/supervisor records identify the chassis. Power supply, fan and transceiver
PIDs do not identify a switch model. Installed optics do not change cage geometry
or establish live speed. This release does not import transceiver serial numbers
into device identity fields automatically.

## Catalyst 3560-E limitation

The supplied 48-port 3560-E backups contain both GigabitEthernet0/49-52 and
TenGigabitEthernet0/1-2 stubs. WS-C3560E-48PD-SF confirms the base switch, but each
X2 bay can hold an X2 transceiver or a TwinGig converter with two independent SFP
sockets. The installed converters cannot be recovered reliably from old interface
settings, descriptions or a switch-only asset screenshot.

These inputs are recognized as Cisco and their configured interfaces are retained
under `LogicalInterfaces` in the blocked inventory JSON. Physical diagrams remain
blocked until each bay's installed module is established. A typed report model
does not bypass this requirement. Supply `show inventory` or equivalent bay/PID
evidence for those switches before enabling a specific converter layout.

## Hardware references

- [3650 hardware installation guide, model table, front panel and uplink LED numbering](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3650/hardware/installation/guide/Cat3650hig_book/HIGOVERV.html)
- [9500 hardware installation guide, Y4C native numbering figures](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9500/hardware/install/b_catalyst_9500_hig/9500_product-overview.html)
- [Cisco interface guide, 3560-E X2/TwinGig and alternate uplink naming](https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/software/release/15-2_1_e/configuration/guide/scg3750x/swint.html)

Private backups, inventory output, addresses and device serials are not repository
fixtures. CI uses synthetic Cisco and Aruba inputs only.
