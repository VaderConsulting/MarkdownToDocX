using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Result of native Sugiyama layout for a flowchart contract.
/// </summary>
public sealed class FlowchartLayoutResult
{
    /// <summary>Mutable routing and rendering state owned by this layout.</summary>
    public FlowchartRenderContext RenderContext { get; internal set; } = new();

    private readonly List<FlowchartLayoutNodeBounds> _NodeBounds = [];

    public IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds => _NodeBounds;

    internal List<FlowchartLayoutNodeBounds> NodeBoundsMutable => _NodeBounds;

    public double GraphWidth { get; init; }

    public double GraphHeight { get; init; }

    private readonly List<FlowchartLayoutEdgeRoute> _EdgeRoutes = [];

    /// <summary>Routes resolved by the layout model for renderers to consume.</summary>
    internal List<FlowchartResolvedGuide>? ResolvedEdgeGuides { get; set; }

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

    /// <summary>
    /// True when <see cref="DiagramGridLayout"/> rebuilt edge routes on the grid.
    /// </summary>
    public bool UsesGridRoutes { get; internal set; }

    /// <summary>
    /// Copies the drawn route for <paramref name="Edge"/> when this layout stored one.
    /// </summary>
    /// <param name="Edge">The edge to look up.</param>
    /// <param name="Points">The route points in layout coordinates.</param>
    /// <returns>True when a stored route matches <paramref name="Edge"/>.</returns>
    public bool TryCopyDrawnRoutePoints(EdgeContract Edge, out List<(double X, double Y)> Points)
    {
        Points = new List<(double X, double Y)>();
        FlowchartLayoutEdgeRoute? Match = null;
        foreach (FlowchartLayoutEdgeRoute Route in _EdgeRoutes)
        {
            if (!string.IsNullOrEmpty(Edge.Id) && string.Equals(Route.EdgeId, Edge.Id, StringComparison.Ordinal))
            {
                Match = Route;
                break;
            }
        }

        if (Match == null)
        {
            foreach (FlowchartLayoutEdgeRoute Route in _EdgeRoutes)
            {
                if (string.Equals(Route.SourceId, Edge.SourceId, StringComparison.Ordinal)
                    && string.Equals(Route.TargetId, Edge.TargetId, StringComparison.Ordinal))
                {
                    Match = Route;
                    break;
                }
            }
        }

        if (Match == null || Match.Points.Count < 2)
        {
            return false;
        }

        foreach (FlowchartLayoutPoint Point in Match.Points)
        {
            Points.Add((Point.X, Point.Y));
        }

        return true;
    }
}
