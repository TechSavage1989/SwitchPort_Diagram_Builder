# FortiGate 200G and 201G (FortiOS)

## Hardware evidence

Supported models: FG-200G, hardware/config identifier FG2H0G; FG-201G, identifier FG2H1G. Both share the documented physical front panel. Exact identifiers are required in the first backup header line; a different model or a name/alias alone does not establish this layout.

Official reference: [Fortinet FortiGate 200G Series QuickStart Guide](https://docs.fortinet.com/product/fortigate/hardware), front panel pages 8-9. The same bank counts appear in the [FortiGate 200G Series datasheet](https://www.firewalls.com/pub/media/wysiwyg/datasheets/Fortinet/fortigate-200g-series-1.pdf).

The diagram renders all 30 physical Ethernet sockets in their front-panel bank order:

| Faceplate | Physical ports | Type |
| --- | --- | --- |
| Dedicated HA, MGMT | ha, mgmt | 1 GE RJ45, vertical pair |
| GE bank | port1 through port8 | 8 x 1 GE RJ45, odd over even |
| Multigigabit bank | port9 through port16 | 8 x 5/2.5/1 GE RJ45, odd over even |
| SFP+ bank | x1 through x8 | 8 x 10/1 GE SFP+, odd over even |
| SFP bank | port17 through port20 | 4 x 1 GE SFP, odd over even |

No socket is invented for console, USB, cellular/PPPoE modem, VPN tunnels, virtual-switch subinterfaces or the administrative GUI. This is a front-panel map only and does not imply active cable connectivity.

## FortiOS configuration support

- Reads the model identifier and firmware from the configuration-version header, and hostname from system global.
- Reads physical interface aliases/descriptions, configured role, admin status, configured static/DHCP address and speed.
- Reads configured VLAN subinterface name, VLAN ID and parent from system interface. Marks a parent physical socket as tagged for that VLAN, or the physical aggregate members if the VLAN sits on an aggregate. Preserves the logical interface and any configured address separately.
- Reads aggregate member lists from type aggregate interfaces and draws the colored LAG outline on referenced physical sockets.
- Reads static routes and DHCP server interface references for non-physical network inventory summaries.
- Keeps routing, DHCP and interface information as *configuration-derived*, not live observed connectivity. LLDP/MAC/ARP data can still be attached separately through Supplementary data imports.

FortiGate is a routed firewall. A physical interface with a configured IP address or VLAN subinterfaces does **not** imply a native/untagged switch VLAN. Native VLAN remains unspecified unless an explicit supported source proves otherwise. Secondary IPs, non-standard virtual-switch mappings, SD-WAN runtime route decisions and dynamic VPN/HA state are not claimed to be live or completely parsed.

## Safety and validation

Customer FortiGate backups may contain encrypted passwords and keys, privileged API settings and internal or public addressing. Store them securely and do not commit, share or paste them into public issues or CI. The parser copies only an allowlist of configuration values into generated inventory, not the full backup or admin/security-policy sections. Saving a workspace still embeds the original imported configuration, so protect workspace files as confidential.

Unknown FortiGate models are intentionally blocked rather than drawn with an inaccurate faceplate. Use [model identification and hardware coverage](../Source/SUPPORT_COVERAGE.md) for the other supported devices. The test project FortiOsChecks uses only fictional hostnames and reserved documentation IP addresses.
