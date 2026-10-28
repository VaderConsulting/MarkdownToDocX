using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Routes loop edges in top-to-bottom and bottom-to-top flowcharts: a decision's loop back to an earlier node,
/// the decision's one remaining forward branch, and reciprocal pairs between stacked nodes.
/// </summary>
public static class FlowchartLoopBackRouteHelper
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Gap between the loop's return column and the outermost node it passes.
    /// </summary>
    private const double LOOP_CORRIDOR_CLEARANCE_PX = 20.0;

    /// <summary>
    /// Shortest straight run out of the diamond's side point before the loop turns.
    /// </summary>
    private const double MINIMUM_SIDE_STUB_PX = 24.0;

    /// <summary>
    /// Clearance kept between a reciprocal lane and the nearer side of either node.
    /// </summary>
    private const double RECIPROCAL_LANE_INSET_PX = 8.0;

    /// <summary>
    /// Routes a decision's loop back to an earlier node (EIP Dynamic Router, More routing? back to Determine next destination).
    /// The edge leaves the diamond's left or right point, runs out to a column clear of every node between the two rows,
    /// runs back along the flow to the target's middle row, and enters the middle of the target's facing side.
    /// The side faces away from the decision's forward branches. The guide points are axis-aligned; the caller renders them
    /// with rounded corners (<see cref="FlowchartEdgeRouteKind.RetryLoop"/>).
    /// </summary>
    /// <returns>True when the route applies and <paramref name="LayoutPoints"/> holds its guide points.</returns>
    public static bool TryResolveDecisionLoopBackRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];
        if (!IsVerticalFlow(Direction) || !IsDecisionNode(NodeById, Edge.SourceId) || string.Equals(Edge.SourceId, Edge.TargetId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds) || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        IReadOnlySet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        if (!BackEdges.Contains(Edge))
        {
            return false;
        }

        double FlowSign = Direction == FlowchartDirection.BottomToTop ? 1.0 : -1.0;
        double SourceUpstreamFace = SourceBounds.CenterY - (FlowSign * SourceBounds.Height / 2.0);
        double TargetDownstreamFace = TargetBounds.CenterY + (FlowSign * TargetBounds.Height / 2.0);
        if ((SourceUpstreamFace - TargetDownstreamFace) * FlowSign <= COLLINEAR_EPSILON_PX)
        {
            return false;
        }

        double Side = ResolveLoopSide(Edge, Contract, BoundsById, BackEdges, SourceBounds, TargetBounds);
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart);
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd);
        double StartX = SourceBounds.CenterX + (Side * ((SourceBounds.Width / 2.0) + SourceGapPx));
        double StartY = SourceBounds.CenterY;
        double EndX = TargetBounds.CenterX + (Side * ((TargetBounds.Width / 2.0) + TargetGapPx));
        double EndY = TargetBounds.CenterY;
        double SpanMinY = Math.Min(StartY, EndY);
        double SpanMaxY = Math.Max(StartY, EndY);

        double StubX = StartX + (Side * MINIMUM_SIDE_STUB_PX);
        double CorridorX = ResolveParallelLoopCorridorX(
            Edge,
            Contract,
            BoundsById,
            BackEdges,
            Side,
            StubX,
            SpanMinY,
            SpanMaxY);

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            if (string.Equals(Entry.Key, Edge.SourceId, StringComparison.Ordinal) || string.Equals(Entry.Key, Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (CrossesHorizontalRun(Entry.Value, StartX, StubX, StartY)
                || CrossesHorizontalRun(Entry.Value, CorridorX, CorridorX, StartY)
                || CrossesHorizontalRun(Entry.Value, EndX, CorridorX, EndY))
            {
                return false;
            }
        }

        LayoutPoints.Add((StartX, StartY));
        LayoutPoints.Add((StubX, StartY));
        LayoutPoints.Add((CorridorX, StartY));
        LayoutPoints.Add((CorridorX, EndY));
        LayoutPoints.Add((EndX, EndY));
        return true;
    }

    /// <summary>
    /// B15: longest loop-back track sits outermost; shorter loops use parallel interior corridors.
    /// </summary>
    private static double ResolveParallelLoopCorridorX(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlySet<EdgeContract> BackEdges,
        double Side,
        double StubX,
        double SpanMinY,
        double SpanMaxY)
    {
        double BaseCorridorX = StubX;
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (Top < SpanMinY || Bottom > SpanMaxY)
            {
                continue;
            }

            double Candidate = Bounds.CenterX + (Side * ((Bounds.Width / 2.0) + LOOP_CORRIDOR_CLEARANCE_PX));
            if ((Candidate - BaseCorridorX) * Side > 0.0)
            {
                BaseCorridorX = Candidate;
            }
        }

        List<EdgeContract> SameSideLoops = [];
        foreach (EdgeContract Candidate in BackEdges)
        {
            if (!BoundsById.TryGetValue(Candidate.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Candidate.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            double CandidateSide = ResolveLoopSide(Candidate, Contract, BoundsById, BackEdges, SourceBounds, TargetBounds);
            if (Math.Abs(CandidateSide - Side) > COLLINEAR_EPSILON_PX)
            {
                continue;
            }

            SameSideLoops.Add(Candidate);
        }

        if (SameSideLoops.Count <= 1)
        {
            return BaseCorridorX;
        }

        SameSideLoops.Sort((Left, Right) =>
        {
            if (!BoundsById.TryGetValue(Left.SourceId, out FlowchartLayoutNodeBounds LeftSource)
                || !BoundsById.TryGetValue(Left.TargetId, out FlowchartLayoutNodeBounds LeftTarget)
                || !BoundsById.TryGetValue(Right.SourceId, out FlowchartLayoutNodeBounds RightSource)
                || !BoundsById.TryGetValue(Right.TargetId, out FlowchartLayoutNodeBounds RightTarget))
            {
                return 0;
            }

            double LeftSpan = Math.Abs(LeftSource.CenterY - LeftTarget.CenterY);
            double RightSpan = Math.Abs(RightSource.CenterY - RightTarget.CenterY);
            int SpanCompare = RightSpan.CompareTo(LeftSpan);
            if (SpanCompare != 0)
            {
                return SpanCompare;
            }

            return Left.CreationIndex.CompareTo(Right.CreationIndex);
        });

        int LoopIndex = SameSideLoops.FindIndex(
            Candidate => string.Equals(Candidate.SourceId, Edge.SourceId, StringComparison.Ordinal)
                && string.Equals(Candidate.TargetId, Edge.TargetId, StringComparison.Ordinal));
        if (LoopIndex < 0)
        {
            return BaseCorridorX;
        }

        return BaseCorridorX + (Side * LOOP_CORRIDOR_CLEARANCE_PX * LoopIndex);
    }

    /// <summary>
    /// Routes a decision's only forward branch as a straight line from the diamond's downstream point when the
    /// target sits directly below (or above, for bottom-to-top) the diamond. The decision's loop-back edges do not count as branches.
    /// </summary>
    /// <returns>True when the route applies and <paramref name="LayoutPoints"/> holds the two end points.</returns>
    public static bool TryResolveSoleForwardDecisionBranch(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];
        if (!IsVerticalFlow(Direction) || !IsDecisionNode(NodeById, Edge.SourceId))
        {
            return false;
        }

        IReadOnlySet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        if (BackEdges.Count == 0 || BackEdges.Contains(Edge))
        {
            return false;
        }

        List<EdgeContract> Outgoing = GetVisibleOutgoing(Contract, Edge.SourceId);
        if (!Outgoing.Any(Candidate => BackEdges.Contains(Candidate)) || Outgoing.Count(Candidate => !BackEdges.Contains(Candidate)) != 1)
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds) || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (Math.Abs(SourceBounds.CenterX - TargetBounds.CenterX) > COLLINEAR_EPSILON_PX)
        {
            return false;
        }

        string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
        string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
        return FlowchartCentreSpineRouteBuilder.TryBuildRoute(SourceBounds, SourceShape, TargetBounds, TargetShape, Edge.MarkerStart, Edge.MarkerEnd, Direction, LayoutPoints);
    }

    /// <summary>
    /// Routes one edge of a reciprocal pair (A to B and B to A) between two stacked nodes that overlap across the flow.
    /// Each edge is a straight line in its own lane inside the shared width, so the two lines never meet.
    /// The first declared edge takes the lane on the right, matching the two-node pair in F104.
    /// </summary>
    /// <returns>True when the route applies and <paramref name="LayoutPoints"/> holds the two end points.</returns>
    public static bool TryResolveStackedReciprocalLane(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];
        if (!IsVerticalFlow(Direction) || string.Equals(Edge.SourceId, Edge.TargetId, StringComparison.Ordinal))
        {
            return false;
        }

        if (FlowchartReciprocalEdgeHelper.TryGetTbTwoNodeReciprocalPair(Contract, out EdgeContract _, out EdgeContract _))
        {
            return false;
        }

        if (IsDecisionNode(NodeById, Edge.SourceId) || IsDecisionNode(NodeById, Edge.TargetId))
        {
            return false;
        }

        EdgeContract? ReverseEdge = Contract.Edges.FirstOrDefault(Candidate => !IsInvisible(Candidate) && string.Equals(Candidate.SourceId, Edge.TargetId, StringComparison.Ordinal) && string.Equals(Candidate.TargetId, Edge.SourceId, StringComparison.Ordinal));
        if (ReverseEdge is null)
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds) || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        bool TargetBelow = TargetBounds.CenterY < SourceBounds.CenterY;
        double SourceFace = SourceBounds.CenterY + ((TargetBelow ? -1.0 : 1.0) * SourceBounds.Height / 2.0);
        double TargetFace = TargetBounds.CenterY + ((TargetBelow ? 1.0 : -1.0) * TargetBounds.Height / 2.0);
        if ((TargetBelow ? SourceFace - TargetFace : TargetFace - SourceFace) <= COLLINEAR_EPSILON_PX)
        {
            return false;
        }

        double SharedLeft = Math.Max(SourceBounds.CenterX - (SourceBounds.Width / 2.0), TargetBounds.CenterX - (TargetBounds.Width / 2.0));
        double SharedRight = Math.Min(SourceBounds.CenterX + (SourceBounds.Width / 2.0), TargetBounds.CenterX + (TargetBounds.Width / 2.0));
        double LaneOffset = FlowchartReciprocalEdgeHelper.RECIPROCAL_EDGE_LANE_OFFSET_PX;
        if (SharedRight - SharedLeft < (2.0 * (LaneOffset + RECIPROCAL_LANE_INSET_PX)))
        {
            return false;
        }

        bool IsFirstOfPair = Edge.CreationIndex < ReverseEdge.CreationIndex;
        double LaneX = ((SharedLeft + SharedRight) / 2.0) + (IsFirstOfPair ? LaneOffset : -LaneOffset);
        double StartY = SourceFace + ((TargetBelow ? -1.0 : 1.0) * FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart));
        double EndY = TargetFace + ((TargetBelow ? 1.0 : -1.0) * FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd));

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            if (string.Equals(Entry.Key, Edge.SourceId, StringComparison.Ordinal) || string.Equals(Entry.Key, Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Bounds = Entry.Value;
            bool CoversLane = Math.Abs(Bounds.CenterX - LaneX) < Bounds.Width / 2.0;
            bool BetweenRows = Bounds.CenterY - (Bounds.Height / 2.0) < Math.Max(StartY, EndY) && Bounds.CenterY + (Bounds.Height / 2.0) > Math.Min(StartY, EndY);
            if (CoversLane && BetweenRows)
            {
                return false;
            }
        }

        LayoutPoints.Add((LaneX, StartY));
        LayoutPoints.Add((LaneX, EndY));
        return true;
    }

    private static double ResolveLoopSide(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlySet<EdgeContract> BackEdges,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        double ForwardOffset = 0.0;
        foreach (EdgeContract Candidate in GetVisibleOutgoing(Contract, Edge.SourceId))
        {
            if (BackEdges.Contains(Candidate) || !BoundsById.TryGetValue(Candidate.TargetId, out FlowchartLayoutNodeBounds ForwardBounds))
            {
                continue;
            }

            ForwardOffset += ForwardBounds.CenterX - SourceBounds.CenterX;
        }

        if (ForwardOffset > COLLINEAR_EPSILON_PX)
        {
            return -1.0;
        }

        if (ForwardOffset < -COLLINEAR_EPSILON_PX)
        {
            return 1.0;
        }

        double TargetOffset = TargetBounds.CenterX - SourceBounds.CenterX;
        if (TargetOffset < -COLLINEAR_EPSILON_PX)
        {
            return -1.0;
        }

        return 1.0;
    }

    private static bool CrossesHorizontalRun(FlowchartLayoutNodeBounds Bounds, double FromX, double ToX, double RunY)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        return RunY > Bottom && RunY < Top && Math.Max(FromX, ToX) > Left && Math.Min(FromX, ToX) < Right;
    }

    private static List<EdgeContract> GetVisibleOutgoing(DiagramContract Contract, string SourceId)
    {
        return [.. Contract.Edges.Where(Candidate => !IsInvisible(Candidate) && string.Equals(Candidate.SourceId, SourceId, StringComparison.Ordinal))];
    }

    private static bool IsInvisible(EdgeContract Edge)
    {
        return string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVerticalFlow(FlowchartDirection Direction)
    {
        return Direction == FlowchartDirection.TopToBottom || Direction == FlowchartDirection.BottomToTop;
    }

    private static bool IsDecisionNode(IReadOnlyDictionary<string, NodeContract> NodeById, string NodeId)
    {
        if (!NodeById.TryGetValue(NodeId, out NodeContract? Node) || string.IsNullOrEmpty(Node.Shape))
        {
            return false;
        }

        string Shape = Node.Shape.Trim();
        return string.Equals(Shape, "diamond", StringComparison.OrdinalIgnoreCase) || string.Equals(Shape, "rhombus", StringComparison.OrdinalIgnoreCase);
    }
}
