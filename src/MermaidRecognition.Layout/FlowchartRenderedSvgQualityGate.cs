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

    private const double CLEAN_CROSSING_MIN_DEGREES = 50.0;

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
        return Evaluate(DiagramName, Contract, ForcedLayout: null);
    }

    /// <summary>
    /// Evaluates a contract with an optional forced node placement (sample 03 oracle).
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    /// <param name="Contract">Parsed flowchart contract.</param>
    /// <param name="ForcedLayout">When set, skip Sugiyama and render these bounds.</param>
    /// <returns>The quality report.</returns>
    public static FlowchartRenderedSvgQualityReport Evaluate(
        string DiagramName,
        DiagramContract Contract,
        FlowchartLayoutResult? ForcedLayout)
    {
        FlowchartRenderedSvgQualityReport Report = new FlowchartRenderedSvgQualityReport(DiagramName);
        if (Contract.Nodes.Count == 0)
        {
            return Report;
        }

        FlowchartLayoutResult Layout;
        if (ForcedLayout != null)
        {
            Layout = ForcedLayout;
        }
        else if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out Layout))
        {
            Report.Failures.Add(new FlowchartLayoutQualityFailure(
                "LayoutFailed",
                "Sugiyama layout failed",
                [],
                null));
            return Report;
        }

        string Svg = ForcedLayout != null
            ? FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract, Layout)
            : FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        return EvaluateRenderedSvg(DiagramName, Contract, Layout, Svg, ApplyJustifications: true);
    }

    /// <summary>
    /// Evaluates a stored picture with no search justifications. Used to prove the gate still
    /// reports defects that are visible on a known picture.
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    /// <param name="MermaidSource">Mermaid flowchart source.</param>
    /// <param name="Svg">Rendered SVG markup.</param>
    /// <returns>The quality report.</returns>
    public static FlowchartRenderedSvgQualityReport EvaluatePicture(
        string DiagramName,
        string MermaidSource,
        string Svg)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(MermaidSource);
        FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout);
        return EvaluateRenderedSvg(DiagramName, Contract, Layout, Svg, ApplyJustifications: false);
    }

    /// <summary>
    /// Evaluates a stored SVG against the quality gate. Used to prove the gate still
    /// reports defects that are visible on a known picture.
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    /// <param name="MermaidSource">Mermaid flowchart source.</param>
    /// <param name="Svg">Rendered SVG markup.</param>
    /// <returns>The quality report.</returns>
    public static FlowchartRenderedSvgQualityReport EvaluateRenderedSvg(
        string DiagramName,
        string MermaidSource,
        string Svg)
    {
        return EvaluatePicture(DiagramName, MermaidSource, Svg);
    }

    /// <summary>
    /// Evaluates a stored SVG with an already parsed contract and layout.
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    /// <param name="Contract">Parsed flowchart contract.</param>
    /// <param name="Layout">Layout used for membership and justifications.</param>
    /// <param name="Svg">Rendered SVG markup.</param>
    /// <returns>The quality report.</returns>
    public static FlowchartRenderedSvgQualityReport EvaluateRenderedSvg(
        string DiagramName,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        string Svg)
    {
        return EvaluateRenderedSvg(DiagramName, Contract, Layout, Svg, ApplyJustifications: false);
    }

    /// <summary>
    /// Evaluates a stored SVG with an already parsed contract and layout.
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    /// <param name="Contract">Parsed flowchart contract.</param>
    /// <param name="Layout">Layout used for membership and justifications.</param>
    /// <param name="Svg">Rendered SVG markup.</param>
    /// <param name="ApplyJustifications">When true, search-backed reasons may tolerate some defects.</param>
    /// <returns>The quality report.</returns>
    public static FlowchartRenderedSvgQualityReport EvaluateRenderedSvg(
        string DiagramName,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        string Svg,
        bool ApplyJustifications)
    {
        FlowchartRenderedSvgQualityReport Report = new FlowchartRenderedSvgQualityReport(DiagramName);
        if (Contract.Nodes.Count == 0)
        {
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

        FlowchartRenderedSvgDocument Document = FlowchartRenderedSvgDocument.Parse(Svg, Contract);
        List<ParsedEdgeStroke> Strokes = Document.Strokes.Count > 0
            ? MapStrokes(Document)
            : ParseEdgeStrokes(Svg, Contract);
        List<(double X, double Y)> Arrowheads = Document.Arrowheads.Count > 0
            ? new List<(double X, double Y)>(Document.Arrowheads)
            : ParseArrowheads(Svg);
        List<ParsedLabel> Labels = Document.Labels.Count > 0
            ? MapLabels(Document)
            : ParseEdgeLabels(Svg, Contract);
        List<SvgNodeRect> NodeRects = MapNamedBoxes(Document, Contract, GroupsOnly: false);
        if (NodeRects.Count == 0
            && FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Layout, out FlowchartSvgCoordinateFrame Frame))
        {
            NodeRects = BuildNodeRects(Contract, BoundsById, Frame);
        }

        List<SvgNodeRect> GroupRects = MapNamedBoxes(Document, Contract, GroupsOnly: true);
        if (GroupRects.Count == 0
            && FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Layout, out FlowchartSvgCoordinateFrame GroupFrame))
        {
            GroupRects = BuildGroupRects(Contract, BoundsById, GroupFrame);
        }

        List<FlowchartLayoutQualityFailure> Violations = [];

        CollectNodeIntersections(Contract, Strokes, NodeRects, Violations);
        CollectStackedArrowheads(Arrowheads, Violations);
        CollectLabelDefects(Strokes, Labels, Violations);
        CollectLabelOnBox(Strokes, Labels, NodeRects, Violations);
        CollectParallelDefects(Strokes, Violations);
        CollectOverlayingEdges(Strokes, NodeRects, Violations);
        CollectMixedStyle(Guides, Violations);
        CollectMixedStyleFromStrokes(Strokes, Violations);
        CollectIllegalGroupTraversals(Contract, Strokes, GroupRects, Violations);
        CollectGroupInteriorDiagonals(Contract, Strokes, GroupRects, Violations);
        CollectHooks(Strokes, Violations);
        CollectCrossings(Strokes, NodeRects, Violations);
        CollectDetachedEndpoints(Strokes, NodeRects, GroupRects, Violations);
        CollectDanglingArrowheads(Strokes, Arrowheads, NodeRects, GroupRects, Violations);
        CollectRankGaps(NodeRects, Violations);
        CollectLongDiagonals(Strokes, Violations);
        CollectCrampedArrows(Strokes, Violations);

        IReadOnlyList<FlowchartLayoutQualityFailure> Justifications = ApplyJustifications
            ? FlowchartSharedEdgeRouter.LastJustifications
            : [];
        foreach (FlowchartLayoutQualityFailure Violation in Violations)
        {
            if (!ApplyJustifications || IsUnjustifiableKind(Violation.Kind))
            {
                Report.Failures.Add(Violation);
                continue;
            }

            string? Reason = FindJustification(Violation, Justifications, Contract, BoundsById, Direction);
            if (!FlowchartLayoutQualityJustification.IsSpecificReason(Reason))
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
            List<(double X, double Y)> Dense = Densify(Stroke.Samples, 6.0);
            double Travelled = 0.0;
            for (int Index = 1; Index < Dense.Count; Index++)
            {
                (double StartX, double StartY) = Dense[Index - 1];
                (double EndX, double EndY) = Dense[Index];
                double Segment = Distance(StartX, StartY, EndX, EndY);
                double Mid = Travelled + (Segment / 2.0);
                Travelled += Segment;
                if (Mid < 8.0 || Mid > Total - 8.0)
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

                    bool Hits = SegmentHitsRect(StartX, StartY, EndX, EndY, Rect, 1.0)
                        || (StartX >= Rect.Left + 1.0
                            && StartX <= Rect.Right - 1.0
                            && StartY >= Rect.Top + 1.0
                            && StartY <= Rect.Bottom - 1.0);
                    if (Hits)
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

            double OwnDistance = DistanceToLabelLine(Label.X, Label.Y, Own.Samples);
            if (OwnDistance > LABEL_OWN_EDGE_PX || PathLength(Own.Samples) < 22.0)
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

    private static bool IsUnjustifiableKind(string Kind)
    {
        return string.Equals(Kind, "DanglingArrowhead", StringComparison.Ordinal)
            || string.Equals(Kind, "DetachedEndpoint", StringComparison.Ordinal)
            || string.Equals(Kind, "OverlayingEdges", StringComparison.Ordinal)
            || string.Equals(Kind, "LabelOnBox", StringComparison.Ordinal)
            || string.Equals(Kind, "NodeIntersection", StringComparison.Ordinal)
            || string.Equals(Kind, "MixedStyle", StringComparison.Ordinal)
            || string.Equals(Kind, "ForeignGroupTraversal", StringComparison.Ordinal)
            || string.Equals(Kind, "GroupBorderDiagonal", StringComparison.Ordinal)
            || string.Equals(Kind, "RankGap", StringComparison.Ordinal)
            || string.Equals(Kind, "LongDiagonal", StringComparison.Ordinal)
            || string.Equals(Kind, "CrampedArrow", StringComparison.Ordinal);
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

    private static void CollectIllegalGroupTraversals(
        DiagramContract Contract,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (GroupRects.Count == 0)
        {
            return;
        }

        Dictionary<string, GroupContract> GroupById = FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        foreach (SvgNodeRect GroupRect in GroupRects)
        {
            foreach (ParsedEdgeStroke Stroke in Strokes)
            {
                bool SourceIn = IsEndpointInGroup(Contract, GroupById, Stroke.SourceId, GroupRect.NodeId);
                bool TargetIn = IsEndpointInGroup(Contract, GroupById, Stroke.TargetId, GroupRect.NodeId);
                int Crossings = CountRectBorderCrossings(Stroke.Samples, GroupRect);
                double InteriorLength = InteriorPathLength(Stroke.Samples, GroupRect);
                bool CrossingIsPerpendicular = LastCrossingIsPerpendicular(Stroke.Samples, GroupRect);

                if (!SourceIn && !TargetIn)
                {
                    if (InteriorLength > 12.0 || Crossings >= 1
                        || InteriorPathLength(Stroke.Samples, ExpandRect(GroupRect, 20.0)) > 24.0)
                    {
                        Violations.Add(new FlowchartLayoutQualityFailure(
                            "ForeignGroupTraversal",
                            Stroke.SourceId
                            + "->"
                            + Stroke.TargetId
                            + " cuts through foreign group "
                            + GroupRect.NodeId,
                            [Stroke.SourceId, Stroke.TargetId, GroupRect.NodeId],
                            null));
                    }

                    continue;
                }

                if (SourceIn == TargetIn)
                {
                    continue;
                }

                if (Crossings > 1)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "GroupBorderReentry",
                        Stroke.SourceId
                        + "->"
                        + Stroke.TargetId
                        + " crosses group "
                        + GroupRect.NodeId
                        + " more than once",
                        [Stroke.SourceId, Stroke.TargetId, GroupRect.NodeId],
                        null));
                }

                double NearestFace = NearestFaceDistanceToRect(Stroke, GroupRect, SourceIn);
                if (InteriorLength > NearestFace + 80.0)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "GroupBorderDiagonal",
                        Stroke.SourceId
                        + "->"
                        + Stroke.TargetId
                        + " takes a long diagonal through group "
                        + GroupRect.NodeId,
                        [Stroke.SourceId, Stroke.TargetId, GroupRect.NodeId],
                        null));
                }
                else if (Crossings >= 1 && !CrossingIsPerpendicular && InteriorLength > 48.0)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "GroupBorderDiagonal",
                        Stroke.SourceId
                        + "->"
                        + Stroke.TargetId
                        + " leaves group "
                        + GroupRect.NodeId
                        + " at an angle",
                        [Stroke.SourceId, Stroke.TargetId, GroupRect.NodeId],
                        null));
                }
            }
        }
    }

    private static void CollectGroupInteriorDiagonals(
        DiagramContract Contract,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        Dictionary<string, GroupContract> GroupById = FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 2)
            {
                continue;
            }

            if (!IsNearlyStraightDiagonal(Stroke.Samples))
            {
                continue;
            }

            foreach (SvgNodeRect GroupRect in GroupRects)
            {
                bool SourceIn = IsEndpointInGroup(Contract, GroupById, Stroke.SourceId, GroupRect.NodeId);
                bool TargetIn = IsEndpointInGroup(Contract, GroupById, Stroke.TargetId, GroupRect.NodeId);
                if (!SourceIn || !TargetIn)
                {
                    continue;
                }

                if (InteriorPathLength(Stroke.Samples, GroupRect) < 160.0)
                {
                    continue;
                }

                Violations.Add(new FlowchartLayoutQualityFailure(
                    "GroupInteriorDiagonal",
                    Stroke.SourceId
                    + "->"
                    + Stroke.TargetId
                    + " is a long diagonal across group "
                    + GroupRect.NodeId,
                    [Stroke.SourceId, Stroke.TargetId, GroupRect.NodeId],
                    null));
            }
        }
    }

    private static void CollectHooks(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (FlowchartSharedEdgeRouter.PathHasTerminalHook(Stroke.Samples))
            {
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "Hook",
                    Stroke.SourceId + "->" + Stroke.TargetId + " hooks back on itself",
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
            }
        }
    }

    private static void CollectCrossings(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        for (int LeftIndex = 0; LeftIndex < Strokes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Strokes.Count; RightIndex++)
            {
                ParsedEdgeStroke Left = Strokes[LeftIndex];
                ParsedEdgeStroke Right = Strokes[RightIndex];
                if (!TryFindCrossing(Left.Samples, Right.Samples, out double CrossX, out double CrossY, out _))
                {
                    continue;
                }

                if (SharesEndpoint(Left, Right)
                    && CrossingIsNearSharedEndpoint(Left, Right, CrossX, CrossY, NodeRects))
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
    }

    private static void CollectLabelOnBox(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<ParsedLabel> Labels,
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedLabel Label in Labels)
        {
            ParsedEdgeStroke? Own = FindOwnStroke(Strokes, Label);
            foreach (SvgNodeRect Rect in NodeRects)
            {
                if (Own != null
                    && (string.Equals(Rect.NodeId, Own.SourceId, StringComparison.Ordinal)
                        || string.Equals(Rect.NodeId, Own.TargetId, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (DistanceToRect(Label.X, Label.Y, Rect) <= 24.0)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "LabelOnBox",
                        "label '" + Label.Text + "' sits on " + Rect.NodeId,
                        Own == null ? [Rect.NodeId] : [Own.SourceId, Own.TargetId, Rect.NodeId],
                        null));
                }
            }
        }
    }

    private static void CollectOverlayingEdges(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        for (int LeftIndex = 0; LeftIndex < Strokes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Strokes.Count; RightIndex++)
            {
                ParsedEdgeStroke Left = Strokes[LeftIndex];
                ParsedEdgeStroke Right = Strokes[RightIndex];
                if (!TryOverlayRun(
                    Left.Samples,
                    Right.Samples,
                    out double MidX,
                    out double MidY,
                    out double Gap)
                    || Gap >= 8.0)
                {
                    continue;
                }

                if (SharesEndpoint(Left, Right)
                    && CrossingIsNearSharedEndpoint(Left, Right, MidX, MidY, NodeRects))
                {
                    continue;
                }

                Violations.Add(new FlowchartLayoutQualityFailure(
                    "OverlayingEdges",
                    Left.SourceId + "->" + Left.TargetId
                    + " runs on top of "
                    + Right.SourceId + "->" + Right.TargetId,
                    [Left.SourceId, Left.TargetId, Right.SourceId, Right.TargetId],
                    null));
            }
        }
    }

    private static void CollectMixedStyleFromStrokes(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        bool HasStraight = false;
        bool HasOrthogonal = false;
        bool HasCurve = false;
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (PathLength(Stroke.Samples) < 20.0)
            {
                continue;
            }

            StrokeStyleKind Style = ClassifyStrokeStyle(Stroke.Samples);
            if (Style == StrokeStyleKind.Straight)
            {
                HasStraight = true;
            }
            else if (Style == StrokeStyleKind.Orthogonal)
            {
                HasOrthogonal = true;
            }
            else
            {
                HasCurve = true;
            }
        }

        int Styles = 0;
        if (HasStraight)
        {
            Styles++;
        }

        if (HasOrthogonal)
        {
            Styles++;
        }

        if (HasCurve)
        {
            Styles++;
        }

        if (Styles >= 2)
        {
            Violations.Add(new FlowchartLayoutQualityFailure(
                "MixedStyle",
                "diagram mixes straight, right-angle and curved lines",
                [],
                null));
        }
    }

    private static void CollectDetachedEndpoints(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        List<SvgNodeRect> Boxes = [];
        Boxes.AddRange(NodeRects);
        Boxes.AddRange(GroupRects);
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 2)
            {
                continue;
            }

            SvgNodeRect? Source = FindRect(Boxes, Stroke.SourceId);
            SvgNodeRect? Target = FindRect(Boxes, Stroke.TargetId);
            if (Source != null
                && DistanceToRect(Stroke.Samples[0].X, Stroke.Samples[0].Y, Source) > 40.0)
            {
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "DetachedEndpoint",
                    Stroke.SourceId + "->" + Stroke.TargetId + " starts away from " + Stroke.SourceId,
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
            }

            if (Target != null
                && DistanceToRect(
                    Stroke.Samples[Stroke.Samples.Count - 1].X,
                    Stroke.Samples[Stroke.Samples.Count - 1].Y,
                    Target) > 40.0)
            {
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "DetachedEndpoint",
                    Stroke.SourceId + "->" + Stroke.TargetId + " is not attached to " + Stroke.TargetId,
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
            }
        }
    }

    private static void CollectDanglingArrowheads(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<(double X, double Y)> Arrowheads,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        List<SvgNodeRect> Boxes = [];
        Boxes.AddRange(NodeRects);
        Boxes.AddRange(GroupRects);
        foreach ((double TipX, double TipY) in Arrowheads)
        {
            bool NearBox = false;
            foreach (SvgNodeRect Rect in Boxes)
            {
                if (DistanceToRect(TipX, TipY, Rect) <= 40.0)
                {
                    NearBox = true;
                    break;
                }
            }

            if (NearBox)
            {
                continue;
            }

            ParsedEdgeStroke? Owner = FindNearestStrokeEnd(Strokes, TipX, TipY);
            Violations.Add(new FlowchartLayoutQualityFailure(
                "DanglingArrowhead",
                Owner == null
                    ? "arrowhead at ("
                        + TipX.ToString("0", CultureInfo.InvariantCulture)
                        + ","
                        + TipY.ToString("0", CultureInfo.InvariantCulture)
                        + ") is not attached to a box"
                    : Owner.SourceId + "->" + Owner.TargetId + " ends in a dangling arrowhead",
                Owner == null ? [] : [Owner.SourceId, Owner.TargetId],
                null));
        }

        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 2)
            {
                continue;
            }

            SvgNodeRect? Target = FindRect(Boxes, Stroke.TargetId);
            if (Target == null)
            {
                continue;
            }

            (double EndX, double EndY) = Stroke.Samples[Stroke.Samples.Count - 1];
            if (DistanceToRect(EndX, EndY, Target) <= 40.0)
            {
                continue;
            }

            bool AlreadyReported = false;
            foreach (FlowchartLayoutQualityFailure Existing in Violations)
            {
                if (string.Equals(Existing.Kind, "DanglingArrowhead", StringComparison.Ordinal)
                    && Existing.InvolvedIds.Count >= 2
                    && string.Equals(Existing.InvolvedIds[0], Stroke.SourceId, StringComparison.Ordinal)
                    && string.Equals(Existing.InvolvedIds[1], Stroke.TargetId, StringComparison.Ordinal))
                {
                    AlreadyReported = true;
                    break;
                }
            }

            if (AlreadyReported)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "DanglingArrowhead",
                Stroke.SourceId + "->" + Stroke.TargetId + " ends in a dangling arrowhead",
                [Stroke.SourceId, Stroke.TargetId],
                null));
        }
    }

    private static void CollectRankGaps(
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (NodeRects.Count < 4)
        {
            return;
        }

        List<double> Centres = [];
        foreach (SvgNodeRect Rect in NodeRects)
        {
            Centres.Add((Rect.Top + Rect.Bottom) / 2.0);
        }

        Centres.Sort();
        List<double> RankCentres = [Centres[0]];
        for (int Index = 1; Index < Centres.Count; Index++)
        {
            if (Centres[Index] - RankCentres[RankCentres.Count - 1] > 40.0)
            {
                RankCentres.Add(Centres[Index]);
            }
            else
            {
                RankCentres[RankCentres.Count - 1] = (RankCentres[RankCentres.Count - 1] + Centres[Index]) / 2.0;
            }
        }

        if (RankCentres.Count < 3)
        {
            return;
        }

        List<double> Gaps = [];
        double Largest = 0.0;
        for (int Index = 1; Index < RankCentres.Count; Index++)
        {
            double Gap = RankCentres[Index] - RankCentres[Index - 1];
            Gaps.Add(Gap);
            if (Gap > Largest)
            {
                Largest = Gap;
            }
        }

        Gaps.Sort();
        double Median = Gaps[Gaps.Count / 2];
        if (Largest > 200.0 && Largest > (Median * 2.2))
        {
            Violations.Add(new FlowchartLayoutQualityFailure(
                "RankGap",
                "empty band of "
                + Largest.ToString("0", CultureInfo.InvariantCulture)
                + " px between ranks",
                [],
                null));
        }
    }

    private static void CollectLongDiagonals(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 2)
            {
                continue;
            }

            (double StartX, double StartY) = Stroke.Samples[0];
            (double EndX, double EndY) = Stroke.Samples[Stroke.Samples.Count - 1];
            double Dx = Math.Abs(EndX - StartX);
            double Dy = Math.Abs(EndY - StartY);
            double Length = PathLength(Stroke.Samples);
            bool AxisAligned = Dx <= 8.0 || Dy <= 8.0;
            if (AxisAligned || Length < 180.0 || Math.Min(Dx, Dy) < 40.0)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "LongDiagonal",
                Stroke.SourceId + "->" + Stroke.TargetId + " is a long diagonal",
                [Stroke.SourceId, Stroke.TargetId],
                null));
        }
    }

    private static void CollectCrampedArrows(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            double Length = PathLength(Stroke.Samples);
            if (Length > 0.0 && Length < 26.0)
            {
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "CrampedArrow",
                    Stroke.SourceId + "->" + Stroke.TargetId
                    + " is a cramped "
                    + Length.ToString("0.0", CultureInfo.InvariantCulture)
                    + " px arrow",
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
            }
        }
    }

    private static StrokeStyleKind ClassifyStrokeStyle(IReadOnlyList<(double X, double Y)> Samples)
    {
        if (Samples.Count < 2)
        {
            return StrokeStyleKind.Straight;
        }

        bool AllAxis = true;
        int Corners = 0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            bool Horizontal = Math.Abs(Samples[Index].Y - Samples[Index - 1].Y) <= 1.5;
            bool Vertical = Math.Abs(Samples[Index].X - Samples[Index - 1].X) <= 1.5;
            if (!Horizontal && !Vertical)
            {
                AllAxis = false;
            }

            if (Index >= 2)
            {
                bool PreviousHorizontal = Math.Abs(Samples[Index - 1].Y - Samples[Index - 2].Y) <= 1.5;
                bool CurrentHorizontal = Math.Abs(Samples[Index].Y - Samples[Index - 1].Y) <= 1.5;
                if (PreviousHorizontal != CurrentHorizontal && (Horizontal || Vertical))
                {
                    Corners++;
                }
            }
        }

        if (AllAxis && Corners >= 1)
        {
            return StrokeStyleKind.Orthogonal;
        }

        if (AllAxis)
        {
            return StrokeStyleKind.Straight;
        }

        return StrokeStyleKind.Curve;
    }

    private static SvgNodeRect? FindRect(IReadOnlyList<SvgNodeRect> Rects, string NodeId)
    {
        foreach (SvgNodeRect Rect in Rects)
        {
            if (string.Equals(Rect.NodeId, NodeId, StringComparison.Ordinal))
            {
                return Rect;
            }
        }

        return null;
    }

    private static ParsedEdgeStroke? FindNearestStrokeEnd(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        double X,
        double Y)
    {
        ParsedEdgeStroke? Best = null;
        double BestDistance = 24.0;
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count == 0)
            {
                continue;
            }

            (double EndX, double EndY) = Stroke.Samples[Stroke.Samples.Count - 1];
            double Gap = Distance(X, Y, EndX, EndY);
            if (Gap < BestDistance)
            {
                BestDistance = Gap;
                Best = Stroke;
            }
        }

        return Best;
    }

    private static SvgNodeRect ExpandRect(SvgNodeRect Rect, double PaddingPx)
    {
        return new SvgNodeRect(
            Rect.NodeId,
            Rect.Left - PaddingPx,
            Rect.Top - PaddingPx,
            Rect.Right + PaddingPx,
            Rect.Bottom + PaddingPx);
    }

    private static bool TryOverlayRun(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right,
        out double MidX,
        out double MidY,
        out double Gap)
    {
        MidX = 0.0;
        MidY = 0.0;
        Gap = double.MaxValue;
        double BestRun = 0.0;
        double RunStartX = 0.0;
        double RunStartY = 0.0;
        double RunEndX = 0.0;
        double RunEndY = 0.0;
        double RunGap = double.MaxValue;
        bool InRun = false;
        List<(double X, double Y)> DenseLeft = Densify(Left, 8.0);
        for (int LeftIndex = 0; LeftIndex < DenseLeft.Count; LeftIndex++)
        {
            double LocalGap = DistanceToPolyline(DenseLeft[LeftIndex].X, DenseLeft[LeftIndex].Y, Right);
            if (LocalGap < 8.0)
            {
                if (!InRun)
                {
                    InRun = true;
                    RunStartX = DenseLeft[LeftIndex].X;
                    RunStartY = DenseLeft[LeftIndex].Y;
                    RunGap = LocalGap;
                }

                RunEndX = DenseLeft[LeftIndex].X;
                RunEndY = DenseLeft[LeftIndex].Y;
                RunGap = Math.Min(RunGap, LocalGap);
                continue;
            }

            if (InRun)
            {
                double RunLength = Distance(RunStartX, RunStartY, RunEndX, RunEndY);
                if (RunLength > BestRun)
                {
                    BestRun = RunLength;
                    MidX = (RunStartX + RunEndX) / 2.0;
                    MidY = (RunStartY + RunEndY) / 2.0;
                    Gap = RunGap;
                }

                InRun = false;
            }
        }

        if (InRun)
        {
            double RunLength = Distance(RunStartX, RunStartY, RunEndX, RunEndY);
            if (RunLength > BestRun)
            {
                BestRun = RunLength;
                MidX = (RunStartX + RunEndX) / 2.0;
                MidY = (RunStartY + RunEndY) / 2.0;
                Gap = RunGap;
            }
        }

        return BestRun >= 20.0;
    }

    private static List<(double X, double Y)> Densify(
        IReadOnlyList<(double X, double Y)> Samples,
        double StepPx)
    {
        List<(double X, double Y)> Dense = [];
        if (Samples.Count == 0)
        {
            return Dense;
        }

        Dense.Add(Samples[0]);
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double Dx = Samples[Index].X - Samples[Index - 1].X;
            double Dy = Samples[Index].Y - Samples[Index - 1].Y;
            double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            int Steps = Math.Max(1, (int)Math.Ceiling(Length / StepPx));
            for (int Step = 1; Step <= Steps; Step++)
            {
                double T = Step / (double)Steps;
                Dense.Add((Samples[Index - 1].X + (Dx * T), Samples[Index - 1].Y + (Dy * T)));
            }
        }

        return Dense;
    }

    private static bool TryClosestPair(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right,
        out double X,
        out double Y)
    {
        X = 0.0;
        Y = 0.0;
        double Best = double.MaxValue;
        for (int LeftIndex = 0; LeftIndex < Left.Count; LeftIndex++)
        {
            for (int RightIndex = 0; RightIndex < Right.Count; RightIndex++)
            {
                double Gap = Distance(Left[LeftIndex].X, Left[LeftIndex].Y, Right[RightIndex].X, Right[RightIndex].Y);
                if (Gap < Best)
                {
                    Best = Gap;
                    X = (Left[LeftIndex].X + Right[RightIndex].X) / 2.0;
                    Y = (Left[LeftIndex].Y + Right[RightIndex].Y) / 2.0;
                }
            }
        }

        return Best < double.MaxValue;
    }

    private static double DistanceToRect(double X, double Y, SvgNodeRect Rect)
    {
        if (X >= Rect.Left && X <= Rect.Right && Y >= Rect.Top && Y <= Rect.Bottom)
        {
            return 0.0;
        }

        double ClosestX = Math.Max(Rect.Left, Math.Min(Rect.Right, X));
        double ClosestY = Math.Max(Rect.Top, Math.Min(Rect.Bottom, Y));
        return Distance(X, Y, ClosestX, ClosestY);
    }

    private enum StrokeStyleKind
    {
        Straight = 0,
        Orthogonal = 1,
        Curve = 2
    }

    private static bool CrossingIsNearSharedEndpoint(
        ParsedEdgeStroke Left,
        ParsedEdgeStroke Right,
        double CrossX,
        double CrossY,
        IReadOnlyList<SvgNodeRect> NodeRects)
    {
        HashSet<string> Shared = [];
        if (string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.SourceId, Right.TargetId, StringComparison.Ordinal))
        {
            Shared.Add(Left.SourceId);
        }

        if (string.Equals(Left.TargetId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal))
        {
            Shared.Add(Left.TargetId);
        }

        foreach (string NodeId in Shared)
        {
            foreach (SvgNodeRect Rect in NodeRects)
            {
                if (!string.Equals(Rect.NodeId, NodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                double MidX = (Rect.Left + Rect.Right) / 2.0;
                double MidY = (Rect.Top + Rect.Bottom) / 2.0;
                if (Distance(CrossX, CrossY, MidX, MidY) <= 36.0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEndpointInGroup(
        DiagramContract Contract,
        Dictionary<string, GroupContract> GroupById,
        string EndpointId,
        string GroupId)
    {
        if (string.Equals(EndpointId, GroupId, StringComparison.Ordinal))
        {
            return true;
        }

        if (!GroupById.TryGetValue(GroupId, out GroupContract? Group))
        {
            return false;
        }

        if (FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(EndpointId, Group, Contract, GroupById))
        {
            return true;
        }

        if (GroupById.TryGetValue(EndpointId, out GroupContract? EndpointGroup))
        {
            string? Cursor = EndpointGroup.ParentGroupId;
            while (!string.IsNullOrEmpty(Cursor))
            {
                if (string.Equals(Cursor, GroupId, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!GroupById.TryGetValue(Cursor, out GroupContract? Parent))
                {
                    break;
                }

                Cursor = Parent.ParentGroupId;
            }
        }

        return false;
    }

    private static int CountRectBorderCrossings(IReadOnlyList<(double X, double Y)> Samples, SvgNodeRect Rect)
    {
        int Count = 0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            bool StartInside = PointInside(Samples[Index - 1].X, Samples[Index - 1].Y, Rect, 0.0);
            bool EndInside = PointInside(Samples[Index].X, Samples[Index].Y, Rect, 0.0);
            if (StartInside != EndInside)
            {
                Count++;
            }
        }

        return Count;
    }

    private static bool LastCrossingIsPerpendicular(IReadOnlyList<(double X, double Y)> Samples, SvgNodeRect Rect)
    {
        for (int Index = Samples.Count - 1; Index >= 1; Index--)
        {
            bool StartIn = PointInside(Samples[Index - 1].X, Samples[Index - 1].Y, Rect, 0.0);
            bool EndIn = PointInside(Samples[Index].X, Samples[Index].Y, Rect, 0.0);
            if (StartIn == EndIn)
            {
                continue;
            }

            double Dx = Math.Abs(Samples[Index].X - Samples[Index - 1].X);
            double Dy = Math.Abs(Samples[Index].Y - Samples[Index - 1].Y);
            return SegmentIsSquareToAxis(Dx, Dy);
        }

        return true;
    }

    private static double InteriorPathLength(IReadOnlyList<(double X, double Y)> Samples, SvgNodeRect Rect)
    {
        double Length = 0.0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double MidX = (Samples[Index - 1].X + Samples[Index].X) / 2.0;
            double MidY = (Samples[Index - 1].Y + Samples[Index].Y) / 2.0;
            if (PointInside(MidX, MidY, Rect, 2.0))
            {
                Length += Distance(
                    Samples[Index - 1].X,
                    Samples[Index - 1].Y,
                    Samples[Index].X,
                    Samples[Index].Y);
            }
        }

        return Length;
    }

    private static double NearestFaceDistance(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        ParsedEdgeStroke Stroke,
        string GroupId,
        bool SourceInside)
    {
        string InnerId = SourceInside ? Stroke.SourceId : Stroke.TargetId;
        if (!BoundsById.TryGetValue(InnerId, out FlowchartLayoutNodeBounds Inner)
            || !BoundsById.TryGetValue(GroupId, out FlowchartLayoutNodeBounds Group))
        {
            return 40.0;
        }

        double ToLeft = Math.Abs(Inner.CenterX - (Group.CenterX - (Group.Width / 2.0)));
        double ToRight = Math.Abs((Group.CenterX + (Group.Width / 2.0)) - Inner.CenterX);
        double ToTop = Math.Abs((Group.CenterY + (Group.Height / 2.0)) - Inner.CenterY);
        double ToBottom = Math.Abs(Inner.CenterY - (Group.CenterY - (Group.Height / 2.0)));
        return Math.Min(Math.Min(ToLeft, ToRight), Math.Min(ToTop, ToBottom));
    }

    private static bool PointInside(double X, double Y, SvgNodeRect Rect, double Inset)
    {
        return X >= Rect.Left + Inset
            && X <= Rect.Right - Inset
            && Y >= Rect.Top + Inset
            && Y <= Rect.Bottom - Inset;
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

        return null;
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

    private static double DistanceToLabelLine(double X, double Y, IReadOnlyList<(double X, double Y)> Samples)
    {
        double ToPath = DistanceToPolyline(X, Y, Samples);
        double AxisLength = 0.0;
        double Total = PathLength(Samples);
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double Segment = Distance(
                Samples[Index - 1].X,
                Samples[Index - 1].Y,
                Samples[Index].X,
                Samples[Index].Y);
            bool Axis = Math.Abs(Samples[Index].X - Samples[Index - 1].X) <= 2.0
                || Math.Abs(Samples[Index].Y - Samples[Index - 1].Y) <= 2.0;
            if (Axis && Segment > 16.0)
            {
                AxisLength += Segment;
            }
        }

        if (Total > 40.0 && AxisLength > (Total * 0.35))
        {
            double BestAxis = double.PositiveInfinity;
            for (int Index = 1; Index < Samples.Count; Index++)
            {
                bool Axis = Math.Abs(Samples[Index].X - Samples[Index - 1].X) <= 2.0
                    || Math.Abs(Samples[Index].Y - Samples[Index - 1].Y) <= 2.0;
                double Segment = Distance(
                    Samples[Index - 1].X,
                    Samples[Index - 1].Y,
                    Samples[Index].X,
                    Samples[Index].Y);
                if (!Axis || Segment <= 16.0)
                {
                    continue;
                }

                BestAxis = Math.Min(
                    BestAxis,
                    DistanceToSegment(
                        X,
                        Y,
                        Samples[Index - 1].X,
                        Samples[Index - 1].Y,
                        Samples[Index].X,
                        Samples[Index].Y));
            }

            if (!double.IsPositiveInfinity(BestAxis))
            {
                return BestAxis;
            }
        }

        return ToPath;
    }

    private static List<ParsedEdgeStroke> MapStrokes(FlowchartRenderedSvgDocument Document)
    {
        List<ParsedEdgeStroke> Strokes = [];
        foreach (FlowchartRenderedSvgStroke Stroke in Document.Strokes)
        {
            Strokes.Add(new ParsedEdgeStroke(Stroke.SourceId, Stroke.TargetId, Stroke.Samples));
        }

        return Strokes;
    }

    private static List<ParsedLabel> MapLabels(FlowchartRenderedSvgDocument Document)
    {
        List<ParsedLabel> Labels = [];
        foreach (FlowchartRenderedSvgLabel Label in Document.Labels)
        {
            Labels.Add(new ParsedLabel(Label.Text, Label.X, Label.Y, Label.SourceId, Label.TargetId));
        }

        return Labels;
    }

    private static bool IsNearlyStraightDiagonal(IReadOnlyList<(double X, double Y)> Samples)
    {
        if (Samples.Count < 2)
        {
            return false;
        }

        double Dx = Math.Abs(Samples[Samples.Count - 1].X - Samples[0].X);
        double Dy = Math.Abs(Samples[Samples.Count - 1].Y - Samples[0].Y);
        double Chord = Math.Sqrt((Dx * Dx) + (Dy * Dy));
        if (Dx <= 80.0 || Dy <= 80.0 || Chord <= 200.0)
        {
            return false;
        }

        double MaxDeviation = 0.0;
        for (int Index = 1; Index < Samples.Count - 1; Index++)
        {
            MaxDeviation = Math.Max(
                MaxDeviation,
                DistanceToSegment(
                    Samples[Index].X,
                    Samples[Index].Y,
                    Samples[0].X,
                    Samples[0].Y,
                    Samples[Samples.Count - 1].X,
                    Samples[Samples.Count - 1].Y));
        }

        return MaxDeviation <= 36.0;
    }

    private static List<SvgNodeRect> MapNamedBoxes(
        FlowchartRenderedSvgDocument Document,
        DiagramContract Contract,
        bool GroupsOnly)
    {
        HashSet<string> GroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupIds.Add(Group.Id);
        }

        List<SvgNodeRect> Rects = [];
        foreach (FlowchartRenderedSvgBox Box in Document.Boxes)
        {
            if (string.IsNullOrEmpty(Box.NodeId))
            {
                continue;
            }

            bool IsGroupId = GroupIds.Contains(Box.NodeId);
            if (GroupsOnly != IsGroupId)
            {
                continue;
            }

            Rects.Add(new SvgNodeRect(Box.NodeId, Box.Left, Box.Top, Box.Right, Box.Bottom));
        }

        return Rects;
    }

    private static List<SvgNodeRect> BuildGroupRects(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSvgCoordinateFrame Frame)
    {
        List<SvgNodeRect> Rects = [];
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            (double Left, double Top) = Frame.LayoutToSvg(
                Bounds.CenterX - (Bounds.Width / 2.0),
                Bounds.CenterY + (Bounds.Height / 2.0));
            Rects.Add(new SvgNodeRect(
                Group.Id,
                Left,
                Top,
                Left + Bounds.Width,
                Top + Bounds.Height));
        }

        return Rects;
    }

    private static double NearestFaceDistanceToRect(
        ParsedEdgeStroke Stroke,
        SvgNodeRect GroupRect,
        bool SourceInside)
    {
        (double InnerX, double InnerY) = SourceInside
            ? Stroke.Samples[0]
            : Stroke.Samples[Stroke.Samples.Count - 1];
        double ToLeft = Math.Abs(InnerX - GroupRect.Left);
        double ToRight = Math.Abs(GroupRect.Right - InnerX);
        double ToTop = Math.Abs(InnerY - GroupRect.Top);
        double ToBottom = Math.Abs(GroupRect.Bottom - InnerY);
        return Math.Min(Math.Min(ToLeft, ToRight), Math.Min(ToTop, ToBottom));
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

    private static bool SegmentIsSquareToAxis(double DeltaX, double DeltaY)
    {
        double AbsX = Math.Abs(DeltaX);
        double AbsY = Math.Abs(DeltaY);
        if (AbsX < 8.0 || AbsY < 8.0)
        {
            return true;
        }

        double Longer = Math.Max(AbsX, AbsY);
        double Shorter = Math.Min(AbsX, AbsY);
        return Shorter / Longer <= 0.25;
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
