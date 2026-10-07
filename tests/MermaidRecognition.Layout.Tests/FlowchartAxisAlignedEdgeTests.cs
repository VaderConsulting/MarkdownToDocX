using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Synthetic checks for the straight-segment rule. These points are not taken from a diagram.
/// </summary>
[TestFixture]
public sealed class FlowchartAxisAlignedEdgeTests
{
    [Test]
    public void VerticalEndpoints_DropMiddleWaypoints()
    {
        List<(double X, double Y)> Points =
        [
            (100.4, 20.2),
            (140.0, 80.0),
            (99.6, 180.4)
        ];

        bool Collapsed = FlowchartCurvePathBuilder.TryCollapseAxisAlignedEndpoints(
            Points,
            out List<(double X, double Y)> Segment);

        Assert.That(Collapsed, Is.True);
        Assert.That(Segment, Has.Count.EqualTo(2));
        Assert.That(Segment[0].X, Is.EqualTo(Segment[1].X));
        Assert.That(Segment[0].Y, Is.Not.EqualTo(Segment[1].Y));
        string PathData = FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(Points, "basis");
        Assert.That(PathData, Does.Not.Contain("C"));
        Assert.That(PathData, Does.Contain("L"));
    }

    [Test]
    public void HorizontalEndpoints_DropMiddleWaypoints()
    {
        List<(double X, double Y)> Points =
        [
            (10.2, 50.4),
            (40.0, 90.0),
            (80.0, 40.0),
            (160.6, 49.6)
        ];

        bool Collapsed = FlowchartCurvePathBuilder.TryCollapseAxisAlignedEndpoints(
            Points,
            out List<(double X, double Y)> Segment);

        Assert.That(Collapsed, Is.True);
        Assert.That(Segment, Has.Count.EqualTo(2));
        Assert.That(Segment[0].Y, Is.EqualTo(Segment[1].Y));
        Assert.That(Segment[0].X, Is.Not.EqualTo(Segment[1].X));
    }

    [Test]
    public void OffsetEndpoints_KeepTheBasisWaypoints()
    {
        List<(double X, double Y)> Points =
        [
            (10.0, 10.0),
            (40.0, 80.0),
            (90.0, 30.0)
        ];

        bool Collapsed = FlowchartCurvePathBuilder.TryCollapseAxisAlignedEndpoints(
            Points,
            out List<(double X, double Y)> Segment);

        Assert.That(Collapsed, Is.False);
        Assert.That(Segment, Is.Empty);
        string PathData = FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(Points, "basis");
        Assert.That(PathData, Does.Contain("C"));
    }
}
