using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Keeps terminal branches beside a parent's straight continuation.</summary>
internal static class FlowchartTerminalBranchPlacement
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        bool Horizontal, int CrossPitch)
    {
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        foreach (var Leaf in Nodes)
        {
            if (Leaf.IsDiamond || Edges.Any(Edge => Edge.SourceId == Leaf.Id)) continue;
            var Incoming = Edges.Where(Edge => Edge.TargetId == Leaf.Id).ToList();
            if (Incoming.Count != 1 || !ById.TryGetValue(Incoming[0].SourceId, out var Parent)
                || Parent.IsDiamond || Parent.Rank >= Leaf.Rank || Parent.GroupId != Leaf.GroupId) continue;
            // A parent's longer bypass owns the space beside its intermediate
            // ranks. Only compact a fan whose children share the same rank.
            if (Edges.Any(Edge => Edge.SourceId == Parent.Id
                && ById.TryGetValue(Edge.TargetId, out var Child) && Child.Rank != Leaf.Rank)) continue;
            bool HasContinuation = Edges.Any(Edge => Edge.SourceId == Parent.Id && Edge.TargetId != Leaf.Id
                && ById.TryGetValue(Edge.TargetId, out var Child) && Child.Rank == Leaf.Rank
                && Child.GroupId == Leaf.GroupId && Math.Abs(Cross(Child) - Cross(Parent)) < 0.5
                && Edges.Any(Next => Next.SourceId == Child.Id));
            if (!HasContinuation) continue;
            double Best = Cross(Leaf), Centre = Cross(Parent);
            int Preferred = Math.Sign(Best - Centre);
            foreach (int Step in Enumerable.Range(1, Nodes.Count + 1))
            foreach (int Side in new[] { Preferred == 0 ? -1 : Preferred, Preferred == 0 ? 1 : -Preferred })
            {
                double Candidate = Centre + Side * Step * CrossPitch;
                if (Math.Abs(Candidate - Centre) >= Math.Abs(Best - Centre) - 0.5) continue;
                if (Nodes.Any(Other => Other != Leaf && Other.Rank == Leaf.Rank
                    && Math.Abs(Cross(Other) - Candidate) < Half(Other) + Half(Leaf) + DiagramGridMetrics.NODE_GAP_PX)) continue;
                Best = Candidate;
            }
            if (Horizontal) Leaf.Y = (int)Math.Round(Best); else Leaf.X = (int)Math.Round(Best);
        }
    }
}
