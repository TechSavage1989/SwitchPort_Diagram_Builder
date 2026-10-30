using SwitchportGenerator;
static class GeometryChecks
{
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static bool Proportional(QuickSheet.Result sheet)=>sheet.Sockets.All(p=>Math.Abs(p.Tile.W-QuickSheet.PortWidth*sheet.GeometryScale)<.001&&Math.Abs(p.Tile.H-(p.Optical?QuickSheet.OpticalHeight:QuickSheet.PortHeight)*sheet.GeometryScale)<.001);
    static bool SamePoints((double X,double Y)[] a,(double X,double Y)[] b)=>a.Length==b.Length&&a.Zip(b).All(pair=>Math.Abs(pair.First.X-pair.Second.X)<1e-6&&Math.Abs(pair.First.Y-pair.Second.Y)<1e-6);
    static int Main()
    {
        try{Run();return 0;}catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
    static void Run()
    {
        CatalogueChecks();
        WrappedStackChecks();
        foreach(string product in new[]{"JL256A","JL558A","JL661A","JL662A","J9536A","J9990A","J9538A","J9993A","J9534A","J9986A","J9987A"})
        {
            var inv=Parser.Parse((product is "JL661A" or "JL662A"?"! AOS-CX ":"; ")+product+" Configuration Editor\nhostname TEST\n");var placements=Charts.Layout(inv);
            Assert(Charts.Validate(inv,placements).Count==0,"Base geometry invalid: "+product);
            var cleanScene=Charts.GeometryReferenceScene(inv,placements,"TEST");
            Assert(Charts.ValidateScene(cleanScene).Count==0,"Scene text/shape collision: "+product+": "+string.Join(",",Charts.ValidateScene(cleanScene)));
            var injected=Charts.GeometryReferenceScene(inv,placements,"TEST");injected.Labels.Add(new Label(placements[0].Tile.X,placements[0].Tile.Y,"COLLISION",12));
            Assert(Charts.ValidateScene(injected).Count>0,"Injected text/port collision must be rejected");
            int copper=inv.Groups[0].Copper;
            if(copper>0){Assert(placements[0].Tile.Y<placements[1].Tile.Y,"Odd/even rows");Assert(placements[0].Tile.X==placements[1].Tile.X,"Odd/even column");}
            if(product is "JL256A" or "JL558A")Assert(placements[copper].Tile.X<placements[0].Tile.X,"Optical bank must be left");
            if(product is "JL661A" or "JL662A" or "J9990A")Assert(placements[copper].Tile.X>placements[0].Tile.X,"Optical bank must be right");
            if(product is "J9538A" or "J9993A")Assert(placements.Select(p=>p.Tile.Y).Distinct().Count()==1,"Optical-only row exception");
            var bad=placements.ToList();bad[0]=bad[0] with {Tile=bad[0].Tile with {X=-10}};
            Assert(Charts.Validate(inv,bad).Count>0,"Out-of-bounds geometry must fail");
            bad=placements.ToList();bad[0]=bad[0] with {Marker=bad[0].Marker with {Y=bad[0].Tile.Y+5}};
            Assert(Charts.Validate(inv,bad).Count>0,"Marker/label overlap must fail");
            bad=placements.ToList();bad[1]=bad[1] with {Tile=bad[0].Tile};Assert(Charts.Validate(inv,bad).Count>0,"Tile overlap must fail");
            inv.Ports.RemoveAt(0);Assert(Charts.Validate(inv,placements).Count>0,"Count/order mismatch must fail");
        }
        var port=new Port{UntaggedVlan=10,UntaggedState="Known"};Assert(Charts.Color(port)=="#4a90e2","Native color");port.TaggedVlans.Add(254);Assert(Charts.Color(port)=="#4a90e2","Tagged VLAN must not change fill");port.StackLink=true;Assert(Charts.Color(port)=="#000000","Stack color");
        var visual=Parser.Parse("; JL256A Configuration Editor\ntrunk 49-50 trk1 lacp\nvlan 10\n untagged 1-48,trk1\n exit\nvlan 110\n tagged 1,trk1\n exit\nvlan 120\n tagged trk1\n exit\n");
        var layout=Charts.Layout(visual);var scene=Charts.GeometryReferenceScene(visual,layout,"TEST");
        Assert(scene.Dots.Count(d=>layout.Any(place=>d.X>=place.Marker.X&&d.X<=place.Marker.X+44&&d.Y>=place.Marker.Y&&d.Y<=place.Marker.Y+place.Marker.H))==visual.Ports.Sum(p=>p.TaggedVlans.Count),"Every tagged VLAN needs a dot");
        foreach(var place in layout)
        {
            var p=visual.Ports.Single(p=>p.Id==place.Port);
            var outline=scene.Shapes.Single(shape=>Math.Abs(shape.Points.Min(v=>v.X)-place.Tile.X)<0.01&&Math.Abs(shape.Points.Min(v=>v.Y)-place.Tile.Y)<0.01&&Math.Abs(shape.Points.Max(v=>v.X)-place.Tile.X-44)<0.01);
            Assert(outline.Fill==Charts.Color(p),"Shape fill must match native VLAN");
            Assert(outline.Points.Length==8&&Math.Abs(outline.Points[0].X-place.Tile.X-(p.Media=="RJ-45"?0:4))<0.01,"Distinct RJ-45 and optical silhouettes");
            Assert(outline.StrokeWidth==(p.Aggregate.Length>0?3.2:0.8),"Visible aggregation border");
            Assert(outline.Stroke==(p.Aggregate.Length>0?Charts.AggregateColors(visual)[p.Aggregate]:"#172333"),"Aggregate outline matches its own key color");
            var dots=scene.Dots.Where(d=>d.X>=place.Marker.X&&d.X<=place.Marker.X+44&&d.Y>=place.Marker.Y&&d.Y<=place.Marker.Y+place.Marker.H).ToList();
            Assert(dots.Count==p.TaggedVlans.Count,"Tagged dots stay in their port marker area");
            Assert(dots.Select(d=>d.Fill).SequenceEqual(p.TaggedVlans.Order().Select(Charts.VlanColor)),"Dots match VLAN palette");
        }
        var aggregateConfig="; JL558A Configuration Editor\n"+string.Join("\n",Enumerable.Range(1,14).Select(i=>$"trunk {i*2-1}-{i*2} trk{i} "+(i==14?"trunk":"lacp")))+"\nvlan 10\n untagged 1-52\n exit\n";
        var aggregates=Parser.Parse(aggregateConfig);var aggregateScene=Charts.GeometryReferenceScene(aggregates,Charts.Layout(aggregates),"TEST");
        var colors=Charts.AggregateColors(aggregates);
        Assert(colors.Count==14&&colors.Values.Distinct().Count()==14,"Each of fourteen aggregates has a distinct outline color");
        Assert(colors.SequenceEqual(Charts.AggregateColors(aggregates)),"Color assignment is deterministic for the same configuration");
        foreach(var p in aggregates.Ports.Where(p=>p.Aggregate.Length>0))
            Assert(AggregatePalette.Difference(colors[p.Aggregate],Charts.Color(p))>=AggregatePalette.MinimumMemberDistance,"LAG colors must remain perceptually separate from every member's native fill");
        foreach(var a in colors)foreach(var b in colors.Where(b=>string.CompareOrdinal(a.Key,b.Key)<0))
            Assert(AggregatePalette.Difference(a.Value,b.Value)>=22,"Fourteen LAG groups retain separated outline colors");
        foreach(var a in colors)
        {
            Assert(aggregateScene.Shapes.Count(s=>s.Stroke==a.Value&&s.StrokeWidth==3.2)==3,"Two member outlines plus a matching key outline per aggregate");
            Assert(aggregateScene.Labels.Any(l=>l.Text==a.Key+(a.Key=="trk14"?" (TRUNK)":" (LACP)")),"Key identifies aggregate and configured mode");
        }
        Assert(Charts.ValidateScene(aggregateScene).Count==0,"Fourteen aggregate entries must fit without collision");
        foreach(var p in aggregates.Ports)p.Aggregate="trk1";
        var wrapped=Charts.GeometryReferenceScene(aggregates,Charts.Layout(aggregates),"TEST");
        Assert(Charts.ValidateScene(wrapped).Count==0,"Long aggregate member lists wrap without collision");
        Assert(wrapped.Labels.Count(l=>l.Text.Contains(", "))>1,"Long member list wraps into multiple lines");
        var cxLag=Parser.Parse("! AOS-CX JL661A\ninterface lag 1\n lacp mode active\n exit\ninterface 1/1/49-1/1/50\n lag 1\n exit\n");
        var cxScene=Charts.GeometryReferenceScene(cxLag,Charts.Layout(cxLag),"TEST");
        Assert(cxScene.Labels.Any(l=>l.Text=="lag1 (LACP ACTIVE)"),"CX LACP mode appears in aggregate key");
        Assert(Charts.ValidateScene(cxScene).Count==0,"CX aggregate key has no collision");
        visual.Ports[0].TaggedVlans=Enumerable.Range(200,90).ToList();
        layout=Charts.Layout(visual);scene=Charts.GeometryReferenceScene(visual,layout,"TEST");
        Assert(Charts.Validate(visual,layout).Count==0,"Wrapped dot rows need nonoverlapping geometry");
        Assert(scene.Dots.Count(d=>layout.Any(place=>d.X>=place.Marker.X&&d.X<=place.Marker.X+44&&d.Y>=place.Marker.Y&&d.Y<=place.Marker.Y+place.Marker.H))==visual.Ports.Sum(p=>p.TaggedVlans.Count),"Large VLAN lists must not lose dots");
        Assert(layout[0].Marker.H>=91,"Marker area must grow for many tagged VLANs");
        Assert(Charts.VlanColor(201)==Charts.VlanColor(201)&&Charts.VlanColor(201)!=Charts.VlanColor(202),"Stable additional VLAN colors");
        foreach(var entry in scene.Labels.Where(label=>label.Text.StartsWith("VLAN ")&&int.TryParse(label.Text[5..],out _)))
        {
            int id=int.Parse(entry.Text[5..]);
            Assert(scene.Dots.Any(d=>Math.Abs(d.X-(entry.X-12))<0.01&&Math.Abs(d.Y-(entry.Y+7))<0.01&&d.Fill==Charts.VlanColor(id)),"VLAN key must pair its block with a matching colored dot");
            Assert(scene.Boxes.Any(b=>Math.Abs(b.X-(entry.X-44))<0.01&&Math.Abs(b.Y-(entry.Y-1))<0.01&&b.Fill==Charts.VlanColor(id)),"VLAN key needs matching fill block");
        }
        var stack=Parser.Parse("; Aruba Configuration Editor\nvsf member 1 type JL256A\nvsf member 2 type JL256A\nvsf member 1 link 1 49-50\nvsf member 2 link 1 2/49-2/50\n");
        Assert(stack.Blockers.Count==0,"Relative and fully qualified stack link syntax");
        Assert(stack.Ports.Count(p=>p.StackLink)==4,"All explicit VSF links must be detected");
        Assert(stack.Ports.Where(p=>p.StackLink).All(p=>Charts.Color(p)=="#000000"),"VSF links must be pure black");
        var stackScene=Charts.GeometryReferenceScene(stack,Charts.Layout(stack),"TEST");
        Assert(stackScene.Boxes.Any(b=>b.Fill=="#000000"),"Key includes black stack reference");
        var voiced=Parser.Parse("; JL256A Configuration Editor\nhostname \"VOICE-SWITCH\"\nvlan 150\n name \"Voice\"\n tagged 1-52\n voice\n exit\nvlan 10\n untagged 1-52\n exit\n");
        voiced.Hostname=new string('X',600);
        var voicedScene=Charts.GeometryReferenceScene(voiced,Charts.Layout(voiced),"TEST");
        Assert(Charts.ValidateScene(voicedScene).Count==0,"Long hostname and voice diamonds must not collide");
        Assert(voicedScene.Dots.Count(d=>d.Voice)==53,"Every voice member plus its legend reference has a diamond marker");
        Assert(!voicedScene.Labels.Any(l=>l.Text.StartsWith("VOICE ")),"Voice badges are removed");
        Assert(Charts.Layout(voiced)[0].Marker.H==12,"Voice-only labeling must not add a badge row");
        Assert(voicedScene.Labels.Any(l=>l.Text=="VLAN 150 (Voice)"),"Voice VLAN is identified in the key");
        Assert(voicedScene.Labels.Any(l=>l.Text=="Voice"&&l.Width==152),"Configured VLAN name appears in a constrained second key line");
        voiced.Vlans.Single(v=>v.Id==150).Name=new string('W',200);
        var namedScene=Charts.GeometryReferenceScene(voiced,Charts.Layout(voiced),"TEST");
        Assert(Charts.ValidateScene(namedScene).Count==0,"Long VLAN names must not collide with adjacent key entries");
        Assert(ReportRenderer.Measure(namedScene.Labels.Single(l=>l.Text==new string('W',200))).Text.EndsWith("..."),"Long VLAN names are shortened to fit");
        Assert(voicedScene.Dots.Where(d=>d.Voice).All(d=>d.Fill==Charts.VlanColor(150)),"Voice diamonds retain the VLAN color");
        voiced.Ports[0].TaggedVlans.Clear();
        var nativeVoiceScene=Charts.GeometryReferenceScene(voiced,Charts.Layout(voiced),"TEST");
        Assert(nativeVoiceScene.Dots.Count(d=>d.Voice)==53,"Voice membership remains visible without a separate tagged VLAN entry");
        Assert(Charts.ValidateScene(nativeVoiceScene).Count==0,"Native voice dot must not collide");
        var multiVoice=Parser.Parse("; JL256A Configuration Editor\nvlan 110\n untagged 1\n voice\n exit\nvlan 200\n tagged 1\n voice\n exit\n");
        var multiScene=Charts.GeometryReferenceScene(multiVoice,Charts.Layout(multiVoice),"TEST");
        Assert(multiScene.Dots.Count(d=>d.Voice)==4,"Native and tagged voice roles both receive diamonds plus two legend references");
        Assert(Charts.ValidateScene(multiScene).Count==0,"Multiple voice dots stay clear of labels");
        var wide=Charts.GeometryReferenceScene(visual,layout,"TEST");Assert(Charts.ValidateScene(wide).Count==0,"Dense tagged list still has no text collision");
        var naming=new Inventory{Hostname="CORE-SW01"};Assert(ReportNaming.FolderName(naming)=="CORE-SW01","Hostname folder");
        string instant1960="config-file-header\nTEST\nvInstantOn_1960_2.7.0.0 (103)\nSKU Description \"Instant On 1960 JL805A\"\n"+string.Join("\n",Enumerable.Range(1,4).Select(i=>$"unit-type unit {i} network te uplink none"))+"\n!\nvoice vlan id 232\nvoice vlan state oui-enabled\n"+string.Join("\n",Enumerable.Range(1,4).Select(i=>$"interface {i}/8\n channel-group 2 mode auto\n!\ninterface {i}/16\n channel-group 1 mode auto\n!"))+"\ninterface TRK1\n switchport general allowed vlan add 1 untagged\n switchport general allowed vlan add 77,232 tagged\n!\ninterface TRK2\n switchport general allowed vlan add 9 untagged\n switchport general allowed vlan add 232 tagged\n!\n";
        var stacked1960=Parser.Parse(instant1960);
        Assert(stacked1960.Blockers.Count==0,"1960 four-unit aggregate fixture imports");
        Assert(stacked1960.Groups.Count==4&&stacked1960.Ports.Count==64,"1960 has four complete 16-port members");
        Assert(stacked1960.Ports.Single(p=>p.Id=="4/16").UntaggedVlan==1&&stacked1960.Ports.Single(p=>p.Id=="1/8").UntaggedVlan==9,"TRK VLAN settings reach members across units");
        Assert(stacked1960.Ports.Count(p=>p.VoiceVlan==232)==8,"TRK voice VLAN reaches all aggregate members");
        var stackPlaces=Charts.Layout(stacked1960);var stackChart=Charts.GeometryReferenceScene(stacked1960,stackPlaces,"TEST");
        foreach(var group in stacked1960.Groups)
        {
            var pp=stackPlaces.Where(p=>p.Group==group.Id).ToList();
            Assert(pp.Select(p=>p.Tile.Y).Distinct().Count()==1,"JL805A copper and optical ports share one physical row");
            Assert(pp.Single(p=>p.Port==group.Prefix+"13").Tile.X>pp.Single(p=>p.Port==group.Prefix+"12").Tile.X,"JL805A fiber bank is to the right of copper");
        }
        Assert(Charts.Validate(stacked1960,stackPlaces).Count==0&&Charts.ValidateScene(stackChart).Count==0,"1960 stack/key layout is collision free");
        Assert(Charts.AggregateColors(stacked1960).Count==2&&stackChart.Shapes.Count(s=>s.StrokeWidth==3.2)==10,"Eight trunk port outlines plus two matching key samples");
        Assert(!stacked1960.Ports.Any(p=>p.StackLink),"Omitted physical stacking links must not be inferred");
        var shadow1960=Parser.Parse(instant1960.Replace("interface 1/8\n channel-group", "interface 1/8\n switchport general allowed vlan add 20 untagged\n switchport general pvid 20\n switchport general allowed vlan add 30 tagged\n channel-group"));
        Assert(shadow1960.Blockers.Count==0&&shadow1960.Ports.Single(p=>p.Id=="1/8").UntaggedVlan==9&&shadow1960.Ports.Single(p=>p.Id=="1/8").TaggedVlans.SequenceEqual(new[]{232}),"Active TRK settings replace per-port VLAN shadow configuration");
        foreach(var model in new[]{("JL680A",8),("JL681A",8),("JL682A",24),("JL683A",24),("JL683B",24),("JL684A",24),("JL684B",24),("JL685A",48),("JL686A",48),("JL686B",48)})
        {
            var instant=Parser.Parse($"config-file-header\nTEST\nvInstantOn_1930_3.3.2.0 (5)\nSKU Description \"Instant On 1930 {model.Item1}\"\n!\nvlan database\nvlan 10,40\nexit\nvoice vlan id 40\nvoice vlan state oui-enabled\ninterface vlan 40\n name Voice\n!\ninterface 1\n switchport general allowed vlan add 10 untagged\n switchport general pvid 10\n switchport general allowed vlan add 40 tagged\n!\n");
            Assert(instant.Blockers.Count==0,"Instant On supported model: "+model.Item1);
            Assert(instant.Ports.Count==model.Item2+(model.Item2==8?2:4),"Instant On hardware port count");
            var ip=Charts.Layout(instant);Assert(Charts.Validate(instant,ip).Count==0,"Instant On geometry: "+model.Item1);
            Assert(Charts.ValidateScene(Charts.GeometryReferenceScene(instant,ip,"TEST")).Count==0,"Instant On scene text and markers: "+model.Item1);
            Assert(instant.Ports[0].UntaggedVlan==10&&instant.Ports[0].VoiceVlan==40,"Instant On VLAN/voice normalization");
            Assert(instant.Ports[1].UntaggedVlan==null,"Omitted Instant On port settings remain unknown");
            var copperPort=ip.Single(p=>p.Port==model.Item2.ToString());var fiberPort=ip.Single(p=>p.Port==(model.Item2+1).ToString());
            Assert(fiberPort.Tile.X>copperPort.Tile.X,"Instant On optics remain to right of copper");
        }
        foreach(var chassis in new[]{("J9821A",6),("J9822A",12),("J8697A",6),("J8698A",12),("J8715B",12),("J9475A",6)})
        {
            char last=(char)('A'+chassis.Item2-1);
            var modular=Parser.Parse($"; {chassis.Item1} Configuration Editor\nmodule A type J9986A\nmodule B type J9538A\nmodule {last} type J9990A\nvlan 10\n name Data\n untagged A1-A24\n exit\n");
            Assert(modular.Blockers.Count==0,"Supported chassis identity: "+chassis.Item1);
            var view=Charts.PhysicalView(modular)!;var frames=view.Frames;
            Assert(frames.Count==chassis.Item2,"All physical slots, including undeclared slots, are retained");
            Assert(frames[0].Slot=="Slot A"&&frames[0].X<frames[1].X&&frames[0].Y==frames[1].Y,"A/B are left/right on the top row");
            Assert(frames[2].X==frames[0].X&&frames[2].Y>frames[0].Y,"C is directly below A, even when not declared");
            Assert(frames.Last().Slot=="Slot "+last&&frames.Last().X==frames[1].X,"Last slot remains at its real right-hand position");
            var mp=Charts.Layout(modular);Assert(Charts.Validate(modular,mp).Count==0,"Modular port/frame geometry: "+chassis.Item1);
            Assert(Charts.ValidateScene(Charts.GeometryReferenceScene(modular,mp,"TEST")).Count==0,"Modular key/labels clear: "+chassis.Item1);
            if(chassis.Item1=="J8715B")Assert(view.Bands.Any(b=>b.Text.StartsWith("Fabric")&&b.Y>frames[4].Y&&b.Y<frames[6].Y),"8212 fabric region separates E/F from G/H");
        }
        foreach(string model in new[]{"JL071A","JL072A","JL073A","JL074A","JL075A","JL076A"})
        {
            var flex=Parser.Parse($"; {model} Configuration Editor\nmodule 1 type {model}\nmodule A type JL083A\n"+(model=="JL075A"?"module B type JL079A\n":""));
            Assert(flex.Blockers.Count==0,"3810M supported base/flex combination");
            var frames=Charts.PhysicalView(flex)!.Frames;
            Assert(frames[0].Slot=="Fixed ports"&&frames[1].Slot=="Uplink A"&&frames[1].X>frames[0].X&&frames[1].Y==frames[0].Y,"3810M flex bays sit to right of fixed ports");
            if(model=="JL075A")Assert(frames[2].Slot=="Uplink B"&&frames[2].X>frames[1].X,"3810M second bay is rightmost");
            Assert(flex.Ports.Any(p=>p.Id=="A1")&&flex.Ports.Any(p=>p.Id=="1"),"Fixed and flex port identities remain distinct");
            var fp=Charts.Layout(flex);Assert(Charts.Validate(flex,fp).Count==0,"3810M port bounds and physical bank centering: "+model);
            Assert(Charts.ValidateScene(Charts.GeometryReferenceScene(flex,fp,"TEST")).Count==0,"3810M scene has no collisions: "+model);
        }
        var badSlot=Parser.Parse("; J9821A Configuration Editor\nmodule G type J9986A\n");
        Assert(badSlot.Blockers.Any(b=>b.Contains("capacity")),"G is invalid on a six-slot chassis");Charts.Layout(badSlot);
        var modularStack=Parser.Parse("; Aruba Configuration Editor\nvsf member 1 type J9821A\nvsf member 2 type J9821A\nmodule 1/A type J9986A\nmodule 2/B type J9986A\n");
        Assert(modularStack.Blockers.Count==0&&modularStack.Ports.Any(p=>p.Id=="2/B24"),"Modular member/slot prefixes are retained");
        Assert(Charts.PhysicalView(modularStack)!.Frames.Count==12,"Each modular member receives its own six-slot chassis view");
        Assert(Charts.ValidateScene(Charts.GeometryReferenceScene(modularStack,Charts.Layout(modularStack),"TEST")).Count==0,"Multiple chassis remain separate and clear");
        naming.Hostname="Unknown";naming.Groups.Add(new Group{Product="JL256A"});Assert(ReportNaming.FolderName(naming)=="JL256A","Model folder fallback");
        naming.Hostname="CORE/ONE:WEST";Assert(ReportNaming.FolderName(naming)=="CORE_ONE_WEST","Windows filename sanitization");
        naming.Hostname="CON";Assert(ReportNaming.FolderName(naming)=="Switch_CON","Reserved Windows filename");
        var numeric=Parser.Parse("; J4850A Configuration Editor\nmodule 4 type J4820A\nmodule 1 type J4878A\nvlan 1\n untagged\n A1-A4,D1-D24\n exit\n");
        Assert(numeric.Blockers.Count==0&&numeric.Topology=="Modular"&&numeric.ChassisSlots==4,"Numeric XL slots normalize only with identified chassis");
        Assert(numeric.Ports.Count==28&&numeric.Ports.All(p=>p.UntaggedVlan==1),"Command-only wrapped native list retains every port");
        var xl=Charts.Layout(numeric);Placement XP(string id)=>xl.Single(p=>p.Port==id);
        Assert(XP("D1").Tile.Y==XP("D6").Tile.Y&&XP("D7").Tile.Y>XP("D1").Tile.Y&&XP("D7").Tile.X==XP("D1").Tile.X,"J4820 first six top / next six bottom");
        Assert(XP("D13").Tile.Y==XP("D1").Tile.Y&&XP("D13").Tile.X>XP("D6").Tile.X&&XP("D24").Tile.X==XP("D18").Tile.X,"J4820 second twelve-port bank");
        Assert(xl.Where(p=>p.Group=="Module A").Select(p=>p.Tile.Y).Distinct().Count()==1,"Four mini-GBIC sockets in a horizontal row");
        Assert(Charts.PhysicalView(numeric)!.Frames.Single(f=>f.Slot=="Slot D").X>Charts.PhysicalView(numeric)!.Frames.Single(f=>f.Slot=="Slot A").X,"XL slot D lower right");
        var combo=Parser.Parse("; J4819A Configuration Editor\nmodule 7 type J4907A\nvlan 10\n untagged G1-G6,G\n8-G16\n exit\n");
        Assert(combo.Blockers.Count==0&&combo.Ports.Count==16&&combo.Ports.Single(p=>p.Id=="G8").UntaggedVlan==10&&combo.Ports.Single(p=>p.Id=="G7").UntaggedVlan==null,"Split-token list is rejoined without inventing omitted membership");
        var cp=Charts.Layout(combo);Assert(cp.Count==18&&cp.Count(p=>p.Socket=="M")==2,"Sixteen combo interfaces have eighteen physical sockets");
        Assert(cp.Single(p=>p.Port=="G15"&&p.Socket=="M").Tile.X>cp.Single(p=>p.Port=="G15"&&p.Socket=="T").Tile.X,"Combo SFP follows its corresponding copper socket");
        Assert(Charts.Validate(combo,cp).Count==0&&Charts.ValidateScene(Charts.GeometryReferenceScene(combo,cp,"TEST")).Count==0,"Combo sockets and shared-interface annotation clear all collision checks");
        var comboChassis=Parser.Parse("; J4819A Configuration Editor\nmodule 1 type J4878A\nmodule 2 type J4820A\nmodule 7 type J4907A\n");
        Assert(Charts.ModuleDetails(comboChassis,Charts.GeometryReferenceScene(comboChassis,Charts.Layout(comboChassis),"TEST"),"TEST").All(d=>Charts.ValidateScene(d).Count==0),"Enlarged combo title clears its shared-interface annotation");
        var flexBackup=Parser.Parse("; hpStack_KB Configuration Editor\nstacking\n member 1 type \"JL075A\"\n member 1 flexible-module B type JL081A\n exit\nvlan 10\n untagged 1/1-1/16,1/B1-1/B4\n exit\n");
        Assert(flexBackup.Blockers.Count==0&&flexBackup.Ports.Count==20&&flexBackup.Groups.Single(g=>g.Slot=="").Product=="JL075A","Flexible-module declaration must not replace the base SKU");
        Assert(Charts.PhysicalView(flexBackup)!.Frames.Any(f=>f.Slot=="Uplink A"&&f.Group==""),"3810 missing bay A remains in place before populated B");
        var legacy=Parser.Parse("; J9573A Configuration Editor\nmodule 1 type J9573x\n");
        Assert(legacy.Blockers.Count==0&&legacy.Ports.Count==26&&legacy.Groups.Single().BlankLeft,"Corroborated legacy 3800 identity and blank fascia");
        Assert(Parser.Parse("; Configuration Editor\nmodule 1 type J9573x\n").Blockers.Count>0,"Uncorroborated product alias fails closed");
        var cx6200=Parser.Parse("!Version ArubaOS-CX ML.10.13.1000\nvsf member 1\n type jl727b\n");
        var cx6200p=Charts.Layout(cx6200);Assert(cx6200.Blockers.Count==0&&cx6200.Firmware=="ML.10.13.1000"&&cx6200p.Single(p=>p.Port=="1/1/49").Tile.X>cx6200p.Single(p=>p.Port=="1/1/48").Tile.X,"6200F optical bank right and complete firmware");
        var namedCommunity=Parser.Parse("! AOS-CX JL661A\nvlan 50\n name Community Center\n");
        Assert(namedCommunity.Vlans.Single(v=>v.Id==50).Name=="Community Center","A VLAN name containing Community is not an SNMP credential directive");
        string plPorts=string.Join("\n",Enumerable.Range(1,52).Select(n=>"interface 1/1/"+n));
        var profilePl=Parser.Parse("!Version ArubaOS-CX PL.10.10.1070\nhostname FireStation-LAB\nvlan 1,23,30,40\n"+plPorts);
        Assert(profilePl.Blockers.Count==0&&profilePl.Groups.Single().Product==Parser.SharedPl48Profile&&profilePl.Vlans.Select(v=>v.Id).SequenceEqual(new[]{1,23,30,40})&&profilePl.Assumptions.Any(a=>a.Contains("Exact SKU")),"PL shared profile is independent of hostname and records model uncertainty");
        Assert(profilePl.Ports.Count(p=>p.Media=="RJ-45")==48&&profilePl.Ports.Count(p=>p.Media=="SFP/SFP+")==4,"PL shared profile does not invent SFP+ capability");
        var plSheet=QuickSheet.Build(profilePl,Charts.Layout(profilePl),"TEST");
        Assert(Charts.ValidateScene(plSheet.Scene).Count==0&&!plSheet.Scene.Labels.Any(l=>l.Text.Contains("6000")||l.Text.Contains("6100")),"PL shared geometry retains ports without displaying an unconfirmed model");
        Assert(ReportNaming.FolderName(profilePl)=="FireStation-LAB"&&ReportNaming.FolderName(Parser.Parse("!Version ArubaOS-CX PL.10.10.1070\n"+plPorts))=="Unknown-Switch","Hostname is preferred and an unknown model is omitted from fallback folders");
        Assert(ReportText.ReadHostname("!Version ArubaOS-CX\nhostname \"Client Switch\"\n")=="Client Switch"&&ReportText.ReadHostname("sysname COMWARE-CORE\n")=="COMWARE-CORE"&&ReportText.ReadHostname("interface 1\n description hostname example\n")=="","Hostname preview reads only hostname/sysname commands");
        Assert(HardwareCatalog.IsSelection("JL675A")&&HardwareCatalog.IsSelection("1=JG932A,2=JG934A")&&!HardwareCatalog.IsSelection("Aruba CX 6100")&&!HardwareCatalog.IsSelection("CX6100"),"Manual SKU selection and printed model names use their correct argument types");
        var moduleOnly=Parser.Parse("; Configuration Editor\nmodule A type J9986A\n");
        Assert(ReportText.SwitchModel(moduleOnly)==""&&ReportNaming.FolderName(moduleOnly)=="Unknown-Switch","Known line-card models do not become an unidentified chassis model");
        Assert(plSheet.Sockets.Single(s=>s.Port=="1/1/49").Tile.X<plSheet.Sockets.Single(s=>s.Port=="1/1/1").Tile.X&&plSheet.Sockets.Single(s=>s.Port=="1/1/49").Tile.Y<plSheet.Sockets.Single(s=>s.Port=="1/1/50").Tile.Y&&plSheet.Sockets.Single(s=>s.Port=="1/1/49").Tile.X<plSheet.Sockets.Single(s=>s.Port=="1/1/51").Tile.X,"PL shared faceplate places optical 49/51 above 50/52 to the left of copper");
        Assert(Parser.Parse("!Version ArubaOS-CX PL.10.13.0005\n"+plPorts.ToUpperInvariant()).Groups.Single().Product==Parser.SharedPl48Profile,"PL full inventory tolerates command case and omitted hostname");
        var explicitPl=Parser.Parse("!Version ArubaOS-CX PL.10.10.1070\n! JL675A\n"+plPorts);
        Assert(explicitPl.Groups.Single().Product=="JL675A"&&explicitPl.Ports.Last().Media=="SFP+"&&!explicitPl.Assumptions.Any(a=>a.Contains("shared 48G")),"Explicit SKU takes precedence over the shared profile");
        foreach(string invalid in new[]{
            "!Version ArubaOS-CX PL.10.10.1070\nhostname TEST-6100\ninterface 1/1/1",
            "!Version ArubaOS-CX ML.10.10.1070\n"+plPorts,
            "!Version ArubaOS-CX PL.10.10.1070\n"+plPorts+"\ninterface 1/1/53",
            "!Version ArubaOS-CX PL.10.10.1070\n"+plPorts+"\ninterface 2/1/1",
            "!Version ArubaOS-CX PL.10.10.1070\nvsf member 1\n"+plPorts,
            "!Version ArubaOS-CX PL.10.10.1070\n! R8N99A\n"+plPorts,
            "!Version ArubaOS-CX PL.10.10.1070\nmodel UNKNOWN\n"+plPorts,
            "!Version ArubaOS-CX PL.10.10.1070\n"+plPorts+"\ninterface 1/1/1"})
        {
            var plRejected=Parser.Parse(invalid);
            Assert(plRejected.Ports.Count==0&&plRejected.Blockers.Count>0&&!plRejected.Blockers.Any(b=>b.Contains("outside the validated model inventory")),"Incomplete, contradictory or unsupported identity is blocked without cascading port errors");
        }
        var readable=Parser.Parse("; J9851A Configuration Editor\nhostname READABILITY\nmodule A type J9993A\nmodule B type J9993A\n"+string.Join("\n",Enumerable.Range(2,7).Select(n=>"module "+(char)('A'+n)+" type J9986A")));
        var readableLayout=Charts.Layout(readable);var readableChart=Charts.GeometryReferenceScene(readable,readableLayout,"TEST");
        var details=Charts.ModuleDetails(readable,readableChart,"TEST");
        Assert(details.Count==3,"Nine populated chassis modules get three enlarged sections");
        Assert(details.All(d=>Charts.ValidateScene(d).Count==0),"Enlarged module diagrams and legends are collision free");
        Assert(details.SelectMany(d=>d.Labels).Where(l=>l.Owner.Length>0&&l.Size>8).All(l=>l.Size*Math.Min(805.89/1720,559.28/1190)>=8),"Enlarged chassis interface labels print at least eight points");
        Assert(readableLayout.All(p=>p.Marker.Y>p.Tile.Y&&p.Marker.Y+p.Marker.H<=p.Tile.Y+p.Tile.H-4),"All VLAN marker rows stay within port outlines");
        Assert(readableChart.Shapes.Count(s=>s.Owner.Length>0)==readableLayout.Count,"One silhouette per socket; no internal contact or cage geometry");
        Assert(Charts.PhysicalView(readable)!.Frames.Single(f=>f.Slot=="Slot K").H<Charts.PhysicalView(readable)!.Frames.Single(f=>f.Slot=="Slot C").H,"Entirely undeclared rows use compact placeholders");
        for(int n=0;n<readable.Ports.Count;n++){readable.Ports[n].UntaggedVlan=n+1;readable.Ports[n].UntaggedState="Known";}
        var manyKeys=Charts.ModuleDetails(readable,Charts.GeometryReferenceScene(readable,Charts.Layout(readable),"TEST"),"TEST");
        Assert(manyKeys.Count>details.Count&&manyKeys.All(d=>Charts.ValidateScene(d).Count==0),"Large per-module keys paginate before reaching the footer");
        var nonconsecutive=Parser.Parse("; Aruba Configuration Editor\nvsf member 1 type J9821A\nvsf member 5 type J9821A\nmodule 1/A type J9986A\nmodule 1/B type J9986A\nmodule 5/A type J9986A\nmodule 5/B type J9986A\n");
        var memberDetails=Charts.ModuleDetails(nonconsecutive,Charts.GeometryReferenceScene(nonconsecutive,Charts.Layout(nonconsecutive),"TEST"),"TEST");
        Assert(memberDetails.Any(d=>d.Labels.Any(l=>l.Text.Contains("Member 5"))),"Enlarged pages retain actual nonconsecutive member IDs");
        foreach(var p in readable.Ports){p.UntaggedVlan=null;p.UntaggedState="Unknown";}
        var sheet=QuickSheet.Build(readable,Charts.Layout(readable),"TEST");
        Assert(sheet.Scene.Labels.Any(l=>l.Text=="Native VLAN")&&sheet.Scene.Labels.Any(l=>l.Text=="Tagged VLAN")&&sheet.Scene.Labels.Any(l=>l.Text=="Voice VLAN")&&!sheet.Scene.Labels.Any(l=>l.Text.StartsWith("Fill:")),"Reading guide uses actual symbols with short meaning labels");
        Assert(sheet.Scene.Width==805.89&&sheet.Scene.Height==559.28,"Single-page sheet uses actual A4 content points without scale-to-fit shrinking");
        Assert(Charts.ValidateScene(sheet.Scene).Count==0,"Single-page keys, labels and fixed-scale ports are collision free");
        Assert(sheet.Scene.Boxes.Count(b=>b.Role=="ActivePanel")==readable.Groups.Count&&sheet.Scene.Boxes.Where(b=>b.Role=="ActivePanel").All(b=>Math.Abs(b.H-sheet.Scene.Boxes.First(b=>b.Role=="ActivePanel").H)<.001),"Optical-only and copper module panels share a consistent vertical size within the diagram");
        Assert(sheet.Scene.Boxes.Any(b=>b.Role=="InactivePanel"&&b.H<sheet.Scene.Boxes.First(b=>b.Role=="ActivePanel").H),"Wholly undeclared rows retain compact physical placeholders");
        var compactChassis=Parser.Parse("; J9850A Configuration Editor\nmodule A type J9993A\nmodule C type J9986A\ntrunk A1,C1 trk1 lacp\n");
        var compactChassisSheet=QuickSheet.Build(compactChassis,Charts.Layout(compactChassis),"TEST");
        Assert(compactChassisSheet.Scene.Labels.Single(l=>l.Text=="Trunks / LAGs").X>compactChassisSheet.Sockets.Max(p=>p.Tile.X+p.Tile.W)&&Charts.ValidateScene(compactChassisSheet.Scene).Count==0,"Modular trunk keys use available sidebar space to preserve room below the chassis");
        Assert(Proportional(sheet),"Every interface/media retains the reference proportions under one uniform diagram scale");
        Assert(QuickSheet.Outline(0,0,false).Length==8&&QuickSheet.Outline(0,0,false).Select(p=>p.Y).Distinct().Count()==3,"RJ45 outline has one simple rectangular tab without extra shoulder steps");
        var rj=QuickSheet.Outline(0,0,false);
        Assert(Math.Abs((rj[4].X-rj[5].X)/QuickSheet.PortWidth-.42)<.001&&QuickSheet.PortHeight-QuickSheet.TabTop<=5.4+.001,"Reference RJ45 proportions use a narrow tab and reserved number area");
        Assert(QuickSheet.PortWidth/QuickSheet.OpticalHeight>=1.5&&QuickSheet.PortWidth/QuickSheet.OpticalHeight<=1.6&&QuickSheet.Outline(0,0,true)[0].X<1,"SFP cages use a wide shallow aspect ratio and small corner bevels");
        visual.Ports[0].TaggedVlans=new List<int>{110};
        var tagSheet=QuickSheet.Build(visual,Charts.Layout(visual),"TEST");
        Assert(Proportional(tagSheet),"Fixed and modular switches preserve the same interface proportions with uniform scaling");
        foreach(var socket in tagSheet.Sockets)
        {
            var p=visual.Ports.Single(p=>p.Id==socket.Port);var shapes=tagSheet.Scene.Shapes.Where(s=>s.Owner==p.Id).ToList();
            Assert(shapes.Count==(p.Aggregate.Length>0?3:2),"Aggregates use a native face, continuous colored outline and dark edge without a white separator");
            Assert(shapes[^1].Stroke=="#182536"&&Math.Abs(shapes[^1].StrokeWidth-QuickSheet.FaceStroke*tagSheet.GeometryScale)<.001,"Every native-filled port has its own visible thin dark boundary");
            if(p.Aggregate.Length>0)
            {
                Assert(shapes[0].Fill==VlanPalette.NativeFill(p,tagSheet.VlanColors)&&shapes[1].Stroke==tagSheet.AggregateColors[p.Aggregate]&&Math.Abs(shapes[1].StrokeWidth-QuickSheet.AggregateStroke*tagSheet.GeometryScale)<.001,"Native fill touches the distinct LAG color directly");
                Assert(SamePoints(shapes[1].Points,QuickSheet.FrameOutline(shapes[0].Points,QuickSheet.AggregateOutset*tagSheet.GeometryScale))&&shapes[^1].Points.SequenceEqual(shapes[1].Points),"The complete LAG frame follows the same port silhouette, including the RJ45 tab or SFP bottom edge");
                Assert(shapes.All(s=>s.ClipBounds==null)&&shapes[1].ClipInside,"No rectangular clip interrupts the continuous LAG outline");
            }
            var dots=tagSheet.Scene.Dots.Where(d=>d.X>socket.Tile.X&&d.X<socket.Tile.X+socket.Tile.W&&d.Y>socket.Tile.Y&&d.Y<socket.Tile.Y+socket.Tile.H).ToList();
            Assert(dots.Count==p.TaggedVlans.Count,"Every explicit tagged membership stays inside its fixed port silhouette");
        }
        Assert(!tagSheet.Scene.Labels.Any(l=>l.Text.Contains("Tagged VLAN sets")||System.Text.RegularExpressions.Regex.IsMatch(l.Text,@"^[a-z]+: ")),"Visual diagram contains no tagged-set table or letter references");
        Assert(tagSheet.Scene.Labels.Where(l=>l.Owner.Length>0).Count()==tagSheet.Sockets.Count,"Only the compact port identifier is printed on each socket; no native-number or letter footer");
        foreach(var color in tagSheet.AggregateColors.Values)
            Assert(tagSheet.Scene.Shapes.Any(s=>s.Owner.Length==0&&s.Stroke==color&&s.Points.Length==4),"Every aggregate has a matching rectangular outline in the key");
        var dense=Parser.Parse("; JL256A Configuration Editor\ntrunk 1,49 trk1 lacp\n");
        dense.Vlans=Enumerable.Range(1,28).Select(id=>new Vlan{Id=id,Voice=id%7==0}).ToList();
        dense.Ports.First(p=>p.Media=="RJ-45").TaggedVlans=Enumerable.Range(1,28).ToList();
        dense.Ports.First(p=>p.Media!="RJ-45").TaggedVlans=Enumerable.Range(1,28).ToList();
        dense.Ports.First(p=>p.Media!="RJ-45").Disabled=true;
        var denseSheet=QuickSheet.Build(dense,Charts.Layout(dense),"TEST");
        bool Within((double X,double Y)[] outline,double x,double y)
        {
            bool inside=false;
            for(int i=0,j=outline.Length-1;i<outline.Length;j=i++)
                if((outline[i].Y>y)!=(outline[j].Y>y)&&x<(outline[j].X-outline[i].X)*(y-outline[i].Y)/(outline[j].Y-outline[i].Y)+outline[i].X)inside=!inside;
            return inside;
        }
        foreach(var item in new[]{sheet,tagSheet,denseSheet})foreach(var socket in item.Sockets)
        {
            var number=item.Scene.Labels.Single(l=>l.Owner==socket.Port);
            Assert(number.Bold&&number.Size>=QuickSheet.MinimumPortFont-.001,"Port identifiers retain bold type at or above the readable printed size floor");
            var measured=ReportRenderer.Measure(number);
            Assert(Math.Abs(number.X+measured.Width/2-(socket.Tile.X+socket.Tile.W/2))<.001,"Port number is horizontally centered");
            Assert(number.Y>socket.Tile.Y+socket.Tile.H*.4,"Port number is reserved at the bottom of its socket");
            if(!socket.Optical)Assert(number.InkBox&&number.Y>=socket.Tile.Y+QuickSheet.TabTop*item.GeometryScale&&number.Y+measured.Height<socket.Tile.Y+socket.Tile.H,"RJ45 number stays within the narrow tab and clear of the thin dark border");
            var outline=item.Scene.Shapes.Last(shape=>shape.Owner==socket.Port).Points;
            if(socket.Optical)
                Assert(number.Color=="#101820"&&number.Y>socket.Tile.Y+socket.Tile.H+.5&&number.Y+measured.Height<socket.Tile.Y+QuickSheet.PortHeight*item.GeometryScale,"Optical numbers sit below the cage on the RJ45 row's number line, including black stack cages");
            else foreach(var corner in new[]{(number.X,number.Y),(number.X+measured.Width,number.Y),(number.X,number.Y+measured.Height),(number.X+measured.Width,number.Y+measured.Height)})
                Assert(Within(outline,corner.Item1,corner.Item2),"RJ45 number stays within its tab");
            var marks=item.Scene.Dots.Where(d=>d.X>socket.Tile.X&&d.X<socket.Tile.X+socket.Tile.W&&d.Y>socket.Tile.Y&&d.Y<socket.Tile.Y+socket.Tile.H).ToList();
            double Extent(Dot d)=>d.Voice?d.Radius+d.VoiceExtra+d.Outline/2:Math.Max(d.Radius+d.Halo,d.Radius+d.Outline/2);
            foreach(var mark in marks)
            {
                double extent=Extent(mark);
                foreach(var corner in new[]{(mark.X-extent,mark.Y-extent),(mark.X+extent,mark.Y-extent),(mark.X-extent,mark.Y+extent),(mark.X+extent,mark.Y+extent)})
                    Assert(Within(outline,corner.Item1,corner.Item2),"Full marker stroke/halo stays inside port");
                Assert(mark.Y+extent<number.Y-.2,"Enlarged markers remain clear of the reserved port number area");
            }
            for(int i=0;i<marks.Count;i++)for(int j=i+1;j<marks.Count;j++)
                Assert(Math.Abs(marks[i].X-marks[j].X)>Extent(marks[i])+Extent(marks[j])||Math.Abs(marks[i].Y-marks[j].Y)>Extent(marks[i])+Extent(marks[j]),"Marker extents never overlap each other");
            foreach(var row in marks.GroupBy(d=>d.Y))
                Assert(Math.Abs((row.Min(d=>d.X)+row.Max(d=>d.X))/2-(socket.Tile.X+socket.Tile.W/2))<.001,"Every marker row is centered, including partial rows");
            if(marks.Count>0)Assert(marks.Min(d=>d.Y-Extent(d))>=socket.MarkerTop-.001&&marks.Max(d=>d.Y+Extent(d))<=socket.MarkerBottom+.001,"Markers remain within the recorded top and bottom packing region");
        }
        Assert(denseSheet.Scene.Dots.All(d=>Math.Abs(d.Radius-denseSheet.MarkerRadius)<.0001&&d.VoiceExtra==0),"All port and key markers have one consistent adaptive size, including voice diamonds");
        Assert(Charts.ValidateScene(denseSheet.Scene).Count==0,"Twenty-eight enlarged markers, including voice diamonds, fit RJ45 and SFP aggregate outlines without text collisions");
        Assert(denseSheet.Scene.Dots.Count(d=>denseSheet.Sockets.Any(p=>d.X>p.Tile.X&&d.X<p.Tile.X+p.Tile.W&&d.Y>p.Tile.Y&&d.Y<p.Tile.Y+p.Tile.H))==56,"Dense enlarged markers are all preserved in both media types");
        var tabCases=Parser.Parse("; JL256A Configuration Editor\ntrunk 1-52 trk1 lacp\n");foreach(var p in tabCases.Ports)p.Disabled=true;
        var tabSheet=QuickSheet.Build(tabCases,Charts.Layout(tabCases),"TEST");
        Assert(Charts.ValidateScene(tabSheet.Scene).Count==0,"All disabled 1-52 labels fit at the same bold size inside full RJ45 and SFP aggregate frames");
        var copperDense=Parser.Parse("; JL256A Configuration Editor\n");copperDense.Vlans=Enumerable.Range(1,28).Select(id=>new Vlan{Id=id}).ToList();copperDense.Ports[0].TaggedVlans=Enumerable.Range(1,28).ToList();
        var copperSheet=QuickSheet.Build(copperDense,Charts.Layout(copperDense),"TEST");var copperSocket=copperSheet.Sockets.Single(p=>p.Port==copperDense.Ports[0].Id);
        Assert(Charts.ValidateScene(copperSheet.Scene).Count==0&&copperSheet.Scene.Dots.Count(d=>d.X>copperSocket.Tile.X&&d.X<copperSocket.Tile.X+copperSocket.Tile.W&&d.Y>copperSocket.Tile.Y&&d.Y<copperSocket.Tile.Y+copperSocket.Tile.H)==28,"Twenty-eight RJ45 markers remain complete without stretching the interface face");
        dense.Ports.First(p=>p.Media!="RJ-45").TaggedVlans=Enumerable.Range(1,90).ToList();
        bool tooDense=false;try{QuickSheet.Build(dense,Charts.Layout(dense),"TEST");}catch(OnePageCapacityException){tooDense=true;}
        Assert(tooDense,"Over-capacity markers are reported explicitly rather than resized, overlapped or omitted");
        var fiveConfig="; Aruba Configuration Editor\nhostname FIVE-MEMBER-REFERENCE\n"+string.Join("\n",Enumerable.Range(1,5).Select(i=>$"vsf member {i} type JL558A"))+"\n"+
            string.Join("\n",Enumerable.Range(1,14).Select(i=>$"trunk 3/{i*2-1},3/{i*2} trk{i} lacp"))+"\nvlan 120\n untagged "+string.Join(",",Enumerable.Range(1,5).Select(i=>$"{i}/1-{i}/52"))+"\n exit\n";
        var five=Parser.Parse(fiveConfig);five.Vlans=Enumerable.Range(1,11).Select(id=>new Vlan{Id=id,Name="VLAN "+id}).Append(new Vlan{Id=120,Name="Staff"}).ToList();
        var fiveSheet=QuickSheet.Build(five,Charts.Layout(five),"TEST");
        var fivePanels=fiveSheet.Scene.Boxes.Where(b=>b.Role=="ActivePanel").OrderBy(b=>b.Y).ToList();
        Assert(fivePanels.Count==5&&fivePanels.All(b=>Math.Abs(b.H-fivePanels[0].H)<.001)&&fivePanels.Zip(fivePanels.Skip(1)).All(pair=>pair.Second.Y>=pair.First.Y+pair.First.H&&Math.Abs(pair.Second.Y-pair.First.Y-pair.First.H-(fivePanels[1].Y-fivePanels[0].Y-fivePanels[0].H))<.001),"Stack members use consistent panel heights and compact nonoverlapping vertical gaps");
        foreach(var item in new[]{tagSheet,denseSheet,copperSheet,fiveSheet,tabSheet})
        {
            Assert(item.Scene.Boxes.Where(b=>b.Role=="ActivePanel").All(b=>Math.Abs(b.H-item.Scene.Boxes.First(b=>b.Role=="ActivePanel").H)<.001),"Active panel heights remain consistent within every tested switch and module");
            foreach(var row in item.Sockets.GroupBy(s=>s.Tile.Y))
                Assert(row.Max(s=>s.NumberBounds.Y+s.NumberBounds.H)-row.Min(s=>s.NumberBounds.Y+s.NumberBounds.H)<.001,"RJ45 tab numbers and external SFP numbers are horizontally aligned");
        }
        foreach(var item in new[]{tagSheet,denseSheet,copperSheet,fiveSheet})
        {
            var palette=item.VlanColors.Values.ToList();
            for(int i=0;i<palette.Count;i++)for(int j=i+1;j<palette.Count;j++)
                Assert(AggregatePalette.Difference(palette[i],palette[j])>=VlanPalette.MinimumDistance,"Pastel VLAN colors do not repeat or become near duplicates");
            var lag=item.AggregateColors.Values.ToList();
            for(int i=0;i<lag.Count;i++)for(int j=i+1;j<lag.Count;j++)
                Assert(AggregatePalette.Difference(lag[i],lag[j])>=22,"Aggregate outlines remain mutually separated in the pastel working diagram");
        }
        Assert(five.Ports.Count==260&&fiveSheet.Sockets.Count==260&&Charts.ValidateScene(fiveSheet.Scene).Count==0,"Five members, 260 ports and fourteen aggregates fit one A4 visual sheet");
        foreach(var item in new[]{sheet,tagSheet,denseSheet,fiveSheet})
        {
            Assert(item.Scene.Shapes.All(s=>s.ClipInside),"Port and key outline strokes are contained, with no bleed into adjacent cells");
            var key=item.Scene.Labels.Single(l=>l.Text=="VLAN key"||l.Text=="VLANs");
            double bankRight=item.Sockets.Max(p=>p.Tile.X+p.Tile.W),bankBottom=item.Sockets.Max(p=>Math.Max(p.Tile.Y+p.Tile.H,p.NumberBounds.Y+p.NumberBounds.H));
            Assert((key.Y<75&&key.X>bankRight)||(key.Y>bankBottom),"Responsive VLAN key stays beside or below all interface faces and numbers");
            foreach(var group in item.Sockets.GroupBy(p=>p.Tile.Y))
            {
                var cols=group.Select(p=>p.Tile.X).Distinct().Order().ToList();
                for(int i=1;i<cols.Count;i++)Assert(cols[i]-cols[i-1]>=item.Sockets[0].Tile.W-.001,"Touching columns never overlap");
            }
        }
        var topRow=tagSheet.Sockets.Where(p=>p.Tile.Y==tagSheet.Sockets.Min(p=>p.Tile.Y)).OrderBy(p=>p.Tile.X).ToList();
        var steps=topRow.Zip(topRow.Skip(1)).Select(pair=>pair.Second.Tile.X-pair.First.Tile.X).ToList();
        Assert(steps.All(step=>step>=topRow[0].Tile.W),"Adjacent sockets never overlap");
        var boundaries=new HashSet<(string,string)>{("51","1"),("11","13"),("23","25"),("35","37")};
        double normal=steps.Min();
        foreach(var pair in topRow.Zip(topRow.Skip(1)))
            Assert(boundaries.Contains((pair.First.Port,pair.Second.Port))?pair.Second.Tile.X-pair.First.Tile.X>normal+.01:Math.Abs(pair.Second.Tile.X-pair.First.Tile.X-normal)<.001,"2930F bank boundaries remain visible while within-bank spacing stays uniform");
        var header=fiveSheet.Scene.Boxes.First(b=>b.Role=="Heading"&&b.Fill=="#17324d");
        fiveSheet.Scene.Shapes.Add(new(new[]{(header.X+1,header.Y+1),(header.X+5,header.Y+1),(header.X+5,header.Y+5),(header.X+1,header.Y+5)},"","#3267c8",3.2,"",true));
        Assert(Charts.ValidateScene(fiveSheet.Scene).Any(s=>s.Contains("Heading collides")),"Injected LAG outline/header collision is rejected even without a text collision");
        Assert(Charts.Booklet(readable,sheet.Scene,"TEST").Count==1+(readable.Ports.Count+23)/24,"Supplemental booklet has one working diagram, not enlarged replacement diagrams");
        var excessive=Parser.Parse("; JL256A Configuration Editor\n");foreach(var p in excessive.Ports)p.Description=p.Id+new string('X',500);
        bool rejected=false;try{QuickSheet.Build(excessive,Charts.Layout(excessive),"TEST");}catch(OnePageCapacityException){rejected=true;}
        Assert(rejected,"Information exceeding A4 capacity is reported instead of dropped, truncated, split or silently reduced");
        Console.WriteLine("PASS pastel palette distance guards, separated aggregate colors, symbolic reading guide, disabled tab numbers and twenty-eight adaptive RJ45 markers.");
        Console.WriteLine("PASS uniform active panel heights, compact vertical spacing and external optical numbers aligned with RJ45 number rows.");
        Console.WriteLine("PASS reference-based RJ45 body, narrow tab, readable in-tab labels, thin dark edges and wider shallow optical cages.");
        Console.WriteLine("PASS responsive VLAN key, spaced port columns, continuous LAG outlines without white separators, reserved header clearance and an injected outline/header collision.");
        Console.WriteLine("PASS working single A4 sheet, proportional RJ45/optical shapes under uniform scaling, readable bottom-centered numbers, complete adaptive markers, visual VLAN key and matching full aggregate key outlines.");
        Console.WriteLine("PASS in-port VLAN markers, silhouette-only ports, larger measured labels, compact empty rows, enlarged A4 sections, eight-point chassis port labels and paginated keys.");
        Console.WriteLine("PASS numeric chassis slots, XL row-block numbering, wrapped port lists, combo sockets, 3800 aliases, native 3810 flex declarations, hostname-independent PL shared geometry and 6200F bank placement.");
        Console.WriteLine("PASS measured text bounds, injected label collisions, voice VLAN diamonds, long titles and safe switch folder names.");
        Console.WriteLine("PASS distinct aggregate outlines, matching mode/member keys, fourteen groups, wrapped member lists and CX LACP mode.");
        Console.WriteLine("PASS chassis A/B row placement, missing slot preservation, 5406/5412/8206/8212 capacities, fabric region, all six 3810M bases, flex bays and modular member identities.");
        Console.WriteLine("PASS all ten Instant On 1930 SKU profiles, explicit native/tagged/PVID parsing, configured voice markers and unknown omitted settings.");
        Console.WriteLine("PASS 1960 JL805A four-member inventory, single-row geometry, TRK inheritance, shadow settings, cross-member voice and distinct aggregate outlines/key.");
        Console.WriteLine("PASS black VSF links, relative member port mapping and paired VLAN block/dot legend.");
        Console.WriteLine("PASS native fills, per-tag colors/counts, distinct socket/cage geometry, LAG borders and legacy reference-only 90-VLAN dot wrapping.");
        Console.WriteLine("PASS model geometry, bank sides, odd/even ordering, optical-only exception, injected bounds/overlap/marker/count failures, and native-only color.");
    }
    static void WrappedStackChecks()
    {
        foreach(string taggedCommand in new[]{"tagged","TAGGED","TaGgEd"})
        {
            string modular="; J9851A Configuration Editor\nmodule G type J9986A\nmodule H type J9986A\nmodule I type J9986A\nvlan 20\n untagged G24\n exit\nvlan 30\n untagged H1-H15,H17,I1-I10,I12,I19,I21,I23-I24\n exit\nvlan 32\n untagged\n G3,H16\n "+taggedCommand+"\n G24,H1-\nH15,H17,I1-I10,I12,I19,I21,I23-I24\n exit\n";
            var boundary=Parser.Parse(modular);
            Assert(boundary.Blockers.Count==0,"A command-only tagged line must not join the preceding native list");
            Assert(boundary.Ports.Single(p=>p.Id=="G24").UntaggedVlan==20&&boundary.Ports.Single(p=>p.Id=="H1").UntaggedVlan==30,"Adjacent wrapped tagged commands preserve earlier native VLANs");
            Assert(boundary.Ports.Where(p=>p.UntaggedVlan==32).Select(p=>p.Id).ToHashSet().SetEquals(new[]{"G3","H16"}),"Only the explicit native list receives VLAN 32");
            Assert(boundary.Ports.Count(p=>p.TaggedVlans.Contains(32))==32&&boundary.Ports.Single(p=>p.Id=="H15").TaggedVlans.Contains(32),"Wrapped H1-/H15 range and the complete tagged list are retained");
        }
        var contradiction=Parser.Parse("; JL256A Configuration Editor\nvlan 10\n untagged 1\n exit\nvlan 20\n untagged 1\n exit\n");
        Assert(contradiction.Blockers.Any(b=>b.Contains("Contradictory untagged/native VLANs")),"True native VLAN contradictions remain blockers");
        var crossGroup=Parser.Parse("; J9851A Configuration Editor\nmodule G type J9986A\nmodule H type J9986A\nvlan 10\n untagged G24-H1\n exit\n");
        Assert(crossGroup.Blockers.Contains("Interface range crosses inventory groups."),"Invalid cross-module ranges remain blockers");
        Console.WriteLine("PASS adjacent bare tagged/native command boundaries in three letter cases, wrapped module ranges, exact memberships and retained genuine conflict/range rejection.");
        const string aggregateRange="Trk1-Trk17";
        string trunks=string.Join('\n',Enumerable.Range(1,17).Select(n=>$"trunk {n} trk{n} lacp"));
        for(int split=1;split<aggregateRange.Length;split++)
        {
            var inv=Parser.Parse("; JL256A Configuration Editor\n"+trunks+"\nvlan 300\n untagged "+aggregateRange[..split]+"\n "+aggregateRange[split..]+"\n exit\nvlan 500\n tagged Trk1-Trk17\n exit\n");
            Assert(inv.Blockers.Count==0&&inv.Ports.Take(17).All(p=>p.UntaggedVlan==300&&p.TaggedVlans.SequenceEqual(new[]{500})),"Aggregate range wrapping preserves every split position and the next VLAN block");
        }
        const string physicalRange="1/49-1/50";
        for(int split=1;split<physicalRange.Length;split++)
        {
            var inv=Parser.Parse("; Aruba Configuration Editor\nvsf member 1 type JL558A\nvlan 120\n untagged\n "+physicalRange[..split]+"\n "+physicalRange[split..]+"\n exit\n");
            Assert(inv.Blockers.Count==0&&inv.Ports.Count(p=>p.UntaggedVlan==120)==2,"Physical interface ranges survive every split position");
        }
        string members=string.Join('\n',Enumerable.Range(1,8).Select(n=>$" member {n}\n  type {(n==8?"JL256A":"JL558A")}\n  link 1 {n}/51\n  link 2 {n}/52\n  exit"));
        string aggregates="trunk 1/50,2/50,5/50,6/50,7/50 trk1 lacp\n"+string.Join('\n',Enumerable.Range(2,16).Select(n=>$"trunk 7/{n*2-3}-7/{n*2-2} trk{n} lacp"));
        string access="1/1-1/48,2/1-2/49,3/1-3/50,4/1-4/50,5/1-5/49,6/1-6/49,7/33-7/49,8/1-8/20,8/49-8\n/50";
        string allMembers=string.Join(',',Enumerable.Range(1,8).Select(n=>$"{n}/1-{n}/50"));
        string config="; hpStack_WC Configuration Editor\nhostname EIGHT-MEMBER-REFERENCE\nvsf\n"+members+"\n exit\n"+aggregates+"\nvlan 1\n no untagged "+allMembers+",Trk1-T\nrk17\n exit\nvlan 10\n tagged 1/49,Trk1\n exit\nvlan 110\n tagged\n "+access+"\n voice\n exit\nvlan 120\n untagged\n "+access+"\n exit\nvlan 140\n tagged Trk1\n exit\nvlan 150\n name SECURITY-SURVEILLANCE\n untagged 8/21-8/44\n tagged Trk1\n exit\nvlan 160\n name MESSAGE-SYSTEM-BOARDS\n untagged 8/45-8/48\n tagged Trk1\n exit\nvlan 300\n untagged Trk2-Trk17\n tagged Trk1\n exit\nvlan 500\n tagged 1/49,Trk1-Trk17\n exit\nvlan 600\n tagged 1/49,Trk1-Trk17\n exit\n";
        var stack=Parser.Parse(config);
        Assert(stack.Blockers.Count==0&&stack.Ports.Count==416&&stack.Groups.Count==8&&stack.Ports.Count(p=>p.StackLink)==16,"Eight mixed VSF members retain 416 interfaces and 16 explicit stack links");
        var sheet=QuickSheet.Build(stack,Charts.Layout(stack),"EIGHT-MEMBER");
        Assert(sheet.Sockets.Count==416&&sheet.Scene.Boxes.Count(b=>b.Role=="ActivePanel")==8&&Charts.ValidateScene(sheet.Scene).Count==0,"All eight members and the complete 17-trunk key fit one A4 scene without collisions");
        Assert(sheet.AggregateColors.Count==17&&sheet.AggregateColors.Values.Distinct().Count()==17,"All seventeen trunks receive distinct colors");
        foreach(var a in sheet.AggregateColors)foreach(var b in sheet.AggregateColors.Where(b=>string.CompareOrdinal(a.Key,b.Key)<0))
            Assert(AggregatePalette.Difference(a.Value,b.Value)>=22,"Expanded outline palette retains the existing color separation guard");
        foreach(var p in stack.Ports.Where(p=>p.Aggregate.Length>0))
            Assert(AggregatePalette.Difference(sheet.AggregateColors[p.Aggregate],VlanPalette.NativeFill(p,sheet.VlanColors))>=AggregatePalette.MinimumMemberDistance,"Seventeen-trunk outlines remain distinct from member native fills");
        Assert(sheet.Scene.Shapes.Count(s=>s.Owner.Length==0&&s.Points.Length==4&&sheet.AggregateColors.Values.Contains(s.Stroke))==18,"Seventeen rectangle key samples and one reading-guide sample are retained");
        Assert(sheet.Scene.Dots.All(d=>Math.Abs(d.Radius-sheet.MarkerRadius)<.001)&&Proportional(sheet),"Eight-member fitting retains uniform proportional interfaces and consistent marker sizing");
        Console.WriteLine("PASS wrapped Trk fragments and physical ranges at every split position; eight mixed VSF members, 416 ports and 17 separated rectangular trunk keys on one A4 scene.");
    }
    static void CatalogueChecks()
    {
        int count=0;
        foreach(var model in HardwareCatalog.Models)
        {
            string config=model.OS=="Comware"?"# "+model.Sku+"\nversion 7.1.070\nsysname CATALOGUE\n":model.OS=="Instant On 1960"?"config-file-header\nCATALOGUE\nvInstantOn_1960_3.3.0.0\nSKU Description \"Instant On 1960 "+model.Sku+"\"\n":model.OS=="AOS-CX"?"! AOS-CX "+model.Sku+"\nhostname CATALOGUE\n":"; "+model.Sku+" Configuration Editor\nhostname CATALOGUE\n";
            var inv=Parser.Parse(config);var positions=Charts.Layout(inv);
            Assert(inv.Blockers.Count==0&&inv.Ports.Count==model.Copper+model.Optical,"Catalogue parser/count failed: "+model.Sku+": "+string.Join("; ",inv.Blockers));
            Assert(Charts.Validate(inv,positions).Count==0,"Catalogue physical geometry failed: "+model.Sku+": "+string.Join("; ",Charts.Validate(inv,positions)));
            var sheet=QuickSheet.Build(inv,positions,"CATALOGUE");
            Assert(Charts.ValidateScene(sheet.Scene).Count==0,"Catalogue A4 geometry failed: "+model.Sku+": "+string.Join("; ",Charts.ValidateScene(sheet.Scene)));
            Assert(sheet.Sockets.Count==model.Sockets.Count,"Every physical socket, including shared alternatives, is drawn: "+model.Sku);
            count++;
        }
        var stack2920=Parser.Parse("; hpStack_WB Configuration Editor\nstacking\n member 1 type \"J9729A\"\n member 2 type \"J9727A\"\n exit\nhostname TEST-2920\ntrunk 1/47,2/23 trk7 lacp\ninterface 1/B1\n name Rear-Uplink\n exit\nvlan 10\n untagged 1/1-1/48,2/1-2/24,1/B1-1/B2\n exit\nvlan 20\n tagged\n 1/1,1/47,1/B1-1/B2,2\n /23\n exit\n");
        Assert(stack2920.Blockers.Count==0&&stack2920.Ports.Count==74,"2920 mixed stack retains bases and only referenced rear module interfaces");
        var rear=stack2920.Groups.Single(g=>g.Rear);
        Assert(rear.Product==""&&stack2920.Ports.Where(p=>p.Group==rear.Id).All(p=>p.Media=="Unspecified"),"Missing rear SKU does not invent SFP+ or copper media");
        Assert(stack2920.Ports.Single(p=>p.Id=="2/23").TaggedVlans.Contains(20)&&stack2920.Ports.Single(p=>p.Id=="1/47").Aggregate=="trk7","Mid-interface wrapping and cross-member aggregate assignments survive");
        var stack2920Sheet=QuickSheet.Build(stack2920,Charts.Layout(stack2920),"2920");
        Assert(stack2920Sheet.Sockets.Count==82&&stack2920Sheet.Sockets.Count(p=>p.Port=="1/45")==2,"2920 copper/SFP sockets share four interfaces per member");
        Assert(stack2920Sheet.Sockets.Any(p=>p.Port=="1/B1"&&p.Display=="B1")&&stack2920Sheet.Scene.Shapes.Where(p=>p.Owner=="1/B1").All(p=>p.Points.Length==4),"Unspecified rear ports use B1 labels and neutral rectangles");
        Assert(Charts.ValidateScene(stack2920Sheet.Scene).Count==0,"2920 stack with separate rear panels fits A4 without collisions");
        var knownRear=Parser.Parse("; hpStack_WB Configuration Editor\nstacking\n member 1 type \"J9729A\"\n exit\ninterface 1/B1\n exit\n","1/B=J9731A");
        Assert(knownRear.Blockers.Count==0&&knownRear.Ports.Single(p=>p.Id=="1/B1").Media=="SFP+"&&knownRear.Groups.Single(g=>g.Rear).Product=="J9731A","An explicit 2920 rear SKU supplies verified media");
        Assert(Parser.Parse("; J9729A Configuration Editor\ninterface B3\n exit\n").Blockers.Count>0,"An impossible 2920 rear port cannot create extra inventory");
        var smart=Parser.Parse("; J9851A Configuration Editor\nmodule G type j9995a\n");
        Assert(smart.Blockers.Count==0&&smart.Ports.Count==8&&smart.Ports.All(p=>p.Media=="RJ-45")&&Charts.Layout(smart).Select(p=>p.Tile.Y).Distinct().Count()==1,"J9995A retains eight single-row copper ports");
        Console.WriteLine("PASS 2920 mixed stacks, shared combo sockets, optional rear module identities, neutral unknown media, B-port labels, split interface continuations and J9995A single-row copper geometry.");
        var sfp6100=Parser.Parse("! AOS-CX JL679A\n");
        Assert(sfp6100.Ports.Count==16&&sfp6100.Ports.Single(p=>p.Number==15).Media=="SFP+"&&sfp6100.Ports.Single(p=>p.Number==13).Media=="RJ-45","6100 compact model has independent copper 13/14 and optical 15/16");
        var combo=Parser.Parse("; J9774A Configuration Editor\n");var comboSheet=QuickSheet.Build(combo,Charts.Layout(combo),"TEST");
        Assert(combo.Ports.Count==10&&comboSheet.Sockets.Count==12&&comboSheet.Sockets.Count(s=>s.Port=="9")==2&&comboSheet.Sockets.Where(s=>s.Port=="9").Select(s=>s.Optical).Distinct().Count()==2,"2530 combo 9/10 share interfaces while retaining both media silhouettes");
        var cw=Parser.Parse("# JG932A\nversion 7.1.070\nsysname COMWARE\n#\nvlan 10\n name Data\n#\ninterface Bridge-Aggregation1\n port link-type trunk\n port trunk pvid vlan 10\n port trunk permit vlan 10 20 to 21\n link-aggregation mode dynamic\n#\ninterface GigabitEthernet1/0/1\n port link-aggregation group 1\n#\ninterface GigabitEthernet1/0/2\n port link-type hybrid\n port hybrid pvid vlan 10\n port hybrid vlan 10 untagged\n port hybrid vlan 20 to 22 tagged\n undo port hybrid vlan 22\n voice-vlan 20 enable\n#\nirf-port 1/1\n port group interface Ten-GigabitEthernet1/0/28\n#\n");
        Assert(cw.Blockers.Count==0&&cw.Ports[0].UntaggedVlan==10&&cw.Ports[0].TaggedVlans.SequenceEqual(new[]{20,21})&&cw.Ports[0].Aggregate=="BAGG1","Comware aggregate VLAN range/PVID inheritance");
        Assert(cw.Ports[1].UntaggedVlan==10&&cw.Ports[1].TaggedVlans.SequenceEqual(new[]{20,21})&&cw.Ports[1].VoiceVlan==20&&cw.Ports.Last().StackLink,"Comware hybrid removal, voice configuration and explicit black IRF links");
        var cwLayout=Charts.Layout(cw);var uplinks=cwLayout.Where(p=>cw.Ports.Single(q=>q.Id==p.Port).Number>=25).ToList();
        Assert(uplinks.Select(p=>p.Tile.Y).Distinct().Count()==1&&uplinks[0].Tile.Y>cwLayout[0].Tile.Y&&uplinks.Select(p=>p.Tile.X).SequenceEqual(uplinks.Select(p=>p.Tile.X).Order()),"5130 25-28 optical uplinks run horizontally along the lower row");
        var hybrid=Parser.Parse("# JG932A\nversion 7.1.070\nsysname BAD\ninterface GigabitEthernet1/0/1\n port link-type hybrid\n port hybrid vlan 10 20 untagged\n");
        Assert(hybrid.Blockers.Any(b=>b.Contains("Multiple explicit untagged")),"A multicolor native state must not be silently reduced to one VLAN");
        var mixed=Parser.Parse("version 7.1.070\nsysname IRF\ninterface GigabitEthernet1/0/1\n port access vlan 10\n#\ninterface GigabitEthernet2/0/48\n port access vlan 20\n","1=JG932A,2=JG934A");
        Assert(mixed.Blockers.Count==0&&mixed.Ports.Count==80&&mixed.Groups.Count==2,"Explicit mixed Comware member inventory retains all physical ports");
        Assert(Parser.Parse("# JG932A\nversion 7.1.070\nsysname CONFLICT\n","JG934A").Blockers.Count>0,"Selected model cannot override contradictory product evidence");
        Assert(Parser.Parse("! AOS-CX JL675A\n","CX6000-6100-48G-4OPT").Blockers.Count>0,"Shared family inference cannot be chosen as an exact SKU");
        Assert(Parser.Parse("; Configuration Editor\n","J9990A").Blockers.Count>0&&Parser.Parse("; Configuration Editor\n","A=J9990A").Groups.Single().Slot=="A","Selected modules require explicit physical slots rather than a standalone switch identity");
        var dc=Parser.Parse("! AOS-CX JL479A\nhostname DATACENTER\n");var dcMap=Charts.Layout(dc);
        Assert(dc.Firmware=="Unknown","A product SKU in an AOS-CX identity header is not a firmware version");
        var q49=dcMap.Single(p=>p.Port=="1/1/49");var q50=dcMap.Single(p=>p.Port=="1/1/50");var q51=dcMap.Single(p=>p.Port=="1/1/51");var q52=dcMap.Single(p=>p.Port=="1/1/52");
        Assert(q49.Tile.X==q50.Tile.X&&q50.Tile.X==q51.Tile.X&&q49.Tile.Y<q50.Tile.Y&&q50.Tile.Y<q51.Tile.Y&&q52.Tile.X>q49.Tile.X&&q52.Tile.Y==q49.Tile.Y,"8320 QSFP uplinks use two columns of three physical sockets");
        Assert(Math.Abs(QuickSheet.PanelHeight(dc)-81.4)<.01,"Three physical rows retain the same port size, with equal panel height within the diagram");
        Console.WriteLine("PASS all "+count+" source-referenced catalogue profiles parse and fit their verified socket layouts on A4; Comware native/hybrid/voice/aggregate/IRF and explicit mixed-member identities; compact independent ports, combo alternatives and three-row QSFP bank.");
    }
}
