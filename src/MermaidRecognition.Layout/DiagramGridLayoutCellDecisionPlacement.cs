using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans decision detours and loop placement on authoritative logical reservations.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Plans both simple decision branches and their terminal merge on complete whole-cell side lanes.</summary>
    /// <param name="Contract">Existing generic decision/merge classification.</param>
    /// <param name="Placed">Complete shared diagram ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>Whether the complete legal assignment was accepted.</returns>
    internal static bool AlignCellSimpleDecisionMerges(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        foreach (var (Decision, Left, Right, Merge) in SimpleDecisionMerges(Contract, Placed, Horizontal))
        {
            if (new[] { Left, Right, Merge }.Any(Node => Node.PlacementHintLocked)) continue;
            int Centre = Horizontal ? Targets[Decision.Id].StartRow : Targets[Decision.Id].StartColumn;
            int Pitch = new[] { Decision, Left, Right, Merge }.Max(Node => Horizontal ? Targets[Node.Id].RowSpan : Targets[Node.Id].ColumnSpan);
            Targets[Left.Id] = WithCellCross(Targets[Left.Id], checked(Centre - Pitch), Horizontal);
            Targets[Right.Id] = WithCellCross(Targets[Right.Id], checked(Centre + Pitch), Horizontal);
            Targets[Merge.Id] = WithCellCross(Targets[Merge.Id], checked(Centre + Pitch), Horizontal);
        }
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Plans vertical return branches and complete continuing tails on opposite whole-cell lanes.</summary>
    /// <param name="Contract">Forward and return topology.</param>
    /// <param name="Placed">Complete diagram ownership.</param>
    /// <returns>Whether the complete legal branch/tail assignment was accepted.</returns>
    internal static bool AlignCellVerticalLoopBranches(DiagramContract Contract, List<GridNode> Placed)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed); var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Forward = Contract.Edges.Where(Edge => !Back.Contains(Edge) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var Decision in Placed.Where(Node => Node.IsDiamond))
        {
            var Incoming = Forward.Where(Edge => Edge.TargetId == Decision.Id).ToArray();
            var Outgoing = Forward.Where(Edge => Edge.SourceId == Decision.Id).ToArray();
            if (Incoming.Length != 1 || Outgoing.Length != 2) continue;
            var Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId]).ToArray();
            var Loop = Children.FirstOrDefault(Child => Back.Any(Edge => Edge.SourceId == Child.Id && Edge.TargetId == Incoming[0].SourceId));
            if (Children.Length != 2 || Children[0] == Children[1] || Loop == null || Loop.PlacementHintLocked
                || Children.Any(Child => Child.IsDiamond || Child.GroupId != Decision.GroupId || Forward.Count(Edge => Edge.TargetId == Child.Id) != 1)) continue;
            var Continuation = Children.First(Child => Child != Loop);
            var Tail = new List<GridNode>(); var Current = Continuation; var Seen = new HashSet<string>(StringComparer.Ordinal);
            while (Seen.Add(Current.Id))
            {
                Tail.Add(Current);
                var Next = Forward.Where(Edge => Edge.SourceId == Current.Id).ToArray();
                if (Next.Length != 1 || !ById.TryGetValue(Next[0].TargetId, out var Child) || Child.IsDiamond
                    || Child.GroupId != Continuation.GroupId || Forward.Count(Edge => Edge.TargetId == Child.Id) != 1) break;
                Current = Child;
            }
            if (Tail.Any(Node => Node.PlacementHintLocked)) continue;
            int Centre = Targets[Decision.Id].StartColumn;
            int Pitch = Tail.Append(Loop).Append(Decision).Max(Node => Targets[Node.Id].ColumnSpan);
            Targets[Loop.Id] = WithCellCross(Targets[Loop.Id], checked(Centre + Pitch), false);
            foreach (var Node in Tail) Targets[Node.Id] = WithCellCross(Targets[Node.Id], checked(Centre - Pitch), false);
        }
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Plans horizontal success and retry outcomes together using whole cells and measured departure clearance.</summary>
    /// <param name="Contract">Declared decision and return topology.</param>
    /// <param name="Placed">Complete authoritative diagram ownership.</param>
    /// <returns>Whether the complete legal region, gap and rank plan was accepted.</returns>
    internal static bool AlignCellHorizontalRetryOutcomes(DiagramContract Contract, List<GridNode> Placed)
    {
        var Before = FlowchartCellRepair.Capture(Placed); var Grid = Placed[0].CellNode!.Grid;
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Ranks = new Dictionary<GridNode, int>(); var Gaps = new Dictionary<int, double>();
        var Selected = new List<(GridNode Decision, GridNode Retry)>();
        foreach (var Decision in Placed.Where(Node => Node.IsDiamond))
        {
            var Incoming = Visible.Where(Edge => Edge.TargetId == Decision.Id).ToArray();
            var Outgoing = Visible.Where(Edge => Edge.SourceId == Decision.Id).ToArray();
            if (Incoming.Length != 1 || Outgoing.Length != 2 || !ById.TryGetValue(Incoming[0].SourceId, out var Input)
                || Input.IsDiamond || Visible.Count(Edge => Edge.SourceId == Input.Id) != 1) continue;
            var Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId]).Distinct().ToArray();
            var Retry = Children.FirstOrDefault(Child => Visible.Count(Edge => Edge.SourceId == Child.Id) == 1
                && Visible.Any(Edge => Edge.SourceId == Child.Id && Edge.TargetId == Input.Id));
            var Finish = Children.FirstOrDefault(Child => !Visible.Any(Edge => Edge.SourceId == Child.Id));
            if (Children.Length != 2 || Retry == null || Finish == null || Retry.PlacementHintLocked || Finish.PlacementHintLocked
                || Children.Any(Child => Child.IsDiamond || Child.GroupId != Decision.GroupId || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1)
                || Input.GroupId != Decision.GroupId || Visible.Count(Edge => Edge.TargetId == Input.Id) != 1) continue;
            var Anchor = Targets[Decision.Id]; var RetryBefore = Targets[Retry.Id];
            Targets[Finish.Id] = WithCellCross(Targets[Finish.Id], Anchor.StartRow, true);
            Targets[Retry.Id] = new(Anchor.EndRow, Anchor.StartColumn, RetryBefore.RowSpan, RetryBefore.ColumnSpan);
            var D = Decision.CellNode!.Bounds(Anchor, Decision.GeometryProvider);
            var R = Retry.CellNode!.Bounds(Targets[Retry.Id], Retry.GeometryProvider);
            double Missing = 3 * DiagramGridMetrics.GRID_UNIT_PX - (R.CenterY - R.Height / 2 - D.CenterY - D.Height / 2);
            if (Missing > 0)
            {
                int After = Anchor.EndRow - 1;
                if (Targets.Values.Any(Region => Region.StartRow <= After && Region.EndRow > After + 1))
                {
                    // A routing corridor cannot be created inside another
                    // node's region. Offer a whole-cell move instead.
                    var Region = Targets[Retry.Id];
                    for (int Step = 1; Step <= FlowchartLayoutRules.RepairChainDepth; Step++)
                    {
                        var Candidate = new FlowchartCellRegion(checked(Region.StartRow + Step), Region.StartColumn, Region.RowSpan, Region.ColumnSpan);
                        var Bounds = Retry.CellNode.Bounds(Candidate, Retry.GeometryProvider);
                        if (Bounds.CenterY - Bounds.Height / 2 - D.CenterY - D.Height / 2 < 3 * DiagramGridMetrics.GRID_UNIT_PX) continue;
                        Targets[Retry.Id] = Candidate; break;
                    }
                }
                else Gaps[After] = Math.Max(Gaps.GetValueOrDefault(After), Missing);
            }
            Ranks[Retry] = Decision.Rank;
            Selected.Add((Decision, Retry));
        }
        var Plan = FlowchartCellRepair.PlanAssignment(Placed, Targets,
            Gaps.Select(Pair => new FlowchartCellGapGrowth(false, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray());
        if (Plan == null || Selected.Any(Pair =>
            {
                var D = Pair.Decision.CellNode!.Bounds(Plan.After.Regions[Pair.Decision.Id], Pair.Decision.GeometryProvider, Plan.After);
                var R = Pair.Retry.CellNode!.Bounds(Plan.After.Regions[Pair.Retry.Id], Pair.Retry.GeometryProvider, Plan.After);
                return R.CenterY - R.Height / 2 - D.CenterY - D.Height / 2 < 3 * DiagramGridMetrics.GRID_UNIT_PX;
            }) || !Plan.TryApply()) return false;
        foreach (var Pair in Ranks) Pair.Key.Rank = Pair.Value;
        return true;
    }

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
