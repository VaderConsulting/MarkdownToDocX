using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Compares complete frame-clearance assignments in cells and external gaps under the existing placement score.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Offers measured shared-gap growth and whole-cell translations together; only a completely legal winner is published.</summary>
    /// <param name="Contract">Group membership and honoured hint relationships.</param>
    /// <param name="Placed">Complete authoritative node ownership.</param>
    /// <param name="Frames">Derived frame cache updated only for the accepted winner.</param>
    /// <param name="MovingOnly">Optional explicitly requested moving unit.</param>
    /// <param name="RequiredGap">Existing caller's minimum visual clearance.</param>
    /// <param name="ConcernedFrame">Optional requested frame conflict.</param>
    /// <param name="ConcernedNode">Optional requested foreign node.</param>
    /// <param name="Budget">Owning diagram's named candidate and accepted-chain limits.</param>
    /// <returns>Whether the lowest-scoring complete legal candidate examined within the limits was accepted.</returns>
    private static bool SearchScoredCellFrameAssignment(DiagramContract Contract, List<GridNode> Placed,
        List<GridFrame> Frames, string? MovingOnly, int RequiredGap, string? ConcernedFrame,
        string? ConcernedNode, FlowchartRenderContext.RepairSearchBudget Budget)
    {
        using var CostTiming = FlowchartCellCostTiming.Begin("frame-search");
        var SearchClock = System.Diagnostics.Stopwatch.StartNew();
        int FirstLegal = 0;
        var Original = FlowchartCellRepair.Capture(Placed);
        var Initial = CellFrameConflicts(Contract, Placed, Frames, RequiredGap);
        GroupFrameFitPassObserver?.Invoke("CELL_FRAME_SEARCH_INPUT|" + System.Text.Json.JsonSerializer.Serialize(new
        { Conflicts = Initial, Frames = Frames.Select(Frame => new { Frame.Id, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom }) }));
        if (Initial.Count == 0 || Budget.AcceptedMoves >= FlowchartLayoutRules.RepairChainDepth
            || Budget.CandidateMoves >= FlowchartLayoutRules.RepairCandidateMoves) return false;
        if (!Initial.Any(Conflict => (ConcernedFrame == null || Conflict.First == ConcernedFrame || Conflict.Second == ConcernedFrame)
            && (ConcernedNode == null || Conflict.Key == "node|" + ConcernedFrame + "|" + ConcernedNode))) return false;
        var Bases = new List<(FlowchartCellRepair.Assignment Plan, List<CellFrameConflict> Faults, double Score)>();
        FlowchartCellRepair.Assignment? Best = null; double BestScore = double.PositiveInfinity; double BestReading = double.PositiveInfinity;
        var Seen = new HashSet<string>(StringComparer.Ordinal);
        if (MovingOnly == null)
            foreach (var Axes in new[] { (Columns: true, Rows: false), (Columns: false, Rows: true), (Columns: true, Rows: true) })
                Offer(Grow(new(Original.Regions, StringComparer.Ordinal), [], Axes.Columns, Axes.Rows), true);
        var Unchanged = FlowchartCellRepair.PlanAssignment(Placed, Original.Regions, AllowHintedRegionRepair: true);
        if (Unchanged != null) Bases.Add((Unchanged, Initial, FlowchartCellCandidateScore.Measure(Placed, Original).Score));

        // Gap-only assignments and translations are alternatives, not a repair
        // priority. Search the most nearly legal bases first, comparing every
        // complete result by the same score. No trial changes live ownership.
        for (int Depth = 0; Depth < FlowchartLayoutRules.RepairChainDepth
            && Budget.CandidateMoves < FlowchartLayoutRules.RepairCandidateMoves; Depth++)
        {
            var Level = Bases.OrderBy(Item => Item.Faults.Count).ThenBy(Item => Item.Score).ToArray();
            Bases.Clear();
            // Reserve the remaining named budget across the remaining chain
            // levels. A rejected partial assignment stays private; only a
            // fully legal complete chain can become the published winner.
            int LevelLimit = Budget.CandidateMoves + Math.Max(1,
                (FlowchartLayoutRules.RepairCandidateMoves - Budget.CandidateMoves)
                / (FlowchartLayoutRules.RepairChainDepth - Depth));
            foreach (var Base in Level)
            {
                if (Base.Faults.Count == 0 || Budget.CandidateMoves >= LevelLimit) continue;
                var UnitChoices = Base.Faults.SelectMany(Fault => new[] { Fault.First, Fault.Second })
                    .Distinct(StringComparer.Ordinal).Where(Id => MovingOnly == null || Id == MovingOnly)
                    .Select(Id => (Id, Nodes: CellHintDependentUnit(Contract, Placed, Id))).ToList();
                if (MovingOnly == null && Base.Faults.Any(Fault => EnvelopeOf(Fault.First, Base.Plan.After.Regions) == null))
                {
                    // Empty frames remain derived frame obstacles. Moving a whole
                    // connected component offers a single atomic alternative to
                    // repeatedly pushing its foreign nodes into one another.
                    var Unseen = Placed.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                    while (Unseen.Count != 0)
                    {
                        var Members = new HashSet<string>(StringComparer.Ordinal); var Pending = new Queue<string>();
                        Pending.Enqueue(Unseen.First());
                        while (Pending.TryDequeue(out string? Id))
                        {
                            if (!Unseen.Remove(Id)) continue; Members.Add(Id);
                            foreach (var Edge in Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
                            { if (Edge.SourceId == Id && Unseen.Contains(Edge.TargetId)) Pending.Enqueue(Edge.TargetId);
                              if (Edge.TargetId == Id && Unseen.Contains(Edge.SourceId)) Pending.Enqueue(Edge.SourceId); }
                        }
                        if (Members.Count > 1) UnitChoices.Add((string.Join("|", Members.Order(StringComparer.Ordinal)), Placed.Where(Node => Members.Contains(Node.Id)).ToArray()));
                    }
                }
                var Units = UnitChoices
                    .Where(Unit => Unit.Nodes.Length != 0 && !Unit.Nodes.Any(Node => Node.PlacementHintLocked && Node.CellHintValidator == null))
                    .Where(Unit => Unit.Nodes.All(Node => Node.CellHintValidator != null)
                        || !HasExternalCellHintDependent(Contract, Unit.Id, Unit.Nodes)
                            && !Unit.Nodes.Any(Node => RouteRepairStage.HasHintedContainingGroup(Contract, Node.Id)))
                    .OrderBy(Unit => Unit.Nodes.Length).ThenBy(Unit => Unit.Id, StringComparer.Ordinal).ToArray();
                var Proposals = new List<(FlowchartCellRepair.Assignment Plan, int Faults, double Score)>();
                foreach (var Unit in Units)
                {
                    var MemberRegions = Unit.Nodes.Select(Node => Base.Plan.After.Regions[Node.Id]).ToArray();
                    var Envelope = new FlowchartCellRegion(MemberRegions.Min(Region => Region.StartRow), MemberRegions.Min(Region => Region.StartColumn),
                        MemberRegions.Max(Region => Region.EndRow) - MemberRegions.Min(Region => Region.StartRow),
                        MemberRegions.Max(Region => Region.EndColumn) - MemberRegions.Min(Region => Region.StartColumn));
                    var Deltas = new HashSet<(int Rows, int Columns)> { (-1, 0), (1, 0), (0, -1), (0, 1) };
                    foreach (var Fault in Base.Faults.Where(Fault => Fault.First == Unit.Id || Fault.Second == Unit.Id
                        || Unit.Nodes.Any(Node => Node.Id == Fault.First || Node.Id == Fault.Second)))
                    {
                        var Other = EnvelopeOf(Fault.First == Unit.Id || Unit.Nodes.Any(Node => Node.Id == Fault.First)
                            ? Fault.Second : Fault.First, Base.Plan.After.Regions);
                        if (Other == null)
                        {
                            // Empty frames have no member reservation. Enumerate
                            // whole-cell distances using the candidate limit, never
                            // chain depth as a spatial-distance limit.
                            for (int Distance = 1; Distance <= FlowchartLayoutRules.RepairCandidateMoves; Distance++)
                            { Deltas.Add((-Distance, 0)); Deltas.Add((Distance, 0)); Deltas.Add((0, -Distance)); Deltas.Add((0, Distance)); }
                        }
                        else
                        {
                            foreach (int Margin in new[] { 0, 1 })
                            {
                                Deltas.Add((checked(Other.StartRow - Envelope.EndRow - Margin), 0));
                                Deltas.Add((checked(Other.EndRow - Envelope.StartRow + Margin), 0));
                                Deltas.Add((0, checked(Other.StartColumn - Envelope.EndColumn - Margin)));
                                Deltas.Add((0, checked(Other.EndColumn - Envelope.StartColumn + Margin)));
                            }
                        }
                    }
                    foreach (var Delta in Deltas.Where(Delta => Delta != (0, 0)))
                    {
                        var Regions = new Dictionary<string, FlowchartCellRegion>(Base.Plan.After.Regions, StringComparer.Ordinal);
                        foreach (var Node in Unit.Nodes)
                        {
                            var Region = Regions[Node.Id];
                            Regions[Node.Id] = new(checked(Region.StartRow + Delta.Rows), checked(Region.StartColumn + Delta.Columns), Region.RowSpan, Region.ColumnSpan);
                        }
                        var Plan = MovingOnly == null ? Grow(Regions, GrowthOf(Base.Plan.After), true, true)
                            : FlowchartCellRepair.PlanAssignment(Placed, Regions, GrowthOf(Base.Plan.After), AllowHintedRegionRepair: true);
                        if (Plan != null) Proposals.Add((Plan, FaultsOf(Plan.After).Count,
                            FlowchartCellCandidateScore.Measure(Placed, Plan.After).Score));
                    }
                }
                foreach (var Proposal in Proposals.OrderBy(Item => Item.Faults).ThenBy(Item => Item.Score))
                {
                    if (Budget.CandidateMoves >= LevelLimit) break;
                    Offer(Proposal.Plan, true);
                }
            }
        }
        GroupFrameFitPassObserver?.Invoke($"CELL_FRAME_SEARCH_RESULT|candidates={Budget.CandidateMoves}|firstLegal={FirstLegal}|milliseconds={SearchClock.Elapsed.TotalMilliseconds:R}");
        if (Best == null || !Best.TryApply()) return false;
        ReplaceCellFrames(Contract, Placed, Frames);
        Budget.AcceptedMoves++;
        GroupFrameFitPassObserver?.Invoke($"cell frame winner score={BestScore:R}; gaps={Best.After.ColumnGaps.Count + Best.After.RowGaps.Count}; complete legal assignment");
        GroupFrameFitPassObserver?.Invoke("CELL_FRAME_WINNER_DETAIL|" + System.Text.Json.JsonSerializer.Serialize(new
        {
            Score = BestScore,
            Moves = Best.After.Regions.Where(Pair => Pair.Value != Original.Regions[Pair.Key]).Select(Pair => new
            { Id = Pair.Key, OldRow = Original.Regions[Pair.Key].StartRow, OldColumn = Original.Regions[Pair.Key].StartColumn,
                NewRow = Pair.Value.StartRow, NewColumn = Pair.Value.StartColumn }),
            Rows = FlowchartCellRepair.GrownGapsSince(Original, Best.After, false),
            Columns = FlowchartCellRepair.GrownGapsSince(Original, Best.After, true)
        }));
        return true;

        // Scores final complete assignments only. Preparing and projecting a
        // rejected option cannot leak regions, gaps, hints or frame objects.
        /// <summary>Measures one complete alternative against the named budget and retains only a legal score minimum.</summary>
        /// <param name="Plan">Prepared immutable regions and gaps.</param>
        /// <param name="KeepBase">Whether translations may be generated from this gap alternative.</param>
        void Offer(FlowchartCellRepair.Assignment? Plan, bool KeepBase)
        {
            if (Plan == null || ReferenceEquals(Plan.After, Original) || Budget.CandidateMoves >= FlowchartLayoutRules.RepairCandidateMoves) return;
            if (Placed[0].CellNode!.UsesRoutingCoordinates)
            {
                var Moved = Plan.Nodes.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                if (Contract.Edges.Count(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && (Moved.Contains(Edge.SourceId) || Moved.Contains(Edge.TargetId))) > FlowchartLayoutRules.RepairChainLines) return;
            }
            string Key = string.Join(";", Plan.After.Regions.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
                .Select(Pair => $"{Pair.Key}:{Pair.Value.StartRow},{Pair.Value.StartColumn}")) + "|"
                + string.Join(";", Plan.After.RowGaps.OrderBy(Pair => Pair.Key)) + "|" + string.Join(";", Plan.After.ColumnGaps.OrderBy(Pair => Pair.Key));
            if (!Seen.Add(Key)) return;
            Budget.CandidateMoves++;
            var Faults = FaultsOf(Plan.After);
            // Optional review evidence includes the entire unpublished option,
            // not just a conflict count that can conceal a missing combination.
            if (GroupFrameFitPassObserver != null)
                GroupFrameFitPassObserver("CELL_FRAME_CANDIDATE_DETAIL|" + System.Text.Json.JsonSerializer.Serialize(new
                {
                    Candidate = Budget.CandidateMoves, Faults,
                    Moves = Plan.After.Regions.Where(Pair => Pair.Value != Original.Regions[Pair.Key]).Select(Pair => new
                    { Id = Pair.Key, OldRow = Original.Regions[Pair.Key].StartRow, OldColumn = Original.Regions[Pair.Key].StartColumn,
                        NewRow = Pair.Value.StartRow, NewColumn = Pair.Value.StartColumn }),
                    Rows = FlowchartCellRepair.GrownGapsSince(Original, Plan.After, false),
                    Columns = FlowchartCellRepair.GrownGapsSince(Original, Plan.After, true)
                }));
            if (Faults.Count == 0 && FirstLegal == 0) FirstLegal = Budget.CandidateMoves;
            var Layout = new FlowchartLayoutResult();
            foreach (var Node in Placed) Layout.NodeBoundsMutable.Add(Node.CellNode!.Bounds(Plan.After.Regions[Node.Id], Node.GeometryProvider, Plan.After));
            foreach (var Frame in ProjectCellFrames(Contract, Placed, Plan.After)) Layout.ClusterBoundsMutable.Add(new()
            { NodeId = Frame.Id, CenterX = (Frame.Left + Frame.Right) / 2.0, CenterY = (Frame.Top + Frame.Bottom) / 2.0,
                Width = Frame.Right - Frame.Left, Height = Math.Abs(Frame.Top - Frame.Bottom) });
            double Score = FlowchartLayoutScore.MeasurePlacement(Layout);
            if (KeepBase) Bases.Add((Plan, Faults, Score));
            GroupFrameFitPassObserver?.Invoke($"cell frame candidate score={Score:R}; conflicts={Faults.Count}");
            if (Faults.Count == 0)
            {
                double Reading = FlowchartReadingOrderTie.Measure(Contract, Layout.NodeBounds);
                if (Score < BestScore - 0.0001
                    || Math.Abs(Score - BestScore) <= 0.0001 && Reading < BestReading)
                {
                    Best = Plan;
                    BestScore = Score;
                    BestReading = Reading;
                }
            }
        }

        /// <summary>Projects node and frame bounds from one complete candidate without publishing it.</summary>
        /// <param name="State">Immutable ownership and gaps.</param>
        /// <returns>Every remaining frame clearance conflict in deterministic order.</returns>
        List<CellFrameConflict> FaultsOf(FlowchartLogicalGrid.Snapshot State)
        {
            var Bounds = Placed.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Bounds(State.Regions[Node.Id], Node.GeometryProvider, State), StringComparer.Ordinal);
            return CellFrameConflicts(Contract, Placed, ProjectCellFrames(Contract, Placed, State), RequiredGap, Bounds);
        }

        /// <summary>Finds a node or group's declared logical envelope without deriving cell coordinates from drawing bounds.</summary>
        /// <param name="Id">Node or frame identity.</param>
        /// <param name="Regions">Complete proposed ownership.</param>
        /// <returns>Member envelope, or null for an empty frame.</returns>
        FlowchartCellRegion? EnvelopeOf(string Id, IReadOnlyDictionary<string, FlowchartCellRegion> Regions)
        {
            var Members = Placed.Where(Node => Node.Id == Id || IsInsideGroup(Contract, Node.Id, Id)).Select(Node => Regions[Node.Id]).ToArray();
            return Members.Length == 0 ? null : new(Members.Min(Region => Region.StartRow), Members.Min(Region => Region.StartColumn),
                Members.Max(Region => Region.EndRow) - Members.Min(Region => Region.StartRow), Members.Max(Region => Region.EndColumn) - Members.Min(Region => Region.StartColumn));
        }

        /// <summary>Retains actual rounded cumulative gap growth relative to the untouched entry snapshot.</summary>
        /// <param name="State">Candidate carrying the complete gaps.</param>
        /// <returns>Normalized changes for a fresh atomic plan from the original state.</returns>
        List<FlowchartCellGapGrowth> GrowthOf(FlowchartLogicalGrid.Snapshot State) =>
            FlowchartCellRepair.GrownGapsSince(Original, State, true).Select(Pair => new FlowchartCellGapGrowth(true, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX))
                .Concat(FlowchartCellRepair.GrownGapsSince(Original, State, false).Select(Pair => new FlowchartCellGapGrowth(false, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX))).ToList();

        /// <summary>Builds a complete shared-gap alternative from measured shortfalls within the existing chain-depth bound.</summary>
        /// <param name="Regions">Whole-cell ownership, optionally including a complete translated unit.</param>
        /// <param name="Growth">Already planned rounded gap increases.</param>
        /// <param name="Columns">Whether external column-gap alternatives are eligible.</param>
        /// <param name="Rows">Whether external row-gap alternatives are eligible.</param>
        /// <returns>An unpublished candidate; infeasible hint or occupancy changes return null.</returns>
        FlowchartCellRepair.Assignment? Grow(Dictionary<string, FlowchartCellRegion> Regions, List<FlowchartCellGapGrowth> Growth, bool Columns, bool Rows)
        {
            var Plan = FlowchartCellRepair.PlanAssignment(Placed, Regions, Growth, AllowHintedRegionRepair: true);
            for (int Depth = 0; Plan != null && Depth < FlowchartLayoutRules.RepairChainDepth; Depth++)
            {
                var Extra = new Dictionary<(bool Columns, int After), double>();
                foreach (var Fault in FaultsOf(Plan.After))
                {
                    var A = EnvelopeOf(Fault.First, Regions); var B = EnvelopeOf(Fault.Second, Regions);
                    if (A == null || B == null) continue;
                    foreach (bool Axis in new[] { true, false }.Where(Axis => Axis ? Columns : Rows))
                    {
                        int AStart = Axis ? A.StartColumn : A.StartRow, AEnd = Axis ? A.EndColumn : A.EndRow;
                        int BStart = Axis ? B.StartColumn : B.StartRow, BEnd = Axis ? B.EndColumn : B.EndRow;
                        if (AEnd > BStart && BEnd > AStart) continue;
                        int First = Math.Min(AEnd, BEnd) - 1, Last = Math.Max(AStart, BStart) - 1;
                        double Shortfall = Axis ? Fault.ShortfallX : Fault.ShortfallY;
                        var Choices = Regions.Values.SelectMany(Region => Axis
                                ? new[] { Region.StartColumn - 1, Region.EndColumn - 1 } : new[] { Region.StartRow - 1, Region.EndRow - 1 })
                            .Distinct().Where(After => After >= First && After <= Last
                                && !Regions.Values.Any(Region => Axis ? Region.StartColumn <= After && Region.EndColumn > After + 1
                                    : Region.StartRow <= After && Region.EndRow > After + 1));
                        int? BestGap = null; double Score = double.PositiveInfinity;
                        foreach (int After in Choices)
                        {
                            var TrialGrowth = GrowthOf(Plan.After); TrialGrowth.Add(new(Axis, After, Shortfall, DiagramGridMetrics.GRID_UNIT_PX));
                            var Trial = FlowchartCellRepair.PlanAssignment(Placed, Regions, TrialGrowth, AllowHintedRegionRepair: true);
                            if (Trial == null) continue;
                            double Cost = FlowchartCellCandidateScore.Measure(Placed, Trial.After).Score;
                            if (Cost < Score) { BestGap = After; Score = Cost; }
                        }
                        if (BestGap.HasValue) Extra[(Axis, BestGap.Value)] = Math.Max(Extra.GetValueOrDefault((Axis, BestGap.Value)), Shortfall);
                    }
                }
                if (Extra.Count == 0) break;
                Growth = GrowthOf(Plan.After);
                Growth.AddRange(Extra.Select(Pair => new FlowchartCellGapGrowth(Pair.Key.Columns, Pair.Key.After, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)));
                var Next = FlowchartCellRepair.PlanAssignment(Placed, Regions, Growth, AllowHintedRegionRepair: true);
                if (Next == null) break;
                Plan = Next;
            }
            return Plan;
        }
    }
}
