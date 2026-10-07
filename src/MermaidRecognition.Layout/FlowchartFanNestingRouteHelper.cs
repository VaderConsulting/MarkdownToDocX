using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Distance-ordered nested fan-out and fan-in routing (B21).
/// Furthest branches use the outermost track and port; nearest branches use the innermost.
/// Routes nest without crossings or hop arcs.
/// </summary>
public static class FlowchartFanNestingRouteHelper
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double COLUMN_ALIGNMENT_TOLERANCE_PX = 8.0;

    private const double SLOT_INSET_PX = 8.0;

    private const double CORRIDOR_MARGIN_PX = 16.0;

    private const int MINIMUM_FAN_BRANCH_COUNT = 2;

    /// <summary>
    /// Rewrites edge routes for every qualifying fan hub using nested parallel tracks.
    /// </summary>
    internal static void Apply(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        IReadOnlyList<DiagramGridLayout.GridNode> Placed,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        if (Contract == null || Layout == null || Layout.EdgeRoutes.Count == 0)
        {
            return;
        }

        Dictionary<string, DiagramGridLayout.GridNode> PlacedById = new Dictionary<string, DiagramGridLayout.GridNode>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            PlacedById[Node.Id] = Node;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByEdgeKey = new Dictionary<string, FlowchartLayoutEdgeRoute>(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            RouteByEdgeKey[Route.SourceId + "->" + Route.TargetId] = Route;
        }

        List<FanBranchAssignment> Assignments = [];
        foreach (string HubId in CollectFanHubIds(Contract))
        {
            if (!BoundsById.TryGetValue(HubId, out FlowchartLayoutNodeBounds HubBounds))
            {
                continue;
            }

            Nodes.TryGetValue(HubId, out NodeContract? HubNode);
            string? HubShape = HubNode?.Shape;
            if (IsDecisionShape(HubShape))
            {
                continue;
            }

            if (IsManyToManyHub(Contract, HubId, PlacedById))
            {
                continue;
            }

            List<EdgeContract> Outgoing = CollectVisibleEdgesFromSource(Contract, HubId);
            List<EdgeContract> Incoming = CollectVisibleEdgesToTarget(Contract, HubId);
            if (Outgoing.Count >= MINIMUM_FAN_BRANCH_COUNT
                && TryBuildFanOutAssignments(
                    Contract,
                    HubId,
                    HubBounds,
                    Outgoing,
                    BoundsById,
                    Direction,
                    true,
                    Assignments))
            {
                continue;
            }

            if (Incoming.Count >= MINIMUM_FAN_BRANCH_COUNT)
            {
                TryBuildFanOutAssignments(
                    Contract,
                    HubId,
                    HubBounds,
                    Incoming,
                    BoundsById,
                    Direction,
                    false,
                    Assignments);
            }
        }

        foreach (FanBranchAssignment Assignment in Assignments)
        {
            if (!RouteByEdgeKey.TryGetValue(Assignment.EdgeKey, out FlowchartLayoutEdgeRoute? Route))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Assignment.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Assignment.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            Nodes.TryGetValue(Assignment.SourceId, out NodeContract? SourceNode);
            Nodes.TryGetValue(Assignment.TargetId, out NodeContract? TargetNode);
            string? SourceShape = SourceNode?.Shape;
            string? TargetShape = TargetNode?.Shape;

            List<(double X, double Y)> Path = BuildNestedRoute(
                Assignment,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                Direction);

            if (Path.Count < 2)
            {
                throw new InvalidOperationException(
                    "Fan nesting could not build a clean route for "
                    + Assignment.EdgeKey
                    + ". Report this as a layout bug.");
            }

            Route.Points.Clear();
            foreach ((double X, double Y) in Path)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(X),
                    Y = Math.Round(Y)
                });
            }

            Route.ExitSide = Assignment.ExitSide;
            Route.EntrySide = Assignment.EntrySide;
        }

        RepairCrossingsBySwappingTracks(Assignments, BoundsById, Direction, RouteByEdgeKey, Nodes);
    }

    private static List<(double X, double Y)> BuildNestedRoute(
        FanBranchAssignment Assignment,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        FlowchartDirection Direction)
    {
        List<(double X, double Y)> Points = [];
        if (Assignment.HubIsSource)
        {
            BuildHubFaceFanRoute(
                Points,
                Assignment,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                HubIsSource: true,
                Direction);
            return Points;
        }

        BuildColumnFanInRoute(
            Points,
            Assignment,
            SourceBounds,
            SourceShape,
            TargetBounds,
            TargetShape,
            Direction);
        return Points;
    }

    private static void BuildHubFaceFanRoute(
        List<(double X, double Y)> Points,
        FanBranchAssignment Assignment,
        FlowchartLayoutNodeBounds HubBounds,
        string? HubShape,
        FlowchartLayoutNodeBounds BranchBounds,
        string? BranchShape,
        bool HubIsSource,
        FlowchartDirection Direction)
    {
        double HubMarkerGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(EdgeEndpointMarker.None);
        double BranchMarkerGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(EdgeEndpointMarker.None);
        double HubHalfWidth = HubBounds.Width / 2.0;
        double HubHalfHeight = HubBounds.Height / 2.0;
        double HubTop = HubBounds.CenterY + HubHalfHeight;
        double HubBottom = HubBounds.CenterY - HubHalfHeight;
        bool UsesTopFace = Assignment.UsesPositiveSide;
        bool ColumnIsRight = Assignment.ColumnIsFlowForward;
        double SlotX = ResolveFaceSlotX(HubBounds, Assignment.SlotIndex, Assignment.SlotCount, ColumnIsRight);
        double FaceOutwardY = UsesTopFace ? 1.0 : -1.0;
        double HubBoundaryY = UsesTopFace ? HubTop + HubMarkerGap : HubBottom - HubMarkerGap;
        double BranchOutwardX = ColumnIsRight ? -1.0 : 1.0;
        double BranchBoundaryX = ColumnIsRight
            ? BranchBounds.CenterX - (BranchBounds.Width / 2.0) - BranchMarkerGap
            : BranchBounds.CenterX + (BranchBounds.Width / 2.0) + BranchMarkerGap;

        double HubStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(HubBounds);
        double BranchStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(BranchBounds);
        (double HubBoundaryX, double HubBoundaryYPoint) = (SlotX, HubBoundaryY);
        (double HubStubX, double HubStubY) = (SlotX, HubBoundaryY + (FaceOutwardY * HubStubLength));
        (double BranchBoundaryXPoint, double BranchBoundaryY) = (BranchBoundaryX, BranchBounds.CenterY);
        (double BranchStubX, double BranchStubY) = (
            BranchBoundaryX + (BranchOutwardX * BranchStubLength),
            BranchBounds.CenterY);

        if (HubIsSource)
        {
            Points.Add((HubBoundaryX, HubBoundaryYPoint));
            Points.Add((HubStubX, HubStubY));
            Points.Add((BranchStubX, BranchStubY));
            Points.Add((BranchBoundaryXPoint, BranchBoundaryY));
            return;
        }

        Points.Add((BranchBoundaryXPoint, BranchBoundaryY));
        Points.Add((BranchStubX, BranchStubY));
        Points.Add((HubStubX, HubStubY));
        Points.Add((HubBoundaryX, HubBoundaryYPoint));
    }

    private static void BuildColumnFanInRoute(
        List<(double X, double Y)> Points,
        FanBranchAssignment Assignment,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        FlowchartDirection Direction)
    {
        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        double SourceGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(EdgeEndpointMarker.None);
        double TargetGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(EdgeEndpointMarker.None);
        double SourceHalfWidth = SourceBounds.Width / 2.0;
        double TargetHalfWidth = TargetBounds.Width / 2.0;
        double TargetHalfHeight = TargetBounds.Height / 2.0;

        double SourceX = RightToLeft
            ? SourceBounds.CenterX - SourceHalfWidth - SourceGap
            : SourceBounds.CenterX + SourceHalfWidth + SourceGap;
        double SourceY = SourceBounds.CenterY;
        double TargetPortY = Assignment.TargetPortY;
        double TargetX = RightToLeft
            ? TargetBounds.CenterX + TargetHalfWidth + TargetGap
            : TargetBounds.CenterX - TargetHalfWidth - TargetGap;

        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double TargetStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TargetBounds);
        double SourceStubX = RightToLeft ? SourceX - SourceStubLength : SourceX + SourceStubLength;
        double TargetStubX = RightToLeft ? TargetX + TargetStubLength : TargetX - TargetStubLength;
        double CorridorX = Assignment.CorridorCoordinate;

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceY));
        if (Math.Abs(CorridorX - SourceStubX) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((CorridorX, SourceY));
        }

        if (Math.Abs(TargetPortY - SourceY) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((CorridorX, TargetPortY));
        }

        if (Math.Abs(TargetStubX - CorridorX) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, TargetPortY));
        }

        Points.Add((TargetX, TargetPortY));
    }

    private static bool TryBuildFanOutAssignments(
        DiagramContract Contract,
        string HubId,
        FlowchartLayoutNodeBounds HubBounds,
        List<EdgeContract> HubEdges,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        bool HubIsSource,
        List<FanBranchAssignment> Assignments)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (!HorizontalFlow)
        {
            return TryBuildVerticalFanAssignments(
                Contract,
                HubId,
                HubBounds,
                HubEdges,
                BoundsById,
                Direction,
                HubIsSource,
                Assignments);
        }

        List<BranchEdge> Branches = [];
        foreach (EdgeContract HubEdge in HubEdges)
        {
            string OtherId = HubIsSource ? HubEdge.TargetId : HubEdge.SourceId;
            if (!BoundsById.TryGetValue(OtherId, out FlowchartLayoutNodeBounds OtherBounds))
            {
                return false;
            }

            Branches.Add(new BranchEdge
            {
                Edge = HubEdge,
                OtherBounds = OtherBounds
            });
        }

        if (Branches.Count < MINIMUM_FAN_BRANCH_COUNT)
        {
            return false;
        }

        bool FlowForward = Direction == FlowchartDirection.LeftToRight;
        bool ColumnIsFlowForward = FlowForward;
        if (HubIsSource)
        {
            ColumnIsFlowForward = FlowForward;
        }
        else
        {
            ColumnIsFlowForward = !FlowForward;
        }

        double ReferenceColumnX = Branches[0].OtherBounds.CenterX;
        foreach (BranchEdge Branch in Branches)
        {
            if (Math.Abs(Branch.OtherBounds.CenterX - ReferenceColumnX) > COLUMN_ALIGNMENT_TOLERANCE_PX)
            {
                if (HubIsSource)
                {
                    return TryBuildDistributedHorizontalFanAssignments(
                        HubId,
                        HubBounds,
                        Branches,
                        BoundsById,
                        Direction,
                        HubIsSource,
                        Assignments);
                }

                return false;
            }

            bool OnExpectedSide = ColumnIsFlowForward
                ? Branch.OtherBounds.CenterX > HubBounds.CenterX + COLLINEAR_EPSILON_PX
                : Branch.OtherBounds.CenterX < HubBounds.CenterX - COLLINEAR_EPSILON_PX;
            if (!OnExpectedSide)
            {
                return false;
            }
        }

        if (HubIsSource)
        {
            return TryBuildFaceFanAssignments(
                HubId,
                HubBounds,
                Branches,
                ColumnIsFlowForward,
                HubIsSource,
                Assignments);
        }

        return TryBuildColumnFanInAssignments(
            HubId,
            HubBounds,
            Branches,
            ColumnIsFlowForward,
            Assignments);
    }

    private static bool TryBuildFaceFanAssignments(
        string HubId,
        FlowchartLayoutNodeBounds HubBounds,
        List<BranchEdge> Branches,
        bool ColumnIsFlowForward,
        bool HubIsSource,
        List<FanBranchAssignment> Assignments)
    {
        double HubTop = HubBounds.CenterY + (HubBounds.Height / 2.0);
        double HubBottom = HubBounds.CenterY - (HubBounds.Height / 2.0);
        List<BranchEdge> Above = [];
        List<BranchEdge> Below = [];
        foreach (BranchEdge Branch in Branches)
        {
            if (Branch.OtherBounds.CenterY > HubTop)
            {
                Above.Add(Branch);
            }
            else if (Branch.OtherBounds.CenterY < HubBottom)
            {
                Below.Add(Branch);
            }
            else
            {
                return false;
            }
        }

        if (Above.Count > 0)
        {
            AppendFaceGroupAssignments(HubId, HubBounds, Above, true, ColumnIsFlowForward, HubIsSource, Assignments);
        }

        if (Below.Count > 0)
        {
            AppendFaceGroupAssignments(HubId, HubBounds, Below, false, ColumnIsFlowForward, HubIsSource, Assignments);
        }

        return Assignments.Count > 0;
    }

    private static void AppendFaceGroupAssignments(
        string HubId,
        FlowchartLayoutNodeBounds HubBounds,
        List<BranchEdge> Group,
        bool UsesPositiveSide,
        bool ColumnIsFlowForward,
        bool HubIsSource,
        List<FanBranchAssignment> Assignments)
    {
        Group.Sort(delegate(BranchEdge Left, BranchEdge Right)
        {
            double LeftDistance = Math.Abs(Left.OtherBounds.CenterY - HubBounds.CenterY);
            double RightDistance = Math.Abs(Right.OtherBounds.CenterY - HubBounds.CenterY);
            int DistanceCompare = RightDistance.CompareTo(LeftDistance);
            if (DistanceCompare != 0)
            {
                return DistanceCompare;
            }

            return Left.Edge.CreationIndex.CompareTo(Right.Edge.CreationIndex);
        });

        for (int Index = 0; Index < Group.Count; Index++)
        {
            BranchEdge Branch = Group[Index];
            string SourceId = HubIsSource ? HubId : Branch.Edge.SourceId;
            string TargetId = HubIsSource ? Branch.Edge.TargetId : HubId;
            Assignments.Add(new FanBranchAssignment
            {
                EdgeKey = SourceId + "->" + TargetId,
                SourceId = SourceId,
                TargetId = TargetId,
                HubIsSource = HubIsSource,
                SlotIndex = Index,
                SlotCount = Group.Count,
                UsesPositiveSide = UsesPositiveSide,
                ColumnIsFlowForward = ColumnIsFlowForward,
                ExitSide = ResolveHorizontalExitSide(HubIsSource, UsesPositiveSide, ColumnIsFlowForward),
                EntrySide = ResolveHorizontalEntrySide(HubIsSource, UsesPositiveSide, ColumnIsFlowForward)
            });
        }
    }

    private static bool TryBuildColumnFanInAssignments(
        string HubId,
        FlowchartLayoutNodeBounds HubBounds,
        List<BranchEdge> Branches,
        bool ColumnIsFlowForward,
        List<FanBranchAssignment> Assignments)
    {
        double HubTop = HubBounds.CenterY + (HubBounds.Height / 2.0);
        double HubBottom = HubBounds.CenterY - (HubBounds.Height / 2.0);
        List<BranchEdge> Above = [];
        List<BranchEdge> Below = [];
        foreach (BranchEdge Branch in Branches)
        {
            if (Branch.OtherBounds.CenterY > HubTop)
            {
                Above.Add(Branch);
            }
            else if (Branch.OtherBounds.CenterY < HubBottom)
            {
                Below.Add(Branch);
            }
            else
            {
                return false;
            }
        }

        double MaxRightEdge = double.NegativeInfinity;
        foreach (BranchEdge Branch in Branches)
        {
            MaxRightEdge = Math.Max(MaxRightEdge, Branch.OtherBounds.CenterX + (Branch.OtherBounds.Width / 2.0));
        }

        double BaseCorridorX = MaxRightEdge + CORRIDOR_MARGIN_PX;
        int BelowTrackSpan = Below.Count > 0 ? Below.Count * DiagramGridMetrics.GRID_UNIT_PX : 0;
        int AboveBaseOffset = Below.Count > 0 ? BelowTrackSpan + DiagramGridMetrics.GRID_UNIT_PX : 0;
        if (Above.Count > 0)
        {
            AppendColumnFanInGroupAssignments(
                HubId,
                HubBounds,
                Above,
                true,
                BaseCorridorX + AboveBaseOffset,
                Assignments);
        }

        if (Below.Count > 0)
        {
            AppendColumnFanInGroupAssignments(HubId, HubBounds, Below, false, BaseCorridorX, Assignments);
        }

        return Assignments.Count > 0;
    }

    private static void AppendColumnFanInGroupAssignments(
        string HubId,
        FlowchartLayoutNodeBounds HubBounds,
        List<BranchEdge> Group,
        bool UsesPositiveSide,
        double BaseCorridorX,
        List<FanBranchAssignment> Assignments)
    {
        Group.Sort(delegate(BranchEdge Left, BranchEdge Right)
        {
            double LeftDistance = Math.Abs(Left.OtherBounds.CenterY - HubBounds.CenterY);
            double RightDistance = Math.Abs(Right.OtherBounds.CenterY - HubBounds.CenterY);
            int DistanceCompare = RightDistance.CompareTo(LeftDistance);
            if (DistanceCompare != 0)
            {
                return DistanceCompare;
            }

            return Left.Edge.CreationIndex.CompareTo(Right.Edge.CreationIndex);
        });

        double HubHalfHeight = HubBounds.Height / 2.0;
        double HubTop = HubBounds.CenterY + HubHalfHeight;
        double HubBottom = HubBounds.CenterY - HubHalfHeight;
        double HubCentre = HubBounds.CenterY;
        for (int Index = 0; Index < Group.Count; Index++)
        {
            BranchEdge Branch = Group[Index];
            int OutermostOffset = (Group.Count - 1 - Index) * DiagramGridMetrics.GRID_UNIT_PX;
            double TargetPortY;
            if (UsesPositiveSide)
            {
                double Span = HubTop - HubCentre;
                TargetPortY = ResolveHalfFaceSlotY(HubTop, Span, Index, Group.Count, FromTop: true);
            }
            else
            {
                double Span = HubCentre - HubBottom;
                TargetPortY = ResolveHalfFaceSlotY(HubBottom, Span, Index, Group.Count, FromTop: false);
            }

            Assignments.Add(new FanBranchAssignment
            {
                EdgeKey = Branch.Edge.SourceId + "->" + HubId,
                SourceId = Branch.Edge.SourceId,
                TargetId = HubId,
                HubIsSource = false,
                SlotIndex = Index,
                SlotCount = Group.Count,
                UsesPositiveSide = UsesPositiveSide,
                ColumnIsFlowForward = true,
                CorridorCoordinate = BaseCorridorX + OutermostOffset,
                TargetPortY = TargetPortY,
                ExitSide = "right",
                EntrySide = "left"
            });
        }
    }

    private static bool TryBuildDistributedHorizontalFanAssignments(
        string HubId,
        FlowchartLayoutNodeBounds HubBounds,
        List<BranchEdge> Branches,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        bool HubIsSource,
        List<FanBranchAssignment> Assignments)
    {
        Branches.Sort(delegate(BranchEdge Left, BranchEdge Right)
        {
            double LeftDistance = Math.Abs(Left.OtherBounds.CenterY - HubBounds.CenterY);
            double RightDistance = Math.Abs(Right.OtherBounds.CenterY - HubBounds.CenterY);
            int DistanceCompare = RightDistance.CompareTo(LeftDistance);
            if (DistanceCompare != 0)
            {
                return DistanceCompare;
            }

            return Left.Edge.CreationIndex.CompareTo(Right.Edge.CreationIndex);
        });

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        for (int Index = 0; Index < Branches.Count; Index++)
        {
            BranchEdge Branch = Branches[Index];
            string SourceId = HubIsSource ? HubId : Branch.Edge.SourceId;
            string TargetId = HubIsSource ? Branch.Edge.TargetId : HubId;
            bool UsesPositiveSide = Branch.OtherBounds.CenterY > HubBounds.CenterY;
            Assignments.Add(new FanBranchAssignment
            {
                EdgeKey = SourceId + "->" + TargetId,
                SourceId = SourceId,
                TargetId = TargetId,
                HubIsSource = HubIsSource,
                SlotIndex = Index,
                SlotCount = Branches.Count,
                UsesPositiveSide = UsesPositiveSide,
                ColumnIsFlowForward = !RightToLeft,
                ExitSide = HubIsSource
                    ? (RightToLeft ? "left" : "right")
                    : (RightToLeft ? "right" : "left"),
                EntrySide = HubIsSource
                    ? (RightToLeft ? "right" : "left")
                    : (RightToLeft ? "left" : "right")
            });
        }

        return true;
    }

    private static bool TryBuildVerticalFanAssignments(
        DiagramContract Contract,
        string HubId,
        FlowchartLayoutNodeBounds HubBounds,
        List<EdgeContract> HubEdges,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        bool HubIsSource,
        List<FanBranchAssignment> Assignments)
    {
        List<BranchEdge> Branches = [];
        foreach (EdgeContract HubEdge in HubEdges)
        {
            string OtherId = HubIsSource ? HubEdge.TargetId : HubEdge.SourceId;
            if (!BoundsById.TryGetValue(OtherId, out FlowchartLayoutNodeBounds OtherBounds))
            {
                return false;
            }

            Branches.Add(new BranchEdge
            {
                Edge = HubEdge,
                OtherBounds = OtherBounds
            });
        }

        Branches.Sort(delegate(BranchEdge Left, BranchEdge Right)
        {
            double LeftDistance = Math.Abs(Left.OtherBounds.CenterX - HubBounds.CenterX);
            double RightDistance = Math.Abs(Right.OtherBounds.CenterX - HubBounds.CenterX);
            int DistanceCompare = RightDistance.CompareTo(LeftDistance);
            if (DistanceCompare != 0)
            {
                return DistanceCompare;
            }

            return Left.Edge.CreationIndex.CompareTo(Right.Edge.CreationIndex);
        });

        for (int Index = 0; Index < Branches.Count; Index++)
        {
            BranchEdge Branch = Branches[Index];
            string SourceId = HubIsSource ? HubId : Branch.Edge.SourceId;
            string TargetId = HubIsSource ? Branch.Edge.TargetId : HubId;
            bool UsesPositiveSide = Branch.OtherBounds.CenterX > HubBounds.CenterX;
            Assignments.Add(new FanBranchAssignment
            {
                EdgeKey = SourceId + "->" + TargetId,
                SourceId = SourceId,
                TargetId = TargetId,
                HubIsSource = HubIsSource,
                SlotIndex = Index,
                SlotCount = Branches.Count,
                UsesPositiveSide = UsesPositiveSide,
                ColumnIsFlowForward = Direction == FlowchartDirection.TopToBottom,
                ExitSide = HubIsSource ? "bottom" : "top",
                EntrySide = HubIsSource ? "top" : "bottom"
            });
        }

        return Branches.Count >= MINIMUM_FAN_BRANCH_COUNT;
    }

    private static void RepairCrossingsBySwappingTracks(
        List<FanBranchAssignment> Assignments,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByEdgeKey,
        IReadOnlyDictionary<string, NodeContract> Nodes)
    {
        Dictionary<string, List<FanBranchAssignment>> ByHub = new Dictionary<string, List<FanBranchAssignment>>(StringComparer.Ordinal);
        foreach (FanBranchAssignment Assignment in Assignments)
        {
            string HubKey = Assignment.HubIsSource ? Assignment.SourceId : Assignment.TargetId;
            if (!ByHub.TryGetValue(HubKey, out List<FanBranchAssignment>? Group))
            {
                Group = [];
                ByHub[HubKey] = Group;
            }

            Group.Add(Assignment);
        }

        foreach (KeyValuePair<string, List<FanBranchAssignment>> Pair in ByHub)
        {
            List<FanBranchAssignment> Group = Pair.Value;
            if (Group.Count < 2)
            {
                continue;
            }

            bool Improved = true;
            while (Improved)
            {
                Improved = false;
                for (int LeftIndex = 0; LeftIndex < Group.Count - 1; LeftIndex++)
                {
                    for (int RightIndex = LeftIndex + 1; RightIndex < Group.Count; RightIndex++)
                    {
                        FanBranchAssignment Left = Group[LeftIndex];
                        FanBranchAssignment Right = Group[RightIndex];
                        if (!TryCountCrossingBetween(
                                Left,
                                Right,
                                BoundsById,
                                Direction,
                                RouteByEdgeKey,
                                Nodes,
                                out int CrossingCount))
                        {
                            continue;
                        }

                        if (CrossingCount == 0)
                        {
                            continue;
                        }

                        if (!TryCountCrossingAfterSwap(
                                Left,
                                Right,
                                BoundsById,
                                Direction,
                                RouteByEdgeKey,
                                Nodes,
                                out int SwappedCrossings))
                        {
                            continue;
                        }

                        if (SwappedCrossings >= CrossingCount)
                        {
                            continue;
                        }

                        SwapSlots(Left, Right);
                        RebuildAssignmentRoute(Left, BoundsById, Direction, RouteByEdgeKey, Nodes);
                        RebuildAssignmentRoute(Right, BoundsById, Direction, RouteByEdgeKey, Nodes);
                        Improved = true;
                    }
                }
            }
        }
    }

    private static void SwapSlots(FanBranchAssignment Left, FanBranchAssignment Right)
    {
        int LeftSlot = Left.SlotIndex;
        int RightSlot = Right.SlotIndex;
        Left.SlotIndex = RightSlot;
        Right.SlotIndex = LeftSlot;
        double LeftCorridor = Left.CorridorCoordinate;
        double RightCorridor = Right.CorridorCoordinate;
        Left.CorridorCoordinate = RightCorridor;
        Right.CorridorCoordinate = LeftCorridor;
    }

    private static void RebuildAssignmentRoute(
        FanBranchAssignment Assignment,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByEdgeKey,
        IReadOnlyDictionary<string, NodeContract> Nodes)
    {
        if (!RouteByEdgeKey.TryGetValue(Assignment.EdgeKey, out FlowchartLayoutEdgeRoute? Route)
            || !BoundsById.TryGetValue(Assignment.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Assignment.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return;
        }

        Nodes.TryGetValue(Assignment.SourceId, out NodeContract? SourceNode);
        Nodes.TryGetValue(Assignment.TargetId, out NodeContract? TargetNode);
        List<(double X, double Y)> Path = BuildNestedRoute(
            Assignment,
            SourceBounds,
            SourceNode?.Shape,
            TargetBounds,
            TargetNode?.Shape,
            Direction);
        Route.Points.Clear();
        foreach ((double X, double Y) in Path)
        {
            Route.Points.Add(new FlowchartLayoutPoint
            {
                X = Math.Round(X),
                Y = Math.Round(Y)
            });
        }
    }

    private static bool TryCountCrossingBetween(
        FanBranchAssignment Left,
        FanBranchAssignment Right,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByEdgeKey,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        out int CrossingCount)
    {
        CrossingCount = 0;
        if (!TryReadRoutePoints(Left, RouteByEdgeKey, out List<(double X, double Y)> LeftPoints)
            || !TryReadRoutePoints(Right, RouteByEdgeKey, out List<(double X, double Y)> RightPoints))
        {
            return false;
        }

        if (SegmentsCrossPolylines(LeftPoints, RightPoints))
        {
            CrossingCount = 1;
        }

        return true;
    }

    private static bool TryCountCrossingAfterSwap(
        FanBranchAssignment Left,
        FanBranchAssignment Right,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByEdgeKey,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        out int CrossingCount)
    {
        FanBranchAssignment LeftCopy = Left.Clone();
        FanBranchAssignment RightCopy = Right.Clone();
        SwapSlots(LeftCopy, RightCopy);
        if (!BoundsById.TryGetValue(LeftCopy.SourceId, out FlowchartLayoutNodeBounds LeftSource)
            || !BoundsById.TryGetValue(LeftCopy.TargetId, out FlowchartLayoutNodeBounds LeftTarget)
            || !BoundsById.TryGetValue(RightCopy.SourceId, out FlowchartLayoutNodeBounds RightSource)
            || !BoundsById.TryGetValue(RightCopy.TargetId, out FlowchartLayoutNodeBounds RightTarget))
        {
            CrossingCount = int.MaxValue;
            return false;
        }

        Nodes.TryGetValue(LeftCopy.SourceId, out NodeContract? LeftSourceNode);
        Nodes.TryGetValue(LeftCopy.TargetId, out NodeContract? LeftTargetNode);
        Nodes.TryGetValue(RightCopy.SourceId, out NodeContract? RightSourceNode);
        Nodes.TryGetValue(RightCopy.TargetId, out NodeContract? RightTargetNode);
        List<(double X, double Y)> LeftPoints = BuildNestedRoute(
            LeftCopy,
            LeftSource,
            LeftSourceNode?.Shape,
            LeftTarget,
            LeftTargetNode?.Shape,
            Direction);
        List<(double X, double Y)> RightPoints = BuildNestedRoute(
            RightCopy,
            RightSource,
            RightSourceNode?.Shape,
            RightTarget,
            RightTargetNode?.Shape,
            Direction);
        CrossingCount = SegmentsCrossPolylines(LeftPoints, RightPoints) ? 1 : 0;
        return true;
    }

    private static bool TryReadRoutePoints(
        FanBranchAssignment Assignment,
        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByEdgeKey,
        out List<(double X, double Y)> Points)
    {
        Points = [];
        if (!RouteByEdgeKey.TryGetValue(Assignment.EdgeKey, out FlowchartLayoutEdgeRoute? Route))
        {
            return false;
        }

        foreach (FlowchartLayoutPoint Point in Route.Points)
        {
            Points.Add((Point.X, Point.Y));
        }

        return Points.Count >= 2;
    }

    private static bool SegmentsCrossPolylines(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                if (SegmentsCross(
                    Left[LeftIndex - 1].X,
                    Left[LeftIndex - 1].Y,
                    Left[LeftIndex].X,
                    Left[LeftIndex].Y,
                    Right[RightIndex - 1].X,
                    Right[RightIndex - 1].Y,
                    Right[RightIndex].X,
                    Right[RightIndex].Y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsCross(
        double Ax1,
        double Ay1,
        double Ax2,
        double Ay2,
        double Bx1,
        double By1,
        double Bx2,
        double By2)
    {
        double Denominator = ((Bx2 - Bx1) * (Ay2 - Ay1)) - ((By2 - By1) * (Ax2 - Ax1));
        if (Math.Abs(Denominator) < 1e-9)
        {
            return false;
        }

        double Ta = (((Bx1 - Ax1) * (Ay2 - Ay1)) - ((By1 - Ay1) * (Ax2 - Ax1))) / Denominator;
        double Tb = (((Bx1 - Ax1) * (By2 - By1)) - ((By1 - Ay1) * (Bx2 - Bx1))) / Denominator;
        return Ta > 0.01 && Ta < 0.99 && Tb > 0.01 && Tb < 0.99;
    }

    /// <summary>
    /// True when the hub sits in a dense many-to-many link between two ranks (rulebook 2.10).
    /// Fan nesting (5.2) does not apply; port slots and cross-rank tracks handle those edges.
    /// </summary>
    private static bool IsManyToManyHub(
        DiagramContract Contract,
        string HubId,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> PlacedById)
    {
        if (!PlacedById.TryGetValue(HubId, out DiagramGridLayout.GridNode? HubNode))
        {
            return false;
        }

        int HubRank = HubNode.Rank;
        HashSet<int> NeighbourRanks = new HashSet<int>();
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisibleEdge(Edge))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, HubId, StringComparison.Ordinal)
                && PlacedById.TryGetValue(Edge.TargetId, out DiagramGridLayout.GridNode? Target)
                && Target.Rank > HubRank)
            {
                NeighbourRanks.Add(Target.Rank);
            }

            if (string.Equals(Edge.TargetId, HubId, StringComparison.Ordinal)
                && PlacedById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Source)
                && Source.Rank < HubRank)
            {
                NeighbourRanks.Add(Source.Rank);
            }
        }

        foreach (int NeighbourRank in NeighbourRanks)
        {
            HashSet<string> SameRankIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> OtherRankIds = new HashSet<string>(StringComparer.Ordinal);
            int EdgeCount = 0;
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (IsInvisibleEdge(Edge))
                {
                    continue;
                }

                if (!PlacedById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Source)
                    || !PlacedById.TryGetValue(Edge.TargetId, out DiagramGridLayout.GridNode? Target))
                {
                    continue;
                }

                bool Forward = Source.Rank == HubRank && Target.Rank == NeighbourRank;
                bool Backward = Source.Rank == NeighbourRank && Target.Rank == HubRank;
                if (!Forward && !Backward)
                {
                    continue;
                }

                if (Forward)
                {
                    SameRankIds.Add(Source.Id);
                    OtherRankIds.Add(Target.Id);
                }
                else
                {
                    SameRankIds.Add(Target.Id);
                    OtherRankIds.Add(Source.Id);
                }

                EdgeCount++;
            }

            int Span = Math.Max(SameRankIds.Count, OtherRankIds.Count);
            if (EdgeCount > Span)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInvisibleEdge(EdgeContract Edge)
    {
        return string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> CollectFanHubIds(DiagramContract Contract)
    {
        Dictionary<string, int> OutDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, int> InDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            OutDegree.TryGetValue(Edge.SourceId, out int OutCount);
            OutDegree[Edge.SourceId] = OutCount + 1;
            InDegree.TryGetValue(Edge.TargetId, out int InCount);
            InDegree[Edge.TargetId] = InCount + 1;
        }

        HashSet<string> HubIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> Pair in OutDegree)
        {
            if (Pair.Value >= MINIMUM_FAN_BRANCH_COUNT)
            {
                HubIds.Add(Pair.Key);
            }
        }

        foreach (KeyValuePair<string, int> Pair in InDegree)
        {
            if (Pair.Value >= MINIMUM_FAN_BRANCH_COUNT)
            {
                HubIds.Add(Pair.Key);
            }
        }

        return HubIds;
    }

    private static double ResolveFaceSlotX(
        FlowchartLayoutNodeBounds HubBounds,
        int SlotIndex,
        int SlotCount,
        bool ColumnIsRight)
    {
        if (SlotCount <= 1)
        {
            return HubBounds.CenterX;
        }

        double HalfWidth = HubBounds.Width / 2.0;
        double Inset = Math.Min(SLOT_INSET_PX, HalfWidth / 2.0);
        double Usable = HubBounds.Width - (2.0 * Inset);
        double Offset = Inset + (Usable * SlotIndex / (SlotCount - 1));
        if (ColumnIsRight)
        {
            return HubBounds.CenterX - HalfWidth + Offset;
        }

        return HubBounds.CenterX + HalfWidth - Offset;
    }

    private static double ResolveHalfFaceSlotY(
        double FaceEdgeY,
        double Span,
        int SlotIndex,
        int SlotCount,
        bool FromTop)
    {
        if (SlotCount <= 1)
        {
            return FromTop ? FaceEdgeY - (Span / 2.0) : FaceEdgeY + (Span / 2.0);
        }

        double Inset = Math.Min(SLOT_INSET_PX, Span / 4.0);
        double Usable = Span - (2.0 * Inset);
        double Offset = Inset + (Usable * SlotIndex / (SlotCount - 1));
        if (FromTop)
        {
            return FaceEdgeY - Offset;
        }

        return FaceEdgeY + Offset;
    }

    private static string ResolveHorizontalExitSide(bool HubIsSource, bool UsesPositiveSide, bool ColumnIsFlowForward)
    {
        if (HubIsSource)
        {
            return UsesPositiveSide ? "top" : "bottom";
        }

        return ColumnIsFlowForward ? "right" : "left";
    }

    private static string ResolveHorizontalEntrySide(bool HubIsSource, bool UsesPositiveSide, bool ColumnIsFlowForward)
    {
        if (HubIsSource)
        {
            return ColumnIsFlowForward ? "left" : "right";
        }

        return UsesPositiveSide ? "top" : "bottom";
    }

    private static List<EdgeContract> CollectVisibleEdgesFromSource(DiagramContract Contract, string SourceId)
    {
        List<EdgeContract> Edges = [];
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Candidate.SourceId, SourceId, StringComparison.Ordinal))
            {
                Edges.Add(Candidate);
            }
        }

        return Edges;
    }

    private static List<EdgeContract> CollectVisibleEdgesToTarget(DiagramContract Contract, string TargetId)
    {
        List<EdgeContract> Edges = [];
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Candidate.TargetId, TargetId, StringComparison.Ordinal))
            {
                Edges.Add(Candidate);
            }
        }

        return Edges;
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

    private sealed class BranchEdge
    {
        public EdgeContract Edge = null!;

        public FlowchartLayoutNodeBounds OtherBounds = null!;
    }

    private sealed class FanBranchAssignment
    {
        public string EdgeKey = string.Empty;

        public string SourceId = string.Empty;

        public string TargetId = string.Empty;

        public bool HubIsSource;

        public int SlotIndex;

        public int SlotCount;

        public bool UsesPositiveSide;

        public bool ColumnIsFlowForward;

        public double CorridorCoordinate;

        public double TargetPortY;

        public string ExitSide = string.Empty;

        public string EntrySide = string.Empty;

        public FanBranchAssignment Clone()
        {
            return new FanBranchAssignment
            {
                EdgeKey = EdgeKey,
                SourceId = SourceId,
                TargetId = TargetId,
                HubIsSource = HubIsSource,
                SlotIndex = SlotIndex,
                SlotCount = SlotCount,
                UsesPositiveSide = UsesPositiveSide,
                ColumnIsFlowForward = ColumnIsFlowForward,
                CorridorCoordinate = CorridorCoordinate,
                TargetPortY = TargetPortY,
                ExitSide = ExitSide,
                EntrySide = EntrySide
            };
        }
    }
}
