using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Applies rulebook section 2 placement: spine column, parent-child columns,
/// cross-link pairing, many-to-many ordering, and in-rank crossing swaps.
/// </summary>
internal static class DiagramGridPlacementHelper
{
    /// <summary>
    /// Reassigns grid columns from Mermaid's starting order using structural rules only.
    /// </summary>
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Placed, bool Horizontal)
    {
        if (Contract == null || Placed.Count == 0)
        {
            return;
        }

        Dictionary<string, DiagramGridLayout.GridNode> ById = IndexPlaced(Placed);
        int SpineColumn = ResolveSpineColumn(Contract, Placed, ById);
        ApplySpineColumns(Contract, Placed, ById, SpineColumn);
        ApplySingleChildColumns(Contract, ById);
        ApplyMultiParentColumns(Contract, ById);
        ApplyCrossLinkPairing(Contract, Placed, ById);
        ApplyManyToManyOrdering(Contract, Placed, ById);
        ApplyInRankCrossingSwaps(Contract, Placed, ById);
    }

    private static void ApplySpineColumns(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, DiagramGridLayout.GridNode> ById,
        int SpineColumn)
    {
        List<string> Roots = FindRootIds(Contract, ById.Keys);
        if (Roots.Count == 0)
        {
            return;
        }

        string RootId = SelectPrimaryRoot(Contract, Roots);
        HashSet<string> SpineIds = BuildSpineNodeIds(Contract, RootId);
        foreach (string SpineId in SpineIds)
        {
            if (ById.TryGetValue(SpineId, out DiagramGridLayout.GridNode? Node))
            {
                Node.Column = SpineColumn;
            }
        }
    }

    private static void ApplySingleChildColumns(DiagramContract Contract, Dictionary<string, DiagramGridLayout.GridNode> ById)
    {
        foreach (DiagramGridLayout.GridNode Parent in ById.Values)
        {
            List<string> Children = VisibleChildren(Contract, Parent.Id);
            if (Children.Count != 1)
            {
                continue;
            }

            if (!ById.TryGetValue(Children[0], out DiagramGridLayout.GridNode? Child))
            {
                continue;
            }

            if (Child.Rank <= Parent.Rank)
            {
                continue;
            }

            Child.Column = Parent.Column;
        }
    }

    private static void ApplyMultiParentColumns(DiagramContract Contract, Dictionary<string, DiagramGridLayout.GridNode> ById)
    {
        foreach (DiagramGridLayout.GridNode Child in ById.Values)
        {
            List<string> Parents = VisibleParents(Contract, Child.Id);
            if (Parents.Count < 2)
            {
                continue;
            }

            Parents.Sort(delegate(string Left, string Right)
            {
                return CreationIndex(Contract, Left).CompareTo(CreationIndex(Contract, Right));
            });

            foreach (string ParentId in Parents)
            {
                if (!ById.TryGetValue(ParentId, out DiagramGridLayout.GridNode? Parent))
                {
                    continue;
                }

                if (!HasChildDirectlyBelow(Contract, ById, Parent, Child.Rank))
                {
                    Child.Column = Parent.Column;
                    break;
                }
            }
        }
    }

    private static void ApplyCrossLinkPairing(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, DiagramGridLayout.GridNode> ById)
    {
        int RankCount = MaxRank(Placed);
        for (int Rank = 0; Rank + 1 < RankCount; Rank++)
        {
            List<DiagramGridLayout.GridNode> Upper = NodesInRank(Placed, Rank);
            List<DiagramGridLayout.GridNode> Lower = NodesInRank(Placed, Rank + 1);
            if (Upper.Count != 2 || Lower.Count != 2)
            {
                continue;
            }

            Upper.Sort(delegate(DiagramGridLayout.GridNode Left, DiagramGridLayout.GridNode Right)
            {
                return Left.Column.CompareTo(Right.Column);
            });
            Lower.Sort(delegate(DiagramGridLayout.GridNode Left, DiagramGridLayout.GridNode Right)
            {
                return Left.Column.CompareTo(Right.Column);
            });

            DiagramGridLayout.GridNode ParentA = Upper[0];
            DiagramGridLayout.GridNode ParentB = Upper[1];
            DiagramGridLayout.GridNode ChildC = Lower[0];
            DiagramGridLayout.GridNode ChildD = Lower[1];
            if (!IsCompleteBipartite(Contract, ParentA.Id, ParentB.Id, ChildC.Id, ChildD.Id))
            {
                continue;
            }

            int CrossingsDirect = CountPairCrossings(
                ParentA.Column,
                ParentB.Column,
                ChildC.Column,
                ChildD.Column);
            int CrossingsSwap = CountPairCrossings(
                ParentA.Column,
                ParentB.Column,
                ChildD.Column,
                ChildC.Column);
            if (CrossingsSwap < CrossingsDirect)
            {
                DiagramGridLayout.GridNode Swap = ChildC;
                ChildC = ChildD;
                ChildD = Swap;
            }

            ChildC.Column = ParentA.Column;
            ChildD.Column = ParentB.Column;
        }
    }

    private static void ApplyManyToManyOrdering(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, DiagramGridLayout.GridNode> ById)
    {
        int RankCount = MaxRank(Placed);
        for (int Rank = 0; Rank + 1 < RankCount; Rank++)
        {
            List<DiagramGridLayout.GridNode> Upper = NodesInRank(Placed, Rank);
            List<DiagramGridLayout.GridNode> Lower = NodesInRank(Placed, Rank + 1);
            if (Upper.Count < 2 || Lower.Count < 2)
            {
                continue;
            }

            if (!HasMultiEdgeBetweenRanks(Contract, Upper, Lower))
            {
                continue;
            }

            List<DiagramGridLayout.GridNode> SortedUpper = new List<DiagramGridLayout.GridNode>(Upper);
            SortedUpper.Sort(delegate(DiagramGridLayout.GridNode Left, DiagramGridLayout.GridNode Right)
            {
                int ColumnCompare = Left.Column.CompareTo(Right.Column);
                if (ColumnCompare != 0)
                {
                    return ColumnCompare;
                }

                return CreationIndex(Contract, Left.Id).CompareTo(CreationIndex(Contract, Right.Id));
            });

            List<DiagramGridLayout.GridNode> OrderedLower = new List<DiagramGridLayout.GridNode>(Lower);
            OrderedLower.Sort(delegate(DiagramGridLayout.GridNode Left, DiagramGridLayout.GridNode Right)
            {
                double LeftMean = MeanParentColumn(Contract, ById, Left.Id);
                double RightMean = MeanParentColumn(Contract, ById, Right.Id);
                int Compare = LeftMean.CompareTo(RightMean);
                if (Compare != 0)
                {
                    return Compare;
                }

                return CreationIndex(Contract, Left.Id).CompareTo(CreationIndex(Contract, Right.Id));
            });

            if (SortedUpper.Count == OrderedLower.Count)
            {
                for (int Index = 0; Index < OrderedLower.Count; Index++)
                {
                    OrderedLower[Index].Column = SortedUpper[Index].Column;
                }

                continue;
            }

            foreach (DiagramGridLayout.GridNode Child in OrderedLower)
            {
                string? BestParent = SelectMostLinkedParent(Contract, ById, Child.Id, Upper);
                if (BestParent != null && ById.TryGetValue(BestParent, out DiagramGridLayout.GridNode? Parent))
                {
                    Child.Column = Parent.Column;
                }
            }
        }
    }

    private static void ApplyInRankCrossingSwaps(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, DiagramGridLayout.GridNode> ById)
    {
        int Passes = 8;
        while (Passes-- > 0)
        {
            bool Swapped = false;
            int RankCount = MaxRank(Placed);
            for (int Rank = 0; Rank < RankCount; Rank++)
            {
                List<DiagramGridLayout.GridNode> RankNodes = NodesInRank(Placed, Rank);
                RankNodes.Sort(delegate(DiagramGridLayout.GridNode Left, DiagramGridLayout.GridNode Right)
                {
                    return Left.Column.CompareTo(Right.Column);
                });

                for (int Index = 0; Index + 1 < RankNodes.Count; Index++)
                {
                    DiagramGridLayout.GridNode Left = RankNodes[Index];
                    DiagramGridLayout.GridNode Right = RankNodes[Index + 1];
                    int Before = CountRankCrossings(Contract, ById);
                    int LeftColumn = Left.Column;
                    int RightColumn = Right.Column;
                    Left.Column = RightColumn;
                    Right.Column = LeftColumn;
                    int After = CountRankCrossings(Contract, ById);
                    if (After < Before)
                    {
                        Swapped = true;
                        continue;
                    }

                    Left.Column = LeftColumn;
                    Right.Column = RightColumn;
                }
            }

            if (!Swapped)
            {
                break;
            }
        }
    }

    private static int ResolveSpineColumn(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, DiagramGridLayout.GridNode> ById)
    {
        List<string> Roots = FindRootIds(Contract, ById.Keys);
        if (Roots.Count == 0)
        {
            return 0;
        }

        string RootId = SelectPrimaryRoot(Contract, Roots);
        if (ById.TryGetValue(RootId, out DiagramGridLayout.GridNode? Root))
        {
            return Root.Column;
        }

        return 0;
    }

    private static HashSet<string> BuildSpineNodeIds(DiagramContract Contract, string RootId)
    {
        HashSet<string> SpineIds = new HashSet<string>(StringComparer.Ordinal);
        string? CurrentId = RootId;
        while (CurrentId != null)
        {
            if (!SpineIds.Add(CurrentId))
            {
                break;
            }

            CurrentId = SelectSpineChild(Contract, CurrentId);
        }

        return SpineIds;
    }

    private static string? SelectSpineChild(DiagramContract Contract, string SourceId)
    {
        List<EdgeContract> Outgoing = VisibleOutgoing(Contract, SourceId);
        if (Outgoing.Count == 0)
        {
            return null;
        }

        if (Outgoing.Count == 1)
        {
            return Outgoing[0].TargetId;
        }

        string? BestTarget = null;
        int BestDepth = -1;
        int BestIndex = int.MaxValue;
        foreach (EdgeContract Edge in Outgoing)
        {
            int Depth = LongestPathToSink(Contract, Edge.TargetId);
            int Index = Edge.CreationIndex;
            if (Depth > BestDepth || (Depth == BestDepth && Index < BestIndex))
            {
                BestDepth = Depth;
                BestIndex = Index;
                BestTarget = Edge.TargetId;
            }
        }

        return BestTarget;
    }

    private static int LongestPathToSink(DiagramContract Contract, string StartId)
    {
        Dictionary<string, int> Memo = new Dictionary<string, int>(StringComparer.Ordinal);
        return LongestPathToSinkCore(Contract, StartId, Memo);
    }

    private static int LongestPathToSinkCore(
        DiagramContract Contract,
        string NodeId,
        Dictionary<string, int> Memo)
    {
        if (Memo.TryGetValue(NodeId, out int Cached))
        {
            return Cached;
        }

        List<EdgeContract> Outgoing = VisibleOutgoing(Contract, NodeId);
        if (Outgoing.Count == 0)
        {
            Memo[NodeId] = 0;
            return 0;
        }

        int Best = 0;
        foreach (EdgeContract Edge in Outgoing)
        {
            int Depth = 1 + LongestPathToSinkCore(Contract, Edge.TargetId, Memo);
            if (Depth > Best)
            {
                Best = Depth;
            }
        }

        Memo[NodeId] = Best;
        return Best;
    }

    private static bool HasChildDirectlyBelow(
        DiagramContract Contract,
        Dictionary<string, DiagramGridLayout.GridNode> ById,
        DiagramGridLayout.GridNode Parent,
        int ChildRank)
    {
        foreach (string ChildId in VisibleChildren(Contract, Parent.Id))
        {
            if (!ById.TryGetValue(ChildId, out DiagramGridLayout.GridNode? Child))
            {
                continue;
            }

            if (Child.Rank == ChildRank && Child.Column == Parent.Column)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompleteBipartite(
        DiagramContract Contract,
        string ParentA,
        string ParentB,
        string ChildC,
        string ChildD)
    {
        return HasEdge(Contract, ParentA, ChildC)
            && HasEdge(Contract, ParentA, ChildD)
            && HasEdge(Contract, ParentB, ChildC)
            && HasEdge(Contract, ParentB, ChildD);
    }

    private static bool HasMultiEdgeBetweenRanks(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Upper,
        List<DiagramGridLayout.GridNode> Lower)
    {
        HashSet<string> UpperIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> LowerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Upper)
        {
            UpperIds.Add(Node.Id);
        }

        foreach (DiagramGridLayout.GridNode Node in Lower)
        {
            LowerIds.Add(Node.Id);
        }

        int EdgeCount = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisible(Edge))
            {
                continue;
            }

            bool Forward = UpperIds.Contains(Edge.SourceId) && LowerIds.Contains(Edge.TargetId);
            if (Forward)
            {
                EdgeCount++;
            }
        }

        return EdgeCount > Math.Max(Upper.Count, Lower.Count);
    }

    private static string? SelectMostLinkedParent(
        DiagramContract Contract,
        Dictionary<string, DiagramGridLayout.GridNode> ById,
        string ChildId,
        List<DiagramGridLayout.GridNode> Upper)
    {
        string? BestParent = null;
        int BestCount = 0;
        int BestIndex = int.MaxValue;
        foreach (DiagramGridLayout.GridNode Parent in Upper)
        {
            if (!HasEdge(Contract, Parent.Id, ChildId))
            {
                continue;
            }

            int Count = CountEdgesBetween(Contract, Parent.Id, ChildId);
            int Index = CreationIndex(Contract, Parent.Id);
            if (Count > BestCount || (Count == BestCount && Index < BestIndex))
            {
                BestCount = Count;
                BestIndex = Index;
                BestParent = Parent.Id;
            }
        }

        return BestParent;
    }

    private static int CountEdgesBetween(DiagramContract Contract, string SourceId, string TargetId)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisible(Edge))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }

    private static double MeanParentColumn(
        DiagramContract Contract,
        Dictionary<string, DiagramGridLayout.GridNode> ById,
        string ChildId)
    {
        double Sum = 0.0;
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisible(Edge) || !string.Equals(Edge.TargetId, ChildId, StringComparison.Ordinal))
            {
                continue;
            }

            if (ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Parent))
            {
                Sum += Parent.Column;
                Count++;
            }
        }

        if (Count == 0)
        {
            return 0.0;
        }

        return Sum / Count;
    }

    private static int CountPairCrossings(int ParentA, int ParentB, int ChildC, int ChildD)
    {
        int Crossings = 0;
        if (ParentA < ParentB && ChildC > ChildD)
        {
            Crossings++;
        }

        if (ParentA > ParentB && ChildC < ChildD)
        {
            Crossings++;
        }

        return Crossings;
    }

    private static int CountRankCrossings(
        DiagramContract Contract,
        Dictionary<string, DiagramGridLayout.GridNode> ById)
    {
        int Crossings = 0;
        List<EdgeContract> Visible = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!IsInvisible(Edge))
            {
                Visible.Add(Edge);
            }
        }

        for (int Left = 0; Left < Visible.Count; Left++)
        {
            for (int Right = Left + 1; Right < Visible.Count; Right++)
            {
                EdgeContract A = Visible[Left];
                EdgeContract B = Visible[Right];
                if (!ById.TryGetValue(A.SourceId, out DiagramGridLayout.GridNode? SourceA)
                    || !ById.TryGetValue(A.TargetId, out DiagramGridLayout.GridNode? TargetA)
                    || !ById.TryGetValue(B.SourceId, out DiagramGridLayout.GridNode? SourceB)
                    || !ById.TryGetValue(B.TargetId, out DiagramGridLayout.GridNode? TargetB))
                {
                    continue;
                }

                if (SourceA.Rank != SourceB.Rank || TargetA.Rank != TargetB.Rank)
                {
                    continue;
                }

                bool Inverted = (SourceA.Column < SourceB.Column && TargetA.Column > TargetB.Column)
                    || (SourceA.Column > SourceB.Column && TargetA.Column < TargetB.Column);
                if (Inverted)
                {
                    Crossings++;
                }
            }
        }

        return Crossings;
    }

    private static List<string> FindRootIds(DiagramContract Contract, IEnumerable<string> NodeIds)
    {
        HashSet<string> Targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!IsInvisible(Edge))
            {
                Targets.Add(Edge.TargetId);
            }
        }

        List<string> Roots = [];
        foreach (string NodeId in NodeIds)
        {
            if (!Targets.Contains(NodeId))
            {
                Roots.Add(NodeId);
            }
        }

        return Roots;
    }

    private static string SelectPrimaryRoot(DiagramContract Contract, List<string> Roots)
    {
        Roots.Sort(delegate(string Left, string Right)
        {
            return CreationIndex(Contract, Left).CompareTo(CreationIndex(Contract, Right));
        });
        return Roots[0];
    }

    private static List<string> VisibleChildren(DiagramContract Contract, string ParentId)
    {
        List<string> Children = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisible(Edge) || !string.Equals(Edge.SourceId, ParentId, StringComparison.Ordinal))
            {
                continue;
            }

            Children.Add(Edge.TargetId);
        }

        return Children;
    }

    private static List<string> VisibleParents(DiagramContract Contract, string ChildId)
    {
        List<string> Parents = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisible(Edge) || !string.Equals(Edge.TargetId, ChildId, StringComparison.Ordinal))
            {
                continue;
            }

            Parents.Add(Edge.SourceId);
        }

        return Parents;
    }

    private static List<EdgeContract> VisibleOutgoing(DiagramContract Contract, string SourceId)
    {
        List<EdgeContract> Outgoing = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisible(Edge) || !string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal))
            {
                continue;
            }

            Outgoing.Add(Edge);
        }

        Outgoing.Sort(delegate(EdgeContract Left, EdgeContract Right)
        {
            return Left.CreationIndex.CompareTo(Right.CreationIndex);
        });
        return Outgoing;
    }

    private static bool HasEdge(DiagramContract Contract, string SourceId, string TargetId)
    {
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (IsInvisible(Edge))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInvisible(EdgeContract Edge)
    {
        return string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);
    }

    private static int CreationIndex(DiagramContract Contract, string NodeId)
    {
        int Earliest = int.MaxValue;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                || string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                Earliest = Math.Min(Earliest, Edge.CreationIndex);
            }
        }

        return Earliest == int.MaxValue ? 0 : Earliest;
    }

    private static int MaxRank(List<DiagramGridLayout.GridNode> Placed)
    {
        int Max = 0;
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            Max = Math.Max(Max, Node.Rank + 1);
        }

        return Max;
    }

    private static List<DiagramGridLayout.GridNode> NodesInRank(List<DiagramGridLayout.GridNode> Placed, int Rank)
    {
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            if (Node.Rank == Rank)
            {
                Nodes.Add(Node);
            }
        }

        return Nodes;
    }

    private static Dictionary<string, DiagramGridLayout.GridNode> IndexPlaced(List<DiagramGridLayout.GridNode> Placed)
    {
        Dictionary<string, DiagramGridLayout.GridNode> ById = new Dictionary<string, DiagramGridLayout.GridNode>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            ById[Node.Id] = Node;
        }

        return ById;
    }
}
