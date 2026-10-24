from pathlib import Path
import json,re,csv
from pypdf import PdfReader
from PIL import Image
import argparse
ap=argparse.ArgumentParser();ap.add_argument('--root',required=True);args=ap.parse_args()
root=Path(args.root)
results=json.loads((root/'new-config-results-final.json').read_text())
assert len(results)==14 and all(x['result']==0 for x in results),results
for p in sorted((root/'upload').glob('*.txt')):
 d=root/'supported-new-configs-final'/p.stem
 inv=json.loads((d/'inventory.json').read_text());ports={x['Id'].upper():x for x in inv['Ports']}
 source=p.read_text().replace('\r','')
 def expand(spec):
  result=[]
  for item in spec.replace(' ','').upper().split(','):
   if not item:continue
   if '-' not in item:result.append(item);continue
   a,b=item.split('-');m=re.fullmatch(r'(.*?)(\d+)',a);n=re.fullmatch(r'(.*?)(\d+)',b)
   assert m and n and (not n[1] or n[1]==m[1]),item
   result.extend(m[1]+str(i) for i in range(int(m[2]),int(n[2])+1))
  return result
 aggregates={name.upper():expand(spec) for spec,name in re.findall(r'(?im)^trunk (\S+) (trk\d+) \w+',source)}
 expected={port:{'native':None,'tagged':set(),'agg':''} for port in ports}
 for name,ids in aggregates.items():
  for port in ids:expected[port]['agg']=name
 vlan=None;lines=source.splitlines();i=0
 while i<len(lines):
  line=lines[i].strip();i+=1
  if re.fullmatch(r'vlan \d+',line):vlan=int(line.split()[1]);continue
  if line=='exit':vlan=None;continue
  m=re.fullmatch(r'(no )?(untagged|tagged)(?:\s+(.*))?',line)
  if vlan is None or not m:continue
  spec=m[3] or ''
  while i<len(lines) and re.fullmatch(r'[0-9A-LTRKatrk/,\-]+',lines[i].strip()):spec+=lines[i].strip();i+=1
  for token in expand(spec):
   for port in aggregates.get(token,[token]):
    assert port in expected,(p.name,port)
    state=expected[port]
    if m[2]=='untagged':
     if not m[1]:state['native']=vlan
     elif state['native']==vlan:state['native']=None
    elif m[1]:state['tagged'].discard(vlan)
    else:state['tagged'].add(vlan)
 for name,actual in ports.items():
  state=expected[name];state['tagged'].discard(state['native'])
  assert actual['UntaggedVlan']==state['native'],(p.name,name,'native',actual['UntaggedVlan'],state)
  assert set(actual['TaggedVlans'])==state['tagged'],(p.name,name,'tagged',actual['TaggedVlans'],state)
  assert actual['Aggregate'].upper()==state['agg'],(p.name,name,'aggregate')
 members=re.findall(r'(?im)^\s*member (\d+) type "?(J\w+)',source)
 if members:
  assert all(product=='J9729A' for _,product in members)
  expected_front={f'{member}/{n}' for member,_ in members for n in range(1,49)}
  expected_rear={f'{g["Prefix"]}{n}' for g in inv['Groups'] if g['Rear'] for n in [1,2]}
  assert set(ports)==expected_front|expected_rear
  assert inv['Topology'].startswith('Stack') and all(g['Product']=='' for g in inv['Groups'] if g['Rear'])
  assert all(ports[n]['Media']=='Unspecified' for n in expected_rear)
 else:
  assert len(ports)==216 and len([x for x in ports if x.startswith('G')])==8
  assert all(ports['G'+str(n)]['Media']=='RJ-45' for n in range(1,9))
 pdf=PdfReader(d/'a4-one-page.pdf');assert len(pdf.pages)==1
 assert abs(float(pdf.pages[0].mediabox.width)-841.89)<.5 and abs(float(pdf.pages[0].mediabox.height)-595.28)<.5
 text=pdf.pages[0].extract_text()
 assert inv['Hostname'] in text and 'Confirm live link, LLDP/CDP, and MAC address table before moving production cables.' in text
 assert 'Port notes' not in text and 'Tagged VLAN sets' not in text
 assert all('Member '+member in text for member,_ in members)
 assert all('Rear module '+g['Slot'] in text for g in inv['Groups'] if g['Rear'])
 with Image.open(d/'wall-chart.png') as im:assert im.size==(3508,2480)
 with (d/'port-map.csv').open(encoding='utf-8-sig',newline='') as f:assert len(list(csv.DictReader(f)))==len(ports)
 print('PASS',p.name,':',len(ports),'interfaces, exact VLAN/trunk sets, verified inventory, one A4 page')
print('PASS all 14 supplied configs against an independent source-to-inventory comparison')
