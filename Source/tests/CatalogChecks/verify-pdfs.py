import csv
import json
from pathlib import Path
import sys
from PIL import Image
from pypdf import PdfReader

root = Path(sys.argv[1])
for name, count in [('3650', 104), ('9300x', 28), ('6200-compact', 16), ('mikrotik', 5)]:
    directory = root / ('report-' + name)
    inventory = json.loads((directory / 'inventory.json').read_text())
    rows = list(csv.DictReader((directory / 'port-map.csv').open(encoding='utf-8-sig', newline='')))
    assert not inventory['Blockers']
    assert len(rows) == len(inventory['Ports']) == count
    assert 'RENDER_BLOCKED=FALSE' in (directory / 'validation-report.txt').read_text()
    reader = PdfReader(directory / 'a4-booklet.pdf')
    text = ''.join(''.join(page.extract_text().split()) for page in reader.pages)
    for port in inventory['Ports']:
        assert ''.join(port['Id'].split()) in text, (name, port['Id'])
    for file in ['wall-chart.png', 'a4-one-page.png']:
        with Image.open(directory / file) as image:
            assert image.size == (3508, 2480)
            image.verify()
    if name == '3650':
        assert 'WS-C3650-48FD-L' in text
        assert all(row['Device Model'] == 'WS-C3650-48FD-L' for row in rows)
        assert 'Independentnotes1' in text and 'Independentnotes2' in text
        dense = next(row for row in rows if row['Port'] == 'TenGigabitEthernet2/1/4')
        assert dense['Trunk/LAG'] == 'Po7'
    elif name == '9300x':
        assert 'C9300X-NM-4C' in text
        dense = next(row for row in rows if row['Port'] == 'HundredGigE1/1/4')
    elif name == '6200-compact':
        dense = next(row for row in rows if row['Port'] == '1/1/15')
    if name != 'mikrotik':
        assert dense['Tagged VLANs'] == ';'.join(map(str, range(20, 60)))
        assert ','.join(map(str, range(20, 60))) in text
    print('PASS actual catalog PDF/PNG/CSV:', name, count, 'ports;', len(reader.pages), 'pages')
