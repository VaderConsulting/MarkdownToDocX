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
            if (Milliseconds >= 0) StageEvents.Add($"{Stage}: {Milliseconds:0.0} ms");
        };
        DiagramGridLayout.RouteRepairPassObserver = (Stage, Routes, GridCalls, GridMs) =>
            RepairEvents.Add($"{Stage}: {string.Join(",", Routes)}; TryGridPath={GridCalls}; grid-search={GridMs:0.0} ms");
        FlowchartCrossGroupRouteSimplifier.RouteTimingObserver = (RouteId, Milliseconds, Changed) =>
        {
            var Prior = SimplifierRouteTimes.GetValueOrDefault(RouteId);
            SimplifierRouteTimes[RouteId] = (Prior.Ms + Milliseconds, Prior.Attempts + 1,
                Prior.Changes + (Changed ? 1 : 0));
        };
        try
        {
            Assert.That(FlowchartDagrePlacement.TryPlace(Contract, out FlowchartLayoutResult Placed), Is.True);
            Timer.Stop();
            double PlacementMs = Timer.Elapsed.TotalMilliseconds;
            File.AppendAllText(StagePath, $"placement elapsed: {PlacementMs:0.0} ms{Environment.NewLine}");
            Timer.Restart();
            FlowchartLayoutResult Layout = DiagramGridLayout.Apply(Contract, Placed);
            Timer.Stop();
            double GridLayoutMs = Timer.Elapsed.TotalMilliseconds;
            File.AppendAllText(StagePath, $"grid layout elapsed: {GridLayoutMs:0.0} ms{Environment.NewLine}");
            Timer.Restart();
            var Score = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
            Timer.Stop();
            double ScoreMs = Timer.Elapsed.TotalMilliseconds;
            File.AppendAllText(StagePath, $"score elapsed: {ScoreMs:0.0} ms{Environment.NewLine}");
            File.AppendAllLines(TracePath, FitPassEvents);
            File.AppendAllLines(StagePath, StageEvents);
            File.AppendAllLines(StagePath, RepairEvents);
            foreach (var (RouteId, Timing) in SimplifierRouteTimes.OrderByDescending(Item => Item.Value.Ms))
                File.AppendAllText(StagePath, $"cross-group route {RouteId}: {Timing.Ms:0.0} ms total; "
                    + $"{Timing.Attempts} attempts; {Timing.Changes} geometry changes{Environment.NewLine}");
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
                TestContext.Out.WriteLine($"{ScenarioId} route-shape fault: {string.Join(",", Failure.InvolvedIds)}; {Failure.Description}; {Geometry}");
                File.AppendAllText(StagePath, $"{ScenarioId} route-shape fault: {string.Join(",", Failure.InvolvedIds)}; "
                    + $"{Failure.Description}; {Geometry}{Environment.NewLine}");
            }
            TestContext.Out.WriteLine($"{ScenarioId}: dagre {PlacementMs / 1000:0.00}s, grid layout {GridLayoutMs / 1000:0.00}s, {RefitsByCause.Values.Sum()} frame refits, "
                + $"{FitPasses} fitting passes, {Score.Crossings} crossings, {Score.Overlaps} overlaps, "
                + $"score {Score.Score?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "rejected"} "
                + $"({Score.Bends} bends, {Score.Disconnections} disconnections), score took {ScoreMs / 1000:0.00}s.");
            File.AppendAllText(StagePath, $"{ScenarioId} score: crossings={Score.Crossings}, overlaps={Score.Overlaps}, "
                + $"score={Score.Score?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "rejected"}, "
                + $"bends={Score.Bends}, disconnections={Score.Disconnections}{Environment.NewLine}");
        }
        finally
        {
            DiagramGridLayout.GroupFrameRefitObserver = null;
            DiagramGridLayout.GroupFrameFitPassObserver = null;
            DiagramGridLayout.LayoutStageTimingObserver = null;
            DiagramGridLayout.RouteRepairPassObserver = null;
            FlowchartCrossGroupRouteSimplifier.RouteTimingObserver = null;
        }
        foreach (string Event in RefitEvents) TestContext.Out.WriteLine(Event);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? Cursor = new(AppContext.BaseDirectory); Cursor != null; Cursor = Cursor.Parent)
            if (File.Exists(Path.Combine(Cursor.FullName, "enterprise.md"))
                && File.Exists(Path.Combine(Cursor.FullName, "docs", "currentstatus.md"))) return Cursor.FullName;
        throw new DirectoryNotFoundException("Repository root containing enterprise.md was not found.");
    }
}
