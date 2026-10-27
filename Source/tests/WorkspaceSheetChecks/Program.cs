using System.Text.Json;
using SwitchportGenerator;
using SkiaSharp;

static class WorkspaceSheetChecks
{
    static int assertions;
    static void Check(bool ok,string message){assertions++;if(!ok)throw new Exception(message);}
    static Inventory Aruba(string name)=>DeviceModels.Parse("; JL256A Configuration Editor\nhostname "+name+"\ntrunk 49-50 trk1 lacp\nvlan 10\n name Data\n untagged 1-48\n tagged trk1\n exit\nvlan 150\n name Voice\n tagged 1-48,trk1\n voice\n exit\n",Array.Empty<DeviceIdentity>());
    static Inventory Router(string name)=>DeviceModels.Parse("# by RouterOS 7.16\n# model = CRS309-1G-8S+IN\n/system identity\nset name="+name+"\n",Array.Empty<DeviceIdentity>());
    static Inventory TallStack()=>DeviceModels.Parse("; Aruba Configuration Editor\nhostname EIGHT-MEMBER-STACK\n"+string.Join('\n',Enumerable.Range(1,8).Select(index=>$"vsf member {index} type JL558A"))+"\n",Array.Empty<DeviceIdentity>());
    static Inventory Dense()
    {
        var inv=Aruba("DENSE-FORTY");inv.Vlans=new(){new(){Id=1,Name="Data"}};
        inv.Vlans.AddRange(Enumerable.Range(100,40).Select(id=>new Vlan{Id=id,Name="Network "+id,Voice=id==101}));
        foreach(var port in inv.Ports){port.UntaggedVlan=1;port.UntaggedState="Known";port.TaggedVlans.Clear();port.VoiceVlan=null;port.Aggregate="";port.Notes.Clear();}
        foreach(var port in inv.Ports.Where(port=>port.Media=="RJ-45").Take(1).Concat(inv.Ports.Where(port=>port.Media!="RJ-45").Take(1)))
        {port.TaggedVlans=Enumerable.Range(100,40).ToList();port.VoiceVlan=101;}
        return inv;
    }
    static WorkspaceSheetItem Item(string id,Inventory inv)=>new(id,id+".cfg",inv,"SHEET-TEST");
    static void Validate(WorkspaceSheetResult result,IReadOnlyList<WorkspaceSheetItem> inputs)
    {
        Check(result.Placements.Select(item=>item.Id).SequenceEqual(inputs.Select(item=>item.Id)),"Workspace order must be preserved by page placement.");
        foreach(var scene in result.Pages)
        {
            Check(scene.Width==WorkspaceSheet.PageWidth&&scene.Height==WorkspaceSheet.PageHeight,"Sheet remains an exact A4 drawable canvas, not a vertically enlarged custom page.");
            Check(Charts.ValidateScene(scene).Count==0,"Combined sheet must not introduce overlaps: "+string.Join(';',Charts.ValidateScene(scene)));
            Check(scene.Labels.Where(label=>label.Owner.Length>0).All(label=>label.Size>=QuickSheet.MinimumPortFont-.001),"Port numbers retain minimum readable printed size.");
            Check(scene.Labels.Where(label=>label.Owner.Length==0).All(label=>label.Size>=QuickSheet.MinimumBodyFont-.001),"Headings, legends and body retain minimum readable printed size.");
            Check(scene.Shapes.All(shape=>shape.Points.All(point=>point.X>=0&&point.Y>=0&&point.X<=scene.Width&&point.Y<=scene.Height)),"No physical port shapes can be cropped outside page bounds.");
            Check(scene.Dots.All(dot=>dot.X-dot.Radius>=0&&dot.Y-dot.Radius>=0&&dot.X+dot.Radius<=scene.Width&&dot.Y+dot.Radius<=scene.Height),"All VLAN markers stay inside sheet bounds.");
            Check(scene.Labels.Count(label=>label.Text.Contains(ReportText.CableMoveDisclaimer,StringComparison.Ordinal))==1,"Each sheet retains one complete cable-move warning.");
        }
        foreach(var page in result.Placements.GroupBy(item=>item.PageNumber))
        {
            var placed=page.ToArray();
            for(int index=1;index<placed.Length;index++)Check(placed[index].Y>=placed[index-1].Y+placed[index-1].Height+WorkspaceSheet.BlockGap-.001,"Switch blocks retain visible separation.");
            Check(placed.All(item=>item.GeometryScale==placed[0].GeometryScale),"Switches sharing a sheet use identical physical geometry scale.");
        }
        foreach(var input in inputs)
        {
            var placement=result.Placements.Single(item=>item.Id==input.Id);var scene=result.Pages[placement.PageNumber-1];
            var numbers=scene.Labels.Where(label=>label.Owner.StartsWith(input.Id+":",StringComparison.Ordinal)).ToArray();
            Check(numbers.Length==Charts.Layout(input.Inventory).Count,"Every original socket keeps its printed number including combo sockets: "+input.Id);
            Check(scene.Labels.Any(label=>label.Text==ReportText.DisplayName(input.Inventory)),"Every configuration has a visible identifying hostname/display name.");
        }
    }
    static void Save(WorkspaceSheetResult result,string root,string prefix)
    {
        Pdf.Save(result.Pages,Path.Combine(root,prefix+".pdf"));
        for(int index=0;index<result.Pages.Count;index++)Png.Save(result.Pages[index],Path.Combine(root,prefix+"-"+(index+1)+".png"),3508,2480,true);
        File.WriteAllText(Path.Combine(root,prefix+"-plan.json"),JsonSerializer.Serialize(new{result.Placements,result.Warnings,PageCount=result.Pages.Count},new JsonSerializerOptions{WriteIndented=true}));
    }
    static int Main(string[] args)
    {
        string root=args.Length>0?Path.GetFullPath(args[0]):Path.Combine(Path.GetTempPath(),"workspace-sheet-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var alpha=Aruba("ALPHA-ACCESS");var beta=Aruba("BETA-ACCESS");
            var inputs=new[]{Item("alpha",alpha),Item("beta",beta)};
            var before=JsonSerializer.Serialize(inputs.Select(item=>item.Inventory));
            var together=WorkspaceSheet.Build(inputs);Validate(together,inputs);
            Check(together.Pages.Count==1&&together.Placements.All(item=>item.PageNumber==1),"Two actual switch configurations must share the first A4 sheet, not become separate pages.");
            Check(together.Placements.All(item=>!item.Standalone&&item.GeometryScale==1),"Ordinary switches use reusable reference-scale components.");
            Check(JsonSerializer.Serialize(inputs.Select(item=>item.Inventory))==before,"Composing sheets must not alter parsed inventories, memberships or details.");
            foreach(var input in inputs)
            {
                var compact=QuickSheet.BuildCompact(input.Inventory,Charts.Layout(input.Inventory),"SHEET-TEST",together.MarkerRadius);
                var onSheet=together.Pages[0].Shapes.Where(shape=>shape.Owner.StartsWith(input.Id+":",StringComparison.Ordinal)).ToArray();
                Check(onSheet.Length==compact.Scene.Shapes.Count(shape=>shape.Owner.Length>0),"All outlines and native-colored physical faces survive composition.");
                foreach(var group in compact.Scene.Shapes.Where(shape=>shape.Owner.Length>0).GroupBy(shape=>(shape.Fill,shape.Stroke)))
                    Check(onSheet.Count(shape=>shape.Fill==group.Key.Fill&&shape.Stroke==group.Key.Stroke)==group.Count(),"Native VLAN fills/LAG outlines retain source colors and multiplicity.");
                var area=together.Placements.Single(item=>item.Id==input.Id);
                var dots=together.Pages[0].Dots.Where(dot=>dot.Y>=area.Y&&dot.Y<area.Y+area.Height).ToArray();
                Check(dots.Length==compact.Scene.Dots.Count,"Tagged/voice markers, legend and guide are retained exactly.");
                Check(dots.Count(dot=>dot.Voice)==compact.Scene.Dots.Count(dot=>dot.Voice),"Voice diamonds remain distinct from tagged circles.");
            }
            Save(together,root,"two-on-one");
            for(int count=3;count<=5;count++)
            {
                var many=Enumerable.Range(1,count).Select(index=>Item("ordered-"+index,Aruba("ORDERED-"+index))).ToArray();
                var sheets=WorkspaceSheet.Build(many);Validate(sheets,many);
                Check(sheets.Pages.Count<count&&sheets.Placements[0].PageNumber==sheets.Placements[1].PageNumber,"Pagination is capacity-driven, never unconditionally one configuration per page.");
                if(count==5){Check(sheets.Pages.Count>1,"Five detailed switches paginate rather than squeezing below readable geometry.");Save(sheets,root,"five-ordered");}
            }
            var mixed=new[]{Item("mixed-aruba",Aruba("MIXED-ARUBA")),Item("mixed-routeros",Router("MIXED-ROUTEROS"))};
            var mixedSheets=WorkspaceSheet.Build(mixed);Validate(mixedSheets,mixed);Check(mixedSheets.Pages.Count==1,"Mixed supported vendor switches share a sheet.");Save(mixedSheets,root,"mixed-vendors");
            var dense=new[]{Item("dense-before",Aruba("DENSE-BEFORE")),Item("dense",Dense()),Item("dense-stack",TallStack()),Item("dense-after",Router("DENSE-AFTER"))};
            var denseSheets=WorkspaceSheet.Build(dense);Validate(denseSheets,dense);
            Check(denseSheets.Placements.Single(item=>item.Id=="dense-stack").Standalone,"Dense already-supported configuration retains readable standalone fallback rather than failing or losing markers.");
            Check(denseSheets.Warnings.Any(warning=>warning.Contains("dedicated A4")),"Dedicated-page capacity decision is visible in the page plan.");Save(denseSheets,root,"dense-fallback");
            string logoPath=Path.Combine(root,"synthetic-logo.png");
            using(var bitmap=new SKBitmap(120,30)){bitmap.Erase(SKColors.DarkCyan);using var image=SKImage.FromBitmap(bitmap);using var bytes=image.Encode(SKEncodedImageFormat.Png,100);File.WriteAllBytes(logoPath,bytes.ToArray());}
            var branded=WorkspaceSheet.Build(inputs,ReportLogo.Load(logoPath),"Customer workspace");Validate(branded,inputs);
            Check(branded.Pages.All(page=>page.Images.Count==1),"Shared sheet header supports one undistorted report logo on every page.");Save(branded,root,"branded-sheet");
            var longTitle=WorkspaceSheet.Build(inputs,ReportLogo.Load(logoPath),new string('W',100));Validate(longTitle,inputs);
            Console.WriteLine("PASS "+assertions+" shared-sheet geometry/order/readability/preservation assertions. Fixtures: "+root);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{if(args.Length==0)Directory.Delete(root,true);}
    }
}
