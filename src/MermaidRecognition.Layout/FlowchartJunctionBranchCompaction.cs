using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Closes unused space beside junctions while preserving branch order and aligned chains.</summary>
internal static partial class FlowchartJunctionBranchCompaction
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        // Grouped branches need frame clearance as well as body clearance.
        if (Contract.Groups.Count > 0)
        {
            return;
        }

        if (Nodes.Any(Node => Node.CellNode != null))
        {
            // Cell branch proposals compete in the complete routed score.
            // Placement does not force a junction or collector alignment.
            return;
        }
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        List<EdgeContract> Edges = [.. Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && ById.ContainsKey(Edge.SourceId) && ById.ContainsKey(Edge.TargetId))];
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Main(DiagramGridLayout.GridNode Node) => Horizontal ? Node.X : Node.Y;
        double HalfCross(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        double HalfMain(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Width : Node.Height) / 2.0;
        HashSet<DiagramGridLayout.GridNode> Moved = [];
        foreach (DiagramGridLayout.GridNode? Junction in Nodes.Where(Node => Node.Shape == "circle" && Node.GroupId == null))
        {
            List<EdgeContract> Outgoing = [.. Edges.Where(Edge => Edge.SourceId == Junction.Id)];
            if (Outgoing.Count < 2)
            {
                continue;
            }

            foreach (DiagramGridLayout.GridNode? Branch in Outgoing.Select(Edge => ById[Edge.TargetId])
                .OrderBy(Node => Math.Abs(Cross(Node) - Cross(Junction))))
            {
                if (Branch.GroupId != null || Branch.IsDiamond || Branch.Shape == "circle")
                {
                    continue;
                }

                int Side = Math.Sign(Cross(Branch) - Cross(Junction));
                if (Side == 0)
                {
                    continue;
                }

                HashSet<DiagramGridLayout.GridNode> Chain = [Branch];
                Queue<DiagramGridLayout.GridNode> Pending = new Queue<DiagramGridLayout.GridNode>();
                Pending.Enqueue(Branch);
                while (Pending.TryDequeue(out DiagramGridLayout.GridNode? Current))
                {
                    foreach (EdgeContract? Edge in Edges.Where(Edge => Edge.SourceId == Current.Id || Edge.TargetId == Current.Id))
                {
                        DiagramGridLayout.GridNode Other = ById[Edge.SourceId == Current.Id ? Edge.TargetId : Edge.SourceId];
                    if (Other.GroupId != null || Other.Shape == "circle" || Other.IsDiamond
                        || Math.Abs(Cross(Other) - Cross(Branch)) > 0.01 || !Chain.Add(Other))
                        {
                            continue;
                        }

                        Pending.Enqueue(Other);
                }
                }

                double Original = Cross(Branch);
                for (double Shift = DiagramGridMetrics.GRID_UNIT_PX; Shift < Math.Abs(Original - Cross(Junction));
                    Shift += DiagramGridMetrics.GRID_UNIT_PX)
                {
                    double Candidate = Original - (Side * Shift);
                    // An occupied rank owns its ordering; never jump across it.
                    if (Chain.Any(Node => Nodes.Any(Other => !Chain.Contains(Other)
                        && Math.Abs(Main(Node) - Main(Other)) < HalfMain(Node) + HalfMain(Other) + 0.01
                        && (Cross(Other) - Original) * (Cross(Other) - Candidate) <= 0)))
                    {
                        break;
                    }

                    if (Chain.Any(Node => Nodes.Any(Other => !Chain.Contains(Other)
                        && Math.Abs(Main(Node) - Main(Other)) < HalfMain(Node) + HalfMain(Other) + DiagramGridMetrics.NODE_GAP_PX
                        && Math.Abs(Cross(Other) - Candidate) < HalfCross(Node) + HalfCross(Other) + (2 * DiagramGridMetrics.NODE_GAP_PX))))
                    {
                        break;
                    }

                    foreach (DiagramGridLayout.GridNode Node in Chain)
                    {
                        if (Horizontal)
                        {
                            Node.Y = Candidate;
                        }
                        else
                        {
                            Node.X = Candidate;
                        }

                        Moved.Add(Node);
                    }
                }
            }
        }
        foreach (DiagramGridLayout.GridNode? Collector in Nodes.Where(Node => Node.Shape == "circle" && Node.GroupId == null))
        {
            List<DiagramGridLayout.GridNode> Parents = [.. Edges.Where(Edge => Edge.TargetId == Collector.Id).Select(Edge => ById[Edge.SourceId])];
            if (Parents.Count is < 2 or > 3 || !Parents.Any(Moved.Contains)
                || Parents.Any(Node => Node.GroupId != null) || Parents.Select(Main).Distinct().Count() != 1)
            {
                continue;
            }

            double Centre = Parents.Count == 2 ? Parents.Average(Cross) : Cross(Parents.OrderBy(Cross).ElementAt(1));
            double Candidate = Cross(Collector) + (Math.Round((Centre - Cross(Collector)) / DiagramGridMetrics.GRID_UNIT_PX)
                * DiagramGridMetrics.GRID_UNIT_PX);
            if (Nodes.Any(Other => Other != Collector
                && Math.Abs(Main(Other) - Main(Collector)) < HalfMain(Other) + HalfMain(Collector) + DiagramGridMetrics.NODE_GAP_PX
                && Math.Abs(Cross(Other) - Candidate) < HalfCross(Other) + HalfCross(Collector) + DiagramGridMetrics.NODE_GAP_PX))
            {
                continue;
            }

            List<DiagramGridLayout.GridNode> Tail = [Collector];
            DiagramGridLayout.GridNode Current = Collector;
            while (true)
            {
                List<EdgeContract> NextEdges = [.. Edges.Where(Edge => Edge.SourceId == Current.Id)];
                if (NextEdges.Count != 1)
                {
                    break;
                }

                DiagramGridLayout.GridNode Next = ById[NextEdges[0].TargetId];
                if (Next.GroupId != null || Next.Rank == Current.Rank || Tail.Contains(Next)
                    || Edges.Count(Edge => Edge.TargetId == Next.Id) != 1)
                {
                    break;
                }

                Tail.Add(Next); Current = Next;
            }
            if (Tail.Any(Node => Nodes.Any(Other => !Tail.Contains(Other)
                && Math.Abs(Main(Other) - Main(Node)) < HalfMain(Other) + HalfMain(Node) + DiagramGridMetrics.NODE_GAP_PX
                && Math.Abs(Cross(Other) - Candidate) < HalfCross(Other) + HalfCross(Node) + DiagramGridMetrics.NODE_GAP_PX)))
            {
                continue;
            }

            foreach (DiagramGridLayout.GridNode Node in Tail)
            {
                if (Horizontal)
                {
                    Node.Y = Candidate;
                }
                else
                {
                    Node.X = Candidate;
                }
            }
        }
    }
}
