using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartBranchRouteOrderTests
{
    [TestCase(false, false, false, true)]
    [TestCase(true, false, false, true)]
    [TestCase(false, true, false, true)]
    [TestCase(true, true, false, true)]
    [TestCase(false, false, true, true)]
    [TestCase(true, false, true, true)]
    [TestCase(false, false, false, false)]
    [TestCase(true, true, false, false)]
    public void CrossedFanPorts_AreSwappedOnlyWhenOtherRoutesAndAllocatedFacesRemainClear(bool Rotate, bool Incoming, bool BlockingRoute, bool Circle)
    {
        var Contract = new DiagramContract { Direction = Rotate ? "LR" : "TB" };
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>();
        AddNode("hub", 0, 300, Circle ? 60 : 100, 60);
        Contract.NodesMutable[0].Shape = Circle ? "circle" : "rect";
        AddNode("near", 200, 0, 100, 60);
        AddNode("far", 400, -200, 100, 60);
        var Layout = new FlowchartLayoutResult();
        AddRoute("first", "near", "right", [(Circle ? 30 : 50, 300), (200, 300), (200, 30)]);
        AddRoute("second", "far", "bottom", [(0, 270), (0, 150), (400, 150), (400, -170)]);
        if (BlockingRoute)
        {
            var Blocker = new FlowchartLayoutEdgeRoute { SourceId = "externalStart", TargetId = "externalEnd" };
            foreach (var Point in new[] { Map(250, 230), Map(450, 230) }) Blocker.Points.Add(new() { X = Point.X, Y = Point.Y });
            Layout.EdgeRoutesMutable.Add(Blocker);
        }
        var Routes = Layout.EdgeRoutes.Take(2).ToList();
        Assert.That(FlowchartBranchRouteOrder.Intersections(Path(Routes[0]), Path(Routes[1])).Crossings, Is.GreaterThan(0));
        var Identities = Routes.Select(Route => (Route.EdgeId, Route.SourceId, Route.TargetId)).ToArray();
        var FixedEnds = Routes.Select(Route => Incoming ? Path(Route)[0] : Path(Route)[^1]).ToArray();
        var Ports = Routes.Select(Route => Incoming ? Path(Route)[^1] : Path(Route)[0]).ToArray();
        FlowchartBranchRouteOrder.RemoveAvoidableCrossings(Layout, Contract, Bounds,
            Contract.Nodes.ToDictionary(Node => Node.Id), Rotate ? FlowchartDirection.LeftToRight : FlowchartDirection.TopToBottom);
        Assert.That(FlowchartBranchRouteOrder.Intersections(Path(Routes[0]), Path(Routes[1])).Crossings,
            BlockingRoute || !Circle ? Is.GreaterThan(0) : Is.EqualTo(0));
        Assert.That(Routes.Select(Route => (Route.EdgeId, Route.SourceId, Route.TargetId)), Is.EqualTo(Identities));
        Assert.That(Routes.Select(Route => Incoming ? Path(Route)[0] : Path(Route)[^1]), Is.EqualTo(FixedEnds));
        Assert.That(Routes.Select(Route => Incoming ? Path(Route)[^1] : Path(Route)[0]), Is.EqualTo(BlockingRoute || !Circle ? Ports : Ports.Reverse()));

        (double X, double Y) Map(double X, double Y) => Rotate ? (Y, X) : (X, Y);
        void AddNode(string Id, double X, double Y, double Width, double Height)
        {
            var Centre = Map(X, Y);
            Contract.NodesMutable.Add(new() { Id = Id, Shape = "rect" });
            Bounds[Id] = new() { NodeId = Id, CenterX = Centre.X, CenterY = Centre.Y,
                Width = Rotate ? Height : Width, Height = Rotate ? Width : Height };
        }
        void AddRoute(string Id, string Other, string Side, List<(double X, double Y)> Points)
        {
            string RotatedSide = Rotate ? (Side == "right" ? "top" : "left") : Side;
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = Incoming ? Other : "hub",
                TargetId = Incoming ? "hub" : Other, ExitSide = Incoming ? (Rotate ? "right" : "top") : RotatedSide,
                EntrySide = Incoming ? RotatedSide : (Rotate ? "right" : "top") };
            if (Incoming) Points.Reverse();
            foreach (var Point in Points) { var P = Map(Point.X, Point.Y); Route.Points.Add(new() { X = P.X, Y = P.Y }); }
            Layout.EdgeRoutesMutable.Add(Route);
            Contract.EdgesMutable.Add(new() { Id = Id, SourceId = Route.SourceId, TargetId = Route.TargetId });
        }
    }

    private static List<(double X, double Y)> Path(FlowchartLayoutEdgeRoute Route) => Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
}
