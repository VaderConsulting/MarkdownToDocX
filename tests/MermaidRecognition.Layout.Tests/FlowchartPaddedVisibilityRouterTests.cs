using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Visibility-graph shortest path around a blocking box.
/// </summary>
[TestFixture]
public sealed class FlowchartPaddedVisibilityRouterTests
{
    [Test]
    public void TryRoute_GoesAroundABlockingBox()
    {
        List<FlowchartLayoutNodeBounds> Obstacles =
        [
            new FlowchartLayoutNodeBounds
            {
                NodeId = "block",
                CenterX = 0.0,
                CenterY = 0.0,
                Width = 40.0,
                Height = 40.0
            }
        ];

        Assert.That(
            FlowchartPaddedVisibilityRouter.TryRoute(
                -80.0,
                0.0,
                80.0,
                0.0,
                Obstacles,
                out List<(double X, double Y)> Points),
            Is.True);
        Assert.That(Points.Count, Is.GreaterThanOrEqualTo(3));
        foreach ((double X, double Y) Point in Points)
        {
            bool Inside = Math.Abs(Point.X) < 20.0 && Math.Abs(Point.Y) < 20.0;
            Assert.That(Inside, Is.False, "path entered the blocking box at " + Point);
        }
    }
}
