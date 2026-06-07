using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Verifies rendered arrowhead centre axes align with connector path tangents in SVG output.
/// </summary>
public static class FlowchartSvgMarkerTangentFactsHelper
{
    /// <summary>
    /// Updates marker tangent alignment facts from rendered SVG output.
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
        List<RenderedArrowPolygon> ArrowPolygons = ExtractRenderedArrowPolygons(Svg);

        List<EdgeContract> VisibleEdges = Contract.Edges
            .Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int PathIndex = 0;
        int LineIndex = 0;
        int ArrowIndex = 0;

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

            bool RendersAsPath = FlowchartSvgEdgeStrokeClassifier.RendersAsSvgPath(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction);

            string? PathData = null;
            (double LineX1, double LineY1, double LineX2, double LineY2)? LineSegment = null;

            if (RendersAsPath)
            {
                while (PathIndex < EdgePathData.Count
                    && !EdgePathData[PathIndex].StartsWith("M", StringComparison.OrdinalIgnoreCase))
                {
                    PathIndex++;
                }

                if (PathIndex < EdgePathData.Count)
                {
                    PathData = EdgePathData[PathIndex];
                    PathIndex++;
                }
            }
            else if (LineIndex < EdgeLineSegments.Count)
            {
                LineSegment = EdgeLineSegments[LineIndex];
                LineIndex++;
            }

            bool HasEndArrow = Edge.MarkerEnd == EdgeEndpointMarker.Point
                || string.Equals(EdgeResult.ArrowEnd, "Point", StringComparison.Ordinal);
            bool HasStartArrow = Edge.MarkerStart == EdgeEndpointMarker.Point
                || string.Equals(EdgeResult.ArrowStart, "Point", StringComparison.Ordinal);

            if (HasEndArrow)
            {
                if (ArrowIndex >= ArrowPolygons.Count)
                {
                    EdgeResult.MarkerFacts["EndAlignedWithPathTangent"] = false;
                    EdgeResult.MarkerFacts["EndTangentFailure"] = "Rendered end arrow could not be located in SVG output.";
                }
                else
                {
                    RenderedArrowPolygon Arrow = ArrowPolygons[ArrowIndex];
                    ArrowIndex++;
                    bool Aligned = ValidateArrowPolygonAlignsWithStroke(
                        Arrow,
                        PathData,
                        LineSegment,
                        AtTargetEnd: true,
                        out string? EndFailure);
                    EdgeResult.MarkerFacts["EndAlignedWithPathTangent"] = Aligned;
                    if (!string.IsNullOrWhiteSpace(EndFailure))
                    {
                        EdgeResult.MarkerFacts["EndTangentFailure"] = EndFailure;
                    }
                }
            }

            if (HasStartArrow)
            {
                if (ArrowIndex >= ArrowPolygons.Count)
                {
                    EdgeResult.MarkerFacts["StartAlignedWithPathTangent"] = false;
                    EdgeResult.MarkerFacts["StartTangentFailure"] = "Rendered start arrow could not be located in SVG output.";
                }
                else
                {
                    RenderedArrowPolygon Arrow = ArrowPolygons[ArrowIndex];
                    ArrowIndex++;
                    bool Aligned = ValidateArrowPolygonAlignsWithStroke(
                        Arrow,
                        PathData,
                        LineSegment,
                        AtTargetEnd: false,
                        out string? StartFailure);
                    EdgeResult.MarkerFacts["StartAlignedWithPathTangent"] = Aligned;
                    if (!string.IsNullOrWhiteSpace(StartFailure))
                    {
                        EdgeResult.MarkerFacts["StartTangentFailure"] = StartFailure;
                    }
                }
            }
        }
    }

    private static bool ValidateArrowPolygonAlignsWithStroke(
        RenderedArrowPolygon Arrow,
        string? PathData,
        (double X1, double Y1, double X2, double Y2)? LineSegment,
        bool AtTargetEnd,
        out string? FailureMessage)
    {
        FailureMessage = null;
        double FromX = Arrow.BaseCentreX;
        double FromY = Arrow.BaseCentreY;
        double TipX = Arrow.TipX;
        double TipY = Arrow.TipY;

        if (PathData is not null)
        {
            if (!FlowchartRenderedPathMarkerHelper.TryValidateMarkerAxisAlignsWithPath(
                    PathData,
                    TipX,
                    TipY,
                    FromX,
                    FromY,
                    AtTargetEnd,
                    out string PathFailure))
            {
                FailureMessage = PathFailure;
                return false;
            }

            return true;
        }

        if (LineSegment is null)
        {
            FailureMessage = "Rendered arrow could not be matched to a connector stroke.";
            return false;
        }

        (double LineX1, double LineY1, double LineX2, double LineY2) = LineSegment.Value;
        double LineDirX = LineX2 - LineX1;
        double LineDirY = LineY2 - LineY1;
        double LineLength = Math.Sqrt((LineDirX * LineDirX) + (LineDirY * LineDirY));
        if (LineLength < 1e-6)
        {
            FailureMessage = "Straight connector segment is degenerate.";
            return false;
        }

        LineDirX /= LineLength;
        LineDirY /= LineLength;

        double MarkerDirX = TipX - FromX;
        double MarkerDirY = TipY - FromY;
        double MarkerLength = Math.Sqrt((MarkerDirX * MarkerDirX) + (MarkerDirY * MarkerDirY));
        if (MarkerLength < 1e-6)
        {
            FailureMessage = "Arrow axis is degenerate.";
            return false;
        }

        MarkerDirX /= MarkerLength;
        MarkerDirY /= MarkerLength;

        if (!AtTargetEnd)
        {
            LineDirX = -LineDirX;
            LineDirY = -LineDirY;
        }

        double AlignmentDot = (LineDirX * MarkerDirX) + (LineDirY * MarkerDirY);
        if (AlignmentDot < 0.98)
        {
            FailureMessage = "Arrow axis diverges from straight connector direction (dot="
                + AlignmentDot.ToString("F3")
                + ").";
            return false;
        }

        return true;
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

    private static List<(double X1, double Y1, double X2, double Y2)> ExtractRenderedEdgeLineSegments(string Svg)
    {
        List<(double X1, double Y1, double X2, double Y2)> LineSegments =
            new List<(double X1, double Y1, double X2, double Y2)>();
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

    private static List<RenderedArrowPolygon> ExtractRenderedArrowPolygons(string Svg)
    {
        List<RenderedArrowPolygon> Arrows = new List<RenderedArrowPolygon>();
        MatchCollection Matches = Regex.Matches(
            Svg,
            @"<polygon points=""([^""]+)"" fill=""[^""]+"" stroke=""none""",
            RegexOptions.CultureInvariant);

        foreach (Match Match in Matches)
        {
            if (Match.Groups.Count < 2)
            {
                continue;
            }

            string[] PointTokens = Match.Groups[1].Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (PointTokens.Length < 3)
            {
                continue;
            }

            if (!TryParsePointToken(PointTokens[0], out double TipX, out double TipY)
                || !TryParsePointToken(PointTokens[1], out double Base1X, out double Base1Y)
                || !TryParsePointToken(PointTokens[2], out double Base2X, out double Base2Y))
            {
                continue;
            }

            Arrows.Add(new RenderedArrowPolygon
            {
                TipX = TipX,
                TipY = TipY,
                BaseCentreX = (Base1X + Base2X) / 2.0,
                BaseCentreY = (Base1Y + Base2Y) / 2.0
            });
        }

        return Arrows;
    }

    private static bool TryParsePointToken(string Token, out double X, out double Y)
    {
        X = 0.0;
        Y = 0.0;
        int CommaIndex = Token.IndexOf(',');
        if (CommaIndex < 0)
        {
            return false;
        }

        return double.TryParse(Token.Substring(0, CommaIndex), NumberStyles.Float, CultureInfo.InvariantCulture, out X)
            && double.TryParse(Token.Substring(CommaIndex + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out Y);
    }

    private sealed class RenderedArrowPolygon
    {
        public double TipX { get; set; }

        public double TipY { get; set; }

        public double BaseCentreX { get; set; }

        public double BaseCentreY { get; set; }
    }
}
