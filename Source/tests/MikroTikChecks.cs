using HUBTechGenerator;
using System.Text;
static class MikroTikChecks
{
    static int count;
    static void Check(bool value,string message){if(!value)throw new Exception(message);count++;}
    static Inventory Parse(string text)=>DeviceModels.Parse(text,Array.Empty<DeviceIdentity>());
    static void ExportGeometry(Inventory inv,bool expectGrowth=false)
    {
        Check(inv.Blockers.Count==0,"Parser blockers: "+string.Join(';',inv.Blockers));
        var places=Charts.Layout(inv);Check(Charts.Validate(inv,places).Count==0,"Inventory geometry failed.");
        var sheet=QuickSheet.Build(inv,places,"TEST");var scene=sheet.Scene;
        Check(Charts.ValidateScene(scene).Count==0,"Base scene: "+string.Join(';',Charts.ValidateScene(scene)));
        var dots=scene.Dots.ToList();var shapes=scene.Shapes.ToList();QuickSheet.FitToPage(scene);
        foreach(var label in scene.Labels){var size=ReportRenderer.Measure(label);if(label.X<0||label.Y<0||label.X+size.Width>scene.Width||label.Y+size.Height>scene.Height)Console.WriteLine($"BAD BOUNDS {label.Text} {label.X},{label.Y} {size.Width}x{size.Height}");}
        Check(Charts.ValidateScene(scene).Count==0,"Fitted scene: "+string.Join(';',Charts.ValidateScene(scene)));
        Check(scene.DiagramScale>=1,"Fitting must not shrink diagram.");
        if(expectGrowth)Check(scene.DiagramScale>1.3,"Small diagram should enlarge.");
        for(int i=0;i<scene.Dots.Count;i++)Check(scene.Dots[i].Radius>=2.1&&scene.Dots[i].VoiceExtra==0,"Larger markers survive fitting.");
        for(int i=0;i<scene.Shapes.Count;i++)Check(scene.Shapes[i].Points.Length==shapes[i].Points.Length,"Uniform fitting retains all socket silhouettes.");
    }
    static void Main(string[] args)
    {
        var baseInv=Parse("; JL256A Configuration Editor\nhostname SAMPLE\ntrunk 49-50 trk1 lacp\ninterface 1\n name Workstation\n disable\n exit\nvlan 10\n name Data\n untagged 1-48\n tagged trk1\n exit\nvlan 150\n name Voice\n tagged 1-48,trk1\n voice\n exit\n");ExportGeometry(baseInv);
        string header="# jan/01/2026 by RouterOS 7.16.2\n# model = CRS309-1G-8S+IN\n/system identity\nset name=FIBER-LAB\n";
        string config=header+"/interface ethernet\nset [ find default-name=sfp-sfpplus1 ] name=\\\n uplink comment=\"Core link\"\n/interface bridge\nadd name=br vlan-filtering=yes\n/interface bridge port\nadd bridge=br interface=uplink frame-types=admit-only-vlan-tagged\nadd bridge=br interface=sfp-sfpplus2 pvid=20\n/interface bridge vlan\nadd bridge=br tagged=uplink untagged=sfp-sfpplus2 vlan-ids=20 comment=Data\nadd bridge=br tagged=uplink,sfp-sfpplus2 vlan-ids=30 comment=Phones\n";
        var inv=Parse(config);Check(inv.Hostname=="FIBER-LAB"&&inv.Ports.Count==9,"RSC model and hostname.");
        Check(inv.Ports.Single(p=>p.Id=="uplink").TaggedVlans.SequenceEqual(new[]{20,30}),"Renamed interface VLAN references.");
        Check(inv.Ports.Single(p=>p.Id=="sfp-sfpplus2").UntaggedVlan==20,"PVID/native bridge assignment.");
        Check(!inv.Vlans.Any(v=>v.Voice),"VLAN names must not invent a voice designation.");ExportGeometry(inv,true);
        var off=Parse(config.Replace("vlan-filtering=yes","vlan-filtering=no"));Check(off.Ports.All(p=>p.UntaggedVlan==null&&p.TaggedVlans.Count==0),"Filtering-off VLAN settings cannot appear active.");ExportGeometry(off,true);
        var implicitNative=Parse(header+"/interface bridge\nadd name=br vlan-filtering=yes\n/interface bridge port\nadd bridge=br interface=sfp-sfpplus2 pvid=40\n");Check(implicitNative.Ports.Single(p=>p.Id=="sfp-sfpplus2").UntaggedVlan==40,"RSC PVID creates the documented dynamic untagged membership.");
        var bond=Parse(header+"/interface bonding\nadd name=Core-Bond slaves=sfp-sfpplus1,sfp-sfpplus2 mode=802.3ad\n/interface bridge\nadd name=br vlan-filtering=yes\n/interface bridge port\nadd bridge=br interface=Core-Bond frame-types=admit-only-vlan-tagged\n/interface bridge vlan\nadd bridge=br tagged=Core-Bond vlan-ids=20,30\n");Check(bond.Ports.Count(p=>p.Aggregate=="Core-Bond"&&p.TaggedVlans.SequenceEqual(new[]{20,30}))==2,"Bond VLANs propagate to both physical members.");ExportGeometry(bond,true);
        var conflict=Parse(config+"add bridge=br untagged=sfp-sfpplus2 vlan-ids=99\n");Check(conflict.Blockers.Count>0,"Genuine contradictory native memberships block export.");
        var missing=Parse(config.Replace("tagged=uplink untagged", "tagged=missing untagged"));Check(missing.Blockers.Count>0,"Unknown interface references block export.");
        var absent=Parse(config.Replace("# model = CRS309-1G-8S+IN", "# no model provided"));Check(absent.Blockers.Count>0,"Partial RSC inventory must not invent a model.");
        var selected=DeviceModels.Parse(config.Replace("# model = CRS309-1G-8S+IN", "# no model provided"),new[]{new DeviceIdentity{Id="1",ModelSelection="CRS309-1G-8S+IN"}});ExportGeometry(selected,true);
        foreach(var model in MikroTikParser.Models)ExportGeometry(Parse($"# by RouterOS 7.16\n# model = {model.Code}\n/system identity\nset name=MODEL-TEST\n"));
        if(args.Length>0)
        {
            string source=ConfigInput.Read(args[0]);var binary=Parse(source);Check(binary.Ports.Count==27&&binary.LogicalInterfaces.Count==33,"Supplied backup has 27 physical ports and 33 logical interfaces.");
            Check(binary.Hostname=="ScaleFiberSwitch2"&&ReportText.SwitchModel(binary)=="","Hostname is read; unconfirmed model stays blank.");
            Check(binary.Ports.Single(p=>p.Id=="sfp-sfpplus1").UntaggedVlan==220&&binary.Ports.Single(p=>p.Id=="sfp-sfpplus1").TaggedVlans.SequenceEqual(new[]{251}),"Supplied native/tagged port membership.");
            Check(binary.Ports.Count(p=>p.UntaggedVlan==220)==5&&binary.Ports.Count(p=>p.UntaggedVlan==251)==4,"Exact static untagged memberships.");
            Check(binary.Ports.Count(p=>p.RelatedInterfaces.Count==4)==2,"QSFP lanes remain grouped into two physical cages.");ExportGeometry(binary);
            var bytes=File.ReadAllBytes(args[0]);bool bad=false;try{ConfigInput.Decode(bytes[..^1]);}catch(InvalidOperationException){bad=true;}Check(bad,"Truncated backup is rejected.");
            bad=false;try{ConfigInput.Decode(new byte[]{0xef,0xa8,0x91,0x73,0,0,0,0});}catch(InvalidOperationException ex){bad=ex.Message.Contains("encrypted");}Check(bad,"Encrypted backup gives an actionable message.");
        }
        Console.WriteLine("PASS "+count+" MikroTik input, membership, hardware, model-selection and scaling assertions.");
    }
}
