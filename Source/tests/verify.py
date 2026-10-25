import argparse,csv,hashlib,json,os,re,subprocess,tempfile
from pathlib import Path
from PIL import Image
from pypdf import PdfReader

ap=argparse.ArgumentParser();ap.add_argument('--generator',required=True);ap.add_argument('--dotnet');ap.add_argument('--host',action='store_true');ap.add_argument('--report');ap.add_argument('--instant-on-input-dir');ap.add_argument('--instant-on-1960-input');args=ap.parse_args()
command=([args.dotnet] if args.dotnet else [])+[str(Path(args.generator).resolve())]+(['--generate'] if args.host else [])
checks=[]
root=Path(tempfile.mkdtemp(prefix='Switchport verification with spaces '))
def run(name,text,code=0,models=None,model_name=None):
    p=root/name;p.mkdir();source=p/'config with spaces.txt';source.write_text(text);out=p/'output with spaces'
    result=subprocess.run(command+['--input',str(source),'--output',str(out)]+(['--models',models] if models else [])+(['--model-name',model_name] if model_name else []),capture_output=True,text=True,timeout=90)
    assert result.returncode==code,(name,result.returncode,result.stderr)
    if code:
        assert not list(out.glob('*.png')) and not list(out.glob('*.pdf')),name
        assert 'RENDER_BLOCKED=TRUE' in (out/'validation-report.txt').read_text(),name
    else:
        expected={'wall-chart.png','a4-one-page.png','a4-one-page.pdf','a4-booklet.pdf','port-map.csv','technician-summary.txt','validation-report.txt','inventory.json'}
        assert {p.name for p in out.iterdir()}==expected
        inv=json.loads((out/'inventory.json').read_text())
        with (out/'port-map.csv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
        assert [r['Port'] for r in rows]==[p['Id'] for p in inv['Ports']]
        fingerprint=hashlib.sha256((out/'inventory.json').read_bytes()).hexdigest()
        assert fingerprint in (out/'validation-report.txt').read_text()
        assert 'RENDER_BLOCKED=FALSE' in (out/'validation-report.txt').read_text()
        for namepng in ['wall-chart.png','a4-one-page.png']:
            with Image.open(out/namepng) as image:image.verify()
        for raster in ['wall-chart.png','a4-one-page.png']:
            with Image.open(out/raster) as image:
                assert image.size==(3508,2480)
                assert abs(image.info['dpi'][0]-300)<0.1 and abs(image.info['dpi'][1]-300)<0.1
        pdf=PdfReader(out/'a4-one-page.pdf');assert len(pdf.pages)==1
        page_text=pdf.pages[0].extract_text()
        disclaimer='Confirm live link, LLDP/CDP, and MAC address table before moving production cables.'
        for file in ('a4-one-page.pdf','a4-booklet.pdf'):
            assert all(disclaimer in page.extract_text() for page in PdfReader(out/file).pages)
        for file in ('technician-summary.txt','validation-report.txt'):
            assert disclaimer in (out/file).read_text()
        assert all(label in page_text for label in ['Native VLAN','Tagged VLAN','Voice VLAN','LAG / trunk'])
        assert 'Fill: native VLAN' not in page_text
        assert 'Tagged VLAN sets' not in pdf.pages[0].extract_text()
        assert abs(float(pdf.pages[0].mediabox.width)-841.89)<0.5
        assert len(PdfReader(out/'a4-booklet.pdf').pages)>=1+(len(inv['Ports'])+23)//24
    checks.append('PASS '+name)
    return out,json.loads((out/'inventory.json').read_text())

models={'JL558A':52,'JL256A':52,'JL661A':52,'JL662A':28,'J9536A':22,'J9538A':8,'J9993A':8,'J9990A':24,'J9534A':24,'J9986A':24,'J9987A':24}
for model,count in models.items():
    iscx=model in ['JL661A','JL662A']
    config=f'!Version FL.10.13.1000\n! {model}\nhostname LAB-{model}\n' if iscx else f'; {model} Configuration Editor; Created on release WC.16.11.0010\nhostname "LAB-{model}"\n'
    out,inv=run('model-'+model,config);assert len(inv['Ports'])==count
    assert len({p['Id'] for p in inv['Ports']})==count

sample='''; JL256A Configuration Editor; Created on release WC.16.11.0010
hostname "Port Test"
password manager plaintext DO_NOT_EXPORT_76
snmp-server community DO_NOT_EXPORT_84 unrestricted
trunk 49-50 trk1 lacp
interface 1
 name "Workstation"
 disable
 exit
vlan 10
 name "Data"
 untagged 1-48
 tagged trk1
 ip address 192.0.2.1 255.255.255.0
 exit
vlan 150
 name "Voice"
 tagged 1-48,trk1
 voice
 exit
ip route 0.0.0.0 0.0.0.0 192.0.2.254
Local Port : 49
System Name : CoreSwitch
'''
out,inv=run('aos-vlans-lag-credentials-neighbor',sample)
p=inv['Ports'][0];assert p['UntaggedVlan']==10 and p['VoiceVlan']==150 and p['Disabled'] is True
assert inv['Ports'][48]['Aggregate']=='trk1' and inv['Ports'][48]['ConnectedDevice']=='CoreSwitch'
for f in out.iterdir():assert b'DO_NOT_EXPORT' not in f.read_bytes(),f

cx='''!Version FL.10.13.1000
! JL661A
hostname CX-TEST
interface lag 1
 no shutdown
 no routing
 vlan trunk native 10
 vlan trunk allowed 10,150
 exit
interface 1/1/49
 lag 1
 exit
interface 1/1/50
 lag 1
 exit
interface 1/1/1
 description "Front Desk"
 no shutdown
 no routing
 vlan access 10
 exit
interface vlan 10
 ip address 192.0.2.2/24
 exit
'''
out,inv=run('cx-native-lag-svi',cx)
assert inv['Ports'][48]['UntaggedVlan']==10 and inv['Ports'][48]['TaggedVlans']==[150]
assert inv['SVIs'][0]['Address']=='192.0.2.2/24'

# PL firmware is shared by the 6000 and 6100 families. A complete inventory
# establishes their common geometry without making a SKU or SFP+ claim.
pl_header='!Version ArubaOS-CX PL.10.10.1070\n'
pl_ports=''.join(f'interface 1/1/{n}\n' for n in range(1,53))
out,inv=run('pl-shared-hostname-independent',pl_header+'hostname FireStation-LAB\nvlan 5\n name Voice\n voice\n'+pl_ports+' description ZETRON 6300\n vlan trunk native 1\n vlan trunk allowed 1,5\n')
assert inv['Groups'][0]['Product']=='CX6000-6100-48G-4OPT' and len(inv['Ports'])==52
assert inv['Groups'][0]['OpticalSide']=='left' and inv['Groups'][0]['OpticalType']=='SFP/SFP+'
assert inv['Ports'][-1]['VoiceVlan']==5 and inv['Ports'][-1]['Description']=='ZETRON 6300'
assert not any(name in PdfReader(out/'a4-one-page.pdf').pages[0].extract_text() for name in ['6000','6100','CX6000-6100-48G-4OPT'])
out,inv=run('pl-manual-model-name',pl_header+'hostname Client-Access\n'+pl_ports,model_name='Aruba CX 6100')
assert inv['ReportModel']=='Aruba CX 6100' and inv['Hostname']=='Client-Access' and len(inv['Ports'])==52
assert all(label in PdfReader(out/'a4-one-page.pdf').pages[0].extract_text() for label in ['Aruba CX 6100','Client-Access'])
out,inv=run('model-name-does-not-establish-hardware',pl_header+'interface 1/1/1\n',3,model_name='Aruba CX 6100')
out,inv=run('pl-shared-no-hostname',pl_header+pl_ports)
assert inv['Hostname']=='Unknown' and inv['Groups'][0]['Product']=='CX6000-6100-48G-4OPT'
out,inv=run('pl-explicit-sku-precedence',pl_header+'! JL675A\n'+pl_ports)
assert inv['Groups'][0]['Product']=='JL675A' and inv['Ports'][-1]['Media']=='SFP+'
assert 'JL675A' in PdfReader(out/'a4-one-page.pdf').pages[0].extract_text()
for name,config in [
    ('partial-inventory',pl_header+'hostname TEST-6100\ninterface 1/1/1\n'),
    ('non-pl-firmware',pl_header.replace('PL.','ML.')+pl_ports),
    ('extra-interface',pl_header+pl_ports+'interface 1/1/53\n'),
    ('extra-member',pl_header+pl_ports+'interface 2/1/1\n'),
    ('stack-without-types',pl_header+'vsf member 1\n'+pl_ports),
    ('unsupported-product',pl_header+'! R8N99A\n'+pl_ports),
    ('conflicting-products',pl_header+'! JL675A JL661A\n'+pl_ports),
    ('duplicate-header',pl_header+pl_ports+'interface 1/1/1\n'),
    ('hostname-alone',pl_header+'hostname TEST-6100\n'),
    ('unsupported-model-declaration',pl_header+'model UNKNOWN\n'+pl_ports),
    ('range-is-not-full-inventory',pl_header+'interface 1/1/1-1/1/52\n'),
    ('vsx-without-identity',pl_header+'vsx\n'+pl_ports),
]:
    out,inv=run('pl-reject-'+name,config,3)
    assert not inv['Ports'] and not any('outside the validated model inventory' in b for b in inv['Blockers'])

stack='''; Aruba Configuration Editor; Created on release WC.16.11.0010
hostname STACK
vsf
 member 1
  type "JL256A"
 member 2
  type "JL256A"
 exit
vlan 10
 untagged 1/1-1/48,2/1-2/48
 tagged 1/49-1/52,2/49-2/52
 exit
'''
out,inv=run('two-member-stack',stack);assert len(inv['Ports'])==104 and inv['Ports'][52]['Id']=='2/1'
modular='''; HPE Configuration Editor; Created on release KB.16.11.0010
hostname MODULAR
module A type J9990A
module B type J9538A
vlan 1
 untagged A1-A20
 tagged A21-A24,B1-B8
 exit
'''
out,inv=run('modular-optical-rows',modular);assert len(inv['Ports'])==32
stacklinks='; Aruba Configuration Editor\nhostname STACKLINKS\nvsf member 1 type JL256A\nvsf member 2 type JL256A\nvsf member 1 link 1 49-50\nvsf member 2 link 1 2/49-2/50\n'
out,inv=run('vsf-black-link-port-detection',stacklinks)
assert sum(p['StackLink'] for p in inv['Ports'])==4
with Image.open(out/'wall-chart.png') as im:assert sum(count for count,color in im.convert('RGB').getcolors(im.width*im.height) if color==(0,0,0))>1000
cxstack='!Version FL.10.13.1000\nhostname CXSTACK\nvsf member 1\n type JL661A\n link 1 1/1/49\n link 2 1/1/50\n exit\n'
out,inv=run('cx-vsf-black-link-port-detection',cxstack)
assert sum(p['StackLink'] for p in inv['Ports'])==2
with Image.open(out/'wall-chart.png') as im:assert sum(count for count,color in im.convert('RGB').getcolors(im.width*im.height) if color==(0,0,0))>1000
run('unsupported-vendor','config system global\n set hostname Fortigate\nend\n',3)
run('unknown-product','; JL999A Configuration Editor\nhostname unknown\n',3)
run('invalid-port','; J9536A Configuration Editor\ninterface 23\n name invalid\n exit\n',3)
run('contradictory-native','; JL256A Configuration Editor\nvlan 10\n untagged 1\n exit\nvlan 20\n untagged 1\n exit\n',3)
run('unrecognized-vlan','!Version FL.10.13.1000\n! JL661A\ninterface 1/1/1\n vlan access banana\n exit\n',3)
run('description-is-not-model','; Configuration Editor\nhostname Unknown\ninterface 1\n name JL256A\n exit\n',3)
p=root/'missing input';p.mkdir();result=subprocess.run(command+['--input',str(p/'absent.txt'),'--output',str(p/'out')],capture_output=True);assert result.returncode==3;checks.append('PASS missing-input')
result=subprocess.run(command+['--input',str(root/'not-needed'),'--output',str(out)],capture_output=True);assert result.returncode==2;assert (out/'wall-chart.png').exists();checks.append('PASS existing-output-protected')
result=subprocess.run(command,capture_output=True);assert result.returncode==2;checks.append('PASS missing-arguments')
def named(name,text,expected):
    case=root/name;case.mkdir();config=case/'input.txt';config.write_text(text);parent=case/'chosen parent with spaces'
    for suffix in ['', ' (2)']:
        result=subprocess.run(command+['--input',str(config),'--output',str(parent),'--named-folder'],capture_output=True,text=True,timeout=90)
        assert result.returncode==0,(name,result.stderr)
        destination=parent/(expected+suffix)
        assert destination.is_dir() and len(list(destination.iterdir()))==8
        assert 'OUTPUT_DIRECTORY='+str(destination) in result.stdout
        assert (parent/expected/'a4-one-page.pdf').exists()
    checks.append('PASS '+name+' and repeat export protection')
named('hostname-folder','; JL256A Configuration Editor\nhostname "CORE-MDF-001"\n','CORE-MDF-001')
named('model-folder-fallback','; JL256A Configuration Editor\n','JL256A')
named('safe-hostname-folder','; JL256A Configuration Editor\nhostname "CORE/1:WEST"\n','CORE_1_WEST')
for sku,slots in [('J9821A',6),('J9822A',12),('J8697A',6),('J8698A',12),('J8715B',12),('J9475A',6)]:
    last=chr(ord('A')+slots-1)
    run('chassis-'+sku,f'; {sku} Configuration Editor\nhostname CHASSIS\nmodule A type J9986A\nmodule B type J9538A\nmodule {last} type J8705A\nvlan 10\n name Data\n untagged A1-A24\n exit\n')
for sku in ['JL071A','JL072A','JL073A','JL074A','JL075A','JL076A']:
    run('flex-'+sku,f'; {sku} Configuration Editor\nhostname FLEX\nmodule 1 type {sku}\nmodule A type JL083A\n'+('module B type JL079A\n' if sku=='JL075A' else ''))
run('slot-outside-chassis','; J9821A Configuration Editor\nmodule G type J9986A\n',3)
instant='config-file-header\nTEST\nvInstantOn_1930_3.3.2.0 (5)\nSKU Description "Instant On 1930 JL686B"\n!\n'
basic=instant+'vlan database\nvlan 10,40,50-52\nexit\nvoice vlan id 40\nvoice vlan state oui-enabled\ninterface vlan 40\n name "Voice Services"\n!\ninterface 1\n switchport general allowed vlan add 10 untagged\n switchport general pvid 10\n switchport general allowed vlan add 40,50-52 tagged\n switchport general allowed vlan remove 51 tagged\n!\n'
out,inv=run('instanton1930-basic',basic)
assert len(inv['Ports'])==52 and inv['Ports'][0]['UntaggedVlan']==10 and inv['Ports'][0]['TaggedVlans']==[40,50,52]
assert inv['Ports'][0]['VoiceVlan']==40 and inv['Ports'][48]['Media']=='SFP+' and inv['Firmware']=='3.3.2.0'
out,inv=run('instanton-pvid-only',instant+'interface 1\n switchport general pvid 10\n!\n')
assert inv['Ports'][0]['UntaggedVlan'] is None and 'Configured ingress PVID: 10' in inv['Ports'][0]['Notes']
out,inv=run('instanton-voice-disabled',basic.replace('oui-enabled','disabled'))
assert inv['Ports'][0]['VoiceVlan'] is None
run('instanton-unsupported-family',instant.replace('_1930_','_1830_'),3)
run('instanton-missing-sku',instant.replace('JL686B','Unknown'),3)
run('instanton-invalid-port',instant+'interface 53\n switchport general pvid 10\n!\n',3)
run('instanton-multiple-untagged',instant+'interface 1\n switchport general allowed vlan add 10,20 untagged\n!\n',3)
run('instanton-pvid-mismatch',instant+'interface 1\n switchport general allowed vlan add 10 untagged\n switchport general pvid 20\n!\n',3)
run('instanton-unknown-switchport',instant+'interface 1\n switchport access vlan 10\n!\n',3)
run('instanton-unvalidated-aggregation',instant+'interface 1\n channel-group 1 mode auto\n!\n',3)
if args.instant_on_input_dir:
    files=sorted(Path(args.instant_on_input_dir).glob('running-config_*.txt'))
    assert len(files)==3,'Expected the three supplied Instant On input files'
    for index,source in enumerate(files,1):
        text=source.read_text();out,inv=run('instanton-supplied-'+str(index),text)
        assert inv['OS']=='Instant On 1930' and inv['Groups'][0]['Product']=='JL686B' and len(inv['Ports'])==52
        expected={};names={}
        for block in re.split(r'(?m)^!\s*$',text):
            match=re.search(r'(?m)^interface (\d+)\s*$',block)
            if match:
                native_ids=[];tagged_ids=[]
                for ids,kind in re.findall(r'switchport general allowed vlan add ([\d,]+) (tagged|untagged)',block):
                    (tagged_ids if kind=='tagged' else native_ids).extend(map(int,ids.split(',')))
                pv=re.search(r'switchport general pvid (\d+)',block)
                expected[match[1]]=(sorted(set(native_ids)),sorted(set(tagged_ids)),int(pv[1]) if pv else None)
            vm=re.search(r'(?m)^interface vlan (\d+)\s*\n\s+name (.+)',block)
            if vm:names[int(vm[1])]=vm[2].strip().strip('"')
        for port in inv['Ports']:
            natives,tags,pvid=expected.get(port['Id'],([],[],None))
            assert port['UntaggedVlan']==(natives[0] if natives else None)
            assert port['TaggedVlans']==tags
            assert port['VoiceVlan']==(40 if 40 in natives+tags else None)
            if pvid:assert 'Configured ingress PVID: '+str(pvid) in port['Notes']
        assert {v['Id']:v['Name'] for v in inv['Vlans'] if v['Name']}==names
        payloads=[line.strip().encode() for line in text.splitlines() if re.fullmatch(r'[A-Za-z0-9+/=]{40,}',line.strip())]
        for file in [out/'inventory.json',out/'port-map.csv',out/'technician-summary.txt']:
            data=file.read_bytes();assert all(payload not in data for payload in payloads)
instant1960=instant.replace('_1930_','_1960_').replace('1930 JL686B','1960 JL805A')
out,inv=run('instanton1960-standalone',instant1960+'interface 16\n switchport general allowed vlan add 77 untagged\n switchport general pvid 77\n!\n')
assert len(inv['Ports'])==16 and inv['Ports'][11]['Media']=='RJ-45' and inv['Ports'][12]['Media']=='SFP+'
two1960=instant1960+'unit-type unit 1 network te uplink none\nunit-type unit 2 network te uplink none\n!\ninterface 1/16\n channel-group 1 mode auto\n!\ninterface 2/16\n channel-group 1 mode auto\n!\ninterface TRK1\n switchport general allowed vlan add 77 untagged\n switchport general allowed vlan add 232,250-252 tagged\n!\nvoice vlan id 232\nvoice vlan state oui-enabled\n'
out,inv=run('instanton1960-cross-member-trk',two1960)
members=[p for p in inv['Ports'] if p['Aggregate']=='TRK1']
assert len(inv['Ports'])==32 and len(members)==2 and all(p['UntaggedVlan']==77 and p['TaggedVlans']==[232,250,251,252] and p['VoiceVlan']==232 for p in members)
run('instanton1960-ambiguous-member',two1960.replace('unit 2 network te uplink none','unit 2 network gi uplink te'),3)
run('instanton1960-undeclared-member',two1960.replace('interface 2/16','interface 3/16'),3)
run('instanton1960-missing-trk',two1960[:two1960.index('interface TRK1')],3)
run('instanton1960-orphan-trk',instant1960+'interface TRK1\n switchport general allowed vlan add 1 untagged\n!\n',3)
run('instanton1960-conflicting-modes',two1960.replace('interface 2/16\n channel-group 1 mode auto','interface 2/16\n channel-group 1 mode on'),3)
run('instanton1960-unvalidated-sku',instant1960.replace('JL805A','JL810A'),3)
if args.instant_on_1960_input:
    text=Path(args.instant_on_1960_input).read_text();out,inv=run('instanton1960-supplied-stack',text)
    assert inv['OS']=='Instant On 1960' and inv['Firmware']=='2.7.0.0'
    assert len(inv['Groups'])==4 and len(inv['Ports'])==64 and all(g['Product']=='JL805A' for g in inv['Groups'])
    expected={};names={}
    def vidlist(spec):
        values=[]
        for token in spec.split(','):
            limits=list(map(int,token.split('-')));values.extend(range(limits[0],limits[-1]+1))
        return values
    for block in re.split(r'(?m)^!\s*$',text):
        match=re.search(r'(?m)^interface (\d+/\d+|TRK\d+)\s*$',block)
        if match:
            natives=[];tags=[]
            for spec,kind in re.findall(r'switchport general allowed vlan add ([\d,\-]+) (tagged|untagged)',block):
                (tags if kind=='tagged' else natives).extend(vidlist(spec))
            lag=re.search(r'channel-group (\d+) mode (\w+)',block);pv=re.search(r'switchport general pvid (\d+)',block)
            expected[match[1]]={'native':sorted(set(natives)),'tags':sorted(set(tags)),'lag':'TRK'+lag[1] if lag else '', 'pvid':int(pv[1]) if pv else None}
        vm=re.search(r'(?m)^interface vlan (\d+)\s*\n\s+name (.+)',block)
        if vm:names[int(vm[1])]=vm[2].strip().strip('"')
    for port in inv['Ports']:
        e=expected.get(port['Id'],{'native':[],'tags':[],'lag':'','pvid':None})
        assert port['Aggregate']==e['lag']
        effective=expected[e['lag']] if e['lag'] else e
        assert port['UntaggedVlan']==(effective['native'][0] if effective['native'] else None)
        assert port['TaggedVlans']==effective['tags']
        assert port['VoiceVlan']==(232 if 232 in effective['native']+effective['tags'] else None)
        assert not port['StackLink']
        if effective['pvid']:assert 'Configured ingress PVID: '+str(effective['pvid']) in port['Notes']
    assert sum(p['Aggregate']=='TRK1' for p in inv['Ports'])==4 and sum(p['Aggregate']=='TRK2' for p in inv['Ports'])==4
    assert {v['Id']:v['Name'] for v in inv['Vlans'] if v['Name']}==names
    assert any(s['Interface']=='VLAN 77' and s['Address']=='10.15.14.250 255.255.255.0' for s in inv['SVIs'])
    payloads=[line.strip().encode() for line in text.splitlines() if re.fullmatch(r'[A-Za-z0-9+/=]{40,}',line.strip())]
    for file in [out/'inventory.json',out/'port-map.csv',out/'technician-summary.txt']:
        data=file.read_bytes();assert all(payload not in data for payload in payloads)
# Source-referenced families and Comware command semantics.
cw_header='# JG932A\nversion 7.1.070\nsysname COMWARE-LAB\n#\n'
cw_access='''vlan 10
 name Data
#
vlan 20
 name Voice
#
interface GigabitEthernet1/0/1
 port access vlan 10
 voice-vlan 20 enable
 description Reception
#
interface GigabitEthernet1/0/2
 port link-type hybrid
 port hybrid pvid vlan 10
 port hybrid vlan 10 untagged
 port hybrid vlan 20 30 to 31 tagged
 undo port hybrid vlan 31
#
irf-port 1/1
 port group interface Ten-GigabitEthernet1/0/28
#
'''
out,inv=run('comware-access-voice-hybrid-irf',cw_header+cw_access)
assert inv['Ports'][0]['UntaggedVlan']==10 and inv['Ports'][0]['VoiceVlan']==20
assert inv['Ports'][1]['TaggedVlans']==[20,30] and inv['Ports'][-1]['StackLink']
cw_lag='''interface Bridge-Aggregation1
 description Core uplink
 port link-type trunk
 port trunk pvid vlan 10
 port trunk permit vlan 10 20 to 22
 link-aggregation mode dynamic
#
interface GigabitEthernet1/0/1
 port access vlan 99
 port link-aggregation group 1
#
interface GigabitEthernet1/0/2
 port link-aggregation group 1
#
'''
out,inv=run('comware-lacp-inheritance',cw_header+cw_lag)
assert all(p['UntaggedVlan']==10 and p['TaggedVlans']==[20,21,22] and p['Aggregate']=='BAGG1' for p in inv['Ports'][:2])
assert any('LACP dynamic' in note for note in inv['Ports'][0]['Notes'])
out,inv=run('comware5-1920',cw_header.replace('JG932A','JG924A').replace('7.1.070','5.20')+'interface GigabitEthernet1/0/25\n port access vlan 10\n')
assert inv['Ports'][24]['Media']=='SFP' and len(inv['Ports'])==28
out,inv=run('comware1950-mixed-media',cw_header.replace('JG932A','JG961A')+'interface Ten-GigabitEthernet1/0/49\n port access vlan 10\n#\ninterface Ten-GigabitEthernet1/0/51\n port access vlan 20\n')
assert inv['Ports'][48]['Media']=='SFP+' and inv['Ports'][50]['Media']=='RJ-45'
out,inv=run('comware5130-shared-sockets',cw_header.replace('JG932A','JG933A')+'interface GigabitEthernet1/0/17\n port access vlan 10\n')
assert len(inv['Ports'])==28 and sum(s['Number']==17 for s in inv['Groups'][0]['SocketLayout'])==2
out,inv=run('comware-selected-missing-sku',cw_header.replace('# JG932A\n','')+cw_access,models='JG932A')
assert len(inv['Ports'])==28 and any('operator' in a for a in inv['Assumptions'])
out,inv=run('comware-selected-mixed-irf','version 7.1.070\nsysname MIXED-IRF\ninterface GigabitEthernet1/0/1\n port access vlan 10\n#\ninterface GigabitEthernet2/0/48\n port access vlan 20\n',models='1=JG932A,2=JG934A')
assert len(inv['Ports'])==80 and len(inv['Groups'])==2
run('comware-unidentified-model',cw_header.replace('# JG932A\n','')+cw_access,3)
run('comware-multiple-untagged',cw_header+'interface GigabitEthernet1/0/1\n port link-type hybrid\n port hybrid vlan 10 20 untagged\n',3)
run('comware-wrong-media-interface',cw_header+'interface GigabitEthernet1/0/25\n port access vlan 10\n',3)
run('comware-orphan-aggregate',cw_header+'interface Bridge-Aggregation1\n port access vlan 10\n',3)
run('comware-unsupported-breakout',cw_header+'interface Ten-GigabitEthernet1/0/25:1\n port access vlan 10\n',3)
run('comware-selected-identity-conflict',cw_header,3,models='JG934A')
out,inv=run('cx-selected-missing-sku','!Version ArubaOS-CX ML.10.13.1000\nhostname SELECTED\ninterface 1/1/1\n vlan access 10\n',models='R8Q69A')
assert len(inv['Ports'])==52 and inv['Groups'][0]['Product']=='R8Q69A'
run('selected-os-conflict','; Configuration Editor\nhostname WRONG-OS\n',3,models='JG932A')
run('catalogue-unverified-layout-blocked','! AOS-CX R8Z96A\nhostname PENDING\n',3)
out,inv=run('aoss2530-combo','; J9774A Configuration Editor\nhostname COMBO\nvlan 10\n untagged 9\n exit\nvlan 20\n tagged 9\n exit\n')
assert len(inv['Ports'])==10 and len(inv['Groups'][0]['SocketLayout'])==12
out,inv=run('aoss2530-fastethernet-media','; J9778A Configuration Editor\nhostname FAST-ETHERNET\nvlan 10\n untagged 49-52\n exit\n')
assert inv['Ports'][48]['Media']=='RJ-45' and inv['Ports'][50]['Media']=='SFP'
out,inv=run('cx6100-compact-independent-ports','! AOS-CX JL679A\nhostname COMPACT\ninterface 1/1/13\n vlan access 10\ninterface 1/1/15\n vlan access 20\n')
assert len(inv['Ports'])==16 and inv['Ports'][12]['Media']=='RJ-45' and inv['Ports'][14]['Media']=='SFP+'
out,inv=run('cx8320-three-row-qsfp','! AOS-CX JL479A\nhostname DATA-CENTRE\ninterface lag 1\n lacp mode active\n vlan trunk native 10\n vlan trunk allowed 10,20\ninterface 1/1/49\n lag 1\ninterface 1/1/52\n lag 1\n')
assert len(inv['Ports'])==54 and inv['Groups'][0]['SocketLayout'][-4]['Row']==2
for sku in ('JL806A','JL809A'):
 out,inv=run('instanton1960-new-'+sku,f'config-file-header\nNEW-1960\nvInstantOn_1960_3.3.0.0\nSKU Description "Instant On 1960 {sku}"\n!\ninterface 1\n switchport general allowed vlan add 10 untagged\n switchport general pvid 10\n!\n')
 assert len(inv['Ports'])==(28 if sku=='JL806A' else 52)
new1960='config-file-header\nMIXED-1960\nvInstantOn_1960_3.3.0.0\nSKU Description "Instant On 1960 JL806A"\n!\ninterface 1/1\n switchport general allowed vlan add 10 untagged\n switchport general pvid 10\n!\ninterface 2/48\n switchport general allowed vlan add 20 untagged\n switchport general pvid 20\n!\n'
out,inv=run('instanton1960-selected-mixed-members',new1960,models='1=JL806A,2=JL809A')
assert len(inv['Ports'])==80 and len(inv['Groups'])==2
run('instanton1960-unidentified-extra-members',new1960+'unit-type unit 1 network ge uplink te\nunit-type unit 2 network ge uplink te\n',3)
run('selected-internal-shared-profile-rejected','!Version ArubaOS-CX PL.10.10.1070\nhostname BAD-SELECTION\n',3,models='CX6000-6100-48G-4OPT')

report='\n'.join(checks)+f'\n{len(checks)} regression cases passed.\n'
print(report)
if args.report:Path(args.report).write_text(report)
print('Verification output directory:',root)
