namespace SwitchportGenerator;

/// <summary>Last successful output, independent of the editable/selected configuration.</summary>
public sealed record GeneratedProject(string Label,string OutputDirectory,string[] Charts,string? PdfPath,bool Paged,string Summary,bool Stale=false);

public sealed class GeneratedProjectHistory
{
    public GeneratedProject? Current {get;private set;}
    public void MarkStale(){if(Current!=null)Current=Current with{Stale=true};}
    public bool RecordSingle(bool success,string label,string output,string? chart,string summary)
    {
        if(!success||chart==null)return false;
        return Record(new(label,output,new[]{chart},null,false,summary));
    }
    public bool RecordBatch(WorkspaceExportResult result,bool combinedRequested,string label,string summary)
    {
        if(result.SuccessCount==0||combinedRequested&&result.CombinedDiagramsPath==null)return false;
        string[] charts=combinedRequested?result.CombinedImages.ToArray():result.Items.Where(item=>item.Success)
            .Select(item=>Path.Combine(item.OutputDirectory,"wall-chart.png")).ToArray();
        return Record(new(label,result.OutputDirectory,charts,combinedRequested?result.CombinedDiagramsPath:null,true,summary));
    }
    bool Record(GeneratedProject project)
    {
        string label=string.IsNullOrWhiteSpace(project.Label)?"Diagram project":project.Label.Trim();
        if(label.Length>200)label=label[..197]+"...";
        project=project with{Label=label};
        if(!Valid(project)||!Directory.Exists(project.OutputDirectory)||project.Charts.Length==0||project.Charts.Any(path=>!File.Exists(path))
            ||project.PdfPath!=null&&!File.Exists(project.PdfPath))return false;
        Current=project with{Charts=project.Charts.ToArray(),Stale=false};return true;
    }
    public void Restore(GeneratedProject? project)
    {
        // Runtime handoff only: explicit workspace files never contain generated paths.
        if(project==null||!Valid(project))return;
        if(Directory.Exists(project.OutputDirectory)||project.Charts.Any(File.Exists)||project.PdfPath!=null&&File.Exists(project.PdfPath))
            Current=project with{Charts=project.Charts.ToArray()};
    }
    public string[] ExistingCharts()=>Current?.Charts.Where(File.Exists).ToArray()??Array.Empty<string>();
    public bool FolderAvailable=>Current!=null&&Directory.Exists(Current.OutputDirectory);
    public bool PdfAvailable=>Current?.PdfPath is {} path&&File.Exists(path);
    public bool ChartAvailable=>ExistingCharts().Length>0||PdfAvailable;
    static bool Valid(GeneratedProject project)
    {
        if(string.IsNullOrWhiteSpace(project.Label)||project.Label.Length>200||project.Summary==null||project.Charts==null||project.Charts.Length>1000
            ||string.IsNullOrWhiteSpace(project.OutputDirectory)||!Path.IsPathFullyQualified(project.OutputDirectory))return false;
        try
        {
            bool Child(string path,string extension)
            {
                if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||!path.EndsWith(extension,StringComparison.OrdinalIgnoreCase))return false;
                string relative=Path.GetRelativePath(project.OutputDirectory,path);
                return !Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal);
            }
            return project.Charts.All(path=>Child(path,".png"))&&(project.PdfPath==null||Child(project.PdfPath,".pdf"));
        }
        catch(ArgumentException){return false;}
    }
}
