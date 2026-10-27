using System.Diagnostics;
using SwitchportGenerator;
namespace SwitchportDiagramBuilder;

public sealed partial class MainForm
{
    static string DisplayName(ConfigurationItem item)
    {
        string name=ReportText.DisplayName(item.Inventory);
        return name.Equals("Unknown",StringComparison.OrdinalIgnoreCase)?"":name;
    }
    void SendWorkspace()=>Send(new
    {
        type="workspace",selectedId=workspace.SelectedId,dirty=workspace.Dirty,batchAvailable=lastBatchOutput!=null&&Directory.Exists(lastBatchOutput),
        items=workspace.Items.Select(item=>new
        {
            id=item.Id,name=item.Name,hostname=DisplayName(item),model=ReportText.SwitchModel(item.Inventory),
            error=string.Join("; ",item.Inventory.Blockers),dirty=item.Dirty
        }).ToArray()
    });
    void SendSelection()
    {
        SendWorkspace();
        var item=workspace.Selected;
        Send(new
        {
            type="selected",id=item?.Id,path=item?.SourcePath??"",name=item?.Name??"",hostname=item==null?"":DisplayName(item),
            chartAvailable=item?.LastChart is {} chart&&File.Exists(chart),statusType=item?.StatusType??"",statusText=item?.Status??""
        });
    }
    void SendImportResult(int added,List<string> errors)=>Send(new
    {
        type="importComplete",message=$"Added {added} configuration{(added==1?"":"s")}."+(errors.Count>0?$" {errors.Count} file{(errors.Count==1?"":"s")} could not be added.":""),errors
    });
    void ImportFiles(IncomingFile[] files)
    {
        int added=0;var errors=new List<string>();
        foreach(var file in files)
        {
            string name=string.IsNullOrWhiteSpace(file?.name)?"Dropped configuration":file.name;
            try
            {
                if(file==null)throw new InvalidOperationException("Invalid dropped file.");
                if(!string.IsNullOrWhiteSpace(file.error))throw new InvalidOperationException(file.error);
                string text=file.encoding=="base64"?ConfigInput.Decode(Convert.FromBase64String(file.text??"")):file.text??"";
                workspace.AddText(text,name);added++;
            }
            catch(Exception ex) when(ex is InvalidOperationException or FormatException or ArgumentException)
            {errors.Add(name+": "+ex.Message);}
        }
        SendSelection();SendImportResult(added,errors);
    }
    bool ConfirmDiscard(string question)=>MessageBox.Show(this,question+"\n\nSave workspace first to keep configurations, order and device details. Generated reports are not deleted.",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;
    void RemoveItem(string id)
    {
        var item=workspace.Items.FirstOrDefault(item=>item.Id==id);
        if(item==null)return;
        if(item.Dirty&&!ConfirmDiscard("Remove \""+item.Name+"\" and discard its unsaved workspace changes?"))return;
        workspace.Remove(id);SendSelection();
    }
    void SaveWorkspace()
    {
        using var dialog=new SaveFileDialog
        {
            Title="Save workspace (contains original configurations)",Filter="Switchport workspace|*.spworkspace.json",DefaultExt="spworkspace.json",AddExtension=true,
            FileName="Switchport workspace.spworkspace.json",OverwritePrompt=true
        };
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        workspace.Save(dialog.FileName);SendWorkspace();
        Send(new{type="workspaceSaved",message="Workspace saved. It contains configuration text; protect it like the original files."});
    }
    void OpenWorkspace()
    {
        using var dialog=new OpenFileDialog{Title="Open workspace",Filter="Switchport workspace|*.spworkspace.json|JSON files|*.json",CheckFileExists=true};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        // Fully validate before asking to replace the current in-memory workspace.
        var loaded=ConfigurationWorkspace.Load(dialog.FileName);
        if(workspace.Items.Count>0&&!ConfirmDiscard("Replace the current workspace with \""+Path.GetFileName(dialog.FileName)+"\"?"))return;
        workspace=loaded;lastBatchOutput=null;SendSelection();
    }
    void InvalidateOutputs()
    {
        foreach(var item in workspace.Items){item.LastOutput=null;item.LastChart=null;item.Status="";item.StatusType="";}
        lastBatchOutput=null;
    }
    async Task RunAll(bool combined)
    {
        if(workspace.Items.Count==0)throw new InvalidOperationException("Add configurations first.");
        busy=true;
        try
        {
            Send(new{type="status",message="Building reports in workspace order..."});
            var result=await Task.Run(()=>WorkspaceExport.Export(workspace,outputRoot,reportLogo==null?null:LogoPath,combined));
            lastBatchOutput=result.OutputDirectory;
            SendSelection();
            Send(new
            {
                type="batchComplete",output=result.OutputDirectory,
                message=$"Batch complete: {result.Items.Count(item=>item.Success)} of {result.Items.Count} configurations exported.",
                details=string.Join("\n",result.Items.Select((item,index)=>$"{index+1}. {workspace.Items.First(source=>source.Id==item.Id).Name}: "+(item.Success?"Exported":"Blocked: "+item.Error)))+(result.CombinedError.Length>0?"\n\n"+result.CombinedError:combined?"\n\nCombined PDFs include successful configurations only, in workspace order.":"")
            });
        }
        finally{busy=false;updater?.Publish();}
    }
    void OpenBatch()
    {
        if(lastBatchOutput==null||!Directory.Exists(lastBatchOutput))return;
        Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=true,ArgumentList={lastBatchOutput}});
    }
}
