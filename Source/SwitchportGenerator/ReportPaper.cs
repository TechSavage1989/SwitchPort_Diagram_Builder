using System.Text.Json.Serialization;

namespace SwitchportGenerator;

/// <summary>
/// Report paper is saved separately from source configuration and hardware.
/// The diagram's legacy A4 scene stays proportional when placed on other paper.
/// </summary>
public sealed record ReportPaper
{
    public string Preset { get; init; } = "a4";
    public string Orientation { get; init; } = "landscape";

    public static ReportPaper Default => new();
    public static readonly (string Id,string Name,double WidthMm,double HeightMm)[] Choices =
    {
        ("letter","US Letter",215.9,279.4),
        ("legal","US Legal",215.9,355.6),
        ("tabloid","Tabloid / Ledger",279.4,431.8),
        ("a5","A5",148,210),
        ("a4","A4",210,297),
        ("a3","A3",297,420),
        ("a2","A2",420,594)
    };

    public static ReportPaper Parse(string? preset,string? orientation)
    {
        string id=string.IsNullOrWhiteSpace(preset)?"a4":preset.Trim().ToLowerInvariant();
        string direction=string.IsNullOrWhiteSpace(orientation)?"landscape":orientation.Trim().ToLowerInvariant();
        if(!Choices.Any(choice=>choice.Id==id))
            throw new InvalidOperationException("Choose a supported report size: Letter, Legal, Tabloid, A5, A4, A3 or A2.");
        if(direction is not ("landscape" or "portrait"))
            throw new InvalidOperationException("Report orientation must be landscape or portrait.");
        return new(){Preset=id,Orientation=direction};
    }
    public static ReportPaper Normalize(ReportPaper? value)=>Parse(value?.Preset,value?.Orientation);

    [JsonIgnore] public bool IsA4Default=>Preset=="a4"&&Orientation=="landscape";
    [JsonIgnore] public string FileStem=>IsA4Default?"a4":Preset+"-"+Orientation;
    [JsonIgnore] public string Description=>Choices.Single(choice=>choice.Id==Preset).Name+" "+Orientation;
    [JsonIgnore] public (double Width,double Height) SizeMm
    {
        get
        {
            var row=Choices.Single(choice=>choice.Id==Preset);
            double small=Math.Min(row.WidthMm,row.HeightMm),large=Math.Max(row.WidthMm,row.HeightMm);
            return Orientation=="portrait"?(small,large):(large,small);
        }
    }
    [JsonIgnore] public double WidthPt=>IsA4Default?841.89:SizeMm.Width*72/25.4;
    [JsonIgnore] public double HeightPt=>IsA4Default?595.28:SizeMm.Height*72/25.4;
    [JsonIgnore] public int PixelWidth=>checked((int)Math.Round(SizeMm.Width*300/25.4));
    [JsonIgnore] public int PixelHeight=>checked((int)Math.Round(SizeMm.Height*300/25.4));
    public void Validate()
    {
        if(PixelWidth<=0||PixelHeight<=0||(long)PixelWidth*PixelHeight>50_000_000)
            throw new InvalidOperationException("Selected paper exceeds the 300 DPI raster limit.");
    }
}
