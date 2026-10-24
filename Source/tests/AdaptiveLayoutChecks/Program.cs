using HUBTechGenerator;
using System.Text.Json;
using System.Text.RegularExpressions;

static class AdaptiveLayoutChecks
{
    static int checks;
    static readonly SkiaSharp.SKTypeface Regular=LoadFont("Report-Regular.ttf"),Bold=LoadFont("Report-Bold.ttf");
    static SkiaSharp.SKTypeface LoadFont(string name)
    {
        using var stream=typeof(ReportRenderer).Assembly.GetManifestResourceStream("HUBTechGenerator.Fonts."+name)??throw new Exception("Missing embedded font: "+name);
        return SkiaSharp.SKTypeface.FromStream(stream)??throw new Exception("Unreadable embedded font: "+name);
    }
    static void Check(bool value,string message)
    {
        checks++;
        if(!value)throw new Exception(message);
    }
    static Inventory Small()=>Parser.Parse("config-file-header\nSMALL\nvInstantOn_1930_3.3.2.0 (5)\nSKU Description \"Instant On 1930 JL680A\"\n");
    static Inventory Wide()=>Parser.Parse("; JL256A Configuration Editor\nhostname WIDE-LAYOUT\n");
    static Inventory Modular()=>Parser.Parse("; J9850A Configuration Editor\nhostname CHASSIS-LAYOUT\nmodule A type J9986A\nmodule B type J9993A\nmodule C type J9990A\n");
    static Inventory Stack()=>Parser.Parse("; Aruba Configuration Editor\nhostname STACK-LAYOUT\n"+string.Join('\n',Enumerable.Range(1,8).Select(n=>$"vsf member {n} type JL558A"))+"\n");
    static void Dense(Inventory inv,int count,bool aggregate=false)
    {
        Check(inv.Blockers.Count==0,"Fixture parser failed: "+string.Join(';',inv.Blockers));
        inv.Vlans=new(){new Vlan{Id=1,Name="Data"}};
        inv.Vlans.AddRange(Enumerable.Range(100,count).Select((id,index)=>new Vlan{Id=id,Voice=index%7==0}));
        foreach(var port in inv.Ports)
        {port.UntaggedVlan=1;port.UntaggedState="Known";port.TaggedVlans.Clear();port.VoiceVlan=null;port.Aggregate="";port.Notes.Clear();}
        foreach(var group in inv.Groups)
        {
            var ports=inv.Ports.Where(p=>p.Group==group.Id).ToList();
            var targets=ports.Where(p=>p.Media=="RJ-45").Take(1).Concat(ports.Where(p=>p.Media!="RJ-45").Take(1)).ToList();
            foreach(var port in targets)
            {port.TaggedVlans=Enumerable.Range(100,count).ToList();if(count>0)port.VoiceVlan=100;}
            if(aggregate&&targets.Count==2)foreach(var port in targets)
            {port.Aggregate="trk1";port.Notes.Add("Aggregation mode: lacp");}
        }
        DeviceIdentities.Apply(inv,Array.Empty<DeviceIdentity>());
    }
    static List<int> MarkerIds(Inventory inv,Port p)=>p.TaggedVlans
        .Concat(p.VoiceVlan.HasValue?new[]{p.VoiceVlan.Value}:Array.Empty<int>())
        .Concat(p.UntaggedVlan.HasValue&&inv.Vlans.Any(v=>v.Id==p.UntaggedVlan&&v.Voice)?new[]{p.UntaggedVlan.Value}:Array.Empty<int>())
        .Distinct().Order().ToList();
    static Box Bounds(Shape shape)=>new(shape.Points.Min(p=>p.X),shape.Points.Min(p=>p.Y),shape.Points.Max(p=>p.X)-shape.Points.Min(p=>p.X),shape.Points.Max(p=>p.Y)-shape.Points.Min(p=>p.Y),"");
    static bool Contains(Shape shape,double x,double y)
    {
        bool inside=false;var points=shape.Points;
        for(int i=0,j=points.Length-1;i<points.Length;j=i++)
        {
            var a=points[j];var b=points[i];
            double cross=(x-a.X)*(b.Y-a.Y)-(y-a.Y)*(b.X-a.X);
            if(Math.Abs(cross)<1e-7&&x>=Math.Min(a.X,b.X)-1e-7&&x<=Math.Max(a.X,b.X)+1e-7&&y>=Math.Min(a.Y,b.Y)-1e-7&&y<=Math.Max(a.Y,b.Y)+1e-7)return true;
            if((points[i].Y>y)!=(points[j].Y>y)&&x<(points[j].X-points[i].X)*(y-points[i].Y)/(points[j].Y-points[i].Y)+points[i].X)inside=!inside;
        }
        return inside;
    }
    static double Extent(Dot d)=>d.Radius+Math.Max(d.Voice?d.VoiceExtra+d.Outline/2:d.Halo,d.Outline/2);
    static List<Shape> Faces(Scene scene)=>scene.Shapes.Where(s=>s.Owner.Length>0&&s.Fill.Length>0).ToList();
    static void CheckHeadingInk(Scene scene,string name)
    {
        var backdrops=scene.Boxes.Where(b=>b.Fill.Equals("#17324d",StringComparison.OrdinalIgnoreCase)||b.Fill.Equals("#13283d",StringComparison.OrdinalIgnoreCase)).ToList();
        foreach(var label in scene.Labels.Where(l=>l.Owner.Length==0&&l.Color.Equals("#ffffff",StringComparison.OrdinalIgnoreCase)))
        {
            using var font=new SkiaSharp.SKFont(label.Bold?Bold:Regular,(float)label.Size){Edging=SkiaSharp.SKFontEdging.Antialias};
            string display=ReportRenderer.Measure(label).Text;font.MeasureText(display,out var ink);
            double x=label.InkBox?label.X:label.X+ink.Left,y=label.InkBox?label.Y:label.Y-font.Metrics.Ascent+ink.Top;
            Check(backdrops.Any(b=>x>=b.X-.05&&y>=b.Y-.05&&x+ink.Width<=b.X+b.W+.05&&y+ink.Height<=b.Y+b.H+.05),name+": white heading ink escapes its navy backdrop and loses contrast: "+display+
                $"; ink=({x:0.###},{y:0.###})-({x+ink.Width:0.###},{y+ink.Height:0.###}), G={scene.DiagramScale:0.###}, C={scene.CompactionLevel}, nearby bands="+string.Join(" / ",backdrops.Where(b=>Math.Abs(b.Y-label.Y)<15).Select(b=>$"({b.X:0.###},{b.Y:0.###})-({b.X+b.W:0.###},{b.Y+b.H:0.###})")));
        }
    }
    static bool CornerClears(Shape face,double x,double y,double inset)
    {
        if(!Contains(face,x,y))return false;
        var points=face.Points;
        for(int i=0;i<points.Length;i++)
        {
            var a=points[i];var b=points[(i+1)%points.Length];double dx=b.X-a.X,dy=b.Y-a.Y;
            double t=Math.Clamp(((x-a.X)*dx+(y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
            double distance=Math.Sqrt(Math.Pow(x-a.X-t*dx,2)+Math.Pow(y-a.Y-t*dy,2));
            if(distance+1e-6<inset)return false;
        }
        return true;
    }
    // Independent oracle: enumerate every rectangular grid instead of using
    // the production planner or its capacity/packing functions.
    static bool CanPack(QuickSheet.Socket socket,double radius)
    {
        if(socket.MarkerCount==0)return true;
        var face=new Shape(socket.FacePoints,"","",0);var bounds=Bounds(face);
        double extent=radius+.08,pitch=2*extent+.14;
        for(int columns=1;columns<=socket.MarkerCount;columns++)
        {
            int rows=(socket.MarkerCount+columns-1)/columns;
            if(socket.MarkerTop+2*extent+(rows-1)*pitch>socket.MarkerBottom+1e-6)continue;
            bool fits=true;
            for(int row=0;row<rows&&fits;row++)
            {
                int count=Math.Min(columns,socket.MarkerCount-row*columns);
                double start=bounds.X+bounds.W/2-(count-1)*pitch/2,cy=socket.MarkerTop+extent+row*pitch;
                for(int col=0;col<count&&fits;col++)
                {
                    double cx=start+col*pitch;
                    foreach(var corner in new[]{(cx-extent,cy-extent),(cx+extent,cy-extent),(cx-extent,cy+extent),(cx+extent,cy+extent)})
                        if(!CornerClears(face,corner.Item1,corner.Item2,socket.MarkerInset)){fits=false;break;}
                }
            }
            if(fits)return true;
        }
        return false;
    }
    static void CheckSmallestReduction(QuickSheet.Result sheet,string name)
    {
        var sockets=sheet.Sockets.Where(s=>s.MarkerCount>0).ToList();
        double preferred=Math.Min(4.2,2.1*sheet.GeometryScale);
        Check(sheet.MarkerRadius>=1.1-1e-5&&sheet.MarkerRadius<=preferred+1e-5,name+": selected radius exceeds readable/preferred bounds.");
        Check(Math.Abs(sheet.Scene.MarkerRadius-sheet.MarkerRadius)<1e-5,name+": result/scene marker metadata disagree.");
        Check(Math.Abs(sheet.Scene.DiagramScale-sheet.GeometryScale)<1e-5,name+": result/scene geometry metadata disagree.");
        if(sockets.Count==0)return;
        Check(sockets.All(s=>CanPack(s,sheet.MarkerRadius)),name+": selected radius does not independently fit the recorded face/inset regions.");
        double low=0,high=preferred;
        for(int iteration=0;iteration<28;iteration++)
        {double mid=(low+high)/2;if(sockets.All(s=>CanPack(s,mid)))low=mid;else high=mid;}
        Check(sheet.MarkerRadius+.02>=low,name+": markers were reduced more than needed. Chosen="+sheet.MarkerRadius+", feasible="+low);
    }
    static (double Scale,double Radius,int Markers) ValidateScene(Inventory inv,Scene scene,string name,bool final)
    {
        var faces=Faces(scene);Check(faces.Count>0,name+": no port faces.");
        var scales=new List<double>();int markers=0,voice=0;
        foreach(var face in faces)
        {
            var port=inv.Ports.Single(p=>p.Id==face.Owner);var bounds=Bounds(face);bool optical=port.Media!="RJ-45";
            double widthScale=bounds.W/25.2,heightScale=bounds.H/(optical?16.2:23.2);
            Check(Math.Abs(widthScale-heightScale)<1e-5,name+": interface shape was stretched: "+port.Id);
            scales.Add(widthScale);
            var dots=scene.Dots.Where(d=>Contains(face,d.X,d.Y)).ToList();
            var ids=MarkerIds(inv,port);Check(dots.Count==ids.Count,name+": markers were lost or added on "+port.Id+".");markers+=dots.Count;
            foreach(var id in ids)
            {
                string color=scene.VlanColors?[id]??throw new Exception("The chart must retain its selected VLAN palette.");
                Check(dots.Count(d=>d.Fill==color)==1,name+": VLAN marker color is missing/duplicated on "+port.Id+" for VLAN "+id);
            }
            var number=scene.Labels.Where(l=>l.Owner==port.Id&&l.InkBox)
                .OrderBy(l=>Math.Abs(l.X+ReportRenderer.Measure(l).Width/2-(bounds.X+bounds.W/2))).FirstOrDefault();
            Check(number!=null,name+": port number is missing: "+port.Id);var numberSize=ReportRenderer.Measure(number!);
            Check(numberSize.Text==number!.Text,name+": port number was truncated: "+port.Id);
            Check(Math.Abs(number.X+numberSize.Width/2-(bounds.X+bounds.W/2))<.035,name+": port number is not centered: "+port.Id);
            if(final)Check(number.Size>=4.5-1e-5,name+": printed port number is below its readable floor.");
            if(!optical)
            {
                foreach(var c in new[]{(number.X,number.Y),(number.X+numberSize.Width,number.Y),(number.X,number.Y+numberSize.Height),(number.X+numberSize.Width,number.Y+numberSize.Height)})
                    Check(Contains(face,c.Item1,c.Item2),name+": RJ45 number escaped its tab: "+port.Id);
            }
            else Check(number.Y>bounds.Y+bounds.H,name+": optical number must remain below its cage.");
            foreach(var d in dots)
            {
                double extent=Extent(d);if(d.Voice)voice++;
                foreach(var c in new[]{(d.X-extent,d.Y-extent),(d.X+extent,d.Y-extent),(d.X-extent,d.Y+extent),(d.X+extent,d.Y+extent)})
                    Check(Contains(face,c.Item1,c.Item2),name+": marker halo/outline escaped its proportional face: "+port.Id);
                Check(d.Y+extent<number.Y-.05,name+": marker touches the reserved number area: "+port.Id);
            }
            for(int i=0;i<dots.Count;i++)for(int j=i+1;j<dots.Count;j++)
                Check(Math.Abs(dots[i].X-dots[j].X)>Extent(dots[i])+Extent(dots[j])-1e-5||Math.Abs(dots[i].Y-dots[j].Y)>Extent(dots[i])+Extent(dots[j])-1e-5,name+": marker extents overlap: "+port.Id);
        }
        Check(scales.Max()-scales.Min()<1e-5,name+": interfaces/media use different geometry scales.");
        var all=scene.Dots;Check(all.Count>=2,name+": guide markers are missing.");
        Check(all.Max(d=>d.Radius)-all.Min(d=>d.Radius)<1e-5,name+": markers in ports, voice diamonds and keys use different radii.");
        Check(all.All(d=>d.VoiceExtra==0),name+": voice diamonds and circles must use one consistent outer size.");
        if(final)
        {
            Check(scales.Min()>=1-1e-4,name+": interfaces were uniformly shrunk instead of reducing markers as the fallback.");
            Check(all[0].Radius>=1.1-1e-5&&all[0].Radius<=Math.Min(4.2,2.1*scene.DiagramScale)+1e-5,name+": marker size exceeds readable/preferred bounds.");
            foreach(var label in scene.Labels.Where(l=>l.Owner.Length==0))Check(label.Size>=7-1e-5,name+": printed heading/key text is below seven points: "+label.Text);
            CheckHeadingInk(scene,name);
            Check(Charts.ValidateScene(scene).Count==0,name+": final text/shape/marker collision: "+string.Join(';',Charts.ValidateScene(scene)));
        }
        if(MarkerIds(inv,inv.Ports.First()).Count>0)Check(voice>0,name+": dense fixtures must retain voice diamonds.");
        return (scales[0],all[0].Radius,markers);
    }
    static (double Scale,double Radius) Run(string name,Inventory inv,int count,bool aggregate=false,bool inspectBooklet=false,bool withLogo=false)
    {
        Dense(inv,count,aggregate);var source=JsonSerializer.Serialize(inv);var layout=Charts.Layout(inv);
        Check(Charts.Validate(inv,layout).Count==0,name+": physical layout invalid.");
        var sheet=QuickSheet.Build(inv,layout,"ADAPTIVE-TEST");
        Check(sheet.Scene.IsAdaptiveLayout,name+": the layout must identify its completed adaptive fit.");
        if(name=="small-0")Check(sheet.CompactionLevel==0,name+": small banks should not compact fonts/padding when the preferred markers already fit with uniform growth.");
        CheckSmallestReduction(sheet,name);
        ValidateScene(inv,sheet.Scene,name+" pre-fit",false);
        var beforePoints=sheet.Scene.Shapes.SelectMany(s=>s.Points).ToArray();var beforeDots=sheet.Scene.Dots.ToArray();var beforeLabels=sheet.Scene.Labels.ToArray();
        QuickSheet.FitToPage(sheet.Scene);var result=ValidateScene(inv,sheet.Scene,name,true);
        Check(beforePoints.SequenceEqual(sheet.Scene.Shapes.SelectMany(s=>s.Points))&&beforeDots.SequenceEqual(sheet.Scene.Dots)&&beforeLabels.SequenceEqual(sheet.Scene.Labels),name+": a completed adaptive fit must be idempotent.");
        Check(Math.Abs(result.Scale-sheet.GeometryScale)<1e-5&&Math.Abs(result.Radius-sheet.MarkerRadius)<1e-5,name+": measured geometry disagrees with sizing metadata.");
        if(withLogo)
        {
            string folder=Path.Combine(Path.GetTempPath(),"adaptive-layout-logo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            string file=Path.Combine(folder,"client-logo.png");
            try
            {
                using(var surface=SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(320,100)))
                {
                    surface.Canvas.Clear(SkiaSharp.SKColors.White);
                    using var paint=new SkiaSharp.SKPaint{Color=SkiaSharp.SKColors.Navy};
                    surface.Canvas.DrawRect(15,20,290,60,paint);
                    using var image=surface.Snapshot();using var data=image.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);File.WriteAllBytes(file,data.ToArray());
                }
                var logo=ReportLogo.Load(file);logo.Apply(sheet.Scene);
                Check(sheet.Scene.Images.Count==1,name+": client logo is missing.");
                Check(Charts.ValidateScene(sheet.Scene).Count==0,name+": logo or header collides after adaptive sizing.");
                var after=ValidateScene(inv,sheet.Scene,name+" logo",true);
                Check(Math.Abs(after.Scale-result.Scale)<1e-5&&Math.Abs(after.Radius-result.Radius)<1e-5,name+": applying a logo changed interface or marker sizing.");
            }
            finally
            {
                if(File.Exists(file))File.Delete(file);
                Directory.Delete(folder);
            }
        }
        Check(JsonSerializer.Serialize(inv)==source,name+": display sizing changed inventory data.");
        if(inspectBooklet)
        {
            var pages=Charts.Booklet(inv,sheet.Scene,"ADAPTIVE-TEST");
            foreach(var port in inv.Ports)
            {
                var labels=pages.Skip(1).SelectMany(p=>p.Labels).Where(l=>l.Owner=="inventory:"+port.Id+":4");
                var numbers=Regex.Matches(string.Join(" ",labels.Select(l=>l.Text)),@"\b\d+\b").Select(m=>int.Parse(m.Value));
                Check(numbers.SequenceEqual(port.TaggedVlans),name+": PDF inventory loses tagged VLAN IDs while sizing the diagram.");
            }
        }
        Console.WriteLine($"PASS {name}: geometry {result.Scale:0.###}x, marker radius {result.Radius:0.###} pt, compaction {sheet.CompactionLevel}, {result.Markers} physical markers.");
        return (result.Scale,result.Radius);
    }
    static void VoiceRolesRemainLocal()
    {
        var inv=Wide();Dense(inv,1);
        inv.Vlans.Single(v=>v.Id==100).Voice=false;
        foreach(var port in inv.Ports)port.VoiceVlan=null;
        var voice=inv.Ports[0];var ordinary=inv.Ports[1];
        voice.TaggedVlans=new(){100};voice.VoiceVlan=100;ordinary.TaggedVlans=new(){100};
        var sheet=QuickSheet.Build(inv,Charts.Layout(inv),"LOCAL-VOICE");
        var voiceFace=Faces(sheet.Scene).Single(s=>s.Owner==voice.Id);var ordinaryFace=Faces(sheet.Scene).Single(s=>s.Owner==ordinary.Id);
        var voiceDot=sheet.Scene.Dots.Single(d=>Contains(voiceFace,d.X,d.Y));var ordinaryDot=sheet.Scene.Dots.Single(d=>Contains(ordinaryFace,d.X,d.Y));
        Check(voiceDot.Voice,"An interface's explicit voice VLAN must retain its diamond.");
        Check(!ordinaryDot.Voice,"An explicit voice designation on another interface must not turn an ordinary tagged membership into a diamond.");
        Check(Math.Abs(voiceDot.Radius-ordinaryDot.Radius)<1e-6,"Local voice/circle roles must retain the same global marker size.");
        inv.Vlans.Single(v=>v.Id==1).Voice=true;
        var nativeVoice=QuickSheet.Build(inv,Charts.Layout(inv),"NATIVE-VOICE");
        var nativeFace=Faces(nativeVoice.Scene).Single(s=>s.Owner==inv.Ports[2].Id);
        var nativeDot=nativeVoice.Scene.Dots.Single(d=>Contains(nativeFace,d.X,d.Y));
        Check(nativeDot.Voice&&nativeDot.Fill==nativeVoice.VlanColors[1],"A globally designated native voice VLAN must retain its diamond even without a separate tagged membership.");
        Console.WriteLine("PASS explicit voice designation remains local to its interface while VLAN colors and marker sizes stay shared.");
    }
    static void Main()
    {
        foreach(int count in new[]{0,1,15,28,36,40})
        {
            var small=Run("small-"+count,Small(),count,aggregate:count>0,inspectBooklet:count==28);
            var wide=Run("wide-"+count,Wide(),count,aggregate:count>0,inspectBooklet:count==40);
            if(count==28)
            {
                Check(small.Scale>1.3,"Small dense diagrams must use available uniform growth before marker reduction.");
                Check(small.Radius>=Math.Min(4.2,2.1*small.Scale)-1e-4,"A small 28-marker diagram should retain preferred marker size when uniform growth can accommodate it.");
                Check(wide.Radius<Math.Min(4.2,2.1*wide.Scale)-1e-4,"A wide 28-marker bank must reduce markers consistently when proportional growth cannot fit the page.");
            }
        }
        Run("modular-28",Modular(),28,inspectBooklet:true);
        Run("modular-40",Modular(),40);
        Run("eight-members-28",Stack(),28);
        Run("eight-members-40",Stack(),40);
        Run("small-28-logo",Small(),28,aggregate:true,withLogo:true);
        Run("wide-40-logo",Wide(),40,aggregate:true,withLogo:true);
        VoiceRolesRemainLocal();
        Console.WriteLine("PASS "+checks+" adaptive geometry, complete-marker, voice, readable-font, consistent-scale and PDF-membership assertions.");
    }
}
