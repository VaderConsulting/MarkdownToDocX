using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Exercises the actual catalogue terminal pairs which fractional-port rounding hid.</summary>
[TestFixture, NonParallelizable]
public sealed class TerminalTrackRegressionTests
{
    /// <summary>Checks final production geometry, not a synthetic replacement of a catalogue path.</summary>
    /// <param name="Id">Catalogue scenario.</param>
    /// <param name="First">First declared relationship.</param>
    /// <param name="Second">Second declared relationship.</param>
    /// <param name="MaximumBends">Accepted total bend ceiling for the scenario.</param>
    [TestCase("A110", "containerHosting->orderManager", "messageQueue->webShop", 180)]
    [TestCase("A117", "g3->target", "baseline->legacyPayroll", 138)]
    [TestCase("A117", "g2->legacyPayroll", "g3->rosterApi", 138)]
    public void ProductionTerminalPair_DoesNotOverlapOrAddBends(string Id, string First, string Second, int MaximumBends)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Item => Item.Id == Id);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        FlowchartTerminalTrackRepair.Observer = (Kind, A, B) =>
        {
            if (Kind.StartsWith("gap", StringComparison.Ordinal) || (Kind != "conflict" && (A.Contains(First, StringComparison.Ordinal) || A.Contains(Second, StringComparison.Ordinal))))
            {
                TestContext.Out.WriteLine($"TERMINAL_REPAIR|{Id}|{Kind}|{A}|{B}");
            }
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract,
                new FlowchartRenderContext { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            FlowchartLayoutEdgeRoute A = Layout.EdgeRoutes.Single(Route => Route.EdgeId.StartsWith(First + ":", StringComparison.Ordinal));
            FlowchartLayoutEdgeRoute B = Layout.EdgeRoutes.Single(Route => Route.EdgeId.StartsWith(Second + ":", StringComparison.Ordinal));
            Assert.That(Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Is.LessThanOrEqualTo(MaximumBends));
            Assert.That(FlowchartBranchRouteOrder.Intersections(
                A.Points.Select(Point => (Point.X, Point.Y)).ToArray(), B.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Overlap,
                Is.EqualTo(0).Within(1e-9));
        }
        finally { FlowchartTerminalTrackRepair.Observer = null; }
    }
}
