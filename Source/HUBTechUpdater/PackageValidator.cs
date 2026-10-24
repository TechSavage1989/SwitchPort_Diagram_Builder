using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace HUBTechUpdater;

public static class PackageValidator
{
    static readonly HashSet<string> AllowedNames=new(StringComparer.Ordinal)
    {UpdateProtocol.ExecutableName,"READ_ME.txt","SHA256SUMS.txt","UPDATE_INFO.txt"};
    public static string Stage(string zipPath,UpdateOffer offer,string stageDirectory,bool validateExecutable=true)
    {
        UpdateService.ValidateOffer(offer);
        var file=new FileInfo(zipPath);
        if(!file.Exists||file.Length!=offer.PackageSize)throw new UpdateException("The update package size is invalid.");
        if(!ExecutableValidator.Sha256(zipPath).Equals(offer.PackageSha256,StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("The update package checksum is invalid.");
        string destination=Path.GetFullPath(stageDirectory);
        if(Directory.Exists(destination)&&Directory.EnumerateFileSystemEntries(destination).Any())
            throw new UpdateException("The update staging directory is not empty.");
        Directory.CreateDirectory(destination);
        string staged=Path.Combine(destination,UpdateProtocol.ExecutableName);
        bool created=false;
        try
        {
            using var archive=ZipFile.OpenRead(zipPath);
            if(archive.Entries.Count is <1 or >4)throw new UpdateException("The update package contains unexpected files.");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ZipArchiveEntry? executable=null;long total=0;
            foreach(var entry in archive.Entries)
            {
                string name=entry.FullName;
                if(!AllowedNames.Contains(name)||!names.Add(name))throw new UpdateException("The update package contains an unsafe, duplicate or unexpected path.");
                int unixKind=(entry.ExternalAttributes>>16)&0xF000;
                if(unixKind is 0xA000 or 0x4000||(entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0)
                    throw new UpdateException("Links and directories are not accepted in update packages.");
                long maximum=name==UpdateProtocol.ExecutableName?UpdateProtocol.MaximumExecutableBytes:2*1024*1024;
                if(entry.Length<=0||entry.Length>maximum||entry.Length>Math.Max(1024*1024,entry.CompressedLength*150))
                    throw new UpdateException("An update package member exceeds its safe size limit.");
                total+=entry.Length;
                if(total>UpdateProtocol.MaximumExecutableBytes+6*1024*1024)throw new UpdateException("The expanded update package is too large.");
                if(name==UpdateProtocol.ExecutableName)executable=entry;
            }
            if(executable==null||executable.Length!=offer.ExecutableSize)throw new UpdateException("The update package has no matching application executable.");
            using(var input=executable.Open())
            using(var output=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.SequentialScan))
            {
                created=true;byte[] buffer=new byte[128*1024];long count=0;
                while(true)
                {
                    int read=input.Read(buffer,0,buffer.Length);if(read==0)break;
                    count+=read;if(count>offer.ExecutableSize)throw new UpdateException("The expanded executable exceeded its declared size.");
                    output.Write(buffer,0,read);
                }
                if(count!=offer.ExecutableSize)throw new UpdateException("The executable in the update package is incomplete.");
                output.Flush(true);
            }
            if(validateExecutable)ExecutableValidator.Validate(staged,offer.Version,offer.ExecutableSha256,offer.ExecutableSize);
            else if(!ExecutableValidator.Sha256(staged).Equals(offer.ExecutableSha256,StringComparison.OrdinalIgnoreCase))
                throw new UpdateException("The executable checksum in the update package is invalid.");
            return staged;
        }
        catch(Exception ex)when(ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if(created)try{File.Delete(staged);}catch{ }
            throw new UpdateException("The update package could not be safely staged.",ex);
        }
        catch
        {
            if(created)try{File.Delete(staged);}catch{ }
            throw;
        }
    }
}

public static class ExecutableValidator
{
    public static string Sha256(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    public static void Validate(string path,Version expectedVersion,string expectedSha256,long expectedSize)
    {
        UpdateProtocol.Hash(expectedSha256);
        var info=new FileInfo(path);
        if(!info.Exists||info.Length!=expectedSize||expectedSize<256||expectedSize>UpdateProtocol.MaximumExecutableBytes)
            throw new UpdateException("The staged executable size is invalid.");
        if(!Sha256(path).Equals(expectedSha256,StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("The staged executable checksum is invalid.");
        try
        {
            using var stream=File.OpenRead(path);using var reader=new BinaryReader(stream);
            if(reader.ReadUInt16()!=0x5A4D)throw new UpdateException("The update is not a Windows executable.");
            stream.Position=0x3c;int peOffset=reader.ReadInt32();
            if(peOffset<0x40||peOffset>stream.Length-120)throw new UpdateException("The executable header is invalid.");
            stream.Position=peOffset;
            if(reader.ReadUInt32()!=0x00004550||reader.ReadUInt16()!=0x8664)
                throw new UpdateException("The update is not a Windows x64 executable.");
            ushort sections=reader.ReadUInt16();
            stream.Position=peOffset+20;ushort optionalSize=reader.ReadUInt16();
            if(sections is <1 or >96||optionalSize<112||peOffset+24L+optionalSize+40L*sections>stream.Length)
                throw new UpdateException("The executable section table is invalid.");
            stream.Position=peOffset+24;
            if(reader.ReadUInt16()!=0x20B)throw new UpdateException("The update is not a 64-bit executable.");
            stream.Position=peOffset+24+68;
            if(reader.ReadUInt16()!=2)throw new UpdateException("The update is not a Windows desktop application.");
            if(OperatingSystem.IsWindows())
            {
                // Keep Windows' native version-resource validation on the platform
                // that actually installs updates. On Unix FileVersionInfo cannot
                // read native PE apphost resources and returns null/zero fields.
                var version=FileVersionInfo.GetVersionInfo(path);
                ValidateMetadata(version.ProductName,version.OriginalFilename,version.FileMajorPart,version.FileMinorPart,
                    version.FileBuildPart,version.FilePrivatePart,expectedVersion);
            }
            else
            {
                var version=PortableVersionInfo.Read(path);
                ValidateMetadata(version.ProductName,version.OriginalFilename,version.Major,version.Minor,version.Build,version.Revision,expectedVersion);
            }
        }
        catch(UpdateException){throw;}
        catch(Exception ex)when(ex is IOException or ArgumentException or InvalidOperationException)
        {throw new UpdateException("The Windows executable metadata could not be validated.",ex);}
    }
    static void ValidateMetadata(string? product,string? original,int major,int minor,int build,int revision,Version expected)
    {
        if(product!=UpdateProtocol.Product||
            !(original?.Equals(UpdateProtocol.ExecutableName,StringComparison.OrdinalIgnoreCase)==true||
              original?.Equals(UpdateProtocol.Product+".dll",StringComparison.OrdinalIgnoreCase)==true))
            throw new UpdateException("The update executable belongs to a different application.");
        if(major!=expected.Major||minor!=expected.Minor||build!=Math.Max(0,expected.Build)||revision!=0)
            throw new UpdateException("The executable version does not match the update manifest.");
    }
}
