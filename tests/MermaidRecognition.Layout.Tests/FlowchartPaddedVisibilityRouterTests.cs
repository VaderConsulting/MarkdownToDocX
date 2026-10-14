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
        for (int Index = 1; Index < Points.Count; Index++)
        {
            bool AxisAligned = Math.Abs(Points[Index].X - Points[Index - 1].X) <= 0.6
                || Math.Abs(Points[Index].Y - Points[Index - 1].Y) <= 0.6;
            Assert.That(AxisAligned, Is.True, "segment is not right-angled at " + Points[Index]);
        }

        foreach ((double X, double Y) Point in Points)
        {
            bool Inside = Math.Abs(Point.X) < 20.0 && Math.Abs(Point.Y) < 20.0;
            Assert.That(Inside, Is.False, "path entered the blocking box at " + Point);
        }
    }

    [Test]
    public void TryRoute_UsesIndexedObstacleChecksForLargeObstacleSets()
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
        for (int Index = 0; Index < 64; Index++)
        {
            Obstacles.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = "distractor-" + Index,
                CenterX = 1000.0,
                CenterY = 1000.0,
                Width = 20.0,
                Height = 20.0
            });
        }

        FlowchartPaddedVisibilityRouter.ResetDiagnostics();
        Assert.That(FlowchartPaddedVisibilityRouter.TryRoute(-80.0, 0.0, 80.0, 0.0,
            Obstacles, out List<(double X, double Y)> Points), Is.True);
        Assert.That(FlowchartPaddedVisibilityRouter.GridPathCallCount, Is.GreaterThan(0));
        for (int Index = 1; Index < Points.Count; Index++)
        {
            Assert.That(Math.Abs(Points[Index].X - Points[Index - 1].X) <= 0.6
                || Math.Abs(Points[Index].Y - Points[Index - 1].Y) <= 0.6, Is.True);
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Points[Index - 1].X, Points[Index - 1].Y, Points[Index].X, Points[Index].Y,
                Obstacles[0]), Is.False);
        }
    }

    [Test]
    public void EnsureOrthogonal_TurnsADiagonalIntoRightAngles()
    {
        List<(double X, double Y)> Orthogonal = FlowchartPaddedVisibilityRouter.EnsureOrthogonal(
            [(-10.0, 10.0), (10.0, -10.0)]);
        Assert.That(Orthogonal.Count, Is.GreaterThanOrEqualTo(3));
        for (int Index = 1; Index < Orthogonal.Count; Index++)
        {
            bool AxisAligned = Math.Abs(Orthogonal[Index].X - Orthogonal[Index - 1].X) <= 0.6
                || Math.Abs(Orthogonal[Index].Y - Orthogonal[Index - 1].Y) <= 0.6;
            Assert.That(AxisAligned, Is.True);
        }
    }
}
