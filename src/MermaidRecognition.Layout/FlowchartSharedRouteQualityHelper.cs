using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Shared post-pass for every resolved flowchart route: obstacles, port spreading, hooks, and diagram bounds.
/// </summary>
public static class FlowchartSharedRouteQualityHelper
{
    /// <summary>
    /// Clearance between a corridor and an unrelated node body.
    /// </summary>
    public const double OBSTACLE_CLEARANCE_PX = 16.0;

    /// <summary>
    /// Inset from the content bounding box that a route must stay inside.
    /// </summary>
    public const double DIAGRAM_MARGIN_PX = 8.0;

    /// <summary>
    /// Two attach points closer than this are treated as stacked.
    /// </summary>
    public const double STACKED_PORT_EPSILON_PX = 2.5;

    /// <summary>
    /// Minimum cross-track gap between parallel segments of different edges.
    /// </summary>
    public const double PARALLEL_SEPARATION_PX = 12.0;

    /// <summary>
    /// Minimum gap from a group border when a route runs parallel to that border.
    /// </summary>
    public const double GROUP_BORDER_CLEARANCE_PX = 12.0;

    /// <summary>
    /// Maximum allowed backtrack along the source-to-target axis before a route is treated as hooked.
    /// </summary>
    public const double HOOK_BACKTRACK_PX = 24.0;

    /// <summary>
    /// Maximum distance from a label centre to its own edge.
    /// </summary>
    public const double LABEL_OWN_EDGE_TOLERANCE_PX = 28.0;

    private const double NEAR_ALIGNED_SPAN_PX = 40.0;

    private const double CORNER_HOLD_PX = 28.0;

    private const double FACE_SPAN_RATIO = 0.72;

    private const double SEGMENT_ENDPOINT_TOLERANCE_PX = 1.25;

    private const double SELF_DUPLICATE_PX = 6.0;

    private const double GROUP_HUG_OVERLAP_PX = 24.0;

    /// <summary>
    /// Repairs a resolved route so it stays clear of non-endpoint nodes, inside the diagram, and unhooked.
    /// Spreads this edge's attach points when other edges share the same face.
    /// </summary>
    /// <param name="Edge">The edge.</param>
    /// <param name="Contract">The diagram.</param>
    /// <param name="BoundsById">Node bounds, optionally including group rectangles.</param>
    /// <param name="NodeById">Nodes by id.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <param name="SourceBounds">Source bounds (node or group).</param>
    /// <param name="TargetBounds">Target bounds (node or group).</param>
    /// <param name="SourceShape">Source shape.</param>
    /// <param name="TargetShape">Target shape.</param>
    /// <param name="LayoutPoints">Resolved guide points; replaced when repaired.</param>
    /// <param name="RouteKind">Resolved kind; updated when the path is rebuilt.</param>
    /// <returns>True when a usable route remains.</returns>
    public static bool TryApply(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape,
        List<(double X, double Y)> LayoutPoints,
        ref FlowchartEdgeRouteKind RouteKind)
    {
        if (LayoutPoints.Count < 2)
        {
            return false;
        }

        bool PreserveDesignedRoute = RouteKind == FlowchartEdgeRouteKind.RetryLoop
            || RouteKind == FlowchartEdgeRouteKind.Decision
            || RouteKind == FlowchartEdgeRouteKind.GroupedTwoByTwo;
        if (PreserveDesignedRoute)
        {
            return true;
        }

        if (ShouldSpreadSharedFacePorts(LayoutPoints[0], SourceBounds, Edge, Contract, BoundsById, Direction, IsSource: true)
            || ShouldSpreadSharedFacePorts(LayoutPoints[LayoutPoints.Count - 1], TargetBounds, Edge, Contract, BoundsById, Direction, IsSource: false))
        {
            ApplySharedFacePortSpread(
                Edge,
                Contract,
                BoundsById,
                SourceBounds,
                TargetBounds,
                LayoutPoints);
        }

        List<FlowchartLayoutNodeBounds> Obstacles = CollectObstacles(
            Contract,
            BoundsById,
            Edge.SourceId,
            Edge.TargetId);
        List<FlowchartLayoutNodeBounds> GroupWalls = CollectGroupWalls(Contract, BoundsById);
        (double BoundsLeft, double BoundsBottom, double BoundsRight, double BoundsTop) ContentBounds =
            ComputeContentBounds(Contract, BoundsById);

        bool ChordClearOfClearance = !SegmentComesWithinClearance(
            LayoutPoints[0].X,
            LayoutPoints[0].Y,
            LayoutPoints[LayoutPoints.Count - 1].X,
            LayoutPoints[LayoutPoints.Count - 1].Y,
            Obstacles);
        bool TightAligned = PolylineSpanIsNearAligned(LayoutPoints);
        bool Hooked = HasHookedBacktrack(LayoutPoints) || HasDuplicatedParallelSegments(LayoutPoints);
        bool HitsClearance = PolylineComesWithinClearance(LayoutPoints, Obstacles);
        bool LeavesBounds = PolylineLeavesBounds(LayoutPoints, ContentBounds);
        bool HugsGroup = PolylineHugsGroupBorders(LayoutPoints, GroupWalls);

        if (TightAligned && ChordClearOfClearance && LayoutPoints.Count > 2)
        {
            (double StartX, double StartY) = LayoutPoints[0];
            (double EndX, double EndY) = LayoutPoints[LayoutPoints.Count - 1];
            LayoutPoints.Clear();
            LayoutPoints.Add((StartX, StartY));
            LayoutPoints.Add((EndX, EndY));
            if (RouteKind == FlowchartEdgeRouteKind.Corridor)
            {
                RouteKind = FlowchartEdgeRouteKind.Curved;
            }

            Hooked = false;
            HitsClearance = false;
            HugsGroup = PolylineHugsGroupBorders(LayoutPoints, GroupWalls);
            LeavesBounds = PolylineLeavesBounds(LayoutPoints, ContentBounds);
        }

        bool ExistingCorridorIsHealthy = RouteKind == FlowchartEdgeRouteKind.Corridor
            && !PolylineHitsObstacles(LayoutPoints, Obstacles)
            && !LeavesBounds
            && !Hooked
            && !HugsGroup
            && !TightAligned;
        if (ExistingCorridorIsHealthy)
        {
            return true;
        }

        if (HitsClearance || LeavesBounds || Hooked || HugsGroup || PolylineHitsObstacles(LayoutPoints, Obstacles))
        {
            if (TryBuildInsideChannelRoute(
                    LayoutPoints[0].X,
                    LayoutPoints[0].Y,
                    LayoutPoints[LayoutPoints.Count - 1].X,
                    LayoutPoints[LayoutPoints.Count - 1].Y,
                    SourceBounds,
                    SourceShape,
                    TargetBounds,
                    TargetShape,
                    Obstacles,
                    ContentBounds,
                    Direction,
                    out List<(double X, double Y)> ChannelPoints))
            {
                LayoutPoints.Clear();
                LayoutPoints.AddRange(DensifyOrthogonalChannel(ChannelPoints));
                RouteKind = FlowchartEdgeRouteKind.Curved;
            }
            else if (TryBuildSmoothDetour(
                    LayoutPoints[0].X,
                    LayoutPoints[0].Y,
                    LayoutPoints[LayoutPoints.Count - 1].X,
                    LayoutPoints[LayoutPoints.Count - 1].Y,
                    Obstacles,
                    GroupWalls,
                    ContentBounds,
                    out List<(double X, double Y)> DetourPoints))
            {
                LayoutPoints.Clear();
                LayoutPoints.AddRange(DetourPoints);
                RouteKind = FlowchartEdgeRouteKind.Curved;
            }
        }

        return LayoutPoints.Count >= 2;
    }

    /// <summary>
    /// Returns true when any segment of <paramref name="LayoutPoints"/> intersects a non-endpoint node or unrelated cluster.
    /// </summary>
    public static bool PolylineIntersectsNonEndpointNodes(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId)
    {
        List<FlowchartLayoutNodeBounds> Obstacles = CollectObstacles(
            Contract,
            BoundsById,
            SourceNodeId,
            TargetNodeId);
        return PolylineHitsObstacles(LayoutPoints, Obstacles);
    }

    /// <summary>
    /// Returns true when any segment comes within <see cref="OBSTACLE_CLEARANCE_PX"/> of a non-endpoint node.
    /// </summary>
    public static bool PolylineComesWithinClearance(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId)
    {
        List<FlowchartLayoutNodeBounds> Obstacles = CollectObstacles(
            Contract,
            BoundsById,
            SourceNodeId,
            TargetNodeId);
        return PolylineComesWithinClearance(LayoutPoints, Obstacles);
    }

    /// <summary>
    /// Returns true when any guide point sits outside the content box plus <see cref="DIAGRAM_MARGIN_PX"/>.
    /// </summary>
    public static bool PolylineLeavesDiagramBounds(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        (double BoundsLeft, double BoundsBottom, double BoundsRight, double BoundsTop) ContentBounds =
            ComputeContentBounds(Contract, BoundsById);
        return PolylineLeavesBounds(LayoutPoints, ContentBounds);
    }

    /// <summary>
    /// Returns true when the polyline backtracks toward the source after making progress toward the target.
    /// </summary>
    public static bool HasDisconnectedOrHookedSegments(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        return HasHookedBacktrack(LayoutPoints)
            || HasDegenerateGap(LayoutPoints)
            || HasDuplicatedParallelSegments(LayoutPoints);
    }

    /// <summary>
    /// Returns true when two segments of the same polyline run side by side.
    /// </summary>
    public static bool HasDuplicatedParallelSegments(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        if (LayoutPoints.Count < 4)
        {
            return false;
        }

        for (int LeftIndex = 0; LeftIndex < LayoutPoints.Count - 1; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 2; RightIndex < LayoutPoints.Count - 1; RightIndex++)
            {
                if (ParallelSegmentsOverlap(
                        LayoutPoints[LeftIndex],
                        LayoutPoints[LeftIndex + 1],
                        LayoutPoints[RightIndex],
                        LayoutPoints[RightIndex + 1],
                        SELF_DUPLICATE_PX,
                        GROUP_HUG_OVERLAP_PX))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when the polyline runs parallel to a group border within <see cref="GROUP_BORDER_CLEARANCE_PX"/>.
    /// </summary>
    public static bool PolylineHugsGroupBorders(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        return PolylineHugsGroupBorders(LayoutPoints, CollectGroupWalls(Contract, BoundsById));
    }

    /// <summary>
    /// Returns true when two different-edge polylines have parallel overlapping segments closer than <see cref="PARALLEL_SEPARATION_PX"/>.
    /// </summary>
    public static bool ParallelSegmentsCloserThanSeparation(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        for (int LeftIndex = 0; LeftIndex < Left.Count - 1; LeftIndex++)
        {
            for (int RightIndex = 0; RightIndex < Right.Count - 1; RightIndex++)
            {
                if (ParallelSegmentsOverlap(
                        Left[LeftIndex],
                        Left[LeftIndex + 1],
                        Right[RightIndex],
                        Right[RightIndex + 1],
                        PARALLEL_SEPARATION_PX,
                        GROUP_HUG_OVERLAP_PX))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when the visible routes mix orthogonal corridors with loose curves.
    /// Designed retry, decision, and grouped routes are ignored.
    /// </summary>
    public static bool UsesMixedRoutingStyle(IReadOnlyList<FlowchartEdgeRouteKind> RouteKinds)
    {
        bool HasCorridor = false;
        bool HasLooseCurve = false;
        foreach (FlowchartEdgeRouteKind Kind in RouteKinds)
        {
            if (Kind == FlowchartEdgeRouteKind.RetryLoop
                || Kind == FlowchartEdgeRouteKind.Decision
                || Kind == FlowchartEdgeRouteKind.GroupedTwoByTwo)
            {
                continue;
            }

            if (Kind == FlowchartEdgeRouteKind.Corridor)
            {
                HasCorridor = true;
            }

            if (Kind == FlowchartEdgeRouteKind.Curved
                || Kind == FlowchartEdgeRouteKind.Unknown
                || Kind == FlowchartEdgeRouteKind.CrossGroup)
            {
                HasLooseCurve = true;
            }
        }

        return HasCorridor && HasLooseCurve;
    }

    /// <summary>
    /// When a diagram mixes orthogonal corridors with loose curves, rebuild the corridors as flowchart curves.
    /// Designed retry, decision, and grouped routes are left unchanged.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="BoundsById">Node and group bounds.</param>
    /// <param name="Guides">Resolved guides; corridor entries are rewritten in place.</param>
    public static void UnifyDiagramRoutingStyle(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides)
    {
        List<FlowchartEdgeRouteKind> Kinds = [];
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            Kinds.Add(Guide.RouteKind);
        }

        if (!UsesMixedRoutingStyle(Kinds))
        {
            return;
        }

        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (Guide.RouteKind != FlowchartEdgeRouteKind.Corridor || Guide.Points.Count < 2)
            {
                continue;
            }

            List<(double X, double Y)> Held = DensifyOrthogonalChannel(Guide.Points);
            Guide.Points.Clear();
            Guide.Points.AddRange(Held);
            Guide.RouteKind = FlowchartEdgeRouteKind.Curved;
        }
    }

    /// <summary>
    /// Offsets overlapping parallel runs of different edges so they stay at least <see cref="PARALLEL_SEPARATION_PX"/> apart.
    /// </summary>
    public static void SeparateParallelChannels(IReadOnlyList<List<(double X, double Y)>> Routes)
    {
        for (int LeftIndex = 0; LeftIndex < Routes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Routes.Count; RightIndex++)
            {
                OffsetOverlappingParallelRuns(Routes[LeftIndex], Routes[RightIndex]);
            }
        }
    }

    /// <summary>
    /// Returns true when the guide is a stepped orthogonal channel (at least one horizontal-to-vertical corner).
    /// </summary>
    public static bool LooksLikeOrthogonalCorridor(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        if (LayoutPoints.Count < 3)
        {
            return false;
        }

        for (int Index = 1; Index < LayoutPoints.Count - 1; Index++)
        {
            double InDeltaX = LayoutPoints[Index].X - LayoutPoints[Index - 1].X;
            double InDeltaY = LayoutPoints[Index].Y - LayoutPoints[Index - 1].Y;
            double OutDeltaX = LayoutPoints[Index + 1].X - LayoutPoints[Index].X;
            double OutDeltaY = LayoutPoints[Index + 1].Y - LayoutPoints[Index].Y;
            bool IncomingHorizontal = Math.Abs(InDeltaY) <= 1.0 && Math.Abs(InDeltaX) > 1.0;
            bool IncomingVertical = Math.Abs(InDeltaX) <= 1.0 && Math.Abs(InDeltaY) > 1.0;
            bool OutgoingHorizontal = Math.Abs(OutDeltaY) <= 1.0 && Math.Abs(OutDeltaX) > 1.0;
            bool OutgoingVertical = Math.Abs(OutDeltaX) <= 1.0 && Math.Abs(OutDeltaY) > 1.0;
            if ((IncomingHorizontal && OutgoingVertical) || (IncomingVertical && OutgoingHorizontal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when two attach points coincide within <see cref="STACKED_PORT_EPSILON_PX"/>.
    /// </summary>
    public static bool AttachPointsStack((double X, double Y) Left, (double X, double Y) Right)
    {
        double DeltaX = Left.X - Right.X;
        double DeltaY = Left.Y - Right.Y;
        return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY)) <= STACKED_PORT_EPSILON_PX;
    }

    /// <summary>
    /// Distance from a label centre to the nearest point on the guide polyline.
    /// </summary>
    public static double DistanceFromPointToPolyline(
        double PointX,
        double PointY,
        IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        if (LayoutPoints.Count == 0)
        {
            return double.PositiveInfinity;
        }

        if (LayoutPoints.Count == 1)
        {
            return Distance(PointX, PointY, LayoutPoints[0].X, LayoutPoints[0].Y);
        }

        double Best = double.PositiveInfinity;
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            double Candidate = DistanceFromPointToSegment(
                PointX,
                PointY,
                LayoutPoints[Index].X,
                LayoutPoints[Index].Y,
                LayoutPoints[Index + 1].X,
                LayoutPoints[Index + 1].Y);
            if (Candidate < Best)
            {
                Best = Candidate;
            }
        }

        return Best;
    }

    /// <summary>
    /// Places a label at the polyline mid-arc, then walks along the path until the box clears nodes,
    /// staying beside the stroke.
    /// </summary>
    public static (double X, double Y) ResolveLabelOnOwnEdge(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        double LabelWidth,
        double LabelHeight,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId,
        double OffsetPx)
    {
        (double AnchorX, double AnchorY) = FlowchartEdgeLabelPlacementHelper.ComputeOffsetLabelPosition(
            LayoutPoints,
            OffsetPx);
        if (!LabelOverlapsUnrelatedNode(AnchorX, AnchorY, LabelWidth, LabelHeight, BoundsById, SourceNodeId, TargetNodeId))
        {
            return (AnchorX, AnchorY);
        }

        const int SAMPLE_COUNT = 9;
        for (int SampleIndex = 0; SampleIndex < SAMPLE_COUNT; SampleIndex++)
        {
            double Fraction = 0.2 + (0.6 * SampleIndex / (SAMPLE_COUNT - 1));
            (double MidX, double MidY) = SamplePolyline(LayoutPoints, Fraction);
            (double TangentX, double TangentY) = SampleTangent(LayoutPoints, Fraction);
            (double OffsetX, double OffsetY) = OffsetPerpendicular(MidX, MidY, TangentX, TangentY, OffsetPx);
            if (!LabelOverlapsUnrelatedNode(OffsetX, OffsetY, LabelWidth, LabelHeight, BoundsById, SourceNodeId, TargetNodeId))
            {
                return (OffsetX, OffsetY);
            }

            (double OtherX, double OtherY) = OffsetPerpendicular(MidX, MidY, TangentX, TangentY, -OffsetPx);
            if (!LabelOverlapsUnrelatedNode(OtherX, OtherY, LabelWidth, LabelHeight, BoundsById, SourceNodeId, TargetNodeId))
            {
                return (OtherX, OtherY);
            }
        }

        return (AnchorX, AnchorY);
    }

    private static bool ShouldSpreadSharedFacePorts(
        (double X, double Y) Attach,
        FlowchartLayoutNodeBounds SelfBounds,
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        bool IsSource)
    {
        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            SelfBounds,
            Attach.X,
            Attach.Y);
        if (Face == FlowchartEdgeAttachFace.Unknown)
        {
            return false;
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        bool FlowFacingFace = VerticalFlow
            ? Face == FlowchartEdgeAttachFace.Top || Face == FlowchartEdgeAttachFace.Bottom
            : Face == FlowchartEdgeAttachFace.Left || Face == FlowchartEdgeAttachFace.Right;
        if (!FlowFacingFace)
        {
            return false;
        }

        int SiblingCount = 0;
        string SelfId = IsSource ? Edge.SourceId : Edge.TargetId;
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool CandidateUsesSelfAsSource = string.Equals(Candidate.SourceId, SelfId, StringComparison.Ordinal);
            bool CandidateUsesSelfAsTarget = string.Equals(Candidate.TargetId, SelfId, StringComparison.Ordinal);
            if (IsSource && !CandidateUsesSelfAsSource)
            {
                continue;
            }

            if (!IsSource && !CandidateUsesSelfAsTarget)
            {
                continue;
            }

            string OtherId = IsSource ? Candidate.TargetId : Candidate.SourceId;
            if (!BoundsById.TryGetValue(OtherId, out FlowchartLayoutNodeBounds OtherBounds))
            {
                continue;
            }

            if (OtherNodeLiesOnFaceSide(SelfBounds, OtherBounds, Face))
            {
                SiblingCount++;
            }
        }

        return SiblingCount > 1;
    }

    private static void ApplySharedFacePortSpread(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        List<(double X, double Y)> LayoutPoints)
    {
        (double StartX, double StartY) = SpreadAttachOnFace(
            Edge,
            Contract,
            BoundsById,
            SourceBounds,
            LayoutPoints[0].X,
            LayoutPoints[0].Y,
            IsSource: true);
        (double EndX, double EndY) = SpreadAttachOnFace(
            Edge,
            Contract,
            BoundsById,
            TargetBounds,
            LayoutPoints[LayoutPoints.Count - 1].X,
            LayoutPoints[LayoutPoints.Count - 1].Y,
            IsSource: false);

        ShiftEndpointAndStub(LayoutPoints, 0, StartX, StartY);
        ShiftEndpointAndStub(LayoutPoints, LayoutPoints.Count - 1, EndX, EndY);
    }

    private static (double X, double Y) SpreadAttachOnFace(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SelfBounds,
        double AttachX,
        double AttachY,
        bool IsSource)
    {
        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            SelfBounds,
            AttachX,
            AttachY);
        if (Face == FlowchartEdgeAttachFace.Unknown)
        {
            return (AttachX, AttachY);
        }

        string SelfId = IsSource ? Edge.SourceId : Edge.TargetId;
        List<(string OtherId, double SortKey)> Siblings = [];
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool CandidateUsesSelfAsSource = string.Equals(Candidate.SourceId, SelfId, StringComparison.Ordinal);
            bool CandidateUsesSelfAsTarget = string.Equals(Candidate.TargetId, SelfId, StringComparison.Ordinal);
            if (IsSource && !CandidateUsesSelfAsSource)
            {
                continue;
            }

            if (!IsSource && !CandidateUsesSelfAsTarget)
            {
                continue;
            }

            string OtherId = IsSource ? Candidate.TargetId : Candidate.SourceId;
            if (!BoundsById.TryGetValue(OtherId, out FlowchartLayoutNodeBounds OtherBounds))
            {
                continue;
            }

            if (!OtherNodeLiesOnFaceSide(SelfBounds, OtherBounds, Face))
            {
                continue;
            }

            bool AlreadyListed = false;
            foreach ((string ListedId, double _) in Siblings)
            {
                if (string.Equals(ListedId, OtherId, StringComparison.Ordinal))
                {
                    AlreadyListed = true;
                    break;
                }
            }

            if (AlreadyListed)
            {
                continue;
            }

            double SortKey = Face == FlowchartEdgeAttachFace.Top || Face == FlowchartEdgeAttachFace.Bottom
                ? OtherBounds.CenterX
                : OtherBounds.CenterY;
            Siblings.Add((OtherId, SortKey));
        }

        if (Siblings.Count <= 1)
        {
            return (AttachX, AttachY);
        }

        Siblings.Sort((Left, Right) => Left.SortKey.CompareTo(Right.SortKey));
        string ThisOtherId = IsSource ? Edge.TargetId : Edge.SourceId;
        int SlotIndex = 0;
        for (int Index = 0; Index < Siblings.Count; Index++)
        {
            if (string.Equals(Siblings[Index].OtherId, ThisOtherId, StringComparison.Ordinal))
            {
                SlotIndex = Index;
                break;
            }
        }

        return SlotOnFace(SelfBounds, Face, SlotIndex, Siblings.Count);
    }

    private static bool OtherNodeLiesOnFaceSide(
        FlowchartLayoutNodeBounds Self,
        FlowchartLayoutNodeBounds Other,
        FlowchartEdgeAttachFace Face)
    {
        switch (Face)
        {
            case FlowchartEdgeAttachFace.Left:
                return Other.CenterX <= Self.CenterX;
            case FlowchartEdgeAttachFace.Right:
                return Other.CenterX >= Self.CenterX;
            case FlowchartEdgeAttachFace.Top:
                return Other.CenterY >= Self.CenterY;
            case FlowchartEdgeAttachFace.Bottom:
                return Other.CenterY <= Self.CenterY;
            default:
                return false;
        }
    }

    private static (double X, double Y) SlotOnFace(
        FlowchartLayoutNodeBounds Bounds,
        FlowchartEdgeAttachFace Face,
        int SlotIndex,
        int SlotCount)
    {
        double HalfWidth = Bounds.Width / 2.0;
        double HalfHeight = Bounds.Height / 2.0;
        double Fraction = SlotCount <= 1
            ? 0.5
            : (SlotIndex + 1.0) / (SlotCount + 1.0);
        double Centered = (Fraction - 0.5) * FACE_SPAN_RATIO;

        switch (Face)
        {
            case FlowchartEdgeAttachFace.Top:
                return (Bounds.CenterX + (Centered * Bounds.Width), Bounds.CenterY + HalfHeight);
            case FlowchartEdgeAttachFace.Bottom:
                return (Bounds.CenterX + (Centered * Bounds.Width), Bounds.CenterY - HalfHeight);
            case FlowchartEdgeAttachFace.Left:
                return (Bounds.CenterX - HalfWidth, Bounds.CenterY + (Centered * Bounds.Height));
            case FlowchartEdgeAttachFace.Right:
                return (Bounds.CenterX + HalfWidth, Bounds.CenterY + (Centered * Bounds.Height));
            default:
                return (Bounds.CenterX, Bounds.CenterY);
        }
    }

    private static void ShiftEndpointAndStub(
        List<(double X, double Y)> LayoutPoints,
        int EndpointIndex,
        double NewX,
        double NewY)
    {
        double OldX = LayoutPoints[EndpointIndex].X;
        double OldY = LayoutPoints[EndpointIndex].Y;
        double DeltaX = NewX - OldX;
        double DeltaY = NewY - OldY;
        if (Math.Abs(DeltaX) < 1e-6 && Math.Abs(DeltaY) < 1e-6)
        {
            return;
        }

        LayoutPoints[EndpointIndex] = (NewX, NewY);
        int NeighbourIndex = EndpointIndex == 0 ? 1 : LayoutPoints.Count - 2;
        if (NeighbourIndex < 0 || NeighbourIndex >= LayoutPoints.Count)
        {
            return;
        }

        (double NeighbourX, double NeighbourY) = LayoutPoints[NeighbourIndex];
        bool AlignedWithFace = (Math.Abs(NeighbourX - OldX) <= 1.0 && Math.Abs(DeltaX) > 0.5)
            || (Math.Abs(NeighbourY - OldY) <= 1.0 && Math.Abs(DeltaY) > 0.5);
        if (AlignedWithFace)
        {
            LayoutPoints[NeighbourIndex] = (NeighbourX + DeltaX, NeighbourY + DeltaY);
        }
    }


    private static bool TryBuildInsideChannelRoute(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        List<FlowchartLayoutNodeBounds> Obstacles,
        (double Left, double Bottom, double Right, double Top) ContentBounds,
        FlowchartDirection Direction,
        out List<(double X, double Y)> Points)
    {
        Points = [];
        if (!SegmentComesWithinClearance(StartX, StartY, EndX, EndY, Obstacles)
            && PointInsideBounds(StartX, StartY, ContentBounds)
            && PointInsideBounds(EndX, EndY, ContentBounds))
        {
            Points.Add((StartX, StartY));
            Points.Add((EndX, EndY));
            return true;
        }

        List<double> ChannelXs = CollectChannelCoordinates(
            Obstacles,
            Math.Min(StartY, EndY),
            Math.Max(StartY, EndY),
            Horizontal: false,
            ContentBounds);
        List<double> ChannelYs = CollectChannelCoordinates(
            Obstacles,
            Math.Min(StartX, EndX),
            Math.Max(StartX, EndX),
            Horizontal: true,
            ContentBounds);

        List<(double X, double Y)> Best = [];
        double BestLength = double.PositiveInfinity;
        foreach (double ChannelX in ChannelXs)
        {
            List<(double X, double Y)> Candidate = [];
            FlowchartPerpendicularRouteBuilder.AppendOrthogonalPerpendicularRouteWithGuideX(
                Candidate,
                StartX,
                StartY,
                SourceBounds,
                SourceShape,
                EndX,
                EndY,
                TargetBounds,
                TargetShape,
                ChannelX);
            if (IsAcceptableChannel(Candidate, Obstacles, ContentBounds, BestLength, out double Length))
            {
                Best = Candidate;
                BestLength = Length;
            }
        }

        foreach (double ChannelY in ChannelYs)
        {
            List<(double X, double Y)> Candidate = [];
            FlowchartPerpendicularRouteBuilder.AppendPerpendicularRouteWithCorridorY(
                Candidate,
                StartX,
                StartY,
                SourceBounds,
                SourceShape,
                EndX,
                EndY,
                TargetBounds,
                TargetShape,
                ChannelY);
            if (IsAcceptableChannel(Candidate, Obstacles, ContentBounds, BestLength, out double Length))
            {
                Best = Candidate;
                BestLength = Length;
            }
        }

        foreach (double ChannelX in ChannelXs)
        {
            foreach (double ChannelY in ChannelYs)
            {
                List<(double X, double Y)> Stepped =
                [
                    (StartX, StartY),
                    (StartX, ChannelY),
                    (ChannelX, ChannelY),
                    (ChannelX, EndY),
                    (EndX, EndY)
                ];
                if (IsAcceptableChannel(Stepped, Obstacles, ContentBounds, BestLength, out double SteppedLength))
                {
                    Best = Stepped;
                    BestLength = SteppedLength;
                }

                List<(double X, double Y)> Alternate =
                [
                    (StartX, StartY),
                    (ChannelX, StartY),
                    (ChannelX, ChannelY),
                    (EndX, ChannelY),
                    (EndX, EndY)
                ];
                if (IsAcceptableChannel(Alternate, Obstacles, ContentBounds, BestLength, out double AlternateLength))
                {
                    Best = Alternate;
                    BestLength = AlternateLength;
                }
            }
        }

        if (Best.Count >= 2)
        {
            Points = Best;
            return true;
        }

        _ = Direction;
        return false;
    }

    private static bool IsAcceptableChannel(
        List<(double X, double Y)> Candidate,
        List<FlowchartLayoutNodeBounds> Obstacles,
        (double Left, double Bottom, double Right, double Top) ContentBounds,
        double BestLength,
        out double Length)
    {
        Length = PolylineLength(Candidate);
        if (Candidate.Count < 2 || Length >= BestLength)
        {
            return false;
        }

        if (PolylineComesWithinClearance(Candidate, Obstacles) || PolylineLeavesBounds(Candidate, ContentBounds))
        {
            return false;
        }

        return true;
    }

    private static List<double> CollectChannelCoordinates(
        List<FlowchartLayoutNodeBounds> Obstacles,
        double SpanMin,
        double SpanMax,
        bool Horizontal,
        (double Left, double Bottom, double Right, double Top) ContentBounds)
    {
        List<double> Channels = [];
        double MinBound = Horizontal ? ContentBounds.Bottom + DIAGRAM_MARGIN_PX : ContentBounds.Left + DIAGRAM_MARGIN_PX;
        double MaxBound = Horizontal ? ContentBounds.Top - DIAGRAM_MARGIN_PX : ContentBounds.Right - DIAGRAM_MARGIN_PX;
        double SpanLow = double.PositiveInfinity;
        double SpanHigh = double.NegativeInfinity;
        List<(double Low, double High)> Occupied = [];

        foreach (FlowchartLayoutNodeBounds Obstacle in Obstacles)
        {
            double CrossLow = Horizontal
                ? Obstacle.CenterX - (Obstacle.Width / 2.0)
                : Obstacle.CenterY - (Obstacle.Height / 2.0);
            double CrossHigh = Horizontal
                ? Obstacle.CenterX + (Obstacle.Width / 2.0)
                : Obstacle.CenterY + (Obstacle.Height / 2.0);
            if (CrossHigh < SpanMin || CrossLow > SpanMax)
            {
                continue;
            }

            double AlongLow = Horizontal
                ? Obstacle.CenterY - (Obstacle.Height / 2.0)
                : Obstacle.CenterX - (Obstacle.Width / 2.0);
            double AlongHigh = Horizontal
                ? Obstacle.CenterY + (Obstacle.Height / 2.0)
                : Obstacle.CenterX + (Obstacle.Width / 2.0);
            Occupied.Add((AlongLow, AlongHigh));
            if (AlongLow < SpanLow)
            {
                SpanLow = AlongLow;
            }

            if (AlongHigh > SpanHigh)
            {
                SpanHigh = AlongHigh;
            }
        }

        if (Occupied.Count == 0)
        {
            return Channels;
        }

        double LeftChannel = SpanLow - OBSTACLE_CLEARANCE_PX;
        double RightChannel = SpanHigh + OBSTACLE_CLEARANCE_PX;
        AddClampedChannel(Channels, LeftChannel, MinBound, MaxBound);
        AddClampedChannel(Channels, RightChannel, MinBound, MaxBound);

        Occupied.Sort((Left, Right) => Left.Low.CompareTo(Right.Low));
        const double CHANNEL_SCAN_STEP_PX = 20.0;
        for (double Scan = MinBound; Scan <= MaxBound; Scan += CHANNEL_SCAN_STEP_PX)
        {
            AddClampedChannel(Channels, Scan, MinBound, MaxBound);
        }

        double GapCursor = Occupied[0].High;
        for (int Index = 1; Index < Occupied.Count; Index++)
        {
            double NextLow = Occupied[Index].Low;
            double Gap = NextLow - GapCursor;
            if (Gap >= OBSTACLE_CLEARANCE_PX)
            {
                AddClampedChannel(Channels, (GapCursor + NextLow) / 2.0, MinBound, MaxBound);
            }

            if (Occupied[Index].High > GapCursor)
            {
                GapCursor = Occupied[Index].High;
            }
        }

        return Channels;
    }

    private static void AddClampedChannel(List<double> Channels, double Value, double MinBound, double MaxBound)
    {
        double Clamped = Math.Max(MinBound, Math.Min(MaxBound, Value));
        foreach (double Existing in Channels)
        {
            if (Math.Abs(Existing - Clamped) < 1.0)
            {
                return;
            }
        }

        Channels.Add(Clamped);
    }

    private static List<(double X, double Y)> DensifyOrthogonalChannel(List<(double X, double Y)> Points)
    {
        if (Points.Count < 3)
        {
            return Points;
        }

        List<(double X, double Y)> Held = [];
        Held.Add(Points[0]);
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            (double PreviousX, double PreviousY) = Points[Index - 1];
            (double CornerX, double CornerY) = Points[Index];
            (double NextX, double NextY) = Points[Index + 1];
            double InDeltaX = CornerX - PreviousX;
            double InDeltaY = CornerY - PreviousY;
            double OutDeltaX = NextX - CornerX;
            double OutDeltaY = NextY - CornerY;
            bool IncomingHorizontal = Math.Abs(InDeltaY) <= 1.0 && Math.Abs(InDeltaX) > 1.0;
            bool IncomingVertical = Math.Abs(InDeltaX) <= 1.0 && Math.Abs(InDeltaY) > 1.0;
            bool OutgoingHorizontal = Math.Abs(OutDeltaY) <= 1.0 && Math.Abs(OutDeltaX) > 1.0;
            bool OutgoingVertical = Math.Abs(OutDeltaX) <= 1.0 && Math.Abs(OutDeltaY) > 1.0;
            bool IsCorner = (IncomingHorizontal && OutgoingVertical) || (IncomingVertical && OutgoingHorizontal);
            if (!IsCorner)
            {
                Held.Add((CornerX, CornerY));
                continue;
            }

            double InLength = Distance(PreviousX, PreviousY, CornerX, CornerY);
            double OutLength = Distance(CornerX, CornerY, NextX, NextY);
            double InHold = Math.Min(CORNER_HOLD_PX, InLength * 0.45);
            double OutHold = Math.Min(CORNER_HOLD_PX, OutLength * 0.45);
            if (InHold >= 4.0)
            {
                Held.Add((
                    CornerX - ((InDeltaX / InLength) * InHold),
                    CornerY - ((InDeltaY / InLength) * InHold)));
            }

            Held.Add((CornerX, CornerY));
            if (OutHold >= 4.0)
            {
                Held.Add((
                    CornerX + ((OutDeltaX / OutLength) * OutHold),
                    CornerY + ((OutDeltaY / OutLength) * OutHold)));
            }
        }

        Held.Add(Points[Points.Count - 1]);
        return Held;
    }

    private static bool PointInsideBounds(
        double PointX,
        double PointY,
        (double Left, double Bottom, double Right, double Top) ContentBounds)
    {
        return PointX >= ContentBounds.Left - DIAGRAM_MARGIN_PX
            && PointX <= ContentBounds.Right + DIAGRAM_MARGIN_PX
            && PointY >= ContentBounds.Bottom - DIAGRAM_MARGIN_PX
            && PointY <= ContentBounds.Top + DIAGRAM_MARGIN_PX;
    }

    private static List<FlowchartLayoutNodeBounds> CollectObstacles(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId)
    {
        List<FlowchartLayoutNodeBounds> Obstacles = [];
        HashSet<string> SkipIds = new HashSet<string>(StringComparer.Ordinal)
        {
            SourceNodeId,
            TargetNodeId
        };

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
        {
            if (SkipIds.Contains(Pair.Key) || FlowchartCompoundEndpointHelper.IsGroupId(Contract, Pair.Key))
            {
                continue;
            }

            Obstacles.Add(Pair.Value);
        }

        string? SourceGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(SourceNodeId, Contract);
        string? TargetGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(TargetNodeId, Contract);
        List<FlowchartSubgraphLayoutRect> ClusterRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        foreach (FlowchartSubgraphLayoutRect Cluster in ClusterRects)
        {
            if (string.Equals(Cluster.GroupId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(Cluster.GroupId, TargetNodeId, StringComparison.Ordinal)
                || IsSameOrAncestorGroup(Cluster.GroupId, SourceGroupId, Contract)
                || IsSameOrAncestorGroup(Cluster.GroupId, TargetGroupId, Contract))
            {
                continue;
            }

            Obstacles.Add(FlowchartCompoundEndpointHelper.ToNodeBounds(Cluster));
        }

        return Obstacles;
    }

    private static bool IsSameOrAncestorGroup(string CandidateGroupId, string? MemberGroupId, DiagramContract Contract)
    {
        string? Cursor = MemberGroupId;
        while (!string.IsNullOrEmpty(Cursor))
        {
            if (string.Equals(Cursor, CandidateGroupId, StringComparison.Ordinal))
            {
                return true;
            }

            string? Parent = null;
            foreach (GroupContract Group in Contract.Groups)
            {
                if (string.Equals(Group.Id, Cursor, StringComparison.Ordinal))
                {
                    Parent = Group.ParentGroupId;
                    break;
                }
            }

            Cursor = Parent;
        }

        return false;
    }

    private static (double Left, double Bottom, double Right, double Top) ComputeContentBounds(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double Left = double.PositiveInfinity;
        double Bottom = double.PositiveInfinity;
        double Right = double.NegativeInfinity;
        double Top = double.NegativeInfinity;
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            ExpandBounds(ref Left, ref Bottom, ref Right, ref Top, Bounds);
        }

        foreach (FlowchartSubgraphLayoutRect Cluster in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById))
        {
            if (Cluster.Left < Left)
            {
                Left = Cluster.Left;
            }

            if (Cluster.Bottom < Bottom)
            {
                Bottom = Cluster.Bottom;
            }

            if (Cluster.Right > Right)
            {
                Right = Cluster.Right;
            }

            if (Cluster.Top > Top)
            {
                Top = Cluster.Top;
            }
        }

        if (double.IsInfinity(Left))
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        return (Left, Bottom, Right, Top);
    }

    private static void ExpandBounds(
        ref double Left,
        ref double Bottom,
        ref double Right,
        ref double Top,
        FlowchartLayoutNodeBounds Bounds)
    {
        double NodeLeft = Bounds.CenterX - (Bounds.Width / 2.0);
        double NodeRight = Bounds.CenterX + (Bounds.Width / 2.0);
        double NodeBottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double NodeTop = Bounds.CenterY + (Bounds.Height / 2.0);
        if (NodeLeft < Left)
        {
            Left = NodeLeft;
        }

        if (NodeBottom < Bottom)
        {
            Bottom = NodeBottom;
        }

        if (NodeRight > Right)
        {
            Right = NodeRight;
        }

        if (NodeTop > Top)
        {
            Top = NodeTop;
        }
    }

    private static bool PolylineHitsObstacles(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        List<FlowchartLayoutNodeBounds> Obstacles)
    {
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            if (SegmentHitsObstacleList(
                    LayoutPoints[Index].X,
                    LayoutPoints[Index].Y,
                    LayoutPoints[Index + 1].X,
                    LayoutPoints[Index + 1].Y,
                    Obstacles))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentHitsObstacleList(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        List<FlowchartLayoutNodeBounds> Obstacles)
    {
        foreach (FlowchartLayoutNodeBounds Obstacle in Obstacles)
        {
            if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    StartX,
                    StartY,
                    EndX,
                    EndY,
                    Obstacle,
                    SEGMENT_ENDPOINT_TOLERANCE_PX))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PolylineComesWithinClearance(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        List<FlowchartLayoutNodeBounds> Obstacles)
    {
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            if (SegmentComesWithinClearance(
                    LayoutPoints[Index].X,
                    LayoutPoints[Index].Y,
                    LayoutPoints[Index + 1].X,
                    LayoutPoints[Index + 1].Y,
                    Obstacles))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentComesWithinClearance(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        List<FlowchartLayoutNodeBounds> Obstacles)
    {
        foreach (FlowchartLayoutNodeBounds Obstacle in Obstacles)
        {
            FlowchartLayoutNodeBounds Inflated = InflateBounds(Obstacle, OBSTACLE_CLEARANCE_PX);
            if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    StartX,
                    StartY,
                    EndX,
                    EndY,
                    Inflated,
                    0.0))
            {
                return true;
            }
        }

        return false;
    }

    private static FlowchartLayoutNodeBounds InflateBounds(FlowchartLayoutNodeBounds Bounds, double PaddingPx)
    {
        return new FlowchartLayoutNodeBounds
        {
            NodeId = Bounds.NodeId,
            CenterX = Bounds.CenterX,
            CenterY = Bounds.CenterY,
            Width = Bounds.Width + (2.0 * PaddingPx),
            Height = Bounds.Height + (2.0 * PaddingPx)
        };
    }


    private static bool PolylineLeavesBounds(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        (double Left, double Bottom, double Right, double Top) ContentBounds)
    {
        double MinX = ContentBounds.Left - DIAGRAM_MARGIN_PX;
        double MaxX = ContentBounds.Right + DIAGRAM_MARGIN_PX;
        double MinY = ContentBounds.Bottom - DIAGRAM_MARGIN_PX;
        double MaxY = ContentBounds.Top + DIAGRAM_MARGIN_PX;
        foreach ((double PointX, double PointY) in LayoutPoints)
        {
            if (PointX < MinX || PointX > MaxX || PointY < MinY || PointY > MaxY)
            {
                return true;
            }
        }

        return false;
    }


    private static bool HasHookedBacktrack(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        if (LayoutPoints.Count < 3)
        {
            return false;
        }

        double AxisX = LayoutPoints[LayoutPoints.Count - 1].X - LayoutPoints[0].X;
        double AxisY = LayoutPoints[LayoutPoints.Count - 1].Y - LayoutPoints[0].Y;
        double AxisLength = Math.Sqrt((AxisX * AxisX) + (AxisY * AxisY));
        if (AxisLength < 1e-6)
        {
            return false;
        }

        AxisX /= AxisLength;
        AxisY /= AxisLength;
        double MaximumProgress = 0.0;
        int LastInteriorIndex = LayoutPoints.Count - 1;
        for (int Index = 0; Index < LastInteriorIndex; Index++)
        {
            double Progress = ((LayoutPoints[Index].X - LayoutPoints[0].X) * AxisX)
                + ((LayoutPoints[Index].Y - LayoutPoints[0].Y) * AxisY);
            if (Progress > MaximumProgress)
            {
                MaximumProgress = Progress;
            }
            else if ((MaximumProgress - Progress) > HOOK_BACKTRACK_PX)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDegenerateGap(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            if (double.IsNaN(LayoutPoints[Index].X) || double.IsNaN(LayoutPoints[Index].Y))
            {
                return true;
            }
        }

        return false;
    }


    private static bool LabelOverlapsUnrelatedNode(
        double CentreX,
        double CentreY,
        double LabelWidth,
        double LabelHeight,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId)
    {
        double LabelLeft = CentreX - (LabelWidth / 2.0);
        double LabelRight = CentreX + (LabelWidth / 2.0);
        double LabelBottom = CentreY - (LabelHeight / 2.0);
        double LabelTop = CentreY + (LabelHeight / 2.0);
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
        {
            if (string.Equals(Pair.Key, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(Pair.Key, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Bounds = Pair.Value;
            double NodeLeft = Bounds.CenterX - (Bounds.Width / 2.0);
            double NodeRight = Bounds.CenterX + (Bounds.Width / 2.0);
            double NodeBottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double NodeTop = Bounds.CenterY + (Bounds.Height / 2.0);
            if (LabelRight > NodeLeft && LabelLeft < NodeRight && LabelTop > NodeBottom && LabelBottom < NodeTop)
            {
                return true;
            }
        }

        return false;
    }

    private static (double X, double Y) SamplePolyline(IReadOnlyList<(double X, double Y)> LayoutPoints, double Fraction)
    {
        double Total = PolylineLength(LayoutPoints);
        if (Total <= 1e-7)
        {
            return LayoutPoints[0];
        }

        double Target = Total * Math.Max(0.0, Math.Min(1.0, Fraction));
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            double Segment = Distance(
                LayoutPoints[Index].X,
                LayoutPoints[Index].Y,
                LayoutPoints[Index + 1].X,
                LayoutPoints[Index + 1].Y);
            if (Segment < Target)
            {
                Target -= Segment;
                continue;
            }

            if (Segment <= 1e-7)
            {
                continue;
            }

            double Ratio = Target / Segment;
            return (
                LayoutPoints[Index].X + ((LayoutPoints[Index + 1].X - LayoutPoints[Index].X) * Ratio),
                LayoutPoints[Index].Y + ((LayoutPoints[Index + 1].Y - LayoutPoints[Index].Y) * Ratio));
        }

        return LayoutPoints[LayoutPoints.Count - 1];
    }

    private static (double X, double Y) SampleTangent(IReadOnlyList<(double X, double Y)> LayoutPoints, double Fraction)
    {
        (double MidX, double MidY) = SamplePolyline(LayoutPoints, Fraction);
        (double NextX, double NextY) = SamplePolyline(LayoutPoints, Math.Min(1.0, Fraction + 0.05));
        return (NextX - MidX, NextY - MidY);
    }

    private static (double X, double Y) OffsetPerpendicular(
        double AnchorX,
        double AnchorY,
        double TangentX,
        double TangentY,
        double OffsetPx)
    {
        double Length = Math.Sqrt((TangentX * TangentX) + (TangentY * TangentY));
        if (Length <= 1e-7)
        {
            return (AnchorX, AnchorY + OffsetPx);
        }

        return (AnchorX + ((-TangentY / Length) * OffsetPx), AnchorY + ((TangentX / Length) * OffsetPx));
    }

    private static double PolylineLength(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        double Total = 0.0;
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            Total += Distance(
                LayoutPoints[Index].X,
                LayoutPoints[Index].Y,
                LayoutPoints[Index + 1].X,
                LayoutPoints[Index + 1].Y);
        }

        return Total;
    }

    private static double DistanceFromPointToSegment(
        double PointX,
        double PointY,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        double DeltaX = EndX - StartX;
        double DeltaY = EndY - StartY;
        double LengthSquared = (DeltaX * DeltaX) + (DeltaY * DeltaY);
        if (LengthSquared <= 1e-12)
        {
            return Distance(PointX, PointY, StartX, StartY);
        }

        double Parameter = (((PointX - StartX) * DeltaX) + ((PointY - StartY) * DeltaY)) / LengthSquared;
        Parameter = Math.Max(0.0, Math.Min(1.0, Parameter));
        return Distance(PointX, PointY, StartX + (DeltaX * Parameter), StartY + (DeltaY * Parameter));
    }

    private static double Distance(double LeftX, double LeftY, double RightX, double RightY)
    {
        double DeltaX = LeftX - RightX;
        double DeltaY = LeftY - RightY;
        return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
    }

    private static bool PolylineSpanIsNearAligned(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        double MinX = LayoutPoints[0].X;
        double MaxX = LayoutPoints[0].X;
        double MinY = LayoutPoints[0].Y;
        double MaxY = LayoutPoints[0].Y;
        for (int Index = 1; Index < LayoutPoints.Count; Index++)
        {
            if (LayoutPoints[Index].X < MinX)
            {
                MinX = LayoutPoints[Index].X;
            }

            if (LayoutPoints[Index].X > MaxX)
            {
                MaxX = LayoutPoints[Index].X;
            }

            if (LayoutPoints[Index].Y < MinY)
            {
                MinY = LayoutPoints[Index].Y;
            }

            if (LayoutPoints[Index].Y > MaxY)
            {
                MaxY = LayoutPoints[Index].Y;
            }
        }

        return (MaxX - MinX) <= NEAR_ALIGNED_SPAN_PX || (MaxY - MinY) <= NEAR_ALIGNED_SPAN_PX;
    }

    private static List<FlowchartLayoutNodeBounds> CollectGroupWalls(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        List<FlowchartLayoutNodeBounds> Walls = [];
        foreach (FlowchartSubgraphLayoutRect Cluster in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById))
        {
            Walls.Add(FlowchartCompoundEndpointHelper.ToNodeBounds(Cluster));
        }

        return Walls;
    }

    private static bool PolylineHugsGroupBorders(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        List<FlowchartLayoutNodeBounds> GroupWalls)
    {
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            foreach (FlowchartLayoutNodeBounds Wall in GroupWalls)
            {
                if (SegmentHugsGroupWall(
                        LayoutPoints[Index].X,
                        LayoutPoints[Index].Y,
                        LayoutPoints[Index + 1].X,
                        LayoutPoints[Index + 1].Y,
                        Wall))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentHugsGroupWall(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds Wall)
    {
        double Left = Wall.CenterX - (Wall.Width / 2.0);
        double Right = Wall.CenterX + (Wall.Width / 2.0);
        double Bottom = Wall.CenterY - (Wall.Height / 2.0);
        double Top = Wall.CenterY + (Wall.Height / 2.0);
        bool Vertical = Math.Abs(EndX - StartX) <= GROUP_BORDER_CLEARANCE_PX
            && Math.Abs(EndY - StartY) > GROUP_HUG_OVERLAP_PX;
        bool Horizontal = Math.Abs(EndY - StartY) <= GROUP_BORDER_CLEARANCE_PX
            && Math.Abs(EndX - StartX) > GROUP_HUG_OVERLAP_PX;
        if (Vertical)
        {
            double SegLow = Math.Min(StartY, EndY);
            double SegHigh = Math.Max(StartY, EndY);
            double Overlap = Math.Min(SegHigh, Top) - Math.Max(SegLow, Bottom);
            if (Overlap < GROUP_HUG_OVERLAP_PX)
            {
                return false;
            }

            double RunX = (StartX + EndX) / 2.0;
            return Math.Abs(RunX - Left) < GROUP_BORDER_CLEARANCE_PX
                || Math.Abs(RunX - Right) < GROUP_BORDER_CLEARANCE_PX;
        }

        if (Horizontal)
        {
            double SegLow = Math.Min(StartX, EndX);
            double SegHigh = Math.Max(StartX, EndX);
            double Overlap = Math.Min(SegHigh, Right) - Math.Max(SegLow, Left);
            if (Overlap < GROUP_HUG_OVERLAP_PX)
            {
                return false;
            }

            double RunY = (StartY + EndY) / 2.0;
            return Math.Abs(RunY - Bottom) < GROUP_BORDER_CLEARANCE_PX
                || Math.Abs(RunY - Top) < GROUP_BORDER_CLEARANCE_PX;
        }

        return false;
    }

    private static bool TryBuildSmoothDetour(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        List<FlowchartLayoutNodeBounds> Obstacles,
        List<FlowchartLayoutNodeBounds> GroupWalls,
        (double Left, double Bottom, double Right, double Top) ContentBounds,
        out List<(double X, double Y)> Points)
    {
        Points = [];
        List<(double X, double Y)> Direct = [(StartX, StartY), (EndX, EndY)];
        if (IsAcceptableSmoothRoute(Direct, Obstacles, GroupWalls, ContentBounds))
        {
            Points = Direct;
            return true;
        }

        double MidX = (StartX + EndX) / 2.0;
        double MidY = (StartY + EndY) / 2.0;
        double DeltaX = EndX - StartX;
        double DeltaY = EndY - StartY;
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (Length < 1e-6)
        {
            return false;
        }

        double PerpX = -DeltaY / Length;
        double PerpY = DeltaX / Length;
        double[] Offsets = [48.0, 72.0, 96.0, 120.0, 160.0, 200.0, 260.0, -48.0, -72.0, -96.0, -120.0, -160.0, -200.0, -260.0];
        foreach (double Offset in Offsets)
        {
            List<(double X, double Y)> Candidate =
            [
                (StartX, StartY),
                (MidX + (PerpX * Offset), MidY + (PerpY * Offset)),
                (EndX, EndY)
            ];
            if (IsAcceptableSmoothRoute(Candidate, Obstacles, GroupWalls, ContentBounds))
            {
                Points = Candidate;
                return true;
            }
        }

        double ThirdX = StartX + (DeltaX / 3.0);
        double ThirdY = StartY + (DeltaY / 3.0);
        double TwoThirdX = StartX + ((2.0 * DeltaX) / 3.0);
        double TwoThirdY = StartY + ((2.0 * DeltaY) / 3.0);
        foreach (double Offset in Offsets)
        {
            List<(double X, double Y)> Candidate =
            [
                (StartX, StartY),
                (ThirdX + (PerpX * Offset), ThirdY + (PerpY * Offset)),
                (TwoThirdX - (PerpX * Offset), TwoThirdY - (PerpY * Offset)),
                (EndX, EndY)
            ];
            if (IsAcceptableSmoothRoute(Candidate, Obstacles, GroupWalls, ContentBounds))
            {
                Points = Candidate;
                return true;
            }
        }

        if (TryBuildAroundObstacleRoute(StartX, StartY, EndX, EndY, Obstacles, GroupWalls, ContentBounds, out Points))
        {
            return true;
        }

        return false;
    }

    private static bool TryBuildAroundObstacleRoute(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        List<FlowchartLayoutNodeBounds> Obstacles,
        List<FlowchartLayoutNodeBounds> GroupWalls,
        (double Left, double Bottom, double Right, double Top) ContentBounds,
        out List<(double X, double Y)> Points)
    {
        Points = [];
        List<FlowchartLayoutNodeBounds> Blocking = [];
        AddBlocking(Blocking, Obstacles, StartX, StartY, EndX, EndY);
        AddBlocking(Blocking, GroupWalls, StartX, StartY, EndX, EndY);
        if (Blocking.Count == 0)
        {
            return false;
        }

        double Clear = Math.Max(OBSTACLE_CLEARANCE_PX, GROUP_BORDER_CLEARANCE_PX) + 2.0;
        (double Left, double Bottom, double Right, double Top) RelaxedBounds =
            (ContentBounds.Left - Clear,
                ContentBounds.Bottom - Clear,
                ContentBounds.Right + Clear,
                ContentBounds.Top + Clear);

        List<(double X, double Y)> Best = [];
        double BestLength = double.PositiveInfinity;
        for (int Iteration = 0; Iteration < 6; Iteration++)
        {
            double BoxLeft = double.PositiveInfinity;
            double BoxBottom = double.PositiveInfinity;
            double BoxRight = double.NegativeInfinity;
            double BoxTop = double.NegativeInfinity;
            foreach (FlowchartLayoutNodeBounds Obstacle in Blocking)
            {
                ExpandBounds(ref BoxLeft, ref BoxBottom, ref BoxRight, ref BoxTop, Obstacle);
            }

            double RightX = Math.Max(Math.Max(StartX, EndX), BoxRight + Clear);
            double LeftX = Math.Min(Math.Min(StartX, EndX), BoxLeft - Clear);
            double HighY = Math.Max(Math.Max(StartY, EndY), BoxTop + Clear);
            double LowY = Math.Min(Math.Min(StartY, EndY), BoxBottom - Clear);
            int BlockingCountBefore = Blocking.Count;

            List<List<(double X, double Y)>> Candidates =
            [
                [(StartX, StartY), (RightX, StartY), (RightX, EndY), (EndX, EndY)],
                [(StartX, StartY), (LeftX, StartY), (LeftX, EndY), (EndX, EndY)],
                [(StartX, StartY), (StartX, HighY), (EndX, HighY), (EndX, EndY)],
                [(StartX, StartY), (StartX, LowY), (EndX, LowY), (EndX, EndY)],
                [(StartX, StartY), (StartX, HighY), (RightX, HighY), (RightX, EndY), (EndX, EndY)],
                [(StartX, StartY), (StartX, HighY), (LeftX, HighY), (LeftX, EndY), (EndX, EndY)],
                [(StartX, StartY), (StartX, LowY), (RightX, LowY), (RightX, EndY), (EndX, EndY)],
                [(StartX, StartY), (StartX, LowY), (LeftX, LowY), (LeftX, EndY), (EndX, EndY)],
                [(StartX, StartY), (RightX, StartY), (RightX, HighY), (EndX, HighY), (EndX, EndY)],
                [(StartX, StartY), (LeftX, StartY), (LeftX, HighY), (EndX, HighY), (EndX, EndY)],
                [(StartX, StartY), (RightX, StartY), (RightX, LowY), (EndX, LowY), (EndX, EndY)],
                [(StartX, StartY), (LeftX, StartY), (LeftX, LowY), (EndX, LowY), (EndX, EndY)]
            ];

            foreach (List<(double X, double Y)> Candidate in Candidates)
            {
                if (IsAcceptableSmoothRoute(Candidate, Obstacles, GroupWalls, RelaxedBounds))
                {
                    double Length = PolylineLength(Candidate);
                    if (Length < BestLength)
                    {
                        Best = Candidate;
                        BestLength = Length;
                    }
                }
                else
                {
                    AddBlockingFromPolyline(Blocking, Obstacles, Candidate);
                    AddBlockingFromPolyline(Blocking, GroupWalls, Candidate);
                }
            }

            if (Best.Count >= 2)
            {
                Points = Best;
                return true;
            }

            if (Blocking.Count == BlockingCountBefore)
            {
                break;
            }
        }

        if (Best.Count >= 2)
        {
            Points = Best;
            return true;
        }

        return false;
    }

    private static void AddBlocking(
        List<FlowchartLayoutNodeBounds> Blocking,
        List<FlowchartLayoutNodeBounds> Candidates,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        foreach (FlowchartLayoutNodeBounds Candidate in Candidates)
        {
            if (SegmentComesWithinClearance(StartX, StartY, EndX, EndY, [Candidate])
                || SegmentHugsGroupWall(StartX, StartY, EndX, EndY, Candidate))
            {
                AddUniqueBounds(Blocking, Candidate);
            }
        }
    }

    private static void AddBlockingFromPolyline(
        List<FlowchartLayoutNodeBounds> Blocking,
        List<FlowchartLayoutNodeBounds> Candidates,
        IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        foreach (FlowchartLayoutNodeBounds Candidate in Candidates)
        {
            if (PolylineComesWithinClearance(LayoutPoints, [Candidate])
                || PolylineHugsGroupBorders(LayoutPoints, [Candidate]))
            {
                AddUniqueBounds(Blocking, Candidate);
            }
        }
    }

    private static void AddUniqueBounds(
        List<FlowchartLayoutNodeBounds> Blocking,
        FlowchartLayoutNodeBounds Candidate)
    {
        foreach (FlowchartLayoutNodeBounds Existing in Blocking)
        {
            if (Math.Abs(Existing.CenterX - Candidate.CenterX) < 0.5
                && Math.Abs(Existing.CenterY - Candidate.CenterY) < 0.5
                && Math.Abs(Existing.Width - Candidate.Width) < 0.5
                && Math.Abs(Existing.Height - Candidate.Height) < 0.5)
            {
                return;
            }
        }

        Blocking.Add(Candidate);
    }

    private static bool IsAcceptableSmoothRoute(
        List<(double X, double Y)> Candidate,
        List<FlowchartLayoutNodeBounds> Obstacles,
        List<FlowchartLayoutNodeBounds> GroupWalls,
        (double Left, double Bottom, double Right, double Top) ContentBounds)
    {
        return !PolylineComesWithinClearance(Candidate, Obstacles)
            && !PolylineLeavesBounds(Candidate, ContentBounds)
            && !PolylineHugsGroupBorders(Candidate, GroupWalls)
            && !HasDuplicatedParallelSegments(Candidate)
            && !HasHookedBacktrack(Candidate);
    }

    private static bool ParallelSegmentsOverlap(
        (double X, double Y) LeftStart,
        (double X, double Y) LeftEnd,
        (double X, double Y) RightStart,
        (double X, double Y) RightEnd,
        double SeparationPx,
        double MinimumOverlapPx)
    {
        const double ALIGN_TOLERANCE_PX = 8.0;
        bool LeftVertical = Math.Abs(LeftEnd.X - LeftStart.X) <= ALIGN_TOLERANCE_PX
            && Math.Abs(LeftEnd.Y - LeftStart.Y) > MinimumOverlapPx;
        bool RightVertical = Math.Abs(RightEnd.X - RightStart.X) <= ALIGN_TOLERANCE_PX
            && Math.Abs(RightEnd.Y - RightStart.Y) > MinimumOverlapPx;
        if (LeftVertical && RightVertical)
        {
            double Overlap = Math.Min(Math.Max(LeftStart.Y, LeftEnd.Y), Math.Max(RightStart.Y, RightEnd.Y))
                - Math.Max(Math.Min(LeftStart.Y, LeftEnd.Y), Math.Min(RightStart.Y, RightEnd.Y));
            double LeftX = (LeftStart.X + LeftEnd.X) / 2.0;
            double RightX = (RightStart.X + RightEnd.X) / 2.0;
            return Overlap >= MinimumOverlapPx
                && Math.Abs(LeftX - RightX) < SeparationPx;
        }

        bool LeftHorizontal = Math.Abs(LeftEnd.Y - LeftStart.Y) <= ALIGN_TOLERANCE_PX
            && Math.Abs(LeftEnd.X - LeftStart.X) > MinimumOverlapPx;
        bool RightHorizontal = Math.Abs(RightEnd.Y - RightStart.Y) <= ALIGN_TOLERANCE_PX
            && Math.Abs(RightEnd.X - RightStart.X) > MinimumOverlapPx;
        if (LeftHorizontal && RightHorizontal)
        {
            double Overlap = Math.Min(Math.Max(LeftStart.X, LeftEnd.X), Math.Max(RightStart.X, RightEnd.X))
                - Math.Max(Math.Min(LeftStart.X, LeftEnd.X), Math.Min(RightStart.X, RightEnd.X));
            double LeftY = (LeftStart.Y + LeftEnd.Y) / 2.0;
            double RightY = (RightStart.Y + RightEnd.Y) / 2.0;
            return Overlap >= MinimumOverlapPx
                && Math.Abs(LeftY - RightY) < SeparationPx;
        }

        return false;
    }

    private static void OffsetOverlappingParallelRuns(
        List<(double X, double Y)> Left,
        List<(double X, double Y)> Right)
    {
        for (int LeftIndex = 0; LeftIndex < Left.Count - 1; LeftIndex++)
        {
            for (int RightIndex = 0; RightIndex < Right.Count - 1; RightIndex++)
            {
                if (!ParallelSegmentsOverlap(
                        Left[LeftIndex],
                        Left[LeftIndex + 1],
                        Right[RightIndex],
                        Right[RightIndex + 1],
                        PARALLEL_SEPARATION_PX,
                        GROUP_HUG_OVERLAP_PX))
                {
                    continue;
                }

                bool Vertical = Math.Abs(Left[LeftIndex + 1].X - Left[LeftIndex].X) <= 8.0;
                double Gap = PARALLEL_SEPARATION_PX;
                if (Vertical)
                {
                    double LeftX = (Left[LeftIndex].X + Left[LeftIndex + 1].X) / 2.0;
                    double RightX = (Right[RightIndex].X + Right[RightIndex + 1].X) / 2.0;
                    double Sign = LeftX <= RightX ? -1.0 : 1.0;
                    EnsureInteriorPoint(Left, LeftIndex, Vertical: true);
                    EnsureInteriorPoint(Right, RightIndex, Vertical: true);
                    ShiftRun(Left, LeftIndex, Sign * (Gap / 2.0), Vertical: true);
                    ShiftRun(Right, RightIndex, -Sign * (Gap / 2.0), Vertical: true);
                }
                else
                {
                    double LeftY = (Left[LeftIndex].Y + Left[LeftIndex + 1].Y) / 2.0;
                    double RightY = (Right[RightIndex].Y + Right[RightIndex + 1].Y) / 2.0;
                    double Sign = LeftY <= RightY ? -1.0 : 1.0;
                    EnsureInteriorPoint(Left, LeftIndex, Vertical: false);
                    EnsureInteriorPoint(Right, RightIndex, Vertical: false);
                    ShiftRun(Left, LeftIndex, Sign * (Gap / 2.0), Vertical: false);
                    ShiftRun(Right, RightIndex, -Sign * (Gap / 2.0), Vertical: false);
                }
            }
        }
    }

    private static void EnsureInteriorPoint(
        List<(double X, double Y)> Points,
        int SegmentIndex,
        bool Vertical)
    {
        if (Points.Count > 2)
        {
            return;
        }

        (double StartX, double StartY) = Points[SegmentIndex];
        (double EndX, double EndY) = Points[SegmentIndex + 1];
        Points.Insert(
            SegmentIndex + 1,
            ((StartX + EndX) / 2.0, (StartY + EndY) / 2.0));
        _ = Vertical;
    }

    private static void ShiftRun(
        List<(double X, double Y)> Points,
        int SegmentIndex,
        double Delta,
        bool Vertical)
    {
        if (SegmentIndex < 0 || SegmentIndex + 1 >= Points.Count)
        {
            return;
        }

        (double StartX, double StartY) = Points[SegmentIndex];
        (double EndX, double EndY) = Points[SegmentIndex + 1];
        double RunX = (StartX + EndX) / 2.0;
        double RunY = (StartY + EndY) / 2.0;
        double Low = Vertical ? Math.Min(StartY, EndY) : Math.Min(StartX, EndX);
        double High = Vertical ? Math.Max(StartY, EndY) : Math.Max(StartX, EndX);
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            bool OnRun = Vertical
                ? Math.Abs(Points[Index].X - RunX) <= 8.0
                    && Points[Index].Y >= Low - 1.0
                    && Points[Index].Y <= High + 1.0
                : Math.Abs(Points[Index].Y - RunY) <= 8.0
                    && Points[Index].X >= Low - 1.0
                    && Points[Index].X <= High + 1.0;
            if (OnRun)
            {
                Points[Index] = Vertical
                    ? (Points[Index].X + Delta, Points[Index].Y)
                    : (Points[Index].X, Points[Index].Y + Delta);
            }
        }
    }
}
