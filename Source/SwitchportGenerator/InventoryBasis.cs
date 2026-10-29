namespace SwitchportGenerator;

/// <summary>Keep software interface views separate from confirmed physical counts.</summary>
public static class InventoryBasis
{
    public static bool HasConfiguredInterfaces(Inventory inv)=>inv.Groups.Any(g=>g.ConfiguredInterfaces);
    public static bool IsConfiguredInterface(Inventory inv,Port port)=>inv.Groups.Any(g=>g.Id==port.Group&&g.ConfiguredInterfaces);
    public static int ConfiguredInterfaceCount(Inventory inv)=>inv.Ports.Count(p=>IsConfiguredInterface(inv,p));
    public static int ConfirmedPhysicalPortCount(Inventory inv)=>inv.Ports.Count(p=>inv.Groups.Any(g=>g.Id==p.Group&&!g.ConfiguredInterfaces));
    public static string CountLabel(Inventory inv)=>HasConfiguredInterfaces(inv)?ConfirmedPhysicalPortCount(inv)+" confirmed ports + "+ConfiguredInterfaceCount(inv)+" configured uplink interfaces":inv.Ports.Count+" interfaces";
    public static string Qualifier(Inventory inv)=>HasConfiguredInterfaces(inv)?"Uplink module unconfirmed":"";
    public static string PortBasis(Inventory inv,Port port)=>IsConfiguredInterface(inv,port)?"Configured interface - installed socket unconfirmed":"Confirmed physical port";
}
