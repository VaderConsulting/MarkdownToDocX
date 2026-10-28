using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
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
        List<(double X1, double Y1, double X2, double Y2)> EdgeLineSegments = ExtractRenderedEdgeLineSegments(Svg);
        List<EdgeContract> VisibleEdges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];

        int PathIndex = 0;
        int LineIndex = 0;
        foreach (EdgeContract Edge in VisibleEdges)
        {
            EdgeActualResult? EdgeResult = Diagram.Edges.FirstOrDefault(
                Candidate => string.Equals(Candidate.SourceNodeId, Edge.SourceId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetNodeId, Edge.TargetId, StringComparison.Ordinal)
                    && string.Equals(Candidate.Label ?? string.Empty, Edge.Label ?? string.Empty, StringComparison.Ordinal));

            if (EdgeResult is null || !EdgeResult.IsVisible)
            {
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

            bool RendersAsPath = FlowchartSvgEdgeStrokeClassifier.RendersAsSvgPath(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction);

            FlowchartRenderedPathValidator.FlowchartRenderedPathValidationResult ValidationResult;

            if (RendersAsPath)
            {
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

                ValidationResult = FlowchartRenderedPathValidator.Validate(
                    PathData,
                    Edge,
                    Contract,
                    Layout,
                    LayoutGuidePoints,
                    RouteKind,
                    Direction);

                EdgeResult.Facts["Path.RenderedAsSvgPath"] = true;
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
            }
            else
            {
                if (LineIndex >= EdgeLineSegments.Count)
                {
                    break;
                }

                (double LineX1, double LineY1, double LineX2, double LineY2) = EdgeLineSegments[LineIndex];
                LineIndex++;

                ValidationResult = FlowchartRenderedPathValidator.ValidateStraightLine(
                    LineX1,
                    LineY1,
                    LineX2,
                    LineY2,
                    Edge,
                    Contract,
                    Layout,
                    LayoutGuidePoints,
                    RouteKind,
                    Direction);
            }

            if (!ValidationResult.PerpendicularAttach)
            {
                EdgeResult.Facts["Ports.PerpendicularAttach"] = false;
                if (!string.IsNullOrWhiteSpace(ValidationResult.FailureMessage))
                {
                    EdgeResult.Facts["Path.RenderedAttachFailure"] = ValidationResult.FailureMessage;
                }
            }
        }
    }

    private static List<string> ExtractRenderedEdgePathData(string Svg)
    {
        List<string> PathData = [];
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

    private static List<(double X1, double Y1, double X2, double Y2)> ExtractRenderedEdgeLineSegments(string Svg)
    {
        List<(double X1, double Y1, double X2, double Y2)> LineSegments =
            [];
        MatchCollection Matches = Regex.Matches(
            Svg,
            @"<line x1=""([^""]+)"" y1=""([^""]+)"" x2=""([^""]+)"" y2=""([^""]+)""[^>]*stroke-linecap=""butt""",
            RegexOptions.CultureInvariant);

        foreach (Match Match in Matches)
        {
            if (Match.Groups.Count < 5)
            {
                continue;
            }

            if (!double.TryParse(Match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double X1)
                || !double.TryParse(Match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Y1)
                || !double.TryParse(Match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double X2)
                || !double.TryParse(Match.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Y2))
            {
                continue;
            }

            LineSegments.Add((X1, Y1, X2, Y2));
        }

        return LineSegments;
    }
}
