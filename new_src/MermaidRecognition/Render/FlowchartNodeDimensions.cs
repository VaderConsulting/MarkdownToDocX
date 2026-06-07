namespace MermaidRecognition.Render;

/// <summary>
/// Measured node box for Sugiyama layout (spec §3.17.3).
/// </summary>
public sealed class FlowchartNodeDimensions
{
    private double _Width;

    public double Width
    {
        get
        {
            return _Width;
        }
        set
        {
            _Width = value;
        }
    }

    private double _Height;

    public double Height
    {
        get
        {
            return _Height;
        }
        set
        {
            _Height = value;
        }
    }

    private string _DisplayLabel = string.Empty;

    /// <summary>
    /// Label text after wrapping for layout and SVG (may include line breaks).
    /// </summary>
    public string DisplayLabel
    {
        get
        {
            return _DisplayLabel;
        }
        set
        {
            _DisplayLabel = value;
        }
    }
}
