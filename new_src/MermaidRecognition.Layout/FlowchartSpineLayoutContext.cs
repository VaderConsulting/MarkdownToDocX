using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Spine column layout metadata shared by classification and routing.
/// </summary>
public sealed class FlowchartSpineLayoutContext
{
    private readonly HashSet<string> _SpineNodeIds;

    /// <summary>
    /// Creates spine layout metadata for TB/BT diagrams with a terminal collector sink.
    /// </summary>
    public FlowchartSpineLayoutContext(
        HashSet<string> SpineNodeIds,
        string PrimaryRootId,
        string PrimarySinkId,
        double SpineCrossAxis,
        double ColumnStep)
    {
        _SpineNodeIds = SpineNodeIds;
        this.PrimaryRootId = PrimaryRootId;
        this.PrimarySinkId = PrimarySinkId;
        this.SpineCrossAxis = SpineCrossAxis;
        this.ColumnStep = ColumnStep;
    }

    /// <summary>
    /// Node identifiers on the primary spine walk from root to sink.
    /// </summary>
    public IReadOnlySet<string> SpineNodeIds => _SpineNodeIds;

    /// <summary>
    /// Earliest root node used to seed the spine walk.
    /// </summary>
    public string PrimaryRootId { get; }

    /// <summary>
    /// Primary sink node at the end of the spine walk.
    /// </summary>
    public string PrimarySinkId { get; }

    /// <summary>
    /// Shared cross-axis coordinate of spine-aligned nodes.
    /// </summary>
    public double SpineCrossAxis { get; }

    /// <summary>
    /// Horizontal distance between spine and adjacent margin columns.
    /// </summary>
    public double ColumnStep { get; }
}
