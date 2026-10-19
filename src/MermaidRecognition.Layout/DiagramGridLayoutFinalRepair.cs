using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    /// <summary>Measurement-only override for the last pass; no earlier repair reads it and production leaves it unset.</summary>
    internal static double? FinalRepairLengthCapForTests;
    /// <summary>Reports complete candidate decisions and their measured added length.</summary>
    internal static Action<string, string, double>? FinalRepairObserver;
    /// <summary>Exposes immutable decision boundaries for exact rollback regression tests.</summary>
    internal static Action<string, FlowchartLayoutResult, IReadOnlyList<GridNode>>? FinalRepairStateObserver;
    /// <summary>Supplies one explicit diagnostic region assignment; production leaves it unset.</summary>
    internal static Func<IReadOnlyList<GridNode>, IReadOnlyDictionary<string, FlowchartCellRegion>>? FinalRepairRegionForTests;
    /// <summary>Exposes fully rerouted diagnostic geometry before any admission guard, without retaining it.</summary>
    internal static Action<string, FlowchartLayoutResult>? FinalRepairMeasurementObserver;

    private static partial class RouteRepairStage
    {
        /// <summary>A complete existing route or cell assignment, not a mutation of the published layout.</summary>
        /// <param name="Key">Stable candidate identity.</param>
        /// <param name="Build">Applies its existing assignment factory.</param>
        /// <param name="ReplayAfterRouting">Whether a track/port assignment must be replayed after reference routing.</param>
        private sealed record FinalOption(string Key, Func<bool> Build, bool ReplayAfterRouting);

        /// <summary>Runs the identity exception only after every ordinary repair, with complete reference routing and whole-layout checks.</summary>
        /// <param name="State">Settled cell-owned layout; nothing except publication follows this stage.</param>
        internal static void ApplyFinalCompleteRepair(GridLayoutState State)
        {
            if (State.SkipRootGroupRepacking || !State.Placed.Any(Node => Node.CellNode != null)
                || State.Copy.RenderContext.IsEvaluatingCellRouteCandidate) return;
            using var Timing = FlowchartCellCostTiming.Begin("final-complete-repair");
            var Layout = State.Copy;
            var Bounds = BuildPlacedBounds(State.Placed, State.Frames);
            var Original = new CellGeometrySnapshot(State);
            var Before = FlowchartLayoutScore.Measure(State.Contract, Layout, false);
            var Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
            var BeforeCounts = FinalCounts(State);
            var DeclaredRoutes = Layout.EdgeRoutes.Select(Route => Route.EdgeId).OrderBy(Id => Id, StringComparer.Ordinal).ToArray();
            double BeforeLength = RouteLength(Layout);
            int Examined = 0, Accepted = 0;
            var Queues = new[] { PairOptions().GetEnumerator(), RegionOptions().GetEnumerator() }.ToList();
            try
            {
                while (Queues.Count > 0 && Examined < FlowchartLayoutRules.RepairCandidateMoves && Accepted < FlowchartLayoutRules.RepairChainDepth)
                for (int Index = 0; Index < Queues.Count && Examined < FlowchartLayoutRules.RepairCandidateMoves && Accepted < FlowchartLayoutRules.RepairChainDepth; Index++)
                {
                    if (!Queues[Index].MoveNext()) { Queues[Index].Dispose(); Queues.RemoveAt(Index--); continue; }
                    var Option = Queues[Index].Current;
                    bool Keep = false;
                    double ExtraLength = 0;
                    string Reason = "assignment";
                    FinalRepairStateObserver?.Invoke("before", Layout, State.Placed);
                    try
                    {
                        // Partial factories can fail to form an assignment. Such
                        // failures never become fully routed layout candidates.
                        if (!Option.Build()) continue;
                        Examined++;
                        // Every line is rerouted, including lines remote from
                        // the repair. The subsequent assignment replay fixes the
                        // candidate's proposed tracks/slots on those fresh routes.
                        using (FlowchartCellCostTiming.ForRoutingPhase("final-confirmation"))
                            CompleteReferenceCellCandidate(State);
                        Bounds = BuildPlacedBounds(State.Placed, State.Frames);
                        if (Option.ReplayAfterRouting && !Option.Build()) { Reason = "replay"; continue; }
                        FinalRepairMeasurementObserver?.Invoke(Option.Key, Layout);
                        if (!Layout.EdgeRoutes.Select(Route => Route.EdgeId).OrderBy(Id => Id, StringComparer.Ordinal).SequenceEqual(DeclaredRoutes))
                        { Reason = "route-inventory"; continue; }
                        if (Layout.RenderContext.LayoutStageViolations.Any(Failure => Failure.Kind is "NoLegalRoute" or "MissingRoute"))
                        { Reason = "unrouted-line"; continue; }
                        // Completion may rebuild public envelopes. Validate that
                        // finished projection, not just the pre-routing frames.
                        if (Layout.ClusterBounds.Any(Box => !double.IsFinite(Box.Width) || !double.IsFinite(Box.Height)
                            || Box.Width <= 0 || Box.Height <= 0)) { Reason = "invalid-frame"; continue; }
                        var PublishedFrames = Layout.ClusterBounds.Select(Box => new GridFrame {
                            Id = Box.NodeId, Left = (int)Math.Round(Box.CenterX - Box.Width / 2),
                            Right = (int)Math.Round(Box.CenterX + Box.Width / 2),
                            Bottom = (int)Math.Round(Box.CenterY - Box.Height / 2), Top = (int)Math.Round(Box.CenterY + Box.Height / 2)
                        }).ToArray();
                        if (CountUnrelatedFrameOverlaps(State.Contract, PublishedFrames) != 0) { Reason = "frame-overlap"; continue; }
                        int Changed = Original.ChangedLines();
                        if (Changed > FlowchartLayoutRules.RepairChainLines) { Reason = "12-line-limit:" + Changed; continue; }
                        ExtraLength = RouteLength(Layout) - BeforeLength;
                        double Cap = FinalRepairLengthCapForTests ?? FlowchartLayoutRules.RepairExtraLength;
                        if (ExtraLength > Cap) { Reason = $"length:{ExtraLength:R}>{Cap:R}"; continue; }
                        var AfterCounts = FinalCounts(State);
                        if (AfterCounts.Bends > BeforeCounts.Bends) { Reason = "bends"; continue; }
                        if (AfterCounts.Crossings > BeforeCounts.Crossings) { Reason = "crossings"; continue; }
                        if (AfterCounts.Shared > BeforeCounts.Shared) { Reason = "shared-tracks"; continue; }
                        if (AfterCounts.FanCrossings > BeforeCounts.FanCrossings) { Reason = "same-fan-count"; continue; }
                        if (AfterCounts.FanPairs.Except(BeforeCounts.FanPairs).Any()) { Reason = "same-fan-crossing"; continue; }
                        if (!Original.HintsUnchanged() || !FlowchartPlacementHints.CellHintsMatch(State.Contract, State.Placed,
                            FlowchartCellRepair.Capture(State.Placed).Regions)) { Reason = "original-hints"; continue; }
                        var AfterFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
                        if (AfterFindings.Count > Findings.Count) { Reason = "total-findings"; continue; }
                        if (AfterFindings.Any(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"))
                        { Reason = "hard-gate"; continue; }
                        var Prepared = FlowchartAllocatedPortFallback.Prepare(State.Contract, Bounds);
                        if (Layout.EdgeRoutes.Any(Route => !FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route,
                            Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(), Prepared)))
                        { Reason = "whole-layout-legality"; continue; }
                        if (Layout.EdgeRoutes.Any(Route => !FlowchartPortSideRouteRepair.HasLabelRoom(Route.Points,
                            FindContractEdge(State.Contract, Route), State.Contract, OrthogonalRunsOnly: true)))
                        { Reason = "whole-layout-label-clearance"; continue; }
                        var After = FlowchartLayoutScore.Measure(State.Contract, Layout, false);
                        var Cost = After.Cost + ExtraLength / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
                            + Changed * FlowchartLayoutRules.RepairMovedLineWeight;
                        if (!FlowchartFinalRepairPolicy.Allows(Findings, AfterFindings,
                            FlowchartFinalRepairPolicy.Measure(Before, () => BeforeCounts.Crossings),
                            FlowchartFinalRepairPolicy.Measure(After, () => AfterCounts.Crossings, Cost)))
                        { Reason = "finding-or-fault"; continue; }
                        if (After.Overlaps > Before.Overlaps || After.HintsHonoured < Before.HintsHonoured || Cost >= Before.Cost)
                        { Reason = "score-or-hint"; continue; }
                        // Diagnostics and guides are projections only. No routing
                        // or placement is allowed after the acceptance decision.
                        PublishFinal(State);
                        Keep = true; Reason = "accepted"; Accepted++;
                        RouteRepairOptionObserver?.Invoke("final-complete-repair", Option.Key);
                        Before = After; Findings = AfterFindings; BeforeCounts = AfterCounts;
                        BeforeLength = RouteLength(Layout); Original = new CellGeometrySnapshot(State);
                    }
                    finally
                    {
                        if (!Keep) Original.Restore(State);
                        FinalRepairObserver?.Invoke(Option.Key, Reason, ExtraLength);
                        FinalRepairStateObserver?.Invoke(Keep ? "accepted" : "rejected", Layout, State.Placed);
                    }
                }
            }
            finally { foreach (var Queue in Queues) Queue.Dispose(); }
            FlowchartRenderContext.ReportOperationCount("final-repair/accepted", Accepted);
            FlowchartRenderContext.ReportOperationCount("final-repair/candidates", Examined);

            // Reuse the original joint track, slot and side factories. A final
            // pass has its own complete-layout budget, never a relaxed guard in
            // any of the earlier passes.
            IEnumerable<FinalOption> PairOptions()
            {
                if (FinalRepairRegionForTests != null) yield break;
                var ById = Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId, StringComparer.Ordinal);
                foreach (var Finding in Findings.Where(Finding => Finding.Rule == "5.9").ToArray())
                {
                    if (CrossingPairDiagnosticFilter?.Invoke(Finding.Subject) == false) continue;
                    var Ids = Finding.Subject.Split(" x ", StringSplitOptions.TrimEntries);
                    if (Ids.Length != 2 || !ById.TryGetValue(Ids[0], out var First) || !ById.TryGetValue(Ids[1], out var Second)) continue;
                    foreach (var Option in CrossingPairOptions(State, Bounds, () => Original, First, Second))
                        yield return new(Finding.Subject + "|" + Option.Kind, Option.Build, true);
                }
            }

            // Region candidates retain atomic ownership, original hints and gaps.
            // Frame fitting, ports and all routes are completed by the same
            // reference path used for confirmation, never by a pixel adapter.
            IEnumerable<FinalOption> RegionOptions()
            {
                if (FinalRepairRegionForTests != null)
                {
                    var Plan = FlowchartCellRepair.PlanAssignment(State.Placed, FinalRepairRegionForTests(State.Placed));
                    if (Plan != null) yield return new("diagnostic-region", () =>
                    {
                        if (!Plan.TryApply()) return false;
                        State.Frames.Clear(); BuildFramesYUp(State.Contract, State.Placed, State.Frames);
                        WriteBack(Layout, State.Placed, State.Frames); return true;
                    }, false);
                    yield break;
                }
                foreach (var Option in MoveCellOptions(State).Concat(GapCellOptions(State)))
                    yield return new(Option.Key, () =>
                    {
                        if (Option.Assignment.Nodes.Any(Node => Node.PlacementHintLocked
                            || HasHintedContainingGroup(State.Contract, Node.Id))) return false;
                        if (HasExternalCellHintDependent(State.Contract, Option.Subject, Option.Assignment.Nodes.ToArray())) return false;
                        if (!Option.Assignment.TryApply()) return false;
                        State.Frames.Clear(); BuildFramesYUp(State.Contract, State.Placed, State.Frames);
                        WriteBack(Layout, State.Placed, State.Frames);
                        return true;
                    }, false);
            }
        }

        /// <summary>Measures all finished lines, including crossings remote from the candidate's dependency closure.</summary>
        /// <param name="State">Completed candidate or incumbent.</param>
        /// <returns>Complete bends, geometric crossings, shared-pair count and exact same-fan crossing identities.</returns>
        private static (int Bends, int Crossings, int Shared, int FanCrossings, HashSet<string> FanPairs) FinalCounts(GridLayoutState State)
        {
            int Crossings = 0, Shared = 0, FanCrossings = 0;
            var FanPairs = new HashSet<string>(StringComparer.Ordinal);
            var Routes = State.Copy.EdgeRoutes;
            for (int A = 0; A < Routes.Count; A++) for (int B = A + 1; B < Routes.Count; B++)
            {
                var First = Routes[A]; var Second = Routes[B];
                var Hits = FlowchartBranchRouteOrder.Intersections(First.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Second.Points.Select(Point => (Point.X, Point.Y)).ToArray());
                Crossings += Hits.Crossings; if (Hits.Overlap > 0) Shared++;
                if (Hits.Crossings > 0 && (First.SourceId == Second.SourceId
                    || First.TargetId == Second.TargetId))
                { FanPairs.Add(First.EdgeId + " x " + Second.EdgeId); FanCrossings += Hits.Crossings; }
            }
            return (RouteBends(State.Copy), Crossings, Shared, FanCrossings, FanPairs);
        }

        /// <summary>Refreshes only diagnostics and drawn guides after an accepted final repair.</summary>
        /// <param name="State">Accepted complete geometry; this method never changes a route or cell.</param>
        private static void PublishFinal(GridLayoutState State)
        {
            State.Copy.ResolvedEdgeGuides = State.Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).Select(Edge =>
            {
                if (!State.Copy.TryCopyDrawnRoutePoints(Edge, out var Points)) return null;
                return new FlowchartResolvedGuide(Edge, Points, Points.Count == 2 ? FlowchartEdgeRouteKind.Straight : FlowchartEdgeRouteKind.Grid);
            }).OfType<FlowchartResolvedGuide>().ToList();
            State.Copy.RenderContext.CachedRouteContract = null;
            State.Copy.RenderContext.CachedRouteGeometry = null;
            State.Copy.RenderContext.CachedRouteGuides = null;
            State.Copy.RenderContext.LayoutStageViolations.RemoveAll(Failure => Failure.Kind is "RouteLegality" or "SharedTrack");
            LegalityValidationStage.Apply(State);
        }
    }
}
