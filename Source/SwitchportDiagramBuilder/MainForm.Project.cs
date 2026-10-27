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
    void OpenProjectChart()
    {
        SendProject();var project=projects.Current;var charts=projects.ExistingCharts();
        if(project==null||!projects.ChartAvailable)throw new InvalidOperationException("The last generated chart is unavailable. Build reports to create a new project.");
        if(project.Paged&&charts.Length>0){Send(new{type="openProjectPreview"});return;}
        string path=charts.FirstOrDefault()??project.PdfPath!;
        Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    }
}
