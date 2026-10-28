using System.Diagnostics;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class GroupFrameRefitProfileTests
{
    [TestCase("A164")]
    [TestCase("A142")]
    [TestCase("A163")]
    [CancelAfter(60000)]
    public void ProfileGroupFrameRefits(string ScenarioId)
    {
        string Root = FindRepositoryRoot();
        string Markdown = File.ReadAllText(Path.Combine(Root, "enterprise.md"));
        Match Heading = Regex.Match(Markdown, $@"(?m)^###\s+{Regex.Escape(ScenarioId)}\b");
        Assert.That(Heading.Success, Is.True, $"Enterprise scenario {ScenarioId} was not found.");
        Match Fence = Regex.Match(Markdown[Heading.Index..], "(?ms)^```mermaid\\s*\\r?\\n(?<source>.*?)^```\\s*$");
        Assert.That(Fence.Success, Is.True, $"Enterprise scenario {ScenarioId} has no Mermaid source.");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(
            EnterpriseValidator.Analyze(Fence.Groups["source"].Value).Document);

        Dictionary<string, int> RefitsByCause = new(StringComparer.Ordinal);
        List<string> RefitEvents = [];
        List<string> FitPassEvents = [];
        List<string> StageEvents = [];
        List<string> RepairEvents = [];
        Dictionary<string, (double Ms, int Attempts, int Changes)> SimplifierRouteTimes = new(StringComparer.Ordinal);
        Dictionary<string, (int Candidates, double CandidateMs, int Searches, double SearchMs, int Rescores, double RescoreMs, int Evaluations)> SimplifierBreakdown = new(StringComparer.Ordinal);
        string TracePath = Path.Combine(Path.GetTempPath(), $"{ScenarioId}-group-frame-refits.log");
        string StagePath = Path.Combine(Path.GetTempPath(), $"{ScenarioId}-group-frame-stages.log");
        File.Delete(TracePath);
        File.Delete(StagePath);
        int FitPasses = 0;
        FlowchartPaddedVisibilityRouter.ResetDiagnostics();
        Stopwatch Timer = Stopwatch.StartNew();
        DiagramGridLayout.GroupFrameRefitObserver = Event =>
        {
            string Trigger = Event["refit for ".Length..Event.IndexOf(';')];
            RefitsByCause[Trigger] = RefitsByCause.GetValueOrDefault(Trigger) + 1;
            RefitEvents.Add(Event);
            File.AppendAllText(TracePath, Event + Environment.NewLine);
        };
        DiagramGridLayout.GroupFrameFitPassObserver = Cause =>
        {
            FitPasses++;
            FitPassEvents.Add($"{FitPasses}: {Cause}");
        };
        DiagramGridLayout.LayoutStageTimingObserver = (Stage, Milliseconds) =>
        {
            if (Milliseconds >= 0)
            {
                StageEvents.Add($"{Stage}: {Milliseconds:0.0} ms");
            }
        };
        DiagramGridLayout.RouteRepairPassObserver = (Stage, Routes, GridCalls, GridMs) =>
            RepairEvents.Add($"{Stage}: {string.Join(",", Routes)}; TryGridPath={GridCalls}; grid-search={GridMs:0.0} ms");
        FlowchartCrossGroupRouteSimplifier.RouteTimingObserver = (RouteId, Milliseconds, Changed) =>
        {
            (double Ms, int Attempts, int Changes) Prior = SimplifierRouteTimes.GetValueOrDefault(RouteId);
            SimplifierRouteTimes[RouteId] = (Prior.Ms + Milliseconds, Prior.Attempts + 1,
                Prior.Changes + (Changed ? 1 : 0));
        };
        FlowchartCrossGroupRouteSimplifier.RouteEvaluationObserver = (RouteId, Candidates, CandidateMs,
            Searches, SearchMs, Rescores, RescoreMs) =>
        {
            (int Candidates, double CandidateMs, int Searches, double SearchMs, int Rescores, double RescoreMs, int Evaluations) Prior = SimplifierBreakdown.GetValueOrDefault(RouteId);
            SimplifierBreakdown[RouteId] = (Prior.Candidates + Candidates, Prior.CandidateMs + CandidateMs,
                Prior.Searches + Searches, Prior.SearchMs + SearchMs, Prior.Rescores + Rescores,
                Prior.RescoreMs + RescoreMs, Prior.Evaluations + 1);
        };
        FlowchartTwoCornerRouteRepair.RouteCandidateObserver = (RouteId, Candidates, Valid, NonWorse, Changed) =>
            File.AppendAllText(StagePath, $"two-corner {RouteId}: {Candidates} candidates, {Valid} clear, "
                + $"{NonWorse} non-worse, changed={Changed}{Environment.NewLine}");
        FlowchartPortSideRouteRepair.RouteCandidateObserver = (RouteId, Candidates, Hinted, Clear, Free, Improving) =>
            File.AppendAllText(StagePath, $"port-side {RouteId}: {Candidates} candidates, {Hinted} hint-compatible, "
                + $"{Clear} clear, {Free} free-port, {Improving} improving{Environment.NewLine}");
        string[] WatchRouteIds = ["register->userRegSvc:25", "define->bwmSvc:31", "retire->bwmSvc:33"];
        DiagramGridLayout.LayoutStageObserver = (Stage, Layout) =>
        {
            foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => WatchRouteIds.Contains(Route.EdgeId)))
            {
                File.AppendAllText(StagePath, $"stage-route {Stage} {Route.EdgeId} {Route.ExitSide}/{Route.EntrySide}: "
                    + string.Join(" ", Route.Points.Select(Point => $"({Point.X:0},{Point.Y:0})")) + Environment.NewLine);
            }
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
            Timer.Stop();
            double GridLayoutMs = Timer.Elapsed.TotalMilliseconds;
            File.AppendAllText(StagePath, $"shared layout path elapsed: {GridLayoutMs:0.0} ms{Environment.NewLine}");
            Timer.Restart();
            FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
            Timer.Stop();
            double ScoreMs = Timer.Elapsed.TotalMilliseconds;
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> RouteFindings =
                FlowchartRouteRuleChecker.Check(Contract, Layout);
            int Rule59Findings = RouteFindings.Count(Failure => Failure.Rule == "5.9");
            string RejectionCauses = string.Join(", ", Score.Violations
                .GroupBy(Failure => Failure.Kind, StringComparer.Ordinal)
                .OrderBy(Group => Group.Key, StringComparer.Ordinal)
                .Select(Group => $"{Group.Key}={Group.Count()}"));
            if (ScenarioId == "A142" && Layout.EdgeRoutes.Count > 0)
            {
                FlowchartLayoutScore.CandidateMeasurement Incremental = new FlowchartLayoutScore.IncrementalContext(
                    Contract, Layout, IncludeArrangementCheck: false)
                    .MeasureCandidateSummary(Layout.EdgeRoutes[0], null);
                Assert.That(Incremental.Score, Is.EqualTo(Score.Score), "A142 incremental score");
                Assert.That(Incremental.OptimizationScore, Is.EqualTo(Score.OptimizationScore),
                    "A142 incremental optimization score");
                Assert.That(Incremental.Crossings, Is.EqualTo(Score.Crossings), "A142 incremental crossings");
                Assert.That(Incremental.Overlaps, Is.EqualTo(Score.Overlaps), "A142 incremental overlaps");
            }
            File.AppendAllText(StagePath, $"score elapsed: {ScoreMs:0.0} ms{Environment.NewLine}");
            File.AppendAllLines(TracePath, FitPassEvents);
            File.AppendAllLines(StagePath, StageEvents);
            File.AppendAllLines(StagePath, RepairEvents);
            foreach ((string? RouteId, (double Ms, int Attempts, int Changes) Timing) in SimplifierRouteTimes.OrderByDescending(Item => Item.Value.Ms))
            {
                File.AppendAllText(StagePath, $"cross-group route {RouteId}: {Timing.Ms:0.0} ms total; "
                    + $"{Timing.Attempts} attempts; {Timing.Changes} geometry changes{Environment.NewLine}");
            }

            foreach ((string? RouteId, (int Candidates, double CandidateMs, int Searches, double SearchMs, int Rescores, double RescoreMs, int Evaluations) Timing) in SimplifierBreakdown.OrderByDescending(Item =>
                         Item.Value.CandidateMs + Item.Value.SearchMs + Item.Value.RescoreMs).Take(10))
            {
                File.AppendAllText(StagePath, $"cross-group breakdown {RouteId}: {Timing.Evaluations} evaluations; "
                    + $"{Timing.Candidates} candidates; candidate-generation {Timing.CandidateMs:0.0} ms; "
                    + $"TryGridPath {Timing.Searches} calls / {Timing.SearchMs:0.0} ms; "
                    + $"rescoring {Timing.Rescores} calls / {Timing.RescoreMs:0.0} ms{Environment.NewLine}");
            }

            File.AppendAllLines(StagePath, [$"TryGridPath total: {FlowchartPaddedVisibilityRouter.GridPathCallCount}; "
                + $"{FlowchartPaddedVisibilityRouter.GridPathElapsedMilliseconds:0.0} ms total; "
                + $"average {(FlowchartPaddedVisibilityRouter.GridPathCallCount == 0 ? 0 : FlowchartPaddedVisibilityRouter.GridPathElapsedMilliseconds / FlowchartPaddedVisibilityRouter.GridPathCallCount):0.000} ms/call; "
                + $"max visibility vertices {FlowchartPaddedVisibilityRouter.GridPathMaximumVertexCount}"]);
            foreach (FlowchartLayoutQualityFailure Failure in Score.Violations.Where(Failure => Failure.Kind == "LineShape"))
            {
                FlowchartLayoutEdgeRoute? FaultRoute = Layout.EdgeRoutes.FirstOrDefault(Route =>
                    Route.SourceId == Failure.InvolvedIds.FirstOrDefault()
                    && Route.TargetId == Failure.InvolvedIds.Skip(1).FirstOrDefault());
                FlowchartLayoutNodeBounds? SourceBox = Layout.NodeBounds.FirstOrDefault(Box => Box.NodeId == FaultRoute?.SourceId);
                FlowchartLayoutNodeBounds? TargetBox = Layout.NodeBounds.FirstOrDefault(Box => Box.NodeId == FaultRoute?.TargetId);
                string Geometry = FaultRoute == null ? "route unavailable"
                    : $"exit={FaultRoute.ExitSide}, entry={FaultRoute.EntrySide}, points="
                        + string.Join(" ", FaultRoute.Points.Select(Point => $"({Point.X:0},{Point.Y:0})"))
                        + (SourceBox == null || TargetBox == null ? "" : $"; sourceBox=({SourceBox.CenterX:0},{SourceBox.CenterY:0},{SourceBox.Width:0},{SourceBox.Height:0})"
                            + $"; targetBox=({TargetBox.CenterX:0},{TargetBox.CenterY:0},{TargetBox.Width:0},{TargetBox.Height:0})");
                string Nearby = FaultRoute == null || SourceBox == null || TargetBox == null ? ""
                    : "; nearby=" + string.Join(" | ", Layout.NodeBounds.Where(Box =>
                        Box.NodeId != SourceBox.NodeId && Box.NodeId != TargetBox.NodeId
                        && Box.CenterX >= Math.Min(SourceBox.CenterX, TargetBox.CenterX) - 300
                        && Box.CenterX <= Math.Max(SourceBox.CenterX, TargetBox.CenterX) + 300
                        && Box.CenterY >= Math.Min(SourceBox.CenterY, TargetBox.CenterY) - 100
                        && Box.CenterY <= Math.Max(SourceBox.CenterY, TargetBox.CenterY) + 100)
                        .Select(Box => $"{Box.NodeId}=({Box.CenterX:0},{Box.CenterY:0},{Box.Width:0},{Box.Height:0})"));
                TestContext.Out.WriteLine($"{ScenarioId} route-shape fault: {string.Join(",", Failure.InvolvedIds)}; {Failure.Description}; {Geometry}");
                File.AppendAllText(StagePath, $"{ScenarioId} route-shape fault: {string.Join(",", Failure.InvolvedIds)}; "
                    + $"{Failure.Description}; {Geometry}{Nearby}{Environment.NewLine}");
            }
            TestContext.Out.WriteLine($"{ScenarioId}: shared layout {GridLayoutMs / 1000:0.00}s, {RefitsByCause.Values.Sum()} frame refits, "
                + $"{FitPasses} fitting passes, {Score.Crossings} crossings, {Score.Overlaps} overlaps, "
                + $"score {Score.Score?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "rejected"} "
                + $"({Score.Bends} bends, {Score.Disconnections} disconnections), score took {ScoreMs / 1000:0.00}s; "
                + $"rule 5.9 findings={Rule59Findings}; rejection causes={RejectionCauses}");
            File.AppendAllText(StagePath, $"{ScenarioId} score: crossings={Score.Crossings}, overlaps={Score.Overlaps}, "
                + $"score={Score.Score?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "rejected"}, "
                + $"bends={Score.Bends}, disconnections={Score.Disconnections}, rule59={Rule59Findings}, "
                + $"rejections={RejectionCauses}{Environment.NewLine}");
        }
        finally
        {
            DiagramGridLayout.GroupFrameRefitObserver = null;
            DiagramGridLayout.GroupFrameFitPassObserver = null;
            DiagramGridLayout.LayoutStageTimingObserver = null;
            DiagramGridLayout.RouteRepairPassObserver = null;
            FlowchartCrossGroupRouteSimplifier.RouteTimingObserver = null;
            FlowchartCrossGroupRouteSimplifier.RouteEvaluationObserver = null;
            FlowchartTwoCornerRouteRepair.RouteCandidateObserver = null;
            FlowchartPortSideRouteRepair.RouteCandidateObserver = null;
            DiagramGridLayout.LayoutStageObserver = null;
        }
        foreach (string Event in RefitEvents)
        {
            TestContext.Out.WriteLine(Event);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? Cursor = new(AppContext.BaseDirectory); Cursor != null; Cursor = Cursor.Parent)
        {
            if (File.Exists(Path.Combine(Cursor.FullName, "enterprise.md"))
                && File.Exists(Path.Combine(Cursor.FullName, "docs", "currentstatus.md")))
            {
                return Cursor.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root containing enterprise.md was not found.");
    }
}
