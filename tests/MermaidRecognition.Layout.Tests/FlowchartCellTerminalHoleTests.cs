using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks unpublished whole-cell hole candidates and hint priority without pixel writes.</summary>
[TestFixture]
public sealed class FlowchartCellTerminalHoleTests
{
    /// <summary>A bounded free slot is offered atomically without mutating regions or rank metadata.</summary>
    [Test]
    public void OffersHoleWithoutApplyingAlignment()
    {
        var (Contract, Nodes, Grid) = Fixture();
        var Before = Grid.Capture();
        var Candidate = FlowchartTerminalRowPlacement.CellCandidates(Contract, Nodes, false).Single();
        FlowchartTerminalRowPlacement.Apply(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        var Leaf = Nodes.Single(Node => Node.Id == "L");
        Assert.That(Leaf.Rank, Is.EqualTo(1));
        Assert.That(Candidate.Subject, Is.EqualTo("L"));
        Assert.That(Candidate.Rows, Is.EqualTo(-1));
        Assert.That(Candidate.Columns, Is.EqualTo(-2));
        Assert.That(FlowchartCellRepair.PlanMove(Candidate.Nodes, Candidate.Rows, Candidate.Columns)!.TryApply(), Is.True);
        Assert.That(Grid.RegionOf("L"), Is.EqualTo(new FlowchartCellRegion(0, 1, 1, 1)));
    }

    /// <summary>A hinted terminal is never nominated for automatic hole filling.</summary>
    [Test]
    public void HintKeepsLeafFixed()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Nodes.Single(Node => Node.Id == "L").PlacementHintLocked = true;
        var Before = Grid.Capture();
        Assert.That(FlowchartTerminalRowPlacement.CellCandidates(Contract, Nodes, false), Is.Empty);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a terminal opposite a collecting sibling, with an empty bounded slot in the parent rank.</summary>
    /// <returns>Graph, bound nodes and complete ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract();
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Row, Column) in new[] { ("P", 0, 0), ("Q", 0, -2), ("O", 0, 3), ("L", 1, 3), ("S", 1, -1) })
        {
            Assert.That(Grid.TryPlace(Id, new(Row, Column, 1, 1)), Is.True);
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "P", TargetId = "L" }, new() { SourceId = "P", TargetId = "S" }, new() { SourceId = "Q", TargetId = "S" }]);
        return (Contract, Nodes, Grid);
    }
}
