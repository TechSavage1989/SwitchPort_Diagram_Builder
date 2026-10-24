using System.Buffers.Binary;
using System.Text;

namespace HUBTechUpdater;

// A bounded, read-only RT_VERSION decoder used for release verification on Unix.
// It does not search bundled DLL bytes or treat arbitrary UTF-16 strings as metadata.
internal sealed record PortableVersionInfo(string ProductName,string OriginalFilename,int Major,int Minor,int Build,int Revision)
{
    const int MaximumResourceBytes=16*1024*1024,MaximumVersionBytes=64*1024;
    static readonly UnicodeEncoding Utf16=new(false,false,true);
    sealed record Block(int End,int Value,int ValueBytes,int Children,int Type,string Key);
    public static PortableVersionInfo Read(string path)
    {
        try
        {
            using var stream=File.OpenRead(path);using var reader=new BinaryReader(stream);
            if(stream.Length<256)throw Invalid();
            stream.Position=0x3c;int pe=reader.ReadInt32();
            if(pe<0x40||pe>stream.Length-136)throw Invalid();
            stream.Position=pe;
            if(reader.ReadUInt32()!=0x4550||reader.ReadUInt16()!=0x8664)throw Invalid();
            int sections=reader.ReadUInt16();
            stream.Position=pe+20;int optional=reader.ReadUInt16();
            if(sections is <1 or >96||optional<136||pe+24L+optional+40L*sections>stream.Length)throw Invalid();
            stream.Position=pe+24;
            if(reader.ReadUInt16()!=0x20b)throw Invalid();
            stream.Position=pe+24+108;
            if(reader.ReadUInt32()<3)throw Invalid();
            stream.Position=pe+24+128;uint resourceRva=reader.ReadUInt32(),resourceSize=reader.ReadUInt32();
            if(resourceSize<16||resourceSize>MaximumResourceBytes||resourceRva==0)throw Invalid();
            long resourceOffset=-1;
            for(int i=0;i<sections;i++)
            {
                stream.Position=pe+24+optional+i*40+12;
                uint address=reader.ReadUInt32(),rawSize=reader.ReadUInt32(),rawOffset=reader.ReadUInt32();
                if(resourceRva>=address&&(ulong)resourceRva+resourceSize<=(ulong)address+rawSize)
                {
                    if(resourceOffset>=0)throw Invalid();
                    resourceOffset=(long)rawOffset+resourceRva-address;
                }
            }
            if(resourceOffset<0||resourceOffset+resourceSize>stream.Length)throw Invalid();
            stream.Position=resourceOffset;byte[] resources=reader.ReadBytes((int)resourceSize);
            if(resources.Length!=resourceSize)throw Invalid();
            var types=Directory(resources,0).Where(e=>e.Name==16).ToArray();
            if(types.Length!=1||(types[0].Data&0x80000000)==0)throw Invalid();
            var infos=new List<PortableVersionInfo>();
            foreach(var name in Directory(resources,Offset(types[0].Data)))
            {
                if((name.Data&0x80000000)==0)throw Invalid();
                foreach(var language in Directory(resources,Offset(name.Data)))
                {
                    if((language.Data&0x80000000)!=0)throw Invalid();
                    int entry=Offset(language.Data);Range(resources,entry,16);
                    uint rva=U32(resources,entry),size=U32(resources,entry+4);
                    if(size<52||size>MaximumVersionBytes||rva<resourceRva||(ulong)rva+size>(ulong)resourceRva+resourceSize)throw Invalid();
                    int offset=checked((int)(rva-resourceRva));
                    infos.Add(Decode(resources.AsSpan(offset,(int)size).ToArray()));
                    if(infos.Count>64)throw Invalid();
                }
            }
            if(infos.Count==0||infos.Any(v=>v!=infos[0]))throw Invalid();
            return infos[0];
        }
        catch(UpdateException){throw;}
        catch(Exception ex)when(ex is IOException or ArgumentException or OverflowException or DecoderFallbackException)
        {throw new UpdateException("The executable version resource is malformed.",ex);}
    }
    static PortableVersionInfo Decode(byte[] data)
    {
        Block root=ReadBlock(data,0,data.Length);
        if(root.Key!="VS_VERSION_INFO"||root.Type!=0||root.ValueBytes!=52||U32(data,root.Value)!=0xfeef04bd||U32(data,root.Value+4)!=0x10000)throw Invalid();
        uint ms=U32(data,root.Value+8),ls=U32(data,root.Value+12);
        var values=new List<(string Product,string Original)>();
        foreach(var child in Children(data,root))
        {
            if(child.Key!="StringFileInfo")continue;
            if(child.Type!=1||child.ValueBytes!=0)throw Invalid();
            foreach(var table in Children(data,child))
            {
                if(table.Type!=1||table.ValueBytes!=0||table.Key.Length!=8||!table.Key.All(Uri.IsHexDigit))throw Invalid();
                var strings=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var item in Children(data,table))
                {
                    if(item.Type!=1||item.ValueBytes<2||item.ValueBytes>4096||(item.ValueBytes&1)!=0||!strings.TryAdd(item.Key,ReadValue(data,item)))throw Invalid();
                }
                if(!strings.TryGetValue("ProductName",out string? product)||!strings.TryGetValue("OriginalFilename",out string? original))throw Invalid();
                values.Add((product,original));
                if(values.Count>32)throw Invalid();
            }
        }
        if(values.Count==0||values.Any(v=>v!=values[0]))throw Invalid();
        return new(values[0].Product,values[0].Original,(int)(ms>>16),(int)(ms&65535),(int)(ls>>16),(int)(ls&65535));
    }
    static string ReadValue(byte[] data,Block block)
    {
        if(U16(data,block.Value+block.ValueBytes-2)!=0)throw Invalid();
        return Utf16.GetString(data,block.Value,block.ValueBytes-2);
    }
    static Block ReadBlock(byte[] data,int offset,int parentEnd)
    {
        Range(data,offset,6);int length=U16(data,offset),valueLength=U16(data,offset+2),type=U16(data,offset+4);
        if(length<6||offset+length>parentEnd||type>1)throw Invalid();
        int end=offset+length,keyStart=offset+6,p=keyStart;
        while(p+2<=end&&U16(data,p)!=0){p+=2;if(p-keyStart>512)throw Invalid();}
        if(p+2>end)throw Invalid();
        string key=Utf16.GetString(data,keyStart,p-keyStart);int value=Align(p+2);
        int bytes=type==1?valueLength*2:valueLength;
        if(bytes>0&&(value>end||bytes>end-value))throw Invalid();
        return new(end,value,bytes,Align(value+bytes),type,key);
    }
    static IEnumerable<Block> Children(byte[] data,Block parent)
    {
        int offset=parent.Children,count=0;
        while(offset<parent.End)
        {
            if(parent.End-offset<6)
            {
                for(int i=offset;i<parent.End;i++)if(data[i]!=0)throw Invalid();
                yield break;
            }
            Block child=ReadBlock(data,offset,parent.End);yield return child;
            offset=Align(child.End);if(++count>128)throw Invalid();
        }
    }
    static (uint Name,uint Data)[] Directory(byte[] data,int offset)
    {
        Range(data,offset,16);int count=U16(data,offset+12)+U16(data,offset+14);
        if(count>256)throw Invalid();Range(data,offset+16,count*8);
        var entries=new (uint,uint)[count];
        for(int i=0;i<count;i++)entries[i]=(U32(data,offset+16+i*8),U32(data,offset+20+i*8));
        return entries;
    }
    static int Offset(uint value)=>checked((int)(value&0x7fffffff));
    static int Align(int value)=>checked((value+3)&~3);
    static ushort U16(byte[] data,int offset){Range(data,offset,2);return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset,2));}
    static uint U32(byte[] data,int offset){Range(data,offset,4);return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset,4));}
    static void Range(byte[] data,int offset,int count){if(offset<0||count<0||offset>data.Length-count)throw Invalid();}
    static UpdateException Invalid()=>new("The executable version resource is malformed or ambiguous.");
}
