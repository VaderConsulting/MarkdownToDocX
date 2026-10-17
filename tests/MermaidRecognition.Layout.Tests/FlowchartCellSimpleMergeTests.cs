using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic two-branch decision/terminal-merge placement.</summary>
[TestFixture]
public sealed class FlowchartCellSimpleMergeTests
{
    /// <summary>Branches occupy opposite whole-cell lanes and the terminal merge follows the right branch.</summary>
    [Test]
    public void PlacesBothBranchesAndMergeTogether()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Assert.That(DiagramGridLayout.AlignCellSimpleDecisionMerges(Contract, Nodes, false), Is.True);
        Assert.That(Grid.RegionOf("L").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("R").StartColumn, Is.EqualTo(3));
        Assert.That(Grid.RegionOf("M").StartColumn, Is.EqualTo(3));
    }

    /// <summary>A blocked or hinted merge retains both original branch reservations.</summary>
    /// <param name="Hint">Whether the merge is hinted rather than blocked.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RejectedMergeLeavesBothBranchesUnchanged(bool Hint)
    {
        var (Contract, Nodes, Grid) = Fixture();
        if (Hint) Nodes.Single(Node => Node.Id == "M").PlacementHintLocked = true;
        else
        {
            Grid.TryPlace("O", new(4, 3, 1, 1)); var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        }
        var Before = Grid.Capture();
        DiagramGridLayout.AlignCellSimpleDecisionMerges(Contract, Nodes, false);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Creates a generic diamond whose two independent branches meet at a terminal merge.</summary>
    /// <returns>Graph, bound members and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Rank, Column) in new[] { ("D", 0, 2), ("L", 1, 4), ("R", 1, 6), ("M", 2, 6) })
        {
            Grid.TryPlace(Id, new(Rank * 2, Column, Id == "D" ? 2 : 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D", Rank = Rank, OldX = Column * 180 }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "L" }, new() { SourceId = "D", TargetId = "R" },
            new() { SourceId = "L", TargetId = "M" }, new() { SourceId = "R", TargetId = "M" }]);
        return (Contract, Nodes, Grid);
    }
}
