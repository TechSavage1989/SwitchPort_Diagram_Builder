"""Inspect the actual batch artifacts produced by WorkspaceChecks, including A4 and order."""
import json
import sys
from pathlib import Path
from PIL import Image
from pypdf import PdfReader

root = Path(sys.argv[1])
batch = Path((root / "batch-to-inspect.txt").read_text())
manifest = json.loads((batch / "workspace-export.json").read_text())
successful = [item for item in manifest["Items"] if item["Success"]]
assert len(successful) == 3 and manifest["FailureCount"] == 1
diagrams = PdfReader(batch / "workspace-a4-diagrams.pdf")
assert len(diagrams.pages) == len(successful)
booklets = PdfReader(batch / "workspace-a4-booklets.pdf")
offset = 0
for index, item in enumerate(successful):
    folder = Path(item["OutputDirectory"])
    inv = json.loads((folder / "inventory.json").read_text())
    name = next((row["Name"] for row in inv["Devices"] if row["Name"]), inv["Hostname"])
    assert name in diagrams.pages[index].extract_text(), (index, name)
    individual = PdfReader(folder / "a4-booklet.pdf")
    assert name in booklets.pages[offset].extract_text(), (offset, name)
    for local_index, page in enumerate(individual.pages):
        assert booklets.pages[offset + local_index].extract_text() == page.extract_text()
    offset += len(individual.pages)
    for png in ("wall-chart.png", "a4-one-page.png"):
        with Image.open(folder / png) as image:
            assert image.size == (3508, 2480)
            assert abs(image.info["dpi"][0] - 300) < 0.1
assert len(booklets.pages) == offset
for pdf in (diagrams, booklets):
    for page in pdf.pages:
        assert abs(float(page.mediabox.width) - 841.89) < 0.5
        assert abs(float(page.mediabox.height) - 595.28) < 0.5
        assert "Confirm live link, LLDP/CDP, and MAC address table" in page.extract_text()
print("PASS combined PDF page order, per-item booklet equality, A4 sizes and 300 DPI raster exports")
