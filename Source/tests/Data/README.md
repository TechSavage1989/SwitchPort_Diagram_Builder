CIEDE2000 reference data
=======================

ciede2000-reference.tsv contains the 34 numerical reference color pairs and
published differences supplied by Gaurav Sharma, Wencheng Wu and Edul N. Dalal
with their CIEDE2000 implementation notes.

Primary source:
https://hajim.rochester.edu/ece/sites/gsharma/ciede2000/
https://hajim.rochester.edu/ece/sites/gsharma/ciede2000/dataNprograms/ciede2000testdata.txt

Paper: The CIEDE2000 Color-Difference Formula: Implementation Notes,
Supplementary Test Data, and Mathematical Observations. Color Research and
Application 30(1), 21-30, 2005. DOI 10.1002/col.20070.

PaletteChecks validates the application's independently implemented formula
against every pair, including the near-180-degree cases that expose incorrect
hue-angle handling. No reference MATLAB implementation is bundled or executed.
