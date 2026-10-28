using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture, NonParallelizable]
public sealed class ExactConnectionPointTests
{
    [TestCase("application-component")]
    [TestCase("application-service")]
    [TestCase("data-object")]
    public void InitialLayoutRetainsExactPointsOnRendererBoundaries(string Type)
    {
        string Source = "enterprise\n" + Type + " a\n" + Type + " b below a +3\na -association-> b [leave S@-2, enter N@1]";
        var Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.HasErrors, Is.False);
        var Contract = EnterpriseFlowchartContractMapper.Build(Analysis.Document);
        List<string> Trace = [];
        DiagramGridLayout.LayoutStageObserver = (Stage, Result) => Trace.Add(Stage + ":" + string.Join(";", Result.EdgeRoutes
            .Where(Route => Route.Points.Count > 0).Select(Route => Route.Points[0].X + "," + Route.Points[0].Y)));
        FlowchartLayoutResult Layout;
        try { Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out Layout), Is.True); }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
        Assert.That(FlowchartConnectionPoints.Validate(Contract, Layout, CheckRoutes: true), Is.Null, string.Join("\n", Trace));
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout), Is.Empty);
        var Box = Layout.NodeBounds.Single(Box => Box.NodeId == "a");
        Assert.That(Layout.EdgeRoutes.Single().Points[0].X, Is.EqualTo(Box.CenterX - 2 * FlowchartLayoutRules.LineSpacing).Within(0.51));
        var Points = FlowchartConnectionPoints.ForNode(Contract.Nodes.Single(Node => Node.Id == "a"), Box);
        Assert.That(Points, Is.Not.Empty);
        Assert.That(Points.Select(Point => (Point.X, Point.Y)).Distinct().Count(), Is.EqualTo(Points.Count));
    }

    [Test]
    public void InitialLayoutReportsAnUnavailableExactPointInsteadOfClaimingItWasHonoured()
    {
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(
            "enterprise\napplication-component a\napplication-component b\na -flow-> b [leave S@10000]").Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        Assert.That(Contract.Edges[0].RoutingHint!.UnhonouredReason, Does.Contain("not available"));
        Assert.That(Layout.RenderContext.LayoutStageViolations.Any(Failure => Failure.Kind == "NoLegalRoute"), Is.True);
    }
}
