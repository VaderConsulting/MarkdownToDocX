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
    /// Maximum allowed backtrack along the source-to-target axis before a route is treated as hooked.
    /// </summary>
    public const double HOOK_BACKTRACK_PX = 24.0;

    private const double NEAR_ALIGNED_SPAN_PX = 40.0;

    private const double FACE_SPAN_RATIO = 0.72;

    private const double SEGMENT_ENDPOINT_TOLERANCE_PX = 1.25;

    private const double COLINEAR_GUIDE_TOLERANCE_PX = 8.0;

    private const double CORNER_HOLD_PX = 28.0;

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

        if (ShouldSpreadSideFacePorts(Direction, LayoutPoints[0], SourceBounds)
            || ShouldSpreadSideFacePorts(Direction, LayoutPoints[LayoutPoints.Count - 1], TargetBounds))
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
        (double BoundsLeft, double BoundsBottom, double BoundsRight, double BoundsTop) ContentBounds =
            ComputeContentBounds(Contract, BoundsById);

        bool ChordClearOfClearance = !SegmentComesWithinClearance(
            LayoutPoints[0].X,
            LayoutPoints[0].Y,
            LayoutPoints[LayoutPoints.Count - 1].X,
            LayoutPoints[LayoutPoints.Count - 1].Y,
            Obstacles);
        bool NearAligned = IsNearAxisAligned(LayoutPoints[0], LayoutPoints[LayoutPoints.Count - 1]);
        bool Hooked = HasHookedBacktrack(LayoutPoints);
        bool HitsClearance = PolylineComesWithinClearance(LayoutPoints, Obstacles);
        bool LeavesBounds = PolylineLeavesBounds(LayoutPoints, ContentBounds);
        bool ColinearGuides = AllPointsNearChord(LayoutPoints, COLINEAR_GUIDE_TOLERANCE_PX);

        bool ExistingCorridorIsHealthy = RouteKind == FlowchartEdgeRouteKind.Corridor
            && !PolylineHitsObstacles(LayoutPoints, Obstacles)
            && !LeavesBounds
            && !Hooked;
        if (ExistingCorridorIsHealthy)
        {
            return true;
        }

        if (NearAligned && ChordClearOfClearance && (Hooked || (ColinearGuides && LayoutPoints.Count > 2)))
        {
            (double StartX, double StartY) = LayoutPoints[0];
            (double EndX, double EndY) = LayoutPoints[LayoutPoints.Count - 1];
            LayoutPoints.Clear();
            LayoutPoints.Add((StartX, StartY));
            LayoutPoints.Add((EndX, EndY));
            if (Hooked)
            {
                RouteKind = FlowchartEdgeRouteKind.Straight;
            }

            Hooked = false;
            HitsClearance = false;
            LeavesBounds = PolylineLeavesBounds(LayoutPoints, ContentBounds);
        }

        if (HitsClearance || LeavesBounds || Hooked)
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
                List<(double X, double Y)> HeldChannel = DensifyOrthogonalChannel(ChannelPoints);
                LayoutPoints.Clear();
                LayoutPoints.AddRange(HeldChannel);
                RouteKind = HeldChannel.Count == 2
                    ? FlowchartEdgeRouteKind.Straight
                    : FlowchartEdgeRouteKind.Corridor;
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
        return HasHookedBacktrack(LayoutPoints) || HasDegenerateGap(LayoutPoints);
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

    private static bool ShouldSpreadSideFacePorts(
        FlowchartDirection Direction,
        (double X, double Y) Attach,
        FlowchartLayoutNodeBounds Bounds)
    {
        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            Bounds,
            Attach.X,
            Attach.Y);
        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        if (VerticalFlow)
        {
            return Face == FlowchartEdgeAttachFace.Left || Face == FlowchartEdgeAttachFace.Right;
        }

        return Face == FlowchartEdgeAttachFace.Top || Face == FlowchartEdgeAttachFace.Bottom;
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

        List<(string OtherId, double SortKey)> Siblings = [];
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string SelfId = IsSource ? Candidate.SourceId : Candidate.TargetId;
            string OtherId = IsSource ? Candidate.TargetId : Candidate.SourceId;
            if (!string.Equals(SelfId, IsSource ? Edge.SourceId : Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(OtherId, out FlowchartLayoutNodeBounds OtherBounds))
            {
                continue;
            }

            if (!OtherNodeLiesOnFaceSide(SelfBounds, OtherBounds, Face))
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

    private static bool AllPointsNearChord(IReadOnlyList<(double X, double Y)> LayoutPoints, double TolerancePx)
    {
        if (LayoutPoints.Count < 2)
        {
            return true;
        }

        double StartX = LayoutPoints[0].X;
        double StartY = LayoutPoints[0].Y;
        double EndX = LayoutPoints[LayoutPoints.Count - 1].X;
        double EndY = LayoutPoints[LayoutPoints.Count - 1].Y;
        for (int Index = 0; Index < LayoutPoints.Count; Index++)
        {
            if (DistanceFromPointToSegment(
                    LayoutPoints[Index].X,
                    LayoutPoints[Index].Y,
                    StartX,
                    StartY,
                    EndX,
                    EndY) > TolerancePx)
            {
                return false;
            }
        }

        return true;
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
        for (int Index = 0; Index < LayoutPoints.Count; Index++)
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

    private static bool IsNearAxisAligned((double X, double Y) Start, (double X, double Y) End)
    {
        return Math.Abs(End.X - Start.X) <= NEAR_ALIGNED_SPAN_PX
            || Math.Abs(End.Y - Start.Y) <= NEAR_ALIGNED_SPAN_PX;
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
}
