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

    private const double PARALLEL_SEPARATION_PX = 10.0;

    private const double BORDER_HUG_PX = 6.0;

    private const double BORDER_HUG_MIN_PX = 28.0;

    private const double CORNER_ATTACH_PX = 12.0;

    private const double WIDE_FACE_PX = 100.0;

    private const double WIDE_CORNER_PX = 24.0;

    private const double TITLE_CLEARANCE_PX = 8.0;

    private const double GROUP_BORDER_CLEAR_PX = 16.0;

    private const double DOGLEG_STUB_PX = 16.0;

    private const double SHARED_STRETCH_MIN_PX = 36.0;

    private const double CANVAS_MARGIN_PX = 10.0;

    private const double LABEL_OFF_LINE_PX = 4.0;

    private const double SOURCE_DOGLEG_PX = 16.0;

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
        List<SvgNodeRect> LayoutNodeRects = [];
        if (FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Layout, out FlowchartSvgCoordinateFrame DoglegFrame))
        {
            LayoutNodeRects = BuildNodeRects(Contract, BoundsById, DoglegFrame);
        }

        List<SvgNodeRect> NodeRects = MapNamedBoxes(Document, Contract, GroupsOnly: false);
        if (NodeRects.Count == 0
            && FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Layout, out FlowchartSvgCoordinateFrame Frame))
        {
            NodeRects = BuildNodeRects(Contract, BoundsById, Frame);
        }

        AppendUnnamedBoxes(Document, NodeRects);

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
        FlowchartQualityGateProfile Profile = FlowchartQualityGateProfile.Detect(Svg);
        CollectMixedStyle(Guides, Violations);
        CollectMixedStyleFromStrokes(Strokes, Profile, Violations);
        CollectIllegalGroupTraversals(Contract, Strokes, GroupRects, Profile, Violations);
        CollectGroupInteriorDiagonals(Contract, Strokes, GroupRects, Violations);
        CollectHooks(Strokes, Violations);
        CollectCrossings(Strokes, NodeRects, Violations);
        CollectDetachedEndpoints(Strokes, NodeRects, GroupRects, Violations);
        CollectDanglingArrowheads(Strokes, Arrowheads, NodeRects, GroupRects, Violations);
        CollectRankGaps(NodeRects, Violations);
        CollectLongDiagonals(Strokes, Violations);
        CollectCrampedArrows(Strokes, Violations);
        CollectCanvasExits(Strokes, NodeRects, GroupRects, Svg, Violations);
        CollectLongDetours(Strokes, Violations);
        CollectBorderHugs(Strokes, NodeRects, GroupRects, Violations);
        CollectCornerArrowheads(Contract, Strokes, NodeRects, GroupRects, Violations);
        CollectOutlineGaps(Contract, Strokes, NodeRects, Violations);
        CollectSharedStretches(Strokes, Violations);
        CollectTextStrikes(Svg, Contract, Strokes, Labels, NodeRects, Violations);
        CollectCanvasMargins(Strokes, NodeRects, GroupRects, Svg, Violations);
        CollectSourceDoglegs(Strokes, NodeRects, GroupRects, Violations);
        CollectLabelsOffLine(Strokes, Labels, Violations);
        CollectDanglingStubs(Strokes, Violations);
        CollectTitleClearance(Svg, Contract, Strokes, NodeRects, Violations);
        CollectGroupBorderClearance(Strokes, GroupRects, Violations);
        CollectInteriorStrokes(Strokes, NodeRects, Violations);
        CollectNeedlessDoglegs(
            Strokes,
            LayoutNodeRects.Count > 0 ? LayoutNodeRects : NodeRects,
            GroupRects,
            Violations);
        CollectLabelOnStroke(Svg, Strokes, Labels, Violations);

        IReadOnlyList<FlowchartLayoutQualityFailure> Justifications = ApplyJustifications
            ? FlowchartSharedEdgeRouter.LastJustifications
            : [];
        foreach (FlowchartLayoutQualityFailure Violation in Violations)
        {
            string? Reason = FindJustification(Violation, Justifications, Contract, BoundsById, Direction);
            bool OccupiedCorridor = ApplyJustifications
                && FlowchartLayoutQualityJustification.IsOccupiedCorridorReason(Reason)
                && IsOccupiedCorridorKind(Violation.Kind);
            if ((!ApplyJustifications || IsUnjustifiableKind(Violation.Kind)) && !OccupiedCorridor)
            {
                Report.Failures.Add(Violation);
                continue;
            }

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
            || string.Equals(Kind, "CrampedArrow", StringComparison.Ordinal)
            || string.Equals(Kind, "CanvasExit", StringComparison.Ordinal)
            || string.Equals(Kind, "LongDetour", StringComparison.Ordinal)
            || string.Equals(Kind, "DanglingStub", StringComparison.Ordinal)
            || string.Equals(Kind, "TitleClearance", StringComparison.Ordinal)
            || string.Equals(Kind, "GroupBorderClearance", StringComparison.Ordinal)
            || string.Equals(Kind, "InteriorStroke", StringComparison.Ordinal)
            || string.Equals(Kind, "NeedlessDogleg", StringComparison.Ordinal)
            || string.Equals(Kind, "LabelOnStroke", StringComparison.Ordinal)
            || string.Equals(Kind, "OutlineGap", StringComparison.Ordinal);
    }

    private static bool IsOccupiedCorridorKind(string Kind)
    {
        return string.Equals(Kind, "LabelOnBox", StringComparison.Ordinal)
            || string.Equals(Kind, "LabelOwnEdge", StringComparison.Ordinal)
            || string.Equals(Kind, "GroupBorderReentry", StringComparison.Ordinal)
            || string.Equals(Kind, "ForeignGroupTraversal", StringComparison.Ordinal)
            || string.Equals(Kind, "OverlayingEdges", StringComparison.Ordinal);
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

            if (Guide.RouteKind == FlowchartEdgeRouteKind.Straight
                || Guide.RouteKind == FlowchartEdgeRouteKind.Corridor)
            {
                HasOrthogonal = true;
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
        FlowchartQualityGateProfile Profile,
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
                if (InteriorLength > NearestFace + 80.0 && !StrokeIsOrthogonal(Stroke.Samples))
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
                else if (Crossings >= 1
                    && !CrossingIsPerpendicular
                    && InteriorLength > 48.0
                    && !IsArchiMateOrthogonalStroke(Profile, Stroke))
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
                    && CrossingIsNearSharedPort(Left, Right, MidX, MidY))
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
        FlowchartQualityGateProfile Profile,
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
            else if (!Profile.TreatDashedAndSolidAsOneStyle)
            {
                HasCurve = true;
            }
            else if (PathLength(Stroke.Samples) >= 40.0 && !IsOrthogonalPolyline(Stroke.Samples))
            {
                HasCurve = true;
            }
        }

        bool HasRightAngle = HasStraight || HasOrthogonal;
        int Styles = 0;
        if (HasRightAngle)
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
            (double EndX, double EndY) = Stroke.Samples[Stroke.Samples.Count - 1];
            bool NearTarget = Target != null && DistanceToRect(EndX, EndY, Target) <= 40.0;
            if (!NearTarget)
            {
                foreach (SvgNodeRect Rect in Boxes)
                {
                    if (DistanceToRect(EndX, EndY, Rect) <= 40.0)
                    {
                        NearTarget = true;
                        break;
                    }
                }
            }

            if (NearTarget)
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

            bool AllAxisAligned = true;
            for (int Index = 1; Index < Stroke.Samples.Count; Index++)
            {
                bool SegmentHorizontal = Math.Abs(Stroke.Samples[Index].Y - Stroke.Samples[Index - 1].Y) <= 1.5;
                bool SegmentVertical = Math.Abs(Stroke.Samples[Index].X - Stroke.Samples[Index - 1].X) <= 1.5;
                if (!SegmentHorizontal && !SegmentVertical)
                {
                    AllAxisAligned = false;
                    break;
                }
            }

            if (AllAxisAligned)
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

    private static void CollectCanvasExits(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        string Svg,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        double MinX = double.PositiveInfinity;
        double MinY = double.PositiveInfinity;
        double MaxX = double.NegativeInfinity;
        double MaxY = double.NegativeInfinity;
        foreach (SvgNodeRect Rect in NodeRects)
        {
            MinX = Math.Min(MinX, Math.Min(Rect.Left, Rect.Right));
            MinY = Math.Min(MinY, Math.Min(Rect.Top, Rect.Bottom));
            MaxX = Math.Max(MaxX, Math.Max(Rect.Left, Rect.Right));
            MaxY = Math.Max(MaxY, Math.Max(Rect.Top, Rect.Bottom));
        }

        foreach (SvgNodeRect Rect in GroupRects)
        {
            MinX = Math.Min(MinX, Math.Min(Rect.Left, Rect.Right));
            MinY = Math.Min(MinY, Math.Min(Rect.Top, Rect.Bottom));
            MaxX = Math.Max(MaxX, Math.Max(Rect.Left, Rect.Right));
            MaxY = Math.Max(MaxY, Math.Max(Rect.Top, Rect.Bottom));
        }

        if (double.IsInfinity(MinX))
        {
            return;
        }

        MinX -= 16.0;
        MinY -= 16.0;
        MaxX += 16.0;
        MaxY += 16.0;
        if (TryParseViewBox(Svg, out double ViewMinX, out double ViewMinY, out double ViewMaxX, out double ViewMaxY))
        {
            MinX = Math.Max(MinX, ViewMinX);
            MinY = Math.Max(MinY, ViewMinY);
            MaxX = Math.Min(MaxX, ViewMaxX);
            MaxY = Math.Min(MaxY, ViewMaxY);
        }

        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            foreach ((double X, double Y) in Stroke.Samples)
            {
                if (X < MinX || X > MaxX || Y < MinY || Y > MaxY)
                {
                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "CanvasExit",
                        Stroke.SourceId + "->" + Stroke.TargetId + " leaves the canvas",
                        [Stroke.SourceId, Stroke.TargetId],
                        null));
                    break;
                }
            }
        }
    }

    private static bool TryParseViewBox(
        string Svg,
        out double MinX,
        out double MinY,
        out double MaxX,
        out double MaxY)
    {
        MinX = 0.0;
        MinY = 0.0;
        MaxX = 0.0;
        MaxY = 0.0;
        Match ViewBox = Regex.Match(
            Svg,
            "viewBox=\"([^\"]+)\"",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!ViewBox.Success)
        {
            return false;
        }

        string[] Parts = ViewBox.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (Parts.Length < 4)
        {
            return false;
        }

        if (!double.TryParse(Parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double OriginX)
            || !double.TryParse(Parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double OriginY)
            || !double.TryParse(Parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double Width)
            || !double.TryParse(Parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double Height))
        {
            return false;
        }

        MinX = OriginX;
        MinY = OriginY;
        MaxX = OriginX + Width;
        MaxY = OriginY + Height;
        return Width > 0.0 && Height > 0.0;
    }

    private static void CollectLongDetours(
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
            double Manhattan = Math.Abs(EndX - StartX) + Math.Abs(EndY - StartY);
            double Length = PathLength(Stroke.Samples);
            if (Length > (2.8 * Math.Max(Manhattan, 40.0)) && (Length - Manhattan) > 80.0)
            {
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "LongDetour",
                    Stroke.SourceId + "->" + Stroke.TargetId
                    + " detours "
                    + Length.ToString("0", CultureInfo.InvariantCulture)
                    + " px against a "
                    + Manhattan.ToString("0", CultureInfo.InvariantCulture)
                    + " px run",
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

    private static bool CrossingIsNearSharedPort(
        ParsedEdgeStroke Left,
        ParsedEdgeStroke Right,
        double CrossX,
        double CrossY)
    {
        if (Left.Samples.Count < 1 || Right.Samples.Count < 1)
        {
            return false;
        }

        (double LeftStartX, double LeftStartY) = Left.Samples[0];
        (double LeftEndX, double LeftEndY) = Left.Samples[Left.Samples.Count - 1];
        (double RightStartX, double RightStartY) = Right.Samples[0];
        (double RightEndX, double RightEndY) = Right.Samples[Right.Samples.Count - 1];
        const double PortRadiusPx = 20.0;
        if (string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            && Distance(CrossX, CrossY, LeftStartX, LeftStartY) <= PortRadiusPx
            && Distance(CrossX, CrossY, RightStartX, RightStartY) <= PortRadiusPx)
        {
            return true;
        }

        if (string.Equals(Left.SourceId, Right.TargetId, StringComparison.Ordinal)
            && Distance(CrossX, CrossY, LeftStartX, LeftStartY) <= PortRadiusPx
            && Distance(CrossX, CrossY, RightEndX, RightEndY) <= PortRadiusPx)
        {
            return true;
        }

        if (string.Equals(Left.TargetId, Right.SourceId, StringComparison.Ordinal)
            && Distance(CrossX, CrossY, LeftEndX, LeftEndY) <= PortRadiusPx
            && Distance(CrossX, CrossY, RightStartX, RightStartY) <= PortRadiusPx)
        {
            return true;
        }

        if (string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal)
            && Distance(CrossX, CrossY, LeftEndX, LeftEndY) <= PortRadiusPx
            && Distance(CrossX, CrossY, RightEndX, RightEndY) <= PortRadiusPx)
        {
            return true;
        }

        return false;
    }

    private static bool HasFarSideExcursion(IReadOnlyList<(double X, double Y)> Samples, double ThresholdPx)
    {
        if (Samples.Count < 2)
        {
            return false;
        }

        (double StartX, double StartY) = Samples[0];
        (double EndX, double EndY) = Samples[Samples.Count - 1];
        double MinX = Math.Min(StartX, EndX);
        double MaxX = Math.Max(StartX, EndX);
        double MinY = Math.Min(StartY, EndY);
        double MaxY = Math.Max(StartY, EndY);
        foreach ((double X, double Y) in Samples)
        {
            if (X < MinX - ThresholdPx
                || X > MaxX + ThresholdPx
                || Y < MinY - ThresholdPx
                || Y > MaxY + ThresholdPx)
            {
                return true;
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

            if (Justification.IsJustified
                && FlowchartLayoutQualityJustification.IsOccupiedCorridorReason(Justification.Justification)
                && IsOccupiedCorridorKind(Violation.Kind)
                && ContainsAllIds(Violation.InvolvedIds, Justification.InvolvedIds))
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

    private static void AppendUnnamedBoxes(
        FlowchartRenderedSvgDocument Document,
        List<SvgNodeRect> NodeRects)
    {
        int UnnamedIndex = 0;
        foreach (FlowchartRenderedSvgBox Box in Document.Boxes)
        {
            if (!string.IsNullOrEmpty(Box.NodeId) || Box.IsGroup)
            {
                continue;
            }

            bool AlreadyCovered = false;
            foreach (SvgNodeRect Existing in NodeRects)
            {
                double OverlapWidth = Math.Min(Existing.Right, Box.Right) - Math.Max(Existing.Left, Box.Left);
                double OverlapHeight = Math.Min(Existing.Bottom, Box.Bottom) - Math.Max(Existing.Top, Box.Top);
                if (OverlapWidth > 8.0 && OverlapHeight > 8.0)
                {
                    AlreadyCovered = true;
                    break;
                }
            }

            if (AlreadyCovered)
            {
                continue;
            }

            NodeRects.Add(new SvgNodeRect(
                "unnamed-" + UnnamedIndex.ToString(CultureInfo.InvariantCulture),
                Box.Left,
                Box.Top,
                Box.Right,
                Box.Bottom));
            UnnamedIndex++;
        }
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

    private static bool ContainsAllIds(IReadOnlyList<string> Haystack, IReadOnlyList<string> Needles)
    {
        if (Needles.Count == 0)
        {
            return false;
        }

        HashSet<string> Set = new HashSet<string>(Haystack, StringComparer.Ordinal);
        foreach (string Id in Needles)
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

    private static bool IsArchiMateOrthogonalStroke(
        FlowchartQualityGateProfile Profile,
        ParsedEdgeStroke Stroke)
    {
        if (!Profile.TreatDashedAndSolidAsOneStyle)
        {
            return false;
        }

        StrokeStyleKind Style = ClassifyStrokeStyle(Stroke.Samples);
        return Style == StrokeStyleKind.Straight || Style == StrokeStyleKind.Orthogonal;
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

                bool LeftHorizontal = Math.Abs(Ly2 - Ly1) <= 1.5;
                bool RightHorizontal = Math.Abs(Ry2 - Ry1) <= 1.5;
                bool LeftVertical = Math.Abs(Lx2 - Lx1) <= 1.5;
                bool RightVertical = Math.Abs(Rx2 - Rx1) <= 1.5;
                if (LeftHorizontal && RightHorizontal)
                {
                    double Overlap = AxisOverlap(Lx1, Lx2, Rx1, Rx2);
                    if (Overlap >= 16.0 && Math.Abs(Ly1 - Ry1) < Gap)
                    {
                        Gap = Math.Abs(Ly1 - Ry1);
                    }

                    continue;
                }

                if (LeftVertical && RightVertical)
                {
                    double Overlap = AxisOverlap(Ly1, Ly2, Ry1, Ry2);
                    if (Overlap >= 16.0 && Math.Abs(Lx1 - Rx1) < Gap)
                    {
                        Gap = Math.Abs(Lx1 - Rx1);
                    }

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

    private static double AxisOverlap(double A1, double A2, double B1, double B2)
    {
        double Start = Math.Max(Math.Min(A1, A2), Math.Min(B1, B2));
        double End = Math.Min(Math.Max(A1, A2), Math.Max(B1, B2));
        return End - Start;
    }

    private static void CollectBorderHugs(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        List<SvgNodeRect> Boxes = new List<SvgNodeRect>(NodeRects.Count + GroupRects.Count);
        Boxes.AddRange(NodeRects);
        Boxes.AddRange(GroupRects);
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            for (int Index = 1; Index < Stroke.Samples.Count; Index++)
            {
                (double X1, double Y1) = Stroke.Samples[Index - 1];
                (double X2, double Y2) = Stroke.Samples[Index];
                double Length = Distance(X1, Y1, X2, Y2);
                if (Length < BORDER_HUG_MIN_PX)
                {
                    continue;
                }

                bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5;
                bool Vertical = Math.Abs(X2 - X1) <= 1.5;
                if (!Horizontal && !Vertical)
                {
                    continue;
                }

                foreach (SvgNodeRect Box in Boxes)
                {
                    double Overlap;
                    double Separation;
                    string Side;
                    if (Horizontal)
                    {
                        Overlap = AxisOverlap(X1, X2, Box.Left, Box.Right);
                        double TopGap = Math.Abs(Y1 - Box.Top);
                        double BottomGap = Math.Abs(Y1 - Box.Bottom);
                        if (TopGap <= BottomGap)
                        {
                            Separation = TopGap;
                            Side = "top";
                        }
                        else
                        {
                            Separation = BottomGap;
                            Side = "bottom";
                        }
                    }
                    else
                    {
                        Overlap = AxisOverlap(Y1, Y2, Box.Top, Box.Bottom);
                        double LeftGap = Math.Abs(X1 - Box.Left);
                        double RightGap = Math.Abs(X1 - Box.Right);
                        if (LeftGap <= RightGap)
                        {
                            Separation = LeftGap;
                            Side = "left";
                        }
                        else
                        {
                            Separation = RightGap;
                            Side = "right";
                        }
                    }

                    if (Overlap < BORDER_HUG_MIN_PX || Separation > BORDER_HUG_PX)
                    {
                        continue;
                    }

                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "BorderHug",
                        Stroke.SourceId + "->" + Stroke.TargetId
                        + " runs along the "
                        + Side
                        + " of "
                        + Box.NodeId
                        + " for "
                        + Overlap.ToString("0.0", CultureInfo.InvariantCulture)
                        + " px at "
                        + Separation.ToString("0.0", CultureInfo.InvariantCulture)
                        + " px",
                        [Stroke.SourceId, Stroke.TargetId, Box.NodeId],
                        null));
                    break;
                }
            }
        }
    }

    private static void CollectCornerArrowheads(
        DiagramContract Contract,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        List<SvgNodeRect> Boxes = new List<SvgNodeRect>(NodeRects.Count + GroupRects.Count);
        Boxes.AddRange(NodeRects);
        Boxes.AddRange(GroupRects);
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 2)
            {
                continue;
            }

            ConsiderCorner(
                Contract,
                Stroke,
                Boxes,
                Stroke.Samples[0].X,
                Stroke.Samples[0].Y,
                Stroke.Samples[1].X,
                Stroke.Samples[1].Y,
                Stroke.SourceId,
                Violations);
            int Last = Stroke.Samples.Count - 1;
            ConsiderCorner(
                Contract,
                Stroke,
                Boxes,
                Stroke.Samples[Last].X,
                Stroke.Samples[Last].Y,
                Stroke.Samples[Last - 1].X,
                Stroke.Samples[Last - 1].Y,
                Stroke.TargetId,
                Violations);
        }
    }

    private static void ConsiderCorner(
        DiagramContract Contract,
        ParsedEdgeStroke Stroke,
        IReadOnlyList<SvgNodeRect> Boxes,
        double TipX,
        double TipY,
        double PrevX,
        double PrevY,
        string EndId,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (IsRoundedOutlineShape(Contract, EndId))
        {
            return;
        }

        double Run = Distance(PrevX, PrevY, TipX, TipY);
        bool ApproachHorizontal = Math.Abs(TipY - PrevY) <= 1.5 && Run > 8.0;
        bool ApproachVertical = Math.Abs(TipX - PrevX) <= 1.5 && Run > 8.0;
        foreach (SvgNodeRect Box in Boxes)
        {
            if (!string.Equals(Box.NodeId, EndId, StringComparison.Ordinal))
            {
                continue;
            }

            double FaceLength = ApproachVertical
                ? Box.Right - Box.Left
                : Box.Bottom - Box.Top;
            if (FaceLength < 56.0)
            {
                continue;
            }

            double Limit = FaceLength >= WIDE_FACE_PX ? WIDE_CORNER_PX : CORNER_ATTACH_PX;
            double CornerDistance = DistanceToNearestCorner(TipX, TipY, Box);
            bool Tangential = false;
            if (ApproachVertical && (Math.Abs(TipX - Box.Left) <= BORDER_HUG_PX || Math.Abs(TipX - Box.Right) <= BORDER_HUG_PX))
            {
                Tangential = true;
            }

            if (ApproachHorizontal && (Math.Abs(TipY - Box.Top) <= BORDER_HUG_PX || Math.Abs(TipY - Box.Bottom) <= BORDER_HUG_PX))
            {
                Tangential = true;
            }

            if (CornerDistance >= Limit && !Tangential)
            {
                continue;
            }

            if (CornerDistance >= Limit && Tangential && Run < BORDER_HUG_MIN_PX)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "CornerArrowhead",
                Stroke.SourceId + "->" + Stroke.TargetId
                + " meets "
                + Box.NodeId
                + " "
                + CornerDistance.ToString("0.0", CultureInfo.InvariantCulture)
                + " px from a corner",
                [Stroke.SourceId, Stroke.TargetId, Box.NodeId],
                null));
            break;
        }
    }

    private static void CollectOutlineGaps(
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

            ConsiderOutlineGap(Contract, Stroke, NodeRects, Stroke.SourceId, Stroke.Samples[0].X, Stroke.Samples[0].Y, AtStart: true, Violations);
            int Last = Stroke.Samples.Count - 1;
            ConsiderOutlineGap(
                Contract,
                Stroke,
                NodeRects,
                Stroke.TargetId,
                Stroke.Samples[Last].X,
                Stroke.Samples[Last].Y,
                AtStart: false,
                Violations);
        }
    }

    private static void ConsiderOutlineGap(
        DiagramContract Contract,
        ParsedEdgeStroke Stroke,
        IReadOnlyList<SvgNodeRect> NodeRects,
        string EndId,
        double X,
        double Y,
        bool AtStart,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (!TryOutlineRadius(Contract, EndId, out double Radius))
        {
            return;
        }

        foreach (SvgNodeRect Box in NodeRects)
        {
            if (!string.Equals(Box.NodeId, EndId, StringComparison.Ordinal))
            {
                continue;
            }

            double Gap = DistanceOutsideRounded(X, Y, Box, Radius);
            double Limit = AtStart ? 2.5 : 12.0;
            if (Gap <= Limit)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "OutlineGap",
                Stroke.SourceId + "->" + Stroke.TargetId
                + (AtStart ? " starts " : " ends ")
                + Gap.ToString("0.0", CultureInfo.InvariantCulture)
                + " px off the outline of "
                + EndId,
                [Stroke.SourceId, Stroke.TargetId, EndId],
                null));
            break;
        }
    }

    private static bool IsRoundedOutlineShape(DiagramContract Contract, string NodeId)
    {
        return TryOutlineRadius(Contract, NodeId, out _);
    }

    private static bool TryOutlineRadius(DiagramContract Contract, string NodeId, out double Radius)
    {
        Radius = 0.0;
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!string.Equals(Node.Id, NodeId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(Node.Shape))
            {
                continue;
            }

            string Normalised = Node.Shape.Trim().ToLowerInvariant();
            if (string.Equals(Normalised, "stadium", StringComparison.Ordinal))
            {
                Radius = -1.0;
                return true;
            }

            if (string.Equals(Normalised, "rounded", StringComparison.Ordinal))
            {
                Radius = 5.0;
                return true;
            }
        }

        return false;
    }

    private static double DistanceOutsideRounded(double X, double Y, SvgNodeRect Box, double Radius)
    {
        double Height = Box.Bottom - Box.Top;
        double Width = Box.Right - Box.Left;
        bool Stadium = Radius < 0.0;
        double Used = Stadium ? Height / 2.0 : Math.Min(Radius, Math.Min(Width / 2.0, Height / 2.0));
        if (Used < 1.0)
        {
            return 0.0;
        }

        // Parsed stadium paths omit the arc bulge, so the box is the flat inner rectangle.
        double Left = Stadium ? Box.Left - Used : Box.Left;
        double Right = Stadium ? Box.Right + Used : Box.Right;
        double CentreX = (Left + Right) / 2.0;
        double CentreY = (Box.Top + Box.Bottom) / 2.0;
        double FlatLeft = Left + Used;
        double FlatRight = Right - Used;
        if (X >= FlatLeft && X <= FlatRight)
        {
            if (Y >= Box.Top && Y <= Box.Bottom)
            {
                return 0.0;
            }

            return Y < Box.Top ? Box.Top - Y : Y - Box.Bottom;
        }

        double CapX = X < CentreX ? FlatLeft : FlatRight;
        double Distance = Math.Sqrt(Math.Pow(X - CapX, 2.0) + Math.Pow(Y - CentreY, 2.0));
        return Math.Max(0.0, Distance - Used);
    }

    private static double DistanceToNearestCorner(double X, double Y, SvgNodeRect Box)
    {
        double Best = double.PositiveInfinity;
        double[] Xs = [Box.Left, Box.Right];
        double[] Ys = [Box.Top, Box.Bottom];
        foreach (double CornerX in Xs)
        {
            foreach (double CornerY in Ys)
            {
                Best = Math.Min(Best, Distance(X, Y, CornerX, CornerY));
            }
        }

        return Best;
    }

    private static void CollectSharedStretches(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        for (int LeftIndex = 0; LeftIndex < Strokes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Strokes.Count; RightIndex++)
            {
                ParsedEdgeStroke Left = Strokes[LeftIndex];
                ParsedEdgeStroke Right = Strokes[RightIndex];
                double Shared = LongestSharedRun(Left.Samples, Right.Samples);
                if (Shared < SHARED_STRETCH_MIN_PX)
                {
                    continue;
                }

                Violations.Add(new FlowchartLayoutQualityFailure(
                    "SharedStretch",
                    Left.SourceId + "->" + Left.TargetId
                    + " shares "
                    + Shared.ToString("0.0", CultureInfo.InvariantCulture)
                    + " px with "
                    + Right.SourceId + "->" + Right.TargetId,
                    [Left.SourceId, Left.TargetId, Right.SourceId, Right.TargetId],
                    null));
            }
        }
    }

    private static double LongestSharedRun(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        double Best = 0.0;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            (double Lx1, double Ly1) = Left[LeftIndex - 1];
            (double Lx2, double Ly2) = Left[LeftIndex];
            bool LeftHorizontal = Math.Abs(Ly2 - Ly1) <= 1.5;
            bool LeftVertical = Math.Abs(Lx2 - Lx1) <= 1.5;
            if (!LeftHorizontal && !LeftVertical)
            {
                continue;
            }

            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                (double Rx1, double Ry1) = Right[RightIndex - 1];
                (double Rx2, double Ry2) = Right[RightIndex];
                double Overlap = 0.0;
                if (LeftHorizontal && Math.Abs(Ry2 - Ry1) <= 1.5 && Math.Abs(Ly1 - Ry1) <= 2.5)
                {
                    Overlap = AxisOverlap(Lx1, Lx2, Rx1, Rx2);
                }
                else if (LeftVertical && Math.Abs(Rx2 - Rx1) <= 1.5 && Math.Abs(Lx1 - Rx1) <= 2.5)
                {
                    Overlap = AxisOverlap(Ly1, Ly2, Ry1, Ry2);
                }

                if (Overlap > Best)
                {
                    Best = Overlap;
                }
            }
        }

        return Best;
    }

    private static void CollectTextStrikes(
        string Svg,
        DiagramContract Contract,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<ParsedLabel> Labels,
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (Match Match in TextRegex.Matches(Svg))
        {
            string Text = Regex.Replace(Match.Groups[1].Value, "<[^>]+>", " ").Trim();
            Text = Regex.Replace(Text, @"\s+", " ");
            if (Text.Length == 0)
            {
                continue;
            }

            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!TryReadTextAnchor(Attributes, Match.Value, out double AnchorX, out double AnchorY, out double FontSize))
            {
                continue;
            }

            double Width = Math.Max(12.0, Text.Length * FontSize * 0.52);
            double Left = AnchorX - (Width / 2.0);
            double Right = AnchorX + (Width / 2.0);
            if (Attributes.TryGetValue("text-anchor", out string? Anchor) && string.Equals(Anchor, "start", StringComparison.OrdinalIgnoreCase))
            {
                Left = AnchorX;
                Right = AnchorX + Width;
            }

            double Top = AnchorY - (FontSize * 0.85);
            double Bottom = AnchorY + (FontSize * 0.25);
            bool EdgeLabel = false;
            string? LabelSource = null;
            string? LabelTarget = null;
            foreach (ParsedLabel Label in Labels)
            {
                if (string.Equals(Label.Text, Text, StringComparison.Ordinal))
                {
                    EdgeLabel = true;
                    LabelSource = Label.SourceId;
                    LabelTarget = Label.TargetId;
                    break;
                }
            }

            if (!EdgeLabel && PointInsideAny(NodeRects, AnchorX, AnchorY))
            {
                continue;
            }

            foreach (ParsedEdgeStroke Stroke in Strokes)
            {
                if (!PolylineHitsRect(Stroke.Samples, Left, Top, Right, Bottom))
                {
                    continue;
                }

                if (EdgeLabel
                    && string.Equals(Stroke.SourceId, LabelSource, StringComparison.Ordinal)
                    && string.Equals(Stroke.TargetId, LabelTarget, StringComparison.Ordinal))
                {
                    double OwnDistance = DistanceToPolyline(AnchorX, AnchorY - (FontSize * 0.35), Stroke.Samples);
                    if (OwnDistance <= LABEL_OFF_LINE_PX)
                    {
                        continue;
                    }
                }

                string Kind = EdgeLabel ? "LabelStrike" : "TitleStrike";
                Violations.Add(new FlowchartLayoutQualityFailure(
                    Kind,
                    Stroke.SourceId + "->" + Stroke.TargetId + " crosses '" + Text + "'",
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
                break;
            }

            _ = Contract;
        }
    }

    private static bool TryReadTextAnchor(
        Dictionary<string, string> Attributes,
        string Token,
        out double AnchorX,
        out double AnchorY,
        out double FontSize)
    {
        AnchorX = 0.0;
        AnchorY = 0.0;
        FontSize = 16.0;
        if (!Attributes.TryGetValue("x", out string? XText) || !TryParseLeadingDouble(XText, out AnchorX))
        {
            Match Tspan = Regex.Match(Token, "x=\"([^\"]+)\"", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!Tspan.Success || !TryParseLeadingDouble(Tspan.Groups[1].Value, out AnchorX))
            {
                return false;
            }
        }

        if (!Attributes.TryGetValue("y", out string? YText) || !TryParseLeadingDouble(YText, out AnchorY))
        {
            return false;
        }

        if (Attributes.TryGetValue("font-size", out string? FontText) && TryParseLeadingDouble(FontText, out double ParsedSize))
        {
            FontSize = ParsedSize;
        }

        return true;
    }

    private static bool TryParseLeadingDouble(string Text, out double Value)
    {
        Value = 0.0;
        if (string.IsNullOrWhiteSpace(Text))
        {
            return false;
        }

        int End = 0;
        string Trimmed = Text.Trim();
        while (End < Trimmed.Length
            && (char.IsDigit(Trimmed[End]) || Trimmed[End] == '.' || Trimmed[End] == '-' || Trimmed[End] == '+'))
        {
            End++;
        }

        if (End == 0)
        {
            return false;
        }

        return TryParseDouble(Trimmed.Substring(0, End), out Value);
    }

    private static bool PointInsideAny(IReadOnlyList<SvgNodeRect> Rects, double X, double Y)
    {
        foreach (SvgNodeRect Rect in Rects)
        {
            if (X > Rect.Left + 2.0 && X < Rect.Right - 2.0 && Y > Rect.Top + 2.0 && Y < Rect.Bottom - 2.0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PolylineHitsRect(
        IReadOnlyList<(double X, double Y)> Samples,
        double Left,
        double Top,
        double Right,
        double Bottom)
    {
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            (double X1, double Y1) = Samples[Index - 1];
            (double X2, double Y2) = Samples[Index];
            double MinX = Math.Min(X1, X2);
            double MaxX = Math.Max(X1, X2);
            double MinY = Math.Min(Y1, Y2);
            double MaxY = Math.Max(Y1, Y2);
            if (MaxX < Left || MinX > Right || MaxY < Top || MinY > Bottom)
            {
                continue;
            }

            if (Math.Abs(Y2 - Y1) <= 1.5 && Y1 > Top && Y1 < Bottom && AxisOverlap(X1, X2, Left, Right) > 4.0)
            {
                return true;
            }

            if (Math.Abs(X2 - X1) <= 1.5 && X1 > Left && X1 < Right && AxisOverlap(Y1, Y2, Top, Bottom) > 4.0)
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectCanvasMargins(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        string Svg,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (!TryParseViewBox(Svg, out double ViewMinX, out double ViewMinY, out double ViewMaxX, out double ViewMaxY))
        {
            return;
        }

        List<SvgNodeRect> Boxes = new List<SvgNodeRect>(NodeRects.Count + GroupRects.Count);
        Boxes.AddRange(NodeRects);
        Boxes.AddRange(GroupRects);
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            foreach ((double X, double Y) in Stroke.Samples)
            {
                double Margin = Math.Min(Math.Min(X - ViewMinX, ViewMaxX - X), Math.Min(Y - ViewMinY, ViewMaxY - Y));
                if (Margin >= CANVAS_MARGIN_PX || PointLiesOnBox(Boxes, X, Y))
                {
                    continue;
                }

                Violations.Add(new FlowchartLayoutQualityFailure(
                    "CanvasMargin",
                    Stroke.SourceId + "->" + Stroke.TargetId
                    + " is "
                    + Margin.ToString("0.0", CultureInfo.InvariantCulture)
                    + " px from the canvas edge",
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
                break;
            }
        }
    }

    private static bool PointLiesOnBox(IReadOnlyList<SvgNodeRect> Boxes, double X, double Y)
    {
        foreach (SvgNodeRect Box in Boxes)
        {
            bool InsideX = X >= Box.Left - 2.0 && X <= Box.Right + 2.0;
            bool InsideY = Y >= Box.Top - 2.0 && Y <= Box.Bottom + 2.0;
            if (!InsideX || !InsideY)
            {
                continue;
            }

            double EdgeDistance = Math.Min(
                Math.Min(Math.Abs(X - Box.Left), Math.Abs(X - Box.Right)),
                Math.Min(Math.Abs(Y - Box.Top), Math.Abs(Y - Box.Bottom)));
            if (EdgeDistance <= 2.5)
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectSourceDoglegs(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        List<SvgNodeRect> Boxes = new List<SvgNodeRect>(NodeRects.Count + GroupRects.Count);
        Boxes.AddRange(NodeRects);
        Boxes.AddRange(GroupRects);
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 3)
            {
                continue;
            }

            (double X0, double Y0) = Stroke.Samples[0];
            (double X1, double Y1) = Stroke.Samples[1];
            (double X2, double Y2) = Stroke.Samples[2];
            double First = Distance(X0, Y0, X1, Y1);
            if (First > SOURCE_DOGLEG_PX || First < 1.0)
            {
                continue;
            }

            SvgNodeRect? Source = null;
            foreach (SvgNodeRect Box in Boxes)
            {
                if (string.Equals(Box.NodeId, Stroke.SourceId, StringComparison.Ordinal))
                {
                    Source = Box;
                    break;
                }
            }

            if (Source is null)
            {
                continue;
            }

            double LeftGap = Math.Abs(X0 - Source.Left);
            double RightGap = Math.Abs(X0 - Source.Right);
            double TopGap = Math.Abs(Y0 - Source.Top);
            double BottomGap = Math.Abs(Y0 - Source.Bottom);
            double Nearest = Math.Min(Math.Min(LeftGap, RightGap), Math.Min(TopGap, BottomGap));
            bool HorizontalFace = Nearest == TopGap || Nearest == BottomGap;
            bool FirstHorizontal = Math.Abs(Y1 - Y0) <= 1.5;
            bool FirstVertical = Math.Abs(X1 - X0) <= 1.5;
            bool SlidesAlongFace = (HorizontalFace && FirstHorizontal) || (!HorizontalFace && FirstVertical);
            bool LeavesFace = (HorizontalFace && FirstVertical) || (!HorizontalFace && FirstHorizontal);
            if (!SlidesAlongFace && !LeavesFace)
            {
                continue;
            }

            double TurnX = X2 - X1;
            double TurnY = Y2 - Y1;
            double FirstX = X1 - X0;
            double FirstY = Y1 - Y0;
            double TurnLength = Math.Sqrt((TurnX * TurnX) + (TurnY * TurnY));
            double FirstLength = Math.Sqrt((FirstX * FirstX) + (FirstY * FirstY));
            if (TurnLength < 1.0 || FirstLength < 1.0)
            {
                continue;
            }

            double Dot = ((FirstX * TurnX) + (FirstY * TurnY)) / (FirstLength * TurnLength);
            bool ContinuesAfterJog = false;
            if (Stroke.Samples.Count >= 4 && TurnLength <= SOURCE_DOGLEG_PX)
            {
                (double X3, double Y3) = Stroke.Samples[3];
                bool ThirdVertical = Math.Abs(X3 - X2) <= 1.5;
                bool ThirdHorizontal = Math.Abs(Y3 - Y2) <= 1.5;
                ContinuesAfterJog = (FirstVertical && ThirdVertical) || (FirstHorizontal && ThirdHorizontal);
            }

            if (SlidesAlongFace && Dot <= 0.35)
            {
                // A short slide along the source face before the route turns away.
            }
            else if (!ContinuesAfterJog)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "SourceDogleg",
                Stroke.SourceId + "->" + Stroke.TargetId
                + " steps "
                + First.ToString("0.0", CultureInfo.InvariantCulture)
                + " px along its source before turning",
                [Stroke.SourceId, Stroke.TargetId],
                null));
        }
    }

    private static void CollectDanglingStubs(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            for (int Index = 1; Index < Stroke.Samples.Count - 1; Index++)
            {
                (double PreviousX, double PreviousY) = Stroke.Samples[Index - 1];
                (double SpurX, double SpurY) = Stroke.Samples[Index];
                (double NextX, double NextY) = Stroke.Samples[Index + 1];
                double SegmentX = SpurX - PreviousX;
                double SegmentY = SpurY - PreviousY;
                double LengthSquared = (SegmentX * SegmentX) + (SegmentY * SegmentY);
                if (LengthSquared < 4.0)
                {
                    continue;
                }

                double Along = ((NextX - PreviousX) * SegmentX) + ((NextY - PreviousY) * SegmentY);
                if (Along < 1.0 || Along > LengthSquared - 1.0)
                {
                    continue;
                }

                double Cross = Math.Abs(((NextX - PreviousX) * SegmentY) - ((NextY - PreviousY) * SegmentX));
                if (Cross > Math.Sqrt(LengthSquared) * 1.25)
                {
                    continue;
                }

                double Spur = Math.Sqrt(Math.Pow(SpurX - NextX, 2.0) + Math.Pow(SpurY - NextY, 2.0));
                Violations.Add(new FlowchartLayoutQualityFailure(
                    "DanglingStub",
                    Stroke.SourceId + "->" + Stroke.TargetId
                    + " continues "
                    + Spur.ToString("0.0", CultureInfo.InvariantCulture)
                    + " px past a bend",
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
                break;
            }
        }
    }

    private static void CollectTitleClearance(
        string Svg,
        DiagramContract Contract,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (Match Match in TextRegex.Matches(Svg))
        {
            string Text = Regex.Replace(Match.Groups[1].Value, "<[^>]+>", " ").Trim();
            Text = Regex.Replace(Text, @"\s+", " ");
            if (Text.Length == 0)
            {
                continue;
            }

            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!Attributes.TryGetValue("text-anchor", out string? Anchor)
                || !string.Equals(Anchor, "middle", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryReadTextAnchor(Attributes, Match.Value, out double AnchorX, out double AnchorY, out double FontSize))
            {
                continue;
            }

            bool EdgeLabel = Attributes.ContainsKey("data-edge-label")
                || Match.Value.Contains("data-edge-source", StringComparison.Ordinal);
            if (EdgeLabel)
            {
                continue;
            }

            bool OnGroupTitle = false;
            foreach (GroupContract Group in Contract.Groups)
            {
                if (string.Equals(Group.Title, Text, StringComparison.Ordinal))
                {
                    OnGroupTitle = true;
                    break;
                }
            }

            if (!OnGroupTitle)
            {
                continue;
            }

            double Width = Math.Max(48.0, Text.Length * FontSize * 0.58);
            double Left = AnchorX - (Width / 2.0) - TITLE_CLEARANCE_PX;
            double Right = AnchorX + (Width / 2.0) + TITLE_CLEARANCE_PX;
            double Top = AnchorY - FontSize - TITLE_CLEARANCE_PX;
            double Bottom = AnchorY + (FontSize * 0.35) + TITLE_CLEARANCE_PX;
            foreach (ParsedEdgeStroke Stroke in Strokes)
            {
                if (!PolylineHitsRect(Stroke.Samples, Left, Top, Right, Bottom))
                {
                    continue;
                }

                Violations.Add(new FlowchartLayoutQualityFailure(
                    "TitleClearance",
                    Stroke.SourceId + "->" + Stroke.TargetId + " comes within "
                    + TITLE_CLEARANCE_PX.ToString("0", CultureInfo.InvariantCulture)
                    + " px of '" + Text + "'",
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
                break;
            }
        }
    }

    private static void CollectGroupBorderClearance(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            for (int Index = 1; Index < Stroke.Samples.Count; Index++)
            {
                (double X1, double Y1) = Stroke.Samples[Index - 1];
                (double X2, double Y2) = Stroke.Samples[Index];
                double Length = Distance(X1, Y1, X2, Y2);
                if (Length < BORDER_HUG_MIN_PX)
                {
                    continue;
                }

                bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5;
                bool Vertical = Math.Abs(X2 - X1) <= 1.5;
                if (!Horizontal && !Vertical)
                {
                    continue;
                }

                foreach (SvgNodeRect Box in GroupRects)
                {
                    if (string.Equals(Box.NodeId, Stroke.SourceId, StringComparison.Ordinal)
                        || string.Equals(Box.NodeId, Stroke.TargetId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    double Overlap;
                    double Separation;
                    if (Horizontal)
                    {
                        Overlap = AxisOverlap(X1, X2, Box.Left, Box.Right);
                        Separation = Math.Min(Math.Abs(Y1 - Box.Top), Math.Abs(Y1 - Box.Bottom));
                    }
                    else
                    {
                        Overlap = AxisOverlap(Y1, Y2, Box.Top, Box.Bottom);
                        Separation = Math.Min(Math.Abs(X1 - Box.Left), Math.Abs(X1 - Box.Right));
                    }

                    if (Overlap < BORDER_HUG_MIN_PX || Separation >= GROUP_BORDER_CLEAR_PX)
                    {
                        continue;
                    }

                    Violations.Add(new FlowchartLayoutQualityFailure(
                        "GroupBorderClearance",
                        Stroke.SourceId + "->" + Stroke.TargetId
                        + " runs "
                        + Separation.ToString("0.0", CultureInfo.InvariantCulture)
                        + " px from the border of "
                        + Box.NodeId,
                        [Stroke.SourceId, Stroke.TargetId, Box.NodeId],
                        null));
                    break;
                }
            }
        }
    }

    private static void CollectInteriorStrokes(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 3)
            {
                continue;
            }

            SvgNodeRect? Target = FindRect(NodeRects, Stroke.TargetId);
            if (Target == null)
            {
                continue;
            }

            for (int Index = 1; Index < Stroke.Samples.Count - 1; Index++)
            {
                (double X, double Y) = Stroke.Samples[Index];
                if (X <= Target.Left + 2.0 || X >= Target.Right - 2.0 || Y <= Target.Top + 2.0 || Y >= Target.Bottom - 2.0)
                {
                    continue;
                }

                Violations.Add(new FlowchartLayoutQualityFailure(
                    "InteriorStroke",
                    Stroke.SourceId + "->" + Stroke.TargetId + " travels inside " + Stroke.TargetId,
                    [Stroke.SourceId, Stroke.TargetId],
                    null));
                break;
            }
        }
    }

    private static void CollectNeedlessDoglegs(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<SvgNodeRect> NodeRects,
        IReadOnlyList<SvgNodeRect> GroupRects,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        List<SvgNodeRect> Boxes = new List<SvgNodeRect>(NodeRects.Count + GroupRects.Count);
        Boxes.AddRange(NodeRects);
        Boxes.AddRange(GroupRects);
        foreach (ParsedEdgeStroke Stroke in Strokes)
        {
            if (Stroke.Samples.Count < 4)
            {
                continue;
            }

            (double X0, double Y0) = Stroke.Samples[0];
            (double X1, double Y1) = Stroke.Samples[1];
            double First = Distance(X0, Y0, X1, Y1);
            if (First > DOGLEG_STUB_PX || First < 1.0)
            {
                continue;
            }

            int Bends = 0;
            for (int Index = 2; Index < Stroke.Samples.Count; Index++)
            {
                double InX = Stroke.Samples[Index - 1].X - Stroke.Samples[Index - 2].X;
                double InY = Stroke.Samples[Index - 1].Y - Stroke.Samples[Index - 2].Y;
                double OutX = Stroke.Samples[Index].X - Stroke.Samples[Index - 1].X;
                double OutY = Stroke.Samples[Index].Y - Stroke.Samples[Index - 1].Y;
                if (Math.Abs((InX * OutY) - (InY * OutX)) > 1.0)
                {
                    Bends++;
                }
            }

            if (Bends < 2)
            {
                continue;
            }

            (double EndX, double EndY) = Stroke.Samples[Stroke.Samples.Count - 1];
            List<(double X, double Y)> ElbowA = [(X0, Y0), (EndX, Y0), (EndX, EndY)];
            List<(double X, double Y)> ElbowB = [(X0, Y0), (X0, EndY), (EndX, EndY)];
            bool ElbowAClear = OrthogonalClears(ElbowA, Boxes, Stroke.SourceId, Stroke.TargetId);
            bool ElbowBClear = OrthogonalClears(ElbowB, Boxes, Stroke.SourceId, Stroke.TargetId);
            if (!ElbowAClear && !ElbowBClear)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "NeedlessDogleg",
                Stroke.SourceId + "->" + Stroke.TargetId
                + " jogs "
                + First.ToString("0.0", CultureInfo.InvariantCulture)
                + " px where a single bend is clear",
                [Stroke.SourceId, Stroke.TargetId],
                null));
        }
    }

    private static bool OrthogonalClears(
        IReadOnlyList<(double X, double Y)> Points,
        IReadOnlyList<SvgNodeRect> Boxes,
        string SourceId,
        string TargetId)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            (double X1, double Y1) = Points[Index - 1];
            (double X2, double Y2) = Points[Index];
            foreach (SvgNodeRect Box in Boxes)
            {
                bool Endpoint = string.Equals(Box.NodeId, SourceId, StringComparison.Ordinal)
                    || string.Equals(Box.NodeId, TargetId, StringComparison.Ordinal);
                if (!Endpoint && SegmentHitsRect(X1, Y1, X2, Y2, Box, 0.0))
                {
                    return false;
                }

                bool Terminal = (Index == 1 && string.Equals(Box.NodeId, SourceId, StringComparison.Ordinal))
                    || (Index == Points.Count - 1 && string.Equals(Box.NodeId, TargetId, StringComparison.Ordinal));
                double MinimumOverlap = Terminal ? 8.0 : BORDER_HUG_MIN_PX;
                bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) >= MinimumOverlap;
                bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) >= MinimumOverlap;
                if (Horizontal && AxisOverlap(X1, X2, Box.Left, Box.Right) >= MinimumOverlap
                    && (Math.Abs(Y1 - Box.Top) <= BORDER_HUG_PX || Math.Abs(Y1 - Box.Bottom) <= BORDER_HUG_PX))
                {
                    return false;
                }

                if (Vertical && AxisOverlap(Y1, Y2, Box.Top, Box.Bottom) >= MinimumOverlap
                    && (Math.Abs(X1 - Box.Left) <= BORDER_HUG_PX || Math.Abs(X1 - Box.Right) <= BORDER_HUG_PX))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void CollectLabelOnStroke(
        string Svg,
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<ParsedLabel> Labels,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        List<(double Left, double Top, double Right, double Bottom)> Gaps = [];
        foreach (Match RectMatch in Regex.Matches(Svg, "<rect\\b[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            Dictionary<string, string> Attributes = ParseAttributes(RectMatch.Value);
            if (!Attributes.TryGetValue("fill", out string? Fill)
                || string.Equals(Fill, "none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Attributes.TryGetValue("x", out string? XText)
                || !Attributes.TryGetValue("y", out string? YText)
                || !Attributes.TryGetValue("width", out string? WidthText)
                || !Attributes.TryGetValue("height", out string? HeightText)
                || !TryParseLeadingDouble(XText, out double X)
                || !TryParseLeadingDouble(YText, out double Y)
                || !TryParseLeadingDouble(WidthText, out double Width)
                || !TryParseLeadingDouble(HeightText, out double Height)
                || Width > 240.0
                || Height > 40.0)
            {
                continue;
            }

            Gaps.Add((X, Y, X + Width, Y + Height));
        }

        foreach (ParsedLabel Label in Labels)
        {
            ParsedEdgeStroke? Own = FindOwnStroke(Strokes, Label);
            if (Own is null)
            {
                continue;
            }

            double Width = Math.Max(12.0, Label.Text.Length * 16.0 * 0.55);
            double Left = Label.X - (Width / 2.0);
            double Right = Label.X + (Width / 2.0);
            double Top = Label.Y - 14.0;
            double Bottom = Label.Y + 4.0;
            if (!PolylineHitsRect(Own.Samples, Left, Top, Right, Bottom))
            {
                continue;
            }

            bool Covered = false;
            foreach ((double GapLeft, double GapTop, double GapRight, double GapBottom) in Gaps)
            {
                if (Label.X >= GapLeft && Label.X <= GapRight && Label.Y >= GapTop && Label.Y <= GapBottom)
                {
                    Covered = true;
                    break;
                }
            }

            if (Covered)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "LabelOnStroke",
                "label '" + Label.Text + "' sits on " + Own.SourceId + "->" + Own.TargetId,
                [Own.SourceId, Own.TargetId],
                null));
        }
    }

    private static void CollectLabelsOffLine(
        IReadOnlyList<ParsedEdgeStroke> Strokes,
        IReadOnlyList<ParsedLabel> Labels,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (ParsedLabel Label in Labels)
        {
            ParsedEdgeStroke? Own = FindOwnStroke(Strokes, Label);
            if (Own is null || !StrokeIsOrthogonal(Own.Samples))
            {
                continue;
            }

            double OwnDistance = DistanceToPolyline(Label.X, Label.Y, Own.Samples);
            if (OwnDistance <= LABEL_OFF_LINE_PX || OwnDistance > 24.0)
            {
                continue;
            }

            Violations.Add(new FlowchartLayoutQualityFailure(
                "LabelOffLine",
                "label '"
                + Label.Text
                + "' sits "
                + OwnDistance.ToString("0.0", CultureInfo.InvariantCulture)
                + " px off "
                + Own.SourceId
                + "->"
                + Own.TargetId,
                [Own.SourceId, Own.TargetId],
                null));
        }
    }

    private static bool StrokeIsOrthogonal(IReadOnlyList<(double X, double Y)> Samples)
    {
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double DeltaX = Math.Abs(Samples[Index].X - Samples[Index - 1].X);
            double DeltaY = Math.Abs(Samples[Index].Y - Samples[Index - 1].Y);
            if (DeltaX > 1.5 && DeltaY > 1.5)
            {
                return false;
            }
        }

        return Samples.Count >= 2;
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
