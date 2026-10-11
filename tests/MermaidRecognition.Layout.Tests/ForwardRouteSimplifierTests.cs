using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ForwardRouteSimplifierTests
{
    [TestCase(FlowchartDirection.TopToBottom)]
    [TestCase(FlowchartDirection.BottomToTop)]
    [TestCase(FlowchartDirection.LeftToRight)]
    [TestCase(FlowchartDirection.RightToLeft)]
    public void ReservedUpstreamCentre_OffsetInputUsesClearNearSideElbow(FlowchartDirection Direction)
    {
        var Context = Create(Direction, "a --> c\nb --> c",
            [("a", 240, 0), ("b", 0, -180), ("c", 0, -360)]);
        var Straight = Add(Context, "b", "c", [(0, -210), (0, -330)]);
        var Offset = Add(Context, "a", "c", [(240, -30), (240, -270), (20, -270), (20, -330)]);
        var Original = Straight.Points.Select(Point => (Point.X, Point.Y)).ToArray();
        Apply(Context, Direction);
        Assert.That(Offset.Points, Has.Count.EqualTo(3));
        var Start = Context.Unmap(Offset.Points[0].X, Offset.Points[0].Y);
        var End = Context.Unmap(Offset.Points[^1].X, Offset.Points[^1].Y);
        Assert.That(Start.Y, Is.EqualTo(-30));
        Assert.That(End.X, Is.EqualTo(60));
        Assert.That(End.Y, Is.EqualTo(-360));
        Assert.That(Straight.Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Original));
        AssertClear(Context);
    }

    [TestCase(FlowchartDirection.TopToBottom)]
    [TestCase(FlowchartDirection.BottomToTop)]
    [TestCase(FlowchartDirection.LeftToRight)]
    [TestCase(FlowchartDirection.RightToLeft)]
    public void OccupiedCentreAndLongerBypass_ShortBranchUsesClearSideElbow(FlowchartDirection Direction)
    {
        var Context = Create(Direction, "a --> b\na --> c\na --> d\nb --> d",
            [("a", 0, 0), ("b", 0, -180), ("c", 240, -180), ("d", 0, -540)]);
        var Straight = Add(Context, "a", "b", [(0, -30), (0, -150)]);
        var Short = Add(Context, "a", "c", [(20, -30), (20, -90), (240, -90), (240, -150)]);
        Add(Context, "a", "d", [(60, 0), (100, 0), (100, -540), (60, -540)]);
        var Original = Straight.Points.Select(Point => (Point.X, Point.Y)).ToArray();
        Apply(Context, Direction);
        Assert.That(Short.Points, Has.Count.EqualTo(3));
        Assert.That(Straight.Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Original));
        AssertClear(Context);
    }

    [TestCase(FlowchartDirection.TopToBottom)]
    [TestCase(FlowchartDirection.BottomToTop)]
    [TestCase(FlowchartDirection.LeftToRight)]
    [TestCase(FlowchartDirection.RightToLeft)]
    public void CoRankedChildren_KeepDownstreamPortsAndRemoveWrongSideHook(FlowchartDirection Direction)
    {
        var Context = Create(Direction, "a --> b\na --> c\na --> d",
            [("a", 0, 0), ("b", 0, -180), ("c", -240, -180), ("d", 240, -180)]);
        Add(Context, "a", "b", [(0, -30), (0, -150)]);
        var Hook = Add(Context, "a", "c", [(60, 0), (100, 0), (100, -90), (-240, -90), (-240, -150)]);
        Add(Context, "a", "d", [(20, -30), (20, -120), (240, -120), (240, -150)]);
        Apply(Context, Direction);
        Assert.That(Hook.Points, Has.Count.EqualTo(4));
        var Port = Context.Unmap(Hook.Points[0].X, Hook.Points[0].Y);
        Assert.That(Port.Y, Is.EqualTo(-30));
        Assert.That(Port.X, Is.LessThan(0));
        AssertClear(Context);
    }

    [TestCase(FlowchartDirection.TopToBottom)]
    [TestCase(FlowchartDirection.BottomToTop)]
    [TestCase(FlowchartDirection.LeftToRight)]
    [TestCase(FlowchartDirection.RightToLeft)]
    public void ForwardBypassAroundOccupiedChain_UsesExteriorNormalPorts(FlowchartDirection Direction)
    {
        var Context = Create(Direction, "a --> b\nb --> c\nc --> d\na --> d",
            [("a", 0, 0), ("b", 0, -180), ("c", 0, -360), ("d", 0, -540)]);
        Add(Context, "a", "b", [(0, -30), (0, -150)]);
        Add(Context, "b", "c", [(0, -210), (0, -330)]);
        Add(Context, "c", "d", [(0, -390), (0, -510)]);
        var Bypass = Add(Context, "a", "d", [(20, -30), (20, -90), (100, -90), (100, -540), (60, -540)]);
        Apply(Context, Direction);
        Assert.That(Bypass.Points, Has.Count.EqualTo(4));
        var Start = Context.Unmap(Bypass.Points[0].X, Bypass.Points[0].Y);
        var End = Context.Unmap(Bypass.Points[^1].X, Bypass.Points[^1].Y);
        Assert.That(Math.Abs(Start.X), Is.EqualTo(60));
        Assert.That(Math.Abs(End.X), Is.EqualTo(60));
        AssertClear(Context);
    }

    private sealed record Context(DiagramContract Contract, FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
        Func<double, double, (double X, double Y)> Map, Func<double, double, (double X, double Y)> Unmap);

    private static Context Create(FlowchartDirection Direction, string Edges, (string Id, double X, double Y)[] Positions)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        double Sign = Direction is FlowchartDirection.BottomToTop or FlowchartDirection.RightToLeft ? -1 : 1;
        (double X, double Y) Map(double X, double Y) => Horizontal ? (-Sign * Y, X) : (X, Sign * Y);
        (double X, double Y) Unmap(double X, double Y) => Horizontal ? (Y, -Sign * X) : (X, Sign * Y);
        var Contract = MermaidRecognizer.Parse("flowchart TB\n" + Edges);
        var Bounds = Positions.ToDictionary(Position => Position.Id, Position =>
        {
            var Point = Map(Position.X, Position.Y);
            return new FlowchartLayoutNodeBounds { NodeId = Position.Id, CenterX = Point.X, CenterY = Point.Y,
                Width = Horizontal ? 60 : 120, Height = Horizontal ? 120 : 60 };
        });
        return new(Contract, new(), Bounds, Map, Unmap);
    }

    private static FlowchartLayoutEdgeRoute Add(Context Context, string Source, string Target, (double X, double Y)[] Points)
    {
        var Route = new FlowchartLayoutEdgeRoute { SourceId = Source, TargetId = Target };
        foreach (var Point in Points)
        {
            var Mapped = Context.Map(Point.X, Point.Y);
            Route.Points.Add(new() { X = Mapped.X, Y = Mapped.Y });
        }
        Context.Layout.EdgeRoutesMutable.Add(Route);
        // Derive sides from the normal terminal run rather than fixture names.
        Route.ExitSide = Side(Route.Points[0], Route.Points[1]);
        Route.EntrySide = Side(Route.Points[^1], Route.Points[^2]);
        return Route;
    }

    private static string Side(FlowchartLayoutPoint Port, FlowchartLayoutPoint Next) => Math.Abs(Port.X - Next.X) < 0.01
        ? (Next.Y > Port.Y ? "top" : "bottom") : (Next.X > Port.X ? "right" : "left");

    private static void Apply(Context Context, FlowchartDirection Direction) => FlowchartForwardRouteSimplifier.Apply(
        Context.Layout, Context.Contract, Context.Bounds, Context.Contract.Nodes.ToDictionary(Node => Node.Id), Direction);

    private static void AssertClear(Context Context)
    {
        foreach (var Route in Context.Layout.EdgeRoutes)
        foreach (var Pair in Route.Points.Zip(Route.Points.Skip(1)))
        {
            Assert.That(Math.Min(Math.Abs(Pair.First.X - Pair.Second.X), Math.Abs(Pair.First.Y - Pair.Second.Y)), Is.LessThan(0.01));
            foreach (var Box in Context.Bounds.Values)
                Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box), Is.False);
        }
        foreach (var A in Context.Layout.EdgeRoutes)
        foreach (var B in Context.Layout.EdgeRoutes.Where(B => B != A))
        {
            var Crossings = FlowchartBranchRouteOrder.Intersections(A.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                B.Points.Select(Point => (Point.X, Point.Y)).ToList());
            Assert.That(Crossings.Crossings, Is.Zero);
            Assert.That(Crossings.Overlap, Is.Zero);
        }
    }
}
