using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Dispatches semantic route classes to specialised path builders before generic allocators.
/// </summary>
public static class FlowchartEdgeRouteStrategyRegistry
{
    /// <summary>
    /// Attempts to resolve a route for a classified semantic edge.
    /// </summary>
    public static bool TryResolveRoute(
        FlowchartEdgeSemanticRouteClass SemanticRouteClass,
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext? SpineContext,
        out List<(double X, double Y)> LayoutPoints,
        out FlowchartEdgeRouteKind RouteKind)
    {
        LayoutPoints = [];
        RouteKind = FlowchartEdgeRouteKind.Unknown;

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode);
        NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode);
        string? SourceShape = SourceNode?.Shape;
        string? TargetShape = TargetNode?.Shape;

        switch (SemanticRouteClass)
        {
            case FlowchartEdgeSemanticRouteClass.LocalDecisionBranch:
                if (FlowchartLocalDecisionBranchRouteBuilder.TryBuildRoute(
                        Edge,
                        Contract,
                        BoundsById,
                        SourceShape,
                        TargetShape,
                        Direction,
                        LayoutPoints))
                {
                    RouteKind = LayoutPoints.Count == 2
                        ? FlowchartEdgeRouteKind.Straight
                        : FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(LayoutPoints)
                            ? FlowchartEdgeRouteKind.Corridor
                            : FlowchartEdgeRouteKind.Decision;
                    return true;
                }

                return false;

            case FlowchartEdgeSemanticRouteClass.LongExceptionRoute:
                if (FlowchartLongExceptionRouteBuilder.TryBuildRoute(
                        Edge,
                        Contract,
                        BoundsById,
                        SourceShape,
                        TargetShape,
                        Direction,
                        SpineContext,
                        LayoutPoints))
                {
                    RouteKind = FlowchartEdgeRouteKind.Corridor;
                    return true;
                }

                return false;

            case FlowchartEdgeSemanticRouteClass.SkipBypass:
                if (FlowchartSkipBypassRouteBuilder.TryBuildRoute(
                        Edge,
                        Contract,
                        BoundsById,
                        SourceShape,
                        TargetShape,
                        Direction,
                        SpineContext,
                        LayoutPoints))
                {
                    RouteKind = FlowchartEdgeRouteKind.Corridor;
                    return true;
                }

                return false;

            case FlowchartEdgeSemanticRouteClass.MergeBack:
                if (FlowchartMergeBackRouteBuilder.TryBuildRoute(
                        Edge,
                        Contract,
                        BoundsById,
                        SourceShape,
                        TargetShape,
                        Direction,
                        SpineContext,
                        LayoutPoints))
                {
                    RouteKind = FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(LayoutPoints)
                        ? FlowchartEdgeRouteKind.Corridor
                        : LayoutPoints.Count == 2
                            ? FlowchartEdgeRouteKind.Straight
                            : FlowchartEdgeRouteKind.Curved;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }
}
