namespace SwitchportGenerator;

/// <summary>Keep software interface views separate from confirmed physical counts.</summary>
public static class InventoryBasis
{
    public static bool HasConfiguredInterfaces(Inventory inv)=>inv.Groups.Any(g=>g.ConfiguredInterfaces);
    public static bool IsConfiguredInterface(Inventory inv,Port port)=>inv.Groups.Any(g=>g.Id==port.Group&&g.ConfiguredInterfaces);
    public static int ConfiguredInterfaceCount(Inventory inv)=>inv.Ports.Count(p=>IsConfiguredInterface(inv,p));
    public static int ConfirmedPhysicalPortCount(Inventory inv)=>inv.Ports.Count(p=>inv.Groups.Any(g=>g.Id==p.Group&&!g.ConfiguredInterfaces));
    public static string CountLabel(Inventory inv)=>HasConfiguredInterfaces(inv)?ConfirmedPhysicalPortCount(inv)+" confirmed ports + "+ConfiguredInterfaceCount(inv)+" configured uplink interfaces":inv.Ports.Count+" interfaces";
    public static string Qualifier(Inventory inv)=>CiscoModuleCatalog.HasUnconfirmedModules(inv)?"Uplink module unconfirmed":HasConfiguredInterfaces(inv)?"Configured uplink alternatives retained":"";
    public static string PortBasis(Inventory inv,Port port)=>IsConfiguredInterface(inv,port)?(inv.Vendor=="Cisco"&&!CiscoModuleCatalog.HasUnconfirmedModules(inv)?"Configured alternative - not an additional installed socket":"Configured interface - installed socket unconfirmed"):"Confirmed physical port";
}
