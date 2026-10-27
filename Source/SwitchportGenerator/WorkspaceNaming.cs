namespace SwitchportGenerator;

public static class WorkspaceNaming
{
    public static string CleanLabel(string? value)
    {
        value=(value??"").Trim();
        if(value.Length>100||value.Any(char.IsControl))throw new InvalidOperationException("Workspace output label must be 100 characters or fewer on one line.");
        return value;
    }
    public static string FromFile(string path)
    {
        string name=Path.GetFileName(path);
        foreach(string suffix in new[]{".spworkspace.json",".json"})
            if(name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)){name=name[..^suffix.Length];break;}
        return Safe(name);
    }
    public static string Suggested(ConfigurationWorkspace workspace)
    {
        if(workspace.SavedWorkspaceName.Length>0)return Safe(workspace.SavedWorkspaceName);
        var names=workspace.Items.Select(item=>
        {
            string name=ReportText.DisplayName(item.Inventory);
            if(string.IsNullOrWhiteSpace(name)||name.Equals("Unknown",StringComparison.OrdinalIgnoreCase))name=ReportText.SwitchModel(item.Inventory);
            if(string.IsNullOrWhiteSpace(name))name=Path.GetFileNameWithoutExtension(item.Name);
            return string.IsNullOrWhiteSpace(name)?"Switch":name;
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(names.Count==0)return "Switchport workspace";
        return Safe(string.Join(" + ",names.Take(3))+(names.Count>3?$" + {names.Count-3} more":""));
    }
    public static string Effective(ConfigurationWorkspace workspace)=>workspace.OutputLabel.Length>0?Safe(workspace.OutputLabel):Suggested(workspace);
    public static string Safe(string label)=>ReportNaming.FolderName(new Inventory{Hostname=string.IsNullOrWhiteSpace(label)?"Switchport workspace":label});
    public static string CreateOutputDirectory(string parent,string label)
    {
        parent=Path.GetFullPath(parent);Directory.CreateDirectory(parent);
        string name=Safe(label);
        bool Exists(string path)=>File.Exists(path)||Directory.Exists(path)||Directory.EnumerateFileSystemEntries(parent).Any(existing=>Path.GetFileName(existing).Equals(Path.GetFileName(path),StringComparison.OrdinalIgnoreCase));
        // Reserve an empty directory, then rename atomically into the human-readable name.
        // Directory.Move refuses an existing target, including a racing export.
        string temporary=Path.Combine(parent,".switchport-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
        try
        {
            for(int suffix=1;;suffix++)
            {
                string candidate=Path.Combine(parent,name+(suffix==1?"":$" ({suffix})"));
                if(Exists(candidate))continue;
                try{Directory.Move(temporary,candidate);return candidate;}
                catch(IOException) when(Exists(candidate)){}
            }
        }
        finally{if(Directory.Exists(temporary))Directory.Delete(temporary);}
    }
}
