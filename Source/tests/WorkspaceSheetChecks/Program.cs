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
    static Inventory DetailOverflow()
    {
        // Synthetic eight-member stack with independent LAGs: no customer input.
        var inv=TallStack();inv.Hostname="DETAIL-OVERFLOW";
        for(int index=0;index<34;index++)
        {
            var port=inv.Ports[index];port.Aggregate="trk"+(index/2+1);
            port.Notes.Add("Aggregation mode: lacp");
            port.Description="Endpoint purpose "+index.ToString("D2")+" with a complete readable location description";
        }
        inv.Vlans.Add(new(){Id=10,Name="Data"});
        foreach(var port in inv.Ports){port.UntaggedVlan=10;port.UntaggedState="Known";}
        return inv;
    }
    static int CheckLocalWorkspace(string directory,string output)
    {
        // Optional consumer-local diagnostic. CI never supplies or uploads private inputs.
        var workspace=new ConfigurationWorkspace();int oldFailures=0;
        foreach(string path in Directory.GetFiles(directory,"*.txt").Order(StringComparer.Ordinal))
        {
            var item=workspace.AddText(File.ReadAllText(path),Path.GetFileName(path));
            try{QuickSheet.BuildCompact(item.Inventory,Charts.Layout(item.Inventory),"LOCAL",null,false,749.89);}
            catch(OnePageCapacityException){oldFailures++;}
        }
        var result=WorkspaceExport.Export(workspace,output);
        if(result.FailureCount>0||result.CombinedError.Length>0)throw new Exception(result.CombinedError+" "+string.Join("; ",result.Items.Where(item=>!item.Success).Select(item=>item.Error)));
        Console.WriteLine(JsonSerializer.Serialize(new{Configurations=workspace.Items.Count,LegacyCompactFailures=oldFailures,result.SuccessCount,result.PageCount,result.OutputDirectory}));return 0;
    }
    static void CheckAtomic(Inventory inv,string root,string prefix)
    {
        var standalone=QuickSheet.Build(inv,Charts.Layout(inv),"ATOMIC");
        Check(Charts.ValidateScene(standalone.Scene).Count==0,"Regression configuration truly fits readable standalone A4.");
        Pdf.Save(new(){standalone.Scene},Path.Combine(root,prefix+"-standalone.pdf"));
        Png.Save(standalone.Scene,Path.Combine(root,prefix+"-standalone.png"),3508,2480,true);
        var target=Item("atomic-target",inv);var small=Item("atomic-small",Router("SMALL-BEFORE"));
        var titled=WorkspaceSheet.Build(new[]{target},title:"Closet location");Validate(titled,new[]{target});
        Check(titled.Pages.Count==1&&titled.Pages[0].Labels.Any(label=>label.Text=="Closet location"),"An optional title cannot force a standalone-fitting configuration to split.");
        foreach(var (name,items) in new[]{("alone",new[]{target}),("after-small",new[]{small,target}),("reordered",new[]{target,small}),("several",new[]{target,target with{Id="atomic-copy"},target with{Id="atomic-copy-two"}})})
        {
            var sheets=WorkspaceSheet.Build(items);Validate(sheets,items);
            foreach(var item in items.Where(item=>item.Id!="atomic-small"))
            {
                var placement=sheets.Placements.Single(place=>place.Id==item.Id);var scene=sheets.Pages[placement.PageNumber-1];
                string text=string.Concat(scene.Labels.Select(label=>label.Text.Replace(" ","")));
                Check(!scene.Labels.Any(label=>label.Text.Contains("Details continued")),"Standalone-fitting details stay with the switch: "+name);
                foreach(var description in inv.Ports.Select(port=>port.Description).Where(value=>value.Length>0).Distinct())Check(text.Contains(description.Replace(" ","")),"All descriptions remain on the switch's page.");
                foreach(var lag in inv.Ports.Select(port=>port.Aggregate).Where(value=>value.Length>0).Distinct())Check(text.Contains(lag+"(" )||text.Contains(lag+":"),"All LAGs remain on the switch's page: "+lag);
            }
            if(name=="after-small")Check(sheets.Placements.Single(place=>place.Id==target.Id).PageNumber==2,"Insufficient remaining space moves the entire block to a fresh page.");
            if(name is "after-small" or "reordered")Save(sheets,root,prefix+"-"+name);
        }
    }
    static int CheckLocalAtomic(string directory,string output)
    {
        Directory.CreateDirectory(output);string path=Directory.GetFiles(directory,"*.txt").Single();
        var inv=DeviceModels.Parse(File.ReadAllText(path),Array.Empty<DeviceIdentity>());
        CheckAtomic(inv,output,"supplied-atomic");
        Console.WriteLine("PASS "+assertions+" original-input standalone/combined/preceding-small/reorder/repeated-config assertions; private output: "+output);return 0;
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
            Check(scene.Labels.Count(label=>label.Text=="VLAN key")==1,"Every rendered sheet has one complete VLAN key, never a repeated key for each switch.");
            var sideKey=scene.Labels.Single(label=>label.Text=="VLAN key");
            Check(sideKey.Y>=WorkspaceSheet.ContentTop&&sideKey.X>scene.DiagramBodyWidth,"The common VLAN key stays in a side column beside the first switch with the symbol key reserved at the bottom, never beneath the switches.");
            Check(scene.Shapes.Where(shape=>shape.Owner.Length>0).All(shape=>shape.Points.Max(point=>point.X)<sideKey.X),"The sidebar never overlaps physical switch sockets.");
            foreach(string symbol in new[]{"Native VLAN","Tagged VLAN","Voice VLAN","LAG / trunk"})
                Check(scene.Labels.Count(label=>label.Text==symbol)==1&&scene.Labels.Single(label=>label.Text==symbol).Y==QuickSheet.IconKeyTop,"One shared symbol key stays at the bottom: "+symbol);
            Check(!scene.Labels.Any(label=>inputs.Any(input=>label.Text.Contains(input.Name,StringComparison.Ordinal))),"Source filenames never appear in combined switch headers.");
            Check(scene.Shapes.Where(shape=>shape.Owner.Length>0).All(shape=>shape.Points.Max(point=>point.Y)<QuickSheet.IconKeyTop),"Physical switch geometry cannot overlap the reserved bottom key.");
            Check(scene.Labels.Single(label=>label.Text.StartsWith(ReportText.CableMoveDisclaimer)).Y>QuickSheet.IconKeyTop+10,"The cable warning stays below the icon key.");
            Check(!scene.Labels.Any(label=>label.Text.Contains("WORKSPACE PORT MAPS",StringComparison.Ordinal)),"Combined sheet has no redundant workspace title bar.");
            Check(scene.Labels.Where(label=>label.Color=="#30465c").All(label=>ReportRenderer.Measure(label).Text==label.Text),"Every shared legend line wraps fully instead of truncating or ellipsizing.");
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
        File.WriteAllText(Path.Combine(root,prefix+"-plan.json"),JsonSerializer.Serialize(new{result.Placements,result.LegendEntries,result.Warnings,SideKeys=result.Pages.Select(page=>page.Labels.Single(label=>label.Text=="VLAN key")).Select(label=>new{label.X,label.Y}),PageCount=result.Pages.Count},new JsonSerializerOptions{WriteIndented=true}));
    }
    static int Main(string[] args)
    {
        if(args.Length==3&&args[0]=="--atomic")return CheckLocalAtomic(args[1],args[2]);
        if(args.Length==3&&args[0]=="--workspace")return CheckLocalWorkspace(args[1],args[2]);
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
            Check(together.Placements.First().Y==WorkspaceSheet.ContentTop,"Without a title the switches start at the top; the symbol key is at the bottom.");
            Check(together.LegendEntries.Count(entry=>entry.VlanId==10)==1&&together.LegendEntries.Count(entry=>entry.VlanId==150)==1,"Matching VLAN IDs are deduplicated in the common side legend.");
            var sharedPalette=WorkspacePalette.Create(inputs.Select(input=>(input.Id,input.Inventory)).ToArray());
            foreach(var input in inputs)
            {
                var rendered=JsonSerializer.Deserialize<Inventory>(JsonSerializer.Serialize(input.Inventory))!;sharedPalette.Apply(input.Id,rendered);
                var compact=QuickSheet.BuildCompact(rendered,Charts.Layout(rendered),"SHEET-TEST",together.MarkerRadius,false);
                var onSheet=together.Pages[0].Shapes.Where(shape=>shape.Owner.StartsWith(input.Id+":",StringComparison.Ordinal)).ToArray();
                Check(onSheet.Length==compact.Scene.Shapes.Count(shape=>shape.Owner.Length>0),"All outlines and native-colored physical faces survive composition.");
                foreach(var group in compact.Scene.Shapes.Where(shape=>shape.Owner.Length>0).GroupBy(shape=>(shape.Fill,shape.Stroke)))
                    Check(onSheet.Count(shape=>shape.Fill==group.Key.Fill&&shape.Stroke==group.Key.Stroke)==group.Count(),"Native VLAN fills/LAG outlines retain source colors and multiplicity.");
                var area=together.Placements.Single(item=>item.Id==input.Id);
                var dots=together.Pages[0].Dots.Where(dot=>dot.X<together.Pages[0].DiagramBodyWidth&&dot.Y>=area.Y&&dot.Y<area.Y+area.Height).ToArray();
                Check(dots.Length==compact.Scene.Dots.Count(dot=>dot.Y>=QuickSheet.DiagramTop),"Tagged/voice port markers and visual guide are retained exactly while duplicate VLAN keys are replaced by the common key.");
                Check(dots.Count(dot=>dot.Voice)==compact.Scene.Dots.Count(dot=>dot.Voice&&dot.Y>=QuickSheet.DiagramTop),"Voice diamonds remain distinct from tagged circles.");
            }
            Save(together,root,"two-on-one");
            var single=QuickSheet.Build(alpha,Charts.Layout(alpha),"BOTTOM-KEY");
            foreach(string symbol in new[]{"Native VLAN","Tagged VLAN","Voice VLAN","LAG / trunk"})
                Check(single.Scene.Labels.Count(label=>label.Text==symbol)==1&&single.Scene.Labels.Single(label=>label.Text==symbol).Y==QuickSheet.IconKeyTop,"Standalone diagrams use exactly one bottom icon key: "+symbol);
            Check(single.Scene.Shapes.Where(shape=>shape.Owner.Length>0).All(shape=>shape.Points.Max(point=>point.Y)<QuickSheet.IconKeyTop),"Standalone sockets leave the bottom key clear.");
            Pdf.Save(new(){single.Scene},Path.Combine(root,"single-bottom-key.pdf"));Png.Save(single.Scene,Path.Combine(root,"single-bottom-key.png"),3508,2480,true);
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
            Check(denseSheets.Pages.Count>1,"Dense and tall configurations paginate without losing markers or legend rows.");Save(denseSheets,root,"dense-fallback");
            var atomic=DetailOverflow();atomic.Hostname="ATOMIC-STACK";
            foreach(var port in atomic.Ports)port.Description="";
            atomic.Ports[0].Description="Uplink to core";
            atomic.Vlans.Add(new(){Id=10,Name="Shared alias"});
            CheckAtomic(atomic,root,"atomic-stack");
            var overflow=DetailOverflow();
            bool legacyOverflow=false;
            try{QuickSheet.BuildCompact(overflow,Charts.Layout(overflow),"SYNTHETIC",null,false,749.89);}
            catch(OnePageCapacityException ex){legacyOverflow=ex.Message.Contains("details need more space");}
            Check(legacyOverflow,"Synthetic tall stack reproduces the non-paginating trunk/description capacity failure, not a physical geometry failure.");
            var detailInputs=new[]{Item("detail-before",Aruba("DETAIL-BEFORE")),Item("detail-overflow",overflow),Item("detail-after",Router("DETAIL-AFTER"))};
            var detailSheets=WorkspaceSheet.Build(detailInputs);Validate(detailSheets,detailInputs);
            Check(detailSheets.Pages.Count>2&&detailSheets.Warnings.Any(warning=>warning.StartsWith("Trunk / port-description details")),"Overflowing detail sections receive readable additional sheets.");
            var detailText=string.Concat(detailSheets.Pages.SelectMany(page=>page.Labels).Select(label=>label.Text.Replace(" ","")));
            foreach(var port in overflow.Ports.Where(port=>port.Description.Length>0))Check(detailText.Contains(port.Description.Replace(" ","")),"Every complete port description survives pagination: "+port.Id);
            foreach(string lag in overflow.Ports.Select(port=>port.Aggregate).Where(value=>value.Length>0).Distinct())Check(detailText.Contains(lag+"(LACP):"),"Every LAG identity and mode survives continuation: "+lag);
            Check(detailSheets.Pages.Any(page=>page.Labels.Any(label=>label.Text.Contains("Details continued"))),"Additional detail pages identify the switch and their continuation purpose.");
            Save(detailSheets,root,"detail-overflow");
            // One oversized description must split at wrapped lines, not disappear.
            var huge=Aruba("ONE-LONG-DESCRIPTION");huge.Ports[0].Description=string.Join(' ',Enumerable.Range(1,900).Select(index=>"Token"+index.ToString("D3")));
            var hugeInputs=new[]{Item("huge-description",huge)};var hugeSheets=WorkspaceSheet.Build(hugeInputs,title:"Details room");Validate(hugeSheets,hugeInputs);
            var hugeText=string.Concat(hugeSheets.Pages.SelectMany(page=>page.Labels).Select(label=>label.Text.Replace(" ","")));
            foreach(int token in Enumerable.Range(1,900))Check(hugeText.Contains("Token"+token.ToString("D3")),"A single oversized entry keeps token "+token);
            Check(hugeSheets.Pages.Count>1,"One exceptionally long description can continue across pages with an optional title.");
            string logoPath=Path.Combine(root,"synthetic-logo.png");
            using(var bitmap=new SKBitmap(120,30)){bitmap.Erase(SKColors.DarkCyan);using var image=SKImage.FromBitmap(bitmap);using var bytes=image.Encode(SKEncodedImageFormat.Png,100);File.WriteAllBytes(logoPath,bytes.ToArray());}
            var branded=WorkspaceSheet.Build(inputs,ReportLogo.Load(logoPath),"Customer workspace");Validate(branded,inputs);
            Check(branded.Pages.All(page=>page.Images.Count==1),"The first switch header supports one undistorted report logo on every page without an added title bar.");Save(branded,root,"branded-sheet");
            var longTitle=WorkspaceSheet.Build(inputs,ReportLogo.Load(logoPath),new string('W',100));Validate(longTitle,inputs);
            Check(longTitle.Pages.All(page=>string.Concat(page.Labels.Where(label=>label.Y<page.Boxes.Single(box=>box.Role=="DocumentTitle").H).Select(label=>label.Text))==new string('W',100)),"Optional long document title wraps completely without clipping.");
            Check(together.Pages.All(page=>!page.Boxes.Any(box=>box.Role=="DocumentTitle")),"Document title is absent by default.");
            var titled=WorkspaceSheet.Build(inputs,ReportLogo.Load(logoPath),"West Closet");Validate(titled,inputs);
            Check(titled.Pages.All(page=>page.Labels.Count(label=>label.Text=="West Closet")==1),"Opt-in workspace title appears once per sheet.");Save(titled,root,"titled-sheet");
            var conflictA=Aruba("CONFLICT-DATA");var conflictB=Aruba("CONFLICT-SERVERS");var conflictC=Aruba("CONFLICT-UNNAMED");
            conflictB.Vlans.Single(vlan=>vlan.Id==10).Name="Servers";conflictC.Vlans.Single(vlan=>vlan.Id==10).Name="";
            var conflicts=new[]{Item("conflict-a",conflictA),Item("conflict-b",conflictB),Item("conflict-c",conflictC)};
            var conflictSheets=WorkspaceSheet.Build(conflicts);Validate(conflictSheets,conflicts);
            Check(conflictSheets.Pages.Count==1,"Three ordinary switches with different names for the same VLAN fit beside one common ID key.");
            var tens=conflictSheets.LegendEntries.Where(entry=>entry.VlanId==10).ToArray();
            Check(tens.Length==1&&tens[0].Identity=="vlan:10","Matching VLAN IDs use exactly one legend identity and color regardless of name.");
            Check(tens[0].Label.Contains("Data / Servers")&&!tens[0].Label.Contains("Unnamed"),"The shared ID key retains all named aliases; blank names do not create another meaning.");
            Check(conflictSheets.LegendEntries.Count(entry=>entry.VlanId==150)==1,"A shared named voice VLAN deduplicates despite other conflicts.");Save(conflictSheets,root,"conflicting-key");
            var unnamedA=Router("UNNAMED-A");var unnamedB=Router("UNNAMED-B");
            foreach(var inv in new[]{unnamedA,unnamedB}){inv.Vlans.Add(new(){Id=20});inv.Ports[0].UntaggedVlan=20;inv.Ports[0].UntaggedState="Known";inv.Ports[1].UntaggedState="None";}
            var unnamed=new[]{new WorkspaceSheetItem("unnamed-a","same.cfg",unnamedA),new WorkspaceSheetItem("unnamed-b","same.cfg",unnamedB)};
            var unnamedSheets=WorkspaceSheet.Build(unnamed);Validate(unnamedSheets,unnamed);
            Check(unnamedSheets.LegendEntries.Count(entry=>entry.VlanId==20)==1,"Two unnamed VLANs with the same ID share one legend entry.");
            Check(unnamedSheets.Pages[0].Labels.Count(label=>label.Text=="Native not supplied")==1&&unnamedSheets.Pages[0].Labels.Count(label=>label.Text=="No native VLAN")==1,"Native unknown/none legend states are retained once, not repeated per switch.");Save(unnamedSheets,root,"unnamed-key");
            var ambiguous=Aruba("AMBIGUOUS");ambiguous.Vlans.Add(new(){Id=10,Name="Second meaning"});
            var ambiguousInput=new[]{Item("ambiguous",ambiguous),Item("ambiguous-peer",Aruba("AMBIGUOUS-PEER"))};var ambiguousSheets=WorkspaceSheet.Build(ambiguousInput);Validate(ambiguousSheets,ambiguousInput);
            Check(ambiguousSheets.LegendEntries.Any(entry=>entry.Label.Contains("Data / Second meaning")),"Internally ambiguous names are not silently collapsed into the first name.");Save(ambiguousSheets,root,"ambiguous-key");
            var manyA=Dense();manyA.Hostname="LONG-KEY-A";var manyB=Dense();manyB.Hostname="LONG-KEY-B";
            foreach(var inv in new[]{manyA,manyB})foreach(var vlan in inv.Vlans)vlan.Name="Infrastructure Management BackPlane Segment "+vlan.Id;
            var manyInput=new[]{Item("long-key-a",manyA),Item("long-key-b",manyB)};var manySheets=WorkspaceSheet.Build(manyInput);Validate(manySheets,manyInput);
            Check(manySheets.LegendEntries.Select(entry=>entry.VlanId).Distinct().Count()==41,"Every long-name VLAN appears across the complete side-key pages.");
            Check(manySheets.Pages.Count>1&&manySheets.Pages.All(page=>page.Labels.Any(label=>label.Text.StartsWith("Key page "))),"Dense side keys paginate explicitly instead of shrinking names or moving below switches.");Save(manySheets,root,"many-wrapped-key");
            var contradictory=inputs.Select(input=>input with{Inventory=JsonSerializer.Deserialize<Inventory>(JsonSerializer.Serialize(input.Inventory))!}).ToArray();
            foreach(var input in contradictory)sharedPalette.Apply(input.Id,input.Inventory);
            contradictory[1].Inventory.VlanColorOverrides![10]=sharedPalette.ForItem("alpha").Vlans[150];
            bool rejected=false;try{WorkspaceSheet.Build(contradictory);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Direct sheet calls reject contradictory provided colors for the same VLAN semantic identity.");
            Console.WriteLine("PASS "+assertions+" shared-sheet geometry/order/readability/preservation assertions. Fixtures: "+root);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{if(args.Length==0)Directory.Delete(root,true);}
    }
}
