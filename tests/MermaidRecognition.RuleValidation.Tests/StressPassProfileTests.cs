using System.Diagnostics;
using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Records one diagnostic stress layout without changing routing, watchdogs or search budgets.</summary>
[TestFixture, NonParallelizable]
public sealed class StressPassProfileTests
{
    /// <summary>Profiles A142 outside the gate, including compact-metric analysis after production routing.</summary>
    [Test, Explicit("Diagnostic only: run once outside the stress gate with STRESS_PASS_PROFILE set.")]
    public void ProfileA142OutsideGate()
    {
        string Path = Environment.GetEnvironmentVariable("STRESS_PASS_PROFILE")
            ?? throw new InvalidOperationException("Set STRESS_PASS_PROFILE to the output file.");
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A142");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Stopwatch Clock = Stopwatch.StartNew();
        File.WriteAllText(Path, "");
        Action<string, double>? Previous = DiagramGridLayout.LayoutStageTimingObserver;
        int Sequence = 0;
        DiagramGridLayout.LayoutStageTimingObserver = (Stage, Milliseconds) =>
            File.AppendAllText(Path, JsonSerializer.Serialize(new { Sequence = ++Sequence, Stage,
                Milliseconds, ElapsedSeconds = Clock.Elapsed.TotalSeconds }) + "\n");
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            File.AppendAllText(Path, JsonSerializer.Serialize(new { Stage = "production-layout-complete", ElapsedSeconds = Clock.Elapsed.TotalSeconds }) + "\n");
            Stopwatch MetricsClock = Stopwatch.StartNew();
            FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout);
            File.AppendAllText(Path, JsonSerializer.Serialize(new { Stage = "compact-metric-analysis", Milliseconds = MetricsClock.Elapsed.TotalMilliseconds,
                ElapsedSeconds = Clock.Elapsed.TotalSeconds, Score.Bends, Score.Crossings }) + "\n");
        }
        finally { DiagramGridLayout.LayoutStageTimingObserver = Previous; }
    }
}
