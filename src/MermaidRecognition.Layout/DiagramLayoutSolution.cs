using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Node boxes and routed edges in SVG coordinates (Y down) from one engine run.
/// </summary>
public sealed class DiagramLayoutSolution
{
    private readonly List<FlowchartLayoutNodeBounds> _NodeBounds = [];

    private readonly List<FlowchartLayoutNodeBounds> _GroupBounds = [];

    private readonly List<DiagramLayoutRoutedEdge> _Edges = [];

    /// <summary>
    /// Leaf node boxes.
    /// </summary>
    public List<FlowchartLayoutNodeBounds> NodeBounds
    {
        get
        {
            return _NodeBounds;
        }
    }

    /// <summary>
    /// Group boxes.
    /// </summary>
    public List<FlowchartLayoutNodeBounds> GroupBounds
    {
        get
        {
            return _GroupBounds;
        }
    }

    /// <summary>
    /// Routed edges with SVG path samples.
    /// </summary>
    public List<DiagramLayoutRoutedEdge> Edges
    {
        get
        {
            return _Edges;
        }
    }

    /// <summary>
    /// Diagram width in pixels.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Diagram height in pixels.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Engine that produced this solution.
    /// </summary>
    public DiagramLayoutEngineKind EngineKind { get; set; }
}

/// <summary>
/// One routed edge in SVG coordinates.
/// </summary>
public sealed class DiagramLayoutRoutedEdge
{
    /// <summary>
    /// Creates a routed edge.
    /// </summary>
    /// <param name="SourceId">Source node or group id.</param>
    /// <param name="TargetId">Target node or group id.</param>
    /// <param name="Label">Edge label, or empty.</param>
    /// <param name="Samples">Path samples in SVG coordinates.</param>
    public DiagramLayoutRoutedEdge(
        string SourceId,
        string TargetId,
        string Label,
        List<(double X, double Y)> Samples)
    {
        this.SourceId = SourceId ?? string.Empty;
        this.TargetId = TargetId ?? string.Empty;
        this.Label = Label ?? string.Empty;
        this.Samples = Samples ?? [];
    }

    /// <summary>
    /// Source node or group id.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Target node or group id.
    /// </summary>
    public string TargetId { get; }

    /// <summary>
    /// Edge label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Path samples in SVG coordinates.
    /// </summary>
    public List<(double X, double Y)> Samples { get; }

    /// <summary>
    /// Label X in SVG space, or NaN when the engine did not place a label.
    /// </summary>
    public double LabelX { get; set; } = double.NaN;

    /// <summary>
    /// Label Y in SVG space, or NaN when the engine did not place a label.
    /// </summary>
    public double LabelY { get; set; } = double.NaN;
}
