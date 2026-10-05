using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Searches alternative same-rank node orderings so layered inversions are removed when membership allows.
/// </summary>
public static class FlowchartLayoutCrossingSearch
{
    private const double SAME_RANK_Y_TOLERANCE_PX = 12.0;

    /// <summary>
    /// Swaps same-group, same-rank node pairs that invert their targets, while a swap reduces inversions.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="Bounds">Current node bounds.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <returns>Bounds after the search. The input list is not mutated.</returns>
    public static List<FlowchartLayoutNodeBounds> ReduceSameRankInversions(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Bounds,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Entry in Bounds)
        {
            BoundsById[Entry.NodeId] = Entry;
        }

        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        int RemainingPasses = 8;
        while (RemainingPasses > 0)
        {
            RemainingPasses--;
            bool Swapped = false;
            List<(string LeftId, string RightId)> InvertedPairs = CollectInvertedPairs(
                Contract,
                BoundsById,
                Horizontal);
            foreach ((string LeftId, string RightId) in InvertedPairs)
            {
                if (!CanSwapSameGroup(Contract, LeftId, RightId))
                {
                    continue;
                }

                int Before = CountInversions(Contract, BoundsById, Horizontal);
                SwapCenters(BoundsById, LeftId, RightId, Horizontal);
                int After = CountInversions(Contract, BoundsById, Horizontal);
                if (After < Before)
                {
                    Swapped = true;
                    continue;
                }

                SwapCenters(BoundsById, LeftId, RightId, Horizontal);
            }

            if (!Swapped)
            {
                break;
            }
        }

        List<FlowchartLayoutNodeBounds> Result = [];
        foreach (FlowchartLayoutNodeBounds Entry in Bounds)
        {
            Result.Add(BoundsById[Entry.NodeId]);
        }

        return Result;
    }

    /// <summary>
    /// True when two visible edges form a layered inversion that cannot be uncrossed by a same-group swap.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="BoundsById">Node bounds.</param>
    /// <param name="Left">First edge.</param>
    /// <param name="Right">Second edge.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <param name="Reason">Justification when the crossing is unavoidable.</param>
    /// <returns>True when the crossing is an unavoidable layered inversion.</returns>
    public static bool TryJustifyUnavoidableLayeredCrossing(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Left,
        EdgeContract Right,
        FlowchartDirection Direction,
        out string Reason)
    {
        Reason = string.Empty;
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (!IsInvertedPair(Left, Right, BoundsById, Horizontal))
        {
            return false;
        }

        bool SourcesLocked = !CanSwapSameGroup(Contract, Left.SourceId, Right.SourceId);
        bool TargetsLocked = !CanSwapSameGroup(Contract, Left.TargetId, Right.TargetId);
        if (!SourcesLocked && !TargetsLocked)
        {
            return false;
        }

        Reason = "layered inversion "
            + Left.SourceId
            + "->"
            + Left.TargetId
            + " and "
            + Right.SourceId
            + "->"
            + Right.TargetId
            + ": a crossing is unavoidable without breaking subgraph membership";
        return true;
    }

    /// <summary>
    /// True when six distinct endpoints form a K3,3 between the two partitions of the two edges plus any third pair.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="Left">First crossing edge.</param>
    /// <param name="Right">Second crossing edge.</param>
    /// <param name="Reason">Justification when K3,3 is present.</param>
    /// <returns>True when a K3,3 subgraph forces a crossing.</returns>
    public static bool TryJustifyK33Crossing(
        DiagramContract Contract,
        EdgeContract Left,
        EdgeContract Right,
        out string Reason)
    {
        Reason = string.Empty;
        HashSet<string> Endpoints = new HashSet<string>(StringComparer.Ordinal)
        {
            Left.SourceId,
            Left.TargetId,
            Right.SourceId,
            Right.TargetId
        };
        if (Endpoints.Count != 4)
        {
            return false;
        }

        List<string> PartitionA = [Left.SourceId, Right.SourceId];
        List<string> PartitionB = [Left.TargetId, Right.TargetId];
        if (string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (NodeContract Extra in Contract.Nodes)
        {
            if (Endpoints.Contains(Extra.Id) || FlowchartCompoundEndpointHelper.IsGroupId(Contract, Extra.Id))
            {
                continue;
            }

            int DegreeA = CountVisibleEdgesToSet(Contract, Extra.Id, PartitionB);
            int DegreeB = CountVisibleEdgesToSet(Contract, Extra.Id, PartitionA);
            if (DegreeA == 2)
            {
                PartitionA.Add(Extra.Id);
                if (IsCompleteBipartite(Contract, PartitionA, PartitionB))
                {
                    Reason = "K3,3 subgraph: a crossing is unavoidable";
                    return true;
                }

                PartitionA.RemoveAt(PartitionA.Count - 1);
            }

            if (DegreeB == 2)
            {
                PartitionB.Add(Extra.Id);
                if (IsCompleteBipartite(Contract, PartitionA, PartitionB))
                {
                    Reason = "K3,3 subgraph: a crossing is unavoidable";
                    return true;
                }

                PartitionB.RemoveAt(PartitionB.Count - 1);
            }
        }

        return false;
    }

    private static List<(string LeftId, string RightId)> CollectInvertedPairs(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool Horizontal)
    {
        List<(string LeftId, string RightId)> Pairs = [];
        List<EdgeContract> Visible = CollectVisibleEdges(Contract);
        for (int LeftIndex = 0; LeftIndex < Visible.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Visible.Count; RightIndex++)
            {
                EdgeContract Left = Visible[LeftIndex];
                EdgeContract Right = Visible[RightIndex];
                if (IsInvertedPair(Left, Right, BoundsById, Horizontal))
                {
                    Pairs.Add((Left.SourceId, Right.SourceId));
                }
            }
        }

        return Pairs;
    }

    private static int CountInversions(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool Horizontal)
    {
        int Count = 0;
        List<EdgeContract> Visible = CollectVisibleEdges(Contract);
        for (int LeftIndex = 0; LeftIndex < Visible.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Visible.Count; RightIndex++)
            {
                if (IsInvertedPair(Visible[LeftIndex], Visible[RightIndex], BoundsById, Horizontal))
                {
                    Count++;
                }
            }
        }

        return Count;
    }

    private static bool IsInvertedPair(
        EdgeContract Left,
        EdgeContract Right,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool Horizontal)
    {
        if (string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal)
            || string.Equals(Left.SourceId, Right.TargetId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.SourceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Left.SourceId, out FlowchartLayoutNodeBounds LeftSource)
            || !BoundsById.TryGetValue(Right.SourceId, out FlowchartLayoutNodeBounds RightSource)
            || !BoundsById.TryGetValue(Left.TargetId, out FlowchartLayoutNodeBounds LeftTarget)
            || !BoundsById.TryGetValue(Right.TargetId, out FlowchartLayoutNodeBounds RightTarget))
        {
            return false;
        }

        if (Horizontal)
        {
            if (Math.Abs(LeftSource.CenterX - RightSource.CenterX) > SAME_RANK_Y_TOLERANCE_PX
                || Math.Abs(LeftTarget.CenterX - RightTarget.CenterX) > SAME_RANK_Y_TOLERANCE_PX)
            {
                return false;
            }

            bool SourcesOrdered = LeftSource.CenterY < RightSource.CenterY;
            bool TargetsOrdered = LeftTarget.CenterY < RightTarget.CenterY;
            return SourcesOrdered != TargetsOrdered;
        }

        if (Math.Abs(LeftSource.CenterY - RightSource.CenterY) > SAME_RANK_Y_TOLERANCE_PX
            || Math.Abs(LeftTarget.CenterY - RightTarget.CenterY) > SAME_RANK_Y_TOLERANCE_PX)
        {
            return false;
        }

        bool SourcesLeft = LeftSource.CenterX < RightSource.CenterX;
        bool TargetsLeft = LeftTarget.CenterX < RightTarget.CenterX;
        return SourcesLeft != TargetsLeft;
    }

    private static bool CanSwapSameGroup(DiagramContract Contract, string LeftId, string RightId)
    {
        string? LeftGroup = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(LeftId, Contract);
        string? RightGroup = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(RightId, Contract);
        return string.Equals(LeftGroup, RightGroup, StringComparison.Ordinal);
    }

    private static void SwapCenters(
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string LeftId,
        string RightId,
        bool Horizontal)
    {
        FlowchartLayoutNodeBounds Left = BoundsById[LeftId];
        FlowchartLayoutNodeBounds Right = BoundsById[RightId];
        if (Horizontal)
        {
            BoundsById[LeftId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Left.NodeId,
                CenterX = Left.CenterX,
                CenterY = Right.CenterY,
                Width = Left.Width,
                Height = Left.Height
            };
            BoundsById[RightId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Right.NodeId,
                CenterX = Right.CenterX,
                CenterY = Left.CenterY,
                Width = Right.Width,
                Height = Right.Height
            };
            return;
        }

        BoundsById[LeftId] = new FlowchartLayoutNodeBounds
        {
            NodeId = Left.NodeId,
            CenterX = Right.CenterX,
            CenterY = Left.CenterY,
            Width = Left.Width,
            Height = Left.Height
        };
        BoundsById[RightId] = new FlowchartLayoutNodeBounds
        {
            NodeId = Right.NodeId,
            CenterX = Left.CenterX,
            CenterY = Right.CenterY,
            Width = Right.Width,
            Height = Right.Height
        };
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

    private static int CountVisibleEdgesToSet(
        DiagramContract Contract,
        string NodeId,
        IReadOnlyList<string> OtherIds)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string OtherId in OtherIds)
            {
                bool Matches =
                    (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                        && string.Equals(Edge.TargetId, OtherId, StringComparison.Ordinal))
                    || (string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal)
                        && string.Equals(Edge.SourceId, OtherId, StringComparison.Ordinal));
                if (Matches)
                {
                    Count++;
                }
            }
        }

        return Count;
    }

    private static bool IsCompleteBipartite(
        DiagramContract Contract,
        IReadOnlyList<string> PartitionA,
        IReadOnlyList<string> PartitionB)
    {
        if (PartitionA.Count != 3 || PartitionB.Count != 3)
        {
            return false;
        }

        foreach (string LeftId in PartitionA)
        {
            foreach (string RightId in PartitionB)
            {
                if (!HasVisibleEdge(Contract, LeftId, RightId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasVisibleEdge(DiagramContract Contract, string LeftId, string RightId)
    {
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool Matches =
                (string.Equals(Edge.SourceId, LeftId, StringComparison.Ordinal)
                    && string.Equals(Edge.TargetId, RightId, StringComparison.Ordinal))
                || (string.Equals(Edge.SourceId, RightId, StringComparison.Ordinal)
                    && string.Equals(Edge.TargetId, LeftId, StringComparison.Ordinal));
            if (Matches)
            {
                return true;
            }
        }

        return false;
    }
}
