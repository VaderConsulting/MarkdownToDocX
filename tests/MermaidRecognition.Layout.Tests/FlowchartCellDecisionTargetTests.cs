using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic whole-cell placement of terminal outcomes on opposite sides of a decision.</summary>
[TestFixture]
public sealed class FlowchartCellDecisionTargetTests
{
    /// <summary>Two terminal children occupy opposite adjacent side lanes without changing their spans or main-axis rank.</summary>
    /// <param name="Horizontal">Whether the cross axis is logical rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void OutcomesUseOppositeWholeCellSideLanes(bool Horizontal)
    {
        var (Contract, Nodes, Grid) = Fixture(Horizontal);
        Assert.That(DiagramGridLayout.AlignCellTerminalDecisionTargets(Contract, Nodes, Horizontal), Is.True);
        var D = Grid.RegionOf("D"); var A = Grid.RegionOf("A"); var B = Grid.RegionOf("B");
        Assert.That(Horizontal ? A.EndRow : A.EndColumn, Is.EqualTo(Horizontal ? D.StartRow : D.StartColumn));
        Assert.That(Horizontal ? B.StartRow : B.StartColumn, Is.EqualTo(Horizontal ? D.EndRow : D.EndColumn));
    }

    /// <summary>A colliding second outcome rejects both children and preserves logical metadata and exact cell/gap state.</summary>
    [Test]
    public void BlockedSecondOutcomeRejectsBothMoves()
    {
        var (Contract, Nodes, Grid) = Fixture(false);
        Grid.TryPlace("O", new(4, 3, 1, 1));
        var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        var Before = Grid.Capture(); var Columns = Nodes.Select(Node => Node.Column).ToArray();
        Assert.That(DiagramGridLayout.AlignCellTerminalDecisionTargets(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
    }

    /// <summary>Creates one two-row diamond and two same-rank terminal outcomes.</summary>
    /// <param name="Horizontal">Whether to transpose starting coordinates.</param>
    /// <returns>Contract, bound members and their common grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("D", new(Horizontal ? 2 : 1, Horizontal ? 1 : 2, 2, 1));
        Grid.TryPlace("A", new(Horizontal ? 0 : 4, Horizontal ? 4 : 0, 1, 1));
        Grid.TryPlace("B", new(4, 4, 1, 1));
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "D", IsDiamond = true, Rank = 0, Column = 2 },
            new() { Id = "A", Rank = 1, Column = 0 }, new() { Id = "B", Rank = 1, Column = 4 } };
        foreach (var Node in Nodes) Node.BindCells(Grid);
        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "A" }, new() { SourceId = "D", TargetId = "B" }]);
        return (Contract, Nodes, Grid);
    }
}
