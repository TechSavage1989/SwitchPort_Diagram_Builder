using SkiaSharp;
namespace HUBTechGenerator;

public sealed class ReportLogo
{
    public byte[] PngData {get;}
    public int Width {get;}
    public int Height {get;}
    ReportLogo(byte[] png,int width,int height){PngData=png;Width=width;Height=height;}
    public static ReportLogo Load(string path)
    {
        var file=new FileInfo(path);
        if(!file.Exists||file.Length==0||file.Length>10_000_000)throw new InvalidOperationException("Choose a PNG, JPG or WebP logo under 10 MB.");
        using var codec=SKCodec.Create(path);
        if(codec==null||codec.Info.Width<1||codec.Info.Height<1||(long)codec.Info.Width*codec.Info.Height>32_000_000)
            throw new InvalidOperationException("The logo cannot be read. Choose a PNG, JPG or WebP image under 32 megapixels.");
        if(codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
            throw new InvalidOperationException("Logo format is unsupported. Use PNG, JPG or WebP.");
        using var bitmap=SKBitmap.Decode(codec)??throw new InvalidOperationException("The logo image is damaged or unsupported.");
        using var source=SKImage.FromBitmap(bitmap);
        double scale=Math.Min(1,1024.0/Math.Max(bitmap.Width,bitmap.Height));
        int width=Math.Max(1,(int)Math.Round(bitmap.Width*scale)),height=Math.Max(1,(int)Math.Round(bitmap.Height*scale));
        using var surface=SKSurface.Create(new SKImageInfo(width,height));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(source,new SKRect(0,0,width,height),new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.Linear));
        using var image=surface.Snapshot();using var data=image.Encode(SKEncodedImageFormat.Png,100);
        return new ReportLogo(data.ToArray(),width,height);
    }
    public void Apply(Scene scene)
    {
        var header=scene.Boxes.FirstOrDefault(b=>b.X==0&&b.Y==0&&b.Fill=="#13283d");
        if(header==null)throw new InvalidOperationException("The report header is unavailable for a logo.");
        double width=scene.Width*.14,height=header.H-12,x=scene.Width-width-8,y=6;
        for(int i=0;i<scene.Labels.Count;i++)
        {
            var label=scene.Labels[i];
            if(label.Y<header.H&&label.X<x&&(label.Width==0||label.X+label.Width>x-10))scene.Labels[i]=label with{Width=x-10-label.X};
        }
        scene.Rect(x,y,width,height,"#ffffff","#ffffff",2,"ReportLogo");
        double scale=Math.Min((width-8)/Width,(height-6)/Height);
        double w=Width*scale,h=Height*scale;
        scene.Images.Add(new(x+(width-w)/2,y+(height-h)/2,w,h,PngData));
    }
}
