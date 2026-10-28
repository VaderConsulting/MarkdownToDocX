using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Places independent inputs beside a terminal collector's outer side.</summary>
internal static partial class FlowchartTerminalInputPlacement
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        bool Horizontal, int CrossPitch)
    {
        if (Nodes.Any(Node => Node.CellNode != null))
        {
            ApplyCells(Contract, Nodes, Horizontal, CrossPitch);
            return;
        }
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        List<EdgeContract> Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        foreach (DiagramGridLayout.GridNode Root in Nodes)
        {
            if (Root.IsDiamond || Edges.Any(Edge => Edge.TargetId == Root.Id))
            {
                continue;
            }

            List<EdgeContract> Outgoing = [.. Edges.Where(Edge => Edge.SourceId == Root.Id)];
            if (Outgoing.Count != 1 || Back.Contains(Outgoing[0])
                || !ById.TryGetValue(Outgoing[0].TargetId, out DiagramGridLayout.GridNode? Target)
                || Target.IsDiamond || Target.GroupId != Root.GroupId || Target.Rank <= Root.Rank
                || Edges.Any(Edge => Edge.SourceId == Target.Id))
            {
                continue;
            }

            List<EdgeContract> Others = [.. Edges.Where(Edge => Edge.TargetId == Target.Id && Edge.SourceId != Root.Id)];
            if (Others.Count == 0 || Others.Any(Edge => Back.Contains(Edge)
                || !ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Parent) || Parent.GroupId != Root.GroupId
                || Parent.Rank >= Target.Rank || !Edges.Any(Incoming => Incoming.TargetId == Parent.Id)))
            {
                continue;
            }
            // Keep a main path's terminal input upstream. Only a side branch
            // leaves a separate straight continuation to preserve on the spine.
            if (!Others.Any(Edge => Edges.Count(Outgoing => Outgoing.SourceId == Edge.SourceId) > 1))
            {
                continue;
            }

            double Centre = Cross(Target);
            int Side = Math.Sign(Centre - Others.Average(Edge => Cross(ById[Edge.SourceId])));
            if (Side == 0)
            {
                continue;
            }

            for (int Step = 1; Step <= Nodes.Count + 1; Step++)
            {
                double Candidate = Centre + (Side * Step * CrossPitch);
                if (Nodes.Any(Other => Other != Root && Other.Rank == Target.Rank
                    && Math.Abs(Cross(Other) - Candidate) < Half(Other) + Half(Root) + DiagramGridMetrics.NODE_GAP_PX))
                {
                    continue;
                }

                Root.Rank = Target.Rank;
                if (Horizontal) { Root.X = Target.X; Root.Y = (int)Math.Round(Candidate); }
                else { Root.Y = Target.Y; Root.X = (int)Math.Round(Candidate); }
                break;
            }
        }
    }
}
