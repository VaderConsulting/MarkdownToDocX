using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic aligned-branch movement and preservation of occupied-rank order.</summary>
[TestFixture]
public sealed class FlowchartCellJunctionBranchTests
{
    /// <summary>A complete connected side chain moves onto the closest legal whole-cell lane.</summary>
    [Test]
    public void CompactsEveryAlignedBranchMemberTogether()
    {
        var (Contract, Nodes, Grid) = Fixture();
        var Before = Grid.Capture();
        var Candidates = FlowchartJunctionBranchCompaction.CellCandidates(Contract, Nodes, false).ToArray();
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        var Candidate = Candidates.First(Item => Item.Subject == "A");
        Assert.That(Candidate.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "A", "B" }));
        Assert.That(FlowchartCellRepair.PlanMove(Candidate.Nodes, Candidate.Rows, Candidate.Columns)!.TryApply(), Is.True);
        Assert.That(Grid.RegionOf("A").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(1));
        Grid.Restore(Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hint on a tail member keeps the entire aligned branch fixed.</summary>
    [Test]
    public void HintedTailAnchorsItsWholeBranch()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Nodes.Single(Node => Node.Id == "B").PlacementHintLocked = true;
        var Before = Grid.Capture();
        Assert.That(FlowchartJunctionBranchCompaction.CellCandidates(Contract, Nodes, false).Any(Item => Item.Subject == "A"), Is.False);
        Assert.That(Grid.RegionOf("A"), Is.SameAs(Before.Regions["A"]));
        Assert.That(Grid.RegionOf("B"), Is.SameAs(Before.Regions["B"]));
    }

    /// <summary>A side chain cannot jump over an occupied member of one of its rank strips.</summary>
    [Test]
    public void OccupiedRankKeepsItsOrdering()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Grid.TryPlace("O", new(2, 2, 1, 1));
        var O = new DiagramGridLayout.GridNode { Id = "O", Rank = 2 }; O.BindCells(Grid); Nodes.Add(O);
        var Candidates = FlowchartJunctionBranchCompaction.CellCandidates(Contract, Nodes, false).ToArray();
        Assert.That(Candidates.Where(Item => Item.Subject == "A").All(Item => 4 + Item.Columns > 2), Is.True);
        Assert.That(Grid.RegionOf("A").StartColumn, Is.GreaterThan(Grid.RegionOf("O").StartColumn));
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(Grid.RegionOf("A").StartColumn));
    }

    /// <summary>Creates one circular fan with a right-hand two-node chain and a left-hand terminal branch.</summary>
    /// <returns>Contract, bound members and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Row, Column) in new[] { ("J", 0, 0), ("A", 1, 4), ("B", 2, 4), ("X", 1, -4) })
        {
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Shape = Id == "J" ? "circle" : "rect", Rank = Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "J", TargetId = "A" }, new() { SourceId = "J", TargetId = "X" }, new() { SourceId = "A", TargetId = "B" }]);
        return (Contract, Nodes, Grid);
    }
}
