using System.Diagnostics;
using SwitchportGenerator;
namespace SwitchportDiagramBuilder;

public sealed partial class MainForm
{
    readonly GeneratedProjectHistory projects=new();
    void SendProject()
    {
        var project=projects.Current;
        Send(new{type="project",available=project!=null,chartAvailable=projects.ChartAvailable,folderAvailable=projects.FolderAvailable,
            combined=project?.Paged??false,pageCount=project?.Charts.Length??0,pdfAvailable=projects.PdfAvailable,
            label=project?.Label??"",output=project?.OutputDirectory??"",stale=project?.Stale??false,summary=project?.Summary??""});
    }
    void OpenProject()
    {
        SendProject();
        if(!projects.FolderAvailable)throw new InvalidOperationException("The last generated project folder is unavailable. Build reports to create a new project.");
        Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=true,ArgumentList={projects.Current!.OutputDirectory}});
    }
    void OpenProjectChart(int? index=null)
    {
        SendProject();string? path=projects.ChartPath(index);
        if(path==null)throw new InvalidOperationException("The generated PNG is unavailable. Open PDF separately if available, or build reports again.");
        Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    }
}
