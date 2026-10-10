using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class JunctionBranchCompactionTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void BranchesCloseLocally_WhileAlignedTailsAndBlockedRanksStayClear(bool Horizontal, bool Blocked)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\ns(( )) --> a\ns --> b\ns --> c\na --> j(( ))\nb --> j\nc --> j\nc --> tail");
        var Nodes = new List<DiagramGridLayout.GridNode>();
        DiagramGridLayout.GridNode Add(string Id, double Cross, double Main, bool Circle = false)
        {
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = (int)(Main / 150),
                X = Horizontal ? Main : Cross, Y = Horizontal ? Cross : Main,
                Width = Circle ? 60 : Horizontal ? 60 : 150,
                Height = Circle ? 60 : Horizontal ? 150 : 60, Shape = Circle ? "circle" : "rect" };
            Nodes.Add(Node); return Node;
        }
        Add("s", 0, 0, true); var Left = Add("a", -900, 150);
        var Centre = Add("b", 0, 150); var Right = Add("c", 900, 150);
        Add("j", 0, 300, true); var Tail = Add("tail", 900, 450);
        if (Blocked) Add("obstacle", 210, 450);
        FlowchartJunctionBranchCompaction.Apply(Contract, Nodes, Horizontal);
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        Assert.That(Cross(Left), Is.EqualTo(-210));
        Assert.That(Cross(Centre), Is.Zero);
        Assert.That(Cross(Right), Is.EqualTo(Blocked ? 420 : 210));
        Assert.That(Cross(Tail), Is.EqualTo(Cross(Right)));
        Assert.That(Nodes.All(Node => Cross(Node) % 30 == 0), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TwoWayCollectorRecentresAfterItsBranchMoves(bool Horizontal)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\ns(( )) --> a\ns --> b\na --> j(( ))\nb --> j");
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var Item in new[] { ("s", 0, 0, true), ("a", 0, 150, false), ("b", 600, 150, false), ("j", 300, 300, true) })
            Nodes.Add(new() { Id = Item.Item1, Rank = Item.Item3 / 150,
                X = Horizontal ? Item.Item3 : Item.Item2, Y = Horizontal ? Item.Item2 : Item.Item3,
                Width = Item.Item4 ? 60 : Horizontal ? 60 : 150, Height = Item.Item4 ? 60 : Horizontal ? 150 : 60,
                Shape = Item.Item4 ? "circle" : "rect" });
        FlowchartJunctionBranchCompaction.Apply(Contract, Nodes, Horizontal);
        double Cross(string Id) => Horizontal ? Nodes.Single(Node => Node.Id == Id).Y : Nodes.Single(Node => Node.Id == Id).X;
        Assert.That(Cross("a"), Is.Zero);
        Assert.That(Cross("b"), Is.EqualTo(210));
        Assert.That(Cross("j"), Is.InRange(90, 120));
        Assert.That(Cross("j") % 30, Is.Zero);
    }
}
