using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans decision detours and loop placement on authoritative logical reservations.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Plans every selected detour, merge and optional terminal input before publishing any cell or rank change.</summary>
    /// <param name="Contract">Visible graph and existing generic detour classification.</param>
    /// <param name="Placed">Complete shared ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>Whether the complete legal assignment was accepted.</returns>
    internal static bool AlignCellDecisionDetours(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Ranks = new Dictionary<GridNode, int>();
        foreach (var (Decision, Detour, Merge) in DecisionDetours(Contract, Placed))
        {
            if (Detour.PlacementHintLocked || Merge.PlacementHintLocked) continue;
            var Anchor = Targets[Decision.Id];
            int Centre = Horizontal ? Anchor.StartRow : Anchor.StartColumn;
            int Pitch = new[] { Decision, Detour, Merge }.Max(Node => Horizontal ? Targets[Node.Id].RowSpan : Targets[Node.Id].ColumnSpan);
            bool Terminal = !Visible.Any(Edge => Edge.SourceId == Merge.Id) && Visible.Count(Edge => Edge.TargetId == Merge.Id) > 2;
            Targets[Detour.Id] = WithCellCross(Targets[Detour.Id], checked(Centre + (Terminal ? Pitch : -Pitch)), Horizontal);
            Targets[Merge.Id] = WithCellCross(Targets[Merge.Id], checked(Centre - (Terminal ? Pitch : 0)), Horizontal);
            if (!Terminal) continue;
            foreach (var Incoming in Visible.Where(Edge => Edge.TargetId == Merge.Id && Edge.SourceId != Decision.Id && Edge.SourceId != Detour.Id))
            {
                var Exit = Placed.FirstOrDefault(Node => Node.Id == Incoming.SourceId);
                if (Exit == null || Exit.IsDiamond || Exit.PlacementHintLocked || Exit.GroupId != Merge.GroupId
                    || Visible.Count(Edge => Edge.SourceId == Exit.Id) != 1 || Visible.Count(Edge => Edge.TargetId == Exit.Id) != 1) continue;
                var ParentEdge = Visible.Single(Edge => Edge.TargetId == Exit.Id);
                var Parent = Placed.FirstOrDefault(Node => Node.Id == ParentEdge.SourceId);
                if (Parent == null || !Parent.IsDiamond || Math.Abs(Parent.Rank - Decision.Rank) < 2) continue;
                var Region = WithCellCross(Targets[Exit.Id], checked(Centre - 2 * Pitch), Horizontal);
                Targets[Exit.Id] = new(Horizontal ? Region.StartRow : Targets[Merge.Id].StartRow,
                    Horizontal ? Targets[Merge.Id].StartColumn : Region.StartColumn, Region.RowSpan, Region.ColumnSpan);
                Ranks[Exit] = Merge.Rank;
            }
        }
        if (!FlowchartCellRepair.TryAssign(Placed, Targets)) return false;
        foreach (var Pair in Ranks) Pair.Key.Rank = Pair.Value;
        return true;
    }

    /// <summary>Replaces one logical cross-axis start without changing a region's main start or spans.</summary>
    /// <param name="Region">Complete private reservation.</param>
    /// <param name="Cross">Whole-cell cross-axis start.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>The complete translated rectangle.</returns>
    private static FlowchartCellRegion WithCellCross(FlowchartCellRegion Region, int Cross, bool Horizontal) =>
        new(Horizontal ? Cross : Region.StartRow, Horizontal ? Region.StartColumn : Cross, Region.RowSpan, Region.ColumnSpan);
}
