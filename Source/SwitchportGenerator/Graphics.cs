using System.Globalization;
using System.IO.Compression;
using System.Text;
namespace SwitchportGenerator;
public record Box(double X,double Y,double W,double H,string Fill,string Stroke="#24354a",double Radius=0,string Role="");
public record Dot(double X,double Y,double Radius,string Fill,bool Voice=false,double VoiceExtra=0.8,double Halo=0.7,double Outline=0.35);
public record Shape((double X,double Y)[] Points,string Fill,string Stroke,double StrokeWidth,string Owner="",bool ClipInside=false,Box? ClipBounds=null);
public record Label(double X,double Y,string Text,double Size,string Color="#172333",bool Bold=false,double Width=0,string Owner="",bool InkBox=false);
public record Raster(double X,double Y,double W,double H,byte[] Data);
public sealed class Scene
{
    public IReadOnlyDictionary<int,string>? VlanColors {get;set;}
    public double DiagramScale {get;set;}=1;
    public double MarkerRadius {get;set;}=QuickSheet.DotRadius;
    public int CompactionLevel {get;set;}
    public bool IsAdaptiveLayout {get;set;}
    public double DiagramBodyWidth {get;set;}
    public double Width {get;set;}=1720;
    public double Height {get;set;}
    public List<Box> Boxes {get;}=new();
    public List<Label> Labels {get;}=new();
    public List<Dot> Dots {get;}=new();
    public List<Shape> Shapes {get;}=new();
    public List<Raster> Images {get;}=new();
    public void Rect(double x,double y,double w,double h,string fill,string stroke="#24354a",double radius=0,string role="")=>Boxes.Add(new(x,y,w,h,fill,stroke,radius,role));
    public void Text(double x,double y,string text,double size=14,string color="#172333",bool bold=false,double width=0,string owner="",bool inkBox=false)=>Labels.Add(new(x,y,Parser.Safe(text),size,color,bold,width,owner,inkBox));
}
public record Placement(string Port,string Group,Box Tile,Box Marker,string Socket="");
public static class Charts
{
    public static readonly Dictionary<int,string> Colors=new(){[1]="#b6b6b6",[10]="#4a90e2",[110]="#f39c38",[120]="#65b96e",[140]="#ad795a",[150]="#f1d64b",[160]="#b69bdd",[254]="#e76a68",[300]="#52b9b0",[500]="#ec9cc0",[600]="#76508f"};
    public static string VlanColor(int id)
    {
        if(Colors.TryGetValue(id,out string? color))return color;
        double h=((id*137.508)%360)/60, c=0.57, x=c*(1-Math.Abs(h%2-1)),m=0.24;
        (double r,double g,double b)=h<1?(c,x,0.0):h<2?(x,c,0.0):h<3?(0.0,c,x):h<4?(0.0,x,c):h<5?(x,0.0,c):(c,0.0,x);
        return $"#{(int)Math.Round((r+m)*255):x2}{(int)Math.Round((g+m)*255):x2}{(int)Math.Round((b+m)*255):x2}";
    }
    public static string Color(Port p)=>p.StackLink?"#000000":p.UntaggedState=="None"?"#ffffff":p.UntaggedVlan.HasValue?VlanColor(p.UntaggedVlan.Value):"#e6e6e6";
    static string PortTextColor(Port port)
    {
        var c=SkiaSharp.SKColor.Parse(Color(port));
        return .2126*c.Red+.7152*c.Green+.0722*c.Blue<138?"#ffffff":"#101820";
    }
    public static Dictionary<string,string> AggregateColors(Inventory inv)
    {
        return AggregatePalette.Assign(inv);
    }
    static List<string> MemberLines(IEnumerable<Port> ports)
    {
        var lines=new List<string>();string line="";
        foreach(var p in ports)
        {
            string candidate=line.Length==0?p.Id:line+", "+p.Id;
            if(line.Length>0&&ReportRenderer.Measure(new Label(0,0,candidate,10)).Width>476){lines.Add(line);line=p.Id;}else line=candidate;
        }
        if(line.Length>0)lines.Add(line);return lines;
    }
    static List<int> LegendVlans(Inventory inv)=>inv.Vlans.Select(v=>v.Id).Concat(inv.Ports.SelectMany(p=>p.TaggedVlans.Concat(p.UntaggedVlan.HasValue?new[]{p.UntaggedVlan.Value}:Array.Empty<int>()))).Distinct().Order().ToList();
    static List<int> PortDots(Inventory inv,Port port)=>port.TaggedVlans.Concat(port.VoiceVlan.HasValue?new[]{port.VoiceVlan.Value}:Array.Empty<int>()).Concat(port.UntaggedVlan.HasValue&&inv.Vlans.Any(v=>v.Id==port.UntaggedVlan.Value&&v.Voice)?new[]{port.UntaggedVlan.Value}:Array.Empty<int>()).Distinct().Order().ToList();
    static int MarkerHeight(Inventory inv,Group g)=>Math.Max(12,inv.Ports.Where(p=>p.Group==g.Id).Select(p=>(int)Math.Ceiling(PortDots(inv,p).Count/6.0)*7+2+((p.Disabled==true||p.Notes.Any(n=>n.StartsWith("Tagged VLANs:")))?10:0)).DefaultIfEmpty(12).Max());
    static int BlankWidth(Inventory inv,Group g)=>inv.FlexibleUplinks||g.Product=="J9573A"?576:192;
    static int PortHeight(Inventory inv,Group g)=>Math.Max(44,32+MarkerHeight(inv,g));
    static int PortTop(Inventory inv,Group g)=>inv.Topology=="Modular"&&!inv.FlexibleUplinks?(g.Product=="J4907A"?56:38):52;
    static int RowPitch(Inventory inv,Group g)=>PortHeight(inv,g)+8;
    static int GroupPitch(Inventory inv,Group g)=>Math.Max(inv.Topology=="Modular"&&!inv.FlexibleUplinks?(g.OpticalOnly?104:164):(g.OpticalOnly?148:196),PortTop(inv,g)+(g.SocketLayout.Count>0?g.SocketLayout.Max(s=>s.Row)+1:g.OpticalOnly?1:2)*RowPitch(inv,g)+(inv.Topology=="Modular"&&!inv.FlexibleUplinks?20:32));
    static double GroupBase(Inventory inv,int index)=>178+inv.Groups.Take(index).Sum(g=>GroupPitch(inv,g));
    static double PortFont(string id)
    {
        double size=18;while(size>5&&ReportRenderer.Measure(new Label(0,0,id,size)).Width>38)size-=0.25;
        return size;
    }
    public static HardwareView? PhysicalView(Inventory inv)
    {
        if(inv.Topology!="Modular"&&!inv.FlexibleUplinks)return null;
        var view=new HardwareView();double y=132;
        foreach(var device in inv.Groups.GroupBy(g=>g.Device).OrderBy(d=>d.Key.Length).ThenBy(d=>d.Key))
        {
            var groups=device.ToList();
            view.Bands.Add(new((inv.ChassisModel.Length>0?inv.ChassisModel:"Chassis unconfirmed")+" | "+(inv.Groups.Select(g=>g.Device).Distinct().Count()>1?"Member "+device.Key+" | ":"")+"FRONT VIEW",24,y,1672,34));y+=46;
            if(inv.FlexibleUplinks)
            {
                var fixedGroup=groups.FirstOrDefault(g=>g.Slot.Length==0);int slots=fixedGroup?.Product=="JL075A"?2:1;
                double pitch=groups.Select(g=>GroupPitch(inv,g)).DefaultIfEmpty(160).Max();
                double fixedWidth=slots==2?826:1226;
                if(fixedGroup!=null)view.Frames.Add(new(fixedGroup.Id,"Fixed ports",40,y,fixedWidth,pitch-16));
                for(int col=0;col<slots;col++)
                {
                    string slot=((char)('A'+col)).ToString();var g=groups.FirstOrDefault(g=>g.Slot==slot);
                    view.Frames.Add(new(g?.Id??"","Uplink "+slot,slots==2?886+400*col:1286,y,380,pitch-16));
                }
                y+=pitch;
            }
            else
            {
                if(inv.ChassisModel.StartsWith("82"))
                {view.Bands.Add(new("Management / system support region (structural reference)",40,y,1626,40));y+=52;}
                else if(inv.ChassisModel.Contains('R'))
                {view.Bands.Add(new("Management module region (structural reference)",40,y,1626,28));y+=40;}
                int slots=inv.ChassisSlots>0?inv.ChassisSlots:Math.Max(2,((groups.Select(g=>g.Slot.Length==1?g.Slot[0]-'A':0).DefaultIfEmpty(0).Max()/2)+1)*2);
                for(int row=0;row<slots/2;row++)
                {
                    if(row==3&&inv.ChassisModel.StartsWith("8212"))
                    {view.Bands.Add(new("Fabric module region (structural reference)",40,y,1626,36));y+=48;}
                    var rowGroups=groups.Where(g=>g.Slot.Length==1&&(g.Slot[0]-'A')/2==row).ToList();
                    double pitch=rowGroups.Select(g=>GroupPitch(inv,g)).DefaultIfEmpty(96).Max();
                    for(int col=0;col<2;col++)
                    {
                        string slot=((char)('A'+row*2+col)).ToString();var g=groups.FirstOrDefault(g=>g.Slot==slot);
                        view.Frames.Add(new(g?.Id??"","Slot "+slot,40+826*col,y,800,pitch-16));
                    }
                    y+=pitch;
                }
                if(inv.ChassisModel.StartsWith("8206")){view.Bands.Add(new("Fabric module region (structural reference)",40,y,1626,36));y+=48;}
            }
            y+=12;
        }
        view.Height=y-132;return view;
    }
    static double ContentHeight(Inventory inv)=>PhysicalView(inv)?.Height??inv.Groups.Sum(g=>GroupPitch(inv,g));
    public static List<Placement> Layout(Inventory inv)
    {
        var result=new List<Placement>();int index=0;var physical=PhysicalView(inv);
        foreach(var g in inv.Groups)
        {
            var frame=physical?.Frames.FirstOrDefault(f=>f.Group==g.Id);
            double y=frame?.Y+PortTop(inv,g)??GroupBase(inv,index++);
            if(g.SocketLayout.Count>0)
            {
                double span=(g.SocketLayout.Max(s=>s.Column)+1)*48;
                double start=frame==null?(1720-span+4)/2:frame.X+(frame.W-span+4)/2;
                foreach(var socket in g.SocketLayout)
                {
                    var port=inv.Ports.Single(p=>p.Group==g.Id&&p.Number==socket.Number);
                    var box=new Box(start+socket.Column*48,y+socket.Row*RowPitch(inv,g),44,PortHeight(inv,g),Color(port));
                    result.Add(new(port.Id,g.Id,box,new Box(box.X,box.Y+27,44,MarkerHeight(inv,g),"#ffffff"),socket.Suffix));
                }
                continue;
            }
            int copperCols=g.OpticalOnly?g.Copper:(g.Copper+1)/2,opticalCols=g.OpticalOnly?g.Optical:(g.Optical+1)/2;
            double blank=g.BlankLeft?BlankWidth(inv,g):0,gap=g.Copper>0&&g.Optical>0?48:0;
            double width=g.Product=="J4907A"?11*48+24:blank+copperCols*48+opticalCols*48+gap;
            double x=frame==null?(1720-width+4)/2:frame.X+(frame.W-width+4)/2;
            double copperX=x+blank+(g.OpticalSide=="left"?opticalCols*48+gap:0);
            double opticalX=g.OpticalSide=="left"?x:x+blank+copperCols*48+gap;
            foreach(var p in inv.Ports.Where(p=>p.Group==g.Id))
            {
                int number=p.Number<=g.Copper?p.Number:p.Number-g.Copper;
                bool optical=p.Number>g.Copper;
                int col=g.OpticalOnly?number-1:(number-1)/2,row=g.OpticalOnly?0:(number-1)%2;
                if(g.Product=="J4820A"){col=(number-1)/12*6+(number-1)%6;row=(number-1)%12/6;}
                if(g.Product=="J4907A")
                {
                    col=number<=14?(number-1)%7:7+(number-15)*2;row=number<=14?(number-1)/7:1;
                }
                double px=g.Product=="J4907A"?x+col*48+(number>14?24:0):(optical?opticalX:copperX)+col*48;
                var tile=new Box(px,y+row*RowPitch(inv,g),44,PortHeight(inv,g),Color(p));
                result.Add(new(p.Id,g.Id,tile,new Box(tile.X,tile.Y+27,44,MarkerHeight(inv,g),"#ffffff"),g.Product=="J4907A"&&number>14?"T":""));
                if(g.Product=="J4907A"&&number>14)
                {
                    var alternative=tile with {X=tile.X+48};
                    result.Add(new(p.Id,g.Id,alternative,new Box(alternative.X,alternative.Y+27,44,MarkerHeight(inv,g),"#ffffff"),"M"));
                }
            }
        }return result;
    }
    public static Scene Create(Inventory inv,List<Placement> places,string hash)=>QuickSheet.Build(inv,places,hash).Scene;
    public static Scene GeometryReferenceScene(Inventory inv,List<Placement> places,string hash)
    {
        var aggregateColors=AggregateColors(inv);
        var aggregateKeys=aggregateColors.Select(a=>(Id:a.Key,Color:a.Value,Ports:inv.Ports.Where(p=>p.Aggregate.Equals(a.Key,StringComparison.OrdinalIgnoreCase)).ToList())).ToList();
        var aggregateRows=aggregateKeys.Chunk(3).Select(row=>(Entries:row,Height:30+row.Max(a=>MemberLines(a.Ports).Count)*14)).ToList();
        double aggregateHeight=aggregateRows.Count==0?0:64+aggregateRows.Sum(row=>row.Height);
        var physical=PhysicalView(inv);
        var s=new Scene{Height=298+ContentHeight(inv)+Math.Ceiling((LegendVlans(inv).Count+3)/8.0)*36+aggregateHeight};
        s.Rect(0,0,1720,112,"#13283d","#13283d");
        s.Text(36,20,"Network port map",27,"#ffffff",true);
        s.Text(36,65,ReportText.DisplayName(inv)+"  |  "+inv.OS+"  |  "+inv.Firmware+"  |  "+inv.Topology,18,"#dce8f3",false,1648);
        if(physical!=null)
        {
            foreach(var band in physical.Bands){s.Rect(band.X,band.Y,band.W,band.H,"#e5ecf3","#9caebb",5);s.Text(band.X+14,band.Y+9,band.Text,12,"#30465c",true,band.W-28);}
            foreach(var empty in physical.Frames.Where(f=>f.Group.Length==0))
            {
                s.Rect(empty.X,empty.Y,empty.W,empty.H,"#edf0f3","#aebcc8",5);
                s.Text(empty.X+16,empty.Y+12,empty.Slot,15,"#52677c",true,empty.W-32);
            }
        }
        int index=0;
        foreach(var g in inv.Groups)
        {
            var frame=physical?.Frames.FirstOrDefault(f=>f.Group==g.Id);
            double y=frame?.Y??GroupBase(inv,index++)-52;double panelX=frame?.X??24,panelWidth=frame?.W??1672;
            s.Rect(panelX,y,panelWidth,frame?.H??GroupPitch(inv,g)-32,"#f6f8fb","#dce4ec",8);
            s.Text(panelX+16,y+12,string.Join(" | ",new[]{frame?.Slot??g.Id,ReportText.ModelName(g.Product),g.Product=="J4907A"?"14 RJ-45 + 2 RJ-45/SFP combo pairs":g.Copper==0?g.Optical+" "+g.OpticalType:g.Optical==0?g.Copper+" RJ-45":g.Copper+" RJ-45 + "+g.Optical+" "+g.OpticalType}.Where(value=>value.Length>0)),15,"#20374e",true,panelWidth-32);
            var pp=places.Where(p=>p.Group==g.Id).ToList();
            if(g.BlankLeft&&pp.Count>0)s.Rect(pp.Min(p=>p.Tile.X)-BlankWidth(inv,g),y+PortTop(inv,g),BlankWidth(inv,g)-16,(g.OpticalOnly?0:RowPitch(inv,g))+PortHeight(inv,g),"#dee2e7");
            foreach(var p in pp)
            {
                var port=inv.Ports.Single(q=>q.Id==p.Port);
                double x=p.Tile.X,y0=p.Tile.Y,h=p.Tile.H;bool optical=p.Socket=="M"||port.Media!="RJ-45";
                // Stepped RJ-45 socket and chamfered optical cage silhouettes.
                (double X,double Y)[] outline=optical?
                    new[]{(x+4,y0),(x+40,y0),(x+44,y0+4),(x+44,y0+h-4),(x+40,y0+h),(x+4,y0+h),(x,y0+h-4),(x,y0+4)}:
                    new[]{(x,y0),(x+44,y0),(x+44,y0+h-5),(x+32,y0+h-5),(x+32,y0+h),(x+12,y0+h),(x+12,y0+h-5),(x,y0+h-5)};
                s.Shapes.Add(new(outline,Color(port),port.Aggregate.Length>0?aggregateColors[port.Aggregate]:"#172333",port.Aggregate.Length>0?3.2:0.8,port.Id));
                s.Text(x+3,y0+2,port.Id+p.Socket,PortFont(port.Id+p.Socket),PortTextColor(port),false,38,port.Id);
                int dot=0;
                foreach(int vlan in PortDots(inv,port))
                {
                    s.Dots.Add(new(p.Marker.X+4.5+(dot%6)*7,p.Marker.Y+3.5+(dot/6)*7,2.3,VlanColor(vlan),port.VoiceVlan==vlan||inv.Vlans.Any(v=>v.Id==vlan&&v.Voice)));dot++;
                }
                double flagY=p.Marker.Y+Math.Ceiling(dot/6.0)*7+2;
                string flags=(port.Disabled==true?"D ":"")+(port.Notes.Any(n=>n.StartsWith("Tagged VLANs:"))?"ALL":"");
                if(flags.Length>0)s.Text(p.Marker.X+1,flagY,flags.Trim(),6.5,PortTextColor(port),false,42,port.Id);

            }
        }
        double legendY=168+ContentHeight(inv);int n=0;
        s.Rect(24,legendY-30,1672,Math.Ceiling((LegendVlans(inv).Count+3)/8.0)*36+44,"#f8fafc","#dce4ec",8);
        s.Text(42,legendY-21,"VLAN key   /   fill = native VLAN   /   circles inside port = tagged VLANs   /   diamond = voice VLAN",12,"#34516b",true);
        foreach(var pair in LegendVlans(inv).Select(id=>new KeyValuePair<int,string>(id,VlanColor(id))).Concat(new[]{new KeyValuePair<int,string>(-1,"#e6e6e6"),new KeyValuePair<int,string>(-2,"#ffffff"),new KeyValuePair<int,string>(-3,"#000000")}))
        {
            double x=36+(n%8)*208,y=legendY+(n/8)*36;s.Rect(x,y,22,16,pair.Value);
            if(pair.Key>0)s.Dots.Add(new(x+32,y+8,3.4,pair.Value,inv.Vlans.Any(v=>v.Id==pair.Key&&v.Voice)));
            s.Text(x+44,y+1,pair.Key==-1?"Unknown":pair.Key==-2?"None":pair.Key==-3?"VSF / Stack":"VLAN "+pair.Key+(inv.Vlans.Any(v=>v.Id==pair.Key&&v.Voice)?" (Voice)":""),13,"#30465c",false,152);
            string vlanName=inv.Vlans.FirstOrDefault(v=>v.Id==pair.Key)?.Name.Trim()??"";
            if(vlanName.Length>0)s.Text(x+44,y+19,vlanName,11,"#52677c",false,152);
            n++;
        }
        if(aggregateRows.Count>0)
        {
            double keyY=legendY+Math.Ceiling((LegendVlans(inv).Count+3)/8.0)*36+38;
            s.Rect(24,keyY-12,1672,aggregateHeight-16,"#f8fafc","#dce4ec",8);
            s.Text(42,keyY,"Trunk / LAG key   /   one outline color per aggregate   /   fill remains native VLAN",12,"#34516b",true);
            double rowY=keyY+28;
            foreach(var row in aggregateRows)
            {
                int col=0;
                foreach(var entry in row.Entries)
                {
                    double x=40+col++*552;
                    s.Shapes.Add(new(new[]{(x,rowY+1),(x+28,rowY+1),(x+28,rowY+17),(x,rowY+17)},"#ffffff",entry.Color,3.2));
                    string mode=string.Join(" / ",entry.Ports.SelectMany(p=>p.Notes).Where(n=>n.StartsWith("Aggregation mode: ")).Select(n=>n[18..].ToUpperInvariant()).Distinct());
                    s.Text(x+42,rowY,entry.Id+(mode.Length>0?" ("+mode+")":""),11,"#30465c",true,476);
                    int line=0;foreach(string members in MemberLines(entry.Ports))s.Text(x+42,rowY+17+line++*14,members,10,"#52677c",false,476);
                }
                rowY+=row.Height;
            }
        }
        s.Text(36,s.Height-65,"FILL: NATIVE VLAN | CIRCLES: VLAN MEMBERSHIP | DIAMONDS: VOICE VLAN | COLORED OUTLINE: TRUNK / LAG KEY | D: DISABLED | ALL: ALL TAGGED",11);
        s.Text(36,s.Height-42,ReportText.CableMoveDisclaimer,12);
        return s;
    }
    public static List<string> Validate(Inventory inv,List<Placement> places)
    {
        var failures=new List<string>();
        if(inv.Groups.Count==0)failures.Add("No validated physical inventory.");
        if(inv.Ports.Count>2000||inv.Groups.Count>48)failures.Add("Inventory exceeds chart safety limits (2000 ports / 48 groups).");
        if(inv.Ports.Select(p=>p.Id.ToUpperInvariant()).Distinct().Count()!=inv.Ports.Count)failures.Add("Duplicate physical interface identifiers.");
        if(places.Count!=inv.Ports.Count+inv.Groups.Sum(g=>g.SocketLayout.Count>0?g.SocketLayout.Count-g.Expected:g.Product=="J4907A"?2:0))failures.Add("Generated interface count differs from normalized inventory.");
        int i=0;var physical=PhysicalView(inv);
        foreach(var g in inv.Groups)
        {
            var ports=inv.Ports.Where(p=>p.Group==g.Id).ToList();var pp=places.Where(p=>p.Group==g.Id).ToList();
            if(ports.Count!=g.Expected)failures.Add("Expected/generated interface count differs for "+g.Id);
            if(!ports.Select(p=>p.Number).SequenceEqual(Enumerable.Range(1,g.Expected)))failures.Add("Incorrect ordering for "+g.Id);
            var frame=physical?.Frames.FirstOrDefault(f=>f.Group==g.Id);
            double baseY=frame?.Y+PortTop(inv,g)??GroupBase(inv,i++);
            foreach(var p in pp)
            {
                if(p.Tile.X<(frame?.X+8??34)||p.Tile.X+p.Tile.W>(frame?.X+frame?.W-8??1686)||p.Tile.Y<baseY||p.Tile.Y+p.Tile.H>(frame?.Y+frame?.H-8??baseY+GroupPitch(inv,g)-84)||p.Marker.Y+p.Marker.H>(frame?.Y+frame?.H-8??baseY+GroupPitch(inv,g)-84))failures.Add("Frame or marker boundary violation: "+p.Port);
                var sourcePort=ports.FirstOrDefault(q=>q.Id==p.Port);
                if(sourcePort==null){failures.Add("Placement has no normalized source interface: "+p.Port);continue;}
                int num=sourcePort.Number;int local=num<=g.Copper?num:num-g.Copper;
                int row=g.OpticalOnly?0:(local-1)%2;
                if(g.Product=="J4820A")row=(num-1)%12/6;
                if(g.Product=="J4907A")row=num<=14?(num-1)/7:1;
                if(g.SocketLayout.Count>0)row=g.SocketLayout.Single(s=>s.Number==num&&s.Suffix==p.Socket).Row;
                double expectedY=baseY+row*RowPitch(inv,g);
                if(p.Tile.Y!=expectedY)failures.Add("Incorrect row geometry for "+p.Port);
                string localLabel=num+p.Socket;
                if(ReportRenderer.Measure(new Label(0,0,localLabel,PortFont(localLabel))).Width+6>p.Tile.W)failures.Add("Port label exceeds tile width: "+p.Port);
                if(p.Tile.W<42||p.Marker.X<p.Tile.X||p.Marker.X+p.Marker.W>p.Tile.X+p.Tile.W||p.Marker.Y<p.Tile.Y+26||p.Marker.Y+p.Marker.H>p.Tile.Y+p.Tile.H-4)failures.Add("Marker containment failed: "+p.Port);
            }
            for(int a=0;a<pp.Count;a++)for(int b=a+1;b<pp.Count;b++)
                if(Overlap(pp[a].Tile,pp[b].Tile)||Overlap(pp[a].Marker,pp[b].Tile)||Overlap(pp[b].Marker,pp[a].Tile))failures.Add("Overlapping geometry in "+g.Id);
            if(pp.Count>0)
            {
                double left=pp.Min(p=>p.Tile.X)-(g.BlankLeft&&g.SocketLayout.Count==0?BlankWidth(inv,g):0),right=pp.Max(p=>p.Tile.X+p.Tile.W);
                // Visible port extents are centered; pitch includes a four-pixel gap.
                double delta=Math.Abs((left+right)/2-(frame==null?860:frame.X+frame.W/2));
                if(delta>0.01)failures.Add("Frame centering delta exceeds tolerance for "+g.Id);
            }
        }
        for(int a=0;a<places.Count;a++)for(int b=a+1;b<places.Count;b++)
            if(places[a].Group!=places[b].Group&&(Overlap(places[a].Tile,places[b].Tile)||Overlap(places[a].Marker,places[b].Tile)||Overlap(places[b].Marker,places[a].Tile)))failures.Add("Overlapping module geometry.");
        return failures.Distinct().ToList();
    }
    public static List<string> ValidateScene(Scene scene)
    {
        var failures=new List<string>();
        var labels=scene.Labels.Select(l=>(Label:l,Size:ReportRenderer.Measure(l))).Where(x=>x.Size.Text.Length>0).ToList();
        for(int i=0;i<labels.Count;i++)
        {
            var item=labels[i];var l=item.Label;
            var bounds=new Box(l.X,l.Y,item.Size.Width,item.Size.Height,"");
            if(bounds.X<0||bounds.Y<0||bounds.X+bounds.W>scene.Width||bounds.Y+bounds.H>scene.Height)failures.Add("Text exceeds page bounds.");
            if(scene.Images.Any(image=>Overlap(bounds,new Box(image.X,image.Y,image.W,image.H,""))))failures.Add("Text collides with report logo.");
            for(int j=i+1;j<labels.Count;j++)
            {
                var other=labels[j];var q=other.Label;
                if(Overlap(bounds,new Box(q.X,q.Y,other.Size.Width,other.Size.Height,"")))failures.Add("Text labels overlap.");
            }
            foreach(var shape in scene.Shapes)
            {
                if(l.Owner.Length>0&&l.Owner==shape.Owner)continue;
                double pad=shape.ClipInside?0:shape.StrokeWidth/2;
                var shapeBounds=new Box(shape.Points.Min(p=>p.X)-pad,shape.Points.Min(p=>p.Y)-pad,shape.Points.Max(p=>p.X)-shape.Points.Min(p=>p.X)+2*pad,shape.Points.Max(p=>p.Y)-shape.Points.Min(p=>p.Y)+2*pad,"");
                if(Overlap(bounds,shapeBounds))failures.Add("Text collides with port geometry.");
            }
            foreach(var dot in scene.Dots)
                if(Overlap(bounds,new Box(dot.X-dot.Radius-(dot.Voice?dot.VoiceExtra+.2:0),dot.Y-dot.Radius-(dot.Voice?dot.VoiceExtra+.2:0),(dot.Radius+(dot.Voice?dot.VoiceExtra+.2:0))*2,(dot.Radius+(dot.Voice?dot.VoiceExtra+.2:0))*2,"")))failures.Add("Text collides with VLAN marker.");
        }
        foreach(var heading in scene.Boxes.Where(b=>b.Role=="Heading"))foreach(var shape in scene.Shapes)
        {
            double pad=shape.ClipInside?0:shape.StrokeWidth/2;
            var bounds=new Box(shape.Points.Min(p=>p.X)-pad,shape.Points.Min(p=>p.Y)-pad,shape.Points.Max(p=>p.X)-shape.Points.Min(p=>p.X)+2*pad,shape.Points.Max(p=>p.Y)-shape.Points.Min(p=>p.Y)+2*pad,"");
            if(Overlap(heading with {X=heading.X-.325,Y=heading.Y-.325,W=heading.W+.65,H=heading.H+.65},bounds))failures.Add("Heading collides with port or aggregate outline.");
        }
        foreach(var image in scene.Images)
            if(image.X<0||image.Y<0||image.W<=0||image.H<=0||image.X+image.W>scene.Width||image.Y+image.H>scene.Height)failures.Add("Report logo exceeds page bounds.");
        return failures.Distinct().ToList();
    }
    static bool Overlap(Box a,Box b)=>a.X<b.X+b.W&&a.X+a.W>b.X&&a.Y<b.Y+b.H&&a.Y+a.H>b.Y;
    public static List<Scene> ModuleDetails(Inventory inv,Scene chart,string hash)
    {
        var result=new List<Scene>();var hardware=PhysicalView(inv);
        if(hardware==null||inv.Groups.Count<3)return result;
        var devices=hardware.Bands.Where(b=>b.Text.EndsWith("FRONT VIEW")).OrderBy(b=>b.Y).ToList();
        var aggregateColors=AggregateColors(inv);
        for(int device=0;device<devices.Count;device++)
        {
            double end=device+1<devices.Count?devices[device+1].Y:double.MaxValue;
            var rows=hardware.Frames.Where(f=>f.Y>devices[device].Y&&f.Y<end).GroupBy(f=>f.Y).OrderBy(r=>r.Key).ToList();
            foreach(var rowSet in rows.Chunk(2))
            {
                var frames=rowSet.SelectMany(r=>r).ToList();var ids=frames.Where(f=>f.Group.Length>0).Select(f=>f.Group).ToHashSet();
                if(ids.Count==0)continue;
                var ports=inv.Ports.Where(p=>ids.Contains(p.Group)).ToList();
                string slots=string.Join(", ",frames.Select(f=>f.Slot));
                string memberLabel=devices[device].Text.Split('|').Select(t=>t.Trim()).FirstOrDefault(t=>t.StartsWith("Member "))??"";
                Scene NewPage(bool continued=false)
                {
                    var page=new Scene{Width=1720,Height=1190};page.Rect(0,0,1720,104,"#13283d","#13283d");
                    page.Text(36,16,ReportText.DisplayName(inv)+" / "+(continued?"Module key continued":"Enlarged module diagram"),30,"#ffffff",true,1648);
                    page.Text(36,60,inv.ChassisModel+" | "+slots+" | "+(memberLabel.Length>0?memberLabel+" | ":"")+"Front view - slot order retained",20,"#dce8f3",false,1648);
                    page.Text(36,1142,"Fill: native VLAN | Circles inside port: tagged VLANs | Diamond: voice | Outline: aggregate",18,"#34516b",false,1648);
                    return page;
                }
                var scene=NewPage();result.Add(scene);
                double top=frames.Min(f=>f.Y),bottom=frames.Max(f=>f.Y+f.H),dy=132-top;
                foreach(var box in chart.Boxes.Where(b=>b.Y>=top&&b.Y+b.H<=bottom))scene.Boxes.Add(box with {Y=box.Y+dy});
                foreach(var shape in chart.Shapes.Where(s=>s.Points.All(p=>p.Y>=top&&p.Y<=bottom)))scene.Shapes.Add(shape with {Points=shape.Points.Select(p=>(p.X,p.Y+dy)).ToArray(),ClipBounds=shape.ClipBounds is Box clip?clip with {Y=clip.Y+dy}:null});
                foreach(var dot in chart.Dots.Where(d=>d.Y-d.Radius>=top&&d.Y+d.Radius<=bottom))scene.Dots.Add(dot with {Y=dot.Y+dy});
                foreach(var label in chart.Labels.Where(l=>l.Y>=top&&l.Y+ReportRenderer.Measure(l).Height<=bottom))
                {
                    double font=label.Size;
                    if(label.Owner.Length==0&&label.Bold&&frames.Any(f=>Math.Abs(label.Y-(f.Y+12))<0.1))font=label.Text.Contains("J4907A")?18:20;
                    scene.Labels.Add(label with {Y=label.Y+dy,Size=font});
                }
                double y=bottom+dy+32;
                void Space(double height)
                {
                    if(y+height<=1110)return;
                    scene=NewPage(true);result.Add(scene);y=132;
                }
                Space(36);scene.Text(40,y,"VLAN key for these modules",24,"#30465c",true,1640);y+=38;
                var vlans=ports.SelectMany(p=>p.TaggedVlans.Concat(p.UntaggedVlan.HasValue?new[]{p.UntaggedVlan.Value}:Array.Empty<int>()).Concat(p.VoiceVlan.HasValue?new[]{p.VoiceVlan.Value}:Array.Empty<int>())).Distinct().Order().ToList();
                foreach(var row in vlans.Chunk(4))
                {
                    Space(58);int col=0;
                    foreach(int id in row)
                    {
                        double x=40+col++*410;scene.Rect(x,y+4,30,22,VlanColor(id));
                        scene.Dots.Add(new(x+46,y+15,5,VlanColor(id),inv.Vlans.Any(v=>v.Id==id&&v.Voice)));
                        scene.Text(x+64,y,"VLAN "+id+(inv.Vlans.Any(v=>v.Id==id&&v.Voice)?" (Voice)":""),18,"#30465c",true,330);
                        string name=inv.Vlans.FirstOrDefault(v=>v.Id==id)?.Name??"";
                        if(name.Length>0)scene.Text(x+64,y+26,name,18,"#52677c",false,330);
                    }y+=58;
                }
                Space(42);scene.Text(40,y,"Unknown native = gray | No native = white | VSF / stack link = black",18,"#52677c",false,1640);y+=42;
                var aggregates=ports.Where(p=>p.Aggregate.Length>0).GroupBy(p=>p.Aggregate,StringComparer.OrdinalIgnoreCase).ToList();
                if(aggregates.Count>0)
                {
                    Space(36);scene.Text(40,y,"Trunk / LAG key for these modules",24,"#30465c",true,1640);y+=38;
                    foreach(var row in aggregates.Chunk(3))
                    {
                        var entries=row.Select(a=>
                        {
                            var lines=new List<string>();string line="";
                            foreach(var port in a)
                            {
                                string next=line.Length==0?port.Id:line+", "+port.Id;
                                if(line.Length>0&&ReportRenderer.Measure(new Label(0,0,next,18)).Width>470){lines.Add(line);line=port.Id;}else line=next;
                            }
                            if(line.Length>0)lines.Add(line);
                            return (Id:a.Key,Ports:a.ToList(),Lines:lines);
                        }).ToList();
                        double height=36+entries.Max(a=>a.Lines.Count)*26;Space(height);int col=0;
                        foreach(var entry in entries)
                        {
                            double x=40+col++*548;string color=aggregateColors[entry.Id];
                            scene.Shapes.Add(new(new[]{(x,y+3),(x+30,y+3),(x+30,y+25),(x,y+25)},"#ffffff",color,3.2));
                            string mode=string.Join(" / ",entry.Ports.SelectMany(p=>p.Notes).Where(n=>n.StartsWith("Aggregation mode: ")).Select(n=>n[18..].ToUpperInvariant()).Distinct());
                            scene.Text(x+44,y,entry.Id+(mode.Length>0?" ("+mode+")":""),18,"#30465c",true,470);
                            for(int n=0;n<entry.Lines.Count;n++)scene.Text(x+44,y+28+n*26,entry.Lines[n],18,"#52677c",false,470);
                        }y+=height;
                    }
                }
            }
        }
        return result;
    }

    // Keep every value intact. Width is checked with the same embedded font
    // used to render the PDF; no inventory data is passed through ellipsis.
    static List<string> InventoryLines(string value,double width,bool bold=false)
    {
        var lines=new List<string>();string line="";
        double Width(string text)=>ReportRenderer.Measure(new Label(0,0,text,10,"#34495e",bold)).Width;
        foreach(string word in value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate=line.Length==0?word:line+" "+word;
            if(Width(candidate)<=width){line=candidate;continue;}
            if(line.Length>0){lines.Add(line);line="";}
            string rest=word;
            while(Width(rest)>width)
            {
                int n=1;while(n<rest.Length&&Width(rest[..(n+1)])<=width)n++;
                lines.Add(rest[..n]);rest=rest[n..];
            }
            line=rest;
        }
        if(line.Length>0)lines.Add(line);
        return lines;
    }
    public static List<Scene> Booklet(Inventory inv,Scene chart,string hash)
    {
        var pages=new List<Scene>{chart};
        pages.AddRange(DeviceIdentities.Pages(inv));
        double[] widths={210,220,105,80,230,105,65,125};
        string[] headers={"Port","Description","Mode","Native","Tagged VLANs","LAG / TRK","Voice","Admin state"};
        const double top=132,bottom=728,padding=6,linePitch=12.5;
        Scene? page=null;double y=top;int segments=0,firstPort=0,lastPort=0;bool pageContinued=false;
        var pagePorts=new HashSet<int>();
        void FinishPage()
        {
            if(page==null)return;
            page.Text(30,55,inv.OS+"  |  "+inv.Firmware+"  |  Ports "+(firstPort+1)+"-"+(lastPort+1)+" of "+inv.Ports.Count+(pageContinued?"  |  Continued":""),12,"#c6d7e6");
            page.Rect(30,737,1140,1,"#d9e3ec","#d9e3ec");
            string identities=string.Join("; ",pagePorts.Select(index=>inv.Groups.FirstOrDefault(g=>g.Id==inv.Ports[index].Group)).Where(g=>g!=null).Cast<Group>()
                .SelectMany(g=>new[]{g.Device,DeviceIdentities.ModuleId(g)}).Where(id=>id.Length>0).Distinct().Select(id=>
                {
                    var device=DeviceIdentities.For(inv,id);string heading=DeviceIdentities.Heading(inv,id);
                    return heading.Length==0?"":(device?.Label??id)+": "+heading;
                }).Where(text=>text.Length>0));
            if(identities.Length>0)page.Text(30,749,identities,10,"#52677c",false,1130);
            page.Text(30,777,ReportText.CableMoveDisclaimer,10,"#34516b");
            page.Text(1070,815,"Page "+pages.Count,9,"#718396");
        }
        void NewPage(int portIndex,bool continued)
        {
            FinishPage();page=new Scene{Width=1190,Height=842};pages.Add(page);
            page.Rect(0,0,1190,84,"#13283d","#13283d");
            page.Text(30,17,ReportText.DisplayName(inv)+"  /  Port inventory",23,"#ffffff",true,1130);
            page.Rect(30,102,1140,28,"#e7eef5","#e7eef5",3);
            double x=30;
            for(int col=0;col<headers.Length;col++){page.Text(x+8,110,headers[col],11,"#29445d",true,widths[col]-16);x+=widths[col];}
            y=top;segments=0;firstPort=lastPort=portIndex;pageContinued=continued;pagePorts.Clear();
        }
        for(int index=0;index<inv.Ports.Count;index++)
        {
            var p=inv.Ports[index];
            string native=p.StackLink?"Stack":p.UntaggedVlan?.ToString()??p.UntaggedState;
            string tagged=ReportText.TaggedVlans(p);
            string[] values={p.Id,p.Description,p.Mode,native,tagged,p.Aggregate,p.VoiceVlan?.ToString()??"",p.Disabled.HasValue?p.Disabled.Value?"Disabled":"Enabled":"Unknown"};
            var lines=values.Select((value,col)=>InventoryLines(value,widths[col]-(col==3?24:8)-7,col==0)).ToList();
            int total=Math.Max(1,lines.Max(cell=>cell.Count)),offset=0;
            while(offset<total)
            {
                if(page==null||segments>=24)NewPage(index,offset>0);
                int remaining=total-offset;
                double completeHeight=Math.Max(24,2*padding+remaining*linePitch);
                // Keep normal rows together whenever they fit on a fresh page.
                if(y>top&&completeHeight<=bottom-top&&y+completeHeight>bottom)NewPage(index,offset>0);
                int capacity=(int)Math.Floor((bottom-y-2*padding)/linePitch);
                if(capacity<1){NewPage(index,offset>0);capacity=(int)Math.Floor((bottom-y-2*padding)/linePitch);}
                int take=Math.Min(remaining,capacity);
                var cells=new List<List<string>>();
                for(int col=0;col<lines.Count;col++)
                {
                    var text=lines[col].Skip(offset).Take(take).ToList();
                    // Repeat short identifying fields on continuation pages.
                    // The VLAN and description text itself is printed exactly once.
                    if(offset>0&&text.Count==0&&col!=1&&col!=4)
                    {
                        var context=lines[col].ToList();
                        if(col==0)context.Add("Continued");
                        if(context.Count<=capacity)text=context;
                        else if(col==0)text=new(){"Port entry "+(index+1),"Continued"};
                    }
                    cells.Add(text);
                }
                int visible=Math.Max(take,cells.Max(cell=>cell.Count));
                double height=Math.Max(24,2*padding+visible*linePitch);
                if(y+height>bottom){NewPage(index,offset>0);continue;}
                page!.Rect(30,y,1140,height,index%2==1?"#f1f5f9":"#ffffff","#e8edf3",0,"InventoryRow");
                double x=30;
                for(int col=0;col<cells.Count;col++)
                {
                    double inset=col==3?24:8;
                    if(col==3)
                    {
                        string fill=chart.VlanColors!=null?VlanPalette.NativeFill(p,chart.VlanColors):Color(p);
                        page.Rect(x+8,y+padding,11,11,fill,"#a9b6c4",2,"InventoryNative");
                    }
                    for(int line=0;line<cells[col].Count;line++)
                        page.Text(x+inset,y+padding+line*linePitch,cells[col][line],10,col==0?"#173f63":"#34495e",col==0,widths[col]-inset-7,"inventory:"+p.Id+":"+col);
                    x+=widths[col];
                }
                y+=height;segments++;lastPort=index;pagePorts.Add(index);offset+=take;
                if(offset<total)NewPage(index,true);
            }
        }
        FinishPage();return pages;
    }
    static string Clip(string s,int max)=>s.Length<=max?s:s[..(max-3)]+"...";
}
public static class ReportRenderer
{
    static readonly SkiaSharp.SKTypeface Regular=Load("Report-Regular.ttf");
    static readonly SkiaSharp.SKTypeface Bold=Load("Report-Bold.ttf");
    static SkiaSharp.SKTypeface Load(string name)
    {
        using var stream=typeof(ReportRenderer).Assembly.GetManifestResourceStream("SwitchportGenerator.Fonts."+name)
            ??throw new InvalidOperationException("Embedded report font is missing.");
        return SkiaSharp.SKTypeface.FromStream(stream)??throw new InvalidOperationException("Report font could not load.");
    }
    public static (string Text,double Width,double Height) Measure(Label label)
    {
        using var font=new SkiaSharp.SKFont(label.Bold?Bold:Regular,(float)label.Size);
        string text=label.Text;
        if(label.Width>0&&font.MeasureText(text)>label.Width)
        {
            while(text.Length>0&&font.MeasureText(text+"...")>label.Width)text=text[..^1];
            text+="...";
        }
        double advance=font.MeasureText(text,out var ink);
        return label.InkBox?(text,ink.Width,ink.Height):(text,advance,font.Metrics.Descent-font.Metrics.Ascent);
    }
    public static void Draw(SkiaSharp.SKCanvas canvas,Scene scene)
    {
        using var paint=new SkiaSharp.SKPaint{IsAntialias=true};
        void Style(string color,bool stroke=false,float width=1)
        {
            paint.Color=SkiaSharp.SKColor.Parse(color);paint.Style=stroke?SkiaSharp.SKPaintStyle.Stroke:SkiaSharp.SKPaintStyle.Fill;paint.StrokeWidth=width;
        }
        foreach(var box in scene.Boxes)
        {
            var rect=new SkiaSharp.SKRect((float)box.X,(float)box.Y,(float)(box.X+box.W),(float)(box.Y+box.H));
            Style(box.Fill);canvas.DrawRoundRect(rect,(float)box.Radius,(float)box.Radius,paint);
            Style(box.Stroke,true,0.65f);canvas.DrawRoundRect(rect,(float)box.Radius,(float)box.Radius,paint);
        }
        foreach(var asset in scene.Images)
        {
            using var image=SkiaSharp.SKImage.FromEncodedData(asset.Data)??throw new InvalidOperationException("Report logo could not be decoded.");
            canvas.DrawImage(image,new SkiaSharp.SKRect((float)asset.X,(float)asset.Y,(float)(asset.X+asset.W),(float)(asset.Y+asset.H)),new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear,SkiaSharp.SKMipmapMode.Linear));
        }
        foreach(var shape in scene.Shapes)
        {
            using var path=new SkiaSharp.SKPath();path.MoveTo((float)shape.Points[0].X,(float)shape.Points[0].Y);
            foreach(var point in shape.Points.Skip(1))path.LineTo((float)point.X,(float)point.Y);path.Close();
            canvas.Save();
            if(shape.ClipInside)canvas.ClipPath(path,SkiaSharp.SKClipOperation.Intersect,true);
            if(shape.ClipBounds is Box clip)canvas.ClipRect(new SkiaSharp.SKRect((float)clip.X,(float)clip.Y,(float)(clip.X+clip.W),(float)(clip.Y+clip.H)),SkiaSharp.SKClipOperation.Intersect,true);
            if(shape.Fill.Length>0){Style(shape.Fill);canvas.DrawPath(path,paint);}
            if(shape.StrokeWidth>0){Style(shape.Stroke,true,(float)shape.StrokeWidth);canvas.DrawPath(path,paint);}
            canvas.Restore();
        }
        foreach(var dot in scene.Dots)
        {
            if(dot.Voice)
            {
                float x=(float)dot.X,y=(float)dot.Y,r=(float)(dot.Radius+dot.VoiceExtra);
                using var diamond=new SkiaSharp.SKPath();
                diamond.MoveTo(x,y-r);diamond.LineTo(x+r,y);diamond.LineTo(x,y+r);diamond.LineTo(x-r,y);diamond.Close();
                Style(dot.Fill);canvas.DrawPath(diamond,paint);
                Style("#13283d",true,(float)dot.Outline);canvas.DrawPath(diamond,paint);
            }
            else
            {
                Style("#ffffff");canvas.DrawCircle((float)dot.X,(float)dot.Y,(float)(dot.Radius+dot.Halo),paint);
                Style(dot.Fill);canvas.DrawCircle((float)dot.X,(float)dot.Y,(float)dot.Radius,paint);
                Style("#40566b",true,(float)dot.Outline);canvas.DrawCircle((float)dot.X,(float)dot.Y,(float)dot.Radius,paint);
            }
        }
        foreach(var label in scene.Labels)
        {
            using var font=new SkiaSharp.SKFont(label.Bold?Bold:Regular,(float)label.Size){Edging=SkiaSharp.SKFontEdging.Antialias};
            string text=Measure(label).Text;
            double x=label.X,y=label.Y-font.Metrics.Ascent;
            if(label.InkBox){font.MeasureText(text,out var ink);x=label.X-ink.Left;y=label.Y-ink.Top;}
            Style(label.Color);canvas.DrawText(text,(float)x,(float)y,SkiaSharp.SKTextAlign.Left,font,paint);
        }
    }
}
public static class Png
{
    public static void Save(Scene scene,string path,int width,int height,bool fitPage=false)
    {
        if((long)width*height>50_000_000)throw new InvalidOperationException("Raster size exceeds limit.");
        using var surface=SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(width,height));
        var canvas=surface.Canvas;canvas.Clear(SkiaSharp.SKColors.White);
        double marginX=width*18/841.89,marginY=height*18/595.28;
        float scale=(float)Math.Min((width-2*marginX)/scene.Width,(height-2*marginY)/scene.Height);
        canvas.Translate((float)(width-scene.Width*scale)/2,(float)marginY);canvas.Scale(scale);ReportRenderer.Draw(canvas,scene);
        using var image=surface.Snapshot();using var data=image.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
        byte[] png=data.ToArray();
        using var file=File.Create(path);file.Write(png,0,33);
        byte[] chunk=new byte[21];
        void Big(int offset,uint value){chunk[offset]=(byte)(value>>24);chunk[offset+1]=(byte)(value>>16);chunk[offset+2]=(byte)(value>>8);chunk[offset+3]=(byte)value;}
        Big(0,9);System.Text.Encoding.ASCII.GetBytes("pHYs").CopyTo(chunk,4);Big(8,11811);Big(12,11811);chunk[16]=1;
        uint crc=0xffffffff;for(int j=4;j<17;j++){crc^=chunk[j];for(int k=0;k<8;k++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}Big(17,crc^0xffffffff);
        file.Write(chunk);file.Write(png,33,png.Length-33);
    }
}
public static class Pdf
{
    public static void Save(List<Scene> scenes,string path)
    {
        using var document=SkiaSharp.SKDocument.CreatePdf(path);
        foreach(var scene in scenes)
        {
            var canvas=document.BeginPage(841.89f,595.28f);canvas.Clear(SkiaSharp.SKColors.White);
            float scale=(float)Math.Min(805.89/scene.Width,559.28/scene.Height);
            canvas.Translate((float)(841.89-scene.Width*scale)/2,18);canvas.Scale(scale);ReportRenderer.Draw(canvas,scene);document.EndPage();
        }
        document.Close();
    }
}
