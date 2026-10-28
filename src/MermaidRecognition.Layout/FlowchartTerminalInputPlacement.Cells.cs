using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans independent terminal inputs beside their collectors in whole cells.</summary>
internal static partial class FlowchartTerminalInputPlacement
{
    /// <summary>Builds all independent-input moves before publishing reservations or changing logical ranks.</summary>
    /// <param name="Contract">Visible relationships and return-edge classification.</param>
    /// <param name="Nodes">Complete shared diagram ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <param name="CrossPitch">Existing measured minimum cross-axis separation.</param>
    /// <returns>Whether a complete legal assignment was accepted.</returns>
    internal static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal, int CrossPitch)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes); FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        EdgeContract[] Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        Dictionary<DiagramGridLayout.GridNode, int> Ranks = [];
        foreach (DiagramGridLayout.GridNode? Root in Nodes.Where(Node => !Node.IsDiamond && !Node.PlacementHintLocked && !Edges.Any(Edge => Edge.TargetId == Node.Id)))
        {
            EdgeContract[] Outgoing = [.. Edges.Where(Edge => Edge.SourceId == Root.Id)];
            if (Outgoing.Length != 1 || Back.Contains(Outgoing[0]) || !ById.TryGetValue(Outgoing[0].TargetId, out DiagramGridLayout.GridNode? Target)
                || Target.IsDiamond || Target.GroupId != Root.GroupId || Target.Rank <= Root.Rank || Edges.Any(Edge => Edge.SourceId == Target.Id))
            {
                continue;
            }

            EdgeContract[] Others = [.. Edges.Where(Edge => Edge.TargetId == Target.Id && Edge.SourceId != Root.Id)];
            if (Others.Length == 0 || Others.Any(Edge => Back.Contains(Edge) || !ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Parent)
                || Parent.GroupId != Root.GroupId || Parent.Rank >= Target.Rank || !Edges.Any(Incoming => Incoming.TargetId == Parent.Id))
                || !Others.Any(Edge => Edges.Count(Outgoing => Outgoing.SourceId == Edge.SourceId) > 1))
            {
                continue;
            }

            FlowchartCellRegion Anchor = Targets[Target.Id];
            int Centre = Horizontal ? Anchor.StartRow : Anchor.StartColumn;
            int Side = Math.Sign(Centre - Others.Average(Edge => Horizontal ? Targets[Edge.SourceId].StartRow : Targets[Edge.SourceId].StartColumn));
            if (Side == 0)
            {
                continue;
            }

            int Pitch = Math.Max(Horizontal ? Anchor.RowSpan : Anchor.ColumnSpan,
                Horizontal ? Targets[Root.Id].RowSpan : Targets[Root.Id].ColumnSpan);
            FlowchartCellRegion Original = Targets[Root.Id];
            for (int Step = 1; Step <= Nodes.Count + 1; Step++)
            {
                int Cross = checked(Centre + (Side * Step * Pitch));
                FlowchartCellRegion Candidate = new FlowchartCellRegion(Horizontal ? Cross : Anchor.StartRow,
                    Horizontal ? Anchor.StartColumn : Cross, Original.RowSpan, Original.ColumnSpan);
                FlowchartLayoutNodeBounds Bounds = Root.CellNode!.Bounds(Candidate, Root.GeometryProvider);
                if (Math.Abs((Horizontal ? Bounds.CenterY : Bounds.CenterX) - (Horizontal ? Target.Y : Target.X)) < CrossPitch)
                {
                    continue;
                }

                Targets[Root.Id] = Candidate;
                if (Grid.TryPrepare(Targets, [], out _)) { Ranks[Root] = Target.Rank; break; }
                Targets[Root.Id] = Original;
            }
        }
        if (!FlowchartCellRepair.TryAssign(Nodes, Targets))
        {
            return false;
        }

        foreach (KeyValuePair<DiagramGridLayout.GridNode, int> Pair in Ranks)
        {
            Pair.Key.Rank = Pair.Value;
        }

        return true;
    }
}
