using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks whole-cell side-branch compaction without moving a straight continuation or explicit hint.</summary>
[TestFixture]
public sealed class FlowchartCellTerminalBranchTests
{
    /// <summary>A distant leaf compacts beside a continuing sibling and rollback retains the exact original cell/gap snapshot.</summary>
    /// <param name="Horizontal">Whether the branch cross axis uses rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void LeafMovesToNearestFreeWholeCellBesideContinuation(bool Horizontal)
    {
        var (Contract, Nodes, Grid) = Fixture(Horizontal);
        var Before = Grid.Capture();
        Assert.That(FlowchartTerminalBranchPlacement.ApplyCells(Contract, Nodes, Horizontal, 30), Is.True);
        Assert.That(Horizontal ? Grid.RegionOf("L").StartRow : Grid.RegionOf("L").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("C"), Is.SameAs(Before.Regions["C"]));
        Grid.Restore(Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Explicit leaf ownership takes priority over automatic side-branch compaction.</summary>
    [Test]
    public void HintedLeafKeepsItsReservation()
    {
        var (Contract, Nodes, Grid) = Fixture(false);
        Nodes.Single(Node => Node.Id == "L").PlacementHintLocked = true;
        var Before = Grid.RegionOf("L");
        FlowchartTerminalBranchPlacement.ApplyCells(Contract, Nodes, false, 30);
        Assert.That(Grid.RegionOf("L"), Is.SameAs(Before));
    }

    /// <summary>Creates a parent, straight continuation, continuation tail and distant terminal sibling.</summary>
    /// <param name="Horizontal">Whether to transpose initial logical positions.</param>
    /// <returns>Graph, bound nodes and shared cell ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Rank, Column) in new[] { ("P", 0, 0), ("C", 1, 0), ("T", 2, 0), ("L", 1, 5) })
        {
            Grid.TryPlace(Id, new(Horizontal ? Column : Rank, Horizontal ? Rank : Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank, Column = Column }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "P", TargetId = "C" }, new() { SourceId = "C", TargetId = "T" }, new() { SourceId = "P", TargetId = "L" }]);
        return (Contract, Nodes, Grid);
    }
}
