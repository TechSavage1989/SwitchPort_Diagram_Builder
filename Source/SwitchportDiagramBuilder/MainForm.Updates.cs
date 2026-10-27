using System.Text.Json;
using SwitchportGenerator;

namespace SwitchportDiagramBuilder;

public sealed partial class MainForm
{
    AppUpdateController? updater;
    UpdateLaunchOptions updateLaunch=new();
    System.Windows.Forms.Timer? healthTimer;
    bool restoreAttempted;
    bool updateEditing;
    void InitializeUpdates(string[] arguments)
    {
        updateLaunch=UpdateLaunchOptions.Parse(arguments);
        try{updater=new(this,()=>busy||updateEditing,Send,SaveUpdateSession,updateLaunch.NormalArguments);}
        catch(Exception ex){try{Directory.CreateDirectory(data);File.AppendAllText(Path.Combine(data,"updater-error.txt"),ex.ToString()+Environment.NewLine);}catch{}}
        FormClosing+=(_,e)=>{if(updater?.Installing==true&&busy)e.Cancel=true;};
        FormClosed+=(_,_)=>{healthTimer?.Stop();healthTimer?.Dispose();updater?.Dispose();};
    }
    async Task UpdateAction(Msg message)
    {
        if(updater==null){Send(new{type="updateState",currentVersion=AppUpdateController.CurrentVersionText,status="Updates are unavailable. Confirm that Local AppData is writable, then reopen the app.",automaticChecks=true});return;}
        if(message.action=="updateState"){updater.Publish();return;}
        if(updater.Installing)return;
        switch(message.action)
        {
            case "updateCheck":await updater.CheckAsync(true);break;
            case "updateDownload":await updater.DownloadAsync();break;
            case "updateInstall":await updater.InstallAsync();break;
            case "updateReview":updater.Review();break;
            case "updateLater":updater.Later();break;
            case "updatePreference":updater.SetAutomaticChecks(message.enabled??true);break;
            case "updateEditing":updateEditing=message.enabled??false;updater.Publish();break;
        }
    }
    void UpdatesReady()
    {
        updater?.Ready();
        _=Task.Run(UpdateApplyHost.CleanupOldHelpers);
        if(updateLaunch.HealthPath.Length==0||healthTimer!=null)return;
        healthTimer=new(){Interval=200};
        healthTimer.Tick+=(_,_)=>
        {
            if(IsDisposed||!Visible||WindowState==FormWindowState.Minimized||web.CoreWebView2==null||!web.Visible||web.ClientSize.Width<100||web.ClientSize.Height<100)return;
            try{updateLaunch.SignalHealthy();}catch(Exception ex){try{File.AppendAllText(Path.Combine(data,"updater-error.txt"),ex.Message+Environment.NewLine);}catch{}}
            healthTimer.Stop();
        };
        healthTimer.Start();
    }
    string? SaveUpdateSession()
    {
        if(workspace.Items.Count==0&&projects.Current==null)return null;
        string directory=UpdatePaths.InstallRoot(Environment.ProcessPath!);
        string path=Path.Combine(directory,"session-"+Guid.NewGuid().ToString("N")+".json");
        AppUpdateController.AtomicJson(path,WorkspaceRestartState.Capture(workspace,projects.Current));
        return path;
    }
    void RestoreUpdateSession()
    {
        if(restoreAttempted||updateLaunch.SessionPath.Length==0)return;restoreAttempted=true;
        try
        {
            string path=updateLaunch.SessionPath;
            if(new FileInfo(path).Length>WorkspaceRestartState.MaxBytes)throw new InvalidDataException("Saved restart workspace is too large.");
            string json=File.ReadAllText(path);
            workspace=WorkspaceRestartState.Restore(json,Directory.Exists,File.Exists);
            projects.Restore(WorkspaceRestartState.RestoreProject(json));
            if(projects.Current==null&&workspace.Selected is {} selected&&selected.LastOutput!=null)
                projects.RecordSingle(true,selected.Name,selected.LastOutput,selected.LastChart,selected.Status);
            SendSelection();
            // Keep the handoff until the helper commits healthy startup so rollback can restore it.
            if(updateLaunch.HealthPath.Length==0)File.Delete(path);
        }
        catch(Exception ex)
        {
            Send(new{type="error",message="The previous workspace could not be restored. Reopen a saved workspace or reload configurations. Existing reports and settings are retained.",details=ex.Message});
        }
    }

}

internal sealed class UpdateLaunchOptions
{
    public string[] NormalArguments{get;private set;}=Array.Empty<string>();
    public string HealthPath{get;private set;}="";
    public string Nonce{get;private set;}="";
    public string SessionPath{get;private set;}="";
    public static UpdateLaunchOptions Parse(string[] args)
    {
        var result=new UpdateLaunchOptions();var normal=new List<string>();
        string root=UpdatePaths.InstallRoot(Environment.ProcessPath??throw new InvalidOperationException("Application path is unavailable."));
        for(int i=0;i<args.Length;i++)
        {
            if(args[i]=="--update-health")
            {
                if(i+2>=args.Length)throw new ArgumentException("Update health handoff is incomplete.");
                result.HealthPath=Path.GetFullPath(args[++i]);result.Nonce=args[++i];
                if(result.Nonce.Length!=32||result.Nonce.Any(c=>!Uri.IsHexDigit(c))||!string.Equals(result.HealthPath,Path.Combine(root,"health-"+result.Nonce+".json"),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Invalid update health handoff.");
            }
            else if(args[i]=="--restore-update-session")
            {
                if(i+1>=args.Length)throw new ArgumentException("Update restart selection is missing.");
                result.SessionPath=Path.GetFullPath(args[++i]);
                if(!UpdatePaths.IsInside(result.SessionPath,root)||!Path.GetFileName(result.SessionPath).StartsWith("session-",StringComparison.Ordinal)||!result.SessionPath.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Invalid update restart selection.");
            }
            else normal.Add(args[i]);
        }
        result.NormalArguments=normal.ToArray();return result;
    }
    public void SignalHealthy()
    {
        if(HealthPath.Length==0)return;
        AppUpdateController.AtomicJson(HealthPath,new StartupHealth(Nonce,Environment.ProcessId,AppUpdateController.CurrentVersionText));
    }
}
