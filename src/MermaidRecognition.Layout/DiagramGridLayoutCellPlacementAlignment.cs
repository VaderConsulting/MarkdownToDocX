using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans logical placement alignments without assigning derived pixel positions.</summary>
public static partial class DiagramGridLayout
{
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
