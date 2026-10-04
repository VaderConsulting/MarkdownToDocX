using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates layout guide polylines for perpendicular terminal stubs on every visible edge.
/// </summary>
public static class FlowchartLayoutTerminalStubFactsHelper
{
    /// <summary>
    /// Records <c>Ports.PerpendicularAttach</c> from resolved layout guide terminal stubs.
    /// </summary>
    public static void Apply(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction)
    {
        if (!Diagram.LayoutSucceeded)
        {
            return;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node, StringComparer.Ordinal);

        foreach (EdgeActualResult EdgeResult in Diagram.Edges)
        {
            if (!EdgeResult.IsVisible)
            {
                continue;
            }

            EdgeContract? Edge = Contract.Edges.FirstOrDefault(
                Candidate => string.Equals(Candidate.SourceId, EdgeResult.SourceNodeId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetId, EdgeResult.TargetNodeId, StringComparison.Ordinal)
                    && string.Equals(Candidate.Label ?? string.Empty, EdgeResult.Label ?? string.Empty, StringComparison.Ordinal));
            if (Edge is null)
            {
                continue;
            }

            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind RouteKind))
            {
                EdgeResult.Facts["Ports.PerpendicularAttach"] = false;
                EdgeResult.Facts["Path.LayoutAttachFailure"] = "Route guide points could not be resolved.";
                continue;
            }

            if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds? SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds? TargetBounds))
            {
                EdgeResult.Facts["Ports.PerpendicularAttach"] = false;
                EdgeResult.Facts["Path.LayoutAttachFailure"] = "Edge endpoint bounds are missing.";
                continue;
            }

            string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
            bool AllowDiagonalCrossEdge = RouteKind == FlowchartEdgeRouteKind.GroupedTwoByTwo;

            bool Valid = FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
                LayoutPoints,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                AllowDiagonalCrossEdge,
                out string FailureMessage);

            EdgeResult.Facts["Ports.PerpendicularAttach"] = Valid;
            if (!Valid && !string.IsNullOrWhiteSpace(FailureMessage))
            {
                EdgeResult.Facts["Path.LayoutAttachFailure"] = FailureMessage;
            }
        }
    }
}
