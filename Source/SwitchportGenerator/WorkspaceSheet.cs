using System.Text;
using System.Text.Json;
namespace SwitchportGenerator;

public sealed record WorkspaceSheetItem(string Id,string Name,Inventory Inventory,string Hash="",int Ordinal=0);
public sealed record WorkspaceSheetPlacement(string Id,string Name,int PageNumber,double Y,double Height,double GeometryScale=1,bool Standalone=false);
public sealed record WorkspaceSheetLegendEntry(string Identity,int VlanId,string Label,string Color,bool Voice,int PageNumber);
public sealed class WorkspaceSheetResult
{
    public List<Scene> Pages {get;}=new();
    public List<WorkspaceSheetPlacement> Placements {get;}=new();
    public List<WorkspaceSheetLegendEntry> LegendEntries {get;}=new();
    public List<string> Warnings {get;}=new();
    public double GeometryScale=>1;
    public double MarkerRadius {get;internal set;}=QuickSheet.DotRadius;
}

/// <summary>Ordered A4 switch blocks with one complete, semantically shared VLAN key per sheet.</summary>
public static class WorkspaceSheet
{
    public const double PageWidth=805.89,PageHeight=559.28,BlockGap=6,ContentTop=0;
    const double ContentBottom=PageHeight-11,LegendGap=6,LegendFont=7.5,LegendHeading=13,LegendInset=21;
    sealed record Component(WorkspaceSheetItem Item,QuickSheet.Result Layout,bool Standalone=false);
    sealed record LegendEntry(string Identity,int VlanId,string Label,string Color,bool Voice=false,bool Marker=true);
    sealed record LegendPlan(List<LegendEntry> Entries,List<List<string>> Lines,int Columns,double ColumnWidth,double LineHeight,double Height);

    public static WorkspaceSheetResult Build(IReadOnlyList<WorkspaceSheetItem> items,ReportLogo? logo=null,string? title=null)
    {
        // The caller's output title continues to name files; it is deliberately not
        // repeated as an oversized generic heading above the switches themselves.
        if(items.Count==0)throw new InvalidOperationException("Add at least one valid configuration to build a workspace sheet.");
        if(items.Any(item=>string.IsNullOrWhiteSpace(item.Id))||items.Select(item=>item.Id).Distinct(StringComparer.Ordinal).Count()!=items.Count)
            throw new InvalidOperationException("Workspace sheet item identifiers must be nonempty and distinct.");
        // Direct callers receive the same semantic palette as batch export, without
        // mutating their inventories. Contradictory supplied mappings fail clearly.
        var palette=WorkspacePalette.Create(items.Select(item=>(item.Id,item.Inventory)).ToArray());
        var prepared=items.Select((item,index)=>
        {
            var inventory=JsonSerializer.Deserialize<Inventory>(JsonSerializer.Serialize(item.Inventory))??throw new InvalidOperationException("Configuration inventory is empty.");
            palette.Apply(item.Id,inventory);return item with{Inventory=inventory,Ordinal=index+1};
        }).ToArray();
        var result=new WorkspaceSheetResult();result.Warnings.AddRange(palette.Warnings);
        var components=new List<Component>();
        foreach(var item in prepared)
        {
            if(item.Inventory.Blockers.Count>0)throw new InvalidOperationException("Resolve configuration validation blockers before adding "+item.Name+" to a workspace sheet.");
            var placements=Charts.Layout(item.Inventory);var errors=Charts.Validate(item.Inventory,placements);
            if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
            try{components.Add(new(item,QuickSheet.BuildCompact(item.Inventory,placements,item.Hash,includeVlanKey:false)));}
            catch(OnePageCapacityException){components.Add(new(item,QuickSheet.Build(item.Inventory,placements,item.Hash),true));}
        }
        double radius=components.Where(component=>!component.Standalone).Select(component=>component.Layout.MarkerRadius).DefaultIfEmpty(QuickSheet.DotRadius).Min();
        result.MarkerRadius=radius;
        for(int index=0;index<components.Count;index++)
        {
            var current=components[index];
            if(!current.Standalone&&Math.Abs(current.Layout.MarkerRadius-radius)>1e-5)
                components[index]=current with{Layout=QuickSheet.BuildCompact(current.Item.Inventory,Charts.Layout(current.Item.Inventory),current.Item.Hash,radius,false)};
        }
        var group=new List<Component>();double bodyHeight=0;int ordinal=0;
        void Flush()
        {
            if(group.Count==0)return;
            var page=new Scene{Width=PageWidth,Height=PageHeight,DiagramScale=1,MarkerRadius=radius,IsAdaptiveLayout=true};
            result.Pages.Add(page);double y=0;
            foreach(var component in group)
            {
                Append(page,component.Layout.Scene,y,component.Item.Id,++ordinal,component.Item.Name);
                result.Placements.Add(new(component.Item.Id,component.Item.Name,result.Pages.Count,y,component.Layout.Scene.Height));
                y+=component.Layout.Scene.Height+BlockGap;
            }
            var legend=PlanLegend(group.Select(component=>component.Item).ToArray());
            DrawLegend(page,legend,bodyHeight+LegendGap,radius);
            foreach(var entry in legend.Entries.Where(entry=>entry.VlanId>=0))
                result.LegendEntries.Add(new(entry.Identity,entry.VlanId,entry.Label,entry.Color,entry.Voice,result.Pages.Count));
            group.Clear();bodyHeight=0;
        }
        foreach(var component in components)
        {
            double candidateHeight=bodyHeight+(group.Count==0?0:BlockGap)+component.Layout.Scene.Height;
            var candidates=group.Append(component).ToArray();
            bool fits=!component.Standalone&&candidateHeight+LegendGap+PlanLegend(candidates.Select(candidate=>candidate.Item).ToArray()).Height<=ContentBottom;
            if(!fits&&group.Count>0)
            {
                Flush();candidateHeight=component.Layout.Scene.Height;
                fits=!component.Standalone&&candidateHeight+LegendGap+PlanLegend(new[]{component.Item}).Height<=ContentBottom;
                result.Warnings.Add("A new sheet starts with "+component.Item.Name+" so complete switch blocks and the shared VLAN key remain readable.");
            }
            if(fits){group.Add(component);bodyHeight=candidateHeight;continue;}
            // A previously supported large/dense configuration retains its complete
            // one-item, one-key standalone page instead of losing ports or legend rows.
            var layout=component.Standalone?component.Layout:QuickSheet.Build(component.Item.Inventory,Charts.Layout(component.Item.Inventory),component.Item.Hash);
            var standalone=new Scene{Width=PageWidth,Height=PageHeight,DiagramScale=layout.GeometryScale,MarkerRadius=layout.MarkerRadius,IsAdaptiveLayout=true};
            Append(standalone,layout.Scene,0,component.Item.Id,++ordinal,component.Item.Name);result.Pages.Add(standalone);
            result.Placements.Add(new(component.Item.Id,component.Item.Name,result.Pages.Count,0,PageHeight,layout.GeometryScale,true));
            result.Warnings.Add(component.Item.Name+" requires a dedicated A4 sheet to retain all interfaces, markers and its single complete VLAN key at readable size.");
        }
        Flush();
        for(int index=0;index<result.Pages.Count;index++)
        {
            var page=result.Pages[index];var footer=page.Labels.FindIndex(label=>label.Text==ReportText.CableMoveDisclaimer);
            string text=ReportText.CableMoveDisclaimer+"  |  Sheet "+(index+1)+" / "+result.Pages.Count;
            if(footer>=0)page.Labels[footer]=page.Labels[footer] with{Text=text};
            else page.Text(0,PageHeight-8.5,text,7,"#52677c",false,PageWidth);
            // The first switch's own header is at y=0; no extra title bar is needed.
            if(logo!=null)logo.Apply(page);
            var errors=Charts.ValidateScene(page);
            if(errors.Count>0)throw new OnePageCapacityException("workspace sheet "+(index+1)+": "+string.Join("; ",errors));
        }
        return result;
    }

    static string Normalize(string? value)=>(value??"").Trim().Normalize(NormalizationForm.FormC);
    static List<LegendEntry> Entries(IReadOnlyList<WorkspaceSheetItem> items)
    {
        var rows=new Dictionary<string,(int Id,List<(WorkspaceSheetItem Item,string[] Names,bool Voice,string Color)> Sources)>(StringComparer.Ordinal);
        foreach(var item in items)
        {
            var colors=VlanPalette.Assign(item.Inventory);
            foreach(int id in WorkspacePalette.VlanIds(item.Inventory))
            {
                string key=WorkspacePalette.VlanKey(item.Id,item.Inventory,id);
                var names=item.Inventory.Vlans.Where(vlan=>vlan.Id==id).Select(vlan=>Normalize(vlan.Name)).Where(name=>name.Length>0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                bool voice=item.Inventory.Vlans.Any(vlan=>vlan.Id==id&&vlan.Voice)||item.Inventory.Ports.Any(port=>port.VoiceVlan==id);
                if(!rows.TryGetValue(key,out var row)){row=(id,new());rows.Add(key,row);}
                row.Sources.Add((item,names,voice,colors[id]));
            }
        }
        var entries=new List<LegendEntry>();
        foreach(var pair in rows.OrderBy(pair=>pair.Value.Id).ThenBy(pair=>pair.Key,StringComparer.Ordinal))
        {
            var row=pair.Value;var first=row.Sources[0];bool scoped=first.Names.Length!=1;
            if(row.Sources.Select(source=>source.Color).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1)
                throw new InvalidOperationException("Shared VLAN identity has contradictory colors: "+pair.Key);
            string name=first.Names.Length==0?"Unnamed":string.Join(" / ",first.Names);
            bool conflict=rows.Count(other=>other.Value.Id==row.Id)>1;
            string scope=scoped||conflict?" ["+string.Join("; ",row.Sources.Select(source=>source.Item.Ordinal+". "+ReportText.DisplayName(source.Item.Inventory)+" / "+source.Item.Name).Distinct())+"]":"";
            string voice=row.Sources.All(source=>source.Voice)?" (Voice)":row.Sources.Any(source=>source.Voice)?" (Voice on "+string.Join("; ",row.Sources.Where(source=>source.Voice).Select(source=>ReportText.DisplayName(source.Item.Inventory)).Distinct())+")":"";
            string label=row.Id+" "+name+voice+(first.Names.Length>1?" (ambiguous names)":"")+scope;
            entries.Add(new(pair.Key,row.Id,label,first.Color,row.Sources.Any(source=>source.Voice)));
        }
        if(items.Any(item=>item.Inventory.Ports.Any(port=>!port.StackLink&&!port.UntaggedVlan.HasValue&&port.UntaggedState=="Unknown")))entries.Add(new("state:unknown",-1,"Native not supplied","#e6e6e6",Marker:false));
        if(items.Any(item=>item.Inventory.Ports.Any(port=>!port.StackLink&&!port.UntaggedVlan.HasValue&&port.UntaggedState=="None")))entries.Add(new("state:none",-1,"No native VLAN","#ffffff",Marker:false));
        return entries;
    }
    static List<string> Wrap(string text,double width)
    {
        var lines=new List<string>();string line="";
        foreach(string word in text.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {
            string next=line.Length==0?word:line+" "+word;
            if(ReportRenderer.Measure(new Label(0,0,next,LegendFont)).Width<=width){line=next;continue;}
            if(line.Length>0){lines.Add(line);line="";}
            string rest=word;
            while(rest.Length>0&&ReportRenderer.Measure(new Label(0,0,rest,LegendFont)).Width>width)
            {
                int count=1;while(count<rest.Length&&ReportRenderer.Measure(new Label(0,0,rest[..(count+1)],LegendFont)).Width<=width)count++;
                lines.Add(rest[..count]);rest=rest[count..];
            }
            line=rest;
        }
        if(line.Length>0)lines.Add(line);return lines;
    }
    static LegendPlan PlanLegend(IReadOnlyList<WorkspaceSheetItem> items)
    {
        var entries=Entries(items);int columns=Math.Max(1,Math.Min(4,entries.Count));
        double width=(PageWidth-12*(columns-1))/columns,lineHeight=ReportRenderer.Measure(new Label(0,0,"Ag",LegendFont)).Height+.6;
        var lines=entries.Select(entry=>Wrap(entry.Label,width-LegendInset)).ToList();double height=LegendHeading;
        for(int index=0;index<entries.Count;index+=columns)height+=Math.Max(8,lines.Skip(index).Take(columns).Max(line=>line.Count)*lineHeight)+3;
        return new(entries,lines,columns,width,lineHeight,height);
    }
    static void DrawLegend(Scene page,LegendPlan plan,double y,double radius)
    {
        page.Rect(0,y,PageWidth,12,"#e7eff6","#c3d2df",2,"Heading");page.Text(5,y+1,"VLAN key",8.5,"#20374e",true,PageWidth-10,inkBox:true);y+=LegendHeading;
        for(int index=0;index<plan.Entries.Count;index+=plan.Columns)
        {
            double rowHeight=Math.Max(8,plan.Lines.Skip(index).Take(plan.Columns).Max(lines=>lines.Count)*plan.LineHeight);
            for(int column=0;column<plan.Columns&&index+column<plan.Entries.Count;column++)
            {
                var entry=plan.Entries[index+column];double x=column*(plan.ColumnWidth+12);
                page.Rect(x,y+1,6,6,entry.Color,"#687b8b");
                if(entry.Marker)page.Dots.Add(new(x+12,y+4,radius,entry.Color,entry.Voice,0,QuickSheet.MarkerHalo,QuickSheet.MarkerStroke));
                var lines=plan.Lines[index+column];for(int line=0;line<lines.Count;line++)page.Text(x+LegendInset,y+line*plan.LineHeight,lines[line],LegendFont,"#30465c",false,plan.ColumnWidth-LegendInset);
            }
            y+=rowHeight+3;
        }
    }
    static void Append(Scene target,Scene source,double dy,string id,int ordinal,string name)
    {
        string Owner(string value)=>value.Length==0?"":id+":"+value;
        foreach(var box in source.Boxes)target.Boxes.Add(box with{Y=box.Y+dy});
        foreach(var label in source.Labels)target.Labels.Add(label with{Y=label.Y+dy,Owner=Owner(label.Owner),Text=label.Text=="NETWORK PORT MAP"?"NETWORK PORT MAP | "+ordinal+". "+name:label.Text});
        foreach(var dot in source.Dots)target.Dots.Add(dot with{Y=dot.Y+dy});
        foreach(var shape in source.Shapes)target.Shapes.Add(shape with{Points=shape.Points.Select(point=>(point.X,point.Y+dy)).ToArray(),Owner=Owner(shape.Owner),ClipBounds=shape.ClipBounds is {} bounds?bounds with{Y=bounds.Y+dy}:null});
        foreach(var image in source.Images)target.Images.Add(image with{Y=image.Y+dy});
    }
}
