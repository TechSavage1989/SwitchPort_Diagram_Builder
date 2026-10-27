"""Inspect actual multi-switch PDF text, page plans, and matching A4 raster outputs."""
import json
import sys
from pathlib import Path
from PIL import Image, ImageChops
from pypdf import PdfReader

root = Path(sys.argv[1])
for prefix in ['two-on-one', 'five-ordered', 'mixed-vendors', 'dense-fallback', 'branded-sheet']:
    plan = json.loads((root / (prefix + '-plan.json')).read_text())
    pdf = PdfReader(root / (prefix + '.pdf'))
    assert len(pdf.pages) == plan['PageCount']
    assert [item['PageNumber'] for item in plan['Placements']] == sorted(item['PageNumber'] for item in plan['Placements'])
    if prefix == 'two-on-one':
        assert len(pdf.pages) == 1
        text = pdf.pages[0].extract_text()
        assert 'ALPHA-ACCESS' in text and 'BETA-ACCESS' in text
        assert text.index('ALPHA-ACCESS') < text.index('BETA-ACCESS')
        assert text.count('VLAN key') == 2
    if prefix == 'five-ordered':
        assert 1 < len(pdf.pages) < 5
        text = '\n'.join(page.extract_text() for page in pdf.pages)
        assert [text.index('ORDERED-' + str(i)) for i in range(1, 6)] == sorted(text.index('ORDERED-' + str(i)) for i in range(1, 6))
    if prefix == 'mixed-vendors':
        assert len(pdf.pages) == 1 and 'MIXED-ARUBA' in pdf.pages[0].extract_text() and 'MIXED-ROUTEROS' in pdf.pages[0].extract_text()
    for index, page in enumerate(pdf.pages, 1):
        assert abs(float(page.mediabox.width) - 841.89) < 0.5
        assert abs(float(page.mediabox.height) - 595.28) < 0.5
        assert 'Confirm live link, LLDP/CDP, and MAC address table' in page.extract_text()
        with Image.open(root / f'{prefix}-{index}.png') as image:
            assert image.size == (3508, 2480)
            assert abs(image.info['dpi'][0] - 300) < 0.1
            assert ImageChops.difference(image.convert('RGB'), Image.new('RGB', image.size, 'white')).getbbox()
            # Each planned component includes an unchanged dark header at its exact
            # vector Y position. Check the raster shares those same vertical blocks.
            sx = 3508 / 841.89
            sy = 2480 / 595.28
            for item in plan['Placements']:
                if item['PageNumber'] != index:
                    continue
                y = round((18 + item['Y'] + 2) * sy)
                x = round((18 + 2) * sx)
                pixel = image.convert('RGB').getpixel((x, y))
                assert max(pixel) < 110, (prefix, item, pixel)
print('PASS multi-switch same-page PDF content/order, capacity pagination, matched raster component placement, A4 and 300 DPI')
