VLAN color selection - version 1.11.1
====================================

Palette selection favors the weakest perceptual separation between VLANs.
CIEDE2000 replaces plain Euclidean Lab distance as the primary ranking measure.
The candidate set retains soft colors with Lab lightness 60-91 and every RGB
component at least 95, while using a wider range of hues and lightness.

The selector tries seven deterministic starting hues and improves the weakest
remaining color slots. White, unspecified-native gray and black remain reserved.
VLAN 1 retains its established blue-gray reference. The preferred Lab18
separation guard is retained for ordinary palettes. Crowded palettes compare
more relaxed candidates by their actual perceptual separation, rather than
failing solely because one fixed distance threshold is no longer attainable.
Every assigned VLAN still receives a unique color.

After selecting the separated color set, assignment favors differences between
VLANs appearing together on a port, native fills on adjacent ports, and adjacent
entries in the key. Familiar approximate VLAN hues are a lower priority.
The palette is deterministic for a configuration. Cached color comparisons and
palette-count searches keep repeated renders inexpensive.

Numerical comparisons against version 1.11.0 (minimum CIEDE2000 distance among
all VLAN pairs, higher is better):

Palette                         Previous       Updated
20 VLANs without VLAN 1            10.398         14.826
28 VLANs without VLAN 1             6.743         11.720
20 VLANs including VLAN 1          10.328         14.794
28 VLANs including VLAN 1           7.608         11.958
36 VLANs including VLAN 1          blocked        11.106

The test set checks 20, 28 and 36-category palettes with and without VLAN 1,
64-color best-effort output, uniqueness, soft-color bounds, reserved-state
separation, deterministic reuse and configuration-aware mapping. All 34
published CIEDE2000 reference pairs pass, including angular boundary cases.

A finite soft-color gamut cannot make arbitrarily many categories equally easy
to distinguish. CIEDE2000 guides the search; it is not an accessibility guarantee,
a mathematical proof of a globally optimal palette, or a guarantee of printed
color accuracy. Full numeric VLAN identities remain authoritative in the key
and the inventory report.

Primary formula and reference data:
https://hajim.rochester.edu/ece/sites/gsharma/ciede2000/
Sharma, Wu and Dalal, Color Research and Application 30(1), 2005,
DOI 10.1002/col.20070.
