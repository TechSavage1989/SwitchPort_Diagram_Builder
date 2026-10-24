import argparse,csv,json,re,subprocess,tempfile
from pathlib import Path
from PIL import Image,ImageDraw
from pypdf import PdfReader

ap=argparse.ArgumentParser();ap.add_argument('--generator',required=True);ap.add_argument('--dotnet');ap.add_argument('--report');args=ap.parse_args()
command=([args.dotnet] if args.dotnet else [])+[str(Path(args.generator).resolve())]
root=Path(tempfile.mkdtemp(prefix='device-report-checks-'));results=[]
logo=root/'client-logo.png';im=Image.new('RGB',(320,100),'white');ImageDraw.Draw(im).text((25,40),'CLIENT LOGO',fill='navy');im.save(logo)
fixed='; JL256A Configuration Editor\nhostname FIXED-REPORT\n'
stack='; Aruba Configuration Editor\nhostname STACK-REPORT\nvsf member 1 type JL558A\nvsf member 2 type JL256A\n'
chassis='; J9851A Configuration Editor\nhostname CHASSIS-REPORT\nmodule A type J9993A\nmodule C type J9986A\nmodule G type J9986A\n'
dense='; Aruba Configuration Editor\nhostname DENSE-REPORT\n'+''.join(f'vsf member {n} type JL558A\n' for n in range(1,9))
def detail(n):return {'Id':str(n),'Name':f'North & East {n}','SerialNumber':f'SN-{n},007','AssetTag':f'ASSET-{n}','Notes':'Rack 2, shelf 4'}
disclaimer='Confirm live link, LLDP/CDP, and MAC address table before moving production cables.'
def run(name,config,details=None,with_logo=False,code=0):
 work=root/name;work.mkdir();source=work/'config.txt';source.write_text(config);out=work/'reports'
 argv=command+['--input',str(source),'--output',str(out)]
 if details is not None:
  identity_file=work/'details.json';identity_file.write_text(json.dumps(details));argv+=['--device-details',str(identity_file)]
 if with_logo:argv+=['--logo',str(logo)]
 result=subprocess.run(argv,capture_output=True,text=True,timeout=90)
 assert result.returncode==code,(name,result.returncode,result.stderr)
 if code:
  assert not list(out.glob('*.pdf')) and not list(out.glob('*.png'))
  assert 'RENDER_BLOCKED=TRUE' in (out/'validation-report.txt').read_text()
  results.append('PASS '+name+': invalid metadata blocks rendering');return
 inv=json.loads((out/'inventory.json').read_text());devices={d['Id']:d for d in inv['Devices']};groups={g['Id']:g for g in inv['Groups']}
 physical_ids={g['Device'] for g in inv['Groups']};module_ids={g['Device']+'/'+g['Slot'].upper() for g in inv['Groups'] if g['Slot']}
 assert set(devices)==physical_ids|module_ids and inv['Schema']=='1.4'
 for group in inv['Groups']:
  if group['Slot']:
   module=devices[group['Device']+'/'+group['Slot'].upper()]
   assert module['Kind']=='Module' and module['ParentId']==group['Device'] and module['GroupId']==group['Id'] and module['Model']==group['Product']
  else:assert devices[group['Device']]['Kind']=='Switch' and not devices[group['Device']]['ParentId']
 expected={d['Id']:d for d in details or []}
 for key,actual in devices.items():
  for field in ['Name','SerialNumber','AssetTag','Notes','ModelSelection']:assert actual[field]==expected.get(key,{}).get(field,''),(name,key,field)
 with (out/'port-map.csv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
 assert len(rows)==len(inv['Ports'])
 for row,port in zip(rows,inv['Ports']):
  group=groups[port['Group']];device_id=group['Device'];assert row['Device ID']==device_id;assert row['Device Model']==devices[device_id]['Model']
  for column,field in [('Device Name','Name'),('Serial Number','SerialNumber'),('Asset Tag','AssetTag'),('Device Details','Notes')]:assert row[column]==devices[device_id][field],(name,port['Id'],column)
  module_id=group['Device']+'/'+group['Slot'].upper() if group['Slot'] else '';assert row['Module ID']==module_id;assert row['Module Model']==devices.get(module_id,{}).get('Model','')
  for column,field in [('Module Name','Name'),('Module Serial Number','SerialNumber'),('Module Asset Tag','AssetTag'),('Module Details','Notes')]:assert row[column]==devices.get(module_id,{}).get(field,''),(name,port['Id'],column)
 booklet=PdfReader(out/'a4-booklet.pdf');single=PdfReader(out/'a4-one-page.pdf')
 assert len(single.pages)==1
 single_text=single.pages[0].extract_text()
 parent_names=list(dict.fromkeys(d['Name'] for d in inv['Devices'] if d['Kind'] in ['Switch','Chassis'] and d['Name']))
 display_name=' / '.join(parent_names) if parent_names else inv['Hostname']
 if name!='dense-maximum-fields':
  for page in booklet.pages:assert display_name in page.extract_text(),(name,'report header uses supplied name or hostname fallback')
 if parent_names:assert inv['Hostname'] not in single_text,(name,'default name must be replaced')
 assert inv['Hostname']==re.search(r'(?m)^(?:hostname|sysname) (.+)$',config).group(1),(name,'original hostname is preserved')
 assert 'Parent:' not in single_text,(name,'no parent fallback in module headings')
 if name=='parent-only-blank-modules':
  assert single_text.count(details[0]['Name'])==1 and single_text.count(details[0]['SerialNumber'])==1,(name,'parent details appear only in report header')
 text='\n'.join(p.extract_text() for p in booklet.pages);compact=re.sub(r'\s+','',text)
 if details:
  assert len(booklet.pages)>1+(len(inv['Ports'])+23)//24
  for device in details:
   for field in ['Name','SerialNumber','AssetTag','Notes','ModelSelection']:
    assert re.sub(r'\s+','',device.get(field,'')) in compact,(name,device['Id'],field,'full booklet value')
  if name not in ['dense-maximum-fields','flexible-module']:
   for device in details:
    assert device.get('Name','') in single_text and device.get('SerialNumber','') in single_text,(name,'diagram identity')
  summary=(out/'technician-summary.txt').read_text()
  assert all(d.get('SerialNumber','') in summary and d.get('ModelSelection','') in summary for d in details)
 else:assert len(booklet.pages)==1+(len(inv['Ports'])+23)//24
 for page in booklet.pages:
  assert disclaimer in page.extract_text() and not re.search(r'hub\s*tech',page.extract_text(),re.I)
  assert abs(float(page.mediabox.width)-841.89)<.5 and abs(float(page.mediabox.height)-595.28)<.5
  assert bool(page.images)==with_logo
 for file in ['wall-chart.png','a4-one-page.png']:
  with Image.open(out/file) as image:assert image.size==(3508,2480)
 assert not inv['Blockers']
 assert len([p for p in out.iterdir() if p.is_file()])==8
 results.append('PASS '+name+': device-name report heading/fallback, no module parent fallback, exact per-device JSON/CSV mappings, full booklet values, summary, logo state and unchanged A4 geometry')

run('no-details',fixed)
run('fixed-named',fixed,[detail(1)])
run('stack-members',stack,[detail(1),detail(2)])
run('chassis-and-modules',chassis,[detail(1),detail('1/A'),detail('1/C'),detail('1/G')],True)
run('parent-only-blank-modules',chassis,[detail(1)])
run('serial-without-name',fixed,[{'Id':'1','SerialNumber':'SERIAL-ONLY','AssetTag':'ASSET-ONLY'}])
run('stack-one-member-named',stack,[detail(2)])
run('module-only',chassis,[detail('1/C')])
run('modules-no-details',chassis)
run('flexible-module','; JL071A Configuration Editor\nhostname FLEX-REPORT\nmodule 1 type JL071A\nmodule A type JL083A\n',[detail(1),detail('1/A')])
run('rear-module','; hpStack_WB Configuration Editor\nhostname REAR-REPORT\nstacking\n member 1 type J9729A\n exit\ninterface 1/B1\n exit\n',[detail(1),detail('1/B')])
long_details=[{'Id':str(n),'Name':f'Dense member {n}','SerialNumber':f'SN-{n}-'+'S'*75,'AssetTag':f'AT-{n}-'+'A'*75,'Notes':'N'*400} for n in range(1,9)]
run('dense-maximum-fields',dense,long_details,True)
run('per-member-model-labels',stack,[{**detail(1),'ModelSelection':'North model'},{**detail(2),'ModelSelection':'South model'}])
run('chassis-model-selection',chassis,[{**detail(1),'ModelSelection':'J9851A'}])
run('model-only',fixed,[{'Id':'1','ModelSelection':'West access model'}])
run('missing-irf-models','version 7.1.070\nsysname MISSING-MODELS\nirf member 1 priority 32\nirf member 2 priority 30\ninterface GigabitEthernet1/0/1\n#\ninterface GigabitEthernet2/0/1\n#\n',[{'Id':'1','ModelSelection':'JG932A'},{'Id':'2','ModelSelection':'JG934A'}])
run('missing-chassis-model','; Aruba Configuration Editor\nhostname MISSING-CHASSIS\nmodule A type J9993A\nmodule G type J9986A\n',[{'Id':'1','ModelSelection':'J9851A'}])
run('maximum-model-label',fixed,[{'Id':'1','ModelSelection':'W'*100}])
run('conflicting-chassis-rejected',chassis,[{'Id':'1','ModelSelection':'J9821A'}],code=3)
run('conflicting-model-rejected',fixed,[{'Id':'1','ModelSelection':'JL558A'}],code=3)
run('unknown-member-rejected',fixed,[detail(2)],code=3)
run('unknown-module-rejected',chassis,[detail('1/Z')],code=3)
run('duplicate-member-rejected',fixed,[detail(1),detail(1)],code=3)
run('oversized-field-rejected',fixed,[{'Id':'1','Name':'X'*81}],code=3)
report='\n'.join(results)+'\n';print(report)
if args.report:Path(args.report).write_text(report)
print('Synthetic verification output:',root)
