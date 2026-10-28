using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Compares complete reserved-corridor route assignments before initial layout publication.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Observes completed assignment alternatives for diagnostics without changing their order or score.</summary>
    internal static Action<string, FlowchartLayoutResult, bool, string>? CorridorAssignmentCandidateObserver { get; set; }

    /// <summary>Compares the completed allocated and fallback assignments and any compatible, spacing-legal placement baseline on identical cells and gaps.</summary>
    /// <param name="Contract">Original links and hint declarations.</param>
    /// <param name="Seed">Unmodified initial graph placement shared by isolated producers.</param>
    /// <param name="Allocated">Completed reserved-track assignment in the caller's context.</param>
    /// <param name="BeforeGrowth">Existing placement preview; eligible only if its complete geometry still uses the selected regions and gaps.</param>
    /// <param name="Cells">Immutable selected occupancy and pre-existing gaps.</param>
    /// <param name="Width">Already selected wrap, never changed by this comparison.</param>
    /// <param name="Context">Caller-owned final render state.</param>
    /// <param name="OriginalHints">Original hint statuses restored before each independent producer.</param>
    /// <returns>The lowest-scoring hard-legal complete assignment, retaining exact hint state and independent ownership.</returns>
    private static FlowchartLayoutResult ConfirmReservedCorridorAssignment(DiagramContract Contract,
        FlowchartLayoutResult Seed, FlowchartLayoutResult Allocated, FlowchartLayoutResult BeforeGrowth,
        FlowchartLogicalGrid.Snapshot Cells, int Width, FlowchartRenderContext Context, BandHintState OriginalHints)
    {
        BandHintState AllocatedHints = new BandHintState(Contract);
        FlowchartRenderContext ReferenceContext = new FlowchartRenderContext { UseCellRegions = true, EnterpriseBandWrapWidth = Width,
            LayoutOptions = Context.LayoutOptions,
            InitialBandPlacement = Cells, SuppressReservedCorridorAssignment = true,
            DisableExpandedBendRepairsForTests = Context.DisableExpandedBendRepairsForTests,
            DisableFinishedBendRepairsForTests = Context.DisableFinishedBendRepairsForTests,
            UsePreviousEnterpriseBandPaddingForTests = Context.UsePreviousEnterpriseBandPaddingForTests };
        FlowchartLayoutResult Reference;
        OriginalHints.Restore();
        using (FlowchartRenderContext.Activate(ReferenceContext))
        {
            Reference = ApplyCore(Contract, Seed, ReferenceContext, false);
        }

        BandHintState ReferenceHints = new BandHintState(Contract);
        List<(string Name, FlowchartLayoutResult Layout, BandHintState Hints, FlowchartLayoutScore.Result Score, bool Legal)> Candidates = [];
        List<string> Diagnostics = [];
        Add("allocated", Allocated, AllocatedHints, false);
        int RequiredHints = Candidates[0].Score.HintsHonoured;
        Add("fallback", Reference, ReferenceHints, false);
        Add("placement-baseline", BeforeGrowth, OriginalHints, true);
        for (int I = 0; I < Candidates.Count; I++)
        {
            if (Candidates[I].Score.HintsHonoured < RequiredHints)
            {
                Candidates[I] = Candidates[I] with { Legal = false };
            }
        }

        int Winner = LowestCostCompleteAssignment(Candidates.Select(Item => (Item.Legal, Item.Score.Cost)).ToArray());
        // A discarded proposal must not force a more expensive fallback. The
        // old independent count ceilings could do that despite both paths being legal.
        if (Winner < 0)
        {
            AllocatedHints.Restore();
            Context.BandSpaceDiagnostics.Add("CORRIDOR_ASSIGNMENT|noLegalCandidate=True");
            return Allocated;
        }
        (string Name, FlowchartLayoutResult Layout, BandHintState Hints, FlowchartLayoutScore.Result Score, bool Legal) Selected = Candidates[Winner];
        bool Keep = Selected.Name == "allocated";
        (int Bends, int Crossings, int Shared, int FanCrossings, HashSet<string> FanPairs) Counts = RouteRepairStage.FinalCounts(Allocated);
        (int Bends, int Crossings, int Shared, int FanCrossings, HashSet<string> FanPairs) PlainCounts = RouteRepairStage.FinalCounts(Reference);
        string Decision = $"CORRIDOR_ASSIGNMENT|keepReserved={Keep}|selected={Selected.Name}|score={Candidates[0].Score.OptimizationScore:R}|reference={Candidates[1].Score.OptimizationScore:R}"
            + $"|bends={Counts.Bends}/{PlainCounts.Bends}|crossings={Counts.Crossings}/{PlainCounts.Crossings}|shared={Counts.Shared}/{PlainCounts.Shared}";
        if (Selected.Name != "fallback")
        {
            Selected.Hints.Restore();
            Selected.Layout.RenderContext.BandSpaceDiagnostics.AddRange(Diagnostics);
            Selected.Layout.RenderContext.BandSpaceDiagnostics.Add(Decision);
            return Selected.Layout;
        }
        var WrapEvidence = Context.BandSpaceDiagnostics.Where(Item => Item.StartsWith("BAND_WRAP|", StringComparison.Ordinal)
            || Item.StartsWith("BAND_WRAP_SELECTED|", StringComparison.Ordinal)).ToArray();
        OriginalHints.Restore(); Context.BeginRender(); Context.EnterpriseBandWrapWidth = Width;
        Context.SuppressReservedCorridorAssignment = true;
        Context.InitialBandPlacement = Cells; Context.BandSpaceDiagnostics.Clear(); Context.BandPreflightDrops.Clear();
        FlowchartLayoutResult Result;
        try
        {
            using (FlowchartRenderContext.Activate(Context))
            {
                Result = ApplyCore(Contract, Seed, Context, false);
            }

            CompletePublishedRoutes(Contract, Result);
        }
        finally { Context.InitialBandPlacement = null; }
        Result.RenderContext.BandSpaceDiagnostics.AddRange(WrapEvidence);
        Result.RenderContext.BandSpaceDiagnostics.AddRange(Diagnostics);
        Result.RenderContext.BandSpaceDiagnostics.Add(Decision);
        return Result;

        void Add(string Name, FlowchartLayoutResult Layout, BandHintState Hints, bool RequireSpacing)
        {
            Hints.Restore();
            bool SamePlacement = SameCorridorCellsAndGaps(Allocated, Layout);
            if (SamePlacement)
            {
                CompletePublishedRoutes(Contract, Layout);
            }

            FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
            int Close = CloseCorridorSegmentPairs(Layout);
            bool HardLegal = BandLayoutIsLegal(Contract, Layout)
                && !FlowchartRouteRuleChecker.Check(Contract, Layout).Any(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7");
            bool Legal = SamePlacement && HardLegal && (!RequireSpacing || Close == 0);
            string Reason = !SamePlacement ? "different-regions-or-gaps" : !HardLegal ? "hard-legality"
                : RequireSpacing && Close > 0 ? "baseline-breaks-line-spacing" : "eligible";
            Candidates.Add((Name, Layout, new BandHintState(Contract), Score, Legal));
            Diagnostics.Add($"CORRIDOR_CANDIDATE|{Name}|eligible={Legal}|reason={Reason}|score={Score.OptimizationScore:R}|bends={Score.Bends}|crossings={RouteRepairStage.FinalCounts(Layout).Crossings}|close={Close}");
            CorridorAssignmentCandidateObserver?.Invoke(Name, Layout, Legal, Reason);
        }
    }

    /// <summary>Selects the cheapest completed legal option in stable candidate order; an ineligible proposal never makes a worse fallback preferable.</summary>
    /// <param name="Candidates">Measured legality and unchanged rule 2.13 comparison costs.</param>
    /// <returns>The cheapest legal candidate index, or minus one if no legal option exists.</returns>
    internal static int LowestCostCompleteAssignment(IReadOnlyList<(bool Legal, FlowchartLayoutScore.ComparisonCost Cost)> Candidates)
    {
        int Best = -1;
        for (int I = 0; I < Candidates.Count; I++)
        {
            if (Candidates[I].Legal && (Best < 0 || Candidates[I].Cost < Candidates[Best].Cost))
            {
                Best = I;
            }
        }

        return Best;
    }

    /// <summary>Counts all distinct-route parallel segment pairs violating the configured pixel separation.</summary>
    /// <param name="Layout">Completed candidate geometry.</param>
    /// <returns>Number of close parallel segment pairs, including exact overlaps and terminal runs.</returns>
    internal static int CloseCorridorSegmentPairs(FlowchartLayoutResult Layout)
    {
        (double X, double Y)[][] Paths = [.. Layout.EdgeRoutes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())];
        int Count = 0;
        for (int I = 0; I < Paths.Length; I++)
        {
            for (int J = I + 1; J < Paths.Length; J++)
            {
                Count += FlowchartFallbackCorridorTracks.CloseParallelSegments(Paths[I], Paths[J]);
            }
        }

        return Count;
    }

    /// <summary>Checks exact ownership and external gaps before comparing complete route assignments.</summary>
    /// <param name="First">Selected fixed geometry.</param>
    /// <param name="Second">Alternative private layout.</param>
    /// <returns>Whether both layouts retain precisely the same cell reservations and row and column gaps.</returns>
    private static bool SameCorridorCellsAndGaps(FlowchartLayoutResult First, FlowchartLayoutResult Second)
    {
        if (First.CellNodes is not { Count: > 0 } || Second.CellNodes is not { Count: > 0 })
        {
            return false;
        }

        FlowchartLogicalGrid.Snapshot A = First.CellNodes[0].CellNode!.Grid.Capture(); FlowchartLogicalGrid.Snapshot B = Second.CellNodes[0].CellNode!.Grid.Capture();
        return A.Regions.Count == B.Regions.Count && A.Regions.All(Pair => B.Regions.GetValueOrDefault(Pair.Key) == Pair.Value)
            && A.ColumnGaps.Count == B.ColumnGaps.Count && A.RowGaps.Count == B.RowGaps.Count
            && A.ColumnGaps.All(Pair => B.ColumnGaps.GetValueOrDefault(Pair.Key) == Pair.Value)
            && A.RowGaps.All(Pair => B.RowGaps.GetValueOrDefault(Pair.Key) == Pair.Value);
    }
}
