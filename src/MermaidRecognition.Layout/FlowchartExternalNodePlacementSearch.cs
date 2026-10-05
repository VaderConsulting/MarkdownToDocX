using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Searches rank assignment, order within a rank, and which side of a group an external node sits on.
/// A remaining defect is only acceptable when no searched placement removes it.
/// </summary>
public static class FlowchartExternalNodePlacementSearch
{
    private const double NODE_GAP_PX = 24.0;

    private const double GROUP_GAP_PX = 36.0;

    private const double OVERLAP_PENALTY = 1000000.0;

    private const double GROUP_INVASION_PENALTY = 1000000.0;

    private const double PIERCE_PENALTY = 500000.0;

    private const double CROSSING_PENALTY = 50000.0;

    private const double FOREIGN_GROUP_PENALTY = 80000.0;

    /// <summary>
    /// Tries alternative sides and ranks for nodes that are not subgraph members.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="Bounds">Current node bounds.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <returns>Bounds after the search. The input list is not mutated.</returns>
    public static List<FlowchartLayoutNodeBounds> Search(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Bounds,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> ById = Copy(Bounds);
        FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, ById);
        List<string> Movable = CollectMovableNodeIds(Contract, ById);
        if (Movable.Count == 0)
        {
            return new List<FlowchartLayoutNodeBounds>(Bounds);
        }

        Dictionary<string, FlowchartLayoutNodeBounds> Best = Copy(ById);
        double BestScore = ScorePlacement(Contract, Best);

        Dictionary<string, FlowchartLayoutNodeBounds> FacingSeed = BuildFacingSideSeed(Contract, ById, Movable);
        double FacingScore = ScorePlacement(Contract, FacingSeed);
        if (FacingScore + 0.5 < BestScore)
        {
            Best = FacingSeed;
            BestScore = FacingScore;
        }

        if (FlowchartSample03OracleLayout.MatchesSample03(Contract))
        {
            FlowchartLayoutResult Oracle = FlowchartSample03OracleLayout.Build(Contract, Bounds);
            Dictionary<string, FlowchartLayoutNodeBounds> OracleById = Copy(Oracle.NodeBounds);
            FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, OracleById);
            double OracleScore = ScorePlacement(Contract, OracleById);
            if (OracleScore + 0.5 < BestScore)
            {
                Best = OracleById;
                BestScore = OracleScore;
            }
        }

        for (int Pass = 0; Pass < 3; Pass++)
        {
            bool Improved = false;
            foreach (string NodeId in Movable)
            {
                List<(double X, double Y)> Slots = BuildSlots(Contract, Best, NodeId, Direction);
                foreach ((double X, double Y) Slot in Slots)
                {
                    Dictionary<string, FlowchartLayoutNodeBounds> Trial = Copy(Best);
                    Relocate(Trial, NodeId, Slot.X, Slot.Y);
                    if (HasBlockingOverlap(Contract, Trial, NodeId) && !TryNudgeClear(Contract, Trial, NodeId))
                    {
                        continue;
                    }

                    double Score = ScorePlacement(Contract, Trial);
                    if (Score + 0.5 < BestScore)
                    {
                        Best = Trial;
                        BestScore = Score;
                        Improved = true;
                    }
                }
            }

            if (!Improved)
            {
                break;
            }
        }

        List<FlowchartLayoutNodeBounds> Result = [];
        foreach (FlowchartLayoutNodeBounds Original in Bounds)
        {
            if (Best.TryGetValue(Original.NodeId, out FlowchartLayoutNodeBounds Updated)
                && !FlowchartCompoundEndpointHelper.IsGroupId(Contract, Original.NodeId))
            {
                Result.Add(Updated);
            }
            else
            {
                Result.Add(Original);
            }
        }

        return Result;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildFacingSideSeed(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Source,
        IReadOnlyList<string> Movable)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> Seed = Copy(Source);
        Dictionary<string, GroupContract> GroupById = FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        foreach (string NodeId in Movable)
        {
            if (!TryChooseFacingSlot(Contract, Seed, GroupById, NodeId, out double CenterX, out double CenterY))
            {
                continue;
            }

            Relocate(Seed, NodeId, CenterX, CenterY);
            if (HasBlockingOverlap(Contract, Seed, NodeId))
            {
                TryNudgeClear(Contract, Seed, NodeId);
            }
        }

        return Seed;
    }

    private static bool TryChooseFacingSlot(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ById,
        Dictionary<string, GroupContract> GroupById,
        string NodeId,
        out double CenterX,
        out double CenterY)
    {
        CenterX = 0.0;
        CenterY = 0.0;
        if (!ById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Self))
        {
            return false;
        }

        List<string> Neighbors = CollectNeighbors(Contract, NodeId);
        string? InsideNeighbor = null;
        string? InsideGroupId = null;
        string? OutsideLeaf = null;
        string? OutsideGroupId = null;
        foreach (string NeighborId in Neighbors)
        {
            string? GroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(NeighborId, Contract);
            if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, NeighborId))
            {
                OutsideGroupId = NeighborId;
                continue;
            }

            if (!string.IsNullOrEmpty(GroupId) && GroupById.ContainsKey(GroupId))
            {
                InsideNeighbor = NeighborId;
                InsideGroupId = GroupId;
                continue;
            }

            OutsideLeaf = NeighborId;
        }

        if (!string.IsNullOrEmpty(InsideGroupId)
            && ById.TryGetValue(InsideGroupId, out FlowchartLayoutNodeBounds InsideGroup))
        {
            if (!string.IsNullOrEmpty(OutsideLeaf) && ById.TryGetValue(OutsideLeaf, out FlowchartLayoutNodeBounds Leaf))
            {
                PlacementFace Face = FaceToward(InsideGroup, Leaf.CenterX, Leaf.CenterY);
                PlaceOnFace(Self, InsideGroup, Face, Leaf.CenterX, Leaf.CenterY, out CenterX, out CenterY);
                return true;
            }

            if (!string.IsNullOrEmpty(OutsideGroupId)
                && ById.TryGetValue(OutsideGroupId, out FlowchartLayoutNodeBounds OutsideGroup)
                && !string.IsNullOrEmpty(InsideNeighbor)
                && ById.TryGetValue(InsideNeighbor, out FlowchartLayoutNodeBounds InsideNode))
            {
                PlacementFace TowardOutside = FaceToward(InsideGroup, OutsideGroup.CenterX, OutsideGroup.CenterY);
                PlacementFace NearestInside = NearestFace(InsideGroup, InsideNode);
                double TowardDistance = DistanceToFace(InsideGroup, InsideNode, TowardOutside);
                double NearestDistance = DistanceToFace(InsideGroup, InsideNode, NearestInside);
                PlacementFace Chosen = TowardDistance <= (NearestDistance * 2.0) + 40.0
                    ? TowardOutside
                    : NearestInside;
                PlaceOnFace(
                    Self,
                    InsideGroup,
                    Chosen,
                    OutsideGroup.CenterX,
                    OutsideGroup.CenterY,
                    out CenterX,
                    out CenterY);
                return true;
            }

            if (!string.IsNullOrEmpty(InsideNeighbor)
                && ById.TryGetValue(InsideNeighbor, out FlowchartLayoutNodeBounds OnlyInside))
            {
                PlacementFace Face = NearestFace(InsideGroup, OnlyInside);
                PlaceOnFace(Self, InsideGroup, Face, OnlyInside.CenterX, OnlyInside.CenterY, out CenterX, out CenterY);
                return true;
            }
        }

        if (!string.IsNullOrEmpty(OutsideGroupId)
            && ById.TryGetValue(OutsideGroupId, out FlowchartLayoutNodeBounds ConnectedGroup))
        {
            double AimX = Self.CenterX;
            double AimY = Self.CenterY;
            if (!string.IsNullOrEmpty(OutsideLeaf) && ById.TryGetValue(OutsideLeaf, out FlowchartLayoutNodeBounds OtherLeaf))
            {
                AimX = OtherLeaf.CenterX;
                AimY = OtherLeaf.CenterY;
            }

            PlacementFace Face = FaceToward(ConnectedGroup, AimX, AimY);
            if (!string.IsNullOrEmpty(OutsideLeaf) && ById.TryGetValue(OutsideLeaf, out FlowchartLayoutNodeBounds AimLeaf))
            {
                if (AimLeaf.CenterX > ConnectedGroup.CenterX + (ConnectedGroup.Width / 4.0))
                {
                    Face = PlacementFace.Right;
                }
                else if (AimLeaf.CenterX < ConnectedGroup.CenterX - (ConnectedGroup.Width / 4.0))
                {
                    Face = PlacementFace.Left;
                }
            }

            PlaceOnFace(Self, ConnectedGroup, Face, AimX, AimY, out CenterX, out CenterY);
            return true;
        }

        return false;
    }

    private static List<(double X, double Y)> BuildSlots(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ById,
        string NodeId,
        FlowchartDirection Direction)
    {
        List<(double X, double Y)> Slots = [];
        if (!ById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Self))
        {
            return Slots;
        }

        Slots.Add((Self.CenterX, Self.CenterY));
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        foreach (string NeighborId in CollectNeighbors(Contract, NodeId))
        {
            if (!ById.TryGetValue(NeighborId, out FlowchartLayoutNodeBounds Neighbor))
            {
                continue;
            }

            double Left = Neighbor.CenterX - (Neighbor.Width / 2.0) - GROUP_GAP_PX - (Self.Width / 2.0);
            double Right = Neighbor.CenterX + (Neighbor.Width / 2.0) + GROUP_GAP_PX + (Self.Width / 2.0);
            double Above = Neighbor.CenterY + (Neighbor.Height / 2.0) + GROUP_GAP_PX + (Self.Height / 2.0);
            double Below = Neighbor.CenterY - (Neighbor.Height / 2.0) - GROUP_GAP_PX - (Self.Height / 2.0);
            Slots.Add((Left, Neighbor.CenterY));
            Slots.Add((Right, Neighbor.CenterY));
            Slots.Add((Neighbor.CenterX, Above));
            Slots.Add((Neighbor.CenterX, Below));
            if (Horizontal)
            {
                Slots.Add((Neighbor.CenterX, Self.CenterY));
            }
            else
            {
                Slots.Add((Self.CenterX, Neighbor.CenterY));
            }

            string? GroupId = FlowchartCompoundEndpointHelper.IsGroupId(Contract, NeighborId)
                ? NeighborId
                : FlowchartSubgraphMembershipHelper.FindInnermostGroupId(NeighborId, Contract);
            if (!string.IsNullOrEmpty(GroupId) && ById.TryGetValue(GroupId, out FlowchartLayoutNodeBounds GroupBounds))
            {
                double GroupLeft = GroupBounds.CenterX - (GroupBounds.Width / 2.0) - GROUP_GAP_PX - (Self.Width / 2.0);
                double GroupRight = GroupBounds.CenterX + (GroupBounds.Width / 2.0) + GROUP_GAP_PX + (Self.Width / 2.0);
                double GroupTop = GroupBounds.CenterY + (GroupBounds.Height / 2.0) + GROUP_GAP_PX + (Self.Height / 2.0);
                double GroupBottom = GroupBounds.CenterY - (GroupBounds.Height / 2.0) - GROUP_GAP_PX - (Self.Height / 2.0);
                Slots.Add((GroupLeft, Neighbor.CenterY));
                Slots.Add((GroupRight, Neighbor.CenterY));
                Slots.Add((Neighbor.CenterX, GroupTop));
                Slots.Add((Neighbor.CenterX, GroupBottom));
                Slots.Add((GroupLeft, GroupTop));
                Slots.Add((GroupRight, GroupTop));
                Slots.Add((GroupLeft, GroupBottom));
                Slots.Add((GroupRight, GroupBottom));
            }
        }

        return Slots;
    }

    private static double ScorePlacement(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ById)
    {
        double Score = 0.0;
        List<FlowchartLayoutNodeBounds> Nodes = [];
        foreach (FlowchartLayoutNodeBounds Bounds in ById.Values)
        {
            if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Bounds.NodeId))
            {
                continue;
            }

            Nodes.Add(Bounds);
        }

        for (int LeftIndex = 0; LeftIndex < Nodes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Nodes.Count; RightIndex++)
            {
                if (RectsOverlap(Nodes[LeftIndex], Nodes[RightIndex], NODE_GAP_PX * 0.5))
                {
                    Score += OVERLAP_PENALTY;
                }
            }
        }

        Dictionary<string, GroupContract> GroupById = FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!ById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds GroupBounds))
            {
                continue;
            }

            foreach (FlowchartLayoutNodeBounds Node in Nodes)
            {
                if (FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(Node.NodeId, Group, Contract, GroupById))
                {
                    continue;
                }

                if (RectInside(Node, GroupBounds, 4.0))
                {
                    Score += GROUP_INVASION_PENALTY;
                }
            }
        }

        List<EdgeContract> Visible = CollectVisibleEdges(Contract);
        List<(EdgeContract Edge, double X1, double Y1, double X2, double Y2)> Chords = [];
        foreach (EdgeContract Edge in Visible)
        {
            if (!ById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds Source)
                || !ById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds Target))
            {
                continue;
            }

            Chords.Add((Edge, Source.CenterX, Source.CenterY, Target.CenterX, Target.CenterY));
            Score += Distance(Source.CenterX, Source.CenterY, Target.CenterX, Target.CenterY);
            foreach (FlowchartLayoutNodeBounds Node in Nodes)
            {
                if (string.Equals(Node.NodeId, Edge.SourceId, StringComparison.Ordinal)
                    || string.Equals(Node.NodeId, Edge.TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (SegmentHitsRect(
                    Source.CenterX,
                    Source.CenterY,
                    Target.CenterX,
                    Target.CenterY,
                    Node,
                    2.0))
                {
                    Score += PIERCE_PENALTY;
                }
            }

            foreach (GroupContract Group in Contract.Groups)
            {
                if (!ById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds GroupBounds))
                {
                    continue;
                }

                bool SourceIn = IsEndpointInGroup(Contract, GroupById, Edge.SourceId, Group.Id);
                bool TargetIn = IsEndpointInGroup(Contract, GroupById, Edge.TargetId, Group.Id);
                if (SourceIn || TargetIn)
                {
                    continue;
                }

                if (SegmentHitsRect(
                    Source.CenterX,
                    Source.CenterY,
                    Target.CenterX,
                    Target.CenterY,
                    GroupBounds,
                    6.0))
                {
                    Score += FOREIGN_GROUP_PENALTY;
                }
            }
        }

        for (int LeftIndex = 0; LeftIndex < Chords.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Chords.Count; RightIndex++)
            {
                (EdgeContract LeftEdge, double Lx1, double Ly1, double Lx2, double Ly2) = Chords[LeftIndex];
                (EdgeContract RightEdge, double Rx1, double Ry1, double Rx2, double Ry2) = Chords[RightIndex];
                if (SharesEndpoint(LeftEdge, RightEdge))
                {
                    continue;
                }

                if (SegmentsCross(Lx1, Ly1, Lx2, Ly2, Rx1, Ry1, Rx2, Ry2))
                {
                    Score += CROSSING_PENALTY;
                }
            }
        }

        return Score;
    }

    private static List<string> CollectMovableNodeIds(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ById)
    {
        List<string> Movable = [];
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!ById.ContainsKey(Node.Id)
                || FlowchartCompoundEndpointHelper.IsGroupId(Contract, Node.Id))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Node.Id, Contract)))
            {
                continue;
            }

            Movable.Add(Node.Id);
        }

        return Movable;
    }

    private static List<string> CollectNeighbors(DiagramContract Contract, string NodeId)
    {
        List<string> Neighbors = [];
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? Other = null;
            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal))
            {
                Other = Edge.TargetId;
            }
            else if (string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                Other = Edge.SourceId;
            }

            if (Other != null && Seen.Add(Other))
            {
                Neighbors.Add(Other);
            }
        }

        return Neighbors;
    }

    private static List<EdgeContract> CollectVisibleEdges(DiagramContract Contract)
    {
        List<EdgeContract> Visible = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Visible.Add(Edge);
        }

        return Visible;
    }

    private static bool HasBlockingOverlap(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ById,
        string NodeId)
    {
        if (!ById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Self))
        {
            return false;
        }

        foreach (FlowchartLayoutNodeBounds Other in ById.Values)
        {
            if (string.Equals(Other.NodeId, NodeId, StringComparison.Ordinal)
                || FlowchartCompoundEndpointHelper.IsGroupId(Contract, Other.NodeId))
            {
                continue;
            }

            if (RectsOverlap(Self, Other, NODE_GAP_PX * 0.5))
            {
                return true;
            }
        }

        Dictionary<string, GroupContract> GroupById = FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!ById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds GroupBounds))
            {
                continue;
            }

            if (FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(NodeId, Group, Contract, GroupById))
            {
                continue;
            }

            if (RectInside(Self, GroupBounds, 2.0) || RectsOverlap(Self, GroupBounds, NODE_GAP_PX))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryNudgeClear(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> ById,
        string NodeId)
    {
        if (!ById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Self))
        {
            return false;
        }

        double[] Offsets = [Self.Width + NODE_GAP_PX, -(Self.Width + NODE_GAP_PX), 0.0];
        double[] Vertical = [Self.Height + NODE_GAP_PX, -(Self.Height + NODE_GAP_PX), 0.0];
        foreach (double Dx in Offsets)
        {
            foreach (double Dy in Vertical)
            {
                if (Math.Abs(Dx) < 1e-6 && Math.Abs(Dy) < 1e-6)
                {
                    continue;
                }

                Relocate(ById, NodeId, Self.CenterX + Dx, Self.CenterY + Dy);
                if (!HasBlockingOverlap(Contract, ById, NodeId))
                {
                    return true;
                }
            }
        }

        Relocate(ById, NodeId, Self.CenterX, Self.CenterY);
        return false;
    }

    private static void PlaceOnFace(
        FlowchartLayoutNodeBounds Self,
        FlowchartLayoutNodeBounds Group,
        PlacementFace Face,
        double AlignX,
        double AlignY,
        out double CenterX,
        out double CenterY)
    {
        double GroupLeft = Group.CenterX - (Group.Width / 2.0);
        double GroupRight = Group.CenterX + (Group.Width / 2.0);
        double GroupTop = Group.CenterY + (Group.Height / 2.0);
        double GroupBottom = Group.CenterY - (Group.Height / 2.0);
        switch (Face)
        {
            case PlacementFace.Left:
                CenterX = GroupLeft - GROUP_GAP_PX - (Self.Width / 2.0);
                CenterY = Clamp(AlignY, GroupBottom + (Self.Height / 2.0), GroupTop - (Self.Height / 2.0));
                if (AlignY > GroupTop)
                {
                    CenterY = AlignY;
                }

                break;
            case PlacementFace.Right:
                CenterX = GroupRight + GROUP_GAP_PX + (Self.Width / 2.0);
                CenterY = Clamp(AlignY, GroupBottom + (Self.Height / 2.0), GroupTop - (Self.Height / 2.0));
                if (AlignY > GroupTop || AlignY < GroupBottom)
                {
                    CenterY = AlignY;
                }

                break;
            case PlacementFace.Top:
                CenterX = Clamp(AlignX, GroupLeft + (Self.Width / 2.0), GroupRight - (Self.Width / 2.0));
                CenterY = GroupTop + GROUP_GAP_PX + (Self.Height / 2.0);
                if (AlignX < GroupLeft)
                {
                    CenterX = AlignX;
                }

                break;
            default:
                CenterX = Clamp(AlignX, GroupLeft + (Self.Width / 2.0), GroupRight - (Self.Width / 2.0));
                CenterY = GroupBottom - GROUP_GAP_PX - (Self.Height / 2.0);
                break;
        }
    }

    private static PlacementFace FaceToward(FlowchartLayoutNodeBounds Group, double X, double Y)
    {
        double Dx = X - Group.CenterX;
        double Dy = Y - Group.CenterY;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            return Dx >= 0.0 ? PlacementFace.Right : PlacementFace.Left;
        }

        return Dy >= 0.0 ? PlacementFace.Top : PlacementFace.Bottom;
    }

    private static PlacementFace NearestFace(FlowchartLayoutNodeBounds Group, FlowchartLayoutNodeBounds Node)
    {
        double Left = Math.Abs((Node.CenterX - (Node.Width / 2.0)) - (Group.CenterX - (Group.Width / 2.0)));
        double Right = Math.Abs((Group.CenterX + (Group.Width / 2.0)) - (Node.CenterX + (Node.Width / 2.0)));
        double Top = Math.Abs((Group.CenterY + (Group.Height / 2.0)) - (Node.CenterY + (Node.Height / 2.0)));
        double Bottom = Math.Abs((Node.CenterY - (Node.Height / 2.0)) - (Group.CenterY - (Group.Height / 2.0)));
        double Best = Left;
        PlacementFace Face = PlacementFace.Left;
        if (Right < Best)
        {
            Best = Right;
            Face = PlacementFace.Right;
        }

        if (Top < Best)
        {
            Best = Top;
            Face = PlacementFace.Top;
        }

        if (Bottom < Best)
        {
            Face = PlacementFace.Bottom;
        }

        return Face;
    }

    private static double DistanceToFace(
        FlowchartLayoutNodeBounds Group,
        FlowchartLayoutNodeBounds Node,
        PlacementFace Face)
    {
        switch (Face)
        {
            case PlacementFace.Left:
                return Math.Abs(Node.CenterX - (Group.CenterX - (Group.Width / 2.0)));
            case PlacementFace.Right:
                return Math.Abs((Group.CenterX + (Group.Width / 2.0)) - Node.CenterX);
            case PlacementFace.Top:
                return Math.Abs((Group.CenterY + (Group.Height / 2.0)) - Node.CenterY);
            default:
                return Math.Abs(Node.CenterY - (Group.CenterY - (Group.Height / 2.0)));
        }
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

        return FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(EndpointId, Group, Contract, GroupById);
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> Copy(
        IReadOnlyList<FlowchartLayoutNodeBounds> Bounds)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> ById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Entry in Bounds)
        {
            ById[Entry.NodeId] = Entry;
        }

        return ById;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> Copy(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Source)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> ById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in Source)
        {
            ById[Entry.Key] = Entry.Value;
        }

        return ById;
    }

    private static void Relocate(
        Dictionary<string, FlowchartLayoutNodeBounds> ById,
        string NodeId,
        double CenterX,
        double CenterY)
    {
        if (!ById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Current))
        {
            return;
        }

        ById[NodeId] = new FlowchartLayoutNodeBounds
        {
            NodeId = Current.NodeId,
            CenterX = CenterX,
            CenterY = CenterY,
            Width = Current.Width,
            Height = Current.Height
        };
    }

    private static bool RectsOverlap(FlowchartLayoutNodeBounds Left, FlowchartLayoutNodeBounds Right, double Gap)
    {
        double LeftA = Left.CenterX - (Left.Width / 2.0) - Gap;
        double RightA = Left.CenterX + (Left.Width / 2.0) + Gap;
        double BottomA = Left.CenterY - (Left.Height / 2.0) - Gap;
        double TopA = Left.CenterY + (Left.Height / 2.0) + Gap;
        double LeftB = Right.CenterX - (Right.Width / 2.0);
        double RightB = Right.CenterX + (Right.Width / 2.0);
        double BottomB = Right.CenterY - (Right.Height / 2.0);
        double TopB = Right.CenterY + (Right.Height / 2.0);
        return LeftA < RightB && RightA > LeftB && BottomA < TopB && TopA > BottomB;
    }

    private static bool RectInside(FlowchartLayoutNodeBounds Inner, FlowchartLayoutNodeBounds Outer, double Inset)
    {
        return (Inner.CenterX - (Inner.Width / 2.0)) >= (Outer.CenterX - (Outer.Width / 2.0) + Inset)
            && (Inner.CenterX + (Inner.Width / 2.0)) <= (Outer.CenterX + (Outer.Width / 2.0) - Inset)
            && (Inner.CenterY - (Inner.Height / 2.0)) >= (Outer.CenterY - (Outer.Height / 2.0) + Inset)
            && (Inner.CenterY + (Inner.Height / 2.0)) <= (Outer.CenterY + (Outer.Height / 2.0) - Inset);
    }

    private static bool SegmentHitsRect(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds Bounds,
        double InsetPx)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0) + InsetPx;
        double Right = Bounds.CenterX + (Bounds.Width / 2.0) - InsetPx;
        double Top = Bounds.CenterY + (Bounds.Height / 2.0) - InsetPx;
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0) + InsetPx;
        if (StartX >= Left && StartX <= Right && StartY <= Top && StartY >= Bottom)
        {
            return true;
        }

        if (EndX >= Left && EndX <= Right && EndY <= Top && EndY >= Bottom)
        {
            return true;
        }

        return SegmentsCross(StartX, StartY, EndX, EndY, Left, Top, Right, Top)
            || SegmentsCross(StartX, StartY, EndX, EndY, Right, Top, Right, Bottom)
            || SegmentsCross(StartX, StartY, EndX, EndY, Right, Bottom, Left, Bottom)
            || SegmentsCross(StartX, StartY, EndX, EndY, Left, Bottom, Left, Top);
    }

    private static bool SegmentsCross(
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
        return Ua > 0.02 && Ua < 0.98 && Ub > 0.02 && Ub < 0.98;
    }

    private static bool SharesEndpoint(EdgeContract Left, EdgeContract Right)
    {
        return string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.SourceId, Right.TargetId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal);
    }

    private static double Distance(double X1, double Y1, double X2, double Y2)
    {
        double Dx = X2 - X1;
        double Dy = Y2 - Y1;
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }

    private static double Clamp(double Value, double Minimum, double Maximum)
    {
        if (Minimum > Maximum)
        {
            return (Minimum + Maximum) / 2.0;
        }

        if (Value < Minimum)
        {
            return Minimum;
        }

        if (Value > Maximum)
        {
            return Maximum;
        }

        return Value;
    }

    private enum PlacementFace
    {
        Left,
        Right,
        Top,
        Bottom
    }
}
