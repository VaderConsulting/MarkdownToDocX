using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Result of native Sugiyama layout for a flowchart contract.
/// </summary>
public sealed class FlowchartLayoutResult
{
    private readonly List<FlowchartLayoutNodeBounds> _NodeBounds = [];

    public IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds => _NodeBounds;

    internal List<FlowchartLayoutNodeBounds> NodeBoundsMutable => _NodeBounds;

    public double GraphWidth { get; init; }

    public double GraphHeight { get; init; }

    private readonly List<FlowchartLayoutEdgeRoute> _EdgeRoutes = [];

    /// <summary>
    /// Dagre edge polylines for the diagram.
    /// </summary>
    public IReadOnlyList<FlowchartLayoutEdgeRoute> EdgeRoutes => _EdgeRoutes;

    internal List<FlowchartLayoutEdgeRoute> EdgeRoutesMutable => _EdgeRoutes;

    private readonly List<FlowchartLayoutNodeBounds> _ClusterBounds = [];

    /// <summary>
    /// Subgraph bounds in the same coordinate system as <see cref="NodeBounds"/>.
    /// </summary>
    public IReadOnlyList<FlowchartLayoutNodeBounds> ClusterBounds => _ClusterBounds;

    internal List<FlowchartLayoutNodeBounds> ClusterBoundsMutable => _ClusterBounds;
}
