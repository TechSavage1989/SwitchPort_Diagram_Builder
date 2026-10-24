"""Compare generated inventories with explicit source VLAN commands, independently of C#.
Requires a results.json list: case (output subdirectory), filename and code.
Uploaded configurations are intentionally external to the source package.
"""
import argparse,json,re
from pathlib import Path
ap=argparse.ArgumentParser();ap.add_argument('--inputs',type=Path,required=True);ap.add_argument('--exports',type=Path,required=True);args=ap.parse_args()
def expand(spec):
    values=[]
    for token in spec.replace(' ','').split(','):
        if not token:continue
        if '-' not in token:values.append(token);continue
        a,b=token.split('-');m=re.fullmatch(r'(.*?)(\d+)',a);n=re.fullmatch(r'(.*?)(\d+)',b)
        assert m and n and (not n[1] or m[1].lower()==n[1].lower()),'Invalid source range'
        values.extend(m[1]+str(x) for x in range(int(m[2]),int(n[2])+1))
    return values
checks=0
for entry in json.loads((args.exports/'results.json').read_text()):
    assert entry['code']==0, f"Export failed: {entry['case']}"
    inv=json.loads((args.exports/str(entry['case'])/'inventory.json').read_text());source=(args.inputs/entry['filename']).read_text(errors='replace');lines=source.splitlines()
    ports={p['Id'].lower():p for p in inv['Ports']};native={k:None for k in ports};tagged={k:set() for k in ports};names={};voice=set();aggregates={}
    cx='!Version ArubaOS-CX' in source
    if not cx:
        for line in lines:
            m=re.match(r'\s*trunk\s+(\S+)\s+(trk\d+)\s+',line,re.I)
            if m:aggregates[m[2].lower()]=[x.lower() for x in expand(m[1])]
        def resolve(spec):
            result=[]
            for token in expand(spec):result.extend(aggregates.get(token.lower(),[token.lower()]))
            assert all(x in ports for x in result),'Source membership references missing interface'
            return result
        active=None;i=0
        while i<len(lines):
            line=lines[i].strip();i+=1
            m=re.fullmatch(r'vlan (\d+)',line,re.I)
            if m:active=int(m[1]);continue
            if line in ('exit','end'):active=None
            if active is None:continue
            if line.startswith('name '):names[active]=line[5:].strip('"')
            if line=='voice':voice.add(active)
            m=re.fullmatch(r'(no )?(untagged|tagged)(?: (.*))?',line,re.I)
            if not m:continue
            spec=m[3] or ''
            # Collect physical-list-only continuations before tokenizing.
            while i<len(lines) and re.fullmatch(r'[A-L0-9][A-L0-9/,\-]*',re.sub(r'(?i)trk','A',lines[i].strip())):spec+=lines[i].strip();i+=1
            for pid in resolve(spec):
                if m[2].lower()=='untagged':
                    if m[1]:
                        if native[pid] in (None,active):native[pid]=None
                    else:native[pid]=active
                elif m[1]:tagged[pid].discard(active)
                else:tagged[pid].add(active)
    else:
        current=None;vlan=None
        for raw in lines:
            line=raw.strip()
            if raw and not raw[0].isspace():
                current=None;vlan=None
                if re.fullmatch(r'interface \d+/\d+/\d+',line):current=line.split()[1].lower()
                if re.fullmatch(r'vlan \d+',line):vlan=int(line.split()[1])
            if vlan is not None:
                if line.startswith('name '):names[vlan]=line[5:].strip('"')
                if line=='voice':voice.add(vlan)
            if current is None:continue
            m=re.fullmatch(r'vlan (?:access|trunk native) (\d+)',line)
            if m:native[current]=int(m[1])
            m=re.fullmatch(r'vlan trunk allowed ([0-9,\-]+)',line)
            if m:tagged[current]=set(map(int,expand(m[1])))
    for pid,p in ports.items():
        expected=tagged[pid]-({native[pid]} if native[pid] is not None else set())
        assert p['UntaggedVlan']==native[pid],f"Native VLAN mismatch in export {entry['case']} interface {pid}"
        assert set(p['TaggedVlans'])==expected,f"Tagged VLAN mismatch in export {entry['case']} interface {pid}"
        for aggregate,members in aggregates.items():
            if pid in members:assert p['Aggregate'].lower()==aggregate
        voices=({native[pid]}|expected)&voice
        if len(voices)==1:assert p['VoiceVlan']==next(iter(voices))
        checks+=1
    actual={v['Id']:v['Name'] for v in inv['Vlans']}
    assert all(actual.get(k)==v for k,v in names.items()),'VLAN name mismatch'
    print(f"PASS export {entry['case']}: {len(ports)} interfaces, explicit native/tagged memberships, aggregate membership and VLAN names")
print(f'PASS {checks} per-interface comparisons across all saved exports')
