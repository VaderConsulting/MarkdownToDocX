using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Protects accepted production crossing reductions without diagnostic pair filtering.</summary>
[TestFixture, NonParallelizable]
public sealed class JointCrossingPairProductionTests
{
    /// <summary>Checks the final catalogue routes, gate findings and the unchanged bend ceiling.</summary>
    /// <param name="Id">Catalogue source improved by the general pair assignment.</param>
    /// <param name="MaximumBends">Established bend ceiling.</param>
    /// <param name="MaximumCrossings">Improved final geometric crossing ceiling.</param>
    [TestCase("A14", 19, 1)]
    [TestCase("A152", 20, 0)]
    [TestCase("A167", 62, 40)]
    [TestCase("EIP271", 8, 1)]
    public void CompletePairAssignment_ReducesCrossingsWithoutAddingBends(string Id, int MaximumBends, int MaximumCrossings)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract,
            new FlowchartRenderContext { UseCellRegions = true }, out var Layout), Is.True);
        int Crossings = 0;
        for (int A = 0; A < Layout.EdgeRoutes.Count; A++) for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
            Crossings += FlowchartBranchRouteOrder.Intersections(
                Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
        Assert.That(Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Is.LessThanOrEqualTo(MaximumBends));
        Assert.That(Crossings, Is.LessThanOrEqualTo(MaximumCrossings));
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
    }
}
