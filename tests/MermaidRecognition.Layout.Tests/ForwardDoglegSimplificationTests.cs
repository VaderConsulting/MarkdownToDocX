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
        Route.Points.AddRange(new[] { Point(0,0), Point(40,0), Point(40,70), Point(30,70), Point(30,140), Point(90,140) });
        Layout.EdgeRoutesMutable.Add(Route);
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>();
        DiagramGridRouteBuilder.RemoveClearForwardDoglegs(Layout, Contract, Bounds);
        Assert.That(Route.Points, Has.Count.EqualTo(4));
        Assert.That(Route.Points[0].X, Is.EqualTo(Point(0,0).X));
        Assert.That(Route.Points[0].Y, Is.EqualTo(Point(0,0).Y));
        Assert.That(Route.Points[^1].X, Is.EqualTo(Point(90,140).X));
        Assert.That(Route.Points[^1].Y, Is.EqualTo(Point(90,140).Y));
        double Along(FlowchartLayoutPoint P) => (Vertical ? P.Y : P.X) * Sign;
        Assert.That(Along(Route.Points[1]), Is.InRange(40,60));
        Assert.That(Along(Route.Points[2]), Is.EqualTo(Along(Route.Points[1])));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ShortcutBlockedByBodyOrReservedTrack_RetainsOriginalRoute(bool Track)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\na --> b");
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute { SourceId = "a", TargetId = "b" };
        foreach (var P in new[] { (0d,0d),(40d,0d),(40d,70d),(30d,70d),(30d,140d),(90d,140d) })
            Route.Points.Add(new() { X=P.Item1,Y=P.Item2 });
        Layout.EdgeRoutesMutable.Add(Route);
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>();
        if (Track)
        {
            var Other = new FlowchartLayoutEdgeRoute { SourceId="c", TargetId="d" };
            Other.Points.AddRange(new[] { new FlowchartLayoutPoint { X=42,Y=90 },new FlowchartLayoutPoint { X=42,Y=130 } });
            Layout.EdgeRoutesMutable.Add(Other);
        }
        else Bounds["obstacle"] = new() { NodeId="obstacle", CenterX=42, CenterY=100, Width=18, Height=12 };
        DiagramGridRouteBuilder.RemoveClearForwardDoglegs(Layout, Contract, Bounds);
        Assert.That(Route.Points, Has.Count.EqualTo(6));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void NarrowFacingGap_GrowsWholeColumnBeforeSimplifying(bool Mirrored)
    {
        int Sign = Mirrored ? -1 : 1;
        var Contract = MermaidRecognizer.Parse("flowchart TB\na --> b");
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute { SourceId="a", TargetId="b" };
        foreach (var P in new[] { (0d,0d),(40d,0d),(40d,70d),(30d,70d),(30d,140d),(60d,140d) })
            Route.Points.Add(new() { X=Sign*P.Item1, Y=P.Item2 });
        Layout.EdgeRoutesMutable.Add(Route);
        var Source = new DiagramGridLayout.GridNode { Id="a",X=Sign*-60,Y=0,Width=120,Height=60 };
        var Target = new DiagramGridLayout.GridNode { Id="b",X=Sign*120,Y=140,Width=120,Height=60 };
        var Aligned = new DiagramGridLayout.GridNode { Id="c",X=Target.X,Y=300,Width=120,Height=60 };
        DiagramGridLayout.GrowFacingDoglegGaps(Layout, Contract, [Source,Target,Aligned], []);
        Assert.That(Target.X, Is.EqualTo(Sign*150));
        Assert.That(Aligned.X, Is.EqualTo(Target.X));
        Assert.That(Source.X, Is.EqualTo(Sign*-60));
        DiagramGridRouteBuilder.RemoveClearForwardDoglegs(Layout, Contract, []);
        Assert.That(Route.Points, Has.Count.EqualTo(4));
        Assert.That(Math.Abs(Route.Points[1].X - Route.Points[0].X) - 20, Is.GreaterThanOrEqualTo(20));
        Assert.That(Math.Abs(Route.Points[^1].X - Route.Points[^2].X) - 20, Is.GreaterThanOrEqualTo(10));
    }}
