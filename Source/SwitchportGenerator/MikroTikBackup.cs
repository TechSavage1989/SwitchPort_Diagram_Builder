using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
namespace SwitchportGenerator;

// Decode only inventory and switching records. Never extract credentials, keys,
// scripts or user databases, and never write archive paths to the filesystem.
public static class MikroTikBackup
{
    public const string Prefix="# Switchport ROUTEROS INVENTORY\n";
    sealed class Reader
    {
        readonly byte[] b;int p;
        public Reader(byte[] value){b=value;}
        public int Remaining=>b.Length-p;
        public byte[] Bytes(int n){if(n<0||n>Remaining)throw new InvalidOperationException("Damaged MikroTik backup record.");var v=b.AsSpan(p,n).ToArray();p+=n;return v;}
        public uint U(int n){var v=Bytes(n);return n==1?v[0]:n==2?BinaryPrimitives.ReadUInt16LittleEndian(v):BinaryPrimitives.ReadUInt32LittleEndian(v);}
    }
    static Dictionary<uint,object> Message(byte[] data,int depth=0)
    {
        if(depth>12||data.Length<2||data[0]!='M'||data[1]!='2')throw new InvalidOperationException("Unsupported MikroTik record encoding. Use a RouterOS .rsc export.");
        var r=new Reader(data);r.Bytes(2);var values=new Dictionary<uint,object>();
        while(r.Remaining>0)
        {
            uint tag=r.U(4),key=tag&0xffffff,kind=tag&0xf8000000;bool small=(tag&0x01000000)!=0;object value;
            if(kind==0)value=small;
            else if(kind==0x08000000)value=r.U(small?1:4);
            else if(kind==0x10000000)value=BinaryPrimitives.ReadUInt64LittleEndian(r.Bytes(8));
            else if(kind is 0x20000000 or 0x28000000 or 0x30000000)
            {
                byte[] raw=r.Bytes((int)r.U(small?1:2));
                value=kind==0x28000000?Message(raw,depth+1):kind==0x30000000?raw:Encoding.UTF8.GetString(raw);
            }
            else if(kind is 0x80000000 or 0x88000000 or 0x90000000 or 0xa0000000)
            {
                int count=(int)r.U(2);var list=new List<object>();
                for(int i=0;i<count;i++)list.Add(kind==0x80000000?r.U(1)!=0:kind==0x88000000?r.U(4):kind==0x90000000?BinaryPrimitives.ReadUInt64LittleEndian(r.Bytes(8)):Encoding.UTF8.GetString(r.Bytes((int)r.U(2))));
                value=list;
            }
            else throw new InvalidOperationException("Unsupported MikroTik record type. Use a RouterOS .rsc export.");
            if(!values.TryAdd(key|kind,value))throw new InvalidOperationException("Duplicate MikroTik record field.");
        }
        return values;
    }
    static uint N(Dictionary<uint,object> m,uint k,uint fallback=0)=>m.TryGetValue(k|0x08000000,out var v)&&v is uint n?n:fallback;
    static bool B(Dictionary<uint,object> m,uint k)=>m.TryGetValue(k,out var v)&&v is true;
    static string S(Dictionary<uint,object> m,uint k)=>m.TryGetValue(k|0x20000000,out var v)&&v is string s?Parser.Safe(s):"";
    static List<uint> A(Dictionary<uint,object> m,uint k)=>m.TryGetValue(k|0x88000000,out var v)&&v is List<object> l?l.OfType<uint>().ToList():new();
    public static string Decode(byte[] bytes)
    {
        var r=new Reader(bytes);
        if(r.U(4)!=0xb1a1ac88||r.U(4)!=bytes.Length)throw new InvalidOperationException("The MikroTik backup is damaged or truncated.");
        var tables=new Dictionary<string,List<Dictionary<uint,object>>>();
        var wanted=new HashSet<string>{"system","net/devices","bridgeports","bridge-vlans","net/switch-vlans","net/music/vlan","net/music/vlan-ingress-trans","net/music/vlan-egress-trans"};
        while(r.Remaining>0)
        {
            int nameLength=(int)r.U(4);if(nameLength>1024)throw new InvalidOperationException("Invalid MikroTik backup table.");
            string name=Encoding.UTF8.GetString(r.Bytes(nameLength));byte[] idx=r.Bytes((int)r.U(4)),dat=r.Bytes((int)r.U(4));
            if(!wanted.Contains(name))continue;
            if(tables.ContainsKey(name)||idx.Length%12!=0)throw new InvalidOperationException("Unsupported MikroTik backup index.");
            var records=new List<Dictionary<uint,object>>();int offset=0;
            for(int at=0;at<idx.Length;at+=12)
            {
                uint id=BinaryPrimitives.ReadUInt32LittleEndian(idx.AsSpan(at)),size=BinaryPrimitives.ReadUInt32LittleEndian(idx.AsSpan(at+4)),flags=BinaryPrimitives.ReadUInt32LittleEndian(idx.AsSpan(at+8));
                if(size<4||size>65535||offset+(long)size>dat.Length||BinaryPrimitives.ReadUInt16LittleEndian(dat.AsSpan(offset))!=size)throw new InvalidOperationException("Damaged MikroTik table entry.");
                var record=dat.AsSpan(offset+2,(int)size-2).ToArray();offset+=(int)size;
                // Tombstones/internal sentinel records are not configured entries.
                if(flags!=0||id>=0xffffff00)continue;
                records.Add(Message(record));
            }
            if(offset!=dat.Length)throw new InvalidOperationException("MikroTik table length does not match its index.");
            tables.Add(name,records);
        }
        List<Dictionary<uint,object>> Table(string name)=>tables.GetValueOrDefault(name)??new();
        var snapshot=new MikroTikParser.Snapshot{Binary=true,Hostname=Table("system").Select(m=>S(m,12)).FirstOrDefault(s=>s.Length>0)??"Unknown"};
        foreach(var m in Table("net/devices"))
        {
            var settings=m.TryGetValue(0x28010008,out var v)&&v is Dictionary<uint,object> nested?nested:new();
            uint type=N(m,0x010001);string name=S(m,0x010006);
            if(type==1)snapshot.Interfaces.Add(new(){Key=N(m,0xfe0001).ToString(),Name=name,DefaultName=name,Disabled=B(settings,0xfe000a),Comment=S(m,0xfe0009)});
            else if(type==12)snapshot.Bridges.Add(new(){Key=N(m,0xfe0001).ToString(),Name=name,Filtering=null});
            else if(type is not (30 or 76))snapshot.Unsupported.Add("Unsupported RouterOS binary interface type "+type+". Use a RouterOS .rsc export.");
        }
        foreach(var m in Table("bridgeports"))
        {
            if(!m.ContainsKey(0x08000003)||!m.ContainsKey(0x08000004)||!m.ContainsKey(0x08000013))throw new InvalidOperationException("Unsupported MikroTik bridge-port schema.");
            uint frame=N(m,0x14);if(frame>2)throw new InvalidOperationException("Unknown MikroTik frame admission mode.");
            snapshot.BridgePorts.Add(new(){Interface=N(m,3).ToString(),Bridge=N(m,4).ToString(),Pvid=(int)N(m,0x13),Frames=frame==1?"admit-only-vlan-tagged":frame==2?"admit-only-untagged-and-priority-tagged":"admit-all",Disabled=B(m,0xfe0008),TagStacking=B(m,0x1a)});
        }
        foreach(var m in Table("bridge-vlans"))
        {
            var range=A(m,1);if(range.Count%2!=0||range.Count==0)throw new InvalidOperationException("Unsupported MikroTik VLAN range encoding.");
            var ids=new List<int>();for(int i=0;i<range.Count;i+=2){if(range[i]<1||range[i+1]>4094||range[i]>range[i+1])throw new InvalidOperationException("Invalid MikroTik VLAN range.");ids.AddRange(Enumerable.Range((int)range[i],(int)(range[i+1]-range[i]+1)));}
            snapshot.Vlans.Add(new(){Bridge=N(m,2).ToString(),Ids=ids,Tagged=A(m,3).Select(n=>n.ToString()).ToList(),Untagged=A(m,4).Select(n=>n.ToString()).ToList(),Comment=S(m,0xfe0009),Disabled=B(m,0xfe0008)});
        }
        foreach(string table in wanted.Where(t=>t.StartsWith("net/")&&t!="net/devices"))if(Table(table).Count>0)snapshot.Unsupported.Add("Switch-chip VLAN tables require a RouterOS .rsc export for validation.");
        return Prefix+JsonSerializer.Serialize(snapshot);
    }
}
