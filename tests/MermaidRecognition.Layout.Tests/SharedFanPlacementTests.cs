using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class SharedFanPlacementTests
{
    [TestCase("TB", "bottom", "top")]
    [TestCase("BT", "top", "bottom")]
    [TestCase("LR", "right", "left")]
    [TestCase("RL", "left", "right")]
    public void CentredSharedFan_UsesFreeFlowFacingPortsAndTwoBends(string Direction, string Exit, string Entry)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\na --> b\na --> c\nd --> b\ne --> c\nc --> f");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        List<FlowchartLayoutEdgeRoute> Routes = [.. Layout.EdgeRoutes.Where(Route => Route.SourceId == "a")];
        Assert.That(Routes, Has.Count.EqualTo(2));
        foreach (FlowchartLayoutEdgeRoute? Route in Routes)
        {
            Assert.That(Route.ExitSide, Is.EqualTo(Exit));
            Assert.That(Route.EntrySide, Is.EqualTo(Entry));
            Assert.That(Route.Points.Count, Is.LessThanOrEqualTo(4));
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void SharedFanWithIndependentInputs_CentresSourceAndAlignsContinuation(bool Horizontal, bool Mirrored)
    {
        int Sign = Mirrored ? -1 : 1;
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\na --> b\na --> c\nd --> b\ne --> c\nc --> f");
        DiagramGridLayout.GridNode Node(string Id, int Rank, int Cross) => new()
        {
            Id = Id, Rank = Rank, Width = 120, Height = 60,
            X = Horizontal ? Rank * 210 : Sign * Cross,
            Y = Horizontal ? Sign * Cross : Rank * 150
        };
        DiagramGridLayout.GridNode Source = Node("a", 0, 0);
        DiagramGridLayout.GridNode Left = Node("b", 1, 180);
        DiagramGridLayout.GridNode Right = Node("c", 1, 540);
        DiagramGridLayout.GridNode InputLeft = Node("d", 0, 180);
        DiagramGridLayout.GridNode InputRight = Node("e", 0, 540);
        DiagramGridLayout.GridNode Tail = Node("f", 2, 360);
        List<DiagramGridLayout.GridNode> Nodes = [Source, Left, Right, InputLeft, InputRight, Tail];
        DiagramGridLayout.AlignSharedFanSourcesAndTails(Contract, Nodes, Horizontal);
        double CrossOf(DiagramGridLayout.GridNode Item) => Horizontal ? Item.Y : Item.X;
        double MainOf(DiagramGridLayout.GridNode Item) => Horizontal ? Item.X : Item.Y;
        Assert.That(CrossOf(Source), Is.EqualTo((CrossOf(Left) + CrossOf(Right)) / 2));
        Assert.That(CrossOf(Tail), Is.EqualTo(CrossOf(Right)));
        Assert.That(CrossOf(InputLeft), Is.EqualTo(Sign * 180));
        Assert.That(CrossOf(InputRight), Is.EqualTo(Sign * 540));
        Assert.That(MainOf(Source), Is.Zero);
        Assert.That(MainOf(Tail), Is.EqualTo(Horizontal ? 420 : 300));
    }

    [Test]
    public void OccupiedDestination_PreservesExistingNodeClearance()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\na --> b\na --> c\nd --> b\ne --> c\nc --> f");
        DiagramGridLayout.GridNode Node(string Id, int Rank, int X) => new()
            { Id = Id, Rank = Rank, X = X, Y = Rank * 150, Width = 120, Height = 60 };
        DiagramGridLayout.GridNode Source = Node("a", 0, 0);
        DiagramGridLayout.GridNode Tail = Node("f", 2, 360);
        List<DiagramGridLayout.GridNode> Nodes = [ Source, Node("b",1,180), Node("c",1,540),
            Node("d",0,180), Node("e",0,540), Tail, Node("obstacle",0,360), Node("other",2,540) ];
        DiagramGridLayout.AlignSharedFanSourcesAndTails(Contract, Nodes, false);
        Assert.That(Source.X, Is.Zero);
        Assert.That(Tail.X, Is.EqualTo(360));
    }
}
