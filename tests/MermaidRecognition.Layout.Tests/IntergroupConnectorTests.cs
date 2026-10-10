using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class IntergroupConnectorTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void OutsideConnectorChain_UsesTheSharedGapAndKeepsClearance(bool Horizontal, bool Blocked)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph left\nsubgraph innerA\na\nend\nend\nsubgraph right\nsubgraph innerB\nb\nend\nend\ninnerA --> service\ninnerB --> service\nservice --> consumer\nb --> consumer\nlink --- innerA\nlink --- innerB");
        var Nodes = new List<DiagramGridLayout.GridNode>();
        DiagramGridLayout.GridNode Add(string Id, double X, double Y, string? Group = null, int Width = 120, int Height = 60)
        {
            var Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = Group,
                X = Horizontal ? Y : X, Y = Horizontal ? X : Y,
                Width = Horizontal ? Height : Width, Height = Horizontal ? Width : Height };
            Nodes.Add(Node); return Node;
        }
        var Frames = new List<DiagramGridLayout.GridFrame>();
        void Frame(string Id, int Left, int Top, int Right, int Bottom) => Frames.Add(new()
        { Id = Id, Left = Horizontal ? Top : Left, Right = Horizontal ? Bottom : Right,
            Top = Horizontal ? Left : Top, Bottom = Horizontal ? Right : Bottom });
        Frame("left", 0, 0, 300, 960); Frame("right", 600, 0, 900, 960);
        Frame("innerA", 60, 120, 240, 360); Frame("innerB", 660, 120, 840, 360);
        Add("a", 150, 240, "innerA"); Add("b", 750, 240, "innerB");
        var Service = Add("service", 180, 1200);
        var Consumer = Add("consumer", 450, 1350);
        var Link = Add("link", 450, -120);
        if (Blocked) Add("obstacle", 450, 480, null, 300, 960);
        var Original = Nodes.Select(Node => (Node.X, Node.Y)).ToArray();
        bool Changed = FlowchartIntergroupNodePlacement.Apply(Contract, Nodes, Frames, Horizontal);
        Assert.That(Changed, Is.EqualTo(!Blocked));
        if (Blocked) { Assert.That(Nodes.Select(Node => (Node.X, Node.Y)), Is.EqualTo(Original)); return; }
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Main(DiagramGridLayout.GridNode Node) => Horizontal ? Node.X : Node.Y;
        Assert.That(Cross(Service), Is.EqualTo(390));
        Assert.That(Main(Service), Is.EqualTo(420));
        Assert.That(Main(Consumer), Is.EqualTo(510));
        Assert.That(Main(Link), Is.EqualTo(180));
        foreach (var Node in new[] { Service, Consumer, Link })
        {
            Assert.That(Cross(Node), Is.InRange(390, 510));
            Assert.That(Main(Node) % 30, Is.Zero);
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void CrossGroupRoute_UsesFreeFacingPortsAndAvoidsObstacles(bool Transpose, bool Blocked)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph first\na\nend\nsubgraph second\nb\nend\na --> b");
        (double X, double Y) Map(double X, double Y) => Transpose ? (Y, X) : (X, Y);
        FlowchartLayoutNodeBounds Box(string Id, double X, double Y, double Width, double Height)
        {
            var P = Map(X, Y);
            return new() { NodeId = Id, CenterX = P.X, CenterY = P.Y,
                Width = Transpose ? Height : Width, Height = Transpose ? Width : Height };
        }
        var Bounds = new[] { Box("first", 0, 0, 240, 300), Box("second", 600, 0, 240, 300),
            Box("a", 0, 0, 120, 60), Box("b", 600, 0, 120, 60) }.ToDictionary(Box => Box.NodeId);
        if (Blocked) Bounds.Add("obstacle", Box("obstacle", 300, 0, 180, 60));
        var Layout = new FlowchartLayoutResult();
        var Edge = Contract.Edges.Single();
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
        foreach (var Point in new[] { (0d, -30d), (0d, -200d), (600d, -200d), (600d, -30d) })
        { var P = Map(Point.Item1, Point.Item2); Route.Points.Add(new() { X = P.X, Y = P.Y }); }
        Layout.EdgeRoutesMutable.Add(Route);
        FlowchartCrossGroupRouteSimplifier.Apply(Layout, Contract, Bounds);
        if (!Blocked) Assert.That(Route.Points, Has.Count.EqualTo(2));
        foreach (var Pair in Route.Points.Zip(Route.Points.Skip(1)))
        foreach (var Obstacle in Bounds.Values.Where(Box => Box.NodeId is not ("first" or "second")))
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Obstacle), Is.False);
    }
}
