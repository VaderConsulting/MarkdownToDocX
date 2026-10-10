using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class CollectorInputPlacementTests
{
    [TestCase(false, false, 3)]
    [TestCase(false, true, 3)]
    [TestCase(true, false, 3)]
    [TestCase(true, true, 3)]
    [TestCase(false, false, 5)]
    [TestCase(true, false, 5)]
    [TestCase(false, false, 9)]
    [TestCase(true, false, 9)]
    public void IndependentInputs_CloseAroundAnchoredChain(bool Horizontal, bool Mirror, int Count)
    {
        int Sign = Mirror ? -1 : 1;
        int Half = Count / 2;
        string Source = "flowchart TB\nupstream --> pLast\npLast --> merge\n";
        var Nodes = new List<DiagramGridLayout.GridNode>();
        DiagramGridLayout.GridNode Add(string Id, int Rank, int Cross)
        {
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank,
                X = Horizontal ? Rank * 150 : Cross, Y = Horizontal ? Cross : Rank * 150,
                Width = Horizontal ? 60 : 150, Height = Horizontal ? 150 : 60 };
            Nodes.Add(Node); return Node;
        }
        var Anchor = Add("pLast", 1, Sign * Half * 210);
        Add("upstream", 0, Sign * Half * 210);
        Add("merge", 2, 0);
        var Inputs = new List<DiagramGridLayout.GridNode>();
        for (int Index = 0; Index < Count - 1; Index++)
        {
            Source += $"p{Index} --> merge\n";
            Inputs.Add(Add($"p{Index}", 1, Sign * (Index - Half - 1) * 210));
        }
        DiagramGridLayout.CompactIndependentFanIn(MermaidRecognizer.Parse(Source), Nodes, Horizontal, 180);
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        Assert.That(Cross(Anchor), Is.EqualTo(Sign * Half * 210));
        var Centres = Inputs.Append(Anchor).Select(Cross).Order().ToArray();
        Assert.That(Centres[Half], Is.Zero);
        Assert.That(Centres.Zip(Centres.Skip(1)).All(Pair => Pair.Second - Pair.First == 210), Is.True);
        Assert.That(Nodes.Where(Node => Node.Rank == 1).All(Node =>
            (Horizontal ? Node.X : Node.Y) == 150), Is.True);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void IndependentTerminalInput_UsesOuterSideWithoutCrossingMainInput(bool Horizontal, bool Mirror)
    {
        int Sign = Mirror ? -1 : 1;
        var Contract = MermaidRecognizer.Parse("flowchart TB\nupstream --> parent\nparent --> terminal\nroot --> terminal");
        var Nodes = new List<DiagramGridLayout.GridNode>();
        DiagramGridLayout.GridNode Add(string Id, int Rank, int Cross)
        {
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank,
                X = Horizontal ? Rank * 150 : Cross, Y = Horizontal ? Cross : Rank * 150,
                Width = Horizontal ? 60 : 150, Height = Horizontal ? 150 : 60 };
            Nodes.Add(Node); return Node;
        }
        Add("upstream", 0, 0); Add("parent", 1, 0);
        var Target = Add("terminal", 2, Sign * 210);
        var Root = Add("root", 1, -Sign * 210);
        FlowchartTerminalInputPlacement.Apply(Contract, Nodes, Horizontal, 210);
        Assert.That(Root.Rank, Is.EqualTo(Target.Rank));
        Assert.That(Horizontal ? Root.X : Root.Y, Is.EqualTo(Horizontal ? Target.X : Target.Y));
        Assert.That(Horizontal ? Root.Y : Root.X, Is.EqualTo(Sign * 420));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CollectorWithOutgoingPath_KeepsItsIndependentInputUpstream(bool Horizontal)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nupstream --> parent\nparent --> merge\nroot --> merge\nmerge --> tail");
        var Nodes = new List<DiagramGridLayout.GridNode>
        {
            new() { Id = "upstream", Rank = 0 }, new() { Id = "parent", Rank = 1 },
            new() { Id = "merge", Rank = 2 }, new() { Id = "tail", Rank = 3 },
            new() { Id = "root", Rank = 1, X = -210, Y = -210 }
        };
        FlowchartTerminalInputPlacement.Apply(Contract, Nodes, Horizontal, 210);
        Assert.That(Nodes[^1].Rank, Is.EqualTo(1));
        Assert.That(Nodes[^1].X, Is.EqualTo(-210));
        Assert.That(Nodes[^1].Y, Is.EqualTo(-210));
    }
}
