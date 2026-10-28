using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    /// <summary>Restricts an explicit diagnostic replay to a requested pair; production leaves this unset.</summary>
    internal static Func<string, bool>? CrossingPairDiagnosticFilter;
    /// <summary>Reports bounded complete-candidate outcomes without changing acceptance.</summary>
    internal static Action<string, string, string>? CrossingPairCandidateObserver;
    /// <summary>Exposes before/rejected state to exact rollback regression tests.</summary>
    internal static Action<string, FlowchartLayoutResult, IReadOnlyList<GridNode>>? CrossingPairStateObserver;

    private static partial class RouteRepairStage
    {
        /// <summary>Tests joint track, slot and side assignments with one complete score and exact rollback.</summary>
        /// <param name="State">Settled cell-owned production geometry.</param>
        /// <param name="Bounds">Unchanged authoritative body and frame bounds.</param>
        internal static void RepairCrossingPairAssignments(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
        {
            FlowchartLayoutResult Layout = State.Copy;
            Dictionary<string, FlowchartLayoutEdgeRoute> ById = Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId, StringComparer.Ordinal);
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
            (string Key, FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)[] Pairs = [.. Findings.Where(Finding => Finding.Rule == "5.9")
                .Where(Finding => CrossingPairDiagnosticFilter?.Invoke(Finding.Subject) != false)
                .Select(Finding => (Key: Finding.Subject, Ids: Finding.Subject.Split(" x ", StringSplitOptions.TrimEntries)))
                .Where(Pair => Pair.Ids.Length == 2 && ById.ContainsKey(Pair.Ids[0]) && ById.ContainsKey(Pair.Ids[1]))
                .Select(Pair => (Pair.Key, First: ById[Pair.Ids[0]], Second: ById[Pair.Ids[1]]))
                .OrderBy(Pair => Math.Min(Pair.First.Points.Count, Pair.Second.Points.Count))];
            if (Pairs.Length == 0)
            {
                return;
            }

            CellGeometrySnapshot Original = new CellGeometrySnapshot(State);
            Dictionary<FlowchartLayoutEdgeRoute, FlowchartLayoutEdgeRoute> Paths = Layout.EdgeRoutes.ToDictionary(Route => Route, FlowchartStarFanNesting.Clone);
            FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(State.Contract, Layout, false);
            FlowchartLayoutScore.CellMovementContext Incremental = new FlowchartLayoutScore.CellMovementContext(State.Contract, Layout);
            int BeforeBends = RouteBends(Layout), BeforeCrossings = TerminalCrossings(Layout);
            double BeforeLength = RouteLength(Layout);
            int Examined = 0;
            List<(string Key, FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second, IEnumerator<(string Kind, Func<bool> Build)> Options)> Queues = [.. Pairs.Select(Pair => (Pair.Key, Pair.First, Pair.Second,
                Options: CrossingPairOptions(State, Bounds, () => Original, Pair.First, Pair.Second).GetEnumerator()))];
            try
            {
                // Round-robin complete options rather than exhausting every
                // track permutation on the first crossing in declaration order.
                while (Queues.Count > 0 && Examined < FlowchartLayoutRules.RepairCandidateMoves)
                {
                    for (int Index = 0; Index < Queues.Count && Examined < FlowchartLayoutRules.RepairCandidateMoves; Index++)
                {
                        (string Key, FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second, IEnumerator<(string Kind, Func<bool> Build)> Options) Pair = Queues[Index];
                    if (!FlowchartRouteRuleChecker.HasProperCrossing(Pair.First, Pair.Second) || !Pair.Options.MoveNext())
                    { Pair.Options.Dispose(); Queues.RemoveAt(Index--); continue; }
                        (string Kind, Func<bool> Build) Option = Pair.Options.Current;
                    Examined++;
                    bool Keep = false;
                    CrossingPairStateObserver?.Invoke("before", Layout, State.Placed);
                    string Reason = "geometry";
                    try
                    {
                        if (!Option.Build())
                            {
                                continue;
                            }

                            HashSet<FlowchartLayoutEdgeRoute> Affected = [.. Layout.EdgeRoutes.Where(Route => !Equal(Route, Paths[Route]))];
                        if (Affected.Count == 0) { Reason = "unchanged"; continue; }
                        if (Affected.Count > FlowchartLayoutRules.RepairChainLines) { Reason = FlowchartLayoutRules.RepairChainLines + "-line-limit"; continue; }
                        if (FlowchartRouteRuleChecker.HasProperCrossing(Pair.First, Pair.Second)) { Reason = "pair-still-crosses"; continue; }
                        int Bends = RouteBends(Layout), Crossings = TerminalCrossings(Layout);
                        double ExtraLength = RouteLength(Layout) - BeforeLength;
                        if (Affected.Any(Route => !FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route,
                            Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()))) { Reason = "body-title-or-terminal"; continue; }
                        if (Affected.Any(Route => !FlowchartPortSideRouteRepair.HasLabelRoom(Route.Points,
                            FindContractEdge(State.Contract, Route), State.Contract, OrthogonalRunsOnly: true)))
                        { Reason = "label-clearance"; continue; }
                            IReadOnlyList<FlowchartRouteRuleChecker.Finding> AfterFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
                        if (!NoNewFindings(Findings, AfterFindings))
                        {
                            Reason = "new-finding:" + string.Join(";", AfterFindings.Where(Finding =>
                                !Findings.Any(Old => Old.Rule == Finding.Rule && Old.Subject == Finding.Subject))
                                .Select(Finding => Finding.Rule + ":" + Finding.Subject));
                            continue;
                        }
                            FlowchartLayoutScore.Result Score = Incremental.Measure(Affected);
                        if (Score.Overlaps > Before.Overlaps || Score.HintsHonoured < Before.HintsHonoured)
                        { Reason = "overlap-or-hint"; continue; }
                        int BeforeFaults = FlowchartRepairCandidateComparison.RoutingFaultCount(Before.Violations);
                        int AfterFaults = FlowchartRepairCandidateComparison.RoutingFaultCount(Score.Violations);
                        if (AfterFaults > BeforeFaults)
                        { Reason = "routing-faults"; continue; }
                        if (AfterFaults == BeforeFaults && (Bends > BeforeBends || Crossings > BeforeCrossings))
                        { Reason = $"quality:{BeforeBends}->{Bends}/{BeforeCrossings}->{Crossings}"; continue; }
                            FlowchartLayoutScore.ComparisonCost RepairCost = Score.Cost + (ExtraLength / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight)
                            + (Affected.Count * FlowchartLayoutRules.RepairMovedLineWeight);
                        if (!FlowchartRepairCandidateComparison.CandidateBeatsBaseline(
                                BeforeFaults, Before.HintsHonoured, Before.Cost, AfterFaults, Score.HintsHonoured, RepairCost))
                        { Reason = "score"; continue; }
                        Keep = true; Reason = "kept";
                        RouteRepairOptionObserver?.Invoke("joint-crossing-pair", Pair.Key);
                        Findings = AfterFindings; Before = Score; BeforeBends = Bends; BeforeCrossings = Crossings;
                        BeforeLength = RouteLength(Layout);
                        Original = new CellGeometrySnapshot(State);
                        Paths = Layout.EdgeRoutes.ToDictionary(Route => Route, FlowchartStarFanNesting.Clone);
                        Incremental = new FlowchartLayoutScore.CellMovementContext(State.Contract, Layout);
                    }
                    finally
                    {
                        if (!Keep)
                            {
                                Original.Restore(State);
                            }

                            CrossingPairCandidateObserver?.Invoke(Pair.Key, Option.Kind, Reason);
                        CrossingPairStateObserver?.Invoke(Keep ? "kept" : "rejected", Layout, State.Placed);
                    }
                }
                }

                foreach ((string Key, FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second) Pair in Pairs.Where(Pair => FlowchartRouteRuleChecker.HasProperCrossing(Pair.First, Pair.Second)))
                {
                    CrossingPairCandidateObserver?.Invoke(Pair.Key, "remaining", Examined >= FlowchartLayoutRules.RepairCandidateMoves
                        ? FlowchartLayoutRules.RepairCandidateMoves + "-candidate-limit" : "no-improving-generated-assignment");
                }
            }
            finally { foreach ((string Key, FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second, IEnumerator<(string Kind, Func<bool> Build)> Options) Queue in Queues)
                {
                    Queue.Options.Dispose();
                }
            }

        }

        /// <summary>Shares the existing joint track, slot and side candidate factories with the final repair pass.</summary>
        /// <param name="State">Live candidate layout.</param>
        /// <param name="Bounds">Current obstacles.</param>
        /// <param name="Snapshot">Incumbent rollback state, refreshed after acceptance.</param>
        /// <param name="First">First crossing line.</param>
        /// <param name="Second">Second crossing line.</param>
        /// <returns>Unapplied route options; the caller owns scoring, limits and rollback.</returns>
        private static IEnumerable<(string Kind, Func<bool> Build)> CrossingPairOptions(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds, Func<CellGeometrySnapshot> Snapshot,
            FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
        {
            FlowchartLayoutResult Layout = State.Copy;
            return Options(First, Second);
            // <summary>Interleaves track, slot and side options within the same named complete-candidate budget.</summary>
            // <param name="First">First declared link.</param>
            // <param name="Second">Second declared link.</param>
            // <returns>Lazy joint options; none changes live state until evaluated.</returns>
            IEnumerable<(string Kind, Func<bool> Build)> Options(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
            {
                List<IEnumerator<(string Kind, Func<bool> Build)>> Families = [ Tracks(First, Second).GetEnumerator(), Slots(First, Second).GetEnumerator(),
                    Sides(First, Second).GetEnumerator() ];
                try
                {
                    while (Families.Count > 0)
                    {
                        for (int Index = 0; Index < Families.Count; Index++)
                        {
                            if (Families[Index].MoveNext())
                            {
                                yield return Families[Index].Current;
                            }
                            else { Families[Index].Dispose(); Families.RemoveAt(Index--); }
                        }
                    }
                }
                finally { foreach (IEnumerator<(string Kind, Func<bool> Build)>? Family in Families)
                    {
                        Family.Dispose();
                    }
                }
            }

            // <summary>Translates an internal track on one or both links, preserving both allocated terminals.</summary>
            // <param name="First">First link in the complete assignment.</param>
            // <param name="Second">Second link in the complete assignment.</param>
            // <returns>Whole-grid internal track alternatives.</returns>
            IEnumerable<(string Kind, Func<bool> Build)> Tracks(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
            {
                foreach (FlowchartLayoutEdgeRoute? Route in new[] { First, Second })
                {
                    foreach (int Index in Runs(Route, Route == First ? Second : First))
                {
                    int Run = Index;
                    if (Run < 0 || Run + 1 >= Route.Points.Count)
                        {
                            continue;
                        }

                        FlowchartLayoutEdgeRoute Other = Route == First ? Second : First;
                    bool Vertical = Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.001;
                    double Current = Vertical ? Route.Points[Run].X : Route.Points[Run].Y;
                    double Low = Other.Points.Min(Point => Vertical ? Point.X : Point.Y);
                    double High = Other.Points.Max(Point => Vertical ? Point.X : Point.Y);
                    double Step = DiagramGridMetrics.GRID_UNIT_PX;
                    foreach (double Offset in new[] { -Step, Step,
                        (Math.Floor(Low / Step) * Step) - Step - Current,
                        (Math.Ceiling(High / Step) * Step) + Step - Current }.Distinct().OrderBy(Math.Abs))
                    {
                    yield return ("track", () => Run + 1 < Route.Points.Count - 1
                        && TryTranslateInternalRun(Route, Run, Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.001,
                            Offset, State.Placed, State.Copy));
                    yield return ("joint-track", () => Run + 1 < Route.Points.Count - 1 && Other.Points.Count >= 4
                        && TryTranslateInternalRun(Route, Run, Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.001,
                            Offset, State.Placed, State.Copy)
                        && TranslateOther());

                    bool TranslateOther()
                    {
                        int OtherRun = Runs(Other, Route).FirstOrDefault();
                        return OtherRun > 0 && TryTranslateInternalRun(Other, OtherRun,
                            Math.Abs(Other.Points[OtherRun].X - Other.Points[OtherRun + 1].X) < 0.001,
                            -Offset, State.Placed, State.Copy);
                    }
                    }
                }
                }
            }

            // <summary>Offers the track actually intersecting its partner before unrelated internal segments.</summary>
            // <param name="Route">Route whose grid tracks are being reconsidered.</param>
            // <param name="Other">Crossing partner.</param>
            // <returns>Internal run indices ordered by observed intersections, then stable source order.</returns>
            IEnumerable<int> Runs(FlowchartLayoutEdgeRoute Route, FlowchartLayoutEdgeRoute Other) =>
                Enumerable.Range(1, Math.Max(0, Route.Points.Count - 3)).OrderByDescending(Index =>
                    FlowchartBranchRouteOrder.Intersections([ (Route.Points[Index].X, Route.Points[Index].Y),
                        (Route.Points[Index + 1].X, Route.Points[Index + 1].Y) ],
                        Other.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings);

            // <summary>Exchanges two already allocated slots only when both ends belong to the same node side.</summary>
            // <param name="First">First declared link.</param>
            // <param name="Second">Second declared link.</param>
            // <returns>Complete two-link slot exchanges checked by the unchanged ordering guard.</returns>
            IEnumerable<(string Kind, Func<bool> Build)> Slots(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
            {
                foreach (bool Source in new[] { true, false })
                {
                    if ((Source ? First.SourceId != Second.SourceId || First.ExitSide != Second.ExitSide
                        : First.TargetId != Second.TargetId || First.EntrySide != Second.EntrySide))
                    {
                        continue;
                    }

                    yield return ("slots", () =>
                    {
                        if (new[] { First, Second }.Any(Route => FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive == true))
                        {
                            return false;
                        }

                        int A = Source ? 0 : First.Points.Count - 1, B = Source ? 0 : Second.Points.Count - 1;
                        (First.Points[A], Second.Points[B]) = (Second.Points[B], First.Points[A]);
                        Rebuild([First, Second]); return true;
                    });
                }
            }

            // <summary>Reallocates the complete endpoint inventory before rerouting every slot-dependent line.</summary>
            // <param name="First">First crossing link.</param>
            // <param name="Second">Second crossing link.</param>
            // <returns>Alternative side assignments with the configured closure limit.</returns>
            IEnumerable<(string Kind, Func<bool> Build)> Sides(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
            {
                foreach (string Side in new[] { "left", "right", "top", "bottom" })
                {
                    foreach (FlowchartLayoutEdgeRoute? Route in new[] { First, Second })
                    {
                        foreach (bool Source in new[] { true, false })
                {
                    if (FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive == true
                        || Side == (Source ? Route.ExitSide : Route.EntrySide))
                            {
                                continue;
                            }

                            yield return ("side", () =>
                    {
                        if (Source)
                        {
                            Route.ExitSide = Side;
                        }
                        else
                        {
                            Route.EntrySide = Side;
                        }

                        string Owner = Source ? Route.SourceId : Route.TargetId;
                        ModelRouteCompletionStage.AllocateFinalPortSlots(State.Contract, Layout, Bounds, State.Nodes,
                            new HashSet<string>(StringComparer.Ordinal) { Owner });
                        HashSet<FlowchartLayoutEdgeRoute> Affected = [.. Snapshot().ChangedTerminalRoutes()]; Affected.Add(Route);
                        if (Affected.Count > FlowchartLayoutRules.RepairChainLines)
                        {
                            CrossingPairCandidateObserver?.Invoke(First.EdgeId + " x " + Second.EdgeId,
                                "side-closure", FlowchartLayoutRules.RepairChainLines + "-line-limit:" + Affected.Count);
                            return false;
                        }
                        Rebuild(Affected); return true;
                    });
                }
                    }
                }
            }

            // <summary>Completes only the links changed by the joint port assignment.</summary>
            // <param name="Affected">Dependency closure admitted by the named chain-line limit.</param>
            void Rebuild(HashSet<FlowchartLayoutEdgeRoute> Affected) => DiagramGridRouteBuilder.RebuildAllRoutes(
                Layout, State.Placed, State.Nodes, State.Contract, State.Frames, State.Direction, Affected);
        }

        /// <summary>Compares exact route values so unchanged lines retain their cached costs and geometry.</summary>
        /// <param name="First">Current route.</param>
        /// <param name="Second">Independent baseline clone.</param>
        /// <returns>Whether ports, normals, sides and every drawn point are identical.</returns>
        private static bool Equal(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second) =>
            First.ExitSide == Second.ExitSide && First.EntrySide == Second.EntrySide
            && First.ExitNormalX == Second.ExitNormalX && First.ExitNormalY == Second.ExitNormalY
            && First.EntryNormalX == Second.EntryNormalX && First.EntryNormalY == Second.EntryNormalY
            && First.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(Second.Points.Select(Point => (Point.X, Point.Y)));
    }
}
