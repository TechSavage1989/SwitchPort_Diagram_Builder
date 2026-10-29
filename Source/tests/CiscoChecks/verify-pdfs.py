import csv
import json
from pathlib import Path
import re
import sys
from PIL import Image
from pypdf import PdfReader

root = Path(sys.argv[1])
expected = (root / 'expected-policy.txt').read_text()
expected_3560 = json.loads((root / 'expected-3560.json').read_text())
def compact(value):
    return ''.join(value.split())

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
    if directory.name in {'report-2', 'report-3'}:
        groups = {group['Id']: group for group in inv['Groups']}
        configured = [port for port in inv['Ports'] if groups[port['Group']].get('ConfiguredInterfaces', False)]
        fixed = [port for port in inv['Ports'] if not groups[port['Group']].get('ConfiguredInterfaces', False)]
        assert len(configured) == 6 and len(fixed) == 48 and len(rows) == 54
        assert {port['Id'] for port in configured} == {
            'GigabitEthernet0/49', 'GigabitEthernet0/50',
            'GigabitEthernet0/51', 'GigabitEthernet0/52',
            'TenGigabitEthernet0/1', 'TenGigabitEthernet0/2',
        }
        assert all(port['Media'] == 'Unspecified' for port in configured)
        csv_ports = {row['Port']: row for row in rows}
        json_ports = {port['Id']: port for port in inv['Ports']}
        body_text = '\n'.join(page.extract_text() for page in pdf.pages[1:])
        for expectation in expected_3560:
            port = json_ports[expectation['Port']]
            row = csv_ports[expectation['Port']]
            assert port['UntaggedVlan'] == expectation['Native']
            assert port['TaggedVlans'] == expectation['Tagged']
            assert port['VoiceVlan'] == expectation['Voice']
            assert port['Aggregate'] == expectation['Aggregate']
            assert port['Disabled'] == expectation['Disabled']
            assert port['Description'] == expectation['Description']
            assert row['Untagged VLAN'] == str(expectation['Native'])
            assert row['Tagged VLANs'] == ';'.join(map(str, expectation['Tagged']))
            assert row['Voice VLAN'] == (str(expectation['Voice']) if expectation['Voice'] is not None else '')
            assert row['Trunk/LAG'] == expectation['Aggregate']
            assert row['Description'] == expectation['Description']
            assert row['Admin State'] == ('Unknown' if expectation['Disabled'] is None else 'Disabled' if expectation['Disabled'] else 'Enabled')
            basis = row['Inventory basis'].lower()
            assert ('configured' in basis and 'unconfirmed' in basis) if expectation['Configured'] else ('confirmed' in basis and 'physical' in basis)
            # Verify the actual PDF row, not just exported JSON/CSV. Full IDs
            # identify rows so a neighboring interface cannot satisfy the check.
            start = body_text.index(expectation['Port'])
            following = [body_text.find(other, start + len(expectation['Port'])) for other in json_ports if other != expectation['Port']]
            end = min((position for position in following if position >= 0), default=len(body_text))
            pdf_row = body_text[start:end]
            assert compact(expectation['Description']) in compact(pdf_row), (directory.name, expectation['Port'], 'description')
            assert str(expectation['Native']) in pdf_row
            if expectation['Tagged']:
                assert compact(', '.join(map(str, expectation['Tagged']))) in compact(pdf_row), (directory.name, expectation['Port'], 'tagged VLANs')
            if expectation['Aggregate']:
                assert expectation['Aggregate'] in pdf_row
        validation = (directory / 'validation-report.txt').read_text().lower()
        for statement in [
            'inventory basis: configured interface view',
            'confirmed physical ports: 48',
            'configured uplink interfaces: 6',
            'installed uplink socket count: unconfirmed',
            'expected interface count: 54',
            'generated interface count: 54',
            'csv data row count: 54',
        ]:
            assert statement in validation, (directory.name, statement)
        assert not re.search(r'(?:expected|generated) physical count:\s*54\b', validation)
        chart = PdfReader(directory / 'a4-one-page.pdf').pages[0].extract_text().lower()
        assert 'configured' in chart and 'unconfirmed' in chart
        assert '48' in chart and '6' in chart
        assert 'configured interface inventory' in body_text.lower()
        for page in pdf.pages[1:]:
            page_text = page.extract_text().lower()
            assert 'configured' in page_text and 'unconfirmed' in page_text
        if directory.name == 'report-2':
            assert not any(group['Product'] == 'WS-C3560E-48PD-SF' for group in inv['Groups'])
        else:
            assert all(group['Product'] == 'WS-C3560E-48PD-SF' for group in inv['Groups'] if not group.get('ConfiguredInterfaces', False))
        assert all(group['Product'] == '' for group in inv['Groups'] if group.get('ConfiguredInterfaces', False))
print('PASS Cisco actual PDF/PNG/CSV inventories, complete all-except policy, independent qualified 3560-E uplinks and management-port exclusion.')
