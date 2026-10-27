using System.Text;
using System.Text.Json;
namespace SwitchportGenerator;

public sealed record WorkspaceSheetItem(string Id,string Name,Inventory Inventory,string Hash="",int Ordinal=0);
public sealed record WorkspaceSheetPlacement(string Id,string Name,int PageNumber,double Y,double Height,double GeometryScale=1,bool Standalone=false,bool InlineTitle=false);
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

/// <summary>Ordered A4 switch blocks with one ID-based VLAN key down the side of each sheet.</summary>
public static class WorkspaceSheet
{
    public const double PageWidth=805.89,PageHeight=559.28,BlockGap=6,ContentTop=0;
    const double ContentBottom=QuickSheet.DiagramBottom,LegendGap=6,LegendFont=7.5,LegendHeading=24,LegendInset=21;
    const double MinimumBodyWidth=580,PreferredLegendWidth=156,MinimumLegendWidth=50,MaximumBodyWidth=PageWidth-LegendGap-MinimumLegendWidth;
    sealed record Component(WorkspaceSheetItem Item,QuickSheet.Result Layout,bool Standalone=false,bool InlineTitle=false);
    sealed record Block(WorkspaceSheetItem Item,Scene Scene,bool Continuation=false,bool Standalone=false,bool InlineTitle=false);
    sealed record LegendEntry(string Identity,int VlanId,string Label,string Color,bool Voice=false,bool Marker=true);
    sealed record LegendPlan(List<LegendEntry> Entries,List<List<string>> Lines,double Width,double LineHeight,double Height);

    public static WorkspaceSheetResult Build(IReadOnlyList<WorkspaceSheetItem> items,ReportLogo? logo=null,string? title=null)
    {
        // A document title is explicitly opt-in; no generic workspace title is added.
        title=string.IsNullOrWhiteSpace(title)?null:title.Trim();
        var titleLines=title==null?new List<string>():Wrap(title,PageWidth-24,14,true);
        double titleHeight=titleLines.Count==0?0:titleLines.Count*17+12,top=titleHeight+ContentTop;
        if(items.Count==0)throw new InvalidOperationException("Add at least one valid configuration to build a workspace sheet.");
        if(items.Any(item=>string.IsNullOrWhiteSpace(item.Id))||items.Select(item=>item.Id).Distinct(StringComparer.Ordinal).Count()!=items.Count)
            throw new InvalidOperationException("Workspace sheet item identifiers must be nonempty and distinct.");
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
            components.Add(Prepare(item,placements,ContentBottom-top,title));
        }
        double radius=Math.Min(QuickSheet.DotRadius,components.Select(component=>component.Layout.MarkerRadius).DefaultIfEmpty(QuickSheet.DotRadius).Min());result.MarkerRadius=radius;
        for(int index=0;index<components.Count;index++)
        {
            var current=components[index];
            if(Math.Abs(current.Layout.MarkerRadius-radius)>1e-5)
            {
                if(current.Standalone)
                {
                    for(int dot=0;dot<current.Layout.Scene.Dots.Count;dot++)current.Layout.Scene.Dots[dot]=current.Layout.Scene.Dots[dot] with{Radius=radius};
                    current.Layout.Scene.MarkerRadius=radius;
                }
                else components[index]=Prepare(current.Item,Charts.Layout(current.Item.Inventory),ContentBottom-top,title,radius);
            }
        }
        var guideInventory=new Inventory{Ports=prepared.SelectMany(item=>item.Inventory.Ports).ToList(),Vlans=prepared.SelectMany(item=>item.Inventory.Vlans).ToList()};
        var guideVlans=prepared.SelectMany(item=>VlanPalette.Assign(item.Inventory)).GroupBy(pair=>pair.Key).ToDictionary(group=>group.Key,group=>group.First().Value);
        var guideLags=palette.Items.Values.SelectMany(item=>item.Aggregates).GroupBy(pair=>pair.Key,StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>group.First().Value,StringComparer.OrdinalIgnoreCase);
        var blocks=components.SelectMany(component=>new[]{new Block(component.Item,component.Layout.Scene,Standalone:component.Standalone,InlineTitle:component.InlineTitle)}
            .Concat(component.Layout.DetailContinuations.Select(scene=>new Block(component.Item,scene,true)))).ToArray();
        foreach(var component in components.Where(component=>component.Layout.DetailContinuations.Count>0))
            result.Warnings.Add("Trunk / port-description details for "+ReportText.DisplayName(component.Item.Inventory)+" continue onto "+component.Layout.DetailContinuations.Count+" additional block(s), at readable text size.");
        var group=new List<Block>();double bodyHeight=0;
        void Flush()
        {
            if(group.Count==0)return;
            double bodyWidth=Math.Max(MinimumBodyWidth,group.Max(component=>component.Scene.DiagramBodyWidth));
            var legends=PlanLegend(group.Select(component=>component.Item).DistinctBy(item=>item.Id).ToArray(),PageWidth-bodyWidth-LegendGap,ContentBottom-top);
            for(int part=0;part<legends.Count;part++)
            {
                var page=new Scene{Width=PageWidth,Height=PageHeight,DiagramScale=1,MarkerRadius=radius,DiagramBodyWidth=bodyWidth,IsAdaptiveLayout=true};
                result.Pages.Add(page);double y=top;
                if(titleLines.Count>0)
                {
                    page.Rect(0,0,PageWidth,titleHeight-4,"#13283d","#13283d",4,"DocumentTitle");
                    for(int line=0;line<titleLines.Count;line++)page.Text(12,5+line*17,titleLines[line],14,"#ffffff",true,PageWidth-24);
                }
                QuickSheet.DrawVisualGuide(page,guideInventory,guideVlans,guideLags);
                foreach(var component in group)
                {
                    Append(page,component.Scene,y,bodyWidth,component.Item.Id,component.Item.Ordinal,component.Item.Name);
                    // Continuation sheets repeat the original switch context; the
                    // ordered placement index points to each switch's first page.
                    if(part==0&&!component.Continuation)result.Placements.Add(new(component.Item.Id,component.Item.Name,result.Pages.Count,y,component.Scene.Height));
                    y+=component.Scene.Height+BlockGap;
                }
                DrawLegend(page,legends[part],bodyWidth+LegendGap,radius,part+1,legends.Count,top);
                foreach(var entry in legends[part].Entries.Where(entry=>entry.VlanId>=0))
                    result.LegendEntries.Add(new(entry.Identity,entry.VlanId,entry.Label,entry.Color,entry.Voice,result.Pages.Count));
                // Apply a single logo within the first switch's own left-column header.
                if(logo!=null){page.Width=bodyWidth;logo.Apply(page,top);page.Width=PageWidth;}
            }
            if(legends.Count>1)result.Warnings.Add("The side VLAN key for "+string.Join(", ",group.Select(component=>component.Item.Name))+" continues across "+legends.Count+" sheets; switch context is repeated so all names remain readable.");
            group.Clear();bodyHeight=0;
        }
        foreach(var component in blocks)
        {
            if(component.Standalone)
            {
                Flush();
                var source=component.Scene;double pageTop=component.InlineTitle?0:top;
                var page=new Scene{Width=PageWidth,Height=PageHeight,DiagramScale=source.DiagramScale,MarkerRadius=radius,IsAdaptiveLayout=true};
                if(titleLines.Count>0&&!component.InlineTitle)
                {
                    page.Rect(0,0,PageWidth,titleHeight-4,"#13283d","#13283d",4,"DocumentTitle");
                    for(int line=0;line<titleLines.Count;line++)page.Text(12,5+line*17,titleLines[line],14,"#ffffff",true,PageWidth-24);
                }
                Append(page,source,pageTop,PageWidth,component.Item.Id,component.Item.Ordinal,component.Item.Name,true);
                if(component.InlineTitle)
                {
                    int header=page.Labels.FindIndex(label=>label.Text=="NETWORK PORT MAP");
                    var label=page.Labels[header] with{Text=title!};
                    if(ReportRenderer.Measure(label).Text!=title)throw new OnePageCapacityException("workspace title is too long for the complete switch page; shorten the title or disable it");
                    page.Labels[header]=label;
                    result.Warnings.Add("The workspace title shares the switch header on a dedicated page to keep the complete configuration together.");
                }
                page.DiagramBodyWidth=page.Labels.Single(label=>label.Text=="VLAN key").X-5;
                QuickSheet.DrawVisualGuide(page,guideInventory,guideVlans,guideLags);
                if(logo!=null)logo.Apply(page,pageTop);
                result.Pages.Add(page);
                result.Placements.Add(new(component.Item.Id,component.Item.Name,result.Pages.Count,pageTop,source.Height,source.DiagramScale,true));
                foreach(var entry in Entries(new[]{component.Item}).Where(entry=>entry.VlanId>=0))
                {
                    var vlan=component.Item.Inventory.Vlans.FirstOrDefault(vlan=>vlan.Id==entry.VlanId);
                    string label=(entry.VlanId+(entry.Voice?" Voice":"")+" "+(vlan?.Name??"")).TrimEnd();
                    result.LegendEntries.Add(new(entry.Identity,entry.VlanId,label,entry.Color,entry.Voice,result.Pages.Count));
                }
                result.Warnings.Add(ReportText.DisplayName(component.Item.Inventory)+" uses a dedicated complete A4 layout so its LAGs and descriptions stay with the switch.");
                continue;
            }
            double candidateHeight=bodyHeight+(group.Count==0?0:BlockGap)+component.Scene.Height;
            if(candidateHeight>ContentBottom-top&&group.Count>0)
            {
                Flush();candidateHeight=component.Scene.Height;
                result.Warnings.Add("A new sheet starts with "+component.Item.Name+" so complete switch blocks remain readable beside the VLAN key.");
            }
            if(candidateHeight>ContentBottom-top)throw new OnePageCapacityException(component.Item.Name+" cannot fit beside its side VLAN key at readable reference geometry; its individual export remains available");
            group.Add(component);bodyHeight=candidateHeight;
        }
        Flush();
        for(int index=0;index<result.Pages.Count;index++)
        {
            var page=result.Pages[index];page.Text(0,PageHeight-8.5,ReportText.CableMoveDisclaimer+"  |  Sheet "+(index+1)+" / "+result.Pages.Count,7,"#52677c",false,PageWidth);
            var errors=Charts.ValidateScene(page);
            if(errors.Count>0)throw new OnePageCapacityException("workspace sheet "+(index+1)+": "+string.Join("; ",errors));
        }
        return result;
    }

    static Component Prepare(WorkspaceSheetItem item,List<Placement> placements,double availableHeight,string? title,double? radius=null)
    {
        // Measure a whole fresh-page block, never the leftover space on a page.
        // Only genuinely oversized detail sets may reach continuation planning.
        foreach(double width in new[]{PageWidth-LegendGap-PreferredLegendWidth,MaximumBodyWidth})
            try{return new(item,QuickSheet.BuildCompact(item.Inventory,placements,item.Hash,radius,false,width,false,availableHeight));}
            catch(OnePageCapacityException){}
        // Dedicated pages still retain all aliases for a numeric VLAN identity.
        // This is the already-cloned render inventory, never the original source.
        item.Inventory.Vlans=item.Inventory.Vlans.GroupBy(vlan=>vlan.Id).Select(group=>new Vlan{Id=group.Key,Voice=group.Any(vlan=>vlan.Voice),Name=string.Join(" / ",group.Select(vlan=>Normalize(vlan.Name)).Where(name=>name.Length>0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}).ToList();
        try{return new(item,QuickSheet.StandaloneComponent(item.Inventory,placements,item.Hash,availableHeight),true);}
        catch(OnePageCapacityException){}
        if(title!=null)
        {
            try{return new(item,QuickSheet.StandaloneComponent(item.Inventory,placements,item.Hash,ContentBottom),true,true);}
            catch(OnePageCapacityException){}
        }
        try
        {
            try{return new(item,QuickSheet.BuildCompact(item.Inventory,placements,item.Hash,radius,false,PageWidth-LegendGap-PreferredLegendWidth,true,availableHeight));}
            catch(OnePageCapacityException){return new(item,QuickSheet.BuildCompact(item.Inventory,placements,item.Hash,radius,false,MaximumBodyWidth,true,availableHeight));}
        }
        catch(OnePageCapacityException ex)
        {throw new InvalidOperationException("Configuration "+ReportText.DisplayName(item.Inventory)+" ("+item.Name+") could not be laid out: "+ex.Message,ex);}
    }

    static string Normalize(string? value)=>(value??"").Trim().Normalize(NormalizationForm.FormC);
    static List<LegendEntry> Entries(IReadOnlyList<WorkspaceSheetItem> items)
    {
        var rows=new Dictionary<int,List<(WorkspaceSheetItem Item,string[] Names,bool Voice,string Color)>>();
        foreach(var item in items)
        {
            var colors=VlanPalette.Assign(item.Inventory);
            foreach(int id in WorkspacePalette.VlanIds(item.Inventory))
            {
                var names=item.Inventory.Vlans.Where(vlan=>vlan.Id==id).Select(vlan=>Normalize(vlan.Name)).Where(name=>name.Length>0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                bool voice=item.Inventory.Vlans.Any(vlan=>vlan.Id==id&&vlan.Voice)||item.Inventory.Ports.Any(port=>port.VoiceVlan==id);
                if(!rows.TryGetValue(id,out var sources)){sources=new();rows.Add(id,sources);}
                sources.Add((item,names,voice,colors[id]));
            }
        }
        var entries=new List<LegendEntry>();
        foreach(var pair in rows.OrderBy(pair=>pair.Key))
        {
            var sources=pair.Value;
            if(sources.Select(source=>source.Color).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1)
                throw new InvalidOperationException("Shared VLAN ID has contradictory colors: "+pair.Key);
            var aliases=sources.SelectMany(source=>source.Names).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string name=aliases.Length==0?"Unnamed":string.Join(" / ",aliases);
            string voice=sources.All(source=>source.Voice)?" (Voice)":sources.Any(source=>source.Voice)?" (Voice on "+string.Join("; ",sources.Where(source=>source.Voice).Select(source=>ReportText.DisplayName(source.Item.Inventory)).Distinct())+")":"";
            entries.Add(new("vlan:"+pair.Key,pair.Key,pair.Key+" "+name+voice,sources[0].Color,sources.Any(source=>source.Voice)));
        }
        if(items.Any(item=>item.Inventory.Ports.Any(port=>!port.StackLink&&!port.UntaggedVlan.HasValue&&port.UntaggedState=="Unknown")))entries.Add(new("state:unknown",-1,"Native not supplied","#e6e6e6",Marker:false));
        if(items.Any(item=>item.Inventory.Ports.Any(port=>!port.StackLink&&!port.UntaggedVlan.HasValue&&port.UntaggedState=="None")))entries.Add(new("state:none",-1,"No native VLAN","#ffffff",Marker:false));
        return entries;
    }
    static List<string> Wrap(string text,double width,double font=LegendFont,bool bold=false)
    {
        var lines=new List<string>();string line="";
        foreach(string word in text.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {
            string next=line.Length==0?word:line+" "+word;
            if(ReportRenderer.Measure(new Label(0,0,next,font,"#30465c",bold)).Width<=width){line=next;continue;}
            if(line.Length>0){lines.Add(line);line="";}
            string rest=word;
            while(rest.Length>0&&ReportRenderer.Measure(new Label(0,0,rest,font,"#30465c",bold)).Width>width)
            {
                int count=1;while(count<rest.Length&&ReportRenderer.Measure(new Label(0,0,rest[..(count+1)],font,"#30465c",bold)).Width<=width)count++;
                lines.Add(rest[..count]);rest=rest[count..];
            }
            line=rest;
        }
        if(line.Length>0)lines.Add(line);return lines;
    }
    static List<LegendPlan> PlanLegend(IReadOnlyList<WorkspaceSheetItem> items,double width,double availableHeight)
    {
        double lineHeight=ReportRenderer.Measure(new Label(0,0,"Ag",LegendFont)).Height+.6;
        var pages=new List<LegendPlan>();var entries=new List<LegendEntry>();var lines=new List<List<string>>();double height=LegendHeading;
        void Flush(){pages.Add(new(entries,lines,width,lineHeight,height));entries=new();lines=new();height=LegendHeading;}
        foreach(var entry in Entries(items))
        {
            var wrapped=Wrap(entry.Label,width-LegendInset);
            int maximumLines=Math.Max(1,(int)Math.Floor((availableHeight-LegendHeading-3)/lineHeight)-3);
            for(int offset=0;offset<wrapped.Count;offset+=maximumLines)
            {
                var segment=wrapped.Skip(offset).Take(maximumLines).ToList();
                if(offset>0)segment.InsertRange(0,Wrap(entry.VlanId>=0?entry.VlanId+" continued":"Continued",width-LegendInset));
                double rowHeight=Math.Max(8,segment.Count*lineHeight)+3;
                if(height+rowHeight>availableHeight&&entries.Count>0)Flush();
                entries.Add(wrapped.Count<=maximumLines?entry:entry with{Label=string.Join(" ",segment)});lines.Add(segment);height+=rowHeight;
            }
        }
        if(entries.Count>0||pages.Count==0)Flush();return pages;
    }
    static void DrawLegend(Scene page,LegendPlan plan,double x,double radius,int part,int parts,double top)
    {
        page.Rect(x,top,plan.Width,12,"#e7eff6","#c3d2df",2,"Heading");page.Text(x+5,top+1,"VLAN key",8.5,"#20374e",true,plan.Width-10,inkBox:true);
        if(parts>1)page.Text(x,top+13,"Key page "+part+" / "+parts,7,"#52677c",false,plan.Width);
        double y=top+LegendHeading;
        for(int index=0;index<plan.Entries.Count;index++)
        {
            var entry=plan.Entries[index];page.Rect(x,y+1,6,6,entry.Color,"#687b8b");
            if(entry.Marker)page.Dots.Add(new(x+12,y+4,radius,entry.Color,entry.Voice,0,QuickSheet.MarkerHalo,QuickSheet.MarkerStroke));
            var lines=plan.Lines[index];for(int line=0;line<lines.Count;line++)page.Text(x+LegendInset,y+line*plan.LineHeight,lines[line],LegendFont,"#30465c",false,plan.Width-LegendInset);
            y+=Math.Max(8,lines.Count*plan.LineHeight)+3;
        }
    }
    static void Append(Scene target,Scene source,double dy,double bodyWidth,string id,int ordinal,string name,bool preserveWidth=false)
    {
        string Owner(string value)=>value.Length==0?"":id+":"+value;
        double Shift(double y)=>!preserveWidth&&y>=QuickSheet.DiagramTop?(bodyWidth-PageWidth)/2:0;
        double Y(double y)=>y+dy;
        foreach(var box in source.Boxes)target.Boxes.Add(box with{X=box.X+Shift(box.Y),Y=Y(box.Y),W=!preserveWidth&&box.Y<37&&box.X==0?bodyWidth:box.W});
        foreach(var original in source.Labels)
        {
            var label=original;double x=label.X+Shift(label.Y),width=label.Width;
            if(!preserveWidth&&label.Y<37)
            {
                double split=bodyWidth*.52;
                if(label.X>=420){x=split;width=bodyWidth-x-12;}else width=split-label.X-12;
            }
            target.Labels.Add(label with{X=x,Y=Y(label.Y),Width=width,Owner=Owner(label.Owner),Text=label.Text});
        }
        foreach(var dot in source.Dots)target.Dots.Add(dot with{X=dot.X+Shift(dot.Y),Y=Y(dot.Y)});
        foreach(var shape in source.Shapes)
        {
            double dx=Shift(shape.Points.Min(point=>point.Y));
            target.Shapes.Add(shape with{Points=shape.Points.Select(point=>(point.X+dx,Y(point.Y))).ToArray(),Owner=Owner(shape.Owner),ClipBounds=shape.ClipBounds is {} bounds?bounds with{X=bounds.X+dx,Y=Y(bounds.Y)}:null});
        }
        foreach(var image in source.Images)target.Images.Add(image with{X=image.X+Shift(image.Y),Y=Y(image.Y)});
    }
}
