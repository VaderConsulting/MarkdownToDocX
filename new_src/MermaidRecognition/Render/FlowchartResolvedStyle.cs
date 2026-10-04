namespace MermaidRecognition.Render;

/// <summary>
/// Resolved paint and typography for one flowchart node or edge (spec §3.17, §4.1).
/// </summary>
public sealed class FlowchartResolvedStyle
{
    private string _Fill = string.Empty;

    public string Fill
    {
        get => _Fill; set => _Fill = value;
    }

    private string _Stroke = string.Empty;

    public string Stroke
    {
        get => _Stroke; set => _Stroke = value;
    }

    private double _StrokeWidth = 1.0;

    public double StrokeWidth
    {
        get => _StrokeWidth; set => _StrokeWidth = value;
    }

    private string? _StrokeDasharray;

    public string? StrokeDasharray
    {
        get => _StrokeDasharray; set => _StrokeDasharray = value;
    }

    private string _TextColor = string.Empty;

    public string TextColor
    {
        get => _TextColor; set => _TextColor = value;
    }

    private string _FontFamily = string.Empty;

    public string FontFamily
    {
        get => _FontFamily; set => _FontFamily = value;
    }

    private double _FontSizePx = 16.0;

    public double FontSizePx
    {
        get => _FontSizePx; set => _FontSizePx = value;
    }

    private string _FontWeight = "normal";

    public string FontWeight
    {
        get => _FontWeight; set => _FontWeight = value;
    }
}
