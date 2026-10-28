namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class RetainedRouteDependencyIndexTests
{
    [Test]
    public void IndexValuesRemainAtTheirCapturedRevisionWhenLiveRoutesChange()
    {
        var Layout = new FlowchartLayoutResult();
        var Route = Add(Layout, "line", "a", "b", (0, 0), (100, 0));
        var Loop = Add(Layout, "loop", "a", "a", (0, 0), (0, 100));
        var Parallel = Add(Layout, "parallel", "a", "b", (0, 20), (100, 20));
        var Index = new RetainedRouteDependencyIndex(Layout);
        Route.SourceId = "x"; Route.TargetId = "y";
        Route.Points.Clear(); Route.Points.AddRange(new[] { (1000d, 1000d), (1100d, 1000d) }
            .Select(Point => new FlowchartLayoutPoint { X = Point.Item1, Y = Point.Item2 }));
        Assert.That(Index.Incident("a"), Is.EquivalentTo(new[] { Route, Loop, Parallel }));
        Assert.That(Index.Incident("x"), Is.Empty);
        Assert.That(Index.Near(Box(50, 0, 10, 10), 0), Does.Contain(Route));
        Assert.That(Index.Near(Box(1050, 1000, 10, 10), 0), Is.Empty);
        var Next = new RetainedRouteDependencyIndex(Layout);
        Assert.That(Next.Incident("x"), Is.EqualTo(new[] { Route }));
        Assert.That(Next.Near(Box(1050, 1000, 10, 10), 0), Is.EqualTo(new[] { Route }));
    }

    [Test]
    public void BoundaryTouchesAndVeryLargeNegativeCoordinatesNeedNoSpatialBuckets()
    {
        var Layout = new FlowchartLayoutResult();
        var Far = Add(Layout, "far", "a", "b", (-1e12, -1e12), (1e12, -1e12));
        var Index = new RetainedRouteDependencyIndex(Layout);
        Assert.That(Index.Near(Box(-1e12 - 5, -1e12, 10, 10), 0), Is.EqualTo(new[] { Far }));
        Assert.That(Index.Near(Box(1e12 + 20, -1e12, 10, 10), 15), Is.EqualTo(new[] { Far }));
        Assert.That(Index.Near(Box(1e12 + 21, -1e12, 10, 10), 15), Is.Empty);
        Assert.That(Index.Near(Box(0, -1e12 + 21, 10, 10), 15), Is.Empty);
    }

    [Test]
    public void ConservativeQueriesNeverMissBruteForceSegmentIntersections()
    {
        var Random = new Random(14731);
        var Layout = new FlowchartLayoutResult();
        for (int Index = 0; Index < 150; Index++)
        {
            double X = Random.Next(-10000, 10001), Y = Random.Next(-10000, 10001);
            Add(Layout, "r" + Index, "s" + Index, "t" + Index, (X, Y),
                (X + Random.Next(-1000, 1001), Y), (X + Random.Next(-1000, 1001), Y + Random.Next(-1000, 1001)));
        }
        // A malformed baseline path must not poison the finite interval prefix.
        Add(Layout, "invalid", "bad", "bad", (double.NaN, double.NaN), (double.NaN, double.NaN));
        var Spatial = new RetainedRouteDependencyIndex(Layout);
        int Hits = 0;
        for (int Query = 0; Query < 100; Query++)
        {
            var Bounds = Box(Random.Next(-10000, 10001), Random.Next(-10000, 10001), Random.Next(1, 2000), Random.Next(1, 2000));
            const double Margin = 20;
            var Expected = Layout.EdgeRoutes.Where(Route => Route.Points.Zip(Route.Points.Skip(1)).Any(Pair =>
                Math.Min(Pair.First.X, Pair.Second.X) <= Bounds.CenterX + Bounds.Width / 2 + Margin
                && Math.Max(Pair.First.X, Pair.Second.X) >= Bounds.CenterX - Bounds.Width / 2 - Margin
                && Math.Min(Pair.First.Y, Pair.Second.Y) <= Bounds.CenterY + Bounds.Height / 2 + Margin
                && Math.Max(Pair.First.Y, Pair.Second.Y) >= Bounds.CenterY - Bounds.Height / 2 - Margin)).ToArray();
            Assert.That(Expected, Is.SubsetOf(Spatial.Near(Bounds, Margin)), "query " + Query);
            Hits += Expected.Length;
        }
        Assert.That(Hits, Is.GreaterThan(10), "the sampled rectangles must exercise actual path intersections");
    }

    private static FlowchartLayoutNodeBounds Box(double X, double Y, double Width, double Height)
        => new() { NodeId = "obstacle", CenterX = X, CenterY = Y, Width = Width, Height = Height };
    private static FlowchartLayoutEdgeRoute Add(FlowchartLayoutResult Layout, string Id, string Source, string Target, params (double X, double Y)[] Points)
    {
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = Source, TargetId = Target };
        Route.Points.AddRange(Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
        Layout.EdgeRoutesMutable.Add(Route); return Route;
    }
}
