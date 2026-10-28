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
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        DiagramGridLayout.CellRouteMoveTarget Candidate = FlowchartTerminalRowPlacement.CellCandidates(Contract, Nodes, false).Single();
        FlowchartTerminalRowPlacement.Apply(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        DiagramGridLayout.GridNode Leaf = Nodes.Single(Node => Node.Id == "L");
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
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Nodes.Single(Node => Node.Id == "L").PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartTerminalRowPlacement.CellCandidates(Contract, Nodes, false), Is.Empty);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a terminal opposite a collecting sibling, with an empty bounded slot in the parent rank.</summary>
    /// <returns>Graph, bound nodes and complete ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract();
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Row, int Column) in new[] { ("P", 0, 0), ("Q", 0, -2), ("O", 0, 3), ("L", 1, 3), ("S", 1, -1) })
        {
            Assert.That(Grid.TryPlace(Id, new(Row, Column, 1, 1)), Is.True);
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "P", TargetId = "L" }, new() { SourceId = "P", TargetId = "S" }, new() { SourceId = "Q", TargetId = "S" }]);
        return (Contract, Nodes, Grid);
    }
}
