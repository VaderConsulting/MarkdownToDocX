using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ObstacleRouteRepairTests
{
    [Test]
    public void RouteJustInsideUnrelatedBox_IsRepairedWithoutAttachTolerance()
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nfirst --> last\nblocker");
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>
        {
            ["first"] = new() { NodeId = "first", CenterX = 0, CenterY = 0, Width = 150, Height = 60 },
            ["last"] = new() { NodeId = "last", CenterX = 600, CenterY = 0, Width = 150, Height = 60 },
            ["blocker"] = new() { NodeId = "blocker", CenterX = 300, CenterY = 0, Width = 150, Height = 60 }
        };
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Contract.Edges[0].Id!, SourceId = "first", TargetId = "last" };
        foreach (var Point in new[] { (75d, 0d), (226d, 0d), (226d, -60d), (525d, -60d), (525d, 0d) })
            Route.Points.Add(new() { X = Point.Item1, Y = Point.Item2 });
        Layout.EdgeRoutesMutable.Add(Route);
        FlowchartObstacleRouteRepair.Apply(Layout, Contract, Bounds);
        foreach (var Pair in Route.Points.Zip(Route.Points.Skip(1)))
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Bounds["blocker"], 0), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BlockedStraightRoute_KeepsPortsAndGoesAroundUnrelatedBodies(bool Frame)
    {
        var Contract = MermaidRecognizer.Parse(Frame
            ? "flowchart LR\nfirst --> last\nsubgraph container\nblocker\nend"
            : "flowchart LR\nfirst --> last\nblocker");
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>
        {
            ["first"] = new() { NodeId = "first", CenterX = 0, CenterY = 0, Width = 150, Height = 60 },
            ["last"] = new() { NodeId = "last", CenterX = 600, CenterY = 0, Width = 150, Height = 60 },
            ["blocker"] = new() { NodeId = "blocker", CenterX = 300, CenterY = 0, Width = 150, Height = 60 }
        };
        if (Frame) Bounds["container"] = new() { NodeId = "container", CenterX = 300, CenterY = 0, Width = 210, Height = 150 };
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Contract.Edges[0].Id!, SourceId = "first", TargetId = "last" };
        Route.Points.Add(new() { X = 75, Y = 0 }); Route.Points.Add(new() { X = 525, Y = 0 });
        Layout.EdgeRoutesMutable.Add(Route);
        FlowchartObstacleRouteRepair.Apply(Layout, Contract, Bounds);
        Assert.That(Route.Points[0].X, Is.EqualTo(75));
        Assert.That(Route.Points[^1].X, Is.EqualTo(525));
        Assert.That(Route.Points[1].Y, Is.Zero);
        Assert.That(Route.Points[^2].Y, Is.Zero);
        foreach (var Pair in Route.Points.Zip(Route.Points.Skip(1)))
        {
            Assert.That(Pair.First.X == Pair.Second.X || Pair.First.Y == Pair.Second.Y, Is.True);
            foreach (var Box in Bounds.Values.Where(Box => Box.NodeId is not ("first" or "last")))
                Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box), Is.False);
        }
    }
}
