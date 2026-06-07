using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates rendered SVG edge geometry for node clearance, kinks, and perpendicular attach.
/// </summary>
public static class FlowchartRenderedEdgePathFactsHelper
{
    /// <summary>
    /// Renders the diagram, samples connector paths, and records rendered-path facts on visible edges.
    /// </summary>
    public static void Apply(DiagramActualResult Diagram, DiagramContract Contract, string Svg)
    {
        if (!Diagram.LayoutSucceeded || string.IsNullOrWhiteSpace(Svg))
        {
            return;
        }

        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
        {
            return;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node, StringComparer.Ordinal);
        List<string> EdgePathData = ExtractRenderedEdgePathData(Svg);
        List<EdgeContract> VisibleEdges = Contract.Edges
            .Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int PathIndex = 0;
        foreach (EdgeContract Edge in VisibleEdges)
        {
            bool RendersAsPath = FlowchartSvgEdgeStrokeClassifier.RendersAsSvgPath(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction);

            if (!RendersAsPath)
            {
                continue;
            }

            while (PathIndex < EdgePathData.Count
                && !EdgePathData[PathIndex].StartsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                PathIndex++;
            }

            if (PathIndex >= EdgePathData.Count)
            {
                break;
            }

            string PathData = EdgePathData[PathIndex];
            PathIndex++;

            EdgeActualResult? EdgeResult = Diagram.Edges.FirstOrDefault(
                Candidate => string.Equals(Candidate.SourceNodeId, Edge.SourceId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetNodeId, Edge.TargetId, StringComparison.Ordinal)
                    && string.Equals(Candidate.Label ?? string.Empty, Edge.Label ?? string.Empty, StringComparison.Ordinal));

            if (EdgeResult is null || !EdgeResult.IsVisible)
            {
                continue;
            }

            if (string.Equals(EdgeResult.RouteClass, "LocalDecisionBranch", StringComparison.Ordinal))
            {
                EdgeResult.Facts["Path.RenderedAsSvgPath"] = true;
                continue;
            }

            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> LayoutGuidePoints,
                    out FlowchartEdgeRouteKind RouteKind))
            {
                continue;
            }

            FlowchartRenderedPathValidator.FlowchartRenderedPathValidationResult ValidationResult =
                FlowchartRenderedPathValidator.Validate(
                    PathData,
                    Edge,
                    Contract,
                    Layout,
                    LayoutGuidePoints,
                    RouteKind,
                    Direction);

            EdgeResult.Facts["Path.RenderedAsSvgPath"] = true;
            EdgeResult.Facts["Ports.PerpendicularAttach"] = ValidationResult.PerpendicularAttach;
            EdgeResult.Facts["Path.RenderedPathClearsUnrelatedNodeBodies"] = ValidationResult.ClearsUnrelatedNodeBodies;
            EdgeResult.Facts["Path.NoVisibleKinkInSmoothBody"] = ValidationResult.NoVisibleKink;

            if (!ValidationResult.ClearsUnrelatedNodeBodies)
            {
                EdgeResult.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"] = false;
                if (!string.IsNullOrWhiteSpace(ValidationResult.IntersectedNodeId))
                {
                    EdgeResult.CollisionFacts.Add(new Dictionary<string, object>
                    {
                        ["Kind"] = "RenderedNodeBodyIntersection",
                        ["IntersectedNodeId"] = ValidationResult.IntersectedNodeId,
                        ["Reason"] = ValidationResult.FailureMessage ?? string.Empty
                    });
                }
            }

            if (!ValidationResult.NoVisibleKink
                && !string.Equals(EdgeResult.RouteClass, "LongBypass", StringComparison.OrdinalIgnoreCase))
            {
                EdgeResult.Facts["Path.NoHardElbowWhenSmoothRequired"] = false;
            }

            if (!ValidationResult.PerpendicularAttach && !string.IsNullOrWhiteSpace(ValidationResult.FailureMessage))
            {
                EdgeResult.Facts["Path.RenderedAttachFailure"] = ValidationResult.FailureMessage;
            }
        }
    }

    private static List<string> ExtractRenderedEdgePathData(string Svg)
    {
        List<string> PathData = new List<string>();
        MatchCollection Matches = Regex.Matches(
            Svg,
            @"<path d=""([^""]+)"" fill=""none"" stroke=""",
            RegexOptions.CultureInvariant);

        foreach (Match Match in Matches)
        {
            if (Match.Groups.Count > 1)
            {
                PathData.Add(Match.Groups[1].Value);
            }
        }

        return PathData;
    }
}
