using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks complete final-model scoring and painted-guide restoration for a real whole-cell placement candidate.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellFinalModelCostTests
{
    /// <summary>A deliberately offset whole-cell endpoint becomes straight through scoring, within the unchanged budget, with no rejected-guide leak.</summary>
    [Test]
    public void ScoresCompletedRoutesAndKeepsTheWinningPaintedGuide()
    {
        var Scenario = new RouteRuleCatalogueAuditTests.Scenario("cell-final-cost-fixture", "flowchart TB\n A[Start] --> B[Finish]", false);
        bool Completed = false;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
        {
            Completed = true;
            var Route = Layout.EdgeRoutes.Single();
            Assert.That(FlowchartLayoutScore.CountCorners(Route.Points), Is.Zero);
            Assert.That(Layout.NodeBounds.Single(Node => Node.NodeId == "A").CenterX,
                Is.EqualTo(Layout.NodeBounds.Single(Node => Node.NodeId == "B").CenterX));
            Assert.That(Layout.RenderContext.MoveBeforeDetourBudget.CandidateMoves, Is.GreaterThan(0)
                .And.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            var Guide = Layout.ResolvedEdgeGuides!.Single();
            Assert.That(Guide.Points, Is.EqualTo(Route.Points.Select(Point => (Point.X, Point.Y)).ToList()));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule == "10"), Is.Empty);
        }, PrepareBeforeRouting: Nodes =>
        {
            Assert.That(FlowchartCellRepair.PlanMove([Nodes.Single(Node => Node.Id == "B")], 0, 2)!.TryApply(), Is.True);
        });
        Assert.That(Completed, Is.True);
    }
}
