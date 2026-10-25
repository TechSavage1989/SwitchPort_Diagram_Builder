using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using SwitchportUpdater;

namespace SwitchportDiagramBuilder;

internal sealed class AppUpdateController : IDisposable
{
    readonly MainForm owner;
    readonly Func<bool> isBusy;
    readonly Action<object> send;
    readonly Func<string?> saveSession;
    readonly string[] arguments;
    readonly UpdateService service=new();
    readonly CancellationTokenSource lifetime=new();
    readonly SemaphoreSlim operation=new(1,1);
    readonly System.Windows.Forms.Timer timer=new(){Interval=5000};
    readonly string directory;
    UpdatePreferences preferences=new();
    UpdateOffer? offer;
    StagedUpdate? staged;
    string status="Updates are checked automatically. You choose when to download and restart.";
    string activity="";
    double progress;
    int automaticFailures;
    bool ready;
    public bool Installing {get;private set;}
    public static Version CurrentVersion=>new(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)??"0.0.0");
    public static string CurrentVersionText=>CurrentVersion.ToString(3);

    public AppUpdateController(MainForm owner,Func<bool> isBusy,Action<object> send,Func<string?> saveSession,string[] arguments)
    {
        this.owner=owner;this.isBusy=isBusy;this.send=send;this.saveSession=saveSession;this.arguments=arguments;
        directory=UpdatePaths.InstallRoot(Environment.ProcessPath??throw new InvalidOperationException("Application path is unavailable."));
        Directory.CreateDirectory(directory);
        try{preferences=JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(Path.Combine(directory,"preferences.json")))??new();}catch(IOException){}catch(JsonException){}catch(UnauthorizedAccessException){}
        try
        {
            var saved=JsonSerializer.Deserialize<StagedUpdate>(File.ReadAllText(Path.Combine(directory,"staged.json")));
            if(saved?.Offer!=null)
            {
                UpdateService.ValidateOffer(saved.Offer);
                if(saved.Offer.Version>CurrentVersion&&UpdatePaths.IsInside(saved.ExecutablePath,Path.Combine(directory,"staging")))
                {
                    ExecutableValidator.Validate(saved.ExecutablePath,saved.Offer.Version,saved.Offer.ExecutableSha256,saved.Offer.ExecutableSize);
                    staged=saved;offer=saved.Offer;status="Version "+offer.VersionText+" is downloaded and verified. Install when you are ready to restart.";
                }
            }
        }catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException or InvalidDataException or ArgumentException or UpdateException or NullReferenceException){status="A previous download is unavailable. Check for updates to download it again.";}
        timer.Tick+=async(_,_)=>
        {
            timer.Stop();
            try
            {
                if(!preferences.AutomaticChecks)timer.Interval=6*60*60*1000;
                else if(isBusy()||Installing)timer.Interval=30000;
                else await CheckAsync(false);
            }
            finally{if(!owner.IsDisposed)timer.Start();}
        };
    }

    public void Ready()
    {
        if(ready)return;ready=true;Publish();timer.Start();
    }

    public void Publish(bool showReminder=false)=>send(new{
        type="updateState",currentVersion=CurrentVersionText,automaticChecks=preferences.AutomaticChecks,
        availableVersion=offer?.VersionText??"",notes=offer?.Notes??"",releaseUrl=offer?.ReleaseUrl.AbsoluteUri??"",
        status,checking=activity=="checking",downloading=activity=="downloading",installing=Installing,
        progress,appBusy=isBusy(),downloaded=staged!=null,canDownload=offer!=null&&staged==null&&activity.Length==0&&!Installing,
        canInstall=staged!=null&&activity.Length==0&&!Installing&&!isBusy(),
        reminder=showReminder&&preferences.LaterUntilUtc<=DateTimeOffset.UtcNow
    });

    public async Task CheckAsync(bool manual)
    {
        if(Installing||!await operation.WaitAsync(0,lifetime.Token))return;
        try
        {
            activity="checking";status="Checking GitHub for a newer stable version...";Publish();
            var found=await service.CheckAsync(CurrentVersion,lifetime.Token);
            if(found==null)
            {
                offer=staged?.Offer;status=offer==null?"You have the latest published version.":"The verified download is ready to install.";
            }
            else
            {
                if(staged?.Offer.Version!=found.Version)staged=null;
                offer=found;status=staged!=null?"Version "+found.VersionText+" is downloaded and verified.":"Version "+found.VersionText+" is available. Review the changes, then download when ready.";
            }
            activity="";Publish(offer!=null&&(manual||preferences.AutomaticChecks));
            automaticFailures=0;if(!manual)timer.Interval=6*60*60*1000;
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested){}
        catch(Exception ex){status="Update check failed: "+ex.Message;activity="";if(!manual&&!owner.IsDisposed){int[] delays={30000,120000,900000,6*60*60*1000};timer.Interval=delays[Math.Min(automaticFailures++,delays.Length-1)];}Publish();}
        finally{activity="";operation.Release();}
    }

    public async Task DownloadAsync()
    {
        if(offer==null||Installing||!await operation.WaitAsync(0,lifetime.Token))return;
        try
        {
            activity="downloading";progress=0;status="Downloading and verifying version "+offer.VersionText+"...";Publish();
            var reporter=new Progress<UpdateProgress>(p=>{progress=p.Percent;Publish();});
            staged=await service.DownloadAsync(offer,Path.Combine(directory,"staging"),reporter,lifetime.Token);
            AtomicJson(Path.Combine(directory,"staged.json"),staged);
            status="Version "+offer.VersionText+" is downloaded and verified. Reports and settings will be retained.";
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested){}
        catch(Exception ex){staged=null;status="Update download failed: "+ex.Message;}
        finally{activity="";operation.Release();Publish();}
    }

    public void SetAutomaticChecks(bool enabled)
    {
        preferences.AutomaticChecks=enabled;SavePreferences();if(enabled&&ready){timer.Stop();timer.Interval=5000;timer.Start();}Publish();
    }
    public void Later(){preferences.LaterUntilUtc=DateTimeOffset.UtcNow.AddHours(24);SavePreferences();Publish();}
    void SavePreferences()=>AtomicJson(Path.Combine(directory,"preferences.json"),preferences);
    public void Review()
    {
        if(offer!=null)Process.Start(new ProcessStartInfo(offer.ReleaseUrl.AbsoluteUri){UseShellExecute=true});
    }

    public async Task InstallAsync()
    {
        if(staged==null||Installing||!await operation.WaitAsync(0,lifetime.Token))return;
        bool handedOff=false;ApplyUpdateRequest? request=null;
        try
        {
            if(isBusy()){status="Finish the report build or save and close the open edit dialog before installing.";return;}
            Installing=true;Publish();
            var verified=staged;
            if(MessageBox.Show(owner,"Install version "+verified.Offer.VersionText+" and restart now?\n\nThe loaded configuration, saved device details, report logo, output folder and existing reports will be retained.","Install update",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            if(isBusy()){status="A report build or edit dialog is open. Finish it before installing.";return;}
            status="Preparing the verified update and restart...";Publish();
            ExecutableValidator.Validate(verified.ExecutablePath,verified.Offer.Version,verified.Offer.ExecutableSha256,verified.Offer.ExecutableSize);
            string? session=saveSession();
            request=await Task.Run(()=>UpdateApplyHost.Prepare(verified,arguments,session),lifetime.Token);
            using var helper=UpdateApplyHost.StartHelper(request);
            var until=DateTimeOffset.UtcNow.AddSeconds(20);
            while(DateTimeOffset.UtcNow<until&&!File.Exists(request.HelperReadyPath)&&!helper.HasExited)await Task.Delay(100,lifetime.Token);
            if(!File.Exists(request.HelperReadyPath))throw new IOException("The update helper could not start. The current app has not been changed. See "+request.LogPath);
            if(File.ReadAllText(request.HelperReadyPath)!=request.Nonce)throw new InvalidDataException("Update handoff could not be verified.");
            handedOff=true;
            owner.Close();
        }
        catch(Exception ex)
        {
            if(request!=null)UpdateApplyHost.Cancel(request);
            status="The update could not be installed: "+ex.Message;
            MessageBox.Show(owner,status,"Update not installed",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        finally{if(!handedOff)Installing=false;operation.Release();Publish();}
    }

    internal static void AtomicJson<T>(string path,T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(value));File.Move(temporary,path,true);}finally{File.Delete(temporary);}
    }
    public void Dispose(){timer.Stop();timer.Dispose();lifetime.Cancel();lifetime.Dispose();service.Dispose();}
    sealed class UpdatePreferences {public bool AutomaticChecks{get;set;}=true;public DateTimeOffset LaterUntilUtc{get;set;}}
}
