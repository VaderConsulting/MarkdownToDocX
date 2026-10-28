using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class TerminalBranchPlacementTests
{
    [TestCase(false, false, 1)]
    [TestCase(false, true, 1)]
    [TestCase(true, false, 1)]
    [TestCase(true, true, 1)]
    [TestCase(false, false, 8)]
    [TestCase(true, false, 8)]
    public void TerminalChildren_UseNearestFreeColumnsBesideStraightContinuation(bool Horizontal, bool Mirrored, int Count)
    {
        int Sign = Mirrored ? -1 : 1;
        string Source = "flowchart TB\np --> c\nc --> tail\n";
        List<DiagramGridLayout.GridNode> Nodes = [];
        DiagramGridLayout.GridNode Add(string Id, int Rank, int Cross)
        {
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank,
                X = Horizontal ? Rank * 150 : Cross, Y = Horizontal ? Cross : Rank * 150,
                Width = Horizontal ? 60 : 180, Height = Horizontal ? 180 : 60 };
            Nodes.Add(Node); return Node;
        }
        DiagramGridLayout.GridNode Parent = Add("p", 0, 0); DiagramGridLayout.GridNode Continuation = Add("c", 1, 0);
        Add("tail", 2, 0); Add("obstacle", 1, -Sign * 210);
        List<DiagramGridLayout.GridNode> Leaves = [];
        for (int Index = 0; Index < Count; Index++)
        {
            Source += $"p --> terminal{Index}\n";
            Leaves.Add(Add($"terminal{Index}", 1, -Sign * (Count + 2 + Index) * 210));
        }
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        FlowchartTerminalBranchPlacement.Apply(Contract, Nodes, Horizontal, 210);
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        Assert.That(Cross(Parent), Is.Zero);
        Assert.That(Cross(Continuation), Is.Zero);
        Assert.That(Cross(Leaves[0]), Is.EqualTo(Sign * 210));
        var Rank = Nodes.Where(Node => Node.Rank == 1).Select(Cross).OrderBy(Value => Value).ToArray();
        Assert.That(Rank.Zip(Rank.Skip(1)).All(Pair => Pair.Second - Pair.First >= 210), Is.True);
        Assert.That(Leaves.All(Leaf => Cross(Leaf) % 30 == 0), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalChild_BesideLongerBypassKeepsItsCorridor(bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\np --> c\nc --> tail\np --> terminal\np --> tail");
        List<DiagramGridLayout.GridNode> Nodes =
        [
            new() { Id = "p", Rank = 0, Width = 180, Height = 60 },
            new() { Id = "c", Rank = 1, Width = 180, Height = 60 },
            new() { Id = "tail", Rank = 2, Width = 180, Height = 60 },
            new() { Id = "terminal", Rank = 1, X = -630, Y = -630, Width = 180, Height = 60 }
        ];
        FlowchartTerminalBranchPlacement.Apply(Contract, Nodes, Horizontal, 210);
        Assert.That(Nodes[^1].X, Is.EqualTo(-630));
        Assert.That(Nodes[^1].Y, Is.EqualTo(-630));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalChild_InAnotherGroupKeepsItsPlacement(bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\np --> c\nc --> tail\np --> terminal");
        List<DiagramGridLayout.GridNode> Nodes =
        [
            new() { Id = "p", Rank = 0, Width = 180, Height = 60 },
            new() { Id = "c", Rank = 1, Width = 180, Height = 60 },
            new() { Id = "tail", Rank = 2, Width = 180, Height = 60 },
            new() { Id = "terminal", Rank = 1, X = -630, Y = -630, Width = 180, Height = 60, GroupId = "separate" }
        ];
        FlowchartTerminalBranchPlacement.Apply(Contract, Nodes, Horizontal, 210);
        Assert.That(Nodes[^1].X, Is.EqualTo(-630));
        Assert.That(Nodes[^1].Y, Is.EqualTo(-630));
    }
}
