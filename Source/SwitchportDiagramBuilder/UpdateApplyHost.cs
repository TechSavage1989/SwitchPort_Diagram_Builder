using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SwitchportUpdater;

namespace SwitchportDiagramBuilder;

internal static class UpdatePaths
{
    public static string InstallRoot(string executable)=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SwitchportDiagramBuilder","Updates",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(executable).ToUpperInvariant())))[..24]);
    public static bool IsInside(string path,string directory)
    {
        string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root,StringComparison.OrdinalIgnoreCase);
    }
    public static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
}

internal sealed record ApplyUpdateRequest
{
    public string RequestPath{get;init;}="";
    public string TargetPath{get;init;}="";
    public string StagedPath{get;init;}="";
    public string ExpectedVersion{get;init;}="";
    public string ExpectedSha256{get;init;}="";
    public long ExpectedSize{get;init;}
    public string OriginalSha256{get;init;}="";
    public string BackupPath{get;init;}="";
    public string HelperPath{get;init;}="";
    public string HelperReadyPath{get;init;}="";
    public string CancelPath{get;init;}="";
    public string HealthPath{get;init;}="";
    public string Nonce{get;init;}="";
    public string LogPath{get;init;}="";
    public string SessionPath{get;init;}="";
    public string WorkingDirectory{get;init;}="";
    public string[] Arguments{get;init;}=Array.Empty<string>();
    public int ParentPid{get;init;}
    public long ParentStartTicks{get;init;}
    public bool SelfTest{get;init;}
    public bool SelfTestHealthy{get;init;}
    public string TestReopenedPath{get;init;}="";
    public string ResultPath{get;init;}="";
}

internal static class UpdateApplyHost
{
    public static ApplyUpdateRequest Prepare(StagedUpdate staged,string[] arguments,string? session)
    {
        string target=Path.GetFullPath(Environment.ProcessPath??throw new IOException("Application path is unavailable."));
        EnsureOnlyInstance(target);
        string probe=Path.Combine(Path.GetDirectoryName(target)!,".diagram-update-"+Guid.NewGuid().ToString("N")+".tmp");
        try{using(File.Create(probe)){}File.Delete(probe);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){throw new IOException("The app folder is not writable. Move the portable app to a writable folder and try again.",ex);}
        string root=UpdatePaths.InstallRoot(target);string nonce=Guid.NewGuid().ToString("N");
        string helperDirectory=Path.Combine(root,"installer-"+nonce);Directory.CreateDirectory(helperDirectory);
        string helper=Path.Combine(helperDirectory,"SwitchportDiagramBuilder.exe");File.Copy(target,helper,false);
        string original=UpdatePaths.Hash(target);
        if(UpdatePaths.Hash(helper)!=original)throw new InvalidDataException("The update helper copy failed verification.");
        using var process=Process.GetCurrentProcess();
        var request=new ApplyUpdateRequest{RequestPath=Path.Combine(helperDirectory,"request.json"),TargetPath=target,StagedPath=Path.GetFullPath(staged.ExecutablePath),ExpectedVersion=staged.Offer.VersionText,ExpectedSha256=staged.Offer.ExecutableSha256,ExpectedSize=staged.Offer.ExecutableSize,OriginalSha256=original,BackupPath=Path.Combine(root,"previous.exe"),HelperPath=helper,HelperReadyPath=Path.Combine(helperDirectory,"ready.txt"),CancelPath=Path.Combine(helperDirectory,"cancel.txt"),HealthPath=Path.Combine(root,"health-"+nonce+".json"),Nonce=nonce,LogPath=Path.Combine(root,"update.log"),SessionPath=session??"",WorkingDirectory=Environment.CurrentDirectory,Arguments=arguments,ParentPid=Environment.ProcessId,ParentStartTicks=process.StartTime.ToUniversalTime().Ticks,ResultPath=Path.Combine(helperDirectory,"result.txt")};
        AppUpdateController.AtomicJson(request.RequestPath,request);return request;
    }
    public static Process StartHelper(ApplyUpdateRequest request)
    {
        var start=new ProcessStartInfo(request.HelperPath){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(request.HelperPath)!};
        start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(request.RequestPath);
        return Process.Start(start)??throw new IOException("The update helper could not start.");
    }
    static void EnsureOnlyInstance(string target)
    {
        foreach(var process in Process.GetProcesses())
        {
            using(process)
            {
                if(process.Id==Environment.ProcessId)continue;
                try{if(string.Equals(process.MainModule?.FileName,target,StringComparison.OrdinalIgnoreCase))throw new IOException("Close other running copies of this app before installing an update.");}
                catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
            }
        }
    }
    public static async Task<int> RunAsync(string path)
    {
        ApplyUpdateRequest? request=null;FileInstallTransaction? transaction=null;Process? updated=null;
        bool replaced=false,committed=false,validatedRequest=false,parentExited=false;
        try
        {
            if(!Path.IsPathFullyQualified(path)||new FileInfo(path).Length>65536)throw new InvalidDataException("Invalid update request.");
            request=JsonSerializer.Deserialize<ApplyUpdateRequest>(File.ReadAllText(path))??throw new InvalidDataException("Empty update request.");
            ValidateRequest(path,request);
            validatedRequest=true;
            using(var installLock=new FileStream(Path.Combine(UpdatePaths.InstallRoot(request.TargetPath),"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                ExecutableValidator.Validate(request.StagedPath,Version.Parse(request.ExpectedVersion),request.ExpectedSha256,request.ExpectedSize);
                Log(request,"Verified staged executable. Waiting for the current process to exit.");
                AtomicText(request.HelperReadyPath,request.Nonce);
                if(request.ParentPid>0)
                {
                    try
                    {
                        using var parent=Process.GetProcessById(request.ParentPid);
                        if(parent.StartTime.ToUniversalTime().Ticks==request.ParentStartTicks)
                        {
                            var until=DateTimeOffset.UtcNow.AddSeconds(120);
                            while(!parent.HasExited&&DateTimeOffset.UtcNow<until)
                            {
                                if(File.Exists(request.CancelPath))throw new OperationCanceledException("The update handoff was cancelled before restart.");
                                await Task.Delay(200);
                            }
                            if(!parent.HasExited)throw new TimeoutException("The current application did not close. Nothing was replaced.");
                        }
                    }catch(ArgumentException){}
                }
                parentExited=true;
                if(File.Exists(request.CancelPath))throw new OperationCanceledException("The update handoff was cancelled before installation.");
                EnsureOnlyInstance(request.TargetPath);
                if(UpdatePaths.Hash(request.TargetPath)!=request.OriginalSha256)throw new InvalidDataException("The installed executable changed after the update was prepared.");
                transaction=FileInstallTransaction.Begin(request.StagedPath,request.TargetPath,request.BackupPath,request.ExpectedSha256,request.OriginalSha256);
                replaced=true;
                AppUpdateController.AtomicJson(Path.Combine(UpdatePaths.InstallRoot(request.TargetPath),"previous.json"),new PreviousExecutableMetadata(request.TargetPath,request.BackupPath,transaction.OriginalSha256,FileVersionInfo.GetVersionInfo(request.BackupPath).FileVersion??"",request.ExpectedVersion,DateTimeOffset.UtcNow));
                Log(request,"Installed verified executable and recorded rollback metadata. Waiting for visible startup readiness.");
                updated=Launch(request,false);
                var deadline=DateTimeOffset.UtcNow.AddSeconds(request.SelfTest?10:90);
                while(DateTimeOffset.UtcNow<deadline)
                {
                    if(Healthy(request,updated.Id))
                    {
                        transaction.Commit();committed=true;Log(request,"New version reported visible startup readiness. Previous executable retained for rollback.");
                        File.WriteAllText(request.ResultPath,"healthy");TryDelete(Path.Combine(UpdatePaths.InstallRoot(request.TargetPath),"staged.json"));TryDelete(request.HealthPath);TryDelete(request.SessionPath);CleanupStage(request);
                        return 0;
                    }
                    if(updated.HasExited)throw new IOException("The updated application exited before its interface was ready.");
                    await Task.Delay(200);
                }
                throw new TimeoutException("The updated interface did not become ready within 90 seconds.");
            }
        }
        catch(Exception ex)
        {
            if(request!=null&&validatedRequest)
            {
                Log(request,"Update failed: "+ex.Message);
                if(replaced&&!committed&&transaction!=null)
                {
                    try
                    {
                        if(updated!=null&&!updated.HasExited)
                        {
                            updated.CloseMainWindow();
                            using var closeTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            try{await updated.WaitForExitAsync(closeTimeout.Token);}catch(OperationCanceledException){updated.Kill(true);await updated.WaitForExitAsync();}
                        }
                        transaction.Rollback();Log(request,"Previous executable restored after failed startup.");
                        using var previous=Launch(request,true);
                        File.WriteAllText(request.ResultPath,"rolled-back");
                    }
                    catch(Exception rollback){Log(request,"Automatic restore could not finish: "+rollback.Message+". Backup: "+request.BackupPath);if(!request.SelfTest)MessageBox.Show("The update did not finish. Your previous executable is saved at:\n"+request.BackupPath+"\n\nSee "+request.LogPath,"Update recovery",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
                }
                else if(!request.SelfTest&&parentExited&&!File.Exists(request.CancelPath))
                {
                    // The parent normally stays open until helper readiness. If it
                    // already closed, reopen the unchanged executable after failure.
                    try{EnsureOnlyInstance(request.TargetPath);using var previous=Launch(request,true);}catch{}
                }
            }
            return 1;
        }
        finally{updated?.Dispose();}
    }
    static void ValidateRequest(string path,ApplyUpdateRequest r)
    {
        string root=UpdatePaths.InstallRoot(r.TargetPath);
        if(r.Nonce.Length!=32||r.Nonce.Any(c=>!Uri.IsHexDigit(c))||(!r.SelfTest&&r.ParentPid<=0)||!Path.IsPathFullyQualified(r.TargetPath)||!r.TargetPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!UpdatePaths.IsInside(path,root)||!string.Equals(Path.GetFullPath(path),Path.GetFullPath(r.RequestPath),StringComparison.OrdinalIgnoreCase)||!UpdatePaths.IsInside(r.HelperPath,root)||!UpdatePaths.IsInside(r.HelperReadyPath,root)||!UpdatePaths.IsInside(r.ResultPath,root)||!string.Equals(Path.GetFullPath(r.BackupPath),Path.Combine(root,"previous.exe"),StringComparison.OrdinalIgnoreCase)||!string.Equals(Path.GetFullPath(r.HealthPath),Path.Combine(root,"health-"+r.Nonce+".json"),StringComparison.OrdinalIgnoreCase)||!string.Equals(Path.GetFullPath(r.LogPath),Path.Combine(root,"update.log"),StringComparison.OrdinalIgnoreCase)||!string.Equals(Environment.ProcessPath,r.HelperPath,StringComparison.OrdinalIgnoreCase)||UpdatePaths.Hash(r.HelperPath)!=r.OriginalSha256)throw new InvalidDataException("The update handoff is invalid.");
        if(!UpdatePaths.IsInside(r.StagedPath,Path.Combine(root,"staging")))throw new InvalidDataException("The staged update is outside this installation's update folder.");
        if(!UpdatePaths.IsInside(r.CancelPath,Path.GetDirectoryName(r.RequestPath)!))throw new InvalidDataException("Invalid update cancellation handoff.");
        if(r.SessionPath.Length>0&&!UpdatePaths.IsInside(r.SessionPath,root))throw new InvalidDataException("Invalid restart session path.");
        if(r.Arguments.Any(a=>a.StartsWith("--apply-update",StringComparison.Ordinal)||a.StartsWith("--updater-self",StringComparison.Ordinal)||a.StartsWith("--update-health",StringComparison.Ordinal)||a.StartsWith("--restore-update-session",StringComparison.Ordinal)))throw new InvalidDataException("Unexpected update launch arguments.");
        if(r.SelfTest&&(!Path.GetFileName(Path.GetDirectoryName(r.TargetPath)!).StartsWith("diagram-updater-test-",StringComparison.Ordinal)||!UpdatePaths.IsInside(r.TargetPath,Path.GetTempPath())))throw new InvalidDataException("Invalid updater test directory.");
    }
    static Process Launch(ApplyUpdateRequest request,bool rollback)
    {
        var start=new ProcessStartInfo(request.TargetPath){UseShellExecute=false,WorkingDirectory=Directory.Exists(request.WorkingDirectory)?request.WorkingDirectory:Path.GetDirectoryName(request.TargetPath)!};
        if(request.SelfTest)
        {
            start.ArgumentList.Add(rollback?"--updater-selftest-reopened":"--updater-selftest-child");
            if(rollback)start.ArgumentList.Add(request.TestReopenedPath);
            else {start.ArgumentList.Add(request.HealthPath);start.ArgumentList.Add(request.Nonce);start.ArgumentList.Add(request.SelfTestHealthy?"healthy":"unhealthy");}
        }
        else
        {
            foreach(string arg in request.Arguments)start.ArgumentList.Add(arg);
            if(request.SessionPath.Length>0){start.ArgumentList.Add("--restore-update-session");start.ArgumentList.Add(request.SessionPath);}
            if(!rollback){start.ArgumentList.Add("--update-health");start.ArgumentList.Add(request.HealthPath);start.ArgumentList.Add(request.Nonce);}
        }
        return Process.Start(start)??throw new IOException("The application could not restart.");
    }
    static bool Healthy(ApplyUpdateRequest r,int pid)
    {
        try
        {
            if(!File.Exists(r.HealthPath))return false;
            var h=JsonSerializer.Deserialize<StartupHealth>(File.ReadAllText(r.HealthPath));
            return h?.Nonce==r.Nonce&&h.Pid==pid&&h.Version==r.ExpectedVersion;
        }catch(IOException){return false;}catch(JsonException){return false;}
    }
    static void Log(ApplyUpdateRequest r,string text){try{File.AppendAllText(r.LogPath,DateTimeOffset.UtcNow.ToString("O")+" "+text+Environment.NewLine);}catch{}}
    static void AtomicText(string path,string text){string temporary=path+".tmp";File.WriteAllText(temporary,text);File.Move(temporary,path,true);}
    static void TryDelete(string path){try{File.Delete(path);}catch{}}
    public static void Cancel(ApplyUpdateRequest request){try{AtomicText(request.CancelPath,request.Nonce);}catch{}}
    static void CleanupStage(ApplyUpdateRequest r)
    {
        try
        {
            string root=Path.Combine(UpdatePaths.InstallRoot(r.TargetPath),"staging");
            string ready=Path.GetDirectoryName(r.StagedPath)!,work=Path.GetDirectoryName(ready)!;
            string name=Path.GetFileName(work);
            if(Path.GetFileName(ready)!="ready"||!name.StartsWith("stage-",StringComparison.Ordinal)||!Guid.TryParseExact(name[6..],"N",out _)||!string.Equals(Path.GetDirectoryName(work),root,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(r.StagedPath)!="SwitchportDiagramBuilder.exe")return;
            File.Delete(r.StagedPath);File.Delete(Path.Combine(work,UpdateProtocol.PackageName(Version.Parse(r.ExpectedVersion))));File.Delete(Path.Combine(work,"package.download"));
            if(!Directory.EnumerateFileSystemEntries(ready).Any())Directory.Delete(ready);
            if(!Directory.EnumerateFileSystemEntries(work).Any())Directory.Delete(work);
        }catch{}
    }

    public static void CleanupOldHelpers()
    {
        string target=Environment.ProcessPath??"";if(target.Length==0)return;string root=UpdatePaths.InstallRoot(target);
        if(!Directory.Exists(root))return;
        // Keep completed journals too if the one retained rollback executable
        // does not have a persistent, independently verified metadata record.
        try
        {
            string metadata=Path.Combine(root,"previous.json");
            if(!File.Exists(metadata)||new FileInfo(metadata).Length>8192)return;
            var previous=JsonSerializer.Deserialize<PreviousExecutableMetadata>(File.ReadAllText(metadata));
            if(previous==null||previous.TargetPath!=target||previous.BackupPath!=Path.Combine(root,"previous.exe")||!File.Exists(previous.BackupPath)||UpdatePaths.Hash(previous.BackupPath)!=previous.OriginalSha256)return;
        }catch{return;}
        foreach(string directory in Directory.EnumerateDirectories(root,"installer-*",SearchOption.TopDirectoryOnly))
        {
            try
            {
                string suffix=Path.GetFileName(directory)[10..];
                if(!Guid.TryParseExact(suffix,"N",out _)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.GetLastWriteTimeUtc(directory)>DateTime.UtcNow.AddMinutes(-10))continue;
                string path=Path.Combine(directory,"request.json");if(!File.Exists(path)||new FileInfo(path).Length>65536)continue;
                var r=JsonSerializer.Deserialize<ApplyUpdateRequest>(File.ReadAllText(path));
                if(r==null||r.TargetPath!=target||r.RequestPath!=path||r.HelperPath!=Path.Combine(directory,"SwitchportDiagramBuilder.exe")||r.ResultPath!=Path.Combine(directory,"result.txt")||r.HelperReadyPath!=Path.Combine(directory,"ready.txt")||r.CancelPath!=Path.Combine(directory,"cancel.txt")||r.Nonce!=suffix||!File.Exists(r.HelperPath)||UpdatePaths.Hash(r.HelperPath)!=r.OriginalSha256)continue;
                if(!File.Exists(r.ResultPath))continue;
                string outcome=File.ReadAllText(r.ResultPath);
                if(outcome!="healthy"&&outcome!="rolled-back")continue;
                bool running=false;
                foreach(var process in Process.GetProcesses())using(process){try{if(string.Equals(process.MainModule?.FileName,r.HelperPath,StringComparison.OrdinalIgnoreCase)){running=true;break;}}catch{}}
                if(running)continue;
                var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"request.json","SwitchportDiagramBuilder.exe","ready.txt","cancel.txt","result.txt"};
                if(Directory.EnumerateFileSystemEntries(directory).Any(p=>Directory.Exists(p)||!allowed.Contains(Path.GetFileName(p))))continue;
                foreach(string file in Directory.EnumerateFiles(directory))File.Delete(file);
                Directory.Delete(directory);
            }catch{}
        }
    }
}

internal sealed record StartupHealth(string Nonce,int Pid,string Version);
internal sealed record PreviousExecutableMetadata(string TargetPath,string BackupPath,string OriginalSha256,string OriginalFileVersion,string CandidateVersion,DateTimeOffset CreatedUtc);
