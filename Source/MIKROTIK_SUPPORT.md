MikroTik support - version 1.11.0
================================

Supported input
---------------
Choose or drop an unencrypted RouterOS .backup file, or choose/drop/paste a
RouterOS .rsc text export. Existing text configuration loading is preserved.
Encrypted backups receive a message requesting an unencrypted backup or text
export. The application does not execute RouterOS commands or restore a backup.

Verified physical profiles
--------------------------
CRS326-24S+2Q+RM: 24 SFP+, two QSFP+ cages and one management Ethernet port.
The SFP+ sockets are paired odd/even across two rows. The QSFP+ cages are on
the right, vertically paired; management Ethernet is farther right. The RJ45
serial console is not represented as a network interface.
CRS328-24P-4S+RM: 24 copper ports and four SFP+ ports, in two rows.
CRS317-1G-16S+RM: one management Ethernet port and sixteen SFP+ ports in one row.
CRS309-1G-8S+IN: one management Ethernet port and eight SFP+ ports in one row.

A text export must identify its model in the RouterOS model comment, or the
operator must select a supported model in Device details. Renamed interfaces
are mapped using their default-name selectors. A typed free-form model label
continues to change the printed label without inventing hardware.

A complete, unique binary interface signature may select the corresponding
physical geometry. It does not establish an exact product identity. Unconfirmed
model names remain blank in the client reports. Exact manual model selection
remains available in Device details.

Bridge VLAN configuration
-------------------------
Text exports support bridge VLAN filtering, tagged/untagged tables, VLAN ID
ranges, ingress PVID, frame admission, interface comments/admin state, renamed
interfaces, and bonding member inheritance including 802.3ad mode. For enabled
bridge filtering, PVID creates the documented dynamic untagged membership for
ports admitting untagged frames. Stored VLAN-table assignments on a bridge
with filtering disabled are not drawn as active assignments. Unknown references,
conflicting native VLANs, incompatible PVID/egress assignments and unsupported
switching constructs block export. VLAN names do not imply voice VLAN status.

The binary decoder is intentionally limited to the inventory and bridge-port/
static VLAN-table schemas validated with the supplied backup. It never extracts
user databases, passwords, keys, certificates or scripts. Static tagged/untagged
assignments are shown as configured; bridge membership disabled state and ingress
PVID are retained in diagnostic data. Bridge filtering state, dynamic forwarding,
firmware, exact model identity and operational link state are not inferred from
undocumented fields. Use a full .rsc export for complete bridge VLAN semantics.
Unsupported binary schemas and encrypted/damaged archives fail explicitly.

For the supplied Winthrop-switch2.backup, the hostname is ScaleFiberSwitch2.
It contains 27 physical network sockets and 33 logical Ethernet interfaces.
There are five static untagged VLAN 220 assignments, four static untagged VLAN
251 assignments, and sfp-sfpplus1 is tagged for VLAN 251 while untagged for 220.
All logical interfaces are retained separately in inventory.json. The physical
CSV appends Logical interfaces to identify each QSFP cage's four source lanes.
The two QSFP cages are drawn once each when all four lanes have identical VLAN,
aggregate and interface admin assignments. Different lane assignments block the
physical chart until a dedicated breakout representation is implemented.

Limits
------
This release does not claim support for all MikroTik models or SwOS backups.
Other CRS/CSS profiles, legacy switch-chip VLAN translation, interface-list VLAN
membership, mixed QSFP lane settings, MLAG and bridge firewall transformations
need additional validation. Unsupported cases receive a validation message.
Raw binary files can only be selected/dropped; the paste dialog accepts text.

Official hardware and configuration references
----------------------------------------------
https://mikrotik.com/product/crs326_24s_2q_rm
https://manual.mikrotik.com/hardware/crs326-24s-plus-2q-plus-rm/
https://mikrotik.com/product/crs328_24p_4s_rm
https://mikrotik.com/product/crs317_1g_16s_rm
https://mikrotik.com/product/crs309_1g_8s_in
https://help.mikrotik.com/docs/spaces/ROS/pages/40992852/Backup
https://help.mikrotik.com/docs/spaces/ROS/pages/328155/Configuration+Management
https://help.mikrotik.com/docs/spaces/ROS/pages/28606465/Bridge+VLAN+Table
https://manual.mikrotik.com/docs/bridging-and-switching/

Binary container structure was cross-checked against the originating project:
https://github.com/BigNerd95/RouterOS-Backup-Tools
The decoder is implemented locally and does not run or depend on that project.
