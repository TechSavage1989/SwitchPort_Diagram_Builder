using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace SwitchportUpdater;

/// <summary>Checks one public GitHub release feed. It never installs or starts a download automatically.</summary>
public sealed class UpdateService : IDisposable
{
    readonly HttpClient client;
    readonly bool ownsClient;
    public UpdateService(HttpClient? client=null)
    {
        ownsClient=client==null;
        this.client=client??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
    }

    public async Task<UpdateOffer?> CheckAsync(Version currentVersion,CancellationToken cancellationToken=default)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            using var response=await GetAsync(UpdateProtocol.LatestReleaseUrl,true,deadline.Token).ConfigureAwait(false);
            if(response.StatusCode==HttpStatusCode.NotFound)return null;
            EnsureSuccess(response);
            byte[] releaseBytes=await ReadBoundedAsync(response,UpdateProtocol.MaximumReleaseBytes,null,deadline.Token).ConfigureAwait(false);
            var release=UpdateProtocol.ParseRelease(releaseBytes);
            if(release==null||release.Version<=UpdateProtocol.Normalize(currentVersion))return null;
            using var manifestResponse=await GetAsync(release.ManifestAsset.Url,false,deadline.Token).ConfigureAwait(false);
            EnsureSuccess(manifestResponse);
            byte[] manifestBytes=await ReadBoundedAsync(manifestResponse,UpdateProtocol.MaximumManifestBytes,release.ManifestAsset.Size,deadline.Token).ConfigureAwait(false);
            var manifest=UpdateManifest.Parse(manifestBytes,release.Tag);
            var asset=release.Assets.Single(a=>a.Name==manifest.PackageName);
            if(asset.Url!=manifest.PackageUrl||asset.Size!=manifest.PackageSize)
                throw new UpdateException("The manifest package does not match the published GitHub release asset.");
            return manifest.ToOffer(release.Notes,release.ReleaseUrl);
        }
        catch(OperationCanceledException ex)when(!cancellationToken.IsCancellationRequested)
        {throw new UpdateException("The update check timed out. Try again later.",ex);}
        catch(HttpRequestException ex){throw new UpdateException("The update service could not be reached. Your current application is unchanged.",ex);}
    }

    public async Task<StagedUpdate> DownloadAsync(UpdateOffer offer,string stageDirectory,IProgress<UpdateProgress>? progress=null,CancellationToken cancellationToken=default)
    {
        ValidateOffer(offer);
        string root=Path.GetFullPath(stageDirectory);
        Directory.CreateDirectory(root);
        string work=Path.Combine(root,"stage-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string temporary=Path.Combine(work,"package.download"),package=Path.Combine(work,offer.PackageName);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            using var response=await GetAsync(offer.PackageUrl,false,deadline.Token).ConfigureAwait(false);
            EnsureSuccess(response);
            if(response.Content.Headers.ContentLength is long length&&length!=offer.PackageSize)
                throw new UpdateException("The downloaded package size does not match its manifest.");
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using(var input=await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false))
            await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.Asynchronous|FileOptions.SequentialScan))
            {
                byte[] buffer=new byte[128*1024];long total=0;
                while(true)
                {
                    int read=await input.ReadAsync(buffer,deadline.Token).ConfigureAwait(false);
                    if(read==0)break;
                    total+=read;
                    if(total>offer.PackageSize)throw new UpdateException("The downloaded package exceeded its declared size.");
                    hash.AppendData(buffer,0,read);
                    await output.WriteAsync(buffer.AsMemory(0,read),deadline.Token).ConfigureAwait(false);
                    progress?.Report(new(total,offer.PackageSize));
                }
                if(total!=offer.PackageSize)throw new UpdateException("The update download was incomplete.");
                if(!Convert.ToHexString(hash.GetHashAndReset()).Equals(offer.PackageSha256,StringComparison.OrdinalIgnoreCase))
                    throw new UpdateException("The update package checksum did not match. The package was discarded.");
                await output.FlushAsync(deadline.Token).ConfigureAwait(false);
            }
            File.Move(temporary,package);
            deadline.Token.ThrowIfCancellationRequested();
            string executable=await Task.Run(()=>PackageValidator.Stage(package,offer,Path.Combine(work,"ready")),deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            return new(offer,executable);
        }
        catch
        {
            // Only this operation's fresh, randomly named staging directory is removed.
            try{Directory.Delete(work,true);}catch{ }
            throw;
        }
    }

    public static void ValidateOffer(UpdateOffer offer)
    {
        if(!offer.ReleaseUrl.IsAbsoluteUri)throw new UpdateException("The release URL is invalid.");
        string tag=Uri.UnescapeDataString(offer.ReleaseUrl.AbsolutePath.Split('/').Last());
        UpdateProtocol.ValidateReleaseUrl(offer.ReleaseUrl.AbsoluteUri,tag);
        if(UpdateProtocol.ParseVersion(tag)!=UpdateProtocol.Normalize(offer.Version)||offer.PackageName!=UpdateProtocol.PackageName(offer.Version))
            throw new UpdateException("The update package version is inconsistent.");
        UpdateProtocol.ValidateAssetUrl(offer.PackageUrl.AbsoluteUri,tag,offer.PackageName);
        UpdateProtocol.Hash(offer.PackageSha256);UpdateProtocol.Hash(offer.ExecutableSha256);
        if(offer.PackageSize<=0||offer.PackageSize>UpdateProtocol.MaximumPackageBytes||offer.ExecutableSize<=0||offer.ExecutableSize>UpdateProtocol.MaximumExecutableBytes)
            throw new UpdateException("The update offer contains an invalid size.");
    }

    async Task<HttpResponseMessage> GetAsync(Uri initial,bool api,CancellationToken cancellationToken)
    {
        Uri current=initial;
        for(int redirects=0;redirects<=5;redirects++)
        {
            if(!AllowedRequest(current,initial,api))throw new UpdateException("The update download was redirected to an unapproved host.");
            using var request=new HttpRequestMessage(HttpMethod.Get,current);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SwitchportDiagramBuilder-Updater","1.14.2"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(api?"application/vnd.github+json":"application/octet-stream"));
            if(api)request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version","2022-11-28");
            var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken).ConfigureAwait(false);
            Uri final=response.RequestMessage?.RequestUri??current;
            if(!AllowedRequest(final,initial,api)){response.Dispose();throw new UpdateException("The update response came from an unapproved host.");}
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location=response.Headers.Location;
                response.Dispose();
                if(location==null)throw new UpdateException("The update server returned an invalid redirect.");
                current=location.IsAbsoluteUri?location:new Uri(current,location);
                continue;
            }
            return response;
        }
        throw new UpdateException("The update server returned too many redirects.");
    }
    static bool AllowedRequest(Uri uri,Uri initial,bool api)
    {
        if(!UpdateProtocol.IsHttps(uri))return false;
        if(api)return uri==UpdateProtocol.LatestReleaseUrl;
        if(uri==initial)return true;
        // GitHub uses short-lived signed URLs on these exact hosts for release assets.
        return uri.Host.Equals("release-assets.githubusercontent.com",StringComparison.OrdinalIgnoreCase)||
            uri.Host.Equals("objects.githubusercontent.com",StringComparison.OrdinalIgnoreCase);
    }
    static void EnsureSuccess(HttpResponseMessage response)
    {
        if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new UpdateException("GitHub is temporarily limiting update requests. Try again later.");
        if(!response.IsSuccessStatusCode)throw new UpdateException("GitHub could not provide the update (HTTP "+(int)response.StatusCode+").");
    }
    static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response,int maximum,long? expected,CancellationToken cancellationToken)
    {
        if(response.Content.Headers.ContentLength is long length&&(length>maximum||expected.HasValue&&length!=expected))
            throw new UpdateException("The update metadata size is invalid.");
        await using var input=await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output=new MemoryStream();byte[] buffer=new byte[16*1024];
        while(true)
        {
            int read=await input.ReadAsync(buffer,cancellationToken).ConfigureAwait(false);if(read==0)break;
            if(output.Length+read>maximum)throw new UpdateException("The update metadata exceeded its size limit.");
            output.Write(buffer,0,read);
        }
        if(expected.HasValue&&output.Length!=expected)throw new UpdateException("The update metadata download was incomplete.");
        return output.ToArray();
    }
    public void Dispose(){if(ownsClient)client.Dispose();}
}
