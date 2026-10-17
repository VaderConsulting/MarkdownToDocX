namespace MermaidRecognition.Layout;

/// <summary>
/// Layout bounds for one flowchart node after Sugiyama placement.
/// </summary>
public sealed class FlowchartLayoutNodeBounds
{
    /// <summary>Renderer-owned local page-down geometry, when the visual box differs from the connection body.</summary>
    public MermaidRecognition.Contract.NodeGeometry? Geometry { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public double CenterX { get; init; }

    public double CenterY { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }
}
