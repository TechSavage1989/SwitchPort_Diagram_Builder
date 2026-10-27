"""Inspect shared-sheet output, per-item appendices, ordering, A4 and image parity."""
import json
import sys
from pathlib import Path
from PIL import Image
from pypdf import PdfReader

root = Path(sys.argv[1])
batch = Path((root / "batch-to-inspect.txt").read_text())
manifest = json.loads((batch / "workspace-export.json").read_text())
layout = json.loads((batch / "workspace-layout.json").read_text())
successful = [item for item in manifest["Items"] if item["Success"]]
assert len(successful) == 3 and manifest["FailureCount"] == 1
diagrams = PdfReader(batch / "workspace-a4-diagrams.pdf")
assert len(diagrams.pages) == manifest["PageCount"] < len(successful)
assert [entry["Id"] for entry in layout["Placements"]] == [item["Id"] for item in successful]
assert any(sum(entry["PageNumber"] == index + 1 for entry in layout["Placements"]) > 1 for index in range(len(diagrams.pages)))
booklets = PdfReader(batch / "workspace-a4-booklets.pdf")
for index, page in enumerate(diagrams.pages):
    text = page.extract_text()
    assert "WORKSPACE PORT MAPS" not in text
    assert text.count("VLAN key") == 1
    assert text.count("10 Data") == 1 and text.count("150 Voice (Voice)") == 1
    page_entries = [entry for entry in layout["LegendEntries"] if entry["PageNumber"] == index + 1]
    assert len({entry["Identity"] for entry in page_entries}) == len(page_entries)
    assert page.extract_text() == booklets.pages[index].extract_text()
offset = len(diagrams.pages)
for index, item in enumerate(successful):
    folder = Path(item["OutputDirectory"])
    inv = json.loads((folder / "inventory.json").read_text())
    name = next((row["Name"] for row in inv["Devices"] if row["Name"]), inv["Hostname"])
    placement = layout["Placements"][index]
    for vlan_id, color in inv.get("VlanColorOverrides", {}).items():
        assert any(entry["VlanId"] == int(vlan_id) and entry["Color"] == color for entry in layout["LegendEntries"] if entry["PageNumber"] == placement["PageNumber"])
    assert name in diagrams.pages[placement["PageNumber"] - 1].extract_text(), (index, name)
    individual = PdfReader(folder / "a4-booklet.pdf")
    for local_index, page in enumerate(individual.pages[1:]):
        assert booklets.pages[offset + local_index].extract_text() == page.extract_text()
    offset += len(individual.pages) - 1
    for png in ("wall-chart.png", "a4-one-page.png"):
        with Image.open(folder / png) as image:
            assert image.size == (3508, 2480)
            assert abs(image.info["dpi"][0] - 300) < 0.1
assert len(booklets.pages) == offset
for png in manifest["CombinedImages"]:
    with Image.open(png) as image:
        assert image.size == (3508, 2480)
        assert abs(image.info["dpi"][0] - 300) < 0.1
for pdf in (diagrams, booklets):
    for page in pdf.pages:
        assert abs(float(page.mediabox.width) - 841.89) < 0.5
        assert abs(float(page.mediabox.height) - 595.28) < 0.5
        assert "Confirm live link, LLDP/CDP, and MAC address table" in page.extract_text()
print("PASS multiple configs on shared A4 sheets, workspace order, full per-item appendices, A4 sizes and 300 DPI previews")
