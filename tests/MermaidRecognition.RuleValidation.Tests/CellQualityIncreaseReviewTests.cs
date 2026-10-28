using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <remarks>Deliberately compares the cells-off compatibility path with cells-on state; this is not a production-only quality baseline.</remarks>
/// <summary>Reviews every cell-induced straightness and facing-side increase on complete catalogue routes.</summary>
[TestFixture, NonParallelizable]
public sealed class CellQualityIncreaseReviewTests
{
    /// <summary>Uses unchanged affected sources, keeping the review independent of production scenario dispatch.</summary>
    /// <returns>Each affected scenario exactly once.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        HashSet<string> Ids = ["A115", "A163", "A164", "A165", "A167", "A169", "EIP137", "EIP18", "EIP301", "EIP332", "A63", "EIP236", "F136"];
        foreach (TestCaseData Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            RouteRuleCatalogueAuditTests.Scenario Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Ids.Contains(Scenario.Id))
            {
                yield return new TestCaseData(Scenario).SetName("CellQualityReview_" + Scenario.Id);
            }
        }
    }

    /// <summary>Requires an explicit legality reason for every rejected straight replacement and checks restored final regions and hints.</summary>
    /// <param name="Scenario">Original source from the affected-scenario census.</param>
    [TestCaseSource(nameof(Cases))]
    public void PublishedRoutesAndRejectedStraightReplacementsAreAccountedFor(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract OffContract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(OffContract, new FlowchartRenderContext { UseCellRegions = false }, out FlowchartLayoutResult? OffLayout), Is.True);
        TestContext.Out.WriteLine("CELL_QUALITY_OFF_REVIEW|" + Scenario.Id + "|" + JsonSerializer.Serialize(new
        {
            Findings = FlowchartRouteRuleChecker.Check(OffContract, OffLayout).Where(Finding => Finding.Rule is "4.2" or "4.11" or "5.8"),
            Bounds = OffLayout.NodeBounds.Concat(OffLayout.ClusterBounds), Routes = OffLayout.EdgeRoutes
        }));
        Action<string, string, double, bool>? Previous = DiagramGridLayout.RouteRepairCandidateObserver;
        DiagramGridLayout.RouteRepairCandidateObserver = (Option, Subject, Score, Keep) =>
        {
            Previous?.Invoke(Option, Subject, Score, Keep);
            if (Option == "facing-assignment")
            {
                TestContext.Out.WriteLine($"CELL_SIDE_REVIEW|{Scenario.Id}|{Subject}|keep={Keep}");
            }
        };
        try
        {
            MermaidRecognition.Contract.DiagramContract? CompletedContract = null;
            FlowchartLayoutResult? CompletedLayout = null;
            CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
            {
                CompletedContract = Contract; CompletedLayout = Layout;
                IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
                Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
                Assert.That(Layout.RenderContext.MoveBeforeDetourBudget.CandidateMoves,
                    Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
                Assert.That(Layout.EdgeRoutes.All(Route => Contract.Edges.Any(Edge => Edge.Id == Route.EdgeId)), Is.True);
                foreach (FlowchartRouteRuleChecker.Finding? Finding in Findings.Where(Finding => Finding.Rule == "4.2"))
                {
                    ReviewDirectReplacement(Scenario.Id, Finding.Subject, Contract, Layout);
                }

                TestContext.Out.WriteLine("CELL_QUALITY_REVIEW|" + Scenario.Id + "|" + JsonSerializer.Serialize(new
                {
                    Findings = Findings.Where(Finding => Finding.Rule is "4.2" or "4.11" or "5.8"),
                    Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds), Routes = Layout.EdgeRoutes,
                    Hints = Contract.Nodes.Select(Node => new { Node.Id, Node.PlacementHint }),
                    Budget = Layout.RenderContext.MoveBeforeDetourBudget.CandidateMoves
                }));
            }, ObserveBoundNodes: Nodes =>
            {
                Assert.That(CellGridAudit.Faults(CompletedContract!, CompletedLayout!, Nodes), Is.Empty);
                Assert.That(FlowchartPlacementHints.CellHintsMatch(CompletedContract!, [.. Nodes],
                    FlowchartCellRepair.Capture(Nodes).Regions), Is.True);
            });
        }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = Previous; }
    }

    /// <summary>Substitutes exactly one two-point candidate, records its rejection evidence, and restores the original path even when an assertion fails.</summary>
    /// <param name="Id">Catalogue identity for the diagnostic record.</param>
    /// <param name="EdgeId">Declared link with aligned allocated terminals.</param>
    /// <param name="Contract">Original parsed hints and shape definitions.</param>
    /// <param name="Layout">Completed published layout whose route is restored before returning.</param>
    private static void ReviewDirectReplacement(string Id, string EdgeId,
        MermaidRecognition.Contract.DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Route => Route.EdgeId == EdgeId);
        FlowchartLayoutPoint[] Saved = [.. Route.Points];
        (double X, double Y)[][] OtherPaths = [.. Layout.EdgeRoutes.Where(Other => Other != Route).Select(Other => Other.Points.Select(Point => (Point.X, Point.Y)).ToArray())];
        (double X, double Y)[] Original = [.. Saved.Select(Point => (Point.X, Point.Y))];
        FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(Contract, Layout, false);
        try
        {
            Route.Points.Clear(); Route.Points.Add(Saved[0]); Route.Points.Add(Saved[^1]);
            (double X, double Y)[] Direct = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
            FlowchartLayoutScore.Result After = FlowchartLayoutScore.Measure(Contract, Layout, false);
            FlowchartRouteRuleChecker.Finding[] Faults = [.. FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Item => Item.Subject == EdgeId && Item.Rule is "10" or "3.1" or "4.1")];
            Assert.That(Faults, Is.Not.Empty, "A legal straight candidate must not be explained away as a necessary detour: " + Id + "/" + EdgeId);
            TestContext.Out.WriteLine("CELL_DIRECT_REVIEW|" + Id + "|" + EdgeId + "|" + JsonSerializer.Serialize(new
            {
                Faults, BeforeScore = Before.OptimizationScore, AfterScore = After.OptimizationScore,
                BeforeOverlap = OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(Original, Other).Overlap),
                AfterOverlap = OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(Direct, Other).Overlap),
                BeforeCrossings = OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(Original, Other).Crossings),
                AfterCrossings = OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(Direct, Other).Crossings)
            }));
        }
        finally { Route.Points.Clear(); Route.Points.AddRange(Saved); }
    }
}
