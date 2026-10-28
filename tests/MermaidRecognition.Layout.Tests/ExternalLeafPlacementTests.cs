using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ExternalLeafPlacementTests
{
    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    public void SoleOutsideNeighbour_AlignsBesideGroupedPathAndRespectsBlockedSide(bool Horizontal, bool Reverse, bool Blocked)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph container\n"
            + (Reverse ? "continuation --> parent" : "parent --> continuation") + "\nend\n"
            + (Reverse ? "outside -->|information| parent" : "parent -->|information| outside"));
        DiagramGridLayout.GridNode Node(string Id, double Cross, double Main, string? Group) => new()
        {
            Id = Id, GroupId = Group, X = Horizontal ? Main : Cross, Y = Horizontal ? Cross : Main,
            Width = Horizontal ? 60 : 150, Height = Horizontal ? 150 : 60, Rank = Main == 0 ? 0 : 1
        };
        DiagramGridLayout.GridNode Parent = Node("parent", 0, 0, "container");
        DiagramGridLayout.GridNode Leaf = Node("outside", 420, 150, null);
        List<DiagramGridLayout.GridNode> Nodes = [Parent, Node("continuation", 0, 150, "container"), Leaf];
        DiagramGridLayout.GridFrame Frame = new DiagramGridLayout.GridFrame { Id = "container", Left = Horizontal ? -60 : -105,
            Right = Horizontal ? 210 : 105, Top = Horizontal ? -105 : -60, Bottom = Horizontal ? 105 : 210 };
        if (Blocked)
        {
            Nodes.Add(Node("obstacle", 270, 0, null));
        }

        Assert.That(FlowchartExternalLeafPlacement.Apply(Contract, Nodes, [Frame], Horizontal), Is.True);
        Assert.That(Horizontal ? Leaf.X : Leaf.Y, Is.EqualTo(0));
        double Cross = Horizontal ? Leaf.Y : Leaf.X;
        Assert.That(Math.Sign(Cross), Is.EqualTo(Blocked ? -1 : 1));
        Assert.That(Math.Abs(Cross) - 75 - 105, Is.GreaterThanOrEqualTo(30));
        Assert.That(Leaf.Rank, Is.EqualTo(Parent.Rank));
        Assert.That(Leaf.GroupId, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OutsideEndpointOfUnbranchedPath_KeepsItsPlacement(bool Incoming)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph container\n"
            + (Incoming ? "parent --> continuation" : "continuation --> parent") + "\nend\n"
            + (Incoming ? "outside --> parent" : "parent --> outside"));
        DiagramGridLayout.GridNode Leaf = new DiagramGridLayout.GridNode { Id = "outside", X = 420, Y = 150, Width = 150, Height = 60 };
        List<DiagramGridLayout.GridNode> Nodes = [ Leaf,
            new() { Id = "parent", GroupId = "container", Width = 150, Height = 60 },
            new() { Id = "continuation", GroupId = "container", Y = 150, Width = 150, Height = 60 } ];
        DiagramGridLayout.GridFrame Frame = new DiagramGridLayout.GridFrame { Id = "container", Left = -105, Right = 105, Top = -60, Bottom = 210 };
        Assert.That(FlowchartExternalLeafPlacement.Apply(Contract, Nodes, [Frame], false), Is.False);
        Assert.That((Leaf.X, Leaf.Y), Is.EqualTo((420d, 150d)));
    }

    [Test]
    public void OutsideNodeWithTwoNeighbours_KeepsItsPlacement()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph container\nparent --> continuation\nend\nparent --> outside\noutside --> continuation");
        DiagramGridLayout.GridNode Leaf = new DiagramGridLayout.GridNode { Id = "outside", X = 420, Y = 150, Width = 150, Height = 60 };
        List<DiagramGridLayout.GridNode> Nodes = [ Leaf,
            new() { Id = "parent", GroupId = "container", Width = 150, Height = 60 },
            new() { Id = "continuation", GroupId = "container", Y = 150, Width = 150, Height = 60 } ];
        DiagramGridLayout.GridFrame Frame = new DiagramGridLayout.GridFrame { Id = "container", Left = -105, Right = 105, Top = -60, Bottom = 210 };
        Assert.That(FlowchartExternalLeafPlacement.Apply(Contract, Nodes, [Frame], false), Is.False);
        Assert.That((Leaf.X, Leaf.Y), Is.EqualTo((420d, 150d)));
    }
}
