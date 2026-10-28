using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// One flowchart edge after dagre routing, in the same coordinate system as the node centres.
/// </summary>
public sealed class FlowchartLayoutEdgeRoute
{
    /// <summary>
    /// Contract edge id, or the generated name used when the contract edge has none.
    /// </summary>
    public string EdgeId { get; set; } = string.Empty;

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

    /// <summary>Outward normal at the source port, when the endpoint is on a slanted outline.</summary>
    public double ExitNormalX { get; set; }

    /// <summary>Outward normal at the source port, when the endpoint is on a slanted outline.</summary>
    public double ExitNormalY { get; set; }

    /// <summary>Outward normal at the target port, when the endpoint is on a slanted outline.</summary>
    public double EntryNormalX { get; set; }

    /// <summary>Outward normal at the target port, when the endpoint is on a slanted outline.</summary>
    public double EntryNormalY { get; set; }

    /// <summary>
    /// Polyline vertices, including the points on the source and target outlines.
    /// </summary>
    public List<FlowchartLayoutPoint> Points { get; } = [];
}
