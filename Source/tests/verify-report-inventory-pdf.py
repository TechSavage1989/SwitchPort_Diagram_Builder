"""Verify rendered PDF glyphs contain every VLAN token produced by the inventory fixtures.

First build/run ReportInventoryChecks, then pass that fixture output directory.
Optionally pass --generator and --dotnet for an ordinary full generation run.
"""
import argparse,json,re,subprocess
from pathlib import Path
import pdfplumber
from pypdf import PdfReader

ap=argparse.ArgumentParser();ap.add_argument('fixtures');ap.add_argument('--generator');ap.add_argument('--dotnet');args=ap.parse_args()
root=Path(args.fixtures).resolve();checks=0
disclaimer='Confirm live link, LLDP/CDP, and MAC address table before moving production cables.'
for meta_path in sorted(root.glob('*.json')):
    meta=json.loads(meta_path.read_text());pdf_path=meta_path.with_suffix('.pdf')
    if not pdf_path.exists() or 'ExpectedTagSequence' not in meta:continue
    reader=PdfReader(pdf_path);assert len(reader.pages)==meta['PageCount'],meta['Name']
    actual=[]
    scale=min(805.89/meta['SceneWidth'],559.28/meta['SceneHeight'])
    dx=(841.89-meta['SceneWidth']*scale)/2
    box=meta['TaggedColumn'];crop=(dx+box['Left']*scale,18+box['Top']*scale,dx+box['Right']*scale,18+box['Bottom']*scale)
    with pdfplumber.open(pdf_path) as pdf:
        for page_index,page in enumerate(pdf.pages):
            text=reader.pages[page_index].extract_text()
            assert disclaimer in text,(meta['Name'],page_index,'disclaimer')
            assert bool(reader.pages[page_index].images)==meta['WithLogo'],(meta['Name'],page_index,'logo')
            assert abs(float(reader.pages[page_index].mediabox.width)-841.89)<.5
            assert abs(float(reader.pages[page_index].mediabox.height)-595.28)<.5
            if page_index==0:continue
            assert 'Tagged VLANs' in text,(meta['Name'],page_index,'repeated inventory header')
            tagged_text=page.crop(crop).extract_text(x_tolerance=1,y_tolerance=2) or ''
            assert '...' not in tagged_text and '\u2026' not in tagged_text,(meta['Name'],page_index,'ellipsis')
            assert not re.search(r'\d\s*[-\u2013]\s*\d',tagged_text),(meta['Name'],page_index,'range compaction hid exact IDs')
            actual.extend(map(int,re.findall(r'\b\d+\b',tagged_text)))
            checks+=1
    assert actual==meta['ExpectedTagSequence'],(meta['Name'],'PDF glyph VLAN sequence differs',len(actual),len(meta['ExpectedTagSequence']))
    print(f"PASS actual PDF {meta['Name']}: {len(actual)} exact tagged VLAN tokens, repeated headers, footer, A4 and logo state.")

if args.generator:
    generator=str(Path(args.generator).resolve());out=root/'normal-generation-output'
    assert not out.exists(),'Use a fresh fixture directory for an end-to-end rerun.'
    command=([args.dotnet] if args.dotnet else [])+[generator,'--input',str(root/'normal-25-tagged-vlans.cfg'),'--output',str(out)]
    result=subprocess.run(command,capture_output=True,text=True,timeout=180)
    assert result.returncode==0,('normal 25-tag generation failed',result.stdout,result.stderr)
    inv=json.loads((out/'inventory.json').read_text());first=inv['Ports'][0]
    expected=first['TaggedVlans'];assert len(expected)==25
    actual=[];scale=min(805.89/1190,559.28/842);dx=(841.89-1190*scale)/2
    with pdfplumber.open(out/'a4-booklet.pdf') as pdf:
        for page in pdf.pages[1:]:
            text=page.extract_text() or ''
            if 'Tagged VLANs' not in text:continue
            crop=(dx+645*scale,18+132*scale,dx+875*scale,18+728*scale)
            actual.extend(map(int,re.findall(r'\b\d+\b',page.crop(crop).extract_text(x_tolerance=1,y_tolerance=2) or '')))
    assert actual==expected,('end-to-end table omits/misorders VLANs',actual,expected)
    assert len(list(out.iterdir()))==8
    print('PASS ordinary full generation: 25 tagged VLANs remain fully visible in the exported booklet.')
print(f'PASS {checks} actual inventory PDF pages checked.')
