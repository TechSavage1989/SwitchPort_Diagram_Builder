using HUBTechGenerator;
using SkiaSharp;
string root=args[0];
void Check(bool value,string reason){if(!value)throw new Exception(reason);}
var inventory=Parser.Parse(File.ReadAllText(Path.Combine(root,"config.txt")));
foreach(string file in new[]{"wide.png","tall.png","square.png","wide.jpg","wide.webp"})
{
 var logo=ReportLogo.Load(Path.Combine(root,file));
 var chart=Charts.Create(inventory,Charts.Layout(inventory),"TEST");
 var pages=Charts.Booklet(inventory,chart,"TEST");
 foreach(var page in pages)
 {
  var boxes=page.Boxes.Count;var dots=page.Dots.Count;var shapes=page.Shapes.Count;var size=(page.Width,page.Height);
  logo.Apply(page);
  Check(page.Images.Count==1&&page.Boxes.Count==boxes+1&&page.Dots.Count==dots&&page.Shapes.Count==shapes&&size==(page.Width,page.Height),"Logo changes no port/VLAN geometry or page dimensions");
  var image=page.Images.Single();Check(Math.Abs(image.W/image.H-(double)logo.Width/logo.Height)<.00001,"Logo preserves aspect ratio");
  Check(Charts.ValidateScene(page).Count==0,"Logo/header must not collide: "+string.Join(';',Charts.ValidateScene(page)));
  page.Labels.Add(new Label(image.X,image.Y,"COLLISION",5));
  Check(Charts.ValidateScene(page).Contains("Text collides with report logo."),"Injected label/logo overlap must fail");
 }
 Console.WriteLine("PASS "+file+": proportional image, every page, unchanged diagram geometry, no header collisions");
}
bool rejected=false;try{ReportLogo.Load(Path.Combine(root,"invalid.png"));}catch{rejected=true;}Check(rejected,"Invalid images are rejected");
using(var bitmap=new SKBitmap(2048,64))
{
 bitmap.Erase(SKColors.Blue);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);File.WriteAllBytes(Path.Combine(root,"large.png"),data.ToArray());
}
var reduced=ReportLogo.Load(Path.Combine(root,"large.png"));Check(reduced.Width==1024&&reduced.Height==32,"Large logos downsample while preserving proportions");
Console.WriteLine("PASS invalid image rejection and bounded normalized logo size");
