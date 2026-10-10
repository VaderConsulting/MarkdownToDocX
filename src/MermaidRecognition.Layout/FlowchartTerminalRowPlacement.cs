using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Uses empty slots beside a fan whose other branch reaches a shared collector.</summary>
internal static class FlowchartTerminalRowPlacement
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        // Group frames and junction branches have their own placement rules.
        if (Contract.Groups.Count > 0 || Nodes.Any(Node => Node.IsDiamond || Node.Shape is not (null or "" or "rect" or "rounded" or "round"))) return;
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        foreach (var Leaf in Nodes)
        {
            if (Edges.Any(Edge => Edge.SourceId == Leaf.Id)) continue;
            var Incoming = Edges.Where(Edge => Edge.TargetId == Leaf.Id).ToList();
            if (Incoming.Count != 1 || Back.Contains(Incoming[0])
                || !ById.TryGetValue(Incoming[0].SourceId, out var Parent) || Parent.Rank >= Leaf.Rank) continue;
            int Side = Math.Sign(Cross(Leaf) - Cross(Parent));
            if (Side == 0) continue;
            var Children = Edges.Where(Edge => Edge.SourceId == Parent.Id && Edge.TargetId != Leaf.Id)
                .Select(Edge => ById.GetValueOrDefault(Edge.TargetId)).ToList();
            if (Children.Count == 0 || Children.Any(Child => Child == null || Child.Rank != Leaf.Rank
                || Math.Sign(Cross(Child) - Cross(Parent)) != -Side
                || Edges.Any(Edge => Edge.SourceId == Child.Id)
                || !Edges.Any(Edge => Edge.TargetId == Child.Id && Edge.SourceId != Parent.Id
                    && ById.TryGetValue(Edge.SourceId, out var Peer) && Peer.Rank == Parent.Rank))) continue;
            double Candidate = Cross(Parent) + Side * DiagramGridMetrics.CeilToGrid(Half(Parent) + Half(Leaf) + DiagramGridMetrics.NODE_GAP_PX);
            // Fill a bounded hole in the existing row, without adding a column.
            if (!Nodes.Any(Other => Other != Leaf && Other.Rank == Parent.Rank
                && Side * (Cross(Other) - Candidate) > 0)) continue;
            if (Nodes.Any(Other => Other != Leaf && Other != Parent && Other.Rank == Parent.Rank
                && Side * (Cross(Other) - Cross(Parent)) > 0
                && Side * (Cross(Other) - Candidate) <= 0)) continue;
            if (Nodes.Any(Other => Other != Leaf && Other.Rank == Parent.Rank
                && Math.Abs(Cross(Other) - Candidate) < Half(Other) + Half(Leaf) + DiagramGridMetrics.NODE_GAP_PX)) continue;
            Leaf.Rank = Parent.Rank;
            if (Horizontal) { Leaf.X = Parent.X; Leaf.Y = Candidate; }
            else { Leaf.Y = Parent.Y; Leaf.X = Candidate; }
        }
    }
}
