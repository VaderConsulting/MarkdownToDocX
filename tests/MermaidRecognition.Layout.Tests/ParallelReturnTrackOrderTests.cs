using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ParallelReturnTrackOrderTests
{
    [TestCase(FlowchartDirection.TopToBottom, 3, false)]
    [TestCase(FlowchartDirection.BottomToTop, 3, false)]
    [TestCase(FlowchartDirection.LeftToRight, 3, false)]
    [TestCase(FlowchartDirection.RightToLeft, 3, false)]
    [TestCase(FlowchartDirection.TopToBottom, 8, false)]
    [TestCase(FlowchartDirection.LeftToRight, 8, false)]
    [TestCase(FlowchartDirection.TopToBottom, 3, true)]
    [TestCase(FlowchartDirection.LeftToRight, 3, true)]
    public void ParallelReturns_NestPortsAndTracksWithoutCrossings(FlowchartDirection Direction, int Count, bool Blocked)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        double Sign = Direction is FlowchartDirection.BottomToTop or FlowchartDirection.RightToLeft ? -1 : 1;
        (double X, double Y) Map(double X, double Y) => Horizontal ? (-Sign * Y, X) : (X, Sign * Y);
        var Source = "flowchart TB\nfirst --> second\n" + string.Join('\n', Enumerable.Range(0, Count).Select(Index => $"second -->|return {Index}| first"));
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new[] { ("first", 0d), ("second", -300d) }.ToDictionary(Item => Item.Item1, Item =>
        {
            (double X, double Y) Point = Map(0, Item.Item2);
            return new FlowchartLayoutNodeBounds { NodeId = Item.Item1, CenterX = Point.X, CenterY = Point.Y,
                Width = Horizontal ? 60 : 120, Height = Horizontal ? 120 : 60 };
        });
        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        for (int EdgeIndex = 0; EdgeIndex < Contract.Edges.Count; EdgeIndex++)
        {
            EdgeContract Edge = Contract.Edges[EdgeIndex];
            bool Return = Edge.SourceId == "second";
            int Index = EdgeIndex - 1;
            double Offset = (Index - ((Count - 1) / 2d)) * 6;
            (double, double)[] Points = Return
                ? [(60d, -300 + Offset), (100 + (Index * 60d), -300 + Offset), (100 + (Index * 60d), Offset), (60d, Offset)]
                : [(0d, -30d), (0d, -270d)];
            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId,
                ExitSide = Return ? Horizontal ? "top" : "right" : Horizontal ? "right" : "bottom",
                EntrySide = Return ? Horizontal ? "top" : "right" : Horizontal ? "left" : "top" };
            foreach ((double, double) Point in Points) { (double X, double Y) P = Map(Point.Item1, Point.Item2); Route.Points.Add(new() { X = P.X, Y = P.Y }); }
            Layout.EdgeRoutesMutable.Add(Route);
        }
        if (Blocked)
        {
            (double X, double Y) Point = Map(100, -150);
            Bounds.Add("obstacle", new() { NodeId = "obstacle", CenterX = Point.X, CenterY = Point.Y, Width = 30, Height = 30 });
        }
        (double X, double Y)[][] Original = [.. Layout.EdgeRoutes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())];
        DiagramGridLayout.SeparateParallelReturnTracks(Contract, Layout, Bounds);
        if (Blocked)
        {
            for (int Index = 0; Index < Original.Length; Index++)
            {
                Assert.That(Layout.EdgeRoutes[Index].Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Original[Index]));
            }

            return;
        }
        Assert.That(Layout.EdgeRoutes[0].Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Original[0]));
        for (int First = 0; First < Layout.EdgeRoutes.Count; First++)
        {
            List<(double X, double Y)> Points = [.. Layout.EdgeRoutes[First].Points.Select(Point => (Point.X, Point.Y))];
            for (int Second = First + 1; Second < Layout.EdgeRoutes.Count; Second++)
            {
                List<(double X, double Y)> Other = [.. Layout.EdgeRoutes[Second].Points.Select(Point => (Point.X, Point.Y))];
                (int Crossings, double Overlap) Intersections = FlowchartBranchRouteOrder.Intersections(Points, Other);
                Assert.That(Intersections.Crossings, Is.Zero);
                Assert.That(Intersections.Overlap, Is.Zero);
            }
            foreach (((double X, double Y) First, (double X, double Y) Second) Pair in Points.Zip(Points.Skip(1)))
            {
                foreach (FlowchartLayoutNodeBounds? Box in Bounds.Values)
                {
                    Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y,
                    Pair.Second.X, Pair.Second.Y, Box), Is.False);
                }
            }
        }
    }
}
