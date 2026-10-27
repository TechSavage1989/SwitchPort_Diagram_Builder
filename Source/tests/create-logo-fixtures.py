"""Self-contained synthetic inputs for the existing report-logo geometry checks."""
from pathlib import Path
import sys
from PIL import Image
root = Path(sys.argv[1])
root.mkdir(parents=True, exist_ok=True)
(root / "config.txt").write_text('; JL256A Configuration Editor\nhostname LOGO-LAB\nvlan 10\n untagged 1-48\n exit\n')
for name, size in [("wide.png", (400, 80)), ("tall.png", (80, 400)), ("square.png", (200, 200)), ("wide.jpg", (400, 80)), ("wide.webp", (400, 80))]:
    Image.new("RGB", size, "#426c86").save(root / name)
(root / "invalid.png").write_text("This is not an image.")
