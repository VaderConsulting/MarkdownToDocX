using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Rules 2.24 and 2.25: prefer stacked chains and nearby single-neighbour nodes when scores tie.</summary>
internal static class FlowchartChainPlacementTie
{
    internal static double Measure(DiagramContract Contract, IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = NodeBounds
            .GroupBy(Bounds => Bounds.NodeId, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        List<EdgeContract> VisibleEdges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        double Cost = 0.0;
        Cost += ChainColumnSpreadCost(Contract, BoundsById);
        Cost += LonelyNeighbourDistanceCost(VisibleEdges, BoundsById);
        return Cost;
    }

    private static double ChainColumnSpreadCost(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        List<List<string>> Chains = FlowchartParallelColumnChainBuilder.BuildColumnChains(
            Contract,
            BoundsById,
            DiagramGridMetrics.GRID_UNIT_PX,
            DiagramGridMetrics.GRID_UNIT_PX / 2.0);
        double Cost = 0.0;
        foreach (List<string> Chain in Chains)
        {
            if (Chain.Count < 2)
            {
                continue;
            }

            List<double> Columns = [.. Chain
                .Where(BoundsById.ContainsKey)
                .Select(NodeId => BoundsById[NodeId].CenterX)];
            if (Columns.Count < 2)
            {
                continue;
            }

            double Span = Columns.Max() - Columns.Min();
            Cost += Span * (Chain.Count - 1);
        }

        return Cost;
    }

    private static double LonelyNeighbourDistanceCost(
        List<EdgeContract> VisibleEdges,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        Dictionary<string, HashSet<string>> Neighbours = new(StringComparer.Ordinal);
        foreach (EdgeContract Edge in VisibleEdges)
        {
            AddNeighbour(Neighbours, Edge.SourceId, Edge.TargetId);
            AddNeighbour(Neighbours, Edge.TargetId, Edge.SourceId);
        }

        double Cost = 0.0;
        foreach (KeyValuePair<string, HashSet<string>> Pair in Neighbours)
        {
            if (Pair.Value.Count != 1 || !BoundsById.TryGetValue(Pair.Key, out FlowchartLayoutNodeBounds? Node))
            {
                continue;
            }

            string PeerId = Pair.Value.First();
            if (!BoundsById.TryGetValue(PeerId, out FlowchartLayoutNodeBounds? Peer))
            {
                continue;
            }

            Cost += Math.Abs(Node.CenterX - Peer.CenterX) + Math.Abs(Node.CenterY - Peer.CenterY);
        }

        return Cost;
    }

    private static void AddNeighbour(Dictionary<string, HashSet<string>> Neighbours, string Left, string Right)
    {
        if (!Neighbours.TryGetValue(Left, out HashSet<string>? Set))
        {
            Set = new HashSet<string>(StringComparer.Ordinal);
            Neighbours[Left] = Set;
        }

        Set.Add(Right);
    }
}
