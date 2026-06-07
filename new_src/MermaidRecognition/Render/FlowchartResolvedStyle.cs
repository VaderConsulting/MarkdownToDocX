namespace MermaidRecognition.Render;

/// <summary>
/// Resolved paint and typography for one flowchart node or edge (spec §3.17, §4.1).
/// </summary>
public sealed class FlowchartResolvedStyle
{
    private string _Fill = string.Empty;

    public string Fill
    {
        get
        {
            return _Fill;
        }
        set
        {
            _Fill = value;
        }
    }

    private string _Stroke = string.Empty;

    public string Stroke
    {
        get
        {
            return _Stroke;
        }
        set
        {
            _Stroke = value;
        }
    }

    private double _StrokeWidth = 1.0;

    public double StrokeWidth
    {
        get
        {
            return _StrokeWidth;
        }
        set
        {
            _StrokeWidth = value;
        }
    }

    private string? _StrokeDasharray;

    public string? StrokeDasharray
    {
        get
        {
            return _StrokeDasharray;
        }
        set
        {
            _StrokeDasharray = value;
        }
    }

    private string _TextColor = string.Empty;

    public string TextColor
    {
        get
        {
            return _TextColor;
        }
        set
        {
            _TextColor = value;
        }
    }

    private string _FontFamily = string.Empty;

    public string FontFamily
    {
        get
        {
            return _FontFamily;
        }
        set
        {
            _FontFamily = value;
        }
    }

    private double _FontSizePx = 16.0;

    public double FontSizePx
    {
        get
        {
            return _FontSizePx;
        }
        set
        {
            _FontSizePx = value;
        }
    }

    private string _FontWeight = "normal";

    public string FontWeight
    {
        get
        {
            return _FontWeight;
        }
        set
        {
            _FontWeight = value;
        }
    }
}
