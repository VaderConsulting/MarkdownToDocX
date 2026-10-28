using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class RoutingHintLayoutTests
{
    [Test]
    public void FanHints_UseDistinctSouthAndNorthPorts()
    {
        const string Source = """
            enterprise
              application-component hub "Hub"
              application-component a "A"
              application-component b "B"
              application-component c "C"
              hub -flow-> a [leave S, enter N]
              hub -flow-> b [leave S, enter N]
              hub -flow-> c [leave S, enter N]
            """;

        (DiagramContract Contract, FlowchartLayoutResult Layout) = LayoutOf(Source);
        double[] SourcePorts = Layout.EdgeRoutes.Select(Route => Route.Points[0].X).Order().ToArray();
        double HubCentre = Layout.NodeBounds.Single(Node => Node.NodeId == "hub").CenterX;
        Assert.Multiple(() =>
        {
            Assert.That(Layout.EdgeRoutes.All(Route => Route.ExitSide == "bottom"), Is.True);
            Assert.That(Layout.EdgeRoutes.All(Route => Route.EntrySide == "top"), Is.True);
            Assert.That(Layout.EdgeRoutes.Select(Route => (Route.Points[0].X, Route.Points[0].Y)).Distinct().Count(),
                Is.EqualTo(3));
            Assert.That(SourcePorts[1], Is.EqualTo(HubCentre).Within(0.01));
            Assert.That(SourcePorts[1] - SourcePorts[0], Is.EqualTo(SourcePorts[2] - SourcePorts[1]).Within(0.01));
            Assert.That(SourcePorts[0] + SourcePorts[2], Is.EqualTo(2 * HubCentre).Within(0.01));
            Assert.That(Contract.Edges.All(Edge => Edge.RoutingHint!.UnhonouredReason == null), Is.True);
        });
    }

    [Test]
    public void RenderedEnterpriseRoutes_PreserveHintedSidesAndPortsAfterTrackRepair()
    {
        var Lines = new List<string> { "enterprise", "  application-component hub \"Hub\"" };
        for (int Index = 1; Index <= 8; Index++)
            Lines.Add($"  application-component target{Index} \"Target {Index}\"");
        for (int Index = 1; Index <= 8; Index++)
            Lines.Add($"  hub -flow-> target{Index} [leave S, enter N]");

        EnterpriseDocument Document = EnterpriseValidator.Analyze(string.Join("\n", Lines)).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        EnterpriseLayoutNode Hub = Layout.Nodes["hub"];

        Assert.Multiple(() =>
        {
            Assert.That(Routes, Has.Count.EqualTo(8));
            Assert.That(Routes.Select(Route => Route.SvgPoints[0]).Distinct().Count(), Is.EqualTo(8),
                "Each hinted line must keep its own source port after enterprise track repair.");
            Assert.That(Routes.All(Route => Math.Abs(Route.SvgPoints[0].Y - (Hub.AbsoluteY + Hub.Height)) < 0.5),
                Is.True, "Each hinted line must leave the south side after enterprise track repair.");
            Assert.That(Routes.All(Route => Math.Abs(Route.SvgPoints[^1].Y
                - Layout.Nodes[Route.Relationship.TargetId].AbsoluteY) < 0.5), Is.True,
                "Each hinted line must keep its north entry after enterprise track repair.");
            Assert.That(Routes.All(Route => Route.Relationship.RoutingHint?.UnhonouredReason == null), Is.True);
        });
    }

    [Test]
    public void AgainstFlowHint_LeavesNorthAndMayUseExtraCorners()
    {
        const string Source = """
            enterprise
              application-component a "A"
              application-component b "B"
              a -flow-> b [leave N]
            """;

        (DiagramContract Contract, FlowchartLayoutResult Layout) = LayoutOf(Source);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Route.ExitSide, Is.EqualTo("top"));
            Assert.That(Route.Points[1].Y, Is.GreaterThan(Route.Points[0].Y));
            Assert.That(Contract.Edges[0].RoutingHint!.UnhonouredReason, Is.Null);
        });
    }

    [Test]
    public void TravelHint_SelectsMiddleRunDirection()
    {
        const string Source = """
            enterprise
              application-component target "Target"
              application-component source "Source" right-of target
              source -flow-> target [leave N, enter S, travel W]
            """;

        (DiagramContract Contract, FlowchartLayoutResult Layout) = LayoutOf(Source);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single();
        List<(double X, double Y)> Path = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(Route.ExitSide, Is.EqualTo("top"));
            Assert.That(Route.Points[0].X, Is.EqualTo(Layout.NodeBounds.Single(Node => Node.NodeId == "source").CenterX).Within(0.01));
            Assert.That(Route.EntrySide, Is.EqualTo("bottom"));
            Assert.That(DiagramGridRouteBuilder.HasTravelDirection(Path, LayoutHintDirection.West), Is.True);
            Assert.That(Contract.Edges[0].RoutingHint!.UnhonouredReason, Is.Null);
        });
    }

    [Test]
    public void Score_SubtractsHonouredHintsAndCountsHintedBends()
    {
        const string Source = """
            enterprise
              application-component target "Target"
              application-component source "Source" right-of target
              source -flow-> target [leave N, enter S, travel W]
            """;

        (DiagramContract Contract, FlowchartLayoutResult Layout) = LayoutOf(Source);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        double WithoutHints = FlowchartLayoutScore.Calculate(
            Metrics.Width, Metrics.Height, Metrics.Crossings, Metrics.ForcedMinimum, Metrics.EmptyShare)
            + Metrics.Bends * FlowchartLayoutScore.BEND_PENALTY;
        double Length = Layout.EdgeRoutes.Sum(Route => Route.Points.Zip(Route.Points.Skip(1)).Sum(double ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair) =>
            Math.Sqrt(Math.Pow(Pair.First.X - Pair.Second.X, 2) + Math.Pow(Pair.First.Y - Pair.Second.Y, 2))))
            / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RouteLengthWeight;
        double Bound = Math.Min(FlowchartLayoutRules.BendScore, FlowchartLayoutRules.CrossingScore);
        WithoutHints += Length / (1 + Length / Bound);
        Assert.Multiple(() =>
        {
            Assert.That(Metrics.HintsHonoured, Is.EqualTo(2));
            Assert.That(Metrics.HintsTotal, Is.EqualTo(2));
            Assert.That(Metrics.Bends, Is.GreaterThan(2));
            Assert.That(Metrics.Score, Is.EqualTo(WithoutHints - 45.0).Within(0.01));
        });
    }

    private static (DiagramContract Contract, FlowchartLayoutResult Layout) LayoutOf(string Source)
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        return (Contract, Layout);
    }
}
