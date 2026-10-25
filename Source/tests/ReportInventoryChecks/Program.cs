using SwitchportGenerator;
using System.Text.Json;
using System.Text.RegularExpressions;

static class ReportInventoryChecks
{
    static int checks;
    static readonly int[] SparseIds={2,7,11,21,49,81,99,101,117,141,197,241,257,331,503,701,997,1009,1201,1501,2003,2503,3001,4001,4094,
        5,17,43,67,89,137,283,419,619,1237,1879,2539,3019,4003,4093};
    static void Assert(bool value,string message)
    {
        checks++;
        if(!value)throw new Exception(message);
    }
    static Inventory Inventory(int count)
    {
        var inv=new Inventory{Hostname="PDF-VLAN-REGRESSION",OS="Test fixture",Firmware="7.0",Topology="Fixed"};
        inv.Groups.Add(new SwitchportGenerator.Group{Id="Switch",Device="1",Product="JL256A",Copper=count,Optical=0});
        for(int i=1;i<=count;i++)inv.Ports.Add(new Port{Id="P"+i.ToString("D3"),Group="Switch",Number=i,Description="Fixture port "+i,Mode="Trunk",Media="RJ-45",UntaggedVlan=1,UntaggedState="Known",Disabled=false});
        return inv;
    }
    static Scene Cover()
    {
        var s=new Scene{Width=1190,Height=842};
        s.Rect(0,0,1190,84,"#13283d","#13283d");
        s.Text(30,17,"PDF VLAN inventory regression fixture",23,"#ffffff",true,1130);
        s.Text(30,777,ReportText.CableMoveDisclaimer,10,"#34516b");
        return s;
    }
    static List<int> Numbers(IEnumerable<Label> labels)=>Regex.Matches(string.Join(" ",labels.Select(l=>l.Text)),@"\b\d+\b").Select(m=>int.Parse(m.Value)).ToList();
    static void FocusedSemantics()
    {
        var native=Inventory(4);
        native.Ports[0].UntaggedVlan=10;
        native.Ports[1].StackLink=true;
        native.Ports[2].UntaggedVlan=null;native.Ports[2].UntaggedState="Unknown";
        native.Ports[3].UntaggedVlan=null;native.Ports[3].UntaggedState="None";
        var chart=Cover();chart.VlanColors=new Dictionary<int,string>{{10,"#abcd12"}};
        var nativePages=Charts.Booklet(native,chart,"NATIVE-PALETTE");
        var samples=nativePages.Skip(1).SelectMany(p=>p.Boxes.Where(b=>b.Role=="InventoryNative")).ToList();
        Assert(samples.Count==4,"Native-color fixture must retain one sample for each source port.");
        Assert(Charts.Color(native.Ports[0])!="#abcd12","Native fixture must distinguish chart metadata from the old fixed palette.");
        Assert(samples.Select(s=>s.Fill).SequenceEqual(new[]{"#abcd12","#000000","#e6e6e6","#ffffff"}),"Inventory native samples must match chart.VlanColors and preserve Stack/Unknown/None reserved fills.");
        Assert(nativePages.Skip(1).All(p=>Charts.ValidateScene(p).Count==0),"Native-color fixture must remain free of cell/marker collisions.");

        var all=Inventory(2);
        all.Ports[0].Notes.Add("Tagged VLANs: all allowed by configuration; exact IDs are unspecified.");
        all.Ports[1].Notes.Add("Description mentions Tagged VLANs: all but this is not an all-membership directive.");
        var allPages=Charts.Booklet(all,Cover(),"EXPLICIT-ALL");
        var firstCell=allPages.Skip(1).SelectMany(p=>p.Labels.Where(l=>l.Owner=="inventory:"+all.Ports[0].Id+":4")).ToList();
        var secondCell=allPages.Skip(1).SelectMany(p=>p.Labels.Where(l=>l.Owner=="inventory:"+all.Ports[1].Id+":4")).ToList();
        Assert(firstCell.Count==1&&firstCell[0].Text=="All","An explicit all-tagged directive must print All in the tagged cell.");
        Assert(Numbers(firstCell).Count==0&&all.Ports[0].TaggedVlans.Count==0,"All must not invent or populate concrete VLAN IDs.");
        Assert(secondCell.Count==0,"A descriptive mention must not become an explicit All directive.");
        Assert(ReportRenderer.Measure(firstCell[0]).Text=="All","The All indicator must render fully without clipping.");
        Console.WriteLine("PASS focused inventory semantics: chart palette native fills, reserved states, explicit All indication and no invented VLAN IDs.");
    }
    static void CheckAndWrite(string output,string name,Inventory inv,bool spansPages=false,bool withLogo=false)
    {
        var pages=Charts.Booklet(inv,Cover(),"REGRESSION");
        var inventoryPages=pages.Skip(1).ToList();
        Assert(inventoryPages.Count>0,name+": no inventory page.");
        foreach(var port in inv.Ports)
        {
            string owner="inventory:"+port.Id+":4";
            var tags=inventoryPages.SelectMany(p=>p.Labels.Where(l=>l.Owner==owner)).ToList();
            Assert(Numbers(tags).SequenceEqual(port.TaggedVlans),name+": tagged VLANs are incomplete/reordered for "+port.Id+". Expected "+port.TaggedVlans.Count+", received "+Numbers(tags).Count+".");
            if(port.TaggedVlans.Count>25)Assert(tags.Count>1,name+": long tagged list must wrap.");
            var descriptions=inventoryPages.SelectMany(p=>p.Labels.Where(l=>l.Owner=="inventory:"+port.Id+":1")).ToList();
            string Clean(string text)=>Regex.Replace(text,@"\s+","");
            Assert(Clean(string.Join("",descriptions.Select(l=>l.Text)))==Clean(port.Description),name+": description text was dropped or truncated for "+port.Id+".");
        }
        foreach(var page in inventoryPages)
        {
            Assert(page.Labels.Count(l=>l.Text=="Tagged VLANs")==1,name+": table header is not repeated exactly once.");
            Assert(page.Labels.Any(l=>l.Text==ReportText.CableMoveDisclaimer),name+": missing cable-move disclaimer.");
            var tableLabels=page.Labels.Where(l=>l.Owner.StartsWith("inventory:",StringComparison.Ordinal)).ToList();
            foreach(var l in tableLabels)
            {
                var measured=ReportRenderer.Measure(l);
                Assert(measured.Text==l.Text,name+": ellipsized table cell: "+l.Owner+".");
                Assert(l.Size>=10,name+": table text was reduced below the readable fixed size.");
                Assert(l.X>=30&&l.X+measured.Width<=1170.01&&l.Y>=132&&l.Y+measured.Height<=728.01,name+": table cell exceeds its content area: "+l.Owner+".");
            }
            var errors=Charts.ValidateScene(page);
            Assert(errors.Count==0,name+": scene layout failure: "+string.Join("; ",errors));
        }
        if(spansPages)
        {
            var giant=inv.Ports.Single(p=>p.TaggedVlans.Count==4094);
            string owner="inventory:"+giant.Id+":4";
            var continued=inventoryPages.Where(p=>p.Labels.Any(l=>l.Owner==owner)).ToList();
            Assert(continued.Count>=3,name+": giant tagged list must span several inventory pages.");
            foreach(var page in continued)
            {
                string identity=string.Join("",page.Labels.Where(l=>l.Owner=="inventory:"+giant.Id+":0"&&l.Text!="Continued").Select(l=>l.Text));
                Assert(identity==giant.Id,name+": continuation page lost the complete original port identity.");
            }
            Assert(continued.Skip(1).All(p=>p.Labels.Any(l=>(l.Owner=="inventory-context"||l.Owner=="inventory:"+giant.Id+":0")&&l.Text.Contains("Continued",StringComparison.OrdinalIgnoreCase))),name+": continuation pages need an explicit continued-row cue.");
        }
        if(withLogo)
        {
            string logoPath=Path.Combine(output,"test-logo.png");
            using(var surface=SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(240,70)))
            {
                surface.Canvas.Clear(SkiaSharp.SKColors.White);
                using var paint=new SkiaSharp.SKPaint{Color=SkiaSharp.SKColors.Navy};
                surface.Canvas.DrawRect(10,10,220,50,paint);
                using var image=surface.Snapshot();using var encoded=image.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
                File.WriteAllBytes(logoPath,encoded.ToArray());
            }
            var logo=ReportLogo.Load(logoPath);foreach(var page in pages)logo.Apply(page);
            Assert(pages.All(p=>p.Images.Count==1),name+": logo must be applied to every continuation page.");
            Assert(pages.All(p=>Charts.ValidateScene(p).Count==0),name+": logo collides with wrapped/paginated inventory.");
        }
        string pdfPath=Path.Combine(output,name+".pdf");Pdf.Save(pages,pdfPath);
        File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(new{
            Name=name,Ports=inv.Ports.Select(p=>new{p.Id,p.Description,p.TaggedVlans}),ExpectedTagSequence=inv.Ports.SelectMany(p=>p.TaggedVlans),
            PageCount=pages.Count,InventoryPageCount=inventoryPages.Count,WithLogo=withLogo,
            TaggedColumn=new{Left=645.0,Right=875.0,Top=132.0,Bottom=728.0},SceneWidth=1190.0,SceneHeight=842.0,
            Pages=pages.Select((p,i)=>new{Index=i,Cells=p.Labels.Where(l=>l.Owner.StartsWith("inventory:",StringComparison.Ordinal)).Select(l=>new{l.Owner,l.Text,l.X,l.Y,l.Width,l.Size})})
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS "+name+": "+inv.Ports.Sum(p=>p.TaggedVlans.Count)+" exact tagged VLAN entries on "+inventoryPages.Count+" inventory page(s).");
    }
    static int Main(string[] args)
    {
        try
        {
            FocusedSemantics();
            if(args.Contains("--focused-only")){Console.WriteLine("PASS "+checks+" focused PDF inventory assertions.");return 0;}
            string output=Path.GetFullPath(args.FirstOrDefault()??Path.Combine(Path.GetTempPath(),"Switchport-pdf-vlan-checks"));Directory.CreateDirectory(output);
            var sparse25=Inventory(3);sparse25.Ports[0].TaggedVlans=SparseIds.Take(25).Order().ToList();sparse25.Ports[1].TaggedVlans=new(){7,111,2222,4094};
            CheckAndWrite(output,"sparse-25",sparse25);
            var sparse40=Inventory(4);sparse40.Ports[0].TaggedVlans=SparseIds.Order().ToList();sparse40.Ports[1].TaggedVlans=Enumerable.Range(101,40).ToList();sparse40.Ports[2].TaggedVlans=new(){1,8,99,101,1001,2001,3001,4001,4094};
            sparse40.Ports[0].Description="This uplink description must remain complete when its tagged VLAN list wraps across several lines.";
            CheckAndWrite(output,"sparse-40-and-consecutive-ids",sparse40,withLogo:true);
            var giant=Inventory(3);giant.Ports[0].TaggedVlans=new(){2,4,6};giant.Ports[1].Id="1/Slot-A/Backbone-Distribution-Port-0002";giant.Ports[1].TaggedVlans=Enumerable.Range(1,4094).ToList();giant.Ports[2].TaggedVlans=new(){17,1009,4093};
            giant.Ports[1].Description="A single physical port with every supported VLAN identifier. Its identity must stay visible on every continued page.";
            CheckAndWrite(output,"all-4094-giant-port-row",giant,spansPages:true,withLogo:true);
            var many=Inventory(29);foreach(var p in many.Ports){p.TaggedVlans=SparseIds.Take(p.Number%2==0?25:40).Order().ToList();p.Description="Distribution uplink "+p.Number+" with a long description that needs wrapping and cannot overwrite the next inventory row.";}
            many.Ports[3].Id="INTERFACE-"+new string('W',120)+"-0004";many.Ports[4].Description=string.Join(' ',Enumerable.Range(1,900).Select(n=>"Item"+n.ToString("D4")));
            CheckAndWrite(output,"long-fields-many-variable-rows",many,withLogo:true);
            // A complete supported input for an ordinary end-to-end generator run.
            string source="; JL256A Configuration Editor\nhostname PDF-END-TO-END\ninterface 1\n name Distribution-Uplink\n exit\n"+string.Join("\n",SparseIds.Take(25).Order().Select(id=>$"vlan {id}\n name Segment-{id}\n tagged 1\n exit\n"));
            File.WriteAllText(Path.Combine(output,"normal-25-tagged-vlans.cfg"),source);
            Console.WriteLine("PASS "+checks+" PDF inventory assertions.");Console.WriteLine("Fixtures: "+output);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
