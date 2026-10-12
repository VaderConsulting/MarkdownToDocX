using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartConvergenceRouteRepairTests
{
    [Test]
    public void OffsetSource_KeepsOccupiedTopPortAndUsesOneCornerIntoFacingSide()
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nleft --> finish\ncentre --> finish");
        FlowchartLayoutResult Layout = LayoutWithSources(LeftY: 240, CentreY: 180);

        FlowchartConvergenceRouteRepair.Apply(Layout, Contract);

        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "left");
        Assert.That(Route.EntrySide, Is.EqualTo("left"));
        Assert.That(Route.Points.Select(Point => (Point.X, Point.Y)),
            Is.EqualTo(new[] { (0d, 210d), (0d, 0d), (165d, 0d) }));
    }

    [Test]
    public void SameRankFan_KeepsItsFanRoutingForTheFanPass()
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nleft --> finish\ncentre --> finish");
        FlowchartLayoutResult Layout = LayoutWithSources(LeftY: 180, CentreY: 180);
        List<(double X, double Y)> Before = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "left")
            .Points.Select(Point => (Point.X, Point.Y)).ToList();

        FlowchartConvergenceRouteRepair.Apply(Layout, Contract);

        Assert.That(Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "left")
            .Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Before));
    }

    private static FlowchartLayoutResult LayoutWithSources(double LeftY, double CentreY)
    {
        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new() { NodeId = "left", CenterX = 0, CenterY = LeftY, Width = 150, Height = 60 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "centre", CenterX = 240, CenterY = CentreY, Width = 150, Height = 60 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "finish", CenterX = 240, CenterY = 0, Width = 150, Height = 60 });

        var Left = new FlowchartLayoutEdgeRoute { SourceId = "left", TargetId = "finish", ExitSide = "bottom", EntrySide = "left" };
        Left.Points.Add(new() { X = 0, Y = LeftY - 30 });
        Left.Points.Add(new() { X = 0, Y = 120 });
        Left.Points.Add(new() { X = -60, Y = 120 });
        Left.Points.Add(new() { X = -60, Y = 0 });
        Left.Points.Add(new() { X = 165, Y = 0 });
        Layout.EdgeRoutesMutable.Add(Left);

        var Centre = new FlowchartLayoutEdgeRoute { SourceId = "centre", TargetId = "finish", ExitSide = "bottom", EntrySide = "top" };
        Centre.Points.Add(new() { X = 240, Y = CentreY - 30 });
        Centre.Points.Add(new() { X = 240, Y = 30 });
        Layout.EdgeRoutesMutable.Add(Centre);
        return Layout;
    }
}
