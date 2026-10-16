using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Tries legal terminal sides and slots before accepting a bent ordinary relationship.</summary>
internal static class FlowchartPortSideRouteRepair
{
    internal static Action<string, int, int, int, int, int>? RouteCandidateObserver { get; set; }
    private sealed record Candidate(List<FlowchartLayoutPoint> Points, string Exit, string Entry,
        int FlowPriority, double Length, bool ReallocatesPorts = false);
    private sealed record Snapshot(FlowchartLayoutEdgeRoute Route, List<FlowchartLayoutPoint> Points,
        string Exit, string Entry, double ExitX, double ExitY, double EntryX, double EntryY);

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        HashSet<string>? AttemptedRouteStates = null, bool IncludeTerminalCandidates = false)
    {
        if (Layout.NodeBounds.Count == 0 || Layout.EdgeRoutes.Count == 0) return;
        AttemptedRouteStates ??= new(StringComparer.Ordinal);
        FlowchartRouteSpatialIndex Spatial = new(Layout);
        if (!Layout.EdgeRoutes.Any(Route => Corners(Route.Points) > 0
                && !AttemptedRouteStates.Contains(RouteStateKey(Route) + "|"
                    + Spatial.DependencySignature(Route.Points)))) return;
        FlowchartLayoutScore.Result Initial = FlowchartLayoutScore.Measure(Contract, Layout);
        List<Snapshot> InitialRoutes = Layout.EdgeRoutes.Select(Route => new Snapshot(Route,
            Route.Points.Select(Copy).ToList(), Route.ExitSide, Route.EntrySide,
            Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY)).ToList();
        var Bounds = Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract)
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var TerminalObstacles = IncludeTerminalCandidates
            ? DiagramGridRouteBuilder.PrepareTerminalObstacles(Contract, Bounds) : null;
        Dictionary<string, List<FlowchartLayoutEdgeRoute>> RoutesByEndpoint = Layout.EdgeRoutes
            .SelectMany(Route => new[] { (Route.SourceId, Route), (Route.TargetId, Route) })
            .GroupBy(Item => Item.Item1, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.Select(Item => Item.Route).Distinct().ToList(), StringComparer.Ordinal);
        Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]> RoutePointCache = Layout.EdgeRoutes
            .ToDictionary(Route => Route, Route => Tuples(Route.Points).ToArray());
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        static string FacingSide(FlowchartLayoutNodeBounds From, FlowchartLayoutNodeBounds To)
        {
            double Dx = To.CenterX - From.CenterX, Dy = To.CenterY - From.CenterY;
            return Math.Abs(Dx) >= Math.Abs(Dy) ? Dx >= 0 ? "right" : "left"
                : Dy >= 0 ? "top" : "bottom";
        }
        bool HasAllocatedFacingSides(FlowchartLayoutEdgeRoute Route) =>
            Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
            && Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)
            && string.Equals(Route.ExitSide, FacingSide(Source, Target), StringComparison.Ordinal)
            && string.Equals(Route.EntrySide, FacingSide(Target, Source), StringComparison.Ordinal);
        var ScoreContext = new FlowchartLayoutScore.IncrementalContext(
            Contract, Layout, IncludeArrangementCheck: false);
        Queue<FlowchartLayoutEdgeRoute> Pending = new();
        HashSet<FlowchartLayoutEdgeRoute> Queued = new();
        bool HasLineInteraction(FlowchartLayoutEdgeRoute Route) => Spatial.QueryRoutes(Route.Points, Route).Any(Other =>
        {
            var Interaction = FlowchartBranchRouteOrder.Intersections(
                Tuples(Route.Points), RoutePointCache[Other]);
            return Interaction.Crossings > 0 || Interaction.Overlap > 0.01;
        });
        bool Eligible(FlowchartLayoutEdgeRoute Route) => (Corners(Route.Points) > 0 || HasLineInteraction(Route))
            && CanRerouteUnderHint(Route, Contract)
            // Rule 4.7 sides are assigned jointly from the placed endpoint
            // geometry before route construction. Do not repeat the side
            // search for an already-facing, non-interacting route. Keep the
            // repair available when another line interacts with it, because
            // crossing removal can justify a different legal side assignment.
            && (!HasAllocatedFacingSides(Route) || HasLineInteraction(Route)
                || IncludeTerminalCandidates)
            && !Back.Contains((Route.SourceId, Route.TargetId))
            && Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
            && Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)
            && !IsGeometricBackEdge(Source, Target, Direction)
            && (CanReassign(Route.SourceId, Nodes) && CanReassign(Route.TargetId, Nodes)
                || IncludeTerminalCandidates && (HasStraightCandidate(Route)
                    || Nodes.TryGetValue(Route.SourceId, out var SourceNode) && FlowchartDiamondAttachHelper.IsDiamondShape(SourceNode.Shape)
                    || Nodes.TryGetValue(Route.TargetId, out var TargetNode) && FlowchartDiamondAttachHelper.IsDiamondShape(TargetNode.Shape)));
        bool HasStraightCandidate(FlowchartLayoutEdgeRoute Route) =>
            Bounds.TryGetValue(Route.SourceId, out var Source)
            && Bounds.TryGetValue(Route.TargetId, out var Target)
            && StraightCandidate(Source, Target, Nodes, Direction) != null;
        string CacheKey(FlowchartLayoutEdgeRoute Route) => RouteStateKey(Route) + "|"
            + Spatial.DependencySignature(Route.Points);
        void Enqueue(FlowchartLayoutEdgeRoute Route)
        {
            bool IsEligible = Eligible(Route);
            if (IsEligible && !AttemptedRouteStates.Contains(CacheKey(Route))
                && Queued.Add(Route)) Pending.Enqueue(Route);
        }
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes) Enqueue(Route);
        var Visits = new Dictionary<FlowchartLayoutEdgeRoute, int>();
        while (Pending.Count > 0)
        {
            FlowchartLayoutEdgeRoute Route = Pending.Dequeue();
            Queued.Remove(Route);
            int Depth = Visits.GetValueOrDefault(Route);
            if (Depth >= FlowchartLayoutRules.RepairChainDepth) continue;
            Visits[Route] = Depth + 1;
            if (!AttemptedRouteStates.Add(CacheKey(Route))) continue;
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)
                || !Eligible(Route)) continue;
            bool IsRejected = Corners(Route.Points) > 2;
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            IReadOnlyCollection<FlowchartLayoutEdgeRoute> BeforeInteractions = Spatial.QueryRoutes(Route.Points, Route);
            if (BeforeInteractions.Count > FlowchartLayoutRules.RepairChainLines) continue;
            FlowchartLayoutScore.CandidateMeasurement Before =
                ScoreContext.MeasureCandidateSummary(Route, BeforeInteractions);
            int BeforeFaults = Before.RejectingFaults, BeforeCrossings = Before.Crossings;
            List<FlowchartLayoutQualityFailure> RouteFaults = [];
            DiagramGridQuality.CollectRouteFailures(Layout, Route, RouteFaults);
            int RouteRejectingFaults = RouteFaults.Count(Failure => Failure.Kind != "ArrangementKept");
            if (!Before.Score.HasValue && RouteRejectingFaults == 0 && BeforeCrossings == 0) continue;
            List<FlowchartLayoutPoint> Original = Route.Points.Select(Copy).ToList();
            var OriginalRoute = new FlowchartLayoutEdgeRoute { EdgeId = Route.EdgeId, SourceId = Route.SourceId,
                TargetId = Route.TargetId, ExitSide = Route.ExitSide, EntrySide = Route.EntrySide,
                ExitNormalX = Route.ExitNormalX, ExitNormalY = Route.ExitNormalY,
                EntryNormalX = Route.EntryNormalX, EntryNormalY = Route.EntryNormalY };
            OriginalRoute.Points.AddRange(Original);
            int OriginalRuleCrossings = BeforeInteractions.Count(Other =>
                (Other.SourceId != Route.SourceId || Other.TargetId != Route.TargetId)
                && FlowchartRouteRuleChecker.HasProperCrossing(OriginalRoute, Other));
            var OriginalNormals = (Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY);
            var AllocationFaults = IncludeTerminalCandidates
                ? FlowchartRouteRuleChecker.CheckPortAllocation(Contract, Layout)
                    .Select(Finding => Finding.Subject).ToHashSet(StringComparer.Ordinal) : [];
            string OriginalExit = Route.ExitSide, OriginalEntry = Route.EntrySide;
            double OriginalLength = Length(Original);
            Candidate? Best = null;
            int BestFaults = BeforeFaults;
            double? BestScore = Before.Score;
            double BestOptimizationScore = Before.OptimizationScore;
            double BestLength = OriginalLength;
            List<(double X, double Y)> OriginalPath = Tuples(Route.Points);
            int OriginalCorners = Corners(Route.Points);
            int LocalCrossings = BeforeInteractions.Sum(Other =>
                FlowchartBranchRouteOrder.Intersections(OriginalPath, RoutePointCache[Other]).Crossings);
            bool OnlyBendCanImprove = LocalCrossings == 0 && RouteFaults.Count == 0;
            bool NeedsDetours = !OnlyBendCanImprove || OriginalCorners > 2;
            IReadOnlyCollection<FlowchartLayoutNodeBounds> NearbyTrackBoxes = Spatial.QueryBoxes(Route.Points);
            int CandidateCount = 0, HintMatched = 0, ClearCount = 0, FreePortCount = 0, ImprovingCount = 0;
            int ExaminedCandidates = 0;
            foreach (Candidate Candidate in Candidates(Source, Target, NearbyTrackBoxes, Direction,
                         Route.ExitSide, Route.EntrySide, Original[0], Original[^1],
                         Nodes,
                         Contract, Bounds, IncludeTerminalCandidates,
                         RequireFacing: !IsRejected, IncludeDetours: NeedsDetours, SearchRegion: Route.Points,
                         TerminalObstacles: TerminalObstacles))
            {
                if (ExaminedCandidates >= FlowchartLayoutRules.RepairCandidateMoves) break;
                ExaminedCandidates++;
                FlowchartRenderContext.ReportOperationCount("route-repair-candidates", 1);
                CandidateCount++;
                if (!MatchesRoutingHint(Candidate, Edge)) continue;
                HintMatched++;
                if (!Clear(Candidate.Points, Route, Spatial)) continue;
                ClearCount++;
                if (!PortsAreFree(Candidate, Route, RoutesByEndpoint)) continue;
                FreePortCount++;
                if (IncludeTerminalCandidates && !HasLabelRoom(Candidate.Points, Edge, Contract,
                        OrthogonalRunsOnly: true)) continue;
                if (!(IsRejected || HasMinimumTerminalRuns(Candidate.Points)
                    && HasLabelRoom(Candidate.Points, Edge, Contract, IncludeTerminalCandidates))) continue;
                if (OnlyBendCanImprove && Corners(Candidate.Points) >= OriginalCorners
                    && Candidate.Length >= OriginalLength - 0.001) continue;
                Set(Route, Candidate);
                if (Candidate.ReallocatesPorts && FlowchartRouteRuleChecker.CheckPortAllocation(Contract, Layout)
                        .Any(Finding => !AllocationFaults.Contains(Finding.Subject)))
                {
                    Restore(Route, Original, OriginalExit, OriginalEntry);
                    (Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY) = OriginalNormals;
                    continue;
                }
                HashSet<FlowchartLayoutEdgeRoute> CandidateInteractions =
                    Spatial.QueryRoutes(Candidate.Points, Route).ToHashSet();
                CandidateInteractions.UnionWith(BeforeInteractions);
                FlowchartLayoutScore.CandidateMeasurement Measured =
                    ScoreContext.MeasureCandidateSummary(Route, CandidateInteractions);
                int Faults = Measured.RejectingFaults;
                bool ScoreImproves = Measured.Score.HasValue && Before.Score.HasValue
                    && Measured.Score.Value < Before.Score.Value - 0.001;
                bool CrossingImproves = Measured.Crossings < BeforeCrossings;
                bool ScoreTiesButShorter = ScoresTie(Measured.Score, Before.Score)
                    && Candidate.Length < OriginalLength - 0.001;
                bool ImprovesBaseline = IsRejected
                    ? Measured.Overlaps == 0 && (Faults < BeforeFaults
                        || Measured.OptimizationScore < Before.OptimizationScore - 0.001)
                    : Before.Score.HasValue
                        ? ScoreImproves || ScoreTiesButShorter || CrossingImproves
                        : Measured.Score.HasValue || Faults < BeforeFaults || CrossingImproves;
                bool BetterThanBest = Best == null
                    || IsRejected && (Faults < BestFaults
                        || Faults == BestFaults && (Measured.OptimizationScore < BestOptimizationScore - 0.001
                            || Math.Abs(Measured.OptimizationScore - BestOptimizationScore) <= 0.001
                                && Candidate.Length < BestLength - 0.001))
                    || !IsRejected && Measured.Score.HasValue && (!BestScore.HasValue || IsStrictlyLower(Measured.Score, BestScore))
                    || !IsRejected && ScoresTie(Measured.Score, BestScore) && (Candidate.Length < BestLength - 0.001
                        || Math.Abs(Candidate.Length - BestLength) <= 0.001
                            && string.CompareOrdinal(CandidateKey(Candidate), CandidateKey(Best)) < 0)
                    || !Measured.Score.HasValue && !BestScore.HasValue && !IsRejected && Faults < BestFaults;
                bool IsBetter = ImprovesBaseline && BetterThanBest;
                if (IsBetter && CandidateInteractions.Count(Other =>
                        (Other.SourceId != Route.SourceId || Other.TargetId != Route.TargetId)
                        && FlowchartRouteRuleChecker.HasProperCrossing(Route, Other)) > OriginalRuleCrossings)
                {
                    // Use the audit's proper-crossing definition, including
                    // common endpoint pairs. Fewer quality faults (such as
                    // excess bends) are not a legality repair and cannot justify
                    // new crossings. Only a strict hard-legality repair can.
                    IsBetter = ReducesSharedTrack(Route, OriginalPath,
                            BeforeInteractions, CandidateInteractions, RoutePointCache)
                        || HardRouteFaults(Contract, Layout, Route)
                        < HardRouteFaults(Contract, Layout, OriginalRoute);
                }
                if (IsBetter) ImprovingCount++;
                if (IsBetter && NoWorseOverlap(Candidate.Points, OriginalPath, Route,
                    CandidateInteractions, RoutePointCache))
                {
                    Best = Candidate;
                    BestFaults = Faults;
                    BestScore = Measured.Score;
                    BestOptimizationScore = Measured.OptimizationScore;
                    BestLength = Candidate.Length;
                }
                Restore(Route, Original, OriginalExit, OriginalEntry);
                (Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY) = OriginalNormals;
            }
            RouteCandidateObserver?.Invoke(Route.EdgeId, CandidateCount, HintMatched, ClearCount,
                FreePortCount, ImprovingCount);
            if (Best != null)
            {
                HashSet<FlowchartLayoutEdgeRoute> Touched = Spatial.QueryRoutes(Original, Route).ToHashSet();
                Touched.UnionWith(Spatial.QueryRoutes(Best.Points, Route));
                Set(Route, Best);
                ScoreContext.CommitCurrent(Route, Touched);
                Spatial.ReplaceRoute(Route);
                RoutePointCache[Route] = Tuples(Route.Points).ToArray();
                foreach (FlowchartLayoutEdgeRoute Neighbour in Touched.Take(FlowchartLayoutRules.RepairChainLines)) Enqueue(Neighbour);
            }
            else
            {
                Restore(Route, Original, OriginalExit, OriginalEntry);
                (Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY) = OriginalNormals;
            }
        }
        FlowchartLayoutScore.Result Final = FlowchartLayoutScore.Measure(Contract, Layout);
        double InitialLength = InitialRoutes.Sum(Saved => Length(Saved.Points));
        double FinalLength = Layout.EdgeRoutes.Sum(Route => Length(Route.Points));
        double InitialOptimizationScore = Initial.Crossings * FlowchartLayoutRules.CrossingScore
            + Initial.Bends * FlowchartLayoutRules.BendScore;
        double FinalOptimizationScore = Final.Crossings * FlowchartLayoutRules.CrossingScore
            + Final.Bends * FlowchartLayoutRules.BendScore;
        bool Keep = Initial.Score.HasValue && Final.Score.HasValue
            ? Final.Score.Value < Initial.Score.Value - 0.001
                || ScoresTie(Final.Score, Initial.Score)
                    && Final.Crossings <= Initial.Crossings && Final.Overlaps <= Initial.Overlaps
                    && FinalLength < InitialLength - 0.001
                || Final.Crossings < Initial.Crossings && Final.Overlaps <= Initial.Overlaps
            : Final.Score.HasValue || Final.Overlaps <= Initial.Overlaps
                && FinalOptimizationScore < InitialOptimizationScore - 0.001;
        if (!Keep)
            foreach (Snapshot Saved in InitialRoutes)
            {
                Restore(Saved.Route, Saved.Points, Saved.Exit, Saved.Entry);
                (Saved.Route.ExitNormalX, Saved.Route.ExitNormalY, Saved.Route.EntryNormalX, Saved.Route.EntryNormalY)
                    = (Saved.ExitX, Saved.ExitY, Saved.EntryX, Saved.EntryY);
            }
    }

    /// <summary>Counts a candidate's hard path faults independently of unrelated routes and quality preferences.</summary>
    /// <param name="Contract">The declarations used by the unchanged audit checker.</param>
    /// <param name="Layout">The authoritative node outlines and group frames.</param>
    /// <param name="Route">The current or saved connector to validate.</param>
    /// <returns>The connector's node/title collisions, invalid terminals and diagonal body findings.</returns>
    private static int HardRouteFaults(DiagramContract Contract, FlowchartLayoutResult Layout,
        FlowchartLayoutEdgeRoute Route)
    {
        var Candidate = new FlowchartLayoutResult();
        Candidate.NodeBoundsMutable.AddRange(Layout.NodeBounds);
        Candidate.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
        Candidate.EdgeRoutesMutable.Add(Route);
        return FlowchartRouteRuleChecker.Check(Contract, Candidate).Count(Finding =>
            Finding.Subject == Route.EdgeId && Finding.Rule is "10" or "3.1" or "4.1");
    }

    /// <summary>Tests a strict shared-segment legality improvement during a coupled repair chain.</summary>
    /// <param name="Route">The only changed connector.</param>
    /// <param name="Original">Its saved geometry before this candidate.</param>
    /// <param name="Before">Routes near the saved path.</param>
    /// <param name="After">Routes near either the saved or candidate path.</param>
    /// <param name="Paths">Unchanged path snapshots used by the spatial score context.</param>
    /// <returns>True only when the candidate strictly reduces shared length; the existing admission also forbids worsening any pair.</returns>
    private static bool ReducesSharedTrack(FlowchartLayoutEdgeRoute Route,
        IReadOnlyList<(double X, double Y)> Original, IReadOnlyCollection<FlowchartLayoutEdgeRoute> Before,
        IReadOnlyCollection<FlowchartLayoutEdgeRoute> After,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]> Paths)
    {
        double BeforeOverlap = Before.Sum(Other => FlowchartBranchRouteOrder.Intersections(Original, Paths[Other]).Overlap);
        var Candidate = Tuples(Route.Points);
        double AfterOverlap = After.Sum(Other => FlowchartBranchRouteOrder.Intersections(Candidate, Paths[Other]).Overlap);
        return AfterOverlap < BeforeOverlap - 0.001;
    }

    private static bool IsStrictlyLower(double? Candidate, double? Current) => Candidate.HasValue
        ? !Current.HasValue || Candidate.Value < Current.Value - 0.001
        : false;

    private static bool ScoresTie(double? Candidate, double? Current) => Candidate.HasValue && Current.HasValue
        && Math.Abs(Candidate.Value - Current.Value) <= 0.001;

    private static bool CanRerouteUnderHint(FlowchartLayoutEdgeRoute Route, DiagramContract Contract)
    {
        RoutingHintContract? Hint = DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint;
        return Hint?.IsActive != true || Hint.Travel == null && (Hint.Leave.HasValue || Hint.Enter.HasValue);
    }

    private static bool MatchesRoutingHint(Candidate Candidate, EdgeContract? Edge)
    {
        RoutingHintContract? Hint = Edge?.RoutingHint;
        if (Hint?.IsActive != true) return true;
        return (Hint.Leave == null || Candidate.Exit == DiagramGridLayout.HintSide(Hint.Leave.Value))
            && (Hint.Enter == null || Candidate.Entry == DiagramGridLayout.HintSide(Hint.Enter.Value));
    }

    private static double Length(IReadOnlyList<FlowchartLayoutPoint> Points) => Enumerable.Range(1, Points.Count - 1)
        .Sum(Index => Distance(Points[Index - 1], Points[Index]));

    private static string CandidateKey(Candidate Candidate) => Candidate.Exit + "|" + Candidate.Entry + "|"
        + string.Join(";", Candidate.Points.Select(Point => Point.X.ToString("R", CultureInfo.InvariantCulture)
            + "," + Point.Y.ToString("R", CultureInfo.InvariantCulture)));

    private static string RouteStateKey(FlowchartLayoutEdgeRoute Route) => Route.EdgeId + "|"
        + Route.ExitSide + "|" + Route.EntrySide + "|"
        + string.Join(";", Route.Points.Select(Point =>
            BitConverter.DoubleToInt64Bits(Point.X).ToString("X16") + ","
            + BitConverter.DoubleToInt64Bits(Point.Y).ToString("X16")));

    private static IEnumerable<Candidate> Candidates(FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target, IEnumerable<FlowchartLayoutNodeBounds> Boxes,
        FlowchartDirection Direction, string AllocatedExit, string AllocatedEntry,
        FlowchartLayoutPoint AllocatedStart, FlowchartLayoutPoint AllocatedEnd,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        DiagramContract Contract, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        bool IncludeTerminalCandidates,
        bool RequireFacing = true, bool IncludeDetours = true,
        IReadOnlyList<FlowchartLayoutPoint>? SearchRegion = null,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>? TerminalObstacles = null)
    {
        // Rule 4.2 is a candidate, not a forced port override. The slanted-edge
        // allocator can put both terminals off the centre line; using only those
        // terminals would hide the legal tip-to-tip alternative from the scorer.
        if (IncludeTerminalCandidates)
        {
            Candidate? Straight = StraightCandidate(Source, Target, Nodes, Direction);
            if (Straight != null) yield return Straight;
            foreach (var Points in DiagramGridRouteBuilder.MinimumBendTerminalCandidates(
                         AllocatedStart, AllocatedEnd, Source, Target, AllocatedExit, AllocatedEntry,
                         Nodes, Bounds, Contract, TerminalObstacles))
                yield return New(Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToList(),
                    AllocatedExit, AllocatedEntry, Direction);
            yield break;
        }
        double Gap = DiagramGridMetrics.NODE_GAP_PX;
        double MinX = SearchRegion == null ? Math.Min(Source.CenterX, Target.CenterX)
            : SearchRegion.Min(Point => Point.X);
        double MaxX = SearchRegion == null ? Math.Max(Source.CenterX, Target.CenterX)
            : SearchRegion.Max(Point => Point.X);
        double MinY = SearchRegion == null ? Math.Min(Source.CenterY, Target.CenterY)
            : SearchRegion.Min(Point => Point.Y);
        double MaxY = SearchRegion == null ? Math.Max(Source.CenterY, Target.CenterY)
            : SearchRegion.Max(Point => Point.Y);
        double Margin = Math.Max(Gap, DiagramGridMetrics.GRID_UNIT_PX);
        IEnumerable<FlowchartLayoutNodeBounds> NearbyBoxes = Boxes.Where(Box =>
            Box.CenterX + Box.Width / 2.0 >= MinX - Margin
            && Box.CenterX - Box.Width / 2.0 <= MaxX + Margin
            && Box.CenterY + Box.Height / 2.0 >= MinY - Margin
            && Box.CenterY - Box.Height / 2.0 <= MaxY + Margin);
        List<double> XTracks = NearbyBoxes.SelectMany(Box => new[]
            { Box.CenterX - Box.Width / 2.0 - Gap / 2.0, Box.CenterX + Box.Width / 2.0 + Gap / 2.0 })
            .Distinct().Where(X => IsFreeVerticalTrack(X, MinY, MaxY, NearbyBoxes, Source, Target)).ToList();
        List<double> YTracks = NearbyBoxes.SelectMany(Box => new[]
            { Box.CenterY - Box.Height / 2.0 - Gap / 2.0, Box.CenterY + Box.Height / 2.0 + Gap / 2.0 })
            .Distinct().Where(Y => IsFreeHorizontalTrack(Y, MinX, MaxX, NearbyBoxes, Source, Target)).ToList();
        // Allocation already selected the rule 4.7 side pair and symmetric
        // slot. Repair may change the interior track only; it must not run a
        // second side/slot search whose cheaper route can undo that decision.
        foreach (string Exit in new[] { AllocatedExit })
        foreach (string Entry in new[] { AllocatedEntry })
        {
            if (RequireFacing && (!OtherEndBeyondSide(Source, Target, Exit)
                || !OtherEndBeyondSide(Target, Source, Entry))) continue;
            foreach (FlowchartLayoutPoint Start in new[] { AllocatedStart })
            foreach (FlowchartLayoutPoint End in new[] { AllocatedEnd })
            {
            if (Math.Abs(Start.X - End.X) < 0.5 || Math.Abs(Start.Y - End.Y) < 0.5)
            {
                List<FlowchartLayoutPoint> Direct = [Start, End];
                if (Directions(Direct, Exit, Entry))
                    yield return New(Direct, Exit, Entry, Direction);
            }
            bool StartHorizontal = Exit is "left" or "right";
            bool EndHorizontal = Entry is "left" or "right";
            if (StartHorizontal != EndHorizontal)
            {
                FlowchartLayoutPoint Bend = StartHorizontal ? Point(End.X, Start.Y) : Point(Start.X, End.Y);
                List<FlowchartLayoutPoint> Points = Collapse([Start, Bend, End]);
                if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry, Direction);
            }
            else if (!IncludeDetours)
            {
                continue;
            }
            else if (StartHorizontal)
            {
                foreach (double X in XTracks)
                {
                    List<FlowchartLayoutPoint> Points = Collapse([Start, Point(X, Start.Y), Point(X, End.Y), End]);
                    if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry, Direction);
                }
            }
            else
            {
                foreach (double Y in YTracks)
                {
                    List<FlowchartLayoutPoint> Points = Collapse([Start, Point(Start.X, Y), Point(End.X, Y), End]);
                    if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry, Direction);
                }
            }
            }
        }
    }

    /// <summary>Offers the axis-aligned outline-to-outline route independently of allocated sloping-edge slots.</summary>
    /// <param name="Source">The placed source outline bounds.</param>
    /// <param name="Target">The placed target outline bounds.</param>
    /// <param name="Nodes">Shape definitions used to resolve real cardinal boundary points.</param>
    /// <param name="Direction">The diagram direction used by the existing candidate tie-break.</param>
    /// <returns>A straight candidate, or null for unaligned centres or unsupported outlines.</returns>
    private static Candidate? StraightCandidate(FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target, IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        bool Supports(FlowchartLayoutNodeBounds Box) => CanReassign(Box.NodeId, Nodes)
            || Nodes.TryGetValue(Box.NodeId, out var Node)
                && (FlowchartDiamondAttachHelper.IsDiamondShape(Node.Shape)
                    || Node.Shape is "circle" or "doublecircle");
        if (!Supports(Source) || !Supports(Target)) return null;
        bool Horizontal = Math.Abs(Source.CenterY - Target.CenterY) < 0.001;
        bool Vertical = Math.Abs(Source.CenterX - Target.CenterX) < 0.001;
        if (Horizontal == Vertical) return null;
        double Sign = Math.Sign(Horizontal ? Target.CenterX - Source.CenterX : Target.CenterY - Source.CenterY);
        double Radius(FlowchartLayoutNodeBounds Box) => Nodes.TryGetValue(Box.NodeId, out var Node)
            && Node.Shape is "circle" or "doublecircle" ? Math.Min(Box.Width, Box.Height) / 2
            : (Horizontal ? Box.Width : Box.Height) / 2;
        var Start = new FlowchartLayoutPoint
        {
            X = Source.CenterX + (Horizontal ? Sign * Radius(Source) : 0),
            Y = Source.CenterY + (Horizontal ? 0 : Sign * Radius(Source))
        };
        var End = new FlowchartLayoutPoint
        {
            X = Target.CenterX - (Horizontal ? Sign * Radius(Target) : 0),
            Y = Target.CenterY - (Horizontal ? 0 : Sign * Radius(Target))
        };
        string Exit = Horizontal ? Sign > 0 ? "right" : "left" : Sign > 0 ? "top" : "bottom";
        string Entry = Horizontal ? Sign > 0 ? "left" : "right" : Sign > 0 ? "bottom" : "top";
        if (!Directions([Start, End], Exit, Entry)) return null;
        return New([Start, End], Exit, Entry, Direction) with { ReallocatesPorts = true };
    }

    private static bool IsFreeVerticalTrack(double X, double MinY, double MaxY,
        IEnumerable<FlowchartLayoutNodeBounds> Boxes, FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target) => !Boxes.Any(Box => Box != Source && Box != Target
            && X > Box.CenterX - Box.Width / 2.0 - 6
            && X < Box.CenterX + Box.Width / 2.0 + 6
            && Math.Max(MinY, Box.CenterY - Box.Height / 2.0 - 6)
                < Math.Min(MaxY, Box.CenterY + Box.Height / 2.0 + 6));

    private static bool IsFreeHorizontalTrack(double Y, double MinX, double MaxX,
        IEnumerable<FlowchartLayoutNodeBounds> Boxes, FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target) => !Boxes.Any(Box => Box != Source && Box != Target
            && Y > Box.CenterY - Box.Height / 2.0 - 6
            && Y < Box.CenterY + Box.Height / 2.0 + 6
            && Math.Max(MinX, Box.CenterX - Box.Width / 2.0 - 6)
                < Math.Min(MaxX, Box.CenterX + Box.Width / 2.0 + 6));

    private static Candidate New(List<FlowchartLayoutPoint> Points, string Exit, string Entry,
        FlowchartDirection Direction) => new(Points, Exit, Entry,
        Exit == DownstreamSide(Direction) ? 0 : Entry == UpstreamSide(Direction) ? 1 : 2,
        Enumerable.Range(1, Points.Count - 1).Sum(Index => Math.Abs(Points[Index].X - Points[Index - 1].X)
            + Math.Abs(Points[Index].Y - Points[Index - 1].Y)));
    private static int Corners(IReadOnlyList<FlowchartLayoutPoint> Points)
        => FlowchartLayoutScore.CountCorners(Points);
    private static bool OtherEndBeyondSide(FlowchartLayoutNodeBounds Box,
        FlowchartLayoutNodeBounds Other, string Side) => Side switch
    {
        "left" => Other.CenterX + Other.Width / 2.0 <= Box.CenterX - Box.Width / 2.0 + 0.5,
        "right" => Other.CenterX - Other.Width / 2.0 >= Box.CenterX + Box.Width / 2.0 - 0.5,
        "top" => Other.CenterY - Other.Height / 2.0 >= Box.CenterY + Box.Height / 2.0 - 0.5,
        _ => Other.CenterY + Other.Height / 2.0 <= Box.CenterY - Box.Height / 2.0 + 0.5
    };

    private static bool CanReassign(string Id, IReadOnlyDictionary<string, NodeContract> Nodes) =>
        !Nodes.TryGetValue(Id, out NodeContract? Node)
        || Node.Shape is null or "" or "rect" or "rounded" or "round" or "stadium" or "note";

    private static bool IsGeometricBackEdge(FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => Target.CenterY > Source.CenterY + 0.5,
        FlowchartDirection.BottomToTop => Target.CenterY < Source.CenterY - 0.5,
        FlowchartDirection.LeftToRight => Target.CenterX < Source.CenterX - 0.5,
        FlowchartDirection.RightToLeft => Target.CenterX > Source.CenterX + 0.5,
        _ => false
    };

    private static bool HasMinimumTerminalRuns(IReadOnlyList<FlowchartLayoutPoint> Points) =>
        Points.Count == 2 || Distance(Points[0], Points[1]) >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
            && Distance(Points[^2], Points[^1]) >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX;

    private static double Distance(FlowchartLayoutPoint A, FlowchartLayoutPoint B) =>
        Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);

    private static bool HasLabelRoom(IReadOnlyList<FlowchartLayoutPoint> Points,
        EdgeContract? Edge, DiagramContract Contract, bool OrthogonalRunsOnly = false)
    {
        if (string.IsNullOrWhiteSpace(Edge?.Label)) return true;
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            Edge.Label, Options, Options.EdgeLabelFontSizePx);
        for (int Index = 1; Index < Points.Count; Index++)
        {
            if (OrthogonalRunsOnly && Math.Abs(Points[Index].X - Points[Index - 1].X) >= 0.5
                && Math.Abs(Points[Index].Y - Points[Index - 1].Y) >= 0.5) continue;
            bool Horizontal = Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5;
            double Required = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                Dimensions, Options.EdgeLabelFontSizePx, Horizontal);
            double CornerRoom = Points.Count > 2 ? 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
                : DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            if (Distance(Points[Index - 1], Points[Index]) >= Required + CornerRoom - 0.5) return true;
        }
        return false;
    }

    private static bool PortsAreFree(Candidate Candidate, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, List<FlowchartLayoutEdgeRoute>> RoutesByEndpoint)
    {
        foreach (string NodeId in new[] { Route.SourceId, Route.TargetId })
        {
            if (!RoutesByEndpoint.TryGetValue(NodeId, out List<FlowchartLayoutEdgeRoute>? Others)) continue;
            foreach (FlowchartLayoutEdgeRoute Other in Others)
            {
                if (Other == Route || Other.Points.Count < 2) continue;
                if (Candidate.ReallocatesPorts)
                {
                    bool SourceEnd = NodeId == Route.SourceId;
                    var Port = SourceEnd ? Candidate.Points[0] : Candidate.Points[^1];
                    var Allocated = SourceEnd ? Route.Points[0] : Route.Points[^1];
                    string Side = SourceEnd ? Candidate.Exit : Candidate.Entry;
                    // A centre alternative cannot steal one member of a jointly
                    // distributed side. Existing centre slots remain candidates.
                    if (!Same(Port, Allocated)
                        && (Other.SourceId == NodeId && Other.ExitSide == Side
                            || Other.TargetId == NodeId && Other.EntrySide == Side)) return false;
                }
                if (Other.SourceId == Route.SourceId && Same(Other.Points[0], Candidate.Points[0])) return false;
                if (Other.TargetId == Route.SourceId && Same(Other.Points[^1], Candidate.Points[0])) return false;
                if (Other.SourceId == Route.TargetId && Same(Other.Points[0], Candidate.Points[^1])) return false;
                if (Other.TargetId == Route.TargetId && Same(Other.Points[^1], Candidate.Points[^1])) return false;
            }
        }
        return true;
    }

    private static bool NoWorseOverlap(IReadOnlyList<FlowchartLayoutPoint> Candidate,
        IReadOnlyList<(double X, double Y)> Original, FlowchartLayoutEdgeRoute Route,
        IReadOnlyCollection<FlowchartLayoutEdgeRoute> Others,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]> RoutePointCache)
    {
        double BeforeOverlap = 0.0, AfterOverlap = 0.0;
        int BeforePairs = 0, AfterPairs = 0;
        List<(double X, double Y)> CandidatePath = Tuples(Candidate);
        foreach (FlowchartLayoutEdgeRoute Other in Others)
        {
            IReadOnlyList<(double X, double Y)> OtherPath = RoutePointCache[Other];
            var Before = FlowchartBranchRouteOrder.Intersections(Original, OtherPath);
            BeforeOverlap += Before.Overlap;
            if (Before.Overlap > 0.001) BeforePairs++;
            var After = FlowchartBranchRouteOrder.Intersections(CandidatePath, OtherPath);
            AfterOverlap += After.Overlap;
            if (After.Overlap > 0.001) AfterPairs++;
        }
        return AfterPairs <= BeforePairs && AfterOverlap <= BeforeOverlap + 0.01;
    }

    private static List<(double X, double Y)> Tuples(IEnumerable<FlowchartLayoutPoint> Points) =>
        Points.Select(Point => (Point.X, Point.Y)).ToList();

    private static bool Same(FlowchartLayoutPoint A, FlowchartLayoutPoint B) =>
        Math.Abs(A.X - B.X) < 0.5 && Math.Abs(A.Y - B.Y) < 0.5;

    private static string DownstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "bottom",
        FlowchartDirection.BottomToTop => "top",
        FlowchartDirection.LeftToRight => "right",
        _ => "left"
    };

    private static string UpstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "top",
        FlowchartDirection.BottomToTop => "bottom",
        FlowchartDirection.LeftToRight => "left",
        _ => "right"
    };
    private static bool Directions(IReadOnlyList<FlowchartLayoutPoint> Points, string Exit, string Entry) =>
        Points.Count >= 2 && Leaves(Points[0], Points[1], Exit) && Enters(Points[^2], Points[^1], Entry)
        && Enumerable.Range(1, Points.Count - 1).All(Index =>
            Math.Abs(Points[Index].X - Points[Index - 1].X) < 0.5
            || Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5);
    private static bool Leaves(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X < A.X, "right" => B.X > A.X, "top" => B.Y > A.Y, _ => B.Y < A.Y
    };
    private static bool Enters(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X > A.X, "right" => B.X < A.X, "top" => B.Y < A.Y, _ => B.Y > A.Y
    };
    private static bool Clear(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Route,
        FlowchartRouteSpatialIndex Spatial)
    {
        foreach (FlowchartLayoutNodeBounds Box in Spatial.QueryBoxes(Points))
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId) continue;
            double Left = Box.CenterX - Box.Width / 2.0 - 6, Right = Box.CenterX + Box.Width / 2.0 + 6;
            double Bottom = Box.CenterY - Box.Height / 2.0 - 6, Top = Box.CenterY + Box.Height / 2.0 + 6;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index];
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top)) return false;
                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right)) return false;
            }
        }
        return true;
    }
    private static List<FlowchartLayoutPoint> Collapse(List<FlowchartLayoutPoint> Points)
    {
        for (int Index = Points.Count - 2; Index > 0; Index--)
            if ((Math.Abs(Points[Index - 1].X - Points[Index].X) < 0.5 && Math.Abs(Points[Index].X - Points[Index + 1].X) < 0.5)
                || (Math.Abs(Points[Index - 1].Y - Points[Index].Y) < 0.5 && Math.Abs(Points[Index].Y - Points[Index + 1].Y) < 0.5))
                Points.RemoveAt(Index);
        return Points;
    }
    private static void Set(FlowchartLayoutEdgeRoute Route, Candidate Candidate)
    {
        Route.Points.Clear(); Route.Points.AddRange(Candidate.Points.Select(Copy));
        Route.ExitSide = Candidate.Exit; Route.EntrySide = Candidate.Entry;
        if (Candidate.ReallocatesPorts)
        {
            // Port normals belong to the selected outline points, not to the
            // earlier sloping-edge allocation that this candidate supersedes.
            Route.ExitNormalX = Candidate.Exit == "left" ? -1 : Candidate.Exit == "right" ? 1 : 0;
            Route.ExitNormalY = Candidate.Exit == "bottom" ? -1 : Candidate.Exit == "top" ? 1 : 0;
            Route.EntryNormalX = Candidate.Entry == "left" ? -1 : Candidate.Entry == "right" ? 1 : 0;
            Route.EntryNormalY = Candidate.Entry == "bottom" ? -1 : Candidate.Entry == "top" ? 1 : 0;
        }
    }
    private static void Restore(FlowchartLayoutEdgeRoute Route, List<FlowchartLayoutPoint> Points, string Exit, string Entry)
    {
        Route.Points.Clear(); Route.Points.AddRange(Points.Select(Copy)); Route.ExitSide = Exit; Route.EntrySide = Entry;
    }
    /// <summary>Copies route geometry without quantising an allocated fractional port or a saved rollback state.</summary>
    /// <param name="Value">The exact point owned by the allocator, candidate or route snapshot.</param>
    /// <returns>An independent point with identical coordinates.</returns>
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Value) => new() { X = Value.X, Y = Value.Y };
    private static FlowchartLayoutPoint Point(double X, double Y) => new() { X = Math.Round(X), Y = Math.Round(Y) };
}
