using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Keeps terminal branches beside a parent's straight continuation.</summary>
internal static partial class FlowchartTerminalBranchPlacement
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        bool Horizontal, int CrossPitch)
    {
        if (Nodes.Any(Node => Node.CellNode != null))
        {
            ApplyCells(Contract, Nodes, Horizontal, CrossPitch);
            return;
        }
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
            if (!HasContinuation)
            {
                // A shared collector owns the side towards its other inputs.
                // Keep a singly connected terminal on the parent's opposite side.
                var Collectors = Edges.Where(Edge => Edge.SourceId == Parent.Id && Edge.TargetId != Leaf.Id)
                    .Select(Edge => ById.GetValueOrDefault(Edge.TargetId))
                    .Where(Child => Child != null && Child.Rank == Leaf.Rank && Child.GroupId == Leaf.GroupId
                        && !Child.IsDiamond && !Edges.Any(Next => Next.SourceId == Child.Id)
                        && Edges.Any(Incoming => Incoming.TargetId == Child.Id && Incoming.SourceId != Parent.Id
                            && ById.TryGetValue(Incoming.SourceId, out var Input) && Input.Rank == Parent.Rank
                            && Input.GroupId == Parent.GroupId)).ToList();
                if (Collectors.Count == 0) continue;
                int Side = -Math.Sign(Collectors.Average(Child => Cross(Child!)) - Cross(Parent));
                if (Side == 0 || Collectors.Any(Child => Math.Sign(Cross(Child!) - Cross(Parent)) != -Side)) continue;
                for (int Step = 1; Step <= Nodes.Count + 1; Step++)
                {
                    int Pitch = Math.Max(CrossPitch, DiagramGridMetrics.CeilToGrid(Half(Parent) + Half(Leaf)
                        + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX));
                    double Candidate = Cross(Parent) + Side * Step * Pitch;
                    if (Math.Abs(Candidate - Cross(Parent)) > Math.Abs(Cross(Leaf) - Cross(Parent)) + 0.5) break;
                    if (Nodes.Any(Other => Other != Leaf && Other.Rank == Leaf.Rank
                        && Math.Abs(Cross(Other) - Candidate) < Half(Other) + Half(Leaf) + DiagramGridMetrics.NODE_GAP_PX)) continue;
                    if (Horizontal) Leaf.Y = (int)Math.Round(Candidate); else Leaf.X = (int)Math.Round(Candidate);
                    break;
                }
                continue;
            }
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
