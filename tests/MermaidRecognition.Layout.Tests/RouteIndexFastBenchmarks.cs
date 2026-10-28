using System.Diagnostics;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class RouteIndexFastBenchmarks
{
    [Test]
    public void CompleteManyToMany_4x4() => Run(4);

    [Test]
    public void CompleteManyToMany_6x6() => Run(6);

    [Test]
    public void CompleteManyToMany_8x8() => Run(8);

    private static void Run(int Size)
    {
        StringBuilder Source = new("flowchart LR\n");
        for (int Index = 0; Index < Size; Index++)
        {
            Source.AppendLine($"s{Index}[Source {Index}]");
            Source.AppendLine($"t{Index}[Target {Index}]");
        }
        for (int From = 0; From < Size; From++)
        {
            for (int To = 0; To < Size; To++)
            {
                Source.AppendLine($"s{From} --> t{To}");
            }
        }

        DiagramContract Contract = MermaidRecognizer.Parse(Source.ToString());
        if (Size == 4)
        {
            List<string> CrossingTrace = [];
            DiagramGridLayout.CrossingCountObserver = (Stage, Crossings) =>
                CrossingTrace.Add($"CROSSING_TRACE stage={Stage} crossings={Crossings}");
            try
            {
                Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out _), Is.True,
                    "Crossing trace layout must complete successfully.");
            }
            finally
            {
                DiagramGridLayout.CrossingCountObserver = null;
            }
            foreach (string Entry in CrossingTrace)
            {
                TestContext.WriteLine(Entry);
            }

            int HorizontalSearchIndex = CrossingTrace.FindIndex(Entry =>
                Entry.StartsWith("CROSSING_TRACE stage=horizontal track search ", StringComparison.Ordinal));
            Assert.That(HorizontalSearchIndex, Is.GreaterThanOrEqualTo(0),
                "The 4x4 trace must include the horizontal track search stage.");
            Assert.That(CrossingTrace.Skip(HorizontalSearchIndex + 1).All(Entry =>
                    int.Parse(Entry[(Entry.LastIndexOf('=') + 1)..], System.Globalization.CultureInfo.InvariantCulture) <= 36),
                Is.True, "No stage after horizontal track search may raise the 4x4 crossing count above 36.");
        }
        if (Size == 8)
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out _), Is.True,
                "Warm-up layout must complete successfully.");
        }

        Stopwatch Timer = Stopwatch.StartNew();
        bool Success = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout);
        Timer.Stop();
        Assert.That(Success, Is.True, "TryLayout must complete successfully.");
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        string Failures = Metrics.Violations.Count == 0 ? "none"
            : string.Join(" || ", Metrics.Violations.Select(Failure => Failure.Kind + ": " + Failure.Description));
        int BaselineCrossings = Size switch { 4 => 36, 6 => 225, 8 => 784, _ => throw new ArgumentOutOfRangeException(nameof(Size)) };
        Assert.That(Metrics.Crossings, Is.LessThanOrEqualTo(BaselineCrossings),
            $"Crossings regressed from baseline {BaselineCrossings}. {Failures}");
        if (Size == 4)
        {
            Assert.That(Metrics.Crossings, Is.EqualTo(36), "The 4x4 target is 36 crossings.");
        }

        if (Size == 6)
        {
            Assert.That(Metrics.Crossings, Is.EqualTo(225), "The 6x6 target is 225 crossings.");
        }

        Assert.That(Metrics.Overlaps, Is.Zero, Failures);
        FlowchartLayoutEdgeRoute DeltaRoute = Layout.EdgeRoutes.First(Route => Route.Points.Count > 2);
        FlowchartLayoutPoint DeltaPoint = DeltaRoute.Points[1];
        double DeltaOriginalX = DeltaPoint.X;
        DeltaPoint.X += 1.0;
        (double X, double Y)[] CandidatePath = [.. DeltaRoute.Points.Select(Point => (Point.X, Point.Y))];
        int FullCrossingDelta = FlowchartLayoutScore.Measure(Contract, Layout,
            IncludeArrangementCheck: false).Crossings - Metrics.Crossings;
        DeltaPoint.X = DeltaOriginalX;
        int IncrementalCrossingDelta = FlowchartManyToManyRoutes.MeasureCrossingDelta(Layout,
            [DeltaRoute], new Dictionary<FlowchartLayoutEdgeRoute, IReadOnlyList<(double X, double Y)>>
            { [DeltaRoute] = CandidatePath });
        Assert.That(IncrementalCrossingDelta, Is.EqualTo(FullCrossingDelta),
            $"{Size}x{Size} affected-route crossing delta must equal the full crossing count delta.");
        Assert.That(Timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(60)), "The benchmark hard ceiling is 60 seconds.");
        if (Size == 8)
        {
            Assert.That(Timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "The 8x8 benchmark ceiling is five seconds.");
        }

        if (Timer.Elapsed > TimeSpan.FromSeconds(10))
        {
            TestContext.WriteLine($"SPEED_FAULT layout {Size}x{Size}: {Timer.Elapsed.TotalSeconds:0.00}s (over 10s target)");
        }

        TestContext.WriteLine($"SIZE={Size} ELAPSED_MS={Timer.ElapsedMilliseconds} CROSSINGS={Metrics.Crossings} "
            + $"OVERLAP={Metrics.Overlaps} FAILURE_TEXT={Failures}");

        FlowchartLayoutScore.IncrementalContext Incremental = new FlowchartLayoutScore.IncrementalContext(
            Contract, Layout, IncludeArrangementCheck: false);
        FlowchartLayoutEdgeRoute[] Routes = [.. Layout.EdgeRoutes];
        FlowchartRouteSpatialIndex Spatial = new(Layout);
        foreach (FlowchartLayoutEdgeRoute Route in Routes)
        {
            IReadOnlyCollection<FlowchartLayoutEdgeRoute> OriginalInteractions =
                Spatial.QueryRoutes(Route.Points, Route);
            FlowchartLayoutPoint Start = Route.Points[0];
            double OriginalX = Start.X;
            Start.X += 1.0;
            HashSet<FlowchartLayoutEdgeRoute> CandidateInteractions =
                [.. Spatial.QueryRoutes(Route.Points, Route)];
            CandidateInteractions.UnionWith(OriginalInteractions);
            FlowchartLayoutScore.CandidateMeasurement Summary = Incremental.MeasureCandidateSummary(
                Route, CandidateInteractions);
            FlowchartLayoutScore.Result Full = FlowchartLayoutScore.Measure(
                Contract, Layout, IncludeArrangementCheck: false);
            Start.X = OriginalX;
            Assert.Multiple(() =>
            {
                Assert.That(Summary.Score, Is.EqualTo(Full.Score), $"{Size}x{Size} {Route.EdgeId} score");
                Assert.That(Summary.OptimizationScore, Is.EqualTo(Full.OptimizationScore), $"{Size}x{Size} {Route.EdgeId} optimization score");
                Assert.That(Summary.Crossings, Is.EqualTo(Full.Crossings), $"{Size}x{Size} {Route.EdgeId} crossings");
                Assert.That(Summary.Overlaps, Is.EqualTo(Full.Overlaps), $"{Size}x{Size} {Route.EdgeId} overlaps");
                Assert.That(Summary.RejectingFaults, Is.EqualTo(Full.Violations.Count(Failure => Failure.Kind != "ArrangementKept")),
                    $"{Size}x{Size} {Route.EdgeId} rejecting faults");
            });
        }
        const int TrialsPerRoute = 8;
        FlowchartLayoutScore.Result[,] Candidates = new FlowchartLayoutScore.Result[Routes.Length, TrialsPerRoute];
        FlowchartLayoutScore.Result[,] FullCandidates = new FlowchartLayoutScore.Result[Routes.Length, TrialsPerRoute];
        double[] OriginalXs = [.. Routes.Select(Route => Route.Points[0].X)];
        Stopwatch IncrementalTimer = Stopwatch.StartNew();
        for (int Index = 0; Index < Routes.Length; Index++)
        {
            FlowchartLayoutEdgeRoute Route = Routes[Index];
            FlowchartLayoutPoint Point = Route.Points[0];
            for (int Trial = 0; Trial < TrialsPerRoute; Trial++)
            {
                Point.X = OriginalXs[Index] + Trial + 1.0;
                Candidates[Index, Trial] = Incremental.MeasureCandidate(Route);
            }
            Incremental.CommitCurrent(Route);
        }
        IncrementalTimer.Stop();

        for (int Index = 0; Index < Routes.Length; Index++)
        {
            Routes[Index].Points[0].X = OriginalXs[Index];
        }

        Stopwatch FullTimer = Stopwatch.StartNew();
        for (int Index = 0; Index < Routes.Length; Index++)
        {
            FlowchartLayoutEdgeRoute Route = Routes[Index];
            FlowchartLayoutPoint Point = Route.Points[0];
            for (int Trial = 0; Trial < TrialsPerRoute; Trial++)
            {
                Point.X = OriginalXs[Index] + Trial + 1.0;
                FullCandidates[Index, Trial] = FlowchartLayoutScore.Measure(
                    Contract, Layout, IncludeArrangementCheck: false);
            }
        }
        FullTimer.Stop();
        for (int Index = 0; Index < Routes.Length; Index++)
        {
            for (int Trial = 0; Trial < TrialsPerRoute; Trial++)
            {
                AssertEquivalent(FullCandidates[Index, Trial], Candidates[Index, Trial], Size, Index);
            }

            Routes[Index].Points[0].X = OriginalXs[Index];
        }
        Assert.That(IncrementalTimer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(60)),
            "Incremental score benchmark hard ceiling is 60 seconds.");
        Assert.That(FullTimer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(60)),
            "Reference score benchmark hard ceiling is 60 seconds.");
        if (IncrementalTimer.Elapsed > TimeSpan.FromSeconds(10))
        {
            TestContext.WriteLine($"SPEED_FAULT incremental score {Size}x{Size}: {IncrementalTimer.Elapsed.TotalSeconds:0.00}s (over 10s target)");
        }

        if (FullTimer.Elapsed > TimeSpan.FromSeconds(10))
        {
            TestContext.WriteLine($"SPEED_FAULT full score {Size}x{Size}: {FullTimer.Elapsed.TotalSeconds:0.00}s (over 10s target)");
        }

        TestContext.WriteLine($"SCORE_SIZE={Size} ROUTES={Routes.Length} TRIALS={Routes.Length * TrialsPerRoute} "
            + $"INCREMENTAL_MS={IncrementalTimer.ElapsedMilliseconds} FULL_MS={FullTimer.ElapsedMilliseconds} "
            + $"EQUAL=true");
    }

    private static void AssertEquivalent(FlowchartLayoutScore.Result Full,
        FlowchartLayoutScore.Result Incremental, int Size, int RouteIndex)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Incremental.Width, Is.EqualTo(Full.Width), $"{Size}x{Size} route {RouteIndex} width");
            Assert.That(Incremental.Height, Is.EqualTo(Full.Height), $"{Size}x{Size} route {RouteIndex} height");
            Assert.That(Incremental.Overlaps, Is.EqualTo(Full.Overlaps), $"{Size}x{Size} route {RouteIndex} overlaps");
            Assert.That(Incremental.Crossings, Is.EqualTo(Full.Crossings), $"{Size}x{Size} route {RouteIndex} crossings");
            Assert.That(Incremental.ForcedMinimum, Is.EqualTo(Full.ForcedMinimum), $"{Size}x{Size} route {RouteIndex} forced minimum");
            Assert.That(Incremental.Bends, Is.EqualTo(Full.Bends), $"{Size}x{Size} route {RouteIndex} bends");
            Assert.That(Incremental.Disconnections, Is.EqualTo(Full.Disconnections), $"{Size}x{Size} route {RouteIndex} disconnections");
            Assert.That(Incremental.HintsHonoured, Is.EqualTo(Full.HintsHonoured), $"{Size}x{Size} route {RouteIndex} hints honoured");
            Assert.That(Incremental.HintsTotal, Is.EqualTo(Full.HintsTotal), $"{Size}x{Size} route {RouteIndex} hints total");
            Assert.That(Incremental.EmptyShare, Is.EqualTo(Full.EmptyShare), $"{Size}x{Size} route {RouteIndex} empty share");
            Assert.That(Incremental.Score, Is.EqualTo(Full.Score), $"{Size}x{Size} route {RouteIndex} score");
            Assert.That(Incremental.OptimizationScore, Is.EqualTo(Full.OptimizationScore),
                $"{Size}x{Size} route {RouteIndex} optimization score");
            Assert.That(Incremental.Violations.Select(FailureKey).OrderBy(Key => Key, StringComparer.Ordinal),
                Is.EqualTo(Full.Violations.Select(FailureKey).OrderBy(Key => Key, StringComparer.Ordinal)),
                $"{Size}x{Size} route {RouteIndex} violations");
        });
    }

    private static string FailureKey(FlowchartLayoutQualityFailure Failure) =>
        Failure.Kind + "|" + Failure.Description + "|" + string.Join(",", Failure.InvolvedIds)
        + "|" + Failure.Justification;
}
