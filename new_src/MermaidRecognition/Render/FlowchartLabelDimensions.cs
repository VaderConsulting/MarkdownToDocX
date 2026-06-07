namespace MermaidRecognition.Render;

/// <summary>
/// Measured wrapped label dimensions (nodes and edges, spec §3.17.3).
/// </summary>
public sealed class FlowchartLabelDimensions
{
    public string DisplayText { get; init; } = string.Empty;

    public double Width { get; init; }

    public double Height { get; init; }
}
