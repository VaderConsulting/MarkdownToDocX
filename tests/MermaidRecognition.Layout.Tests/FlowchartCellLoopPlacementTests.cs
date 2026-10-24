using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic vertical loop and continuing-tail placement with hard hint priority.</summary>
[TestFixture]
public sealed class FlowchartCellLoopPlacementTests
{
    /// <summary>The return branch and the complete unbranched continuing tail occupy opposite whole-cell lanes.</summary>
    [Test]
    public void PlacesLoopAndWholeTailTogether()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Assert.That(DiagramGridLayout.AlignCellVerticalLoopBranches(Contract, Nodes), Is.True);
        Assert.That(Grid.RegionOf("L").StartColumn, Is.EqualTo(3));
        Assert.That(Grid.RegionOf("C").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("T").StartColumn, Is.EqualTo(1));
    }

    /// <summary>Side separation translates the complete tail without flattening its original cross-axis offsets.</summary>
    [Test]
    public void PreservesTailOffsetsForRoutedScoring()
    {
        var (Contract, Nodes, Grid) = Fixture();
        var Before = Grid.RegionOf("T");
        Assert.That(Grid.TryPlace("T", new(Before.StartRow, Before.StartColumn + 1, Before.RowSpan, Before.ColumnSpan)), Is.True);
        Assert.That(DiagramGridLayout.AlignCellVerticalLoopBranches(Contract, Nodes), Is.True);
        Assert.That(Grid.RegionOf("T").StartColumn - Grid.RegionOf("C").StartColumn, Is.EqualTo(1));
    }

    /// <summary>A late occupied or hinted tail member rejects both the continuation chain and the return branch.</summary>
    /// <param name="Hint">Whether the tail is anchored by a hint instead of blocked cells.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RejectedTailKeepsEverySelectedMember(bool Hint)
    {
        var (Contract, Nodes, Grid) = Fixture();
        if (Hint) Nodes.Single(Node => Node.Id == "T").PlacementHintLocked = true;
        else
        {
            Grid.TryPlace("O", new(6, 1, 1, 1)); var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        }
        var Before = Grid.Capture();
        DiagramGridLayout.AlignCellVerticalLoopBranches(Contract, Nodes);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Creates a generic vertical decision whose side branch returns to the input and whose continuation has one tail.</summary>
    /// <returns>Graph, bound nodes and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Rank, Column) in new[] { ("I", 0, 2), ("D", 1, 2), ("L", 2, 4), ("C", 2, 6), ("T", 3, 6) })
        {
            Grid.TryPlace(Id, new(Rank * 2, Column, Id == "D" ? 2 : 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D", Rank = Rank }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "I", TargetId = "D" }, new() { SourceId = "D", TargetId = "L" },
            new() { SourceId = "D", TargetId = "C" }, new() { SourceId = "L", TargetId = "I" }, new() { SourceId = "C", TargetId = "T" }]);
        return (Contract, Nodes, Grid);
    }
}
