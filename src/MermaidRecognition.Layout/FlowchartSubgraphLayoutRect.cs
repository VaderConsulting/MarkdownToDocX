namespace MermaidRecognition.Layout;

/// <summary>
/// Layout-space bounds of a subgraph cluster box (Y-up, same axes as Sugiyama output).
/// </summary>
public sealed class FlowchartSubgraphLayoutRect
{
    /// <summary>Reserved title-strip height, including the renderer's text clearance.</summary>
    public double TitleBandHeightPx { get; set; } = FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX;

    public string GroupId
    {
        get => _GroupId; set => _GroupId = value;
    }

    private string _GroupId = string.Empty;

    public double Left
    {
        get => _Left; set => _Left = value;
    }

    private double _Left;

    public double Right
    {
        get => _Right; set => _Right = value;
    }

    private double _Right;

    public double Bottom
    {
        get => _Bottom; set => _Bottom = value;
    }

    private double _Bottom;

    public double Top
    {
        get => _Top; set => _Top = value;
    }

    private double _Top;

    public string? Title
    {
        get => _Title; set => _Title = value;
    }

    private string? _Title;
}
