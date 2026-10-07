using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// One flowchart edge after dagre routing, in the same coordinate system as the node centres.
/// </summary>
public sealed class FlowchartLayoutEdgeRoute
{
    /// <summary>
    /// Source node id.
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Target node id.
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Side of the source outline the edge leaves: left, right, top or bottom.
    /// </summary>
    public string ExitSide { get; set; } = string.Empty;

    /// <summary>
    /// Side of the target outline the edge enters: left, right, top or bottom.
    /// </summary>
    public string EntrySide { get; set; } = string.Empty;

    /// <summary>
    /// Polyline vertices, including the points on the source and target outlines.
    /// </summary>
    public List<FlowchartLayoutPoint> Points { get; } = new List<FlowchartLayoutPoint>();
}
