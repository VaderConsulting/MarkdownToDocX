using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Replays the archived joint region assignment through complete final-pass confirmation, never through an earlier relaxed guard.</summary>
[TestFixture, NonParallelizable, Explicit("Measures A117's archived joint assignment against the settled final layout.")]
public sealed class FinalRepairA117DiagnosticTests
{
    /// <summary>Measures the exact g3-north/wpSelect-west option with unchanged production limits unless a local cap is requested.</summary>
    /// <param name="Cap">Final-pass measurement cap; the canonical rules remain unchanged.</param>
    [TestCase(61.0), TestCase(65.0), TestCase(80.0)]
    public void MeasureJointRegionCandidate(double Cap)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A117");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        DiagramGridLayout.FinalRepairLengthCapForTests = Cap;
        DiagramGridLayout.FinalRepairRegionForTests = Nodes => new Dictionary<string, FlowchartCellRegion> {
            ["g3"] = FlowchartCellRepair.Translate(Nodes.Single(Node => Node.Id == "g3").CellNode!.Region, -1, 0),
            ["wpSelect"] = FlowchartCellRepair.Translate(Nodes.Single(Node => Node.Id == "wpSelect").CellNode!.Region, 0, -1) };
        DiagramGridLayout.FinalRepairMeasurementObserver = (Key, Layout) => Report("candidate", Layout);
        DiagramGridLayout.FinalRepairObserver = (Key, Reason, Length) => TestContext.Out.WriteLine($"A117_FINAL_DECISION|cap={Cap}|{Reason}|length={Length:R}");
        DiagramGridLayout.FinalRepairStateObserver = (Stage, Layout, Nodes) => { if (Stage == "before") { Report("before", Layout); } };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            Report("published", Layout);
        }
        finally
        {
            DiagramGridLayout.FinalRepairLengthCapForTests = null; DiagramGridLayout.FinalRepairRegionForTests = null;
            DiagramGridLayout.FinalRepairObserver = null; DiagramGridLayout.FinalRepairStateObserver = null;
            DiagramGridLayout.FinalRepairMeasurementObserver = null;
        }

        // <summary>Writes complete model measurements, including remote paths rather than an affected-route subtotal.</summary>
        // <param name="Stage">Before, candidate or published state.</param>
        // <param name="Layout">Finished complete model.</param>
        void Report(string Stage, FlowchartLayoutResult Layout)
        {
            int Crossings = 0, Shared = 0;
            Dictionary<string, int> FanPairs = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int A = 0; A < Layout.EdgeRoutes.Count; A++)
            {
                for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
            {
                (int Crossings, double Overlap) Hits = FlowchartBranchRouteOrder.Intersections(Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray());
                Crossings += Hits.Crossings; if (Hits.Overlap > 0)
                    {
                        Shared++;
                    }

                    FlowchartLayoutEdgeRoute First = Layout.EdgeRoutes[A]; FlowchartLayoutEdgeRoute Second = Layout.EdgeRoutes[B];
                if (Hits.Crossings > 0 && (First.SourceId == Second.SourceId
                    || First.TargetId == Second.TargetId))
                    {
                        FanPairs[First.EdgeId + " x " + Second.EdgeId] = Hits.Crossings;
                    }
                }
            }

            IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout); FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
            TestContext.Out.WriteLine("A117_FINAL_METRICS|" + JsonSerializer.Serialize(new { Stage, Cap, Score.Bends, Crossings, Shared,
                FiveNine = Findings.Count(Finding => Finding.Rule == "5.9"), TotalFindings = Findings.Count,
                SameFanCrossings = FanPairs.Values.Sum(), FanPairs,
                Score = (double)Score.Cost, Length = Layout.EdgeRoutes.Sum(Route => Enumerable.Range(1, Math.Max(0, Route.Points.Count - 1))
                    .Sum(double (int Index) => Math.Abs(Route.Points[Index].X - Route.Points[Index - 1].X) + Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y))) }));
        }
    }
}
