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
            var Layout = State.Copy;
            var ById = Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId, StringComparer.Ordinal);
            var Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
            var Pairs = Findings.Where(Finding => Finding.Rule == "5.9")
                .Where(Finding => CrossingPairDiagnosticFilter?.Invoke(Finding.Subject) != false)
                .Select(Finding => (Key: Finding.Subject, Ids: Finding.Subject.Split(" x ", StringSplitOptions.TrimEntries)))
                .Where(Pair => Pair.Ids.Length == 2 && ById.ContainsKey(Pair.Ids[0]) && ById.ContainsKey(Pair.Ids[1]))
                .Select(Pair => (Pair.Key, First: ById[Pair.Ids[0]], Second: ById[Pair.Ids[1]]))
                .OrderBy(Pair => Math.Min(Pair.First.Points.Count, Pair.Second.Points.Count)).ToArray();
            if (Pairs.Length == 0) return;
            var Original = new CellGeometrySnapshot(State);
            var Paths = Layout.EdgeRoutes.ToDictionary(Route => Route, FlowchartStarFanNesting.Clone);
            var Before = FlowchartLayoutScore.Measure(State.Contract, Layout, false);
            var Incremental = new FlowchartLayoutScore.CellMovementContext(State.Contract, Layout);
            int BeforeBends = RouteBends(Layout), BeforeCrossings = TerminalCrossings(Layout);
            double BeforeLength = RouteLength(Layout);
            int Examined = 0;
            var Queues = Pairs.Select(Pair => (Pair.Key, Pair.First, Pair.Second,
                Options: Options(Pair.First, Pair.Second).GetEnumerator())).ToList();
            try
            {
                // Round-robin complete options rather than exhausting every
                // track permutation on the first crossing in declaration order.
                while (Queues.Count > 0 && Examined < FlowchartLayoutRules.RepairCandidateMoves)
                for (int Index = 0; Index < Queues.Count && Examined < FlowchartLayoutRules.RepairCandidateMoves; Index++)
                {
                    var Pair = Queues[Index];
                    if (!FlowchartRouteRuleChecker.HasProperCrossing(Pair.First, Pair.Second) || !Pair.Options.MoveNext())
                    { Pair.Options.Dispose(); Queues.RemoveAt(Index--); continue; }
                    var Option = Pair.Options.Current;
                    Examined++;
                    bool Keep = false;
                    CrossingPairStateObserver?.Invoke("before", Layout, State.Placed);
                    string Reason = "geometry";
                    try
                    {
                        if (!Option.Build()) continue;
                        var Affected = Layout.EdgeRoutes.Where(Route => !Equal(Route, Paths[Route])).ToHashSet();
                        if (Affected.Count == 0) { Reason = "unchanged"; continue; }
                        if (Affected.Count > FlowchartLayoutRules.RepairChainLines) { Reason = "12-line-limit"; continue; }
                        if (FlowchartRouteRuleChecker.HasProperCrossing(Pair.First, Pair.Second)) { Reason = "pair-still-crosses"; continue; }
                        int Bends = RouteBends(Layout), Crossings = TerminalCrossings(Layout);
                        if (Bends > BeforeBends) { Reason = $"bends:{BeforeBends}->{Bends}"; continue; }
                        if (Crossings > BeforeCrossings) { Reason = $"crossings:{BeforeCrossings}->{Crossings}"; continue; }
                        double ExtraLength = RouteLength(Layout) - BeforeLength;
                        if (ExtraLength > FlowchartLayoutRules.RepairExtraLength)
                        { Reason = $"length:{ExtraLength}>{FlowchartLayoutRules.RepairExtraLength}"; continue; }
                        if (Affected.Any(Route => !FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route,
                            Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()))) { Reason = "body-title-or-terminal"; continue; }
                        if (Affected.Any(Route => !FlowchartPortSideRouteRepair.HasLabelRoom(Route.Points,
                            FindContractEdge(State.Contract, Route), State.Contract, OrthogonalRunsOnly: true)))
                        { Reason = "label-clearance"; continue; }
                        var AfterFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
                        if (!NoNewFindings(Findings, AfterFindings))
                        {
                            Reason = "new-finding:" + string.Join(";", AfterFindings.Where(Finding =>
                                !Findings.Any(Old => Old.Rule == Finding.Rule && Old.Subject == Finding.Subject))
                                .Select(Finding => Finding.Rule + ":" + Finding.Subject));
                            continue;
                        }
                        var Score = Incremental.Measure(Affected);
                        if (Score.Overlaps > Before.Overlaps || Score.HintsHonoured < Before.HintsHonoured)
                        { Reason = "overlap-or-hint"; continue; }
                        var RepairCost = Score.Cost + ExtraLength / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
                            + Affected.Count * FlowchartLayoutRules.RepairMovedLineWeight;
                        if (RepairCost >= Before.Cost) { Reason = "score"; continue; }
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
                        if (!Keep) Original.Restore(State);
                        CrossingPairCandidateObserver?.Invoke(Pair.Key, Option.Kind, Reason);
                        CrossingPairStateObserver?.Invoke(Keep ? "kept" : "rejected", Layout, State.Placed);
                    }
                }
                foreach (var Pair in Pairs.Where(Pair => FlowchartRouteRuleChecker.HasProperCrossing(Pair.First, Pair.Second)))
                    CrossingPairCandidateObserver?.Invoke(Pair.Key, "remaining", Examined >= FlowchartLayoutRules.RepairCandidateMoves
                        ? "16-candidate-limit" : "no-improving-generated-assignment");
            }
            finally { foreach (var Queue in Queues) Queue.Options.Dispose(); }

            // <summary>Interleaves track, slot and side options within the same named complete-candidate budget.</summary>
            // <param name="First">First declared link.</param>
            // <param name="Second">Second declared link.</param>
            // <returns>Lazy joint options; none changes live state until evaluated.</returns>
            IEnumerable<(string Kind, Func<bool> Build)> Options(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
            {
                var Families = new[] { Tracks(First, Second).GetEnumerator(), Slots(First, Second).GetEnumerator(),
                    Sides(First, Second).GetEnumerator() }.ToList();
                try
                {
                    while (Families.Count > 0)
                    for (int Index = 0; Index < Families.Count; Index++)
                        if (Families[Index].MoveNext()) yield return Families[Index].Current;
                        else { Families[Index].Dispose(); Families.RemoveAt(Index--); }
                }
                finally { foreach (var Family in Families) Family.Dispose(); }
            }

            // <summary>Translates an internal track on one or both links, preserving both allocated terminals.</summary>
            // <param name="First">First link in the complete assignment.</param>
            // <param name="Second">Second link in the complete assignment.</param>
            // <returns>Whole-grid internal track alternatives.</returns>
            IEnumerable<(string Kind, Func<bool> Build)> Tracks(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
            {
                foreach (var Route in new[] { First, Second })
                foreach (int Index in Runs(Route, Route == First ? Second : First))
                {
                    int Run = Index;
                    var Other = Route == First ? Second : First;
                    bool Vertical = Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.001;
                    double Current = Vertical ? Route.Points[Run].X : Route.Points[Run].Y;
                    double Low = Other.Points.Min(Point => Vertical ? Point.X : Point.Y);
                    double High = Other.Points.Max(Point => Vertical ? Point.X : Point.Y);
                    double Step = DiagramGridMetrics.GRID_UNIT_PX;
                    foreach (double Offset in new[] { -Step, Step,
                        Math.Floor(Low / Step) * Step - Step - Current,
                        Math.Ceiling(High / Step) * Step + Step - Current }.Distinct().OrderBy(Math.Abs))
                    {
                    yield return ("track", () => Run + 1 < Route.Points.Count - 1
                        && TryTranslateInternalRun(Route, Run, Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.001,
                            Offset, State.Placed));
                    yield return ("joint-track", () => Run + 1 < Route.Points.Count - 1 && Other.Points.Count >= 4
                        && TryTranslateInternalRun(Route, Run, Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.001,
                            Offset, State.Placed)
                        && TranslateOther());

                    bool TranslateOther()
                    {
                        int OtherRun = Runs(Other, Route).FirstOrDefault();
                        return OtherRun > 0 && TryTranslateInternalRun(Other, OtherRun,
                            Math.Abs(Other.Points[OtherRun].X - Other.Points[OtherRun + 1].X) < 0.001,
                            -Offset, State.Placed);
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
                    FlowchartBranchRouteOrder.Intersections(new[] { (Route.Points[Index].X, Route.Points[Index].Y),
                        (Route.Points[Index + 1].X, Route.Points[Index + 1].Y) },
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
                        : First.TargetId != Second.TargetId || First.EntrySide != Second.EntrySide)) continue;
                    yield return ("slots", () =>
                    {
                        if (new[] { First, Second }.Any(Route => FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive == true)) return false;
                        int A = Source ? 0 : First.Points.Count - 1, B = Source ? 0 : Second.Points.Count - 1;
                        (First.Points[A], Second.Points[B]) = (Second.Points[B], First.Points[A]);
                        Rebuild(new HashSet<FlowchartLayoutEdgeRoute> { First, Second }); return true;
                    });
                }
            }

            // <summary>Reallocates the complete endpoint inventory before rerouting every slot-dependent line.</summary>
            // <param name="First">First crossing link.</param>
            // <param name="Second">Second crossing link.</param>
            // <returns>Alternative side assignments with the ordinary twelve-line closure limit.</returns>
            IEnumerable<(string Kind, Func<bool> Build)> Sides(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
            {
                foreach (string Side in new[] { "left", "right", "top", "bottom" })
                foreach (var Route in new[] { First, Second })
                foreach (bool Source in new[] { true, false })
                {
                    if (FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive == true
                        || Side == (Source ? Route.ExitSide : Route.EntrySide)) continue;
                    yield return ("side", () =>
                    {
                        if (Source) Route.ExitSide = Side; else Route.EntrySide = Side;
                        string Owner = Source ? Route.SourceId : Route.TargetId;
                        ModelRouteCompletionStage.AllocateFinalPortSlots(State.Contract, Layout, Bounds, State.Nodes,
                            new HashSet<string>(StringComparer.Ordinal) { Owner });
                        var Affected = Original.ChangedTerminalRoutes().ToHashSet(); Affected.Add(Route);
                        if (Affected.Count > FlowchartLayoutRules.RepairChainLines)
                        {
                            CrossingPairCandidateObserver?.Invoke(First.EdgeId + " x " + Second.EdgeId,
                                "side-closure", "12-line-limit:" + Affected.Count);
                            return false;
                        }
                        Rebuild(Affected); return true;
                    });
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
