using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ForwardDoglegSimplificationTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void FacingParallelPorts_RemoveBackwardStepWithoutMovingEndpoints(bool Vertical, bool Mirrored)
    {
        double Sign = Mirrored ? -1 : 1;
        FlowchartLayoutPoint Point(double X, double Y) => new() { X = Vertical ? Y : Sign * X, Y = Vertical ? Sign * X : Y };
        var Contract = MermaidRecognizer.Parse("flowchart TB\na --> b");
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute { SourceId = "a", TargetId = "b" };
        Route.Points.AddRange(new[] { Point(0,0), Point(40,0), Point(40,70), Point(30,70), Point(30,140), Point(60,140) });
        Layout.EdgeRoutesMutable.Add(Route);
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>();
        DiagramGridRouteBuilder.RemoveClearForwardDoglegs(Layout, Contract, Bounds);
        Assert.That(Route.Points, Has.Count.EqualTo(4));
        Assert.That(Route.Points[0].X, Is.EqualTo(Point(0,0).X));
        Assert.That(Route.Points[0].Y, Is.EqualTo(Point(0,0).Y));
        Assert.That(Route.Points[^1].X, Is.EqualTo(Point(60,140).X));
        Assert.That(Route.Points[^1].Y, Is.EqualTo(Point(60,140).Y));
        double Along(FlowchartLayoutPoint P) => (Vertical ? P.Y : P.X) * Sign;
        Assert.That(Along(Route.Points[1]), Is.InRange(20,30));
        Assert.That(Along(Route.Points[2]), Is.EqualTo(Along(Route.Points[1])));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ShortcutBlockedByBodyOrReservedTrack_RetainsOriginalRoute(bool Track)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\na --> b");
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute { SourceId = "a", TargetId = "b" };
        foreach (var P in new[] { (0d,0d),(40d,0d),(40d,70d),(30d,70d),(30d,140d),(60d,140d) })
            Route.Points.Add(new() { X=P.Item1,Y=P.Item2 });
        Layout.EdgeRoutesMutable.Add(Route);
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>();
        if (Track)
        {
            var Other = new FlowchartLayoutEdgeRoute { SourceId="c", TargetId="d" };
            Other.Points.AddRange(new[] { new FlowchartLayoutPoint { X=30,Y=10 },new FlowchartLayoutPoint { X=30,Y=60 } });
            Layout.EdgeRoutesMutable.Add(Other);
        }
        else Bounds["obstacle"] = new() { NodeId="obstacle", CenterX=30, CenterY=35, Width=12, Height=12 };
        DiagramGridRouteBuilder.RemoveClearForwardDoglegs(Layout, Contract, Bounds);
        Assert.That(Route.Points, Has.Count.EqualTo(6));
    }
}
