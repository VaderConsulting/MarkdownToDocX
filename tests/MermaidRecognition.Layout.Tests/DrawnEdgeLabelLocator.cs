using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Works out where the renderer draws an edge label, in SVG coordinates, using the same placement and
/// clearance steps as <see cref="FlowchartSugiyamaSvgRenderer"/>.
/// </summary>
internal static class DrawnEdgeLabelLocator
{
    /// <summary>
    /// Returns the drawn centre of the label on <paramref name="Edge"/> in SVG coordinates.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="Layout">The layout of <paramref name="Contract"/>.</param>
    /// <param name="Edge">The labelled edge.</param>
    /// <returns>The label centre in SVG coordinates.</returns>
    public static (double X, double Y) ResolveSvgCentre(DiagramContract Contract, FlowchartLayoutResult Layout, EdgeContract Edge)
    {
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes.ToDictionary(Node => Node.Id, Node => Node, StringComparer.Ordinal);
        if (!Layout.TryCopyDrawnRoutePoints(Edge, out List<(double X, double Y)> GuidePoints)
            && !FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(Edge, Contract, BoundsById, NodeById, Direction, out GuidePoints, out FlowchartEdgeRouteKind _))
        {
            throw new InvalidOperationException($"Could not resolve guide points for {Edge.SourceId}->{Edge.TargetId}.");
        }

        Dictionary<string, string> RouteClassByEdgeKey = FlowchartEdgeRoutingIntentAnalyzer.BuildRouteClassNamesByEdgeKey(Contract, Layout, Direction);
        string? RouteClass = null;
        if (!string.IsNullOrWhiteSpace(Edge.Id) && RouteClassByEdgeKey.TryGetValue(Edge.Id, out string? RouteClassById))
        {
            RouteClass = RouteClassById;
        }
        else if (RouteClassByEdgeKey.TryGetValue($"{Edge.SourceId}->{Edge.TargetId}", out string? RouteClassByEndpoints))
        {
            RouteClass = RouteClassByEndpoints;
        }

        string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        (double LayoutX, double LayoutY) = FlowchartSugiyamaSvgRenderer.ResolveDrawnEdgeLabelCentre(Edge, Contract, BoundsById, BoundsById[Edge.SourceId], BoundsById[Edge.TargetId], SourceShape, GuidePoints, Direction, RouteClass, Options);

        if (!FlowchartSvgLayoutCoordinateMapper.TryCreate(Contract, Layout, out FlowchartSvgLayoutCoordinateMapper? Mapper) || Mapper is null)
        {
            throw new InvalidOperationException("Could not build the layout-to-SVG mapper.");
        }

        return Mapper.LayoutToSvg(LayoutX, LayoutY);
    }
}
