using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete shared-root and sole-tail placement with exact rejected ownership.</summary>
[TestFixture]
public sealed class FlowchartCellSharedFanTests
{
    /// <summary>Shared-root classification preserves the root and both tails for scored whole-cell routing candidates.</summary>
    [Test]
    public void SharedRootAndBothTailsPublishTogether()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(DiagramGridLayout.SelectCellSharedFanSources(Contract, Nodes, false), Is.EquivalentTo(["E"]));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A late tail collision or hinted root rejects the root and every tail alignment together.</summary>
    /// <param name="Hint">Whether rejection comes from a root hint rather than an occupied tail target.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RejectedSharedPlanLeavesAllOwnershipUnchanged(bool Hint)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        if (Hint)
        {
            Nodes.Single(Node => Node.Id == "E").PlacementHintLocked = true;
        }
        else
        {
            Grid.TryPlace("O", new(4, 8, 1, 1)); DiagramGridLayout.GridNode O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        }
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(DiagramGridLayout.SelectCellSharedFanSources(Contract, Nodes, false), Is.EquivalentTo(["E"]));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a shared root, two independent inputs and two children with displaced terminal tails.</summary>
    /// <returns>Contract, bound members and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract(); FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Rank, int Column) in new[] { ("E", 0, 10), ("P1", 0, 0), ("P2", 0, 8), ("C1", 1, 0), ("C2", 1, 8), ("T1", 2, 4), ("T2", 2, 12) })
        {
            Grid.TryPlace(Id, new(Rank * 2, Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "E", TargetId = "C1" }, new() { SourceId = "E", TargetId = "C2" },
            new() { SourceId = "P1", TargetId = "C1" }, new() { SourceId = "P2", TargetId = "C2" },
            new() { SourceId = "C1", TargetId = "T1" }, new() { SourceId = "C2", TargetId = "T2" }]);
        return (Contract, Nodes, Grid);
    }
}
