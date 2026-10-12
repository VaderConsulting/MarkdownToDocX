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
        Assert.Multiple(() =>
        {
            Assert.That(Layout.EdgeRoutes.All(Route => Route.ExitSide == "bottom"), Is.True);
            Assert.That(Layout.EdgeRoutes.All(Route => Route.EntrySide == "top"), Is.True);
            Assert.That(Layout.EdgeRoutes.Select(Route => (Route.Points[0].X, Route.Points[0].Y)).Distinct().Count(),
                Is.EqualTo(3));
            Assert.That(Contract.Edges.All(Edge => Edge.RoutingHint!.UnhonouredReason == null), Is.True);
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
            Assert.That(Route.EntrySide, Is.EqualTo("bottom"));
            Assert.That(DiagramGridRouteBuilder.HasTravelDirection(Path, LayoutHintDirection.West), Is.True);
            Assert.That(Contract.Edges[0].RoutingHint!.UnhonouredReason, Is.Null);
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
