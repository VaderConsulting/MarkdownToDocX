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
        // Bound-node hole proposals compete in the routed whole-cell scorer.
        if (Nodes.Any(Node => Node.CellNode != null))
        {
            return;
        }
        // Group frames and junction branches have their own placement rules.
        if (Contract.Groups.Count > 0 || Nodes.Any(Node => Node.IsDiamond || Node.Shape is not (null or "" or "rect" or "rounded" or "round")))
        {
            return;
        }

        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        List<EdgeContract> Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        foreach (DiagramGridLayout.GridNode Leaf in Nodes)
        {
            if (Edges.Any(Edge => Edge.SourceId == Leaf.Id))
            {
                continue;
            }

            List<EdgeContract> Incoming = [.. Edges.Where(Edge => Edge.TargetId == Leaf.Id)];
            if (Incoming.Count != 1 || Back.Contains(Incoming[0])
                || !ById.TryGetValue(Incoming[0].SourceId, out DiagramGridLayout.GridNode? Parent) || Parent.Rank >= Leaf.Rank)
            {
                continue;
            }

            int Side = Math.Sign(Cross(Leaf) - Cross(Parent));
            if (Side == 0)
            {
                continue;
            }

            List<DiagramGridLayout.GridNode?> Children = [.. Edges.Where(Edge => Edge.SourceId == Parent.Id && Edge.TargetId != Leaf.Id).Select(Edge => ById.GetValueOrDefault(Edge.TargetId))];
            if (Children.Count == 0 || Children.Any(Child => Child == null || Child.Rank != Leaf.Rank
                || Math.Sign(Cross(Child) - Cross(Parent)) != -Side
                || Edges.Any(Edge => Edge.SourceId == Child.Id)
                || !Edges.Any(Edge => Edge.TargetId == Child.Id && Edge.SourceId != Parent.Id
                    && ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Peer) && Peer.Rank == Parent.Rank)))
            {
                continue;
            }

            double Candidate = Cross(Parent) + (Side * DiagramGridMetrics.CeilToGrid(Half(Parent) + Half(Leaf) + DiagramGridMetrics.NODE_GAP_PX));
            // Fill a bounded hole in the existing row, without adding a column.
            if (!Nodes.Any(Other => Other != Leaf && Other.Rank == Parent.Rank
                && Side * (Cross(Other) - Candidate) > 0))
            {
                continue;
            }

            if (Nodes.Any(Other => Other != Leaf && Other != Parent && Other.Rank == Parent.Rank
                && Side * (Cross(Other) - Cross(Parent)) > 0
                && Side * (Cross(Other) - Candidate) <= 0))
            {
                continue;
            }

            if (Nodes.Any(Other => Other != Leaf && Other.Rank == Parent.Rank
                && Math.Abs(Cross(Other) - Candidate) < Half(Other) + Half(Leaf) + DiagramGridMetrics.NODE_GAP_PX))
            {
                continue;
            }

            Leaf.Rank = Parent.Rank;
            if (Horizontal) { Leaf.X = Parent.X; Leaf.Y = Candidate; }
            else { Leaf.Y = Parent.Y; Leaf.X = Candidate; }
        }
    }

    /// <summary>Offers a leaf the free whole-cell slot beside its parent without publishing regions or rank metadata.</summary>
    /// <param name="Contract">Visible graph and group restrictions.</param>
    /// <param name="Nodes">Complete diagram ownership.</param>
    /// <param name="Horizontal">Whether parent ranks advance across columns.</param>
    /// <returns>Complete leaf translations; the routed scorer checks clearance, hints and total cost.</returns>
    internal static IEnumerable<DiagramGridLayout.CellRouteMoveTarget> CellCandidates(DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Contract.Groups.Count > 0 || Nodes.Any(Node => Node.IsDiamond || Node.Shape is not (null or "" or "rect" or "rounded" or "round")))
        {
            yield break;
        }

        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = FlowchartCellRepair.Capture(Nodes).Regions;
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        EdgeContract[] Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        foreach (DiagramGridLayout.GridNode? Leaf in Nodes.Where(Node => !Node.PlacementHintLocked && !Edges.Any(Edge => Edge.SourceId == Node.Id)))
        {
            EdgeContract[] Incoming = [.. Edges.Where(Edge => Edge.TargetId == Leaf.Id)];
            if (Incoming.Length != 1 || Back.Contains(Incoming[0]) || !ById.TryGetValue(Incoming[0].SourceId, out DiagramGridLayout.GridNode? Parent)
                || Parent.Rank >= Leaf.Rank)
            {
                continue;
            }

            FlowchartCellRegion A = Regions[Parent.Id]; FlowchartCellRegion B = Regions[Leaf.Id];
            int Side = Math.Sign((Horizontal ? B.StartRow : B.StartColumn) - (Horizontal ? A.StartRow : A.StartColumn));
            if (Side == 0)
            {
                continue;
            }

            DiagramGridLayout.GridNode?[] Children = [.. Edges.Where(Edge => Edge.SourceId == Parent.Id && Edge.TargetId != Leaf.Id).Select(Edge => ById.GetValueOrDefault(Edge.TargetId))];
            if (Children.Length == 0 || Children.Any(Child => Child == null || Child.Rank != Leaf.Rank
                || Math.Sign((Horizontal ? Regions[Child.Id].StartRow : Regions[Child.Id].StartColumn)
                    - (Horizontal ? A.StartRow : A.StartColumn)) != -Side
                || Edges.Any(Edge => Edge.SourceId == Child.Id)
                || !Edges.Any(Edge => Edge.TargetId == Child.Id && Edge.SourceId != Parent.Id
                    && ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Peer) && Peer.Rank == Parent.Rank)))
            {
                continue;
            }

            int Cross = Horizontal ? Side < 0 ? A.StartRow - B.RowSpan : A.EndRow
                : Side < 0 ? A.StartColumn - B.ColumnSpan : A.EndColumn;
            bool Bounded = Nodes.Any(Other => Other != Leaf && Other.Rank == Parent.Rank
                && Side * ((Horizontal ? Regions[Other.Id].StartRow : Regions[Other.Id].StartColumn) - Cross) > 0);
            if (!Bounded)
            {
                continue;
            }

            int Row = Horizontal ? Cross : A.StartRow, Column = Horizontal ? A.StartColumn : Cross;
            FlowchartCellRegion Candidate = new FlowchartCellRegion(Row, Column, B.RowSpan, B.ColumnSpan);
            if (Regions.Any(Pair => Pair.Key != Leaf.Id && Pair.Value.Overlaps(Candidate)))
            {
                continue;
            }

            yield return new($"terminal-hole|{Leaf.Id}|{Row}|{Column}", Leaf.Id, [Leaf],
                checked(Row - B.StartRow), checked(Column - B.StartColumn));
        }
    }
}
