import struct,sys
from pathlib import Path
exe,source=map(Path,sys.argv[1:3]);b=exe.read_bytes();pe=struct.unpack_from('<I',b,60)[0];opt=pe+24
sections=struct.unpack_from('<H',b,pe+6)[0];opsize=struct.unpack_from('<H',b,pe+20)[0]
resource_rva,size=struct.unpack_from('<II',b,opt+112+16)
sec=[]
for i in range(sections):
 o=opt+opsize+i*40;vsize,va,rawsize,raw=struct.unpack_from('<IIII',b,o+8);sec.append((va,max(vsize,rawsize),raw))
def offset(rva):
 for va,size,raw in sec:
  if va<=rva<va+size:return raw+rva-va
 raise ValueError(rva)
base=offset(resource_rva);resources={}
def walk(rel,path=()):
 o=base+rel;count=sum(struct.unpack_from('<HH',b,o+12))
 for i in range(count):
  name,target=struct.unpack_from('<II',b,o+16+8*i);key=name if name<0x80000000 else 'name'
  if target&0x80000000:walk(target&0x7fffffff,path+(key,))
  else:
   rva,size,_,_=struct.unpack_from('<IIII',b,base+target);resources[path+(key,)]=b[offset(rva):offset(rva)+size]
walk(0)
icons=[data for path,data in resources.items() if path[0]==3];groups=[data for path,data in resources.items() if path[0]==14]
ico=source.read_bytes();n=struct.unpack_from('<H',ico,4)[0];frames=[]
for i in range(n):
 size,start=struct.unpack_from('<II',ico,6+16*i+8);frames.append(ico[start:start+size])
assert len(groups)==1 and len(icons)==n and set(icons)==set(frames)
assert struct.unpack_from('<H',groups[0],4)[0]==n
print('PASS native PE contains all',n,'compiled ICO image resources, matching source ICO bytes')
