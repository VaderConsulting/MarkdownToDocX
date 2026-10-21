using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    /// <summary>Observes fully routed initial-wrap candidates without changing their order, limits or acceptance.</summary>
    internal static Action<int, FlowchartLayoutResult, FlowchartLayoutScore.ComparisonCost, bool>? BandWrapCandidateObserver;
    /// <summary>Selects initial Enterprise band wrapping only after complete ports, routes, repairs and validation exist.</summary>
    /// <param name="Contract">Original contract; discarded candidates restore every hint status.</param>
    /// <param name="LaidOut">Unmodified Dagre seed, copied independently by each complete candidate.</param>
    /// <param name="RenderContext">Owning render state, reused only for the final selected layout.</param>
    /// <returns>The fully routed lowest-cost legal candidate; unrelated diagrams retain their existing path.</returns>
    private static FlowchartLayoutResult ApplyWithRoutedBands(DiagramContract Contract, FlowchartLayoutResult LaidOut, FlowchartRenderContext? RenderContext)
    {
        var Context = RenderContext ?? LaidOut.RenderContext;
        if (!Context.UseCellRegions || Contract.LayoutMode == MermaidRecognition.Config.DiagramLayoutMode.Open
            || Contract.Nodes.Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
                .Select(Node => EnterpriseElementCatalog.Elements[Enum.Parse<EnterpriseElementType>(((RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)].Layer)
                .Distinct().Count() <= 1)
            return ApplyCore(Contract, LaidOut, RenderContext, false);
        var OriginalHints = new BandHintState(Contract);
        Context.EnterpriseBandWrapWidth = int.MaxValue;
        FlowchartLayoutResult Best;
        Context.InitialBandCapacityNeeded = false;
        Context.DeferInitialBandCapacity = true;
        try { using (FlowchartRenderContext.Activate(Context)) Best = ApplyCore(Contract, LaidOut, Context, false); }
        finally { Context.DeferInitialBandCapacity = false; }
        var BestHints = new BandHintState(Contract);
        var SelectedPlacement = Best;
        bool NeedsCapacity = Context.InitialBandCapacityNeeded;
        var BestCost = FlowchartLayoutScore.Measure(Contract, Best, false).Cost;
        bool Legal = BandLayoutIsLegal(Contract, Best);
        BandWrapCandidateObserver?.Invoke(int.MaxValue, Best, BestCost, Legal);
        int Width = int.MaxValue;
        var Records = new List<string>();
        foreach (int CandidateWidth in Context.EnterpriseBandWrapWidths.Where(Value => Value != int.MaxValue))
        {
            OriginalHints.Restore();
            var CandidateContext = new FlowchartRenderContext { UseCellRegions = true, EnterpriseBandWrapWidth = CandidateWidth,
                DeferInitialBandCapacity = true,
                DisableExpandedBendRepairsForTests = Context.DisableExpandedBendRepairsForTests,
                DisableFinishedBendRepairsForTests = Context.DisableFinishedBendRepairsForTests,
                UsePreviousEnterpriseBandPaddingForTests = Context.UsePreviousEnterpriseBandPaddingForTests };
            FlowchartLayoutResult Candidate;
            using (FlowchartRenderContext.Activate(CandidateContext)) Candidate = ApplyCore(Contract, LaidOut, CandidateContext, false);
            var Score = FlowchartLayoutScore.Measure(Contract, Candidate, false);
            bool CandidateLegal = BandLayoutIsLegal(Contract, Candidate);
            BandWrapCandidateObserver?.Invoke(CandidateWidth, Candidate, Score.Cost, CandidateLegal);
            bool Keep = CandidateLegal && (!Legal || Score.Cost < BestCost);
            Records.Add($"BAND_WRAP|width={CandidateWidth}|legal={CandidateLegal}|score={Score.OptimizationScore:R}|bends={Score.Bends}|crossings={Score.Crossings}|keep={Keep}");
            if (!Keep) continue;
            Width = CandidateWidth; BestCost = Score.Cost; Legal = true;
            SelectedPlacement = Candidate; NeedsCapacity = CandidateContext.InitialBandCapacityNeeded;
        }
        // A trial owns a separate grid, routes and caches. Confirm the winner in
        // the caller's context rather than transplanting a candidate's ownership.
        // Capacity is sized by the winner's initial region producer. Keep the
        // established wrap/order fixed instead of selecting a different layout
        // merely because the new gap widths change its aspect cost.
        var SelectedCells = SelectedPlacement.CellNodes![0].CellNode!.Grid.Capture();
        NeedsCapacity = NeedsCapacity && Enterprise.EnterpriseBandPlacement.PlanInitialRoutingCapacity(
            Contract, SelectedCells.Regions, SelectedCells.Owner, null, SelectedCells).Count > 0;
        if (Width != int.MaxValue || NeedsCapacity)
        {
            OriginalHints.Restore(); Context.BeginRender(); Context.EnterpriseBandWrapWidth = Width;
            Context.BandSpaceDiagnostics.Clear(); Context.BandPreflightDrops.Clear();
            Context.InitialBandPlacement = NeedsCapacity ? SelectedPlacement.CellNodes![0].CellNode!.Grid.Capture() : null;
            Context.DeferInitialBandCapacity = !NeedsCapacity;
            try { using (FlowchartRenderContext.Activate(Context)) Best = ApplyCore(Contract, LaidOut, Context, false); }
            finally { Context.InitialBandPlacement = null; Context.DeferInitialBandCapacity = false; }
        }
        else BestHints.Restore();
        Context.BandSpaceDiagnostics.AddRange(Records);
        Context.BandSpaceDiagnostics.Add("BAND_WRAP_SELECTED|width=" + Width + "|score=" + (double)FlowchartLayoutScore.Measure(Contract, Best, false).Cost);
        return NeedsCapacity ? ConfirmReservedCorridorAssignment(Contract, LaidOut, Best, SelectedPlacement,
            SelectedCells, Width, Context, OriginalHints) : Best;
    }

    /// <summary>Confirms complete published terminal geometry, reserved titles, frames, ownership and hard route rules.</summary>
    /// <param name="Contract">Declared source and group membership.</param>
    /// <param name="Layout">Completed candidate.</param>
    /// <returns>Whether this candidate is eligible for a quality comparison.</returns>
    private static bool BandLayoutIsLegal(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        if (Layout.EdgeRoutes.Count != Contract.Edges.Count || Layout.EdgeRoutes.Any(Route => Route.Points.Count < 2)
            || Layout.ClusterBounds.Any(Box => !double.IsFinite(Box.Width) || !double.IsFinite(Box.Height) || Box.Width <= 0 || Box.Height <= 0)
            || Layout.RenderContext.LayoutStageViolations.Any(Item => Item.Kind is "NoLegalRoute" or "NodeOverlap" or "FrameOverlap")) return false;
        if (FlowchartRouteRuleChecker.Check(Contract, Layout).Any(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7")) return false;
        var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        var Prepared = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds);
        return Layout.EdgeRoutes.All(Route => FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route,
            Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(), Prepared));
    }

    /// <summary>Owns the only mutable source-contract data used by a complete initial-placement trial.</summary>
    private sealed class BandHintState
    {
        private readonly (PlacementHintContract Hint, bool Active, string? Reason)[] Placement;
        private readonly (RoutingHintContract Hint, bool Active, string? Reason)[] Routing;
        /// <summary>Captures original statuses before a private layout can reject a conflicting hint.</summary>
        /// <param name="Contract">Original hint-bearing declarations.</param>
        internal BandHintState(DiagramContract Contract)
        {
            Placement = Contract.Nodes.Select(Node => Node.PlacementHint).Concat(Contract.Groups.Select(Group => Group.PlacementHint))
                .OfType<PlacementHintContract>().Select(Hint => (Hint, Hint.IsActive, Hint.UnhonouredReason)).ToArray();
            Routing = Contract.Edges.Select(Edge => Edge.RoutingHint).OfType<RoutingHintContract>()
                .Select(Hint => (Hint, Hint.IsActive, Hint.UnhonouredReason)).ToArray();
        }
        /// <summary>Restores exact statuses, leaving regions, routes and caches isolated in their owning contexts.</summary>
        internal void Restore()
        {
            foreach (var Item in Placement) { Item.Hint.IsActive = Item.Active; Item.Hint.UnhonouredReason = Item.Reason; }
            foreach (var Item in Routing) { Item.Hint.IsActive = Item.Active; Item.Hint.UnhonouredReason = Item.Reason; }
        }
    }
}
