using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that pre-placement column metadata cannot reintroduce centre-only policies into cell ownership.</summary>
[TestFixture]
public sealed class FlowchartCellMetadataPolicyTests
{
    /// <summary>A forward chain retains its independently ordered columns until whole-cell routed scoring.</summary>
    [Test]
    public void ForwardChainDoesNotRewriteColumns()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        var Columns = Nodes.Select(Node => Node.Column).ToArray();
        DiagramGridLayout.AlignForwardMainChainColumn(Contract, Nodes, false);
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A converging target retains its independent column instead of being placed on a middle source.</summary>
    [Test]
    public void MergeDoesNotRewriteColumns()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        var Columns = Nodes.Select(Node => Node.Column).ToArray();
        DiagramGridLayout.AlignFanInMergeColumn(Contract, Nodes, false);
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A decision's native branch order survives without assigning a middle child to its parent's column.</summary>
    [Test]
    public void DecisionChildrenKeepTheirOrderingColumns()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        var Columns = Nodes.Select(Node => Node.Column).ToArray();
        DiagramGridLayout.AlignDecisionCentreChildColumn(Contract, Nodes, false);
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Detour metadata keeps its side ordering without putting a continuing merge on the decision line.</summary>
    [Test]
    public void DetourPreservesIndependentMergeColumn()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Contract.EdgesMutable.Clear();
        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "B" }, new() { SourceId = "D", TargetId = "M" }, new() { SourceId = "B", TargetId = "M" }]);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        int Column = Nodes.Single(Node => Node.Id == "M").Column;
        DiagramGridLayout.AlignBypassDetourColumns(Contract, Nodes, false);
        Assert.That(Nodes.Single(Node => Node.Id == "M").Column, Is.EqualTo(Column));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A presumed straight-chord blocker cannot force a pre-score swap through column metadata.</summary>
    [Test]
    public void ChordProposalDoesNotSwapOrderingColumns()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(Grid.TryPlace("O", new(4, 0, 1, 1)), Is.True);
        DiagramGridLayout.GridNode Obstacle = new DiagramGridLayout.GridNode { Id = "O", Rank = 2, Column = 0 };
        Obstacle.BindCells(Grid); Nodes.Add(Obstacle);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        var Columns = Nodes.Select(Node => Node.Column).ToArray();
        DiagramGridLayout.ResolveVerticalChordBlockers(Contract, Nodes);
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Builds a forward fan with a converging merge and deliberately distinct ordering columns.</summary>
    /// <returns>Contract, bound nodes and authoritative grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract();
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Rank, int Column) in new[] { ("D", 0, 1), ("A", 1, 0), ("B", 1, 2), ("C", 1, 4), ("M", 2, 5) })
        {
            Contract.NodesMutable.Add(new() { Id = Id });
            Assert.That(Grid.TryPlace(Id, new(Rank * 2, Column, Id == "D" ? 2 : 1, 1)), Is.True);
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D", Rank = Rank, Column = Column, OldX = Column * 180 };
            Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "A" }, new() { SourceId = "D", TargetId = "B" },
            new() { SourceId = "D", TargetId = "C" }, new() { SourceId = "A", TargetId = "M" }, new() { SourceId = "B", TargetId = "M" }, new() { SourceId = "C", TargetId = "M" }]);
        return (Contract, Nodes, Grid);
    }
}
