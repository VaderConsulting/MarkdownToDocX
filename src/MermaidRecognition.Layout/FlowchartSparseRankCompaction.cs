using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Reuses columns across disjoint ranks in wide, sparsely occupied grids.</summary>
internal static class FlowchartSparseRankCompaction
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Contract.Groups.Count > 0 || Nodes.Count == 0) return;
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Main(DiagramGridLayout.GridNode Node) => Horizontal ? Node.X : Node.Y;
        double HalfCross(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        double HalfMain(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Width : Node.Height) / 2.0;
        var Bands = Nodes.GroupBy(Cross).OrderBy(Band => Band.Key).Select(Band => Band.ToList()).ToList();
        int OccupiedColumns = Nodes.GroupBy(Node => Node.Rank).Max(Rank => Rank.Count());
        if (Bands.Count <= 2 * OccupiedColumns) return;
        double Origin = Bands[0].Max(HalfCross);
        var ById = Nodes.ToDictionary(Node => Node.Id);
        // A rank-skipping connector needs its existing side corridor. Adjacent
        // ranks can reuse columns and use normal upstream/downstream ports.
        var Related = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .Where(Edge => ById.TryGetValue(Edge.SourceId, out var Source) && ById.TryGetValue(Edge.TargetId, out var Target)
                && Math.Abs(Source.Rank - Target.Rank) > 1)
            .SelectMany(Edge => new[] { (Edge.SourceId, Edge.TargetId), (Edge.TargetId, Edge.SourceId) }).ToHashSet();
        var Placed = new List<DiagramGridLayout.GridNode>();
        foreach (var Band in Bands)
        {
            double Candidate = Origin;
            foreach (var Node in Band)
            foreach (var Other in Placed)
                if (Related.Contains((Node.Id, Other.Id))
                    || Math.Abs(Main(Node) - Main(Other)) < HalfMain(Node) + HalfMain(Other) + DiagramGridMetrics.NODE_GAP_PX)
                    Candidate = Math.Max(Candidate, Cross(Other) + HalfCross(Other) + HalfCross(Node) + 2 * DiagramGridMetrics.NODE_GAP_PX);
            Candidate = Origin + Math.Ceiling((Candidate - Origin) / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX;
            foreach (var Node in Band)
            {
                if (Horizontal) Node.Y = Candidate; else Node.X = Candidate;
                Placed.Add(Node);
            }
        }
    }
}
