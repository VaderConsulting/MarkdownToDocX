namespace MermaidRecognition.Layout;

/// <summary>
/// Intended label placement strategy for a labelled edge.
/// </summary>
public enum FlowchartLabelPlacementIntent
{
    /// <summary>
    /// Edge has no label.
    /// </summary>
    None,

    /// <summary>
    /// Label sits on the short branch segment near the decision source.
    /// </summary>
    NearSourceBranchSegment,

    /// <summary>
    /// Label sits on the exterior corridor segment of a long bypass.
    /// </summary>
    NearCorridorSegment,

    /// <summary>
    /// Label offset along the edge midpoint without overlapping node bodies.
    /// </summary>
    OffsetAlongEdgeMidpoint
}
