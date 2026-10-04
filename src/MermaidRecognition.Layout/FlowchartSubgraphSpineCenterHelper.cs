using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves the primary internal flow column centre for subgraph member centreing.
/// </summary>
public static class FlowchartSubgraphSpineCenterHelper
{
    private const double COLUMN_TOLERANCE_PX = 1.0;

    /// <summary>
    /// Returns true when the group has a detectable primary internal column with at least two members.
    /// </summary>
    public static bool TryGetPrimaryColumnCenterX(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        out double CenterX)
    {
        CenterX = 0.0;
        HashSet<string> MemberNodeIds = CollectMemberNodeIds(Group, Contract);
        if (MemberNodeIds.Count < 2)
        {
            return false;
        }

        Dictionary<double, List<string>> NodeIdsByColumnX =
            [];

        foreach (string NodeId in MemberNodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double ColumnKey = Math.Round(Bounds.CenterX / COLUMN_TOLERANCE_PX) * COLUMN_TOLERANCE_PX;
            if (!NodeIdsByColumnX.TryGetValue(ColumnKey, out List<string>? ColumnNodeIds))
            {
                ColumnNodeIds = [];
                NodeIdsByColumnX[ColumnKey] = ColumnNodeIds;
            }

            ColumnNodeIds.Add(NodeId);
        }

        if (NodeIdsByColumnX.Count == 0)
        {
            return false;
        }

        List<string> BestColumnNodeIds = NodeIdsByColumnX.Values
            .OrderByDescending(ColumnNodeIds => ColumnNodeIds.Count)
            .ThenByDescending(ColumnNodeIds => ScoreVerticalChainSpan(ColumnNodeIds, BoundsById))
            .First();

        if (BestColumnNodeIds.Count < 2)
        {
            return false;
        }

        double TotalCenterX = 0.0;
        int Count = 0;
        foreach (string NodeId in BestColumnNodeIds)
        {
            if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                TotalCenterX += Bounds.CenterX;
                Count++;
            }
        }

        if (Count == 0)
        {
            return false;
        }

        CenterX = TotalCenterX / Count;
        return true;
    }

    /// <summary>
    /// Returns true when the group has a detectable primary internal row with at least two members.
    /// </summary>
    public static bool TryGetPrimaryRowCenterY(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        out double CenterY)
    {
        CenterY = 0.0;
        HashSet<string> MemberNodeIds = CollectMemberNodeIds(Group, Contract);
        if (MemberNodeIds.Count < 2)
        {
            return false;
        }

        Dictionary<double, List<string>> NodeIdsByRowY =
            [];

        foreach (string NodeId in MemberNodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double RowKey = Math.Round(Bounds.CenterY / COLUMN_TOLERANCE_PX) * COLUMN_TOLERANCE_PX;
            if (!NodeIdsByRowY.TryGetValue(RowKey, out List<string>? RowNodeIds))
            {
                RowNodeIds = [];
                NodeIdsByRowY[RowKey] = RowNodeIds;
            }

            RowNodeIds.Add(NodeId);
        }

        if (NodeIdsByRowY.Count == 0)
        {
            return false;
        }

        List<string> BestRowNodeIds = NodeIdsByRowY.Values
            .OrderByDescending(RowNodeIds => RowNodeIds.Count)
            .ThenByDescending(RowNodeIds => ScoreHorizontalChainSpan(RowNodeIds, BoundsById))
            .First();

        if (BestRowNodeIds.Count < 2)
        {
            return false;
        }

        double TotalCenterY = 0.0;
        int Count = 0;
        foreach (string NodeId in BestRowNodeIds)
        {
            if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                TotalCenterY += Bounds.CenterY;
                Count++;
            }
        }

        if (Count == 0)
        {
            return false;
        }

        CenterY = TotalCenterY / Count;
        return true;
    }

    /// <summary>
    /// Expands vertical content bounds so the primary row sits in the vertical centre of the cluster.
    /// </summary>
    public static void ExpandVerticalBoundsAroundPrimaryRow(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        ref double MinY,
        ref double MaxY)
    {
        if (!TryGetPrimaryRowCenterY(Contract, Group, BoundsById, out double RowCenterY))
        {
            return;
        }

        if (IsColumnLed(Contract, Group, BoundsById))
        {
            return;
        }

        double BottomExtent = RowCenterY - MinY;
        double TopExtent = MaxY - RowCenterY;
        double HalfSpan = Math.Max(BottomExtent, TopExtent);
        MinY = RowCenterY - HalfSpan;
        MaxY = RowCenterY + HalfSpan;
    }

    /// <summary>
    /// Returns true when the group's longest column has at least as many members as its longest row.
    /// The box of a column-led group is centred on its column only, never on a row (F59).
    /// </summary>
    public static bool IsColumnLed(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        return CountLargestAlignedSet(Contract, Group, BoundsById, ByColumn: true) >= CountLargestAlignedSet(Contract, Group, BoundsById, ByColumn: false);
    }

    /// <summary>
    /// Returns the size of the largest set of members that share one column (or one row).
    /// A top-to-bottom group whose column is at least as long as any row is column-led, so its box must not be centred on a row (F59).
    /// </summary>
    private static int CountLargestAlignedSet(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool ByColumn)
    {
        Dictionary<double, int> CountByKey = [];
        foreach (string NodeId in CollectMemberNodeIds(Group, Contract))
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double Coordinate = ByColumn ? Bounds.CenterX : Bounds.CenterY;
            double Key = Math.Round(Coordinate / COLUMN_TOLERANCE_PX) * COLUMN_TOLERANCE_PX;
            CountByKey[Key] = CountByKey.GetValueOrDefault(Key) + 1;
        }

        int Largest = 0;
        foreach (int Count in CountByKey.Values)
        {
            if (Count > Largest)
            {
                Largest = Count;
            }
        }

        return Largest;
    }

    /// <summary>
    /// Expands horizontal content bounds so the primary column sits in the horizontal centre of the cluster.
    /// </summary>
    public static void ExpandHorizontalBoundsAroundPrimaryColumn(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        ref double MinX,
        ref double MaxX)
    {
        if (!TryGetPrimaryColumnCenterX(Contract, Group, BoundsById, out double ColumnCenterX))
        {
            return;
        }

        double LeftExtent = ColumnCenterX - MinX;
        double RightExtent = MaxX - ColumnCenterX;
        double HalfSpan = Math.Max(LeftExtent, RightExtent);
        MinX = ColumnCenterX - HalfSpan;
        MaxX = ColumnCenterX + HalfSpan;
    }

    private static double ScoreHorizontalChainSpan(
        List<string> RowNodeIds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double MinX = double.MaxValue;
        double MaxX = double.MinValue;
        foreach (string NodeId in RowNodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            if (Left < MinX)
            {
                MinX = Left;
            }

            if (Right > MaxX)
            {
                MaxX = Right;
            }
        }

        if (MinX == double.MaxValue)
        {
            return 0.0;
        }

        return MaxX - MinX;
    }

    private static double ScoreVerticalChainSpan(
        List<string> ColumnNodeIds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double MinY = double.MaxValue;
        double MaxY = double.MinValue;
        foreach (string NodeId in ColumnNodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (Bottom < MinY)
            {
                MinY = Bottom;
            }

            if (Top > MaxY)
            {
                MaxY = Top;
            }
        }

        if (MinY == double.MaxValue)
        {
            return 0.0;
        }

        return MaxY - MinY;
    }

    private static HashSet<string> CollectMemberNodeIds(GroupContract Group, DiagramContract Contract)
    {
        Dictionary<string, GroupContract> GroupById =
            FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        HashSet<string> MemberNodeIds = new HashSet<string>(StringComparer.Ordinal);
        CollectMemberNodeIdsRecursive(Group, Contract, GroupById, MemberNodeIds);
        return MemberNodeIds;
    }

    private static void CollectMemberNodeIdsRecursive(
        GroupContract Group,
        DiagramContract Contract,
        Dictionary<string, GroupContract> GroupById,
        HashSet<string> MemberNodeIds)
    {
        foreach (string NodeId in Group.ChildNodeIds)
        {
            MemberNodeIds.Add(NodeId);
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                MemberNodeIds.Add(Node.Id);
            }
        }

        foreach (GroupContract CandidateGroup in GroupById.Values)
        {
            if (!string.Equals(CandidateGroup.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            CollectMemberNodeIdsRecursive(CandidateGroup, Contract, GroupById, MemberNodeIds);
        }
    }
}
