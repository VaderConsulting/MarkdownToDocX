namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartAlignedRouteStraightenerTests
{
    [Test]
    public void NearlyAlignedPorts_CollapseTinyDoglegToSharedStraightTrack()
    {
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute
        {
            SourceId = "source", TargetId = "target", ExitSide = "bottom", EntrySide = "top"
        };
        foreach ((double X, double Y) in new[] { (0.0, 70.0), (0.0, 50.0), (1.0, 50.0), (1.0, 30.0) })
            Route.Points.Add(new() { X = X, Y = Y });
        Layout.EdgeRoutesMutable.Add(Route);
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal)
        {
            ["source"] = new() { NodeId = "source", CenterX = 0, CenterY = 100, Width = 120, Height = 60 },
            ["target"] = new() { NodeId = "target", CenterX = 1, CenterY = 0, Width = 120, Height = 60 }
        };

        FlowchartAlignedRouteStraightener.ApplyNearlyAlignedFacing(Layout, Bounds);

        Assert.That(Route.Points, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(Route.Points[0].X, Is.EqualTo(0.5));
            Assert.That(Route.Points[1].X, Is.EqualTo(0.5));
            Assert.That(Route.Points[0].Y, Is.EqualTo(70));
            Assert.That(Route.Points[1].Y, Is.EqualTo(30));
        });
    }
}
