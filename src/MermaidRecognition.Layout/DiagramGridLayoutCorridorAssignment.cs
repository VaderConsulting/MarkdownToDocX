using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Compares complete reserved-corridor route assignments before initial layout publication.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Compares complete initial route assignments on the same cells and reserved gaps before publishing the selected layout.</summary>
    /// <param name="Contract">Original links and hint declarations.</param>
    /// <param name="Seed">Unmodified initial graph placement shared by isolated producers.</param>
    /// <param name="Allocated">Completed reserved-track assignment in the caller's context.</param>
    /// <param name="BeforeGrowth">Selected complete cell placement before new gap widths, used to retain its bend ceiling.</param>
    /// <param name="Cells">Immutable selected occupancy and pre-existing gaps.</param>
    /// <param name="Width">Already selected wrap, never changed by this comparison.</param>
    /// <param name="Context">Caller-owned final render state.</param>
    /// <param name="OriginalHints">Original hint statuses restored before each independent producer.</param>
    /// <returns>The lower-score admissible assignment, confirmed in the caller's context with exact discarded-candidate hint restoration.</returns>
    private static FlowchartLayoutResult ConfirmReservedCorridorAssignment(DiagramContract Contract,
        FlowchartLayoutResult Seed, FlowchartLayoutResult Allocated, FlowchartLayoutResult BeforeGrowth,
        FlowchartLogicalGrid.Snapshot Cells, int Width, FlowchartRenderContext Context, BandHintState OriginalHints)
    {
        var AllocatedHints = new BandHintState(Contract);
        var ReferenceContext = new FlowchartRenderContext { UseCellRegions = true, EnterpriseBandWrapWidth = Width,
            InitialBandPlacement = Cells, SuppressReservedCorridorAssignment = true,
            DisableExpandedBendRepairsForTests = Context.DisableExpandedBendRepairsForTests,
            DisableFinishedBendRepairsForTests = Context.DisableFinishedBendRepairsForTests,
            UsePreviousEnterpriseBandPaddingForTests = Context.UsePreviousEnterpriseBandPaddingForTests };
        FlowchartLayoutResult Reference;
        OriginalHints.Restore();
        try { using (FlowchartRenderContext.Activate(ReferenceContext)) Reference = ApplyCore(Contract, Seed, ReferenceContext, false); }
        finally { AllocatedHints.Restore(); }
        var Score = FlowchartLayoutScore.Measure(Contract, Allocated, false);
        var PlainScore = FlowchartLayoutScore.Measure(Contract, Reference, false);
        var Counts = RouteRepairStage.FinalCounts(Allocated);
        var PlainCounts = RouteRepairStage.FinalCounts(Reference);
        var PlainFindings = FlowchartRouteRuleChecker.Check(Contract, Reference).GroupBy(Finding => Finding.Rule)
            .ToDictionary(Group => Group.Key, Group => Group.Count(), StringComparer.Ordinal);
        bool FindingsDoNotRise = FlowchartRouteRuleChecker.Check(Contract, Allocated).GroupBy(Finding => Finding.Rule)
            .All(Group => Group.Count() <= PlainFindings.GetValueOrDefault(Group.Key));
        var Proposed = Allocated.CellNodes![0].CellNode!.Grid.Capture();
        var Ordinary = Reference.CellNodes![0].CellNode!.Grid.Capture();
        bool SameCellsAndGaps = Proposed.Regions.Count == Ordinary.Regions.Count
            && Proposed.Regions.All(Pair => Ordinary.Regions.GetValueOrDefault(Pair.Key) == Pair.Value)
            && Proposed.ColumnGaps.Count == Ordinary.ColumnGaps.Count && Proposed.RowGaps.Count == Ordinary.RowGaps.Count
            && Proposed.ColumnGaps.All(Pair => Ordinary.ColumnGaps.GetValueOrDefault(Pair.Key) == Pair.Value)
            && Proposed.RowGaps.All(Pair => Ordinary.RowGaps.GetValueOrDefault(Pair.Key) == Pair.Value);
        bool Keep = BandLayoutIsLegal(Contract, Allocated) && SameCellsAndGaps && FindingsDoNotRise
            && Score.Cost < PlainScore.Cost && Counts.Crossings <= PlainCounts.Crossings && Counts.Shared <= PlainCounts.Shared
            && Counts.FanCrossings <= PlainCounts.FanCrossings && Score.HintsHonoured >= PlainScore.HintsHonoured
            && Counts.Bends <= FlowchartLayoutScore.Measure(Contract, BeforeGrowth, false).Bends;
        string Decision = $"CORRIDOR_ASSIGNMENT|keepReserved={Keep}|score={Score.OptimizationScore:R}|reference={PlainScore.OptimizationScore:R}"
            + $"|bends={Counts.Bends}/{PlainCounts.Bends}|crossings={Counts.Crossings}/{PlainCounts.Crossings}|shared={Counts.Shared}/{PlainCounts.Shared}";
        if (Keep) { Context.BandSpaceDiagnostics.Add(Decision); return Allocated; }
        var WrapEvidence = Context.BandSpaceDiagnostics.Where(Item => Item.StartsWith("BAND_WRAP|", StringComparison.Ordinal)
            || Item.StartsWith("BAND_WRAP_SELECTED|", StringComparison.Ordinal)).ToArray();
        // Each alternative owns its grid, ports, guides and caches. Confirm the
        // ordinary assignment rather than transplanting a discarded context.
        OriginalHints.Restore(); Context.BeginRender(); Context.EnterpriseBandWrapWidth = Width;
        Context.SuppressReservedCorridorAssignment = true;
        Context.InitialBandPlacement = Cells; Context.BandSpaceDiagnostics.Clear(); Context.BandPreflightDrops.Clear();
        FlowchartLayoutResult Result;
        try { using (FlowchartRenderContext.Activate(Context)) Result = ApplyCore(Contract, Seed, Context, false); }
        finally { Context.InitialBandPlacement = null; }
        Context.BandSpaceDiagnostics.AddRange(WrapEvidence);
        Context.BandSpaceDiagnostics.Add(Decision);
        return Result;
    }
}
