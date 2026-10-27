using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public sealed class OnePageCapacityException : Exception
{
    public OnePageCapacityException(string reason):base("Readable single-page A4 capacity exceeded: "+reason+". No diagram was exported."){}
}

// Reference geometry is fixed by media type across every diagram.
// The RJ45 uses one simple latch tab; the optical cage retains its wide profile.
public static class QuickSheet
{
    public const double PortWidth=25.2,TabTop=17.8,PortHeight=TabTop+5.4,TabFraction=.42,OpticalHeight=16.2,PortFont=4.5,DotRadius=2.1,AggregateStroke=2.4,AggregateOutset=.3,NumberInset=1.1,NativeInset=.9,MarkerTopInset=1.05,CopperMarkerTopInset=1.05,MarkerGap=.25,MarkerHalo=.08,MarkerStroke=.16,PortGap=1,FaceStroke=.8;
    public const double ActivePanelHeight=SocketTop+RowPitch+PortHeight+PanelBottom;
    public const double MarkerPitch=4.5;
    public const double MarkerClearance=.14,MinimumDotRadius=1.1,MaximumDotRadius=4.2,MinimumPortFont=4.5,MinimumBodyFont=7;
    // Faces retain their original proportions at every marker density.
    public static double OpticalFaceHeight(Inventory inv)=>OpticalHeight;
    public static double CopperTabTop(Inventory inv)=>TabTop;
    public static double SocketHeight(Inventory inv)=>PortHeight;
    public static double PanelHeight(Inventory inv)=>SocketTop+(Math.Max(2,inv.Groups.SelectMany(g=>g.SocketLayout).Select(s=>s.Row+1).DefaultIfEmpty(2).Max())-1)*RowPitch+PortHeight+PanelBottom;
    const double PageWidth=805.89,PageHeight=559.28,Pitch=PortWidth+PortGap,RowPitch=PortHeight+PortGap,Header=8,SocketTop=Header+1,PanelBottom=.8,PanelGap=.5,DiagramTop=51,BodyFont=7.5;
    public record Socket(string Port,string Suffix,Box Tile,string Display,bool Optical,Box NumberBounds)
    {
        public (double X,double Y)[] FacePoints {get;init;}=Array.Empty<(double,double)>();
        public double MarkerTop {get;init;}
        public double MarkerBottom {get;init;}
        public double MarkerInset {get;init;}
        public int MarkerCount {get;init;}
    }
    public sealed class Result
    {
        public Scene Scene {get;}=new(){Width=PageWidth,Height=PageHeight};
        public double GeometryScale=>Scene.DiagramScale;
        public double MarkerRadius=>Scene.MarkerRadius;
        public int CompactionLevel=>Scene.CompactionLevel;
        public List<Socket> Sockets {get;}=new();
        public Dictionary<int,string> VlanColors {get;}=new();
        public Dictionary<string,string> AggregateColors {get;}=new(StringComparer.OrdinalIgnoreCase);
    }
    sealed record LayoutPlan(double Scale,int Compact,bool PreferWholeWords=true,bool KeyBelow=false)
    {
        public double Width=>(PageWidth-.8)/Scale;
        public double Bottom=>DiagramTop+(PageHeight-13-DiagramTop)/Scale;
        public double Gap=>Compact==0?PortGap:.65;
        public double Pitch=>PortWidth+Gap;
        public double RowPitch=>PortHeight+Gap;
        public double TitleInkHeight {get;set;}
        public double KeyTextWidth {get;set;}=78;
        public double PreferredRadius=>Math.Min(MaximumDotRadius,DotRadius*Scale);
        public double Header=>Math.Max(Height(Font(7),Scale),TitleInkHeight)+1.0/Scale;
        public double HeaderInset=>.15/Scale;
        public double TitleTop=>.55/Scale;
        public double SocketTop=>Header+(Compact==0?1:Math.Max(.65/Scale,AggregateOutset+.4/Scale));
        public double PanelBottom=>Compact==0?.8:Math.Max(.7/Scale,AggregateOutset+.4/Scale);
        public double PanelGap=>Compact==0?.5:.3/Scale;
        public double Gutter=>(Compact==0?10:5)/Scale;
        public double Font(double normal,double minimum=MinimumBodyFont)=>
            (Compact==2?Math.Max(minimum,normal-.5):Math.Clamp(normal*Scale,minimum,Math.Max(minimum,normal+1.5)))/Scale;
        public double NumberFont=>Math.Min(PortFont,9/Scale);
        public double MarkerTop=>Compact==0?MarkerTopInset:NativeInset+.075;
        public double NumberInset=>Compact==0?QuickSheet.NumberInset:1.0;
        public double MarkerGap=>Compact==0?QuickSheet.MarkerGap:.15/Scale;
        public double KeyWidth=>KeyTextInset+KeyTextWidth/Scale;
        public double KeyCircleX=>6+(PreferredRadius+1.1)/Scale;
        public double KeyTextInset=>6+(2*PreferredRadius+3)/Scale;
        public double HeadingHeight=>12/Scale;
        public double AfterHeading=>13/Scale;
        public double PanelHeight(Inventory inv)=>SocketTop+(Math.Max(2,inv.Groups.SelectMany(g=>g.SocketLayout).Select(s=>s.Row+1).DefaultIfEmpty(2).Max())-1)*RowPitch+PortHeight+PanelBottom;
    }
    record Panel(string Group,string Title,double X,double Y,double W,double H);
    sealed class Section
    {
        public string Title="";
        public int Columns=3;
        public List<string> Entries=new();
        public List<string> OutlineColors=new();
    }
    static double Height(double font,double scale=1)=>ReportRenderer.Measure(new Label(0,0,"Ag",font*scale)).Height/scale;
    static double Width(string text,double font,bool bold=false,double scale=1)=>ReportRenderer.Measure(new Label(0,0,text,font*scale,"#172333",bold)).Width/scale;
    static List<string> Wrap(string text,double width,double font,double scale=1)
    {
        if(width<=0)throw new OnePageCapacityException("text column is too narrow for readable labels");
        var lines=new List<string>();string line="";
        foreach(string word in text.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {
            string next=line.Length==0?word:line+" "+word;
            if(Width(next,font,scale:scale)<=width){line=next;continue;}
            if(line.Length>0){lines.Add(line);line="";}
            string rest=word;
            while(rest.Length>0&&Width(rest,font,scale:scale)>width)
            {
                if(Width(rest[..1],font,scale:scale)>width)throw new OnePageCapacityException("text column cannot fit one readable character");
                int n=1;while(n<rest.Length&&Width(rest[..(n+1)],font,scale:scale)<=width)n++;
                lines.Add(rest[..n]);rest=rest[n..];
            }
            line=rest;
        }
        if(line.Length>0)lines.Add(line);return lines;
    }
    public static string Ranges(IEnumerable<int> values)
    {
        var a=values.Distinct().Order().ToList();var parts=new List<string>();
        for(int i=0;i<a.Count;i++)
        {
            int start=a[i],end=start;while(i+1<a.Count&&a[i+1]==end+1)end=a[++i];
            parts.Add(start==end?start.ToString():start+"-"+end);
        }return string.Join(",",parts);
    }
    static string Ports(IEnumerable<Port> ports)
    {
        if(ports.Any(p=>p.DisplayNumber.Length>0))return string.Join(", ",ports.Select(p=>p.Id));
        var result=new List<string>();
        foreach(var g in ports.GroupBy(p=>Regex.Match(p.Id,@"^(.*?)(\d+)$").Groups[1].Value))
        {
            foreach(string range in Ranges(g.Select(p=>p.Number)).Split(',',StringSplitOptions.RemoveEmptyEntries))
            {
                string[] ends=range.Split('-');result.Add(g.Key+ends[0]+(ends.Length==2?"-"+g.Key+ends[1]:""));
            }
        }return string.Join(", ",result);
    }
    static string Display(Group g,Port p,string suffix)
    {
        // Match physical faceplates: local numbers, with slot/member context
        // in the heading and complete interface IDs in the key and inventory.
        return p.DisplayNumber.Length>0?p.DisplayNumber:(g.Rear?g.Slot:"")+p.Number;
    }
    static string Fill(Port p,IReadOnlyDictionary<int,string> palette)=>VlanPalette.NativeFill(p,palette);
    static string TextColor(string fill)
    {
        return AggregatePalette.Luminance(fill)<.18?"#ffffff":"#101820";
    }
    static bool IsVoice(Inventory inv,int id)=>inv.Vlans.Any(v=>v.Id==id&&v.Voice)||inv.Ports.Any(p=>p.VoiceVlan==id);
    static string PanelTitle(Inventory inv,Group g)
    {
        string identity=ReportText.GroupLabel(inv,g);
        string prefix=!ReportText.IsStack(inv)&&g.Slot.Length==0&&g.Prefix.Length>0?" | "+g.Prefix+"x":"";
        string media=g.Copper==0?g.Optical+" "+g.OpticalType:g.Optical==0?g.Copper+" RJ-45":g.Copper+" RJ-45 + "+g.Optical+" "+g.OpticalType;
        if(inv.OS=="RouterOS"&&g.OpticalType.Contains("QSFP"))media="1 RJ-45 + 24 SFP+ + 2 QSFP+";
        if(g.OpticalType=="Unspecified")media=g.Expected+" ports | Media unspecified";
        if(g.Product=="J4907A")media="14 RJ-45 + 2 combo";
        if(g.SocketLayout.GroupBy(s=>s.Number).Any(s=>s.Count()>1))media=g.Expected+" interfaces / "+g.SocketLayout.Count+" sockets (combo)";
        return string.Join(" | ",new[]{identity+prefix,DeviceModels.GroupModel(inv,g),media}.Where(s=>s.Length>0));
    }
    static List<int> MarkerVlans(Inventory inv,Port p)=>p.TaggedVlans.Concat(p.VoiceVlan.HasValue?new[]{p.VoiceVlan.Value}:Array.Empty<int>()).Concat(p.UntaggedVlan.HasValue&&inv.Vlans.Any(v=>v.Id==p.UntaggedVlan&&v.Voice)?new[]{p.UntaggedVlan.Value}:Array.Empty<int>()).Distinct().Order().ToList();
    static bool Inside((double X,double Y)[] points,double x,double y)
    {
        bool inside=false;
        for(int i=0,j=points.Length-1;i<points.Length;j=i++)
            if((points[i].Y>y)!=(points[j].Y>y)&&x<(points[j].X-points[i].X)*(y-points[i].Y)/(points[j].Y-points[i].Y)+points[i].X)inside=!inside;
        return inside;
    }
    public static (double X,double Y)[] Outline(double x,double y,bool optical,double scale=1,double tabTop=TabTop,double opticalHeight=OpticalHeight)
    {
        double portHeight=tabTop+5.4;const double bevel=.7;
        var points=optical?new[]{(bevel,0.0),(PortWidth-bevel,0.0),(PortWidth,bevel),(PortWidth,opticalHeight-bevel),(PortWidth-bevel,opticalHeight),(bevel,opticalHeight),(0.0,opticalHeight-bevel),(0.0,bevel)}:
            new[]{(0.0,0.0),(PortWidth,0.0),(PortWidth,tabTop),(PortWidth*(1+TabFraction)/2,tabTop),(PortWidth*(1+TabFraction)/2,portHeight),(PortWidth*(1-TabFraction)/2,portHeight),(PortWidth*(1-TabFraction)/2,tabTop),(0.0,tabTop)};
        return points.Select(p=>(x+p.Item1*scale,y+p.Item2*scale)).ToArray();
    }
    // Include the complete white halo and voice diamond stroke in clearance.
    public static double MarkerExtent(double radius)=>radius+Math.Max(MarkerHalo,MarkerStroke/2);
    static bool ContainsBox((double X,double Y)[] outline,double x,double y,double w,double h)=>
        Inside(outline,x,y)&&Inside(outline,x+w,y)&&Inside(outline,x,y+h)&&Inside(outline,x+w,y+h);
    // Every annotation remains on the native-colored face, clear of the frame.
    static bool ContainsFaceBox((double X,double Y)[] outline,double x,double y,double w,double h,double inset=NativeInset+.05)
    {
        if(!ContainsBox(outline,x,y,w,h))return false;
        foreach(var corner in new[]{(X:x,Y:y),(X:x+w,Y:y),(X:x,Y:y+h),(X:x+w,Y:y+h)})
        for(int i=0;i<outline.Length;i++)
        {
            var a=outline[i];var b=outline[(i+1)%outline.Length];double dx=b.X-a.X,dy=b.Y-a.Y;
            double t=Math.Clamp(((corner.X-a.X)*dx+(corner.Y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
            if(Math.Sqrt(Math.Pow(corner.X-a.X-t*dx,2)+Math.Pow(corner.Y-a.Y-t*dy,2))<inset)return false;
        }
        return true;
    }
    public static (double X,double Y)[] FrameOutline((double X,double Y)[] points,double outset)
    {
        return points.Select((p,i)=>
        {
            var a=points[(i+points.Length-1)%points.Length];var b=points[(i+1)%points.Length];
            double ax=p.X-a.X,ay=p.Y-a.Y,bx=b.X-p.X,by=b.Y-p.Y;
            double al=Math.Sqrt(ax*ax+ay*ay),bl=Math.Sqrt(bx*bx+by*by);
            double nx=ay/al,ny=-ax/al,mx=by/bl,my=-bx/bl;
            double factor=outset/(1+nx*mx+ny*my);
            return (p.X+(nx+mx)*factor,p.Y+(ny+my)*factor);
        }).ToArray();
    }
    static void SocketShape(Scene scene,(double X,double Y)[] points,string fill,string? aggregate,string owner="",bool optical=false,double scale=1)
    {
        scene.Shapes.Add(new(points,fill,"#182536",0,owner,true));
        if(aggregate!=null)
        {
            // Offset the continuous frame slightly outward to retain the
            // native-colored tab and number clearance. No white separator.
            points=FrameOutline(points,AggregateOutset*scale);
            scene.Shapes.Add(new(points,"",aggregate,AggregateStroke,owner,true));
        }
        scene.Shapes.Add(new(points,"","#182536",FaceStroke,owner,true));
    }
    static void DrawSocket(Result output,Inventory inv,Group g,Port port,Placement original,double x,double y,Dictionary<string,string> colors,LayoutPlan plan)
    {
        var scene=output.Scene;bool optical=original.Socket=="M"||original.Socket!="T"&&port.Media!="RJ-45";
        double h=optical?OpticalHeight:PortHeight;
        var points=port.Media=="Unspecified"?new[]{(x,y),(x+PortWidth,y),(x+PortWidth,y+h),(x,y+h)}:Outline(x,y,optical);
        string fill=Fill(port,output.VlanColors);
        SocketShape(scene,points,fill,port.Aggregate.Length>0?colors[port.Aggregate]:null,port.Id,optical);
        string display=Display(g,port,original.Socket)+(port.Disabled==true?"!":"");
        double numberWidth=(optical?PortWidth-2*NativeInset:PortWidth*TabFraction)-1;
        var number=new Label(0,0,display,plan.NumberFont,optical?"#101820":TextColor(fill),true,0,port.Id,true);
        // Ink bounds are measured at final printed size, then normalized, so
        // fractional geometry scales do not uncenter or clip small numerals.
        var measured=ReportRenderer.Measure(number with{Size=number.Size*plan.Scale});
        double numberWidthActual=measured.Width/plan.Scale,numberHeight=measured.Height/plan.Scale;
        if(numberWidthActual>numberWidth)throw new OnePageCapacityException("interface number exceeds its reserved bottom area");
        double numberY=y+PortHeight-plan.NumberInset-numberHeight;
        var labelBox=new Box(x+(PortWidth-numberWidthActual)/2,numberY,numberWidthActual,numberHeight,"");
        double numberClearance=port.Aggregate.Length>0?AggregateStroke/2-AggregateOutset+.05:FaceStroke/2+.05;
        if(optical?labelBox.Y<y+h+AggregateOutset+.5:!ContainsFaceBox(points,labelBox.X-.025,labelBox.Y-.02,labelBox.W+.05,labelBox.H+.04,numberClearance))
            throw new OnePageCapacityException("interface number does not fit inside its socket label area");
        scene.Labels.Add(number with {X=labelBox.X,Y=labelBox.Y});
        double markerInset=plan.Compact>0&&port.Aggregate.Length==0?FaceStroke/2+.1:NativeInset+.05;
        output.Sockets.Add(new(port.Id,original.Socket,new Box(x,y,PortWidth,h,fill),display,optical,labelBox)
        {
            FacePoints=points,MarkerTop=y+(plan.Compact==0?MarkerTopInset:markerInset+.025),
            MarkerBottom=Math.Min(labelBox.Y-plan.MarkerGap,optical?y+h:y+TabTop-.65),
            MarkerInset=markerInset,MarkerCount=MarkerVlans(inv,port).Count
        });
    }
    static bool MarkerPositions(Socket socket,double radius,out List<(double X,double Y)> positions)
    {
        positions=new();if(socket.MarkerCount==0)return true;
        double edge=MarkerExtent(radius),pitch=2*edge+MarkerClearance;
        int columns=Math.Max(1,(int)Math.Floor((socket.Tile.W-2*socket.MarkerInset-2*edge)/pitch)+1);
        int rows=Math.Max(0,(int)Math.Floor((socket.MarkerBottom-socket.MarkerTop-2*edge)/pitch)+1);
        for(int row=0;row<rows&&positions.Count<socket.MarkerCount;row++)
        {
            double yy=socket.MarkerTop+edge+row*pitch;
            int count=Math.Min(columns,socket.MarkerCount-positions.Count);
            while(count>0)
            {
                double left=socket.Tile.X+socket.Tile.W/2-(count-1)*pitch/2;
                bool fits=Enumerable.Range(0,count).All(n=>ContainsFaceBox(socket.FacePoints,left+n*pitch-edge,yy-edge,2*edge,2*edge,socket.MarkerInset));
                if(fits){for(int n=0;n<count;n++)positions.Add((left+n*pitch,yy));break;}
                count--;
            }
        }
        return positions.Count==socket.MarkerCount;
    }
    static bool MarkersFit(Result result,double radius)=>result.Sockets.All(s=>MarkerPositions(s,radius,out _));
    static Result AddMarkers(Result result,Inventory inv,double radius)
    {
        var scene=result.Scene;
        for(int i=0;i<scene.Dots.Count;i++)scene.Dots[i]=scene.Dots[i] with{Radius=radius,Halo=MarkerHalo,Outline=MarkerStroke,VoiceExtra=0};
        foreach(var socket in result.Sockets)
        {
            if(!MarkerPositions(socket,radius,out var positions))throw new OnePageCapacityException("markers cannot fit the proportional face of interface "+socket.Port);
            var port=inv.Ports.Single(p=>p.Id==socket.Port);var ids=MarkerVlans(inv,port);
            for(int i=0;i<ids.Count;i++)scene.Dots.Add(new(positions[i].X,positions[i].Y,radius,result.VlanColors[ids[i]],port.VoiceVlan==ids[i]||inv.Vlans.Any(v=>v.Id==ids[i]&&v.Voice),0,MarkerHalo,MarkerStroke));
        }
        scene.MarkerRadius=radius;
        var errors=Charts.ValidateScene(scene);if(errors.Count>0)throw new OnePageCapacityException(string.Join("; ",errors));
        return result;
    }
    static double DrawSection(Scene scene,Section section,double y,double width,double font,bool draw,LayoutPlan plan,double sectionX=0)
    {
        if(section.Entries.Count==0)return y;
        bool outlined=section.OutlineColors.Count>0;
        double lineHeight=Height(font,plan.Scale)+(plan.Compact==0?.3:.15/plan.Scale),gap=plan.Compact==0?12:8/plan.Scale,colWidth=(width-gap*(section.Columns-1))/section.Columns;
        double inset=outlined?25:0;
        if(draw){scene.Rect(sectionX,y,width,plan.HeadingHeight,"#e7eff6","#c3d2df",2,"Heading");scene.Text(sectionX+5,y+1/plan.Scale,section.Title,plan.Font(8.5,7.5),"#20374e",true,width-10,inkBox:true);}y+=plan.AfterHeading;
        int index=0;
        foreach(var row in section.Entries.Chunk(section.Columns))
        {
            var lines=row.Select(e=>Wrap(e,colWidth-inset,font,plan.Scale)).ToList();
            double rowHeight=Math.Max(outlined?(plan.Compact==0?PortHeight*.6+1.4:10+1/plan.Scale):0,lines.Max(l=>l.Count)*lineHeight);
            if(draw)for(int col=0;col<row.Length;col++)
            {
                double x=sectionX+col*(colWidth+gap);
                if(outlined)
                {
                    AggregateKey(scene,x+2,y+(rowHeight-10)/2,17,10,section.OutlineColors[index+col]);
                }
                double textY=y+(outlined?(rowHeight-lines[col].Count*lineHeight)/2:0);
                for(int n=0;n<lines[col].Count;n++)scene.Text(x+inset,textY+n*lineHeight,lines[col][n],font,"#172333",false,colWidth-inset);
            }
            y+=rowHeight+(plan.Compact==0?1.2:.7/plan.Scale);
            index+=row.Length;
        }return y+(plan.Compact==0?2:1/plan.Scale);
    }
    static void AggregateKey(Scene scene,double x,double y,double width,double height,string color)
    {
        scene.Shapes.Add(new(new[]{(x,y),(x+width,y),(x+width,y+height),(x,y+height)},"",color,AggregateStroke,ClipInside:true));
    }
    public static Result Build(Inventory inv,List<Placement> original,string hash)
    {
        var palette=VlanPalette.Assign(inv);
        var aggregate=AggregatePalette.Assign(inv,p=>Fill(p,palette),palette.Values);
        var alternatives=new List<Result>();OnePageCapacityException? last=null;
        foreach(bool keyBelow in new[]{false,true})for(int compact=0;compact<=2;compact++)
        {
            Result? Attempt(double scale)
            {
                foreach(bool wholeWords in new[]{true,false})foreach(bool narrow in new[]{true,false})
                {
                    try
                    {
                        var candidate=BuildCore(inv,original,hash,narrow,new(scale,compact,wholeWords,keyBelow),palette,aggregate);
                        ApplyGeometry(candidate,scale,compact);
                        var errors=Charts.ValidateScene(candidate.Scene);
                        if(errors.Count>0)throw new OnePageCapacityException(string.Join("; ",errors));
                        return candidate;
                    }
                    catch(OnePageCapacityException ex){last=ex;}
                }
                return null;
            }
            var best=Attempt(1);if(best==null)continue;
            double low=1,high=4;
            for(int iteration=0;iteration<12;iteration++)
            {
                double middle=(low+high)/2;var trial=Attempt(middle);
                if(trial==null)high=middle;else{low=middle;best=trial;}
            }
            double preferred=Math.Min(MaximumDotRadius,DotRadius*best.GeometryScale);
            if(MarkersFit(best,preferred))return AddMarkers(best,inv,preferred);
            alternatives.Add(best);
        }
        Result? fallback=null;double largest=-1;
        foreach(var candidate in alternatives)
        {
            if(!MarkersFit(candidate,MinimumDotRadius))continue;
            double low=MinimumDotRadius,high=Math.Min(MaximumDotRadius,DotRadius*candidate.GeometryScale);
            for(int i=0;i<18;i++){double middle=(low+high)/2;if(MarkersFit(candidate,middle))low=middle;else high=middle;}
            if(low>largest+1e-6){fallback=candidate;largest=low;}
        }
        if(fallback!=null)return AddMarkers(fallback,inv,largest);
        throw last??new OnePageCapacityException("the markers cannot fit with readable fonts and proportional interface faces");
    }
    // Reusable reference-scale component for a multi-configuration sheet. Unlike
    // Build(), this does not enlarge a single switch to consume the entire A4 page.
    // All symbol geometry, labels, keys and capacity guards use the same renderer.
    public static Result BuildCompact(Inventory inv,List<Placement> original,string hash,double? markerRadius=null)
    {
        if(markerRadius is {} requested&&(requested<MinimumDotRadius||requested>DotRadius))
            throw new ArgumentOutOfRangeException(nameof(markerRadius));
        var palette=VlanPalette.Assign(inv);
        var aggregate=AggregatePalette.Assign(inv,p=>Fill(p,palette),palette.Values);
        var candidates=new List<Result>();OnePageCapacityException? last=null;
        foreach(bool keyBelow in new[]{false,true})for(int compact=0;compact<=2;compact++)
        foreach(bool wholeWords in new[]{true,false})foreach(bool narrow in new[]{true,false})
        {
            try
            {
                var candidate=BuildCore(inv,original,hash,narrow,new(1,compact,wholeWords,keyBelow),palette,aggregate);
                ApplyGeometry(candidate,1,compact);
                double radius=markerRadius??DotRadius;
                if(!MarkersFit(candidate,radius))
                {
                    if(markerRadius.HasValue||!MarkersFit(candidate,MinimumDotRadius))continue;
                    double low=MinimumDotRadius,high=DotRadius;
                    for(int i=0;i<18;i++){double middle=(low+high)/2;if(MarkersFit(candidate,middle))low=middle;else high=middle;}
                    radius=low;
                }
                AddMarkers(candidate,inv,radius);
                // Each sheet supplies one shared safety footer; remove only this
                // duplicate footer, then retain the bounds of every visible primitive.
                candidate.Scene.Labels.RemoveAll(label=>label.Text==ReportText.CableMoveDisclaimer&&label.Y==PageHeight-10);
                candidate.Scene.Height=CompactBottom(candidate.Scene)+1;
                var errors=Charts.ValidateScene(candidate.Scene);
                if(errors.Count>0)throw new OnePageCapacityException(string.Join("; ",errors));
                candidates.Add(candidate);
            }
            catch(OnePageCapacityException ex){last=ex;}
        }
        // Prefer full-size markers, then the shortest complete readable component.
        // The standalone solver and its enlargement preference are unchanged.
        var best=candidates.OrderByDescending(candidate=>Math.Round(candidate.MarkerRadius,5))
            .ThenBy(candidate=>candidate.Scene.Height).ThenBy(candidate=>candidate.CompactionLevel).FirstOrDefault();
        if(best!=null)return best;
        throw last??new OnePageCapacityException("this configuration cannot fit at the workspace's consistent reference geometry; keep its individual report");
    }
    static double CompactBottom(Scene scene)
    {
        double bottom=0;
        foreach(var box in scene.Boxes)bottom=Math.Max(bottom,box.Y+box.H+.4);
        foreach(var label in scene.Labels)bottom=Math.Max(bottom,label.Y+ReportRenderer.Measure(label).Height);
        foreach(var dot in scene.Dots)bottom=Math.Max(bottom,dot.Y+dot.Radius+Math.Max(dot.Halo,dot.Voice?dot.VoiceExtra+.2:0));
        foreach(var shape in scene.Shapes)bottom=Math.Max(bottom,shape.Points.Max(point=>point.Y)+(shape.ClipInside?0:shape.StrokeWidth/2));
        foreach(var image in scene.Images)bottom=Math.Max(bottom,image.Y+image.H);
        return bottom;
    }
    static Result BuildCore(Inventory inv,List<Placement> original,string hash,bool compact,LayoutPlan plan,Dictionary<int,string> palette,Dictionary<string,string> colors)
    {
        var output=new Result();var scene=output.Scene;var groups=inv.Groups.ToDictionary(g=>g.Id);
        plan.TitleInkHeight=inv.Groups.Select(g=>ReportRenderer.Measure(new Label(0,0,PanelTitle(inv,g),plan.Font(7)*plan.Scale,"#ffffff",true,0,"",true)).Height/plan.Scale).DefaultIfEmpty(0).Max();
        // Reserve readable legend content before maximizing the physical bank.
        // Long names may wrap, but a large diagram cannot squeeze the key into
        // character-wide columns or split the numeric VLAN identity.
        var keyWords=palette.Keys.SelectMany(id=>(inv.Vlans.FirstOrDefault(v=>v.Id==id)?.Name??"").Split(' ',StringSplitOptions.RemoveEmptyEntries));
        double longestKeyWord=keyWords.Select(word=>Width(word,plan.Font(7.5),scale:plan.Scale)*plan.Scale).DefaultIfEmpty(0).Max();
        plan.KeyTextWidth=plan.PreferWholeWords?Math.Max(78,Math.Min(140,longestKeyWord)):78;
        foreach(var pair in palette)output.VlanColors.Add(pair.Key,pair.Value);
        scene.VlanColors=output.VlanColors;
        foreach(var pair in colors)output.AggregateColors.Add(pair.Key,pair.Value);
        var physical=Charts.PhysicalView(inv);var panels=new List<Panel>();double y=DiagramTop;
        double activePanelHeight=plan.PanelHeight(inv);
        bool modular=physical!=null&&!inv.FlexibleUplinks;double diagramWidth=plan.Width-(plan.KeyBelow?0:plan.KeyWidth+plan.Gutter);
        double BankWidth(Group g)
        {
            var xs=original.Where(p=>p.Group==g.Id).Select(p=>p.Tile.X).Distinct().Order().ToList();
            return (inv.OS=="RouterOS"?(xs[^1]-xs[0])/48+1:xs.Count)*plan.Pitch-plan.Gap+4;
        }
        if(compact&&!inv.FlexibleUplinks)diagramWidth=Math.Min(diagramWidth,Math.Max(plan.Compact==2?180/plan.Scale:180,(modular?2:1)*inv.Groups.Select(BankWidth).DefaultIfEmpty(180).Max()+(modular?8:0)));
        double BayWidth(IEnumerable<Group> gs)=>Math.Max(56,gs.Select(BankWidth).DefaultIfEmpty(56).Max());
        // Keep wide 3810 fixed banks and their actual flex bays on one row.
        // The sidebar yields width first; the printed sockets never shrink.
        if(inv.FlexibleUplinks)foreach(var device in inv.Groups.GroupBy(g=>g.Device))
        {
            var baseGroup=device.Single(g=>g.Slot.Length==0);int bays=baseGroup.Product=="JL075A"?2:1;
            diagramWidth=Math.Max(diagramWidth,BankWidth(baseGroup)+bays*(BayWidth(device.Where(g=>g.Slot.Length>0))+6));
        }
        if(!modular&&!inv.FlexibleUplinks)diagramWidth=Math.Max(diagramWidth,inv.Groups.Max(BankWidth));
        if(diagramWidth>plan.Width-(plan.KeyBelow?0:plan.Gutter+plan.KeyWidth)+.001)throw new OnePageCapacityException("physical banks leave insufficient room for a readable VLAN key");
        double keyX=plan.KeyBelow?0:diagramWidth+plan.Gutter;
        double keyWidth=plan.KeyBelow?diagramWidth:compact?Math.Min(plan.KeyWidth,plan.Width-keyX):plan.Width-keyX;
        if(modular)
        {
            var front=physical!.Bands.Where(b=>b.Text.EndsWith("FRONT VIEW")).OrderBy(b=>b.Y).ToList();
            for(int d=0;d<front.Count;d++)
            {
                double end=d+1<front.Count?front[d+1].Y:double.MaxValue;
                string member=front[d].Text.Split('|').Select(t=>t.Trim()).FirstOrDefault(t=>t.StartsWith("Member "))??"";
                if(member.Length>0)
                {
                    var group=inv.Groups.FirstOrDefault(g=>member=="Member "+g.Device);
                    string details=group==null?"":DeviceIdentities.Heading(inv,group.Device);
                    string memberModel=group==null?inv.ChassisModel:DeviceIdentities.For(inv,group.Device)?.Model??inv.ChassisModel;
                    scene.Text(0,y,member+" - "+memberModel+(details.Length>0?" | "+details:""),plan.Font(9),"#20374e",true,diagramWidth);y+=Height(plan.Font(9),plan.Scale)+1/plan.Scale;
                }
                var rows=physical.Frames.Where(f=>f.Y>front[d].Y&&f.Y<end).GroupBy(f=>f.Y).OrderBy(r=>r.Key).ToList();
                bool management=inv.ChassisModel.StartsWith("82")||inv.ChassisModel.Contains('R');
                if(management){scene.Text(0,y,"Management / system region",plan.Font(7),"#52677c",false,diagramWidth);y+=Height(plan.Font(7),plan.Scale)+.35/plan.Scale;}
                for(int r=0;r<rows.Count;r++)
                {
                    if(r==3&&inv.ChassisModel.StartsWith("8212")){scene.Text(0,y,"Fabric region",plan.Font(7),"#52677c",false,diagramWidth);y+=Height(plan.Font(7),plan.Scale)+.35/plan.Scale;}
                    double height=rows[r].Any(f=>f.Group.Length>0)?activePanelHeight:(plan.Compact==2?12/plan.Scale:12);
                    foreach(var f in rows[r])
                    {
                        int col=f.X<800?0:1;
                        panels.Add(new(f.Group,f.Group.Length>0?PanelTitle(inv,groups[f.Group]):f.Slot+" | Not declared",col*(diagramWidth+8)/2,y,(diagramWidth-8)/2,height));
                    }y+=height+plan.PanelGap;
                }
                if(inv.ChassisModel.StartsWith("8206")){scene.Text(0,y,"Fabric region",plan.Font(7),"#52677c",false,diagramWidth);y+=Height(plan.Font(7),plan.Scale)+.35/plan.Scale;}
                y+=1;
            }
        }
        else if(inv.FlexibleUplinks)
        {
            foreach(var device in inv.Groups.GroupBy(g=>g.Device))
            {
                var baseGroup=device.Single(g=>g.Slot.Length==0);var flex=device.Where(g=>g.Slot.Length>0).ToList();
                int bays=baseGroup.Product=="JL075A"?2:1;double bayW=BayWidth(flex),baseW=diagramWidth-bays*(bayW+6);
                double height=activePanelHeight;
                panels.Add(new(baseGroup.Id,PanelTitle(inv,baseGroup),0,y,baseW,height));
                for(int b=0;b<bays;b++)
                {
                    string slot=((char)('A'+b)).ToString();var g=flex.FirstOrDefault(g=>g.Slot==slot);
                    panels.Add(new(g?.Id??"",g==null?"Uplink "+slot+" | Not declared":PanelTitle(inv,g),baseW+6+b*(bayW+6),y,bayW,height));
                }y+=height+plan.PanelGap;
            }
        }
        else
        {
            foreach(var g in inv.Groups){panels.Add(new(g.Id,PanelTitle(inv,g),0,y,diagramWidth,activePanelHeight));y+=activePanelHeight+plan.PanelGap;}
        }
        double diagramBottom=y;
        foreach(var panel in panels)
        {
            scene.Rect(panel.X,panel.Y,panel.W,panel.H,panel.Group.Length==0?"#f7f9fc":"#ffffff","#355774",3,panel.Group.Length==0?"InactivePanel":"ActivePanel");
            if(panel.Group.Length>0)scene.Rect(panel.X+plan.HeaderInset,panel.Y+plan.HeaderInset,panel.W-2*plan.HeaderInset,plan.Header-plan.HeaderInset,"#17324d","#17324d",2,"Heading");
            if(panel.Group.Length==0){scene.Text(panel.X+3,panel.Y+1,panel.Title,plan.Font(8),"#52677c",false,panel.W-6);continue;}
            var g=groups[panel.Group];
            var titleLines=Wrap(panel.Title,panel.W-6,plan.Font(7),plan.Scale);
            if(titleLines.Count>1)
            {
                // Preserve entered identity ahead of model/media on compact headings.
                string shortTitle=string.Join(" | ",new[]{ReportText.GroupLabel(inv,g),DeviceModels.GroupModel(inv,g)}.Where(s=>s.Length>0));
                scene.Text(panel.X+3,panel.Y+plan.TitleTop,shortTitle,plan.Font(7),"#ffffff",true,panel.W-6,inkBox:true);
            }
            else scene.Text(panel.X+3,panel.Y+plan.TitleTop,panel.Title,plan.Font(7),"#ffffff",true,panel.W-6,inkBox:true);
            var pp=original.Where(p=>p.Group==g.Id).ToList();
            var columnXs=pp.Select(p=>p.Tile.X).Distinct().Order().ToList();
            double width=(inv.OS=="RouterOS"?(columnXs[^1]-columnXs[0])/48:columnXs.Count-1)*plan.Pitch+PortWidth;
            double blank=g.BlankLeft?Math.Min(110,Math.Max(40,panel.W-width-15)):0;
            if(width+blank>panel.W-3)throw new OnePageCapacityException("module socket inventory exceeds fixed-scale panel width");
            double x0=panel.X+(panel.W-width-blank)/2+blank;
            if(blank>0)scene.Rect(x0-blank,panel.Y+plan.SocketTop,blank-3,panel.H-plan.SocketTop-plan.PanelBottom,"#e3e8ed","#c6d2dc");
            var rowYs=pp.Select(p=>p.Tile.Y).Distinct().Order().ToList();
            double rowPitch=PortHeight+plan.Gap;
            double bankHeight=(rowYs.Count-1)*rowPitch+PortHeight;
            double bankTop=panel.Y+plan.SocketTop+(panel.H-plan.SocketTop-plan.PanelBottom-bankHeight)/2;
            foreach(var item in pp)
            {
                var port=inv.Ports.Single(p=>p.Id==item.Port);double x=x0+(inv.OS=="RouterOS"?(item.Tile.X-columnXs[0])/48:columnXs.IndexOf(item.Tile.X))*plan.Pitch;
                double yy=bankTop+rowYs.IndexOf(item.Tile.Y)*rowPitch;
                DrawSocket(output,inv,g,port,item,x,yy,colors,plan);
            }
        }
        var vlans=output.VlanColors.Keys.Order().ToList();
        var sections=new List<Section>();
        var aggregates=new Section{Title="Trunks / LAGs",Columns=3};
        foreach(var a in colors)
        {
            var ps=inv.Ports.Where(p=>p.Aggregate.Equals(a.Key,StringComparison.OrdinalIgnoreCase)).ToList();
            string mode=string.Join("/",ps.SelectMany(p=>p.Notes).Where(n=>n.StartsWith("Aggregation mode: ")).Select(n=>n[18..].ToUpperInvariant()).Distinct());
            aggregates.OutlineColors.Add(a.Value);
            aggregates.Entries.Add(a.Key+(mode.Length>0?" ("+mode+")":"")+": "+Ports(ps));
        }sections.Add(aggregates);
        var purposes=new Section{Title="Port descriptions",Columns=3};
        foreach(var d in inv.Ports.Where(p=>p.Description.Length>0).GroupBy(p=>p.Description))purposes.Entries.Add(Ports(d)+": "+d.Key);
        sections.Add(purposes);
        int keyColumns=plan.KeyBelow?Math.Max(1,Math.Min(5,(int)Math.Floor((keyWidth+7/plan.Scale)/(plan.KeyWidth+7/plan.Scale)))):1;
        double keyTop=plan.KeyBelow?diagramBottom+2/plan.Scale:DiagramTop;
        double keyBottom=DrawVlanKey(scene,inv,vlans,keyX,keyTop,keyWidth,keyColumns,true,output.VlanColors,plan);
        if(keyBottom>plan.Bottom)throw new OnePageCapacityException("VLAN key exceeds the available A4 height");
        double infoY=plan.KeyBelow?keyBottom+2/plan.Scale:diagramBottom+(plan.Compact==0?2:1/plan.Scale),font=plan.Font(BodyFont);
        double Required(double f){double next=infoY;foreach(var section in sections)next=DrawSection(scene,section,next,diagramWidth,f,false,plan);return next;}
        if(!plan.KeyBelow&&(modular||Required(font)>plan.Bottom)&&aggregates.Entries.Count>0)
        {
            aggregates.Columns=1;
            double sideBottom=DrawSection(scene,aggregates,keyBottom+3,keyWidth,font,false,plan,keyX);
            if(sideBottom<=plan.Bottom)
            {
                DrawSection(scene,aggregates,keyBottom+3,keyWidth,font,true,plan,keyX);
                sections.Remove(aggregates);
            }
            else aggregates.Columns=3;
        }
        if(Required(font)>plan.Bottom)font=plan.Font(7);
        if(Required(font)>plan.Bottom)throw new OnePageCapacityException("VLAN / trunk / purpose key needs more space at seven-point text");
        foreach(var section in sections)infoY=DrawSection(scene,section,infoY,diagramWidth,font,true,plan);
        string model=ReportText.SwitchModel(inv);
        var chassis=inv.Devices.Where(d=>d.Kind=="Chassis").ToList();
        if(chassis.Count==1)
        {
            var identity=chassis[0];
            string details=identity.SerialNumber.Length>0?"S/N "+identity.SerialNumber:identity.AssetTag.Length>0?"Asset "+identity.AssetTag:"";
            if(details.Length>0)model+=" | "+details;
        }
        scene.Rect(0,0,PageWidth,37,"#13283d","#13283d",4);
        scene.Rect(0,35,PageWidth,2,"#4ca6ce","#4ca6ce");
        scene.Text(12,5,"NETWORK PORT MAP",8.5,"#d5e7f4",true,390);
        scene.Text(12,17,ReportText.DisplayName(inv),14,"#ffffff",true,390);
        if(model.Length>0)scene.Text(420,7,model,9,"#ffffff",true,PageWidth-432);
        scene.Text(420,model.Length>0?22:14,(inv.Firmware=="Unknown"?"":inv.Firmware+"  |  ")+inv.Ports.Count+" interfaces",8,"#d5e7f4",false,PageWidth-432);
        DrawVisualGuide(scene,inv,output.VlanColors,colors);
        scene.Text(0,PageHeight-10,ReportText.CableMoveDisclaimer,7,"#52677c",false,PageWidth);
        scene.DiagramBodyWidth=keyX+keyWidth;
        return output;
    }
    static void ApplyGeometry(Result result,double scale,int compact)
    {
        var scene=result.Scene;double dx=Math.Max(0,(PageWidth-scene.DiagramBodyWidth*scale)/2);
        bool Body(double y)=>y>=DiagramTop-.01&&y<PageHeight-12;
        double X(double x)=>dx+x*scale;double Y(double y)=>DiagramTop+(y-DiagramTop)*scale;
        for(int i=0;i<scene.Boxes.Count;i++){var b=scene.Boxes[i];if(Body(b.Y))scene.Boxes[i]=b with{X=X(b.X),Y=Y(b.Y),W=b.W*scale,H=b.H*scale,Radius=b.Radius*scale};}
        for(int i=0;i<scene.Labels.Count;i++){var l=scene.Labels[i];if(Body(l.Y))scene.Labels[i]=l with{X=X(l.X),Y=Y(l.Y),Size=l.Size*scale,Width=l.Width*scale};}
        for(int i=0;i<scene.Shapes.Count;i++){var p=scene.Shapes[i];if(Body(p.Points.Min(v=>v.Y)))scene.Shapes[i]=p with{Points=p.Points.Select(v=>(X(v.X),Y(v.Y))).ToArray(),StrokeWidth=p.StrokeWidth*scale};}
        for(int i=0;i<scene.Dots.Count;i++){var d=scene.Dots[i];if(Body(d.Y))scene.Dots[i]=d with{X=X(d.X),Y=Y(d.Y),Radius=DotRadius,Halo=MarkerHalo,Outline=MarkerStroke,VoiceExtra=0};}
        Box Box(Box b)=>b with{X=X(b.X),Y=Y(b.Y),W=b.W*scale,H=b.H*scale};
        for(int i=0;i<result.Sockets.Count;i++)
        {
            var p=result.Sockets[i];result.Sockets[i]=p with{Tile=Box(p.Tile),NumberBounds=Box(p.NumberBounds),FacePoints=p.FacePoints.Select(v=>(X(v.X),Y(v.Y))).ToArray(),MarkerTop=Y(p.MarkerTop),MarkerBottom=Y(p.MarkerBottom),MarkerInset=p.MarkerInset*scale};
        }
        scene.DiagramScale=scale;scene.MarkerRadius=DotRadius;scene.CompactionLevel=compact;scene.IsAdaptiveLayout=true;
    }
    public static void FitToPage(Scene scene)
    {
        if(scene.IsAdaptiveLayout)return;
        bool Body(double y)=>y>=DiagramTop-0.01&&y<PageHeight-12;
        var xs=new List<double>();var ys=new List<double>();
        foreach(var b in scene.Boxes.Where(b=>Body(b.Y))){xs.Add(b.X+b.W+.4);ys.Add(b.Y+b.H+.4);}
        foreach(var l in scene.Labels.Where(l=>Body(l.Y))){var m=ReportRenderer.Measure(l);xs.Add(l.X+m.Width);ys.Add(l.Y+m.Height);}
        foreach(var d in scene.Dots.Where(d=>Body(d.Y))){xs.Add(d.X+d.Radius+d.Halo);ys.Add(d.Y+d.Radius+d.Halo);}
        foreach(var shape in scene.Shapes.Where(s=>Body(s.Points.Min(p=>p.Y)))){xs.Add(shape.Points.Max(p=>p.X)+.4);ys.Add(shape.Points.Max(p=>p.Y)+.4);}
        if(xs.Count==0)return;
        double width=xs.Max(),height=ys.Max()-DiagramTop;
        double scale=Math.Max(1,Math.Min((PageWidth-2)/width,(PageHeight-14-DiagramTop)/height));
        scene.DiagramScale=scale;double dx=Math.Max(0,(PageWidth-width*scale)/2);
        double X(double x)=>dx+x*scale;double Y(double y)=>DiagramTop+(y-DiagramTop)*scale;
        for(int i=0;i<scene.Boxes.Count;i++){var b=scene.Boxes[i];if(Body(b.Y))scene.Boxes[i]=b with{X=X(b.X),Y=Y(b.Y),W=b.W*scale,H=b.H*scale,Radius=b.Radius*scale};}
        for(int i=0;i<scene.Labels.Count;i++){var l=scene.Labels[i];if(Body(l.Y))scene.Labels[i]=l with{X=X(l.X),Y=Y(l.Y),Size=l.Size*scale,Width=l.Width*scale};}
        for(int i=0;i<scene.Dots.Count;i++){var d=scene.Dots[i];if(Body(d.Y))scene.Dots[i]=d with{X=X(d.X),Y=Y(d.Y),Radius=d.Radius*scale,Halo=d.Halo*scale,Outline=d.Outline*scale,VoiceExtra=d.VoiceExtra*scale};}
        for(int i=0;i<scene.Shapes.Count;i++){var shape=scene.Shapes[i];if(Body(shape.Points.Min(p=>p.Y)))scene.Shapes[i]=shape with{Points=shape.Points.Select(p=>(X(p.X),Y(p.Y))).ToArray(),StrokeWidth=shape.StrokeWidth*scale};}
    }
    static void DrawVisualGuide(Scene scene,Inventory inv,IReadOnlyDictionary<int,string> palette,IReadOnlyDictionary<string,string> aggregates)
    {
        double x=0,y=39;
        string fill=palette.GetValueOrDefault(10,palette.Values.FirstOrDefault()??"#b9cfdf");
        string voice=palette.Where(p=>IsVoice(inv,p.Key)).Select(p=>p.Value).FirstOrDefault()??fill;
        void Item(string text,double symbolWidth,Action<double> symbol)
        {
            symbol(x);scene.Text(x+symbolWidth+4,y,text,7,"#30465c",false);x+=symbolWidth+4+Width(text,7)+17;
        }
        Item("Native VLAN",6,xx=>scene.Rect(xx,y+1,6,6,fill,"#182536"));
        Item("Tagged VLAN",6,xx=>scene.Dots.Add(new(xx+3,y+4,DotRadius,fill,false,0,MarkerHalo,MarkerStroke)));
        Item("Voice VLAN",6,xx=>scene.Dots.Add(new(xx+3,y+4,DotRadius,voice,true,0,MarkerHalo,MarkerStroke)));
        Item("LAG / trunk",10,xx=>AggregateKey(scene,xx,y+1,10,6,aggregates.Values.FirstOrDefault()??"#486ba4"));
        if(inv.Ports.Any(p=>p.StackLink))Item("Stack / VSF",6,xx=>scene.Rect(xx,y+1,6,6,"#000000","#182536"));
        if(inv.Ports.Any(p=>p.Disabled==true))Item("Disabled",6,xx=>scene.Text(xx+1,y,"!",7.5,"#182536",true));
    }
    static double DrawVlanKey(Scene scene,Inventory inv,List<int> vlans,double x,double y,double width,int columns,bool draw,IReadOnlyDictionary<int,string> palette,LayoutPlan plan)
    {
        if(Width("VLAN key",plan.Font(8.5,7.5),true,plan.Scale)>width-10)throw new OnePageCapacityException("VLAN key heading cannot fit without truncation");
        if(draw){scene.Rect(x,y,width,plan.HeadingHeight,"#e7eff6","#c3d2df",2,"Heading");scene.Text(x+5,y+1/plan.Scale,"VLAN key",plan.Font(8.5,7.5),"#20374e",true,width-10,inkBox:true);}y+=plan.AfterHeading;
        double gap=7/plan.Scale,colWidth=(width-gap*(columns-1))/columns;
        if((colWidth-plan.KeyTextInset)*plan.Scale<78-.01)throw new OnePageCapacityException("VLAN key text column is too narrow");
        foreach(var row in vlans.Chunk(columns))
        {
            if(row.Any(id=>Width(id+(IsVoice(inv,id)?" Voice":""),plan.Font(7.5),scale:plan.Scale)>colWidth-plan.KeyTextInset))throw new OnePageCapacityException("VLAN key must retain each complete numeric identity");
            var texts=row.Select(id=>(id+(IsVoice(inv,id)?" Voice":"")+" "+(inv.Vlans.FirstOrDefault(v=>v.Id==id)?.Name??"")).TrimEnd()).ToList();
            var lines=texts.Select(t=>Wrap(t,colWidth-plan.KeyTextInset,plan.Font(7.5),plan.Scale)).ToList();
            if(draw)for(int col=0;col<row.Length;col++)
            {
                double xx=x+col*(colWidth+gap);int id=row[col];scene.Rect(xx,y+1,6,6,palette[id],"#687b8b");
                scene.Dots.Add(new(xx+plan.KeyCircleX,y+4,DotRadius,palette[id],IsVoice(inv,id),0,MarkerHalo,MarkerStroke));
                for(int n=0;n<lines[col].Count;n++)scene.Text(xx+plan.KeyTextInset,y+n*(Height(plan.Font(7.5),plan.Scale)+.2/plan.Scale),lines[col][n],plan.Font(7.5),"#30465c",false,colWidth-plan.KeyTextInset);
            }
            y+=Math.Max(plan.Compact==0?8:7,lines.Max(l=>l.Count)*(Height(plan.Font(7.5),plan.Scale)+.2/plan.Scale))+(plan.Compact==0?.6:.35/plan.Scale);
        }
        foreach(var status in new[]{(State:"Unknown",Fill:"#e6e6e6",Text:"Native not supplied"),(State:"None",Fill:"#ffffff",Text:"No native VLAN")})
        {
            if(!inv.Ports.Any(p=>!p.StackLink&&!p.UntaggedVlan.HasValue&&p.UntaggedState==status.State))continue;
            var lines=Wrap(status.Text,width-plan.KeyTextInset,plan.Font(7),plan.Scale);
            if(draw){scene.Rect(x,y+1,6,6,status.Fill,"#687b8b");for(int n=0;n<lines.Count;n++)scene.Text(x+plan.KeyTextInset,y+n*(Height(plan.Font(7),plan.Scale)+.2/plan.Scale),lines[n],plan.Font(7),"#52677c",false,width-plan.KeyTextInset);}
            y+=Math.Max(plan.Compact==0?10:7.25,lines.Count*(Height(plan.Font(7),plan.Scale)+.2/plan.Scale)+.6);
        }
        return y+1;
    }
}
