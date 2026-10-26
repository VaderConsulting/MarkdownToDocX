using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Finds the visible edges that close a loop in a flowchart, the same way Dagre's depth-first acyclic pass does.
/// </summary>
public static class FlowchartBackEdgeHelper
{
    private sealed class CachedBackEdges
    {
        public required HashSet<EdgeContract> MutableItems { get; init; }

        public IReadOnlySet<EdgeContract> Items => MutableItems;
    }

    private static readonly ConditionalWeakTable<DiagramContract, CachedBackEdges> ContractCache = new();

    /// <summary>
    /// Returns the visible edges that point back to a node still on the depth-first path.
    /// The walk starts from nodes in the order their first edge was declared and follows outgoing edges in declaration order.
    /// The contract must not change after the first lookup; parse edits into a new contract instead.
    /// </summary>
    /// <param name="Contract">The parsed flowchart.</param>
    /// <returns>The back edges. Empty when the flowchart has no loops.</returns>
    public static IReadOnlySet<EdgeContract> FindBackEdges(DiagramContract Contract)
    {
        CachedBackEdges Cached = ContractCache.GetValue(Contract, ComputeBackEdges);
        DiagramContractLayoutCacheGuard.AssertUnchanged(Contract);
        return Cached.Items;
    }

    private static CachedBackEdges ComputeBackEdges(DiagramContract Contract)
    {
        HashSet<EdgeContract> BackEdges = new HashSet<EdgeContract>();
        if (Contract == null || Contract.Edges.Count == 0)
        {
            return new CachedBackEdges { MutableItems = BackEdges };
        }

        List<EdgeContract> VisibleEdges = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).OrderBy(Edge => Edge.CreationIndex).ToList();
        Dictionary<string, int> EarliestEdgeIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in VisibleEdges)
        {
            if (!EarliestEdgeIndexById.ContainsKey(Edge.SourceId))
            {
                EarliestEdgeIndexById[Edge.SourceId] = Edge.CreationIndex;
            }

            if (!EarliestEdgeIndexById.ContainsKey(Edge.TargetId))
            {
                EarliestEdgeIndexById[Edge.TargetId] = Edge.CreationIndex;
            }
        }

        HashSet<string> TargetIds = new HashSet<string>(VisibleEdges.Select(Edge => Edge.TargetId), StringComparer.Ordinal);
        List<string> StartOrder = EarliestEdgeIndexById.Keys.OrderBy(NodeId => TargetIds.Contains(NodeId) ? 1 : 0).ThenBy(NodeId => EarliestEdgeIndexById[NodeId]).ToList();
        Dictionary<string, int> StateById = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string StartId in StartOrder)
        {
            if (StateById.ContainsKey(StartId))
            {
                continue;
            }

            Stack<(string NodeId, int EdgeIndex)> Pending = new Stack<(string NodeId, int EdgeIndex)>();
            Pending.Push((StartId, 0));
            StateById[StartId] = 1;
            while (Pending.Count > 0)
            {
                (string NodeId, int EdgeIndex) = Pending.Pop();
                int NextIndex = VisibleEdges.FindIndex(EdgeIndex, Edge => string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal));
                if (NextIndex < 0)
                {
                    StateById[NodeId] = 2;
                    continue;
                }

                Pending.Push((NodeId, NextIndex + 1));
                EdgeContract NextEdge = VisibleEdges[NextIndex];
                if (!StateById.TryGetValue(NextEdge.TargetId, out int TargetState))
                {
                    StateById[NextEdge.TargetId] = 1;
                    Pending.Push((NextEdge.TargetId, 0));
                }
                else if (TargetState == 1)
                {
                    BackEdges.Add(NextEdge);
                }
            }
        }

        return new CachedBackEdges { MutableItems = BackEdges };
    }
}
