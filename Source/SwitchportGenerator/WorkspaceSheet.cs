namespace SwitchportGenerator;

public sealed record WorkspaceSheetItem(string Id,string Name,Inventory Inventory,string Hash="");
public sealed record WorkspaceSheetPlacement(string Id,string Name,int PageNumber,double Y,double Height,double GeometryScale=1,bool Standalone=false);
public sealed class WorkspaceSheetResult
{
    public List<Scene> Pages {get;}=new();
    public List<WorkspaceSheetPlacement> Placements {get;}=new();
    public List<string> Warnings {get;}=new();
    public double GeometryScale=>1;
    public double MarkerRadius {get;internal set;}=QuickSheet.DotRadius;
}

/// <summary>Ordered, readable A4 sheets containing as many complete switch blocks as fit.</summary>
public static class WorkspaceSheet
{
    public const double PageWidth=805.89,PageHeight=559.28,BlockGap=8,ContentTop=45;
    const double ContentBottom=PageHeight-11;

    public static WorkspaceSheetResult Build(IReadOnlyList<WorkspaceSheetItem> items,ReportLogo? logo=null,string? title=null)
    {
        if(items.Count==0)throw new InvalidOperationException("Add at least one valid configuration to build a workspace sheet.");
        if(items.Select(item=>item.Id).Distinct(StringComparer.Ordinal).Count()!=items.Count)
            throw new InvalidOperationException("Workspace sheet item identifiers must be distinct.");
        var result=new WorkspaceSheetResult();
        var components=new List<(WorkspaceSheetItem Item,QuickSheet.Result Layout,bool Standalone)>();
        foreach(var item in items)
        {
            if(item.Inventory.Blockers.Count>0)throw new InvalidOperationException("Resolve configuration validation blockers before adding "+item.Name+" to a workspace sheet.");
            var placements=Charts.Layout(item.Inventory);var errors=Charts.Validate(item.Inventory,placements);
            if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
            QuickSheet.Result layout;bool standalone=false;
            try
            {
                layout=QuickSheet.BuildCompact(item.Inventory,placements,item.Hash);
                if(layout.Scene.Height>ContentBottom-ContentTop)standalone=true;
            }
            catch(OnePageCapacityException){layout=QuickSheet.Build(item.Inventory,placements,item.Hash);standalone=true;}
            if(standalone)
            {
                // Already-supported dense/chassis layouts must never disappear merely
                // because a shared title consumes space. Preserve their existing whole
                // page, without mixing a different physical scale on a shared sheet.
                if(layout.Scene.Height<PageHeight)layout=QuickSheet.Build(item.Inventory,placements,item.Hash);
                result.Warnings.Add(item.Name+" requires a dedicated A4 sheet to retain all interfaces, markers and key text at readable size; its existing standalone geometry is preserved.");
            }
            components.Add((item,layout,standalone));
        }
        // Shared sheets use identical socket geometry and one marker diameter.
        // Dense standalone fallbacks retain their established readable renderer.
        double radius=components.Where(component=>!component.Standalone).Select(component=>component.Layout.MarkerRadius).DefaultIfEmpty(QuickSheet.DotRadius).Min();
        result.MarkerRadius=radius;
        for(int index=0;index<components.Count;index++)
        {
            var current=components[index];
            if(!current.Standalone&&Math.Abs(current.Layout.MarkerRadius-radius)>1e-5)
                components[index]=(current.Item,QuickSheet.BuildCompact(current.Item.Inventory,Charts.Layout(current.Item.Inventory),current.Item.Hash,radius),false);
        }
        Scene? page=null;double y=ContentTop;
        for(int index=0;index<components.Count;index++)
        {
            var component=components[index];double height=component.Layout.Scene.Height;
            if(component.Standalone)
            {
                page=null;
                var standalone=new Scene{Width=PageWidth,Height=PageHeight,DiagramScale=component.Layout.GeometryScale,MarkerRadius=component.Layout.MarkerRadius,IsAdaptiveLayout=true};
                Append(standalone,component.Layout.Scene,0,component.Item.Id,index+1,component.Item.Name);
                result.Pages.Add(standalone);
                result.Placements.Add(new(component.Item.Id,component.Item.Name,result.Pages.Count,0,PageHeight,component.Layout.GeometryScale,true));
                continue;
            }
            if(page==null||y+height>ContentBottom)
            {
                if(page!=null)result.Warnings.Add("Sheet "+(result.Pages.Count+1)+" starts with "+component.Item.Name+" because the next complete diagram and key do not fit on the previous sheet at readable size.");
                page=NewPage(title??"Ordered switch configurations",items.Count,radius);
                result.Pages.Add(page);y=ContentTop;
            }
            Append(page,component.Layout.Scene,y,component.Item.Id,index+1,component.Item.Name);
            result.Placements.Add(new(component.Item.Id,component.Item.Name,result.Pages.Count,y,height));
            y+=height+BlockGap;
        }
        for(int index=0;index<result.Pages.Count;index++)
        {
            var sheet=result.Pages[index];
            var footer=sheet.Labels.FindIndex(label=>label.Text==ReportText.CableMoveDisclaimer);
            if(footer>=0)sheet.Labels[footer]=sheet.Labels[footer] with{Text=ReportText.CableMoveDisclaimer+"  |  Sheet "+(index+1)+" / "+result.Pages.Count};
            else sheet.Text(0,PageHeight-8.5,ReportText.CableMoveDisclaimer+"  |  Sheet "+(index+1)+" / "+result.Pages.Count,7,"#52677c",false,PageWidth);
            if(logo!=null)logo.Apply(sheet);
            var errors=Charts.ValidateScene(sheet);
            if(errors.Count>0)throw new OnePageCapacityException("workspace sheet "+(index+1)+": "+string.Join("; ",errors));
        }
        return result;
    }

    static Scene NewPage(string title,int count,double radius)
    {
        var scene=new Scene{Width=PageWidth,Height=PageHeight,DiagramScale=1,MarkerRadius=radius,IsAdaptiveLayout=true};
        scene.Rect(0,0,PageWidth,37,"#13283d","#13283d",4);
        scene.Rect(0,35,PageWidth,2,"#4ca6ce","#4ca6ce");
        scene.Text(12,5,"WORKSPACE PORT MAPS",8.5,"#d5e7f4",true,390);
        scene.Text(12,20,title,12,"#ffffff",true,PageWidth-24);
        scene.Text(420,7,count+" configurations | Workspace order",9,"#ffffff",true,PageWidth-432);
        return scene;
    }
    static void Append(Scene target,Scene source,double dy,string id,int ordinal,string name)
    {
        string Owner(string value)=>value.Length==0?"":id+":"+value;
        foreach(var box in source.Boxes)target.Boxes.Add(box with{Y=box.Y+dy});
        foreach(var label in source.Labels)
        {
            string text=label.Text=="NETWORK PORT MAP"?"NETWORK PORT MAP | "+ordinal+". "+name:label.Text;
            target.Labels.Add(label with{Y=label.Y+dy,Owner=Owner(label.Owner),Text=text});
        }
        foreach(var dot in source.Dots)target.Dots.Add(dot with{Y=dot.Y+dy});
        foreach(var shape in source.Shapes)target.Shapes.Add(shape with
        {
            Points=shape.Points.Select(point=>(point.X,point.Y+dy)).ToArray(),Owner=Owner(shape.Owner),
            ClipBounds=shape.ClipBounds is {} bounds?bounds with{Y=bounds.Y+dy}:null
        });
        foreach(var image in source.Images)target.Images.Add(image with{Y=image.Y+dy});
    }
}
