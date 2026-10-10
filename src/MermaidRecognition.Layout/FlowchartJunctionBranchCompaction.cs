using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Closes unused space beside junctions while preserving branch order and aligned chains.</summary>
internal static class FlowchartJunctionBranchCompaction
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        // Grouped branches need frame clearance as well as body clearance.
        if (Contract.Groups.Count > 0) return;
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && ById.ContainsKey(Edge.SourceId) && ById.ContainsKey(Edge.TargetId)).ToList();
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Main(DiagramGridLayout.GridNode Node) => Horizontal ? Node.X : Node.Y;
        double HalfCross(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        double HalfMain(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Width : Node.Height) / 2.0;
        var Moved = new HashSet<DiagramGridLayout.GridNode>();
        foreach (var Junction in Nodes.Where(Node => Node.Shape == "circle" && Node.GroupId == null))
        {
            var Outgoing = Edges.Where(Edge => Edge.SourceId == Junction.Id).ToList();
            if (Outgoing.Count < 2) continue;
            foreach (var Branch in Outgoing.Select(Edge => ById[Edge.TargetId])
                .OrderBy(Node => Math.Abs(Cross(Node) - Cross(Junction))))
            {
                if (Branch.GroupId != null || Branch.IsDiamond || Branch.Shape == "circle") continue;
                int Side = Math.Sign(Cross(Branch) - Cross(Junction));
                if (Side == 0) continue;
                var Chain = new HashSet<DiagramGridLayout.GridNode> { Branch };
                var Pending = new Queue<DiagramGridLayout.GridNode>();
                Pending.Enqueue(Branch);
                while (Pending.TryDequeue(out var Current))
                foreach (var Edge in Edges.Where(Edge => Edge.SourceId == Current.Id || Edge.TargetId == Current.Id))
                {
                    var Other = ById[Edge.SourceId == Current.Id ? Edge.TargetId : Edge.SourceId];
                    if (Other.GroupId != null || Other.Shape == "circle" || Other.IsDiamond
                        || Math.Abs(Cross(Other) - Cross(Branch)) > 0.01 || !Chain.Add(Other)) continue;
                    Pending.Enqueue(Other);
                }
                double Original = Cross(Branch);
                for (double Shift = DiagramGridMetrics.GRID_UNIT_PX; Shift < Math.Abs(Original - Cross(Junction));
                    Shift += DiagramGridMetrics.GRID_UNIT_PX)
                {
                    double Candidate = Original - Side * Shift;
                    // An occupied rank owns its ordering; never jump across it.
                    if (Chain.Any(Node => Nodes.Any(Other => !Chain.Contains(Other)
                        && Math.Abs(Main(Node) - Main(Other)) < HalfMain(Node) + HalfMain(Other) + 0.01
                        && (Cross(Other) - Original) * (Cross(Other) - Candidate) <= 0))) break;
                    if (Chain.Any(Node => Nodes.Any(Other => !Chain.Contains(Other)
                        && Math.Abs(Main(Node) - Main(Other)) < HalfMain(Node) + HalfMain(Other) + DiagramGridMetrics.NODE_GAP_PX
                        && Math.Abs(Cross(Other) - Candidate) < HalfCross(Node) + HalfCross(Other) + 2 * DiagramGridMetrics.NODE_GAP_PX))) break;
                    foreach (var Node in Chain)
                    {
                        if (Horizontal) Node.Y = Candidate; else Node.X = Candidate;
                        Moved.Add(Node);
                    }
                }
            }
        }
        foreach (var Collector in Nodes.Where(Node => Node.Shape == "circle" && Node.GroupId == null))
        {
            var Parents = Edges.Where(Edge => Edge.TargetId == Collector.Id).Select(Edge => ById[Edge.SourceId]).ToList();
            if (Parents.Count is < 2 or > 3 || !Parents.Any(Moved.Contains)
                || Parents.Any(Node => Node.GroupId != null) || Parents.Select(Main).Distinct().Count() != 1) continue;
            double Centre = Parents.Count == 2 ? Parents.Average(Cross) : Cross(Parents.OrderBy(Cross).ElementAt(1));
            double Candidate = Cross(Collector) + Math.Round((Centre - Cross(Collector)) / DiagramGridMetrics.GRID_UNIT_PX)
                * DiagramGridMetrics.GRID_UNIT_PX;
            if (Nodes.Any(Other => Other != Collector
                && Math.Abs(Main(Other) - Main(Collector)) < HalfMain(Other) + HalfMain(Collector) + DiagramGridMetrics.NODE_GAP_PX
                && Math.Abs(Cross(Other) - Candidate) < HalfCross(Other) + HalfCross(Collector) + DiagramGridMetrics.NODE_GAP_PX)) continue;
            if (Horizontal) Collector.Y = Candidate; else Collector.X = Candidate;
        }
    }
}
