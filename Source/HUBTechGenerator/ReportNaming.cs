using System.Text.RegularExpressions;
namespace HUBTechGenerator;
public static class ReportNaming
{
    public static string FolderName(Inventory inventory)
    {
        string name=inventory.Hostname;
        if(string.IsNullOrWhiteSpace(name)||name.Equals("Unknown",StringComparison.OrdinalIgnoreCase))
            name=inventory.Devices.Any(d=>d.ParentId.Length==0&&d.ModelSelection.Length>0)?ReportText.SwitchModel(inventory):inventory.ReportModel.Length>0?inventory.ReportModel:inventory.ChassisModel.Length>0?inventory.ChassisModel:string.Join("_",inventory.Groups.Where(g=>g.Slot.Length==0).Select(g=>ReportText.ModelName(g.Product)).Where(p=>p.Length>0).Distinct(StringComparer.OrdinalIgnoreCase));
        if(string.IsNullOrWhiteSpace(name))name="Unknown-Switch";
        name=Regex.Replace(name,@"[<>:""/\\|?*\x00-\x1f]","_").Trim().TrimEnd('.',' ');
        if(name.Length>100)name=name[..100].TrimEnd('.',' ');
        if(name.Length==0)name="Unknown-Switch";
        if(Regex.IsMatch(name,@"^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase))name="Switch_"+name;
        return name;
    }
    public static string AvailableDirectory(string parent,Inventory inventory)
    {
        string name=FolderName(inventory),candidate=Path.Combine(parent,name);int suffix=2;
        while(Directory.Exists(candidate)||File.Exists(candidate))candidate=Path.Combine(parent,name+" ("+suffix+++ ")");
        return candidate;
    }
}
