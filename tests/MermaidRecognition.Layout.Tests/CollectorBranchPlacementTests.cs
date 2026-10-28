using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class CollectorBranchPlacementTests
{
    [TestCase(false, false, 1, false)]
    [TestCase(false, true, 1, false)]
    [TestCase(true, false, 1, false)]
    [TestCase(true, true, 1, false)]
    [TestCase(false, false, 6, false)]
    [TestCase(true, false, 6, false)]
    [TestCase(false, false, 1, true)]
    [TestCase(true, false, 1, true)]
    public void TerminalBranches_UseFreeColumnsOppositeTheSharedCollector(bool Horizontal, bool Mirrored, int Count, bool Blocked)
    {
        int Sign = Mirrored ? -1 : 1;
        string Source = "flowchart TB\nroot --> parent\nroot --> peer\nparent --> collector\npeer --> collector\n";
        List<DiagramGridLayout.GridNode> Nodes = [];
        DiagramGridLayout.GridNode Add(string Id, int Rank, double Cross)
        {
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank,
                X = Horizontal ? Rank * 150 : Cross, Y = Horizontal ? Cross : Rank * 150,
                Width = Horizontal ? 60 : 180, Height = Horizontal ? 180 : 60 };
            Nodes.Add(Node); return Node;
        }
        Add("root", -1, 0); DiagramGridLayout.GridNode Parent = Add("parent", 0, 0);
        Add("peer", 0, Sign * 420); DiagramGridLayout.GridNode Collector = Add("collector", 1, Sign * 420);
        if (Blocked)
        {
            Add("obstacle", 1, -Sign * 240);
        }

        List<DiagramGridLayout.GridNode> Leaves = [];
        for (int Index = 0; Index < Count; Index++)
        {
            Source += $"parent --> leaf{Index}\n";
            Leaves.Add(Add($"leaf{Index}", 1, Sign * (Count + Index + 2) * 420));
        }
        FlowchartTerminalBranchPlacement.Apply(MermaidRecognizer.Parse(Source), Nodes, Horizontal, 210);
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        Assert.That(Cross(Collector), Is.EqualTo(Sign * 420));
        Assert.That(Cross(Parent), Is.Zero);
        Assert.That(Cross(Leaves[0]), Is.EqualTo(-Sign * (Blocked ? 480 : 240)));
        Assert.That(Leaves.All(Leaf => Math.Sign(Cross(Leaf)) == -Sign && Cross(Leaf) % 30 == 0), Is.True);
        var Rank = Nodes.Where(Node => Node.Rank == 1).Select(Cross).OrderBy(Value => Value).ToArray();
        Assert.That(Rank.Zip(Rank.Skip(1)).All(Pair => Pair.Second - Pair.First >= 210), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OffsetTerminalFanBesideCollector_UsesSideBranchAndNearSideRejoin(bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nparent --> collector\nparent --> leaf\npeer --> collector");
        (double X, double Y) Map(double X, double Y) => Horizontal ? (-Y, X) : (X, Y);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new[] { ("parent", 0d, 0d), ("peer", 210d, 0d), ("collector", 210d, -150d), ("leaf", -210d, -150d) }
            .ToDictionary(Item => Item.Item1, Item => {
                (double X, double Y) Point = Map(Item.Item2, Item.Item3);
                return new FlowchartLayoutNodeBounds { NodeId = Item.Item1, CenterX = Point.X, CenterY = Point.Y,
                    Width = Horizontal ? 60 : 150, Height = Horizontal ? 150 : 60 }; });
        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        FlowchartLayoutEdgeRoute Add(int Index, params (double X, double Y)[] Points)
        {
            EdgeContract Edge = Contract.Edges[Index];
            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId,
                ExitSide = Horizontal ? "right" : "bottom", EntrySide = Horizontal ? "left" : "top" };
            foreach ((double X, double Y) Point in Points) { (double X, double Y) Mapped = Map(Point.X, Point.Y); Route.Points.Add(new() { X = Mapped.X, Y = Mapped.Y }); }
            Layout.EdgeRoutesMutable.Add(Route); return Route;
        }
        FlowchartLayoutEdgeRoute Rejoin = Add(0, (0, -30), (0, -75), (185, -75), (185, -120));
        FlowchartLayoutEdgeRoute Side = Add(1, (-25, -30), (-25, -75), (-210, -75), (-210, -120));
        FlowchartLayoutEdgeRoute Straight = Add(2, (210, -30), (210, -120));
        FlowchartForwardRouteSimplifier.Apply(Layout, Contract, Bounds, Contract.Nodes.ToDictionary(Node => Node.Id),
            Horizontal ? FlowchartDirection.LeftToRight : FlowchartDirection.TopToBottom);
        Assert.That(Side.Points, Has.Count.EqualTo(4));
        Assert.That(Rejoin.Points, Has.Count.EqualTo(3));
        Assert.That(Straight.Points, Has.Count.EqualTo(2));
        Assert.That(Side.ExitSide, Is.EqualTo(Horizontal ? "bottom" : "left"));
        Assert.That(Rejoin.ExitSide, Is.EqualTo(Horizontal ? "right" : "bottom"));
        Assert.That(Rejoin.EntrySide, Is.EqualTo(Horizontal ? "bottom" : "left"));
        Assert.That(FlowchartBranchRouteOrder.Intersections(Side.Points.Select(Point => (Point.X, Point.Y)).ToList(),
            Rejoin.Points.Select(Point => (Point.X, Point.Y)).ToList()).Crossings, Is.Zero);
    }
}
