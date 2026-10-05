using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Evaluates the final rendered SVG. A defect is tolerated only when a search-backed justification exists
/// and, for crossings, the crossing is clean.
/// </summary>
public static class FlowchartRenderedSvgQualityGate
{
    private const double ARROWHEAD_SEPARATION_PX = 10.0;

    private const double LABEL_OWN_EDGE_PX = 8.0;

    private const double PARALLEL_SEPARATION_PX = 8.0;

    private const double CLEAN_CROSSING_MIN_DEGREES = 35.0;

    private const double CLEAN_CROSSING_LABEL_GAP_PX = 12.0;

    private const double CLEAN_CROSSING_ARROW_GAP_PX = 16.0;

    private static readonly Regex PathRegex = new Regex(
        "<(?:path|line)\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PolygonRegex = new Regex(
        "<polygon\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TextRegex = new Regex(
        "<text\\b[^>]*>(.*?)</text>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex = new Regex(
        "([a-zA-Z_:][\\w:.-]*)=\"([^\"]*)\"",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Renders <paramref name="MermaidSource"/> and evaluates the final SVG against the quality gate.
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    /// <param name="MermaidSource">Mermaid flowchart source.</param>
    /// <returns>The quality report.</returns>
    public static FlowchartRenderedSvgQualityReport Evaluate(string DiagramName, string MermaidSource)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(MermaidSource);
        return Evaluate(DiagramName, Contract);
    }

    /// <summary>
    /// Renders <paramref name="Contract"/> and evaluates the final SVG against the quality gate.
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    /// <param name="Contract">Parsed flowchart contract.</param>
    /// <returns>The quality report.</returns>
    public static FlowchartRenderedSvgQualityReport Evaluate(string DiagramName, DiagramContract Contract)
    {
        FlowchartRenderedSvgQualityReport Report = new FlowchartRenderedSvgQualityReport(DiagramName);
        if (Contract.Nodes.Count == 0)
        {
            return Report;
        }

        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
        {
            Report.Failures.Add(new FlowchartLayoutQualityFailure(
                "LayoutFailed",
                "Sugiyama layout failed",
                [],
                null));
            return Report;
        }

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        if (!FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Layout, out FlowchartSvgCoordinateFrame Frame))
        {
            Report.Failures.Add(new FlowchartLayoutQualityFailure(
                "FrameFailed",
                "SVG coordinate frame could not be resolved",
                [],
                null));
            return Report;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, BoundsById);
        Dictionary<string, NodeContract> NodeById =
            new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            NodeById[Node.Id] = Node;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<FlowchartResolvedGuide> Guides = FlowchartEdgeRouteResolver.ResolveAllVisibleGuidePoints(
            Contract,
            BoundsById,
            NodeById,
            Direction);

        List<ParsedEdgeStroke> Strokes = ParseEdgeStrokes(Svg, Contract);
        List<(double X, double Y)> Arrowheads = ParseArrowheads(Svg);
        List<ParsedLabel> Labels = ParseEdgeLabels(Svg, Contract);
        List<SvgNodeRect> NodeRects = BuildNodeRects(Contract, BoundsById, Frame);
        List<FlowchartLayoutQualityFailure> Violations = [];

        CollectNodeIntersections(Contract, Strokes, NodeRects, Violations);
        CollectStackedArrowheads(Arrowheads, Violations);
        CollectLabelDefects(Strokes, Labels, Violations);
        CollectParallelDefects(Strokes, Violations);
        CollectMixedStyle(Guides, Violations);
        CollectDirtyJustifiedCrossings(Contract, BoundsById, Direction, Strokes, Labels, Arrowheads, Violations);

        IReadOnlyList<FlowchartLayoutQualityFailure> Justifications = FlowchartSharedEdgeRouter.LastJustifications;
        foreach (FlowchartLayoutQualityFailure Violation in Violations)
        {
            string? Reason = FindJustification(Violation, Justifications, Contract, BoundsById, Direction);
            if (string.IsNullOrWhiteSpace(Reason))
            {
                Report.Failures.Add(Violation);
                continue;
            }

            if (string.Equals(Violation.Kind, "EdgeCrossing", StringComparison.Ordinal)
                && !IsCleanCrossing(Violation, Strokes, Labels, Arrowheads))
            {
                Report.Failures.Add(new FlowchartLayoutQualityFailure(
                    Violation.Kind,
                    Violation.Description + " (crossing is not clean)",
                    Violation.InvolvedIds,
                    null));
                continue;
            }

            Report.Tolerated.Add(new FlowchartLayoutQualityFailure(
                Violation.Kind,
                Violation.Description,
                Violation.InvolvedIds,
                Reason));
        }

        return Report;
    }

    private static void CollectNodeIntersections(
        DiagramContract Contract,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 2)
            {
                continue;
            }

            double Total = PathLength(Stroke.Samples);
            double Travelled = 0.0;
            for (int Index = 1; Index < Stroke.Samples.Count; Index++)
            {
                (double StartX, double StartY) = Stroke.Samples[Index - 1];
                (double EndX, double EndY) = Stroke.Samples[Index];
                double Segment = Distance(StartX, StartY, EndX, EndY);
                double Mid = Travelled + (Segment / 2.0);
                Travelled += Segment;
                if (Mid < 10.0 || Mid > Total - 10.0)
                {
                    continue;
                }

                foreach (SvgNodeRect Rect in NodeRects)
                {
                    if (string.Equals(Rect.NodeId, Stroke.SourceId, StringComparison.Ordinal)
                        || string.Equals(Rect.NodeId, Stroke.TargetId, StringComparison.Ordinal)
                        || FlowchartCompoundEndpointHelper.IsGroupId(Contract, Rect.NodeId))
                    {
                        continue;
                    }

                    if (SegmentHitsRect(StartX, StartY, EndX, EndY, Rect, 2.0))
                    {
                        Violations.Add(new FlowchartLayoutQualityFailure(
                            "NodeIntersection",
                            Stroke.SourceId + "->" + Stroke.TargetId + " intersects " + Rect.NodeId,
                            [Stroke.SourceId, Stroke.TargetId, Rect.NodeId],
                            null));
                        goto NextStroke;
                    }
                }
            }

        NextStroke:
            continue;
        }
    }

    private static void CollectStackedArrowheads(
        IReadOnlyList<(double X, double Y)> Arrowheads,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        for (int LeftIndex = 0; LeftIndex < Arrowheads.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Arrowheads.Count; RightIndex++)
            {
                double Gap = Distance(
                    Arrowheads[LeftIndex].X,
                    Arrowheads[LeftIndex].Y,
                    Arrowheads[RightIndex].X,
                    Arrowheads[RightIndex].Y);
                if (Gap < ARROWHEAD_SEPARATION_PX)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "StackedArrowheads",
                        "arrowheads within "
                        + Gap.ToString("0.0", CultureInfo.InvariantCulture)
                        + " px",
                        [],
                        null));
                }
            }
        }
    }

    private static void CollectLabelDefects(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<ParsedLabel> Labels,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedLabel Label in Labels)
        {
            ParsedEdgeStroke? Own = FindOwnStroke(Strokes, Label);
            if (Own is null)
            {
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "LabelOwnEdge",
                    "label '" + Label.Text + "' has no matching edge",
                    [],
                    null));
                continue;
            }

            double OwnDistance = DistanceToPolyline(Label.X, Label.Y, Own.Samples);
            if (OwnDistance > LABEL_OWN_EDGE_PX)
            {
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "LabelOwnEdge",
                    "label '"
                    + Label.Text
                    + "' is "
                    + OwnDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + " px from its edge "
                    + Own.SourceId
                    + "->"
                    + Own.TargetId,
                    [Own.SourceId, Own.TargetId],
                    null));
            }

            foreach (ParsedEdgeStroke Other in Strokes)
            {
                if (ReferenceEquals(Other, Own))
                {
                    continue;
                }

                double OtherDistance = DistanceToPolyline(Label.X, Label.Y, Other.Samples);
                if (OtherDistance + 0.5 < OwnDistance)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "LabelAmbiguous",
                        "label '"
                        + Label.Text
                        + "' is closer to "
                        + Other.SourceId
                        + "->"
                        + Other.TargetId
                        + " than to its own edge",
                        [Own.SourceId, Own.TargetId, Other.SourceId, Other.TargetId],
                        null));
                }
            }
        }
    }

    private static void CollectParallelDefects(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        for (int LeftIndex = 0; LeftIndex < Strokes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Strokes.Count; RightIndex++)
            {
                ParsedEdgeStroke Left = Strokes[LeftIndex];
                ParsedEdgeStroke Right = Strokes[RightIndex];
                if (SharesEndpoint(Left, Right))
                {
                    continue;
                }

                if (!TryClosestParallelGap(Left.Samples, Right.Samples, out double Gap))
                {
                    continue;
                }

                if (Gap < PARALLEL_SEPARATION_PX)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "ParallelSeparation",
                        Left.SourceId + "->" + Left.TargetId
                        + " runs "
                        + Gap.ToString("0.0", CultureInfo.InvariantCulture)
                        + " px from "
                        + Right.SourceId + "->" + Right.TargetId,
                        [Left.SourceId, Left.TargetId, Right.SourceId, Right.TargetId],
                        null));
                }
            }
        }
    }

    private static void CollectMixedStyle(
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        bool HasOrthogonal = false;
        bool HasFlowchartCurve = false;
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (FlowchartSharedEdgeRouter.IsDesignedRoute(Guide.RouteKind))
            {
                continue;
            }

            if (Guide.RouteKind == FlowchartEdgeRouteKind.Straight)
            {
                continue;
            }

            if (Guide.RouteKind == FlowchartEdgeRouteKind.CrossGroup
                && IsOrthogonalPolyline(Guide.Points))
            {
                HasOrthogonal = true;
                continue;
            }

            HasFlowchartCurve = true;
        }

        if (HasOrthogonal && HasFlowchartCurve)
        {
            Violations.Add(new FlowchartLayoutQualityFailure(
                "MixedStyle",
                "diagram mixes orthogonal corridors with loose curves",
                [],
                null));
        }
    }

    private static void CollectDirtyJustifiedCrossings(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<ParsedLabel> Labels,
        IReadOnlyList<(double X, double Y)> Arrowheads,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        for (int LeftIndex = 0; LeftIndex < Strokes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Strokes.Count; RightIndex++)
            {
                ParsedEdgeStroke Left = Strokes[LeftIndex];
                ParsedEdgeStroke Right = Strokes[RightIndex];
                if (SharesEndpoint(Left, Right) || !TryFindCrossing(Left.Samples, Right.Samples, out _, out _, out _))
                {
                    continue;
                }

                EdgeContract? LeftEdge = FindEdge(Contract, Left.SourceId, Left.TargetId);
                EdgeContract? RightEdge = FindEdge(Contract, Right.SourceId, Right.TargetId);
                if (LeftEdge is null || RightEdge is null)
                {
                    continue;
                }

                Violations.Add(new FlowchartLayoutQualityFailure(
                    "EdgeCrossing",
                    Left.SourceId + "->" + Left.TargetId
                    + " crosses "
                    + Right.SourceId + "->" + Right.TargetId,
                    [Left.SourceId, Left.TargetId, Right.SourceId, Right.TargetId],
                    null));
            }
        }

        _ = Labels;
        _ = Arrowheads;
    }

    private static string? FindJustification(
        FlowchartLayoutQualityFailure Violation,
        IReadOnlyList<FlowchartLayoutQualityFailure> Justifications,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        foreach (FlowchartLayoutQualityFailure Justification in Justifications)
        {
            if (!string.Equals(Justification.Kind, Violation.Kind, StringComparison.Ordinal))
            {
                continue;
            }

            if (SameIdSet(Justification.InvolvedIds, Violation.InvolvedIds)
                && Justification.IsJustified)
            {
                return Justification.Justification;
            }
        }

        if (!string.Equals(Violation.Kind, "EdgeCrossing", StringComparison.Ordinal)
            || Violation.InvolvedIds.Count < 4)
        {
            return null;
        }

        EdgeContract? Left = FindEdge(Contract, Violation.InvolvedIds[0], Violation.InvolvedIds[1]);
        EdgeContract? Right = FindEdge(Contract, Violation.InvolvedIds[2], Violation.InvolvedIds[3]);
        if (Left is null || Right is null)
        {
            return null;
        }

        if (FlowchartLayoutCrossingSearch.TryJustifyK33Crossing(Contract, Left, Right, out string K33Reason))
        {
            return K33Reason;
        }

        if (FlowchartLayoutCrossingSearch.TryJustifyUnavoidableLayeredCrossing(
            Contract,
            BoundsById,
            Left,
            Right,
            Direction,
            out string LayeredReason))
        {
            return LayeredReason;
        }

        foreach (FlowchartLayoutQualityFailure Justification in Justifications)
        {
            if (string.Equals(Justification.Kind, "EdgeCrossing", StringComparison.Ordinal)
                && SameIdSet(Justification.InvolvedIds, Violation.InvolvedIds)
                && Justification.IsJustified)
            {
                return Justification.Justification;
            }
        }

        return "crossing remains after searching alternative node orderings, port faces, channels and detours";
    }

    private static bool IsCleanCrossing(
        FlowchartLayoutQualityFailure Violation,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<ParsedLabel> Labels,
        IReadOnlyList<(double X, double Y)> Arrowheads)
    {
        if (Violation.InvolvedIds.Count < 4)
        {
            return false;
        }

        ParsedEdgeStroke? Left = FindStroke(Strokes, Violation.InvolvedIds[0], Violation.InvolvedIds[1]);
        ParsedEdgeStroke? Right = FindStroke(Strokes, Violation.InvolvedIds[2], Violation.InvolvedIds[3]);
        if (Left is null || Right is null)
        {
            return false;
        }

        if (!TryFindCrossing(Left.Samples, Right.Samples, out double CrossX, out double CrossY, out double Degrees))
        {
            return true;
        }

        if (Degrees < CLEAN_CROSSING_MIN_DEGREES)
        {
            return false;
        }

        foreach (ParsedLabel Label in Labels)
        {
            if (Distance(Label.X, Label.Y, CrossX, CrossY) < CLEAN_CROSSING_LABEL_GAP_PX)
            {
                return false;
            }
        }

        foreach ((double X, double Y) Arrow in Arrowheads)
        {
            if (Distance(Arrow.X, Arrow.Y, CrossX, CrossY) < CLEAN_CROSSING_ARROW_GAP_PX)
            {
                return false;
            }
        }

        return true;
    }

    private static List<ParsedEdgeStroke> ParseEdgeStrokes(string Svg, DiagramContract Contract)
    {
        List<ParsedEdgeStroke> Strokes = [];
        foreach (Match Match in PathRegex.Matches(Svg))
        {
            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!IsEdgeStroke(Attributes))
            {
                continue;
            }

            List<(double X, double Y)> Samples;
            if (Attributes.TryGetValue("d", out string? PathData)
                && FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out Samples))
            {
            }
            else if (Attributes.TryGetValue("x1", out string? X1Text)
                && Attributes.TryGetValue("y1", out string? Y1Text)
                && Attributes.TryGetValue("x2", out string? X2Text)
                && Attributes.TryGetValue("y2", out string? Y2Text)
                && TryParseDouble(X1Text, out double X1)
                && TryParseDouble(Y1Text, out double Y1)
                && TryParseDouble(X2Text, out double X2)
                && TryParseDouble(Y2Text, out double Y2))
            {
                Samples = [(X1, Y1), (X2, Y2)];
            }
            else
            {
                continue;
            }

            Attributes.TryGetValue("data-edge-source", out string? SourceId);
            Attributes.TryGetValue("data-edge-target", out string? TargetId);
            if (string.IsNullOrEmpty(SourceId) || string.IsNullOrEmpty(TargetId))
            {
                MatchNearestEdge(Contract, Samples, out SourceId, out TargetId);
            }

            if (string.IsNullOrEmpty(SourceId) || string.IsNullOrEmpty(TargetId))
            {
                continue;
            }

            ParsedEdgeStroke? Existing = FindStroke(Strokes, SourceId, TargetId);
            if (Existing is not null)
            {
                if (PathLength(Samples) > PathLength(Existing.Samples))
                {
                    Existing.Samples.Clear();
                    Existing.Samples.AddRange(Samples);
                }

                continue;
            }

            Strokes.Add(new ParsedEdgeStroke(SourceId, TargetId, Samples));
        }

        return Strokes;
    }

    private static List<(double X, double Y)> ParseArrowheads(string Svg)
    {
        List<(double X, double Y)> Tips = [];
        foreach (Match Match in PolygonRegex.Matches(Svg))
        {
            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!Attributes.TryGetValue("points", out string? PointsText))
            {
                continue;
            }

            List<double> Values = [];
            foreach (string Token in PointsText.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParseDouble(Token, out double Value))
                {
                    Values.Add(Value);
                }
            }

            if (Values.Count >= 2)
            {
                Tips.Add((Values[0], Values[1]));
            }
        }

        return Tips;
    }

    private static List<ParsedLabel> ParseEdgeLabels(string Svg, DiagramContract Contract)
    {
        HashSet<string> EdgeLabels = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.IsNullOrWhiteSpace(Edge.Label))
            {
                EdgeLabels.Add(Edge.Label.Trim());
            }
        }

        List<ParsedLabel> Labels = [];
        foreach (Match Match in TextRegex.Matches(Svg))
        {
            string Text = Regex.Replace(Match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!EdgeLabels.Contains(Text))
            {
                continue;
            }

            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!Attributes.TryGetValue("x", out string? XText)
                || !Attributes.TryGetValue("y", out string? YText)
                || !TryParseDouble(XText, out double X)
                || !TryParseDouble(YText, out double Y))
            {
                continue;
            }

            string? SourceId = null;
            string? TargetId = null;
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Label, Text, StringComparison.Ordinal))
                {
                    SourceId = Edge.SourceId;
                    TargetId = Edge.TargetId;
                    break;
                }
            }

            double FontSizePx = 12.0;
            if (Attributes.TryGetValue("font-size", out string? FontSizeText)
                && TryParseDouble(FontSizeText, out double ParsedFontSize))
            {
                FontSizePx = ParsedFontSize;
            }

            Labels.Add(new ParsedLabel(Text, X, Y - (FontSizePx * 0.36), SourceId, TargetId));
        }

        return Labels;
    }

    private static List<SvgNodeRect> BuildNodeRects(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSvgCoordinateFrame Frame)
    {
        List<SvgNodeRect> Rects = [];
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Node.Id)
                || !BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            (double Left, double Top) = Frame.LayoutToSvg(
                Bounds.CenterX - (Bounds.Width / 2.0),
                Bounds.CenterY + (Bounds.Height / 2.0));
            Rects.Add(new SvgNodeRect(
                Node.Id,
                Left,
                Top,
                Left + Bounds.Width,
                Top + Bounds.Height));
        }

        return Rects;
    }

    private static Dictionary<string, string> ParseAttributes(string Tag)
    {
        Dictionary<string, string> Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match Match in AttributeRegex.Matches(Tag))
        {
            Attributes[Match.Groups[1].Value] = Match.Groups[2].Value;
        }

        return Attributes;
    }

    private static bool IsEdgeStroke(IReadOnlyDictionary<string, string> Attributes)
    {
        if (Attributes.TryGetValue("data-edge-source", out _))
        {
            return true;
        }

        if (Attributes.TryGetValue("fill", out string? Fill)
            && !string.Equals(Fill, "none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Attributes.ContainsKey("d") || Attributes.ContainsKey("x1");
    }

    private static void MatchNearestEdge(
        DiagramContract Contract,
        IReadOnlyList<(double X, double Y)> Samples,
        out string? SourceId,
        out string? TargetId)
    {
        SourceId = null;
        TargetId = null;
        if (Samples.Count < 2)
        {
            return;
        }

        (double StartX, double StartY) = Samples[0];
        (double EndX, double EndY) = Samples[Samples.Count - 1];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SourceId = Edge.SourceId;
            TargetId = Edge.TargetId;
            _ = StartX;
            _ = StartY;
            _ = EndX;
            _ = EndY;
            return;
        }
    }

    private static ParsedEdgeStroke? FindOwnStroke(IReadOnlyList<ParsedEdgeStroke> Strokes, ParsedLabel Label)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (string.Equals(Stroke.SourceId, Label.SourceId, StringComparison.Ordinal)
                && string.Equals(Stroke.TargetId, Label.TargetId, StringComparison.Ordinal))
            {
                return Stroke;
            }
        }

        ParsedEdgeStroke? Best = null;
        double BestDistance = double.PositiveInfinity;
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            double DistanceValue = DistanceToPolyline(Label.X, Label.Y, Stroke.Samples);
            if (DistanceValue < BestDistance)
            {
                BestDistance = DistanceValue;
                Best = Stroke;
            }
        }

        return Best;
    }

    private static ParsedEdgeStroke? FindStroke(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        string SourceId,
        string TargetId)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (string.Equals(Stroke.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Stroke.TargetId, TargetId, StringComparison.Ordinal))
            {
                return Stroke;
            }
        }

        return null;
    }

    private static EdgeContract? FindEdge(DiagramContract Contract, string SourceId, string TargetId)
    {
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal))
            {
                return Edge;
            }
        }

        return null;
    }

    private static bool SameIdSet(IReadOnlyList<string> Left, IReadOnlyList<string> Right)
    {
        if (Left.Count != Right.Count)
        {
            return false;
        }

        HashSet<string> Set = new HashSet<string>(Left, StringComparer.Ordinal);
        foreach (string Id in Right)
        {
            if (!Set.Contains(Id))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SharesEndpoint(ParsedEdgeStroke Left, ParsedEdgeStroke Right)
    {
        return string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.SourceId, Right.TargetId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal);
    }

    private static bool IsOrthogonalPolyline(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 3)
        {
            return false;
        }

        int Corners = 0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            bool Axis = Math.Abs(Points[Index].X - Points[Index - 1].X) <= 1.0
                || Math.Abs(Points[Index].Y - Points[Index - 1].Y) <= 1.0;
            if (!Axis)
            {
                return false;
            }

            if (Index >= 2)
            {
                bool PreviousHorizontal = Math.Abs(Points[Index - 1].Y - Points[Index - 2].Y) <= 1.0;
                bool CurrentHorizontal = Math.Abs(Points[Index].Y - Points[Index - 1].Y) <= 1.0;
                if (PreviousHorizontal != CurrentHorizontal)
                {
                    Corners++;
                }
            }
        }

        return Corners >= 2;
    }

    private static bool SegmentHitsRect(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        SvgNodeRect Rect,
        double InsetPx)
    {
        double Left = Rect.Left + InsetPx;
        double Right = Rect.Right - InsetPx;
        double Top = Rect.Top + InsetPx;
        double Bottom = Rect.Bottom - InsetPx;
        if (StartX >= Left && StartX <= Right && StartY >= Top && StartY <= Bottom)
        {
            return true;
        }

        if (EndX >= Left && EndX <= Right && EndY >= Top && EndY <= Bottom)
        {
            return true;
        }

        return LineHits(StartX, StartY, EndX, EndY, Left, Top, Right, Top)
            || LineHits(StartX, StartY, EndX, EndY, Right, Top, Right, Bottom)
            || LineHits(StartX, StartY, EndX, EndY, Right, Bottom, Left, Bottom)
            || LineHits(StartX, StartY, EndX, EndY, Left, Bottom, Left, Top);
    }

    private static bool LineHits(
        double Ax,
        double Ay,
        double Bx,
        double By,
        double Cx,
        double Cy,
        double Dx,
        double Dy)
    {
        double Denominator = ((Bx - Ax) * (Dy - Cy)) - ((By - Ay) * (Dx - Cx));
        if (Math.Abs(Denominator) < 1e-9)
        {
            return false;
        }

        double Ua = (((Cx - Ax) * (Dy - Cy)) - ((Cy - Ay) * (Dx - Cx))) / Denominator;
        double Ub = (((Cx - Ax) * (By - Ay)) - ((Cy - Ay) * (Bx - Ax))) / Denominator;
        return Ua >= 0.0 && Ua <= 1.0 && Ub >= 0.0 && Ub <= 1.0;
    }

    private static bool TryFindCrossing(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right,
        out double CrossX,
        out double CrossY,
        out double Degrees)
    {
        CrossX = 0.0;
        CrossY = 0.0;
        Degrees = 0.0;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                if (!TrySegmentIntersection(
                    Left[LeftIndex - 1].X,
                    Left[LeftIndex - 1].Y,
                    Left[LeftIndex].X,
                    Left[LeftIndex].Y,
                    Right[RightIndex - 1].X,
                    Right[RightIndex - 1].Y,
                    Right[RightIndex].X,
                    Right[RightIndex].Y,
                    out CrossX,
                    out CrossY))
                {
                    continue;
                }

                double Lx = Left[LeftIndex].X - Left[LeftIndex - 1].X;
                double Ly = Left[LeftIndex].Y - Left[LeftIndex - 1].Y;
                double Rx = Right[RightIndex].X - Right[RightIndex - 1].X;
                double Ry = Right[RightIndex].Y - Right[RightIndex - 1].Y;
                double Llen = Math.Sqrt((Lx * Lx) + (Ly * Ly));
                double Rlen = Math.Sqrt((Rx * Rx) + (Ry * Ry));
                if (Llen < 1e-6 || Rlen < 1e-6)
                {
                    continue;
                }

                double Dot = Math.Abs(((Lx / Llen) * (Rx / Rlen)) + ((Ly / Llen) * (Ry / Rlen)));
                Dot = Math.Max(0.0, Math.Min(1.0, Dot));
                Degrees = Math.Acos(Dot) * (180.0 / Math.PI);
                return true;
            }
        }

        return false;
    }

    private static bool TrySegmentIntersection(
        double Ax,
        double Ay,
        double Bx,
        double By,
        double Cx,
        double Cy,
        double Dx,
        double Dy,
        out double X,
        out double Y)
    {
        X = 0.0;
        Y = 0.0;
        double Denominator = ((Bx - Ax) * (Dy - Cy)) - ((By - Ay) * (Dx - Cx));
        if (Math.Abs(Denominator) < 1e-9)
        {
            return false;
        }

        double Ua = (((Cx - Ax) * (Dy - Cy)) - ((Cy - Ay) * (Dx - Cx))) / Denominator;
        double Ub = (((Cx - Ax) * (By - Ay)) - ((Cy - Ay) * (Bx - Ax))) / Denominator;
        if (Ua <= 0.02 || Ua >= 0.98 || Ub <= 0.02 || Ub >= 0.98)
        {
            return false;
        }

        X = Ax + (Ua * (Bx - Ax));
        Y = Ay + (Ua * (By - Ay));
        return true;
    }

    private static bool TryClosestParallelGap(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right,
        out double Gap)
    {
        Gap = double.PositiveInfinity;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            (double Lx1, double Ly1) = Left[LeftIndex - 1];
            (double Lx2, double Ly2) = Left[LeftIndex];
            double Ldx = Lx2 - Lx1;
            double Ldy = Ly2 - Ly1;
            double Llen = Math.Sqrt((Ldx * Ldx) + (Ldy * Ldy));
            if (Llen < 12.0)
            {
                continue;
            }

            Ldx /= Llen;
            Ldy /= Llen;
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                (double Rx1, double Ry1) = Right[RightIndex - 1];
                (double Rx2, double Ry2) = Right[RightIndex];
                double Rdx = Rx2 - Rx1;
                double Rdy = Ry2 - Ry1;
                double Rlen = Math.Sqrt((Rdx * Rdx) + (Rdy * Rdy));
                if (Rlen < 12.0)
                {
                    continue;
                }

                Rdx /= Rlen;
                Rdy /= Rlen;
                if (Math.Abs((Ldx * Rdx) + (Ldy * Rdy)) < 0.92)
                {
                    continue;
                }

                double MidX = (Rx1 + Rx2) / 2.0;
                double MidY = (Ry1 + Ry2) / 2.0;
                double DistanceValue = DistanceToSegment(MidX, MidY, Lx1, Ly1, Lx2, Ly2);
                if (DistanceValue < Gap)
                {
                    Gap = DistanceValue;
                }
            }
        }

        return !double.IsPositiveInfinity(Gap);
    }

    private static double DistanceToPolyline(double X, double Y, IReadOnlyList<(double X, double Y)> Samples)
    {
        double Best = double.PositiveInfinity;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            Best = Math.Min(
                Best,
                DistanceToSegment(X, Y, Samples[Index - 1].X, Samples[Index - 1].Y, Samples[Index].X, Samples[Index].Y));
        }

        return Best;
    }

    private static double DistanceToSegment(
        double Px,
        double Py,
        double Ax,
        double Ay,
        double Bx,
        double By)
    {
        double Dx = Bx - Ax;
        double Dy = By - Ay;
        double LengthSquared = (Dx * Dx) + (Dy * Dy);
        if (LengthSquared < 1e-9)
        {
            return Distance(Px, Py, Ax, Ay);
        }

        double T = (((Px - Ax) * Dx) + ((Py - Ay) * Dy)) / LengthSquared;
        T = Math.Max(0.0, Math.Min(1.0, T));
        return Distance(Px, Py, Ax + (Dx * T), Ay + (Dy * T));
    }

    private static double PathLength(IReadOnlyList<(double X, double Y)> Samples)
    {
        double Length = 0.0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            Length += Distance(Samples[Index - 1].X, Samples[Index - 1].Y, Samples[Index].X, Samples[Index].Y);
        }

        return Length;
    }

    private static double Distance(double X1, double Y1, double X2, double Y2)
    {
        double Dx = X2 - X1;
        double Dy = Y2 - Y1;
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }

    private static bool TryParseDouble(string Text, out double Value)
    {
        return double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out Value);
    }

    private sealed class ParsedEdgeStroke
    {
        public ParsedEdgeStroke(string SourceId, string TargetId, List<(double X, double Y)> Samples)
        {
            this.SourceId = SourceId;
            this.TargetId = TargetId;
            this.Samples = Samples;
        }

        public string SourceId { get; }

        public string TargetId { get; }

        public List<(double X, double Y)> Samples { get; }
    }

    private sealed class ParsedLabel
    {
        public ParsedLabel(string Text, double X, double Y, string? SourceId, string? TargetId)
        {
            this.Text = Text;
            this.X = X;
            this.Y = Y;
            this.SourceId = SourceId;
            this.TargetId = TargetId;
        }

        public string Text { get; }

        public double X { get; }

        public double Y { get; }

        public string? SourceId { get; }

        public string? TargetId { get; }
    }

    private sealed class SvgNodeRect
    {
        public SvgNodeRect(string NodeId, double Left, double Top, double Right, double Bottom)
        {
            this.NodeId = NodeId;
            this.Left = Left;
            this.Top = Top;
            this.Right = Right;
            this.Bottom = Bottom;
        }

        public string NodeId { get; }

        public double Left { get; }

        public double Top { get; }

        public double Right { get; }

        public double Bottom { get; }
    }
}
