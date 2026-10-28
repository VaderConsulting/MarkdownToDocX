using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds parallel diagram column node chains ordered by primary flow (F73 parity).
/// </summary>
public static class FlowchartParallelColumnChainBuilder
{
    /// <summary>
    /// Builds flow-ordered node chains for each vertical column cluster.
    /// </summary>
    public static List<List<string>> BuildColumnChains(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double ColumnClusterTolerancePx,
        double VerticalStackMinSeparationPx)
    {
        List<FlowchartLayoutNodeBounds> OrderedByColumnAxis = [.. BoundsById.Values.OrderBy(Bounds => Bounds.CenterX)];

        List<List<FlowchartLayoutNodeBounds>> ColumnBounds = [];
        List<FlowchartLayoutNodeBounds> CurrentColumn = [];

        foreach (FlowchartLayoutNodeBounds Bounds in OrderedByColumnAxis)
        {
            if (CurrentColumn.Count == 0)
            {
                CurrentColumn.Add(Bounds);
                continue;
            }

            double PreviousCenterX = CurrentColumn[CurrentColumn.Count - 1].CenterX;
            if (Math.Abs(Bounds.CenterX - PreviousCenterX) <= ColumnClusterTolerancePx)
            {
                CurrentColumn.Add(Bounds);
            }
            else
            {
                ColumnBounds.Add(CurrentColumn);
                CurrentColumn = [Bounds];
            }
        }

        if (CurrentColumn.Count > 0)
        {
            ColumnBounds.Add(CurrentColumn);
        }

        List<List<string>> Chains = [];
        foreach (List<FlowchartLayoutNodeBounds> Column in ColumnBounds)
        {
            if (Column.Count < 2)
            {
                continue;
            }

            HashSet<string> ColumnNodeIds = new HashSet<string>(
                Column.Select(Bounds => Bounds.NodeId),
                StringComparer.Ordinal);
            List<string> FlowOrderedNodeIds = OrderNodesByColumnFlow(Contract, ColumnNodeIds);
            if (FlowOrderedNodeIds.Count < 2)
            {
                continue;
            }

            List<FlowchartLayoutNodeBounds> OrderedTopToBottom = [];
            foreach (string NodeId in FlowOrderedNodeIds)
            {
                if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                {
                    OrderedTopToBottom.Add(Bounds);
                }
            }

            if (!IsVerticalStack(OrderedTopToBottom, VerticalStackMinSeparationPx))
            {
                continue;
            }

            Chains.Add(FlowOrderedNodeIds);
        }

        return Chains;
    }

    private static List<string> OrderNodesByColumnFlow(
        DiagramContract Contract,
        HashSet<string> ColumnNodeIds)
    {
        Dictionary<string, int> InDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string NodeId in ColumnNodeIds)
        {
            InDegree[NodeId] = 0;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ColumnNodeIds.Contains(Edge.SourceId) && ColumnNodeIds.Contains(Edge.TargetId))
            {
                InDegree[Edge.TargetId]++;
            }
        }

        List<string> Ordered = [];
        Queue<string> Ready = new Queue<string>();
        foreach (KeyValuePair<string, int> Entry in InDegree.OrderBy(Entry => GetEarliestEdgeCreationIndex(Contract, Entry.Key)))
        {
            if (Entry.Value == 0)
            {
                Ready.Enqueue(Entry.Key);
            }
        }

        while (Ready.Count > 0)
        {
            string NodeId = Ready.Dequeue();
            Ordered.Add(NodeId);
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ColumnNodeIds.Contains(Edge.TargetId))
                {
                    continue;
                }

                InDegree[Edge.TargetId]--;
                if (InDegree[Edge.TargetId] == 0)
                {
                    Ready.Enqueue(Edge.TargetId);
                }
            }
        }

        if (Ordered.Count < ColumnNodeIds.Count)
        {
            foreach (string NodeId in ColumnNodeIds.OrderBy(Candidate => GetEarliestEdgeCreationIndex(Contract, Candidate)))
            {
                if (!Ordered.Contains(NodeId))
                {
                    Ordered.Add(NodeId);
                }
            }
        }

        return Ordered;
    }

    private static int GetEarliestEdgeCreationIndex(DiagramContract Contract, string NodeId)
    {
        int Earliest = int.MaxValue;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                || string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                if (Edge.CreationIndex < Earliest)
                {
                    Earliest = Edge.CreationIndex;
                }
            }
        }

        return Earliest == int.MaxValue ? 0 : Earliest;
    }

    private static bool IsVerticalStack(
        List<FlowchartLayoutNodeBounds> OrderedTopToBottom,
        double VerticalStackMinSeparationPx)
    {
        for (int Index = 1; Index < OrderedTopToBottom.Count; Index++)
        {
            double UpperCenterY = OrderedTopToBottom[Index - 1].CenterY;
            double LowerCenterY = OrderedTopToBottom[Index].CenterY;
            if (Math.Abs(UpperCenterY - LowerCenterY) < VerticalStackMinSeparationPx)
            {
                return false;
            }
        }

        return true;
    }
}
