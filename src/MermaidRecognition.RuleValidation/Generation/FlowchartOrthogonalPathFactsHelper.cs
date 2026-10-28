using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates that default curveBasis diagrams do not render multi-segment orthogonal edge polylines.
/// </summary>
public static class FlowchartOrthogonalPathFactsHelper
{
    private const double COLLINEAR_EPSILON_PX = 0.5;

    /// <summary>
    /// Records orthogonal path facts on visible edges and diagram routing.
    /// </summary>
    public static void ApplyLayoutOrthogonalPathFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        FlowchartDirection Direction)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        bool RequiresCurveBasisPaths = RequiresCurveBasisPathsForDiagram(Options);
        Diagram.Config["RequiresCurveBasisPaths"] = RequiresCurveBasisPaths;

        bool AnyViolation = false;

        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            Edge.Facts["Path.NoOrthogonalPolylineWhenCurveBasisDefault"] = true;
            Edge.Facts["Path.NoHardElbowWhenSmoothRequired"] = true;

            if (!Edge.IsVisible || !RequiresCurveBasisPaths)
            {
                continue;
            }

            if (IsForbiddenOrthogonalEdge(Edge, Direction, Options, out bool SmoothIntentViolation))
            {
                AnyViolation = true;
                Edge.Facts["Path.NoOrthogonalPolylineWhenCurveBasisDefault"] = false;
            }

            if (SmoothIntentViolation)
            {
                Edge.Facts["Path.NoHardElbowWhenSmoothRequired"] = false;
            }
        }

        Diagram.Routing["NoOrthogonalPolylinesWhenCurveBasisDefault"] = !AnyViolation;
    }

    /// <summary>
    /// Parses rendered SVG and flags edges whose connector path is a multi-segment orthogonal polyline.
    /// </summary>
    public static void ApplyRenderedSvgOrthogonalPathFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        string Svg)
    {
        if (!Diagram.Config.TryGetValue("RequiresCurveBasisPaths", out object? RequiresValue)
            || RequiresValue is not bool RequiresCurveBasisPaths
            || !RequiresCurveBasisPaths)
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
        List<EdgeContract> VisibleEdges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];

        int PathIndex = 0;
        foreach (EdgeContract Edge in VisibleEdges)
        {
            if (!FlowchartSvgEdgeStrokeClassifier.RendersAsSvgPath(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction))
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

            if (string.Equals(EdgeResult.RouteClass, "CycleOrReturn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EdgeResult.RouteClass, "LongBypass", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            List<(double X, double Y)> LayoutPoints = [.. EdgeResult.Path.Points.Select(Point => (Point.X, Point.Y))];
            if (IsExteriorObstacleCorridor(LayoutPoints, Direction))
            {
                continue;
            }

            if (FlowchartCurvePathBuilder.IsMultiSegmentOrthogonalPathData(PathData))
            {
                EdgeResult.Facts["Path.NoOrthogonalPolylineWhenCurveBasisDefault"] = false;
                EdgeResult.Facts["Path.RenderedAsOrthogonalPolyline"] = true;
            }
        }

        bool AnyViolation = Diagram.Edges.Any(
            Edge => Edge.IsVisible
                && Edge.Facts.TryGetValue("Path.NoOrthogonalPolylineWhenCurveBasisDefault", out object? Value)
                && Value is bool Allowed
                && !Allowed);
        Diagram.Routing["NoOrthogonalPolylinesWhenCurveBasisDefault"] = !AnyViolation;
    }

    private static bool RequiresCurveBasisPathsForDiagram(FlowchartRenderOptions Options)
    {
        string Curve = string.IsNullOrWhiteSpace(Options.Curve) ? "basis" : Options.Curve.Trim();
        return !string.Equals(Curve, "linear", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Curve, "step", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Curve, "stepBefore", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Curve, "stepAfter", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenOrthogonalEdge(
        EdgeActualResult Edge,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options,
        out bool SmoothIntentViolation)
    {
        SmoothIntentViolation = false;

        List<(double X, double Y)> Points = [.. Edge.Path.Points.Select(Point => (Point.X, Point.Y))];

        if (Points.Count < 2)
        {
            return false;
        }

        if (IsCollinearSpineExempt(Points, Direction))
        {
            return false;
        }

        if (IsExteriorObstacleCorridor(Points, Direction))
        {
            return false;
        }

        if (string.Equals(Edge.RouteClass, "CycleOrReturn", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(Edge.RouteClass, "CrossLane", StringComparison.OrdinalIgnoreCase)
            && Points.Count >= 4
            && FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Points))
        {
            return false;
        }

        if (string.Equals(Edge.RouteClass, "LongBypass", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string PredictedPathData = PredictRenderedPathData(Edge, Points, Options);
        if (!FlowchartCurvePathBuilder.IsMultiSegmentOrthogonalPathData(PredictedPathData))
        {
            return false;
        }

        SmoothIntentViolation = string.Equals(Edge.PathFamily, "Smooth", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.PathFamily, "Spline", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.PathFamily, "Orthogonal", StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private static bool IsExteriorObstacleCorridor(
        List<(double X, double Y)> Points,
        FlowchartDirection Direction)
    {
        if (Points.Count < 4)
        {
            return false;
        }

        if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Points))
        {
            if (Points.Count < 5)
            {
                return false;
            }

            List<(double X, double Y)> TailPoints = [];
            for (int Index = 1; Index < Points.Count; Index++)
            {
                TailPoints.Add(Points[Index]);
            }

            if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(TailPoints))
            {
                return false;
            }
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        (double StartX, double StartY) = Points[0];
        (double EndX, double EndY) = Points[Points.Count - 1];
        double SpineCoordinate = VerticalFlow
            ? (StartX + EndX) / 2.0
            : (StartY + EndY) / 2.0;
        const double EXTERIOR_DETOUR_TOLERANCE_PX = 32.0;

        foreach ((double PointX, double PointY) in Points)
        {
            double CrossAxisDelta = VerticalFlow
                ? Math.Abs(PointX - SpineCoordinate)
                : Math.Abs(PointY - SpineCoordinate);
            if (CrossAxisDelta > EXTERIOR_DETOUR_TOLERANCE_PX)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCollinearSpineExempt(
        List<(double X, double Y)> Points,
        FlowchartDirection Direction)
    {
        if (Points.Count < 3)
        {
            return true;
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;

        if (VerticalFlow)
        {
            double SpineX = Points[0].X;
            return Points.All(Point => Math.Abs(Point.X - SpineX) <= COLLINEAR_EPSILON_PX);
        }

        double SpineY = Points[0].Y;
        return Points.All(Point => Math.Abs(Point.Y - SpineY) <= COLLINEAR_EPSILON_PX);
    }

    private static string PredictRenderedPathData(
        EdgeActualResult Edge,
        List<(double X, double Y)> Points,
        FlowchartRenderOptions Options)
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }

        if (string.Equals(Edge.PathFamily, "Straight", StringComparison.OrdinalIgnoreCase))
        {
            List<(double X, double Y)> EndpointPoints =
            [
                Points[0],
                Points[Points.Count - 1]
            ];
            return FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(EndpointPoints, "linear");
        }

        if (string.Equals(Edge.PathFamily, "Orthogonal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.RouteClass, "LongBypass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.RouteClass, "FanIn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.RouteClass, "FanOut", StringComparison.OrdinalIgnoreCase))
        {
            return FlowchartCurvePathBuilder.BuildCorridorRoutePathData(
                Points,
                FlowchartEdgeRouteKind.Corridor);
        }

        if (Points.Count >= 4
            && FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Points)
            && (string.Equals(Edge.RouteClass, "CrossLane", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Edge.RouteClass, "LocalDecisionBranch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Edge.RouteClass, "Unknown", StringComparison.OrdinalIgnoreCase)))
        {
            return FlowchartCurvePathBuilder.BuildCorridorRoutePathData(
                Points,
                FlowchartEdgeRouteKind.Corridor);
        }

        if (string.Equals(Edge.RouteClass, "CycleOrReturn", StringComparison.OrdinalIgnoreCase)
            && Points.Count >= 7)
        {
            return FlowchartCurvePathBuilder.BuildRetryLoopPathData(Points);
        }

        if (string.Equals(Edge.PathFamily, "Smooth", StringComparison.OrdinalIgnoreCase))
        {
            if (Points.Count == 5)
            {
                return FlowchartCurvePathBuilder.BuildDecisionBranchPathData(Points);
            }

            if (Points.Count >= 5)
            {
                return FlowchartCurvePathBuilder.BuildVerticalFanRoutePathData(Points);
            }
        }

        return FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(Points, Options.Curve);
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
}
