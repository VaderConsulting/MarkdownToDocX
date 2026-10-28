using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans decision detours and loop placement on authoritative logical reservations.</summary>
public static partial class DiagramGridLayout
{


    /// <summary>Plans vertical return branches and complete continuing tails on opposite whole-cell lanes.</summary>
    /// <param name="Contract">Forward and return topology.</param>
    /// <param name="Placed">Complete diagram ownership.</param>
    /// <returns>Whether the complete legal branch/tail assignment was accepted.</returns>
    internal static bool AlignCellVerticalLoopBranches(DiagramContract Contract, List<GridNode> Placed)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Placed);
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        Dictionary<string, GridNode> ById = IndexPlaced(Placed); IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        EdgeContract[] Forward = [.. Contract.Edges.Where(Edge => !Back.Contains(Edge) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        foreach (GridNode? Decision in Placed.Where(Node => Node.IsDiamond))
        {
            EdgeContract[] Incoming = [.. Forward.Where(Edge => Edge.TargetId == Decision.Id)];
            EdgeContract[] Outgoing = [.. Forward.Where(Edge => Edge.SourceId == Decision.Id)];
            if (Incoming.Length != 1 || Outgoing.Length != 2)
            {
                continue;
            }

            GridNode[] Children = [.. Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId])];
            GridNode? Loop = Children.FirstOrDefault(Child => Back.Any(Edge => Edge.SourceId == Child.Id && Edge.TargetId == Incoming[0].SourceId));
            if (Children.Length != 2 || Children[0] == Children[1] || Loop == null || Loop.PlacementHintLocked
                || Children.Any(Child => Child.IsDiamond || Child.GroupId != Decision.GroupId || Forward.Count(Edge => Edge.TargetId == Child.Id) != 1))
            {
                continue;
            }

            GridNode Continuation = Children.First(Child => Child != Loop);
            IReadOnlyList<GridNode> Tail = CellOutputChain(Continuation, ById, Forward, RequireRankChange: false);
            if (Tail.Any(Node => Node.PlacementHintLocked))
            {
                continue;
            }

            int Centre = Targets[Decision.Id].StartColumn;
            int Pitch = Tail.Append(Loop).Append(Decision).Max(Node => Targets[Node.Id].ColumnSpan);
            Targets[Loop.Id] = WithCellCross(Targets[Loop.Id], checked(Centre + Pitch), false);
            int Shift = checked(Centre - Pitch - Targets[Continuation.Id].StartColumn);
            // Keep the return/continuation side order, but preserve every
            // existing tail offset. Only routed scoring may straighten it.
            foreach (GridNode Node in Tail)
            {
                Targets[Node.Id] = WithCellCross(Targets[Node.Id], checked(Targets[Node.Id].StartColumn + Shift), false);
            }
        }
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Plans horizontal success and retry outcomes together using whole cells and measured departure clearance.</summary>
    /// <param name="Contract">Declared decision and return topology.</param>
    /// <param name="Placed">Complete authoritative diagram ownership.</param>
    /// <returns>Whether the complete legal region, gap and rank plan was accepted.</returns>
    internal static bool AlignCellHorizontalRetryOutcomes(DiagramContract Contract, List<GridNode> Placed)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Placed); FlowchartLogicalGrid Grid = Placed[0].CellNode!.Grid;
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        EdgeContract[] Visible = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        Dictionary<GridNode, int> Ranks = []; Dictionary<int, double> Gaps = [];
        List<(GridNode Decision, GridNode Retry)> Selected = [];
        foreach (GridNode? Decision in Placed.Where(Node => Node.IsDiamond))
        {
            EdgeContract[] Incoming = [.. Visible.Where(Edge => Edge.TargetId == Decision.Id)];
            EdgeContract[] Outgoing = [.. Visible.Where(Edge => Edge.SourceId == Decision.Id)];
            if (Incoming.Length != 1 || Outgoing.Length != 2 || !ById.TryGetValue(Incoming[0].SourceId, out GridNode? Input)
                || Input.IsDiamond || Visible.Count(Edge => Edge.SourceId == Input.Id) != 1)
            {
                continue;
            }

            GridNode[] Children = [.. Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId]).Distinct()];
            GridNode? Retry = Children.FirstOrDefault(Child => Visible.Count(Edge => Edge.SourceId == Child.Id) == 1
                && Visible.Any(Edge => Edge.SourceId == Child.Id && Edge.TargetId == Input.Id));
            GridNode? Finish = Children.FirstOrDefault(Child => !Visible.Any(Edge => Edge.SourceId == Child.Id));
            if (Children.Length != 2 || Retry == null || Finish == null || Retry.PlacementHintLocked || Finish.PlacementHintLocked
                || Children.Any(Child => Child.IsDiamond || Child.GroupId != Decision.GroupId || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1)
                || Input.GroupId != Decision.GroupId || Visible.Count(Edge => Edge.TargetId == Input.Id) != 1)
            {
                continue;
            }

            FlowchartCellRegion Anchor = Targets[Decision.Id]; FlowchartCellRegion RetryBefore = Targets[Retry.Id];
            // Success keeps its existing region. This plan only provides the
            // retry corridor; straightening competes after routes are built.
            Targets[Retry.Id] = new(Anchor.EndRow, Anchor.StartColumn, RetryBefore.RowSpan, RetryBefore.ColumnSpan);
            FlowchartLayoutNodeBounds D = Decision.CellNode!.Bounds(Anchor, Decision.GeometryProvider);
            FlowchartLayoutNodeBounds R = Retry.CellNode!.Bounds(Targets[Retry.Id], Retry.GeometryProvider);
            double Missing = (3 * DiagramGridMetrics.GRID_UNIT_PX) - (R.CenterY - (R.Height / 2) - D.CenterY - (D.Height / 2));
            if (Missing > 0)
            {
                int After = Anchor.EndRow - 1;
                if (Targets.Values.Any(Region => Region.StartRow <= After && Region.EndRow > After + 1))
                {
                    // A routing corridor cannot be created inside another
                    // node's region. Offer a whole-cell move instead.
                    FlowchartCellRegion Region = Targets[Retry.Id];
                    for (int Step = 1; Step <= FlowchartLayoutRules.RepairChainDepth; Step++)
                    {
                        FlowchartCellRegion Candidate = new FlowchartCellRegion(checked(Region.StartRow + Step), Region.StartColumn, Region.RowSpan, Region.ColumnSpan);
                        FlowchartLayoutNodeBounds Bounds = Retry.CellNode.Bounds(Candidate, Retry.GeometryProvider);
                        if (Bounds.CenterY - (Bounds.Height / 2) - D.CenterY - (D.Height / 2) < 3 * DiagramGridMetrics.GRID_UNIT_PX)
                        {
                            continue;
                        }

                        Targets[Retry.Id] = Candidate; break;
                    }
                }
                else
                {
                    Gaps[After] = Math.Max(Gaps.GetValueOrDefault(After), Missing);
                }
            }
            Ranks[Retry] = Decision.Rank;
            Selected.Add((Decision, Retry));
        }
        FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Placed, Targets,
            Gaps.Select(Pair => new FlowchartCellGapGrowth(false, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray());
        if (Plan == null || Selected.Any(Pair =>
            {
                FlowchartLayoutNodeBounds D = Pair.Decision.CellNode!.Bounds(Plan.After.Regions[Pair.Decision.Id], Pair.Decision.GeometryProvider, Plan.After);
                FlowchartLayoutNodeBounds R = Pair.Retry.CellNode!.Bounds(Plan.After.Regions[Pair.Retry.Id], Pair.Retry.GeometryProvider, Plan.After);
                return R.CenterY - (R.Height / 2) - D.CenterY - (D.Height / 2) < 3 * DiagramGridMetrics.GRID_UNIT_PX;
            }) || !Plan.TryApply())
        {
            return false;
        }

        foreach (KeyValuePair<GridNode, int> Pair in Ranks)
        {
            Pair.Key.Rank = Pair.Value;
        }

        return true;
    }

    /// <summary>Plans every selected detour, merge and optional terminal input before publishing any cell or rank change.</summary>
    /// <param name="Contract">Visible graph and existing generic detour classification.</param>
    /// <param name="Placed">Complete shared ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>Whether the complete legal assignment was accepted.</returns>
    internal static bool AlignCellDecisionDetours(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Placed);
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        EdgeContract[] Visible = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        Dictionary<GridNode, int> Ranks = [];
        foreach ((GridNode? Decision, GridNode? Detour, GridNode? Merge) in DecisionDetours(Contract, Placed))
        {
            if (Detour.PlacementHintLocked || Merge.PlacementHintLocked)
            {
                continue;
            }

            FlowchartCellRegion Anchor = Targets[Decision.Id];
            int Centre = Horizontal ? Anchor.StartRow : Anchor.StartColumn;
            int Pitch = new[] { Decision, Detour, Merge }.Max(Node => Horizontal ? Targets[Node.Id].RowSpan : Targets[Node.Id].ColumnSpan);
            bool Terminal = !Visible.Any(Edge => Edge.SourceId == Merge.Id) && Visible.Count(Edge => Edge.TargetId == Merge.Id) > 2;
            Targets[Detour.Id] = WithCellCross(Targets[Detour.Id], checked(Centre + (Terminal ? Pitch : -Pitch)), Horizontal);
            // Terminal multi-input placement reserves a separate collecting
            // lane. A continuing merge keeps its independently chosen start.
            if (Terminal)
            {
                Targets[Merge.Id] = WithCellCross(Targets[Merge.Id], checked(Centre - Pitch), Horizontal);
            }

            if (!Terminal)
            {
                continue;
            }

            foreach (EdgeContract? Incoming in Visible.Where(Edge => Edge.TargetId == Merge.Id && Edge.SourceId != Decision.Id && Edge.SourceId != Detour.Id))
            {
                GridNode? Exit = Placed.FirstOrDefault(Node => Node.Id == Incoming.SourceId);
                if (Exit == null || Exit.IsDiamond || Exit.PlacementHintLocked || Exit.GroupId != Merge.GroupId
                    || Visible.Count(Edge => Edge.SourceId == Exit.Id) != 1 || Visible.Count(Edge => Edge.TargetId == Exit.Id) != 1)
                {
                    continue;
                }

                EdgeContract ParentEdge = Visible.Single(Edge => Edge.TargetId == Exit.Id);
                GridNode? Parent = Placed.FirstOrDefault(Node => Node.Id == ParentEdge.SourceId);
                if (Parent == null || !Parent.IsDiamond || Math.Abs(Parent.Rank - Decision.Rank) < 2)
                {
                    continue;
                }

                FlowchartCellRegion Region = WithCellCross(Targets[Exit.Id], checked(Centre - (2 * Pitch)), Horizontal);
                Targets[Exit.Id] = new(Horizontal ? Region.StartRow : Targets[Merge.Id].StartRow,
                    Horizontal ? Targets[Merge.Id].StartColumn : Region.StartColumn, Region.RowSpan, Region.ColumnSpan);
                Ranks[Exit] = Merge.Rank;
            }
        }
        if (!FlowchartCellRepair.TryAssign(Placed, Targets))
        {
            return false;
        }

        foreach (KeyValuePair<GridNode, int> Pair in Ranks)
        {
            Pair.Key.Rank = Pair.Value;
        }

        return true;
    }

    /// <summary>Replaces one logical cross-axis start without changing a region's main start or spans.</summary>
    /// <param name="Region">Complete private reservation.</param>
    /// <param name="Cross">Whole-cell cross-axis start.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>The complete translated rectangle.</returns>
    private static FlowchartCellRegion WithCellCross(FlowchartCellRegion Region, int Cross, bool Horizontal) =>
        new(Horizontal ? Cross : Region.StartRow, Horizontal ? Region.StartColumn : Cross, Region.RowSpan, Region.ColumnSpan);

    /// <summary>Terminal decision branches share one rank and symmetric whole-cell lanes.</summary>
    internal static bool AlignCellDecisionBranchTargetRow(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        if (Horizontal)
        {
            return false;
        }

        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Placed);
        Dictionary<string, FlowchartCellRegion> Targets =
            new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        IReadOnlySet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        List<EdgeContract> Visible = [.. Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        bool Changed = false;

        foreach (GridNode Parent in Placed.Where(Node => Node.IsDiamond))
        {
            List<GridNode> Children = [];
            foreach (EdgeContract Edge in Visible)
            {
                if (BackEdges.Contains(Edge)
                    || !string.Equals(Edge.SourceId, Parent.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ById.TryGetValue(Edge.TargetId, out GridNode? Child)
                    || Child.Rank <= Parent.Rank
                    || Child.PlacementHintLocked)
                {
                    continue;
                }

                Children.Add(Child);
            }

            if (Children.Count < 2)
            {
                continue;
            }

            bool AllTerminalBranches = Children.All(Child =>
                !Visible.Any(Edge => Edge.SourceId == Child.Id)
                && Visible.Count(Edge => Edge.TargetId == Child.Id) == 1);
            if (!AllTerminalBranches)
            {
                continue;
            }

            int TargetRow = Children
                .GroupBy(Child => Targets[Child.Id].StartRow)
                .OrderByDescending(Group => Group.Count())
                .ThenBy(Group => Group.Key)
                .First()
                .Key;

            foreach (GridNode Child in Children)
            {
                FlowchartCellRegion Region = Targets[Child.Id];
                if (Region.StartRow == TargetRow)
                {
                    continue;
                }

                Targets[Child.Id] = new FlowchartCellRegion(
                    TargetRow,
                    Region.StartColumn,
                    Region.RowSpan,
                    Region.ColumnSpan);
                Changed = true;
            }
        }

        if (!Changed)
        {
            return false;
        }

        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }
}
