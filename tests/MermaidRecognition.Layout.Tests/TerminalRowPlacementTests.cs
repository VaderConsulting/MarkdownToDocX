using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class TerminalRowPlacementTests
{
    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    public void TerminalBranch_FillsOnlyAnUnoccupiedSlot(bool Horizontal, bool Mirrored, bool Blocked)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nparent --> leaf\nparent --> collector\npeer --> collector\na --> b");
        int Sign = Mirrored ? -1 : 1;
        var Nodes = new List<DiagramGridLayout.GridNode>();
        DiagramGridLayout.GridNode Add(string Id, int Rank, int Cross)
        {
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank,
                X = Horizontal ? Rank * 150 : Cross * Sign, Y = Horizontal ? Cross * Sign : Rank * 150,
                Width = Horizontal ? 60 : 150, Height = Horizontal ? 150 : 60 };
            Nodes.Add(Node); return Node;
        }
        var Parent = Add("parent", 1, 360); var Leaf = Add("leaf", 2, 150);
        Add("peer", 1, 570); Add("collector", 2, 570);
        Add("a", 0, 0); Add("b", 1, 0);
        if (Blocked) Add("obstacle", 1, 180);
        FlowchartTerminalRowPlacement.Apply(Contract, Nodes, Horizontal);
        Assert.That(Leaf.Rank, Is.EqualTo(Blocked ? 2 : 1));
        if (!Blocked)
        {
            Assert.That(Horizontal ? Leaf.X : Leaf.Y, Is.EqualTo(Horizontal ? Parent.X : Parent.Y));
            Assert.That(Horizontal ? Leaf.Y : Leaf.X, Is.EqualTo(Sign * 180));
        }
    }

    [Test]
    public void EnterpriseTerminalBranch_AlignsBetweenTheDisconnectedPairAndItsParent()
    {
        var Document = EnterpriseValidator.Analyze("""
            enterprise
            business-role leaf "Leaf"
            business-process collector "Collector"
            business-object lower "Lower"
            application-service parent "Parent"
            application-process peer "Peer"
            application-component root "Root"
            data-object upper "Upper"
            parent -serving-> collector
            parent -serving-> leaf
            peer -realization-> collector
            upper -realization-> lower
            root -realization-> parent
            root -assignment-> peer
            """).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Leaf = Layout.Nodes["leaf"]; var Parent = Layout.Nodes["parent"]; var Lower = Layout.Nodes["lower"];
        Assert.That(Leaf.AbsoluteY, Is.EqualTo(Parent.AbsoluteY));
        Assert.That(Leaf.AbsoluteY, Is.EqualTo(Lower.AbsoluteY));
        Assert.That(Leaf.AbsoluteX - (Lower.AbsoluteX + Lower.Width), Is.GreaterThanOrEqualTo(30));
        Assert.That(Parent.AbsoluteX - (Leaf.AbsoluteX + Leaf.Width), Is.GreaterThanOrEqualTo(30));
        var Edges = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        Assert.That(Edges.Single(Edge => Edge.Relationship.TargetId == "leaf").SvgPoints, Has.Count.EqualTo(2));
        foreach (var Edge in Edges)
        {
            foreach (var Other in Edges.Where(Other => Other != Edge))
                Assert.That(FlowchartBranchRouteOrder.Intersections(Edge.SvgPoints, Other.SvgPoints).Crossings, Is.Zero);
            foreach (var Segment in Edge.SvgPoints.Zip(Edge.SvgPoints.Skip(1)))
            foreach (var Node in Layout.Nodes.Values)
                Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X,
                    Segment.First.Y, Segment.Second.X, Segment.Second.Y, new FlowchartLayoutNodeBounds
                    { NodeId = Node.Id, CenterX = Node.AbsoluteX + Node.Width / 2,
                        CenterY = Node.AbsoluteY + Node.Height / 2, Width = Node.Width, Height = Node.Height }), Is.False);
        }
    }
}
