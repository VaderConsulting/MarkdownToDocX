using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Detects generic layout-space routing defects to explain visual or SVG comparison failures.
/// </summary>
public static class FlowchartEdgeRouteDiagnosticAnalyzer
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double DIAGONAL_CROSS_AXIS_MIN_PX = 8.0;

    private const double DIAGONAL_FLOW_AXIS_MIN_PX = 24.0;

    private const double NODE_PADDING_PX = 12.0;

    private const double MERGE_PORT_MIN_SEPARATION_PX = 14.0;

    private const double LABEL_EDGE_MIN_CLEARANCE_PX = 4.0;

    private const double LABEL_NODE_MIN_CLEARANCE_PX = 6.0;

    private const double LABEL_NEAREST_EDGE_RATIO = 0.55;

    private const double CENTRE_SPINE_DETOUR_RATIO = 1.18;

    private const double HOOK_DOT_PRODUCT_THRESHOLD = -0.15;

    private const int HOOK_ENDPOINT_SEGMENT_WINDOW = 3;

    /// <summary>
    /// Resolved route metadata used while analysing one edge.
    /// </summary>
    private sealed class FlowchartResolvedEdgeRoute
    {
        public EdgeContract Edge { get; init; } = null!;

        public List<(double X, double Y)> LayoutPoints { get; init; } = null!;

        public FlowchartEdgeRouteKind RouteKind { get; init; }

        public FlowchartEdgeSemanticRouteClass SemanticRouteClass { get; init; }

        public FlowchartLayoutNodeBounds SourceBounds { get; init; }

        public FlowchartLayoutNodeBounds TargetBounds { get; init; }
    }

    /// <summary>
    /// Analyses all visible edges in a laid-out diagram and returns explainable routing defects.
    /// </summary>
    public static List<FlowchartEdgeRouteDiagnosticFinding> Analyze(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction)
    {
        List<FlowchartEdgeRouteDiagnosticFinding> Findings = [];
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            RenderOptions,
            out FlowchartSpineLayoutContext? SpineContext);
        FlowchartSpineCorridorBounds? SpineCorridor = null;
        if (SpineContext is not null)
        {
            if (FlowchartSpineCorridorHelper.TryBuildCorridor(BoundsById, SpineContext, out FlowchartSpineCorridorBounds BuiltCorridor))
            {
                SpineCorridor = BuiltCorridor;
            }
        }

        List<FlowchartResolvedEdgeRoute> ResolvedRoutes = [];
        IReadOnlySet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            List<(double X, double Y)> LayoutPoints;
            FlowchartEdgeRouteKind RouteKind;
            if (!FlowchartEdgeRouteResolver.TryResolveGridLayoutPoints(
                    Layout,
                    Contract,
                    NodeById,
                    Edge,
                    out LayoutPoints,
                    out RouteKind,
                    BackEdges,
                    BoundsById)
                && !FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out LayoutPoints,
                    out RouteKind,
                    Layout))
            {
                continue;
            }

            FlowchartEdgeSemanticRouteClass SemanticRouteClass = FlowchartEdgeRouteClassifier.Classify(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                SpineContext);

            FlowchartResolvedEdgeRoute ResolvedRoute = new FlowchartResolvedEdgeRoute
            {
                Edge = Edge,
                LayoutPoints = LayoutPoints,
                RouteKind = RouteKind,
                SemanticRouteClass = SemanticRouteClass,
                SourceBounds = SourceBounds,
                TargetBounds = TargetBounds
            };
            ResolvedRoutes.Add(ResolvedRoute);

            AppendEdgeFindings(
                Findings,
                ResolvedRoute,
                BoundsById,
                Direction,
                SpineCorridor);
        }

        AppendMergeAmbiguityFindings(Findings, ResolvedRoutes);
        AppendLabelAssociationFindings(Findings, ResolvedRoutes, BoundsById, NodeById, Direction, Contract);

        return Findings;
    }

    /// <summary>
    /// Analyses one resolved edge route against generic routing rules.
    /// Intended for regression tests that inject synthetic geometry.
    /// </summary>
    public static List<FlowchartEdgeRouteDiagnosticFinding> AnalyzeEdgeRoute(
        EdgeContract Edge,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartEdgeSemanticRouteClass SemanticRouteClass,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction)
    {
        List<FlowchartEdgeRouteDiagnosticFinding> Findings = [];
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return Findings;
        }

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            RenderOptions,
            out FlowchartSpineLayoutContext? SpineContext);
        FlowchartSpineCorridorBounds? SpineCorridor = null;
        if (SpineContext is not null
            && FlowchartSpineCorridorHelper.TryBuildCorridor(BoundsById, SpineContext, out FlowchartSpineCorridorBounds BuiltCorridor))
        {
            SpineCorridor = BuiltCorridor;
        }

        FlowchartResolvedEdgeRoute ResolvedRoute = new FlowchartResolvedEdgeRoute
        {
            Edge = Edge,
            LayoutPoints = LayoutPoints.ToList(),
            RouteKind = RouteKind,
            SemanticRouteClass = SemanticRouteClass,
            SourceBounds = SourceBounds,
            TargetBounds = TargetBounds
        };

        AppendEdgeFindings(Findings, ResolvedRoute, BoundsById, Direction, SpineCorridor);
        return Findings;
    }

    private static void AppendEdgeFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        FlowchartResolvedEdgeRoute ResolvedRoute,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        FlowchartSpineCorridorBounds? SpineCorridor)
    {
        AppendLongDiagonalCrossingFindings(Findings, ResolvedRoute, BoundsById, Direction, SpineCorridor);
        AppendUnnecessaryHardElbowFindings(Findings, ResolvedRoute);
        AppendCentreSpineRegressionFindings(Findings, ResolvedRoute, BoundsById, Direction);
        AppendHookOrOvershootFindings(Findings, ResolvedRoute);
        AppendNodePassThroughFindings(Findings, ResolvedRoute, BoundsById);
    }

    private static void AppendLongDiagonalCrossingFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        FlowchartResolvedEdgeRoute ResolvedRoute,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        FlowchartSpineCorridorBounds? SpineCorridor)
    {
        List<(double X, double Y)> Points = ResolvedRoute.LayoutPoints;
        if (Points.Count < 2)
        {
            return;
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;

        for (int SegmentIndex = 0; SegmentIndex < Points.Count - 1; SegmentIndex++)
        {
            (double StartX, double StartY) = Points[SegmentIndex];
            (double EndX, double EndY) = Points[SegmentIndex + 1];
            double DeltaX = Math.Abs(EndX - StartX);
            double DeltaY = Math.Abs(EndY - StartY);
            if (DeltaX <= COLLINEAR_EPSILON_PX || DeltaY <= COLLINEAR_EPSILON_PX)
            {
                continue;
            }

            if (DeltaX < DIAGONAL_CROSS_AXIS_MIN_PX || DeltaY < DIAGONAL_FLOW_AXIS_MIN_PX)
            {
                continue;
            }

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
            {
                FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
                if (string.Equals(CandidateBounds.NodeId, ResolvedRoute.Edge.SourceId, StringComparison.Ordinal)
                    || string.Equals(CandidateBounds.NodeId, ResolvedRoute.Edge.TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsNodeBetweenEndpoints(
                        ResolvedRoute.SourceBounds,
                        ResolvedRoute.TargetBounds,
                        CandidateBounds,
                        VerticalFlow))
                {
                    continue;
                }

                if (!SegmentIntersectsPaddedNodeInterior(
                        StartX,
                        StartY,
                        EndX,
                        EndY,
                        CandidateBounds,
                        NODE_PADDING_PX))
                {
                    continue;
                }

                Findings.Add(CreateFinding(
                    ResolvedRoute,
                    FlowchartEdgeRouteDiagnosticKind.LongDiagonalCrossing,
                    "Diagonal segment crosses padded bounds of unrelated node "
                    + CandidateBounds.NodeId
                    + " while spanning multiple ranks; exception routes must use exterior corridor lanes."));
                return;
            }

            if (ResolvedRoute.SemanticRouteClass == FlowchartEdgeSemanticRouteClass.LongExceptionRoute)
            {
                bool CutsBuiltSpineCorridor = SpineCorridor is not null
                    && FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
                        StartX,
                        StartY,
                        EndX,
                        EndY,
                        SpineCorridor.Left,
                        SpineCorridor.Top,
                        SpineCorridor.Right,
                        SpineCorridor.Bottom);
                bool CutsSpineColumn = VerticalFlow
                    && SegmentIntersectsSpineColumn(
                        StartX,
                        StartY,
                        EndX,
                        EndY,
                        ResolvedRoute.SourceBounds,
                        ResolvedRoute.TargetBounds);
                if (CutsBuiltSpineCorridor || CutsSpineColumn)
                {
                    Findings.Add(CreateFinding(
                        ResolvedRoute,
                        FlowchartEdgeRouteDiagnosticKind.LongDiagonalCrossing,
                        "Long exception route uses a diagonal segment that cuts through the spine corridor instead of an exterior bypass lane."));
                    return;
                }
            }
        }
    }

    private static void AppendUnnecessaryHardElbowFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        FlowchartResolvedEdgeRoute ResolvedRoute)
    {
        if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(ResolvedRoute.LayoutPoints))
        {
            return;
        }

        if (ResolvedRoute.LayoutPoints.Count < 4)
        {
            return;
        }

        if (ResolvedRoute.RouteKind == FlowchartEdgeRouteKind.Corridor
            || ResolvedRoute.RouteKind == FlowchartEdgeRouteKind.CrossGroup)
        {
            return;
        }

        if (ResolvedRoute.SemanticRouteClass == FlowchartEdgeSemanticRouteClass.SkipBypass
            || ResolvedRoute.SemanticRouteClass == FlowchartEdgeSemanticRouteClass.LongExceptionRoute)
        {
            return;
        }

        if (ResolvedRoute.SemanticRouteClass == FlowchartEdgeSemanticRouteClass.CentreSpine
            || (ResolvedRoute.SemanticRouteClass == FlowchartEdgeSemanticRouteClass.LocalDecisionBranch
                && ResolvedRoute.RouteKind != FlowchartEdgeRouteKind.Straight))
        {
            Findings.Add(CreateFinding(
                ResolvedRoute,
                FlowchartEdgeRouteDiagnosticKind.UnnecessaryHardElbow,
                "Route introduces hard orthogonal elbows even though the semantic class expects straight spine attachment or a smooth local branch rather than full orthogonal routing."));
        }
    }

    private static void AppendCentreSpineRegressionFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        FlowchartResolvedEdgeRoute ResolvedRoute,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return;
        }

        if (!FlowchartShapeBoundaryAttachHelper.AreSpineColumnAligned(
                ResolvedRoute.SourceBounds,
                ResolvedRoute.TargetBounds)
            || !FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(
                ResolvedRoute.SourceBounds,
                ResolvedRoute.TargetBounds))
        {
            return;
        }

        if (HasInterveningNodeBetween(
                BoundsById,
                ResolvedRoute.SourceBounds,
                ResolvedRoute.TargetBounds))
        {
            return;
        }

        List<(double X, double Y)> Points = ResolvedRoute.LayoutPoints;
        if (Points.Count < 2)
        {
            return;
        }

        (double SourcePortX, double SourcePortY) = Points[0];
        (double TargetPortX, double TargetPortY) = Points[Points.Count - 1];
        bool StraightAttachValid = FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
            SourcePortX,
            SourcePortY,
            TargetPortX,
            TargetPortY);
        if (!StraightAttachValid)
        {
            return;
        }

        double DirectDistance = Math.Sqrt(
            ((TargetPortX - SourcePortX) * (TargetPortX - SourcePortX))
            + ((TargetPortY - SourcePortY) * (TargetPortY - SourcePortY)));
        double RoutedDistance = ComputePolylineLength(Points);
        bool UnnecessarilyComplex = !ArePointsOnChord(Points)
            || RoutedDistance > (DirectDistance * CENTRE_SPINE_DETOUR_RATIO);

        if (!UnnecessarilyComplex)
        {
            return;
        }

        Findings.Add(CreateFinding(
            ResolvedRoute,
            FlowchartEdgeRouteDiagnosticKind.CentreSpineRegression,
            "Spine-aligned unobstructed attach points are rendered with curves, kinks, or detours instead of a straight centre-spine connector."));
    }

    /// <summary>
    /// Returns true when every point lies on the straight line from the first point to the last.
    /// A straight connector keeps its terminal stub points (spec §15.0A), so extra collinear points are not a detour.
    /// </summary>
    private static bool ArePointsOnChord(List<(double X, double Y)> Points)
    {
        (double StartX, double StartY) = Points[0];
        (double EndX, double EndY) = Points[Points.Count - 1];
        double ChordX = EndX - StartX;
        double ChordY = EndY - StartY;
        double ChordLength = Math.Sqrt((ChordX * ChordX) + (ChordY * ChordY));
        if (ChordLength < 1e-6)
        {
            return Points.Count <= 2;
        }

        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            double OffsetX = Points[Index].X - StartX;
            double OffsetY = Points[Index].Y - StartY;
            double Distance = Math.Abs((OffsetX * ChordY) - (OffsetY * ChordX)) / ChordLength;
            double Along = ((OffsetX * ChordX) + (OffsetY * ChordY)) / ChordLength;
            if (Distance > COLLINEAR_EPSILON_PX || Along < -COLLINEAR_EPSILON_PX || Along > ChordLength + COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

    private static void AppendHookOrOvershootFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        FlowchartResolvedEdgeRoute ResolvedRoute)
    {
        List<(double X, double Y)> Points = ResolvedRoute.LayoutPoints;
        if (Points.Count < 3)
        {
            return;
        }

        if (HasEndpointDirectionReversal(Points, NearSource: true))
        {
            Findings.Add(CreateFinding(
                ResolvedRoute,
                FlowchartEdgeRouteDiagnosticKind.HookOrOvershoot,
                "Route reverses direction near the source attach, producing a local hook or overshoot."));
            return;
        }

        if (HasEndpointDirectionReversal(Points, NearSource: false))
        {
            Findings.Add(CreateFinding(
                ResolvedRoute,
                FlowchartEdgeRouteDiagnosticKind.HookOrOvershoot,
                "Route reverses direction near the target attach, producing a merge-back loop or port overshoot."));
        }
    }

    private static void AppendNodePassThroughFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        FlowchartResolvedEdgeRoute ResolvedRoute,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        List<(double X, double Y)> Points = ResolvedRoute.LayoutPoints;
        if (Points.Count < 2)
        {
            return;
        }

        for (int SegmentIndex = 0; SegmentIndex < Points.Count - 1; SegmentIndex++)
        {
            (double StartX, double StartY) = Points[SegmentIndex];
            (double EndX, double EndY) = Points[SegmentIndex + 1];

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
            {
                FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
                if (SegmentIsAllowedEndpointStub(
                        SegmentIndex,
                        Points.Count,
                        CandidateBounds,
                        ResolvedRoute.Edge.SourceId,
                        ResolvedRoute.Edge.TargetId,
                        StartX,
                        StartY,
                        EndX,
                        EndY))
                {
                    continue;
                }

                if (!SegmentCrossesNodeInteriorFromOutside(
                        StartX,
                        StartY,
                        EndX,
                        EndY,
                        CandidateBounds))
                {
                    continue;
                }

                Findings.Add(CreateFinding(
                    ResolvedRoute,
                    FlowchartEdgeRouteDiagnosticKind.NodePassThrough,
                    "Connector segment passes through the interior of node "
                    + CandidateBounds.NodeId
                    + " instead of terminating at source and target boundaries."));
                return;
            }
        }
    }

    private static void AppendMergeAmbiguityFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        List<FlowchartResolvedEdgeRoute> ResolvedRoutes)
    {
        IEnumerable<IGrouping<string, FlowchartResolvedEdgeRoute>> RoutesByTarget = ResolvedRoutes
            .GroupBy(Route => Route.Edge.TargetId, StringComparer.Ordinal);

        foreach (IGrouping<string, FlowchartResolvedEdgeRoute> TargetGroup in RoutesByTarget)
        {
            if (TargetGroup.Count() < 2)
            {
                continue;
            }

            List<(FlowchartResolvedEdgeRoute Route, double TargetX, double TargetY)> TargetPorts =
                [];
            foreach (FlowchartResolvedEdgeRoute Route in TargetGroup)
            {
                (double TargetX, double TargetY) = Route.LayoutPoints[Route.LayoutPoints.Count - 1];
                TargetPorts.Add((Route, TargetX, TargetY));
            }

            for (int LeftIndex = 0; LeftIndex < TargetPorts.Count - 1; LeftIndex++)
            {
                for (int RightIndex = LeftIndex + 1; RightIndex < TargetPorts.Count; RightIndex++)
                {
                    (FlowchartResolvedEdgeRoute LeftRoute, double LeftTargetX, double LeftTargetY) = TargetPorts[LeftIndex];
                    (FlowchartResolvedEdgeRoute RightRoute, double RightTargetX, double RightTargetY) = TargetPorts[RightIndex];
                    double CrossAxisDelta = Math.Abs(LeftTargetX - RightTargetX);
                    if (CrossAxisDelta >= MERGE_PORT_MIN_SEPARATION_PX)
                    {
                        continue;
                    }

                    Findings.Add(CreateFinding(
                        LeftRoute,
                        FlowchartEdgeRouteDiagnosticKind.MergeAmbiguity,
                        "Incoming routes to the same target collapse to nearby target ports ("
                        + FormatPort(LeftTargetX, LeftTargetY)
                        + " vs "
                        + FormatPort(RightTargetX, RightTargetY)
                        + ") and are visually ambiguous at merge."));
                    break;
                }
            }
        }
    }

    private static void AppendLabelAssociationFindings(
        List<FlowchartEdgeRouteDiagnosticFinding> Findings,
        List<FlowchartResolvedEdgeRoute> ResolvedRoutes,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        DiagramContract Contract)
    {
        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);

        foreach (FlowchartResolvedEdgeRoute ResolvedRoute in ResolvedRoutes)
        {
            if (string.IsNullOrWhiteSpace(ResolvedRoute.Edge.Label))
            {
                continue;
            }

            if (FlowchartEdgeLabelPlacementHelper.HasExteriorCorridorDetourForValidation(
                    ResolvedRoute.LayoutPoints,
                    Direction))
            {
                continue;
            }

            (double LabelX, double LabelY) = ResolveLayoutLabelPosition(
                ResolvedRoute,
                Direction,
                NodeById,
                Contract,
                BoundsById,
                RenderOptions);
            double OwnEdgeDistance = ComputePointToPolylineDistance(LabelX, LabelY, ResolvedRoute.LayoutPoints);

            NodeById.TryGetValue(ResolvedRoute.Edge.SourceId, out NodeContract? SourceNode);
            bool SourceIsDecision = IsDecisionShape(SourceNode?.Shape);

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
            {
                if (!string.Equals(Entry.Key, ResolvedRoute.Edge.SourceId, StringComparison.Ordinal)
                    && !string.Equals(Entry.Key, ResolvedRoute.Edge.TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (SourceIsDecision
                    && string.Equals(Entry.Key, ResolvedRoute.Edge.SourceId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (PointInsidePaddedBounds(LabelX, LabelY, Entry.Value, LABEL_NODE_MIN_CLEARANCE_PX))
                {
                    Findings.Add(CreateFinding(
                        ResolvedRoute,
                        FlowchartEdgeRouteDiagnosticKind.LabelAssociationFailure,
                        "Label overlaps the padded boundary of node " + Entry.Key + "."));
                    break;
                }
            }

            FlowchartResolvedEdgeRoute? NearestRoute = null;
            double NearestDistance = double.MaxValue;
            foreach (FlowchartResolvedEdgeRoute CandidateRoute in ResolvedRoutes)
            {
                double CandidateDistance = ComputePointToPolylineDistance(
                    LabelX,
                    LabelY,
                    CandidateRoute.LayoutPoints);
                if (CandidateDistance < NearestDistance)
                {
                    NearestDistance = CandidateDistance;
                    NearestRoute = CandidateRoute;
                }
            }

            if (NearestRoute is not null
                && !ReferenceEquals(NearestRoute, ResolvedRoute)
                && NearestDistance < (OwnEdgeDistance * LABEL_NEAREST_EDGE_RATIO))
            {
                Findings.Add(CreateFinding(
                    ResolvedRoute,
                    FlowchartEdgeRouteDiagnosticKind.LabelAssociationFailure,
                    "Label is closer to edge "
                    + BuildEdgeKey(NearestRoute.Edge)
                    + " than to its labelled edge "
                    + BuildEdgeKey(ResolvedRoute.Edge)
                    + ", so the decision text appears associated with the wrong route."));
            }
        }
    }

    private static (double X, double Y) ResolveLayoutLabelPosition(
        FlowchartResolvedEdgeRoute ResolvedRoute,
        FlowchartDirection Direction,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartRenderOptions RenderOptions)
    {
        _ = RenderOptions;
        NodeById.TryGetValue(ResolvedRoute.Edge.SourceId, out NodeContract? SourceNode);
        string? SourceShape = SourceNode?.Shape;
        string? RouteClass = null;
        if (ResolvedRoute.SemanticRouteClass == FlowchartEdgeSemanticRouteClass.LocalDecisionBranch)
        {
            RouteClass = "LocalDecisionBranch";
        }
        else if (ResolvedRoute.SemanticRouteClass == FlowchartEdgeSemanticRouteClass.CentreSpine)
        {
            RouteClass = "CentreSpine";
        }

        return FlowchartEdgeLabelPlacementHelper.ResolveEdgeLabelPosition(
            ResolvedRoute.SourceBounds,
            ResolvedRoute.TargetBounds,
            ResolvedRoute.LayoutPoints,
            Direction,
            SourceShape,
            ResolvedRoute.Edge.Label,
            ResolvedRoute.Edge,
            Contract,
            BoundsById,
            RouteClass);
    }

    private static FlowchartEdgeRouteDiagnosticFinding CreateFinding(
        FlowchartResolvedEdgeRoute ResolvedRoute,
        FlowchartEdgeRouteDiagnosticKind Kind,
        string Reason)
    {
        List<(double X, double Y)> Points = ResolvedRoute.LayoutPoints;
        (double SourcePortX, double SourcePortY) = Points[0];
        (double TargetPortX, double TargetPortY) = Points[Points.Count - 1];
        return new FlowchartEdgeRouteDiagnosticFinding(
            Kind,
            BuildEdgeKey(ResolvedRoute.Edge),
            ResolvedRoute.Edge.SourceId,
            ResolvedRoute.Edge.TargetId,
            ResolvedRoute.SemanticRouteClass,
            ResolvedRoute.RouteKind,
            SourcePortX,
            SourcePortY,
            TargetPortX,
            TargetPortY,
            Reason);
    }

    private static string BuildEdgeKey(EdgeContract Edge)
    {
        string LabelSuffix = string.IsNullOrWhiteSpace(Edge.Label) ? string.Empty : "|" + Edge.Label;
        return Edge.SourceId + "->" + Edge.TargetId + LabelSuffix;
    }

    private static string FormatPort(double PortX, double PortY) => "(" + PortX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            + "," + PortY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ")";

    private static bool IsNodeBetweenEndpoints(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartLayoutNodeBounds CandidateBounds,
        bool VerticalFlow)
    {
        if (VerticalFlow)
        {
            double UpperY = Math.Max(SourceBounds.CenterY, TargetBounds.CenterY);
            double LowerY = Math.Min(SourceBounds.CenterY, TargetBounds.CenterY);
            return CandidateBounds.CenterY < UpperY - COLLINEAR_EPSILON_PX
                && CandidateBounds.CenterY > LowerY + COLLINEAR_EPSILON_PX;
        }

        double RightX = Math.Max(SourceBounds.CenterX, TargetBounds.CenterX);
        double LeftX = Math.Min(SourceBounds.CenterX, TargetBounds.CenterX);
        return CandidateBounds.CenterX > LeftX + COLLINEAR_EPSILON_PX
            && CandidateBounds.CenterX < RightX - COLLINEAR_EPSILON_PX;
    }

    private static bool SegmentIntersectsPaddedNodeInterior(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds NodeBounds,
        double PaddingPx)
    {
        double Left = NodeBounds.CenterX - (NodeBounds.Width / 2.0) - PaddingPx;
        double Right = NodeBounds.CenterX + (NodeBounds.Width / 2.0) + PaddingPx;
        double Top = NodeBounds.CenterY + (NodeBounds.Height / 2.0) + PaddingPx;
        double Bottom = NodeBounds.CenterY - (NodeBounds.Height / 2.0) - PaddingPx;
        return FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
            StartX,
            StartY,
            EndX,
            EndY,
            Left,
            Top,
            Right,
            Bottom);
    }

    private static bool HasInterveningNodeBetween(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceBounds.NodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetBounds.NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (CandidateBounds.CenterY >= SourceBounds.CenterY - COLLINEAR_EPSILON_PX)
            {
                continue;
            }

            if (CandidateBounds.CenterY <= TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                continue;
            }

            if (Math.Abs(CandidateBounds.CenterX - SourceBounds.CenterX) <= 30.0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEndpointDirectionReversal(
        IReadOnlyList<(double X, double Y)> Points,
        bool NearSource)
    {
        int SegmentCount = Points.Count - 1;
        if (SegmentCount < 2)
        {
            return false;
        }

        int StartSegmentIndex = NearSource ? 0 : Math.Max(0, SegmentCount - HOOK_ENDPOINT_SEGMENT_WINDOW);
        int EndSegmentIndex = NearSource
            ? Math.Min(SegmentCount - 1, HOOK_ENDPOINT_SEGMENT_WINDOW - 1)
            : SegmentCount - 1;

        for (int SegmentIndex = StartSegmentIndex; SegmentIndex < EndSegmentIndex; SegmentIndex++)
        {
            (double Ax, double Ay) = NormalizeVector(
                Points[SegmentIndex + 1].X - Points[SegmentIndex].X,
                Points[SegmentIndex + 1].Y - Points[SegmentIndex].Y);
            (double Bx, double By) = NormalizeVector(
                Points[SegmentIndex + 2].X - Points[SegmentIndex + 1].X,
                Points[SegmentIndex + 2].Y - Points[SegmentIndex + 1].Y);
            double DotProduct = (Ax * Bx) + (Ay * By);
            if (DotProduct < HOOK_DOT_PRODUCT_THRESHOLD)
            {
                return true;
            }
        }

        return false;
    }

    private static (double X, double Y) NormalizeVector(double VectorX, double VectorY)
    {
        double Length = Math.Sqrt((VectorX * VectorX) + (VectorY * VectorY));
        if (Length <= 1e-6)
        {
            return (0.0, 0.0);
        }

        return (VectorX / Length, VectorY / Length);
    }

    private static double ComputePolylineLength(IReadOnlyList<(double X, double Y)> Points)
    {
        double TotalLength = 0.0;
        for (int PointIndex = 1; PointIndex < Points.Count; PointIndex++)
        {
            double DeltaX = Points[PointIndex].X - Points[PointIndex - 1].X;
            double DeltaY = Points[PointIndex].Y - Points[PointIndex - 1].Y;
            TotalLength += Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        return TotalLength;
    }

    private static double ComputePointToPolylineDistance(
        double PointX,
        double PointY,
        IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count == 0)
        {
            return double.MaxValue;
        }

        if (Points.Count == 1)
        {
            return Math.Sqrt(
                ((PointX - Points[0].X) * (PointX - Points[0].X))
                + ((PointY - Points[0].Y) * (PointY - Points[0].Y)));
        }

        double MinimumDistance = double.MaxValue;
        for (int SegmentIndex = 0; SegmentIndex < Points.Count - 1; SegmentIndex++)
        {
            double SegmentDistance = ComputePointToSegmentDistance(
                PointX,
                PointY,
                Points[SegmentIndex].X,
                Points[SegmentIndex].Y,
                Points[SegmentIndex + 1].X,
                Points[SegmentIndex + 1].Y);
            MinimumDistance = Math.Min(MinimumDistance, SegmentDistance);
        }

        return MinimumDistance;
    }

    private static double ComputePointToSegmentDistance(
        double PointX,
        double PointY,
        double SegmentStartX,
        double SegmentStartY,
        double SegmentEndX,
        double SegmentEndY)
    {
        double SegmentX = SegmentEndX - SegmentStartX;
        double SegmentY = SegmentEndY - SegmentStartY;
        double SegmentLengthSquared = (SegmentX * SegmentX) + (SegmentY * SegmentY);
        if (SegmentLengthSquared <= 1e-9)
        {
            double DeltaX = PointX - SegmentStartX;
            double DeltaY = PointY - SegmentStartY;
            return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        double Projection = (((PointX - SegmentStartX) * SegmentX) + ((PointY - SegmentStartY) * SegmentY))
            / SegmentLengthSquared;
        double ClampedProjection = Math.Max(0.0, Math.Min(1.0, Projection));
        double ClosestX = SegmentStartX + (SegmentX * ClampedProjection);
        double ClosestY = SegmentStartY + (SegmentY * ClampedProjection);
        double ClosestDeltaX = PointX - ClosestX;
        double ClosestDeltaY = PointY - ClosestY;
        return Math.Sqrt((ClosestDeltaX * ClosestDeltaX) + (ClosestDeltaY * ClosestDeltaY));
    }

    private static bool SegmentCrossesNodeInteriorFromOutside(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds NodeBounds)
    {
        if (PointInsideNodeBounds(StartX, StartY, NodeBounds, InsetPx: 2.0)
            || PointInsideNodeBounds(EndX, EndY, NodeBounds, InsetPx: 2.0))
        {
            return false;
        }

        return FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
            StartX,
            StartY,
            EndX,
            EndY,
            NodeBounds,
            InsetPx: 2.0);
    }

    private static bool SegmentIsAllowedEndpointStub(
        int SegmentIndex,
        int PointCount,
        FlowchartLayoutNodeBounds CandidateBounds,
        string SourceNodeId,
        string TargetNodeId,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        if (SegmentIndex == 0
            && string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal))
        {
            return true;
        }

        if (SegmentIndex == PointCount - 2
            && string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool PointInsideNodeBounds(
        double PointX,
        double PointY,
        FlowchartLayoutNodeBounds NodeBounds,
        double InsetPx)
    {
        double Left = NodeBounds.CenterX - (NodeBounds.Width / 2.0) + InsetPx;
        double Right = NodeBounds.CenterX + (NodeBounds.Width / 2.0) - InsetPx;
        double Top = NodeBounds.CenterY + (NodeBounds.Height / 2.0) - InsetPx;
        double Bottom = NodeBounds.CenterY - (NodeBounds.Height / 2.0) + InsetPx;
        return PointX >= Left
            && PointX <= Right
            && PointY >= Bottom
            && PointY <= Top;
    }

    private static bool PointInsidePaddedBounds(
        double PointX,
        double PointY,
        FlowchartLayoutNodeBounds NodeBounds,
        double PaddingPx)
    {
        double Left = NodeBounds.CenterX - (NodeBounds.Width / 2.0) - PaddingPx;
        double Right = NodeBounds.CenterX + (NodeBounds.Width / 2.0) + PaddingPx;
        double Top = NodeBounds.CenterY + (NodeBounds.Height / 2.0) + PaddingPx;
        double Bottom = NodeBounds.CenterY - (NodeBounds.Height / 2.0) - PaddingPx;
        return PointX >= Left
            && PointX <= Right
            && PointY >= Bottom
            && PointY <= Top;
    }

    private static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SegmentIntersectsSpineColumn(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        double BandLeft = Math.Min(SourceBounds.CenterX, TargetBounds.CenterX) - NODE_PADDING_PX;
        double BandRight = Math.Max(SourceBounds.CenterX, TargetBounds.CenterX) + NODE_PADDING_PX;
        double BandBottom = Math.Min(SourceBounds.CenterY, TargetBounds.CenterY) - NODE_PADDING_PX;
        double BandTop = Math.Max(SourceBounds.CenterY, TargetBounds.CenterY) + NODE_PADDING_PX;
        return FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
            StartX,
            StartY,
            EndX,
            EndY,
            BandLeft,
            BandTop,
            BandRight,
            BandBottom);
    }

}
