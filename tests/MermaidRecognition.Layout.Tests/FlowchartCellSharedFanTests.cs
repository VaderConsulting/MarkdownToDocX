using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete shared-root and sole-tail placement with exact rejected ownership.</summary>
[TestFixture]
public sealed class FlowchartCellSharedFanTests
{
    /// <summary>A shared root centres on its children while their complete tails align to their respective child starts.</summary>
    [Test]
    public void SharedRootAndBothTailsPublishTogether()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Assert.That(DiagramGridLayout.AlignCellSharedFanSourcesAndTails(Contract, Nodes, false), Is.EquivalentTo(new[] { "E" }));
        Assert.That(Grid.RegionOf("E").StartColumn, Is.EqualTo(4));
        Assert.That(Grid.RegionOf("T1").StartColumn, Is.EqualTo(0));
        Assert.That(Grid.RegionOf("T2").StartColumn, Is.EqualTo(8));
    }

    /// <summary>A late tail collision or hinted root rejects the root and every tail alignment together.</summary>
    /// <param name="Hint">Whether rejection comes from a root hint rather than an occupied tail target.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RejectedSharedPlanLeavesAllOwnershipUnchanged(bool Hint)
    {
        var (Contract, Nodes, Grid) = Fixture();
        if (Hint) Nodes.Single(Node => Node.Id == "E").PlacementHintLocked = true;
        else
        {
            Grid.TryPlace("O", new(4, 8, 1, 1)); var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        }
        var Before = Grid.Capture();
        Assert.That(DiagramGridLayout.AlignCellSharedFanSourcesAndTails(Contract, Nodes, false), Is.Empty);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a shared root, two independent inputs and two children with displaced terminal tails.</summary>
    /// <returns>Contract, bound members and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Rank, Column) in new[] { ("E", 0, 10), ("P1", 0, 0), ("P2", 0, 8), ("C1", 1, 0), ("C2", 1, 8), ("T1", 2, 4), ("T2", 2, 12) })
        {
            Grid.TryPlace(Id, new(Rank * 2, Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "E", TargetId = "C1" }, new() { SourceId = "E", TargetId = "C2" },
            new() { SourceId = "P1", TargetId = "C1" }, new() { SourceId = "P2", TargetId = "C2" },
            new() { SourceId = "C1", TargetId = "T1" }, new() { SourceId = "C2", TargetId = "T2" }]);
        return (Contract, Nodes, Grid);
    }
}
