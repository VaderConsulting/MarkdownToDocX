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
        string TracePath = Path.Combine(Path.GetTempPath(), $"{ScenarioId}-group-frame-refits.log");
        string StagePath = Path.Combine(Path.GetTempPath(), $"{ScenarioId}-group-frame-stages.log");
        File.Delete(TracePath);
        File.Delete(StagePath);
        int FitPasses = 0;
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
            File.AppendAllText(TracePath, $"{FitPasses}: {Cause}{Environment.NewLine}");
        };
        DiagramGridLayout.LayoutStageTimingObserver = (Stage, Milliseconds) =>
        {
            if (Milliseconds >= 0) File.AppendAllText(StagePath, $"{Stage}: {Milliseconds:0.0} ms{Environment.NewLine}");
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
            TestContext.Out.WriteLine($"{ScenarioId}: dagre {PlacementMs / 1000:0.00}s, grid layout {GridLayoutMs / 1000:0.00}s, {RefitsByCause.Values.Sum()} frame refits, "
                + $"{FitPasses} fitting passes, {Score.Crossings} crossings, {Score.Overlaps} overlaps, "
                + $"score {Score.Score?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "rejected"} "
                + $"({Score.Bends} bends, {Score.Disconnections} disconnections), score took {ScoreMs / 1000:0.00}s.");
        }
        finally
        {
            DiagramGridLayout.GroupFrameRefitObserver = null;
            DiagramGridLayout.GroupFrameFitPassObserver = null;
            DiagramGridLayout.LayoutStageTimingObserver = null;
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
