using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks route production itself, before final legality recovery, with both cold and warm geometry caches.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellRouteProductionLegalityTests
{
    /// <summary>Rebuilding either original A112 path must return a legal allocated-normal route without invoking final model repair.</summary>
    /// <param name="Target">Original failing link's target.</param>
    /// <param name="Blocker">Foreign node crossed by the old corridor producer.</param>
    [TestCase("checkPolicy", "checkReceipts")]
    [TestCase("managerOk", "financeOk")]
    public void RebuildPublishesLegalPathBeforeFinalRecovery(string Target, string Blocker)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = Source("A112");
        DiagramContract? Contract = null;
        FlowchartLayoutResult? Layout = null;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true,
            (CompletedContract, CompletedLayout) => { Contract = CompletedContract; Layout = CompletedLayout; }, Nodes =>
            {
                FlowchartLayoutEdgeRoute Route = Layout!.EdgeRoutes.Single(Edge => Edge.TargetId == Target);
                FlowchartLayoutPoint Start = Route.Points[0]; FlowchartLayoutPoint End = Route.Points[^1];
                FlowchartLogicalGrid.Snapshot Before = Nodes[0].CellNode!.Grid.Capture();
                Layout.RenderContext.GridRouteCache.Clear();
                (double X, double Y)[]? ColdPath = null;
                foreach (string CacheState in new[] { "cold", "warm" })
                {
                    // Only constructor inputs are retained. No final completion
                    // or legality-recovery stage runs after this direct call.
                    Route.Points.Clear(); Route.Points.AddRange([Start, End]);
                    DiagramGridRouteBuilder.RebuildAllRoutes(Layout, [.. Nodes],
                        Contract!.Nodes.ToDictionary(Node => Node.Id), Contract, [],
                        FlowchartDirection.TopToBottom, new HashSet<FlowchartLayoutEdgeRoute> { Route });
                    Assert.That(Route.Points.Count, Is.GreaterThanOrEqualTo(2));
                    Assert.That((Route.Points[0].X, Route.Points[0].Y), Is.EqualTo((Start.X, Start.Y)));
                    Assert.That((Route.Points[^1].X, Route.Points[^1].Y), Is.EqualTo((End.X, End.Y)));
                    Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
                        .Where(Finding => Finding.Subject == Route.EdgeId && Finding.Rule is "10" or "3.1" or "4.1"),
                        Is.Empty, $"The {CacheState} producer must not publish a path through {Blocker} or lose its allocated normal.");
                    Assert.That(Nodes[0].CellNode!.Grid.Capture(), Is.SameAs(Before));
                    (double X, double Y)[] Points = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
                    if (ColdPath == null)
                    {
                        ColdPath = Points;
                    }
                    else
                    {
                        Assert.That(Points, Is.EqualTo(ColdPath), "A warm cache must preserve exact normal and body coordinates.");
                    }
                }
            });
    }

    /// <summary>The current checker must keep the parked examples legal without reinstating the deferred 4.2 refinement.</summary>
    /// <param name="Id">Original parked scenario.</param>
    [TestCase("F32")]
    [TestCase("EIP261")]
    public void ParkedExamplesRetainTheirCurrentLegality(string Id) =>
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Source(Id), true, (Contract, Layout) =>
        {
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule == "10"), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
        });

    /// <summary>Returns an unmodified catalogue source, avoiding scenario-specific production code.</summary>
    /// <param name="Id">Declared catalogue identity.</param>
    /// <returns>The original scenario.</returns>
    private static RouteRuleCatalogueAuditTests.Scenario Source(string Id) => RouteRuleCatalogueAuditTests.Catalogue()
        .Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
}
