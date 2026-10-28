using System.Diagnostics;
using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.RuleValidation;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Records explicit before/after production measurements without silently recreating a baseline during normal tests.</summary>
[TestFixture, NonParallelizable]
public sealed class ExpandedBendCatalogueMeasurements
{
    /// <summary>Captures requested scenarios with new repairs disabled only for the measured before state.</summary>
    [Test, Explicit("Set EXPANDED_BEND_CAPTURE and EXPANDED_BEND_PHASE before invocation.")]
    public void Capture()
    {
        string Output = Environment.GetEnvironmentVariable("EXPANDED_BEND_CAPTURE") ?? throw new InvalidOperationException("Set EXPANDED_BEND_CAPTURE.");
        bool Before = Environment.GetEnvironmentVariable("EXPANDED_BEND_PHASE") == "before";
        bool Reference = Environment.GetEnvironmentVariable("EXPANDED_BEND_PHASE") == "overnight-reference";
        string[] Requested = (Environment.GetEnvironmentVariable("EXPANDED_BEND_IDS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlySet<string> Stress = CatalogueScenarioPolicy.StressScenarios;
        File.WriteAllText(Output, "");
        foreach (UnnecessaryBendCatalogueTests.CatalogueScenario? Scenario in UnnecessaryBendCatalogueTests.CatalogueScenarios().Select(Case =>
            (UnnecessaryBendCatalogueTests.CatalogueScenario)Case.Arguments[0]!).Where(Case => Requested.Length > 0 ? Requested.Contains(Case.Id) : !Stress.Contains(Case.Id)))
        {
            DiagramContract Contract = Scenario.Enterprise ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!) : MermaidRecognizer.Parse(Scenario.Source);
            List<string> Decisions = [];
            Action<string, bool>? PreviousObserver = FlowchartFinishedLayoutBendRepair.CandidateObserver;
            FlowchartFinishedLayoutBendRepair.CandidateObserver = (Key, Keep) => Decisions.Add((Keep ? "kept|" : "rejected|") + Key);
            Stopwatch Timer = Stopwatch.StartNew();
            FlowchartLayoutResult Layout;
            bool Complete;
            try
            {
                Complete = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true, DisableExpandedBendRepairsForTests = Before,
                    DisableFinishedBendRepairsForTests = Reference }, out Layout!);
            }
            finally { FlowchartFinishedLayoutBendRepair.CandidateObserver = PreviousObserver; }
            double LayoutSeconds = Timer.Elapsed.TotalSeconds;
            IReadOnlyList<UnnecessaryBendViolation>? Findings = Complete ? UnnecessaryBendChecker.Check(Scenario.Id, Contract, Layout) : null;
            int Crossings = 0;
            for (int A = 0; A < Layout.EdgeRoutes.Count; A++)
            {
                for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
                {
                    Crossings += FlowchartBranchRouteOrder.Intersections(Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
                }
            }

            File.AppendAllText(Output, JsonSerializer.Serialize(new { Scenario.Id, Complete, Before, LayoutSeconds,
                CheckSeconds = Timer.Elapsed.TotalSeconds - LayoutSeconds, Bends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)),
                Crossings, Findings, Decisions,
                QualityFindings = Complete ? FlowchartRouteRuleChecker.Check(Contract, Layout) : null,
                SlotFaults = Complete ? RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout) : null,
                GridFaults = Complete && Layout.CellNodes is { Count: > 0 } Cells ? CellGridAudit.Faults(Contract, Layout, Cells) : null,
                FrameOverlaps = Complete ? RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout).ToArray() : null,
                Hash = ProductionBendRenderPathTests.GeometryHash(Layout), Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes,
                Snapshot = CellProductionParityTests.Snapshot(Contract, Layout) }) + Environment.NewLine);
            TestContext.Out.WriteLine("EXPANDED_BEND_MEASUREMENT|" + Scenario.Id + "|" + Timer.Elapsed.TotalSeconds);
        }
    }
}
