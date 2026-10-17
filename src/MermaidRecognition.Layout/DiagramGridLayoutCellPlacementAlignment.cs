using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans logical placement alignments without assigning derived pixel positions.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Plans each unbranched upstream chain against its decision's logical cross-axis start, with complete collision rejection.</summary>
    /// <param name="Contract">Graph whose return edges do not determine forward alignment.</param>
    /// <param name="Placed">Complete diagram ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>Whether the complete assignment was legal and accepted.</returns>
    internal static bool AlignCellDecisionInputs(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Forward = Contract.Edges.Where(Edge => !BackEdges.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var Decision in Placed.Where(Node => Node.IsDiamond))
        {
            var Incoming = Forward.Where(Edge => Edge.TargetId == Decision.Id).ToArray();
            if (Incoming.Length != 1 || !ById.TryGetValue(Incoming[0].SourceId, out var Input)
                || Input.IsDiamond || Input.GroupId != Decision.GroupId || Forward.Count(Edge => Edge.SourceId == Input.Id) != 1) continue;
            var Anchor = Targets[Decision.Id];
            var Visited = new HashSet<string>(StringComparer.Ordinal);
            while (Visited.Add(Input.Id))
            {
                if (Input.PlacementHintLocked) break;
                var Region = Targets[Input.Id];
                Targets[Input.Id] = new(Horizontal ? Anchor.StartRow : Region.StartRow,
                    Horizontal ? Region.StartColumn : Anchor.StartColumn, Region.RowSpan, Region.ColumnSpan);
                Incoming = Forward.Where(Edge => Edge.TargetId == Input.Id).ToArray();
                if (Incoming.Length != 1 || !ById.TryGetValue(Incoming[0].SourceId, out var Previous)
                    || Previous.IsDiamond || Previous.GroupId != Decision.GroupId || Forward.Count(Edge => Edge.SourceId == Previous.Id) != 1) break;
                Input = Previous;
            }
        }
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Plans every sole terminal child's cross-axis reservation before applying the complete assignment.</summary>
    /// <param name="Contract">Visible graph used by the existing terminal-child selection.</param>
    /// <param name="Placed">Complete shared cell ownership.</param>
    /// <param name="Horizontal">Whether cross-axis alignment uses rows.</param>
    /// <returns>Whether the complete legal plan was accepted.</returns>
    internal static bool AlignCellTerminalChildren(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Aligned = new List<(GridNode Parent, GridNode Child)>();
        foreach (var Parent in Placed)
        {
            var Outgoing = Visible.Where(Edge => Edge.SourceId == Parent.Id).ToArray();
            if (Outgoing.Length != 1 || !ById.TryGetValue(Outgoing[0].TargetId, out var Child)
                || Child.PlacementHintLocked || Child.GroupId != Parent.GroupId || Child.Rank == Parent.Rank
                || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1 || Visible.Any(Edge => Edge.SourceId == Child.Id)) continue;
            var Region = Targets[Child.Id]; var Anchor = Targets[Parent.Id];
            Targets[Child.Id] = new(Horizontal ? Anchor.StartRow : Region.StartRow,
                Horizontal ? Region.StartColumn : Anchor.StartColumn, Region.RowSpan, Region.ColumnSpan);
            Aligned.Add((Parent, Child));
        }
        if (!FlowchartCellRepair.TryAssign(Placed, Targets)) return false;
        // Logical ordering metadata follows only an accepted complete plan.
        foreach (var Pair in Aligned) Pair.Child.Column = Pair.Parent.Column;
        return true;
    }
}
