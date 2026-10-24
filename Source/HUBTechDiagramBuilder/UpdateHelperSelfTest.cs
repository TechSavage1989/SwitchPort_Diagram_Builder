using System.Diagnostics;

namespace HUBTechDiagramBuilder;

// Runs only on explicit developer invocation. The production readiness path
// always waits for the visible WebView interface and never uses these children.
internal static class UpdateHelperSelfTest
{
    public static int Child(string[] args)
    {
        if(args.Length==4&&args[0]=="--updater-selftest-child")
        {
            if(args[3]!="healthy")return 31;
            UpdateLaunchOptions.Parse(new[]{"--update-health",args[1],args[2]}).SignalHealthy();return 0;
        }
        if(args.Length==2&&args[0]=="--updater-selftest-reopened")
        {
            string root=UpdatePaths.InstallRoot(Environment.ProcessPath!);
            if(!UpdatePaths.IsInside(args[1],root)||Path.GetFileName(args[1])!="selftest-reopened.txt")return 32;
            File.WriteAllText(args[1],"reopened");return 0;
        }
        return 33;
    }
    public static async Task<int> RunAsync()
    {
        if(!OperatingSystem.IsWindows())return 34;
        string executable=Environment.ProcessPath??throw new IOException("Executable path is unavailable.");
        foreach(bool healthy in new[]{true,false})
        {
            string testDirectory=Path.Combine(Path.GetTempPath(),"diagram-updater-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(testDirectory);
            string target=Path.Combine(testDirectory,"HUBTechDiagramBuilder.exe");File.Copy(executable,target);
            string root=UpdatePaths.InstallRoot(target),nonce=Guid.NewGuid().ToString("N"),helperDirectory=Path.Combine(root,"installer-"+nonce),stage=Path.Combine(root,"staging","stage-"+Guid.NewGuid().ToString("N"),"ready");
            Directory.CreateDirectory(helperDirectory);Directory.CreateDirectory(stage);
            string candidate=Path.Combine(stage,"HUBTechDiagramBuilder.exe"),helper=Path.Combine(helperDirectory,"HUBTechDiagramBuilder.exe");File.Copy(executable,candidate);File.Copy(executable,helper);
            string hash=UpdatePaths.Hash(target),session=Path.Combine(root,"session-"+nonce+".json");File.WriteAllText(session,"preserved configuration fixture");
            var r=new ApplyUpdateRequest{RequestPath=Path.Combine(helperDirectory,"request.json"),TargetPath=target,StagedPath=candidate,ExpectedVersion=AppUpdateController.CurrentVersionText,ExpectedSha256=hash,ExpectedSize=new FileInfo(candidate).Length,OriginalSha256=hash,BackupPath=Path.Combine(root,"previous.exe"),HelperPath=helper,HelperReadyPath=Path.Combine(helperDirectory,"ready.txt"),CancelPath=Path.Combine(helperDirectory,"cancel.txt"),HealthPath=Path.Combine(root,"health-"+nonce+".json"),Nonce=nonce,LogPath=Path.Combine(root,"update.log"),SessionPath=session,WorkingDirectory=testDirectory,ResultPath=Path.Combine(helperDirectory,"result.txt"),SelfTest=true,SelfTestHealthy=healthy,TestReopenedPath=Path.Combine(helperDirectory,"selftest-reopened.txt")};
            AppUpdateController.AtomicJson(r.RequestPath,r);
            using var process=UpdateApplyHost.StartHelper(r);
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45)))await process.WaitForExitAsync(timeout.Token);
            string expected=healthy?"healthy":"rolled-back";
            if(!File.Exists(r.ResultPath)||File.ReadAllText(r.ResultPath)!=expected||!File.Exists(r.BackupPath)||UpdatePaths.Hash(r.BackupPath)!=hash||UpdatePaths.Hash(target)!=hash)throw new InvalidDataException("Updater helper "+expected+" transaction failed. See "+r.LogPath);
            if(healthy&&process.ExitCode!=0)throw new InvalidDataException("Healthy updater helper returned a failure.");
            if(healthy&&File.Exists(session))throw new InvalidDataException("Committed update did not clean its saved restart selection.");
            if(!healthy)
            {
                var until=DateTimeOffset.UtcNow.AddSeconds(5);while(!File.Exists(r.TestReopenedPath)&&DateTimeOffset.UtcNow<until)await Task.Delay(100);
                if(!File.Exists(r.TestReopenedPath)||!File.Exists(session)||File.ReadAllText(session)!="preserved configuration fixture")throw new InvalidDataException("Rollback did not reopen the previous executable with its restart selection retained.");
            }
            // An invalid handoff must not create a caller-selected log or execute
            // recovery from any paths contained in that unvalidated request.
            string externalLog=Path.Combine(testDirectory,"untrusted.log");
            var invalid=r with{Nonce="invalid",LogPath=externalLog};AppUpdateController.AtomicJson(r.RequestPath,invalid);
            using var rejected=UpdateApplyHost.StartHelper(invalid);
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20)))await rejected.WaitForExitAsync(timeout.Token);
            if(rejected.ExitCode==0||File.Exists(externalLog))throw new InvalidDataException("Malformed update request was not rejected before side effects.");
            Console.WriteLine("PASS native updater helper: "+expected+", checksum/backup, nonce, session retention and malformed-request isolation.");
            try{Directory.Delete(root,true);}catch{}
            try{Directory.Delete(testDirectory,true);}catch{}
        }
        return 0;
    }
}
