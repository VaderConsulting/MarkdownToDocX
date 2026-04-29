namespace MermaidSharp;

public class RenderOptions
{
    public static RenderOptions Default => new();

    public double Padding { get; set; } = 20;
    public string Theme { get; set; } = "default";
    public double FontSize { get; set; } = 16;
    public string FontFamily { get; set; } = "trebuchet ms, verdana, arial, sans-serif";
    public bool ShowBoundingBox { get; set; }
}