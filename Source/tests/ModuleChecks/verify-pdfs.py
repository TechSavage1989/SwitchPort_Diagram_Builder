import csv
import json
from pathlib import Path
import re
import sys

from PIL import Image
from pypdf import PdfReader

root = Path(sys.argv[1])


def compact(value):
    return ''.join(value.split())


for name in ['cisco', 'hpe', '9300', '3850-blank']:
    directory = root / ('report-' + name)
    expected = json.loads((root / (name + '-expectations.json')).read_text())
    inventory = json.loads((directory / 'inventory.json').read_text())
    rows = list(csv.DictReader((directory / 'port-map.csv').open(encoding='utf-8-sig', newline='')))
    assert len(rows) == len(inventory['Ports']) == len(expected)
    csv_ports = {row['Port']: row for row in rows}
    assert len(csv_ports) == len(rows)
    assert 'RENDER_BLOCKED=FALSE' in (directory / 'validation-report.txt').read_text()
    for filename in ['a4-one-page.png', 'wall-chart.png']:
        with Image.open(directory / filename) as image:
            assert image.size == (3508, 2480)
            image.verify()
    pdf = PdfReader(directory / 'a4-booklet.pdf')
    assert len(pdf.pages) > 1
    chart_text = PdfReader(directory / 'a4-one-page.pdf').pages[0].extract_text().lower()
    assert 'uplink module unconfirmed' not in chart_text, (name, 'all selected modules are known')
    if name in {'9300', '3850-blank'}:
        assert 'twingig' not in chart_text and 'x2 /' not in chart_text, (name, 'family-specific alternatives label')
    body = '\n'.join(page.extract_text() for page in pdf.pages[1:])
    for port in expected:
        row = csv_ports[port['Id']]
        assert row['Tagged VLANs'] == port['Tagged'].replace(', ', ';'), (name, port['Id'], 'complete CSV tagged VLANs')
        assert row['Media'] == port['Media'], (name, port['Id'], 'selected media')
        assert row['Voice VLAN'] == (str(port['VoiceVlan']) if port['VoiceVlan'] is not None else '')
        assert row['Trunk/LAG'] == port['Aggregate']
        assert row['Description'] == port['Description']
        basis = row['Inventory basis'].lower()
        assert ('configured' in basis) if port['Configured'] else ('confirmed' in basis and 'physical' in basis)
        # Locate the actual inventory row before checking its settings. The
        # selected and inactive interfaces deliberately have different values.
        found = re.search(re.escape(port['Id']) + r'(?![\d/])', body)
        assert found, (name, port['Id'], 'PDF interface identity')
        start = found.start()
        following = [
            candidate.start()
            for other in expected if other['Id'] != port['Id']
            if (candidate := re.search(re.escape(other['Id']) + r'(?![\d/])', body[found.end():]))
        ]
        end = found.end() + min(following) if following else len(body)
        pdf_row = body[start:end]
        if port['Description']:
            assert compact(port['Description']) in compact(pdf_row), (name, port['Id'], 'PDF description')
        if port['Tagged']:
            assert compact(port['Tagged']) in compact(pdf_row), (name, port['Id'], 'complete PDF tagged VLANs', pdf_row)
        if port['Aggregate']:
            assert port['Aggregate'] in pdf_row, (name, port['Id'], 'PDF LAG')
    selected = inventory['Devices'][0]['ModuleSelections']
    for entry in selected:
        assert entry['Sku'] in body, (name, entry['Slot'], 'module choice in PDF device details')
    if name == 'cisco':
        assert {entry['Slot']: entry['Sku'] for entry in selected} == {'X2-1': 'CVR-X2-SFP', 'X2-2': 'X2-10GB-SR'}
        assert sum(port['Configured'] for port in expected) == 3
        assert len(expected) == 54
        assert csv_ports['GigabitEthernet0/49']['Media'] == 'SFP'
        assert csv_ports['TenGigabitEthernet0/2']['Media'] == 'X2'
        assert csv_ports['GigabitEthernet0/49']['Tagged VLANs'] == ';'.join(str(101 + 2 * n) for n in range(40))
    elif name == 'hpe':
        assert {entry['Slot']: entry['Sku'] for entry in selected} == {'A': 'J9986A', 'B': 'J9993A'}
        assert len(expected) == 32
        assert not any(port['Configured'] for port in expected)
    elif name == '9300':
        assert {entry['Slot']: entry['Sku'] for entry in selected} == {'NM': 'C9300-NM-2Y'}
        assert len(expected) == 51 and sum(port['Configured'] for port in expected) == 1
        assert csv_ports['TwentyFiveGigE1/1/1']['Media'] == 'SFP28'
        assert csv_ports['TwentyFiveGigE1/1/1']['Tagged VLANs'] == ';'.join(str(101 + 2 * n) for n in range(40))
        assert 'configured' in csv_ports['TenGigabitEthernet1/1/1']['Inventory basis'].lower()
    else:
        assert {entry['Slot']: entry['Sku'] for entry in selected} == {'NM': 'C3850-NM-BLANK'}
        assert len(expected) == 50 and sum(port['Configured'] for port in expected) == 2

print('PASS selected-module actual PDF/PNG/CSV exports, complete tagged VLANs, selected connector media and preserved inactive-interface settings.')
