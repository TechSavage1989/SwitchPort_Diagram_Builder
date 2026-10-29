namespace SwitchportGenerator;

public static class ReportText
{
    public static string TaggedVlans(Port port,string separator=", ")=>port.TaggedVlanPolicy??(port.Notes.Any(n=>n.StartsWith("Tagged VLANs:",StringComparison.Ordinal))?"All":string.Join(separator,port.TaggedVlans));
    public const string CableMoveDisclaimer="Confirm live link, LLDP/CDP, and MAC address table before moving production cables.";
    public static string ModelName(string product)=>product==Parser.SharedPl48Profile||product.StartsWith("MIKROTIK-SHARED-")?"":product;
    public static bool IsStack(Inventory inv)=>inv.Groups.Select(g=>g.Device).Distinct().Count()>1||inv.Groups.Any(g=>g.Id.StartsWith("Member ",StringComparison.OrdinalIgnoreCase))||inv.Topology.Contains("Stack",StringComparison.OrdinalIgnoreCase)||inv.Topology.Contains("VSF",StringComparison.OrdinalIgnoreCase)||inv.Topology=="IRF";
    public static string DisplayName(Inventory inv)
    {
        var names=inv.Devices.Where(d=>d.ParentId.Length==0&&(d.Kind=="Switch"||d.Kind=="Chassis")&&d.Name.Length>0).Select(d=>d.Name).Distinct().ToList();
        return names.Count>0?string.Join(" / ",names):inv.Hostname;
    }
    public static string GroupLabel(Inventory inv,Group group)
    {
        string member=IsStack(inv)?"Member "+group.Device:"";
        string label=group.Slot.Length>0?(member.Length>0?member+" | ":"")+(group.Rear?"Rear module ":"Slot ")+group.Slot:member.Length>0?member:group.Id;
        string details=DeviceIdentities.Heading(inv,group.Slot.Length>0?DeviceIdentities.ModuleId(group):group.Device);
        return label+(details.Length>0?" | "+details:"");
    }
    public static string SwitchModel(Inventory inv)=>inv.ReportModel.Length>0?inv.ReportModel:inv.Devices.Any(d=>d.ParentId.Length==0&&d.ModelSelection.Length>0)?string.Join(" / ",inv.Devices.Where(d=>d.ParentId.Length==0).Select(d=>d.Model).Where(s=>s.Length>0).Distinct()):inv.ChassisModel.Length>0?inv.ChassisModel:string.Join(" / ",inv.Groups.Where(g=>g.Slot.Length==0).Select(g=>ModelName(g.Product)).Where(s=>s.Length>0).Distinct());
    public static string ReadHostname(string source)
    {
        if(MikroTikParser.IsConfiguration(source))return MikroTikParser.ReadHostname(source);
        var match=System.Text.RegularExpressions.Regex.Match(source.Replace("\r",""),@"(?im)^[ \t]*(?:hostname|sysname)[ \t]+([^\n]+)$");
        return match.Success?Parser.Safe(match.Groups[1].Value):"";
    }
}
