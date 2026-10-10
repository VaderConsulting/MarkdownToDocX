using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Places independent inputs beside a terminal collector's outer side.</summary>
internal static class FlowchartTerminalInputPlacement
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        bool Horizontal, int CrossPitch)
    {
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        foreach (var Root in Nodes)
        {
            if (Root.IsDiamond || Edges.Any(Edge => Edge.TargetId == Root.Id)) continue;
            var Outgoing = Edges.Where(Edge => Edge.SourceId == Root.Id).ToList();
            if (Outgoing.Count != 1 || Back.Contains(Outgoing[0])
                || !ById.TryGetValue(Outgoing[0].TargetId, out var Target)
                || Target.IsDiamond || Target.GroupId != Root.GroupId || Target.Rank <= Root.Rank
                || Edges.Any(Edge => Edge.SourceId == Target.Id)) continue;
            var Others = Edges.Where(Edge => Edge.TargetId == Target.Id && Edge.SourceId != Root.Id).ToList();
            if (Others.Count == 0 || Others.Any(Edge => Back.Contains(Edge)
                || !ById.TryGetValue(Edge.SourceId, out var Parent) || Parent.GroupId != Root.GroupId
                || Parent.Rank >= Target.Rank || !Edges.Any(Incoming => Incoming.TargetId == Parent.Id))) continue;
            double Centre = Cross(Target);
            int Side = Math.Sign(Centre - Others.Average(Edge => Cross(ById[Edge.SourceId])));
            if (Side == 0) continue;
            for (int Step = 1; Step <= Nodes.Count + 1; Step++)
            {
                double Candidate = Centre + Side * Step * CrossPitch;
                if (Nodes.Any(Other => Other != Root && Other.Rank == Target.Rank
                    && Math.Abs(Cross(Other) - Candidate) < Half(Other) + Half(Root) + DiagramGridMetrics.NODE_GAP_PX)) continue;
                Root.Rank = Target.Rank;
                if (Horizontal) { Root.X = Target.X; Root.Y = (int)Math.Round(Candidate); }
                else { Root.Y = Target.Y; Root.X = (int)Math.Round(Candidate); }
                break;
            }
        }
    }
}
