import csv
import json
from pathlib import Path
import sys
from PIL import Image
from pypdf import PdfReader

root = Path(sys.argv[1])
expected = (root / 'expected-policy.txt').read_text()
for directory in sorted(root.glob('report-*')):
    inv = json.loads((directory / 'inventory.json').read_text())
    rows = list(csv.DictReader((directory / 'port-map.csv').open(encoding='utf-8-sig', newline='')))
    assert len(rows) == len(inv['Ports'])
    assert 'RENDER_BLOCKED=FALSE' in (directory / 'validation-report.txt').read_text()
    assert not any(port['Id'] == 'GigabitEthernet0/0' for port in inv['Ports'])
    for name in ['a4-one-page.png', 'wall-chart.png']:
        with Image.open(directory / name) as image:
            assert image.size == (3508, 2480)
            image.verify()
    pdf = PdfReader(directory / 'a4-booklet.pdf')
    text = '\n'.join(page.extract_text() for page in pdf.pages)
    for port in inv['Ports']:
        assert port['Id'] in text, port['Id']
    if directory.name == 'report-1':
        assert rows[0]['Tagged VLANs'] == expected
        assert ''.join(expected.split()) in ''.join(text.split())
        chart = PdfReader(directory / 'a4-one-page.pdf').pages[0].extract_text()
        assert 'Trunk VLAN policies' in chart
        assert ''.join(expected.split()) in ''.join(chart.split())
print('PASS Cisco actual PDF/PNG/CSV inventory, complete all-except policy and management-port exclusion.')
