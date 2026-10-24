using System.Text.Json;
using System.Text.RegularExpressions;

namespace HUBTechUpdater;

public sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message) { }
    public UpdateException(string message,Exception inner) : base(message,inner) { }
}

public sealed record UpdateOffer(Version Version,string Notes,Uri ReleaseUrl,Uri PackageUrl,
    string PackageName,long PackageSize,string PackageSha256,long ExecutableSize,string ExecutableSha256)
{
    public string VersionText=>Version.ToString(3);
}
public sealed record StagedUpdate(UpdateOffer Offer,string ExecutablePath);
public sealed record UpdateProgress(long BytesReceived,long TotalBytes)
{
    public int Percent=>TotalBytes<=0?0:(int)Math.Clamp(BytesReceived*100/TotalBytes,0,100);
}
public sealed record ReleaseAsset(string Name,Uri Url,long Size);
public sealed record ReleaseInfo(Version Version,string Tag,string Notes,Uri ReleaseUrl,
    ReleaseAsset ManifestAsset,IReadOnlyList<ReleaseAsset> Assets);

/// <summary>The only release channel accepted by the installed application.</summary>
public static class UpdateProtocol
{
    public const string Repository="TechSavage1989/SwitchPort_Diagram_Builder";
    public const string Product="HUBTechDiagramBuilder";
    public const string ExecutableName="HUBTechDiagramBuilder.exe";
    public const string ManifestName="update-manifest.json";
    public const long MaximumPackageBytes=512L*1024*1024;
    public const long MaximumExecutableBytes=512L*1024*1024;
    public const int MaximumManifestBytes=64*1024;
    public const int MaximumReleaseBytes=1024*1024;
    public static readonly Uri LatestReleaseUrl=new("https://api.github.com/repos/"+Repository+"/releases/latest");

    public static Version Normalize(Version version)=>new(version.Major,version.Minor,Math.Max(0,version.Build));

    public static Version ParseVersion(string value)
    {
        string text=value.Trim();
        if(text.StartsWith('v')||text.StartsWith('V'))text=text[1..];
        if(!Regex.IsMatch(text,@"^\d{1,5}\.\d{1,5}\.\d{1,5}$")||
            !Version.TryParse(text,out var version)||version.Major>65535||version.Minor>65535||version.Build>65535)
            throw new UpdateException("The release does not contain a supported stable version.");
        return Normalize(version);
    }

    public static string PackageName(Version version)=>"HUBTech-Diagram-Builder-"+Normalize(version).ToString(3)+"-Windows-x64.zip";

    public static Uri ValidateAssetUrl(string value,string tag,string assetName)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||!IsHttps(uri)||
            !uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||uri.Query.Length!=0||uri.Fragment.Length!=0)
            throw new UpdateException("The update asset URL is not an approved GitHub release URL.");
        string expected="/"+Repository+"/releases/download/"+Uri.EscapeDataString(tag)+"/"+Uri.EscapeDataString(assetName);
        if(!uri.AbsolutePath.Equals(expected,StringComparison.Ordinal))
            throw new UpdateException("The update asset belongs to a different repository, release or filename.");
        return uri;
    }

    public static Uri ValidateReleaseUrl(string value,string tag)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||!IsHttps(uri)||
            !uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||uri.Query.Length!=0||uri.Fragment.Length!=0||
            uri.AbsolutePath!="/"+Repository+"/releases/tag/"+Uri.EscapeDataString(tag))
            throw new UpdateException("The release page belongs to a different repository or release.");
        return uri;
    }

    public static bool IsHttps(Uri uri)=>uri.IsAbsoluteUri&&uri.Scheme==Uri.UriSchemeHttps&&uri.IsDefaultPort&&uri.UserInfo.Length==0;

    public static ReleaseInfo? ParseRelease(ReadOnlyMemory<byte> json)
    {
        if(json.Length>MaximumReleaseBytes)throw new UpdateException("The release metadata is too large.");
        try
        {
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=16});
            var root=document.RootElement;
            if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return null;
            string tag=Text(root,"tag_name");var version=ParseVersion(tag);
            var releaseUrl=ValidateReleaseUrl(Text(root,"html_url"),tag);
            string notes=root.TryGetProperty("body",out var body)&&body.ValueKind==JsonValueKind.String?body.GetString()??"":"";
            if(notes.Length>100_000)notes=notes[..100_000];
            var assets=new List<ReleaseAsset>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var array=root.GetProperty("assets");
            if(array.ValueKind!=JsonValueKind.Array||array.GetArrayLength()>100)throw new UpdateException("The release asset list is invalid.");
            foreach(var item in array.EnumerateArray())
            {
                string name=Text(item,"name");
                if(!names.Add(name))throw new UpdateException("The release contains duplicate asset names.");
                // Ignore unrelated assets, but never accept an unrelated URL for the updater's own files.
                if(name!=ManifestName&&name!=PackageName(version))continue;
                long size=item.GetProperty("size").GetInt64();
                if(size<=0||size>(name==ManifestName?MaximumManifestBytes:MaximumPackageBytes))throw new UpdateException("A release asset has an invalid size.");
                assets.Add(new(name,ValidateAssetUrl(Text(item,"browser_download_url"),tag,name),size));
            }
            var manifest=assets.SingleOrDefault(a=>a.Name==ManifestName)??throw new UpdateException("This release has no update manifest.");
            if(!assets.Any(a=>a.Name==PackageName(version)))throw new UpdateException("This release has no supported Windows update package.");
            return new(version,tag,notes,releaseUrl,manifest,assets);
        }
        catch(UpdateException){throw;}
        catch(Exception ex)when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {throw new UpdateException("The GitHub release metadata is invalid.",ex);}
    }

    internal static string Text(JsonElement element,string name)
    {
        string? value=element.GetProperty(name).GetString();
        if(string.IsNullOrWhiteSpace(value))throw new UpdateException("The update metadata is missing "+name+".");
        return value;
    }
    internal static string Hash(string value)
    {
        if(!Regex.IsMatch(value,@"\A[0-9a-fA-F]{64}\z"))throw new UpdateException("The update checksum is invalid.");
        return value.ToLowerInvariant();
    }
}

public sealed record UpdateManifest(Version Version,string PackageName,Uri PackageUrl,long PackageSize,
    string PackageSha256,long ExecutableSize,string ExecutableSha256)
{
    public static UpdateManifest Parse(ReadOnlyMemory<byte> json,string releaseTag)
    {
        if(json.Length>UpdateProtocol.MaximumManifestBytes)throw new UpdateException("The update manifest is too large.");
        try
        {
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=8});
            var root=document.RootElement;
            if(root.GetProperty("schema_version").GetInt32()!=1||UpdateProtocol.Text(root,"product")!=UpdateProtocol.Product||
                UpdateProtocol.Text(root,"architecture")!="x64")throw new UpdateException("The update manifest is for a different application or platform.");
            var version=UpdateProtocol.ParseVersion(UpdateProtocol.Text(root,"version"));
            if(version!=UpdateProtocol.ParseVersion(releaseTag))throw new UpdateException("The manifest version does not match the release tag.");
            var package=root.GetProperty("package");var executable=root.GetProperty("executable");
            string name=UpdateProtocol.Text(package,"name");
            if(name!=UpdateProtocol.PackageName(version)||UpdateProtocol.Text(executable,"path")!=UpdateProtocol.ExecutableName)
                throw new UpdateException("The update manifest contains an unexpected package or executable name.");
            long packageSize=package.GetProperty("size").GetInt64(),exeSize=executable.GetProperty("size").GetInt64();
            if(packageSize<=0||packageSize>UpdateProtocol.MaximumPackageBytes||exeSize<=0||exeSize>UpdateProtocol.MaximumExecutableBytes)
                throw new UpdateException("The update manifest contains an invalid file size.");
            return new(version,name,UpdateProtocol.ValidateAssetUrl(UpdateProtocol.Text(package,"url"),releaseTag,name),packageSize,
                UpdateProtocol.Hash(UpdateProtocol.Text(package,"sha256")),exeSize,UpdateProtocol.Hash(UpdateProtocol.Text(executable,"sha256")));
        }
        catch(UpdateException){throw;}
        catch(Exception ex)when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {throw new UpdateException("The update manifest is invalid.",ex);}
    }
    public UpdateOffer ToOffer(string notes,Uri releaseUrl)=>new(Version,notes,releaseUrl,PackageUrl,PackageName,PackageSize,PackageSha256,ExecutableSize,ExecutableSha256);
}
