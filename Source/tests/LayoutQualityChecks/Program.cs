using SwitchportGenerator;
using System.Text.Json;
using System.Text.RegularExpressions;

static class LayoutQualityChecks
{
    static int checks,fixtures;
    static void Check(bool value,string message)
    {checks++;if(!value)throw new Exception(message);}
    static Inventory Parse(string text)=>DeviceModels.Parse(text,Array.Empty<DeviceIdentity>());
    static Inventory Small()=>Parse("config-file-header\nSMALL\nvInstantOn_1930_3.3.2.0 (5)\nSKU Description \"Instant On 1930 JL680A\"\n");
    static Inventory Wide()=>Parse("; JL256A Configuration Editor\nhostname WIDE-LAYOUT\n");
    static Inventory Optical()=>Parse("# by RouterOS 7.16.2\n# model = CRS326-24S+2Q+RM\n/system identity\nset name=OPTICAL-LAYOUT\n");
    static Inventory Flex()=>Parse("; JL072A Configuration Editor\nhostname FLEX\nmodule 1 type JL072A\nmodule A type JL083A\n");
    static Inventory MixedIrf()=>DeviceModels.Parse("version 7.1.070\nsysname MIXED-IRF\nirf member 1 priority 32\nirf member 2 priority 30\ninterface GigabitEthernet1/0/1\n#\ninterface GigabitEthernet2/0/1\n#\n",new[]{new DeviceIdentity{Id="1",ModelSelection="JG932A"},new DeviceIdentity{Id="2",ModelSelection="JG934A"}});
    static Inventory Memberships(Inventory inv,int count,bool longNames=false)
    {
        Check(inv.Blockers.Count==0,"Fixture parser blockers: "+string.Join(';',inv.Blockers));
        inv.Vlans=new(){new Vlan{Id=1,Name="Data"}};
        inv.Vlans.AddRange(Enumerable.Range(100,count).Select((id,index)=>new Vlan{Id=id,Name=longNames?"Infrastructure Management BackPlane "+id:"Network "+id,Voice=index==1}));
        foreach(var p in inv.Ports){p.UntaggedVlan=1;p.UntaggedState="Known";p.TaggedVlans.Clear();p.VoiceVlan=null;p.Aggregate="";p.Notes.Clear();}
        foreach(var g in inv.Groups)
        {
            var ps=inv.Ports.Where(p=>p.Group==g.Id).ToList();
            var targets=ps.Where(p=>p.Media=="RJ-45").Take(1).Concat(ps.Where(p=>p.Media!="RJ-45").Take(1));
            foreach(var p in targets){p.TaggedVlans=Enumerable.Range(100,count).ToList();if(count>1)p.VoiceVlan=101;}
        }
        DeviceIdentities.Apply(inv,Array.Empty<DeviceIdentity>());return inv;
    }
    static List<int> MarkerIds(Inventory inv,Port p)=>p.TaggedVlans.Concat(p.VoiceVlan.HasValue?new[]{p.VoiceVlan.Value}:Array.Empty<int>())
        .Concat(p.UntaggedVlan.HasValue&&inv.Vlans.Any(v=>v.Id==p.UntaggedVlan&&v.Voice)?new[]{p.UntaggedVlan.Value}:Array.Empty<int>()).Distinct().Order().ToList();
    static bool Contains((double X,double Y)[] points,double x,double y)
    {
        bool inside=false;
        for(int i=0,j=points.Length-1;i<points.Length;j=i++)
        {
            var a=points[j];var b=points[i];double cross=(x-a.X)*(b.Y-a.Y)-(y-a.Y)*(b.X-a.X);
            if(Math.Abs(cross)<1e-6&&x>=Math.Min(a.X,b.X)-1e-6&&x<=Math.Max(a.X,b.X)+1e-6&&y>=Math.Min(a.Y,b.Y)-1e-6&&y<=Math.Max(a.Y,b.Y)+1e-6)return true;
            if((a.Y>y)!=(b.Y>y)&&x<(b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
        }
        return inside;
    }
    static string Compact(string s)=>Regex.Replace(s,@"\s+","");
    static void Key(Inventory inv,QuickSheet.Result result,string name)
    {
        var scene=result.Scene;
        var title=scene.Labels.Single(l=>l.Text=="VLAN key");var measured=ReportRenderer.Measure(title);
        Check(measured.Text==title.Text,name+": key heading was truncated.");
        Check(title.InkBox,name+": key heading must anchor actual ink.");
        Check(title.Size>=7.5-.01&&title.Size<=10+.01,name+": key title font outside readable bounds.");
        var band=scene.Boxes.Single(b=>b.Role=="Heading"&&b.Fill=="#e7eff6"&&title.X>=b.X&&title.X<=b.X+b.W&&title.Y>=b.Y&&title.Y<=b.Y+b.H);
        Check(title.X+measured.Width<=band.X+band.W+.05&&title.Y+measured.Height<=band.Y+band.H+.05,name+": title ink escaped backing.");
        double bankBottom=result.Sockets.Max(s=>Math.Max(s.Tile.Y+s.Tile.H,s.NumberBounds.Y+s.NumberBounds.H));
        double bankRight=result.Sockets.Max(s=>s.Tile.X+s.Tile.W);
        Check(title.Y>bankBottom||title.X>bankRight,name+": key overlaps interface bank.");
        var swatches=scene.Boxes.Where(b=>b.Stroke=="#687b8b"&&result.VlanColors.Values.Contains(b.Fill)&&b.Y>title.Y).OrderBy(b=>b.Y).ThenBy(b=>b.X).ToList();
        Check(swatches.Count==result.VlanColors.Count,name+": key swatches missing/duplicated.");
        var columnXs=swatches.Select(b=>b.X).Distinct().Order().ToList();var actualIds=new List<int>();
        foreach(var swatch in swatches)
        {
            int id=result.VlanColors.Single(p=>p.Value==swatch.Fill).Key;actualIds.Add(id);
            double columnRight=columnXs.FirstOrDefault(x=>x>swatch.X+.01,band.X+band.W);
            double nextY=swatches.Where(b=>Math.Abs(b.X-swatch.X)<.01&&b.Y>swatch.Y+.01).Select(b=>b.Y-result.GeometryScale).DefaultIfEmpty(scene.Height-12).Min();
            var labels=scene.Labels.Where(l=>l.Color=="#30465c"&&l.Owner.Length==0&&l.Y>=swatch.Y-result.GeometryScale-.05&&l.Y<nextY-.05&&l.X>swatch.X&&l.X<columnRight-.01).OrderBy(l=>l.Y).ToList();
            Check(labels.Count>0,name+": key text missing for VLAN "+id);
            bool voice=inv.Vlans.Any(v=>v.Id==id&&v.Voice)||inv.Ports.Any(p=>p.VoiceVlan==id);
            string prefix=id+(voice?" Voice":"");
            Check(labels[0].Text==prefix||labels[0].Text.StartsWith(prefix+" ",StringComparison.Ordinal),name+": numeric/Voice identity split for VLAN "+id);
            string expected=(prefix+" "+(inv.Vlans.FirstOrDefault(v=>v.Id==id)?.Name??"")).TrimEnd();
            Check(Compact(string.Join(' ',labels.Select(l=>l.Text)))==Compact(expected),name+": key name incomplete/reordered for VLAN "+id);
            foreach(var l in labels)
            {
                Check(l.Width>=78-.05,name+": readable key text width was stolen by bank enlargement.");
                Check(l.Size>=7-.01&&l.Size<=9+.01,name+": key text font outside readable bounds.");
                var m=ReportRenderer.Measure(l);Check(m.Text==l.Text&&m.Width<=l.Width+.05,name+": key label was ellipsized/clipped.");
            }
            var dot=scene.Dots.Single(d=>d.Fill==swatch.Fill&&d.Y>title.Y&&Math.Abs(d.Y-(swatch.Y+3*result.GeometryScale))<.05);
            double extent=dot.Radius+Math.Max(dot.Halo,dot.Outline/2);
            Check(dot.X-extent>=swatch.X+swatch.W+.5,name+": key marker overlaps native swatch.");
            Check(dot.X+extent<labels[0].X-.5,name+": key marker overlaps label.");
        }
        Check(actualIds.SequenceEqual(result.VlanColors.Keys.Order()),name+": key IDs lost numeric row-major order.");
        foreach(var l in scene.Labels.Where(l=>l.Color=="#52677c"&&l.Y>title.Y))
            Check(l.Size>=7-.01&&l.Size<=8.5+.01&&ReportRenderer.Measure(l).Text==l.Text,name+": native-status text is unreadable/truncated.");
    }
    static void Run(string name,Inventory inv,bool preferred,bool below=false)
    {
        fixtures++;var original=JsonSerializer.Serialize(inv);Check(inv.Blockers.Count==0,name+": parser blocked fixture.");
        var result=QuickSheet.Build(inv,Charts.Layout(inv),"QUALITY-REGRESSION");var scene=result.Scene;
        Check(Charts.ValidateScene(scene).Count==0,name+": final scene collisions: "+string.Join(';',Charts.ValidateScene(scene)));
        Check(scene.Dots.Max(d=>d.Radius)-scene.Dots.Min(d=>d.Radius)<1e-6,name+": global marker size differs by context.");
        double target=Math.Min(4.2,2.1*result.GeometryScale);
        Check(result.MarkerRadius>=1.1-.001&&result.MarkerRadius<=target+.001,name+": marker radius outside preferred/readable limits.");
        if(preferred)Check(Math.Abs(result.MarkerRadius-target)<.001,name+": sparse markers did not grow with enlarged ports.");
        foreach(var socket in result.Sockets)
        {
            var p=inv.Ports.Single(p=>p.Id==socket.Port);double h=socket.Optical?16.2:23.2;
            Check(Math.Abs(socket.Tile.W/25.2-socket.Tile.H/h)<1e-6,name+": face stretched nonuniformly.");
            var dots=scene.Dots.Where(d=>Contains(socket.FacePoints,d.X,d.Y)).ToList();var ids=MarkerIds(inv,p);
            Check(dots.Count==ids.Count,name+": marker count differs from memberships on "+p.Id);
            foreach(int id in ids)Check(dots.Count(d=>d.Fill==result.VlanColors[id])==1,name+": marker color/membership missing on "+p.Id);
            foreach(var dot in dots)
            {
                double e=dot.Radius+Math.Max(dot.Halo,dot.Outline/2);
                foreach(var c in new[]{(dot.X-e,dot.Y-e),(dot.X+e,dot.Y-e),(dot.X-e,dot.Y+e),(dot.X+e,dot.Y+e)})
                    Check(Contains(socket.FacePoints,c.Item1,c.Item2),name+": full marker extent escaped face.");
            }
        }
        foreach(var l in scene.Labels.Where(l=>l.Owner.Length>0&&l.InkBox))Check(l.Size>=4.5-.01&&l.Size<=9+.01,name+": port number font outside bounds.");
        foreach(var l in scene.Labels.Where(l=>l.Owner.Length==0&&l.Color=="#ffffff"&&l.Y>=QuickSheet.DiagramTop))Check(l.Size>=7-.01&&l.Size<=8.5+.01,name+": oversized panel heading.");
        Key(inv,result,name);
        if(below)Check(scene.Labels.Single(l=>l.Text=="VLAN key").Y>result.Sockets.Max(s=>Math.Max(s.Tile.Y+s.Tile.H,s.NumberBounds.Y+s.NumberBounds.H)),name+": wide/dense layout did not use below-bank key fallback.");
        Check(JsonSerializer.Serialize(inv)==original,name+": sizing mutated inventory data.");
        Console.WriteLine($"PASS {name}: geometry {result.GeometryScale:0.###}x, markers {result.MarkerRadius:0.###} pt, profile {result.CompactionLevel}.");
    }
    static void Main(string[] args)
    {
        Run("MikroTik-optical-shape",Memberships(Optical(),2),true);
        if(args.Length>0)Run("supplied-MikroTik-backup",Parse(ConfigInput.Read(args[0])),true);
        foreach(int n in new[]{1,2,3}){Run("optical-sparse-"+n,Memberships(Optical(),n),true);Run("wide-sparse-"+n,Memberships(Wide(),n),true);}
        Run("optical-long-names",Memberships(Optical(),3,true),true);Run("wide-long-names",Memberships(Wide(),3,true),true);
        var token=Memberships(Wide(),3,true);token.Vlans.Single(v=>v.Id==101).Name="InfrastructureManagementDistributionBackPlaneTelecommunicationsServices";Run("wide-unbroken-long-name",token,true);
        foreach(int n in new[]{28,40}){Run("small-dense-"+n,Memberships(Small(),n),false,true);Run("wide-dense-"+n,Memberships(Wide(),n),false);}
        Run("wide-dense-long-names",Memberships(Wide(),28,true),false);
        Run("wide-flex-existing",Flex(),true,true);Run("wide-flex-three-VLANs",Memberships(Flex(),3),true,true);
        Run("mixed-IRF-existing",MixedIrf(),true);Run("mixed-IRF-three-VLANs",Memberships(MixedIrf(),3),true);
        Console.WriteLine($"PASS {checks} layout-quality assertions across {fixtures} fixtures.");
    }
}
