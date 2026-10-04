using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Predicts whether a visible edge is emitted as an SVG path or straight line segment.
/// </summary>
public static class FlowchartSvgEdgeStrokeClassifier
{
    /// <summary>
    /// Returns true when the renderer emits a <c>&lt;path&gt;</c> stroke for the edge.
    /// </summary>
    public static bool RendersAsSvgPath(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction)
    {
        if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
        string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;

        if (FlowchartEdgePathBuilder.IsLrRetryLoopEdge(
                Edge,
                Contract,
                SourceBounds,
                TargetBounds,
                SourceShape,
                TargetShape))
        {
            return true;
        }

        if (FlowchartGroupedVertexEdgePathBuilder.TryBuildGroupedTwoByTwoPath(
                Edge,
                Contract,
                SourceBounds,
                TargetBounds,
                Direction,
                out List<(double X, double Y)> GroupedLayoutPoints)
            && GroupedLayoutPoints.Count >= 2)
        {
            return false;
        }

        if (FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> RoutedLayoutPoints,
                out FlowchartEdgeRouteKind RouteKind)
            && RoutedLayoutPoints.Count >= 2)
        {
            if (RouteKind == FlowchartEdgeRouteKind.Straight)
            {
                return false;
            }

            if (RouteKind == FlowchartEdgeRouteKind.Decision && RoutedLayoutPoints.Count == 2)
            {
                return false;
            }

            return true;
        }

        if (FlowchartFanOutPortAllocator.TryResolveEdgeRoute(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> FanOutLayoutPoints)
            && FanOutLayoutPoints.Count >= 2)
        {
            return FanOutLayoutPoints.Count > 2;
        }

        if (FlowchartFanInPortAllocator.TryResolveEdgeRoute(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> FanInLayoutPoints)
            && FanInLayoutPoints.Count >= 2)
        {
            return FanInLayoutPoints.Count > 2;
        }

        if (FlowchartDecisionPortAllocator.TryResolveEdgeRoute(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> DecisionLayoutPoints)
            && DecisionLayoutPoints.Count >= 2)
        {
            return DecisionLayoutPoints.Count > 2;
        }

        return false;
    }
}
