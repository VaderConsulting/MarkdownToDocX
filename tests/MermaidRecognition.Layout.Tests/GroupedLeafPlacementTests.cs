using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class GroupedLeafPlacementTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void NestedLocalInput_AfterFrameRefittingHasAStraightConnector(bool Horizontal)
    {
        var Contract = MermaidRecognizer.Parse($"flowchart {(Horizontal ? "LR" : "TB")}\nsubgraph outer\nexternal\nsubgraph inner\nparent\nleaf\nend\nend\nexternal --> parent\nleaf -->|journal| parent");
        var Raw = new FlowchartLayoutResult();
        foreach (var Item in new[] { (Id: "external", Cross: 150d, Main: 0d),
            (Id: "parent", Cross: 450d, Main: 180d), (Id: "leaf", Cross: 0d, Main: 360d) })
            Raw.NodeBoundsMutable.Add(new() { NodeId = Item.Id,
                CenterX = Horizontal ? Item.Main : Item.Cross, CenterY = Horizontal ? Item.Cross : Item.Main,
                Width = 150, Height = 60 });
        foreach (var EdgeContract in Contract.Edges)
        {
            var Source = Raw.NodeBounds.Single(Node => Node.NodeId == EdgeContract.SourceId);
            var Target = Raw.NodeBounds.Single(Node => Node.NodeId == EdgeContract.TargetId);
            var SeedRoute = new FlowchartLayoutEdgeRoute { EdgeId = EdgeContract.Id!, SourceId = EdgeContract.SourceId, TargetId = EdgeContract.TargetId };
            SeedRoute.Points.Add(new() { X = Source.CenterX, Y = Source.CenterY });
            SeedRoute.Points.Add(new() { X = Target.CenterX, Y = Target.CenterY });
            Raw.EdgeRoutesMutable.Add(SeedRoute);
        }
        var Result = DiagramGridLayout.Apply(Contract, Raw);
        var Leaf = Result.NodeBounds.Single(Node => Node.NodeId == "leaf");
        var Parent = Result.NodeBounds.Single(Node => Node.NodeId == "parent");
        Assert.That(Horizontal ? Leaf.CenterX : Leaf.CenterY, Is.EqualTo(Horizontal ? Parent.CenterX : Parent.CenterY));
        var Edge = Contract.Edges.Single(Edge => Edge.SourceId == "leaf");
        var Route = Result.EdgeRoutes.Single(Route => Route.EdgeId == Edge.Id);
        Assert.That(Route.Points.All(Point => Math.Abs((Horizontal ? Point.X : Point.Y)
            - (Horizontal ? Leaf.CenterX : Leaf.CenterY)) < 0.01), Is.True);
        var Frame = Result.ClusterBounds.Single(Frame => Frame.NodeId == "inner");
        Assert.That(Math.Abs(Leaf.CenterX - Frame.CenterX) + Leaf.Width / 2, Is.LessThanOrEqualTo(Frame.Width / 2 - 30));
        Assert.That(Math.Abs(Leaf.CenterY - Frame.CenterY) + Leaf.Height / 2, Is.LessThanOrEqualTo(Frame.Height / 2 - 30));
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    public void LocalLeafBesideBoundaryPath_AlignsAndReservesClearLabelSpace(bool Horizontal, bool Reverse, bool Blocked)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph outer\nexternal\nsubgraph inner\nparent\nleaf\nobstacle\nend\nend\n"
            + (Reverse ? "parent --> external\nparent -->|information| leaf" : "external --> parent\nleaf -->|information| parent"));
        DiagramGridLayout.GridNode Node(string Id, double Cross, double Main, string? Group) => new()
        {
            Id = Id, GroupId = Group, X = Horizontal ? Main : Cross, Y = Horizontal ? Cross : Main,
            Width = Horizontal ? 60 : 150, Height = Horizontal ? 150 : 60, Rank = Main == 150 ? 1 : 2
        };
        var Parent = Node("parent", 300, 150, "inner");
        var Leaf = Node("leaf", 0, 300, "inner");
        var Nodes = new List<DiagramGridLayout.GridNode> { Parent, Leaf, Node("external", 300, -150, "outer") };
        var Frame = new DiagramGridLayout.GridFrame { Id = "inner", Left = Horizontal ? 90 : -105,
            Right = Horizontal ? 360 : 405, Top = Horizontal ? -105 : 90, Bottom = Horizontal ? 405 : 360 };
        if (Blocked) Nodes.Add(Node("obstacle", 90, 150, "inner"));
        Assert.That(FlowchartGroupedLeafPlacement.Apply(Contract, Nodes, new() { Frame }, Horizontal), Is.True);
        Assert.That(Horizontal ? Leaf.X : Leaf.Y, Is.EqualTo(150));
        double Cross = Horizontal ? Leaf.Y : Leaf.X;
        Assert.That(Math.Sign(Cross - 300), Is.EqualTo(Blocked ? 1 : -1));
        Assert.That(Math.Abs(Cross - 300) - 150, Is.GreaterThanOrEqualTo(30));
        Assert.That(Leaf.Rank, Is.EqualTo(Parent.Rank));
        Assert.That(Leaf.GroupId, Is.EqualTo("inner"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GroupedEndpointOfUnbranchedBoundaryPath_KeepsItsPlacement(bool Reverse)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nexternal\nsubgraph inner\nparent\nleaf\nend\n"
            + (Reverse ? "parent --> external\nleaf --> parent" : "external --> parent\nparent --> leaf"));
        var Leaf = new DiagramGridLayout.GridNode { Id = "leaf", GroupId = "inner", Y = 300, Width = 150, Height = 60 };
        var Nodes = new List<DiagramGridLayout.GridNode> { Leaf,
            new() { Id = "parent", GroupId = "inner", X = 300, Y = 150, Width = 150, Height = 60 },
            new() { Id = "external", X = 300, Y = -150, Width = 150, Height = 60 } };
        Assert.That(FlowchartGroupedLeafPlacement.Apply(Contract, Nodes, new(), false), Is.False);
        Assert.That((Leaf.X, Leaf.Y), Is.EqualTo((0d, 300d)));
    }
}
