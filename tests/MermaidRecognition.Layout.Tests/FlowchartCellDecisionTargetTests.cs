using System.Reflection;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic whole-cell placement of terminal outcomes on opposite sides of a decision.</summary>
[TestFixture]
public sealed class FlowchartCellDecisionTargetTests
{
    /// <summary>Both phases preserve initial branch order and regions for scored routing candidates.</summary>
    /// <param name="Horizontal">Whether the cross axis is logical rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void OutcomesUseOppositeWholeCellSideLanes(bool Horizontal)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(Horizontal);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); var Columns = Nodes.Select(Node => Node.Column).ToArray();
        Invoke(Contract, Nodes, Horizontal);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
    }

    /// <summary>A colliding second outcome rejects both children and preserves logical metadata and exact cell/gap state.</summary>
    [Test]
    public void BlockedSecondOutcomeRejectsBothMoves()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(false);
        Grid.TryPlace("O", new(4, 3, 1, 1));
        DiagramGridLayout.GridNode O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); var Columns = Nodes.Select(Node => Node.Column).ToArray();
        Invoke(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
    }

    /// <summary>Calls both real legacy phases, including their ordering metadata writes.</summary>
    /// <param name="Contract">Visible outcomes.</param>
    /// <param name="Nodes">Authoritative members.</param>
    /// <param name="Horizontal">Flow orientation.</param>
    private static void Invoke(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        MethodInfo Method = typeof(DiagramGridLayout).GetMethod("AlignTerminalDecisionTargets", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        Method.Invoke(null, [Contract, Nodes, Horizontal, DiagramGridMetrics.GRID_UNIT_PX, false]);
        Method.Invoke(null, [Contract, Nodes, Horizontal, DiagramGridMetrics.GRID_UNIT_PX, true]);
    }

    /// <summary>Creates one two-row diamond and two same-rank terminal outcomes.</summary>
    /// <param name="Horizontal">Whether to transpose starting coordinates.</param>
    /// <returns>Contract, bound members and their common grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        DiagramContract Contract = new DiagramContract(); FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("D", new(Horizontal ? 2 : 1, Horizontal ? 1 : 2, 2, 1));
        Grid.TryPlace("A", new(Horizontal ? 0 : 4, Horizontal ? 4 : 0, 1, 1));
        Grid.TryPlace("B", new(4, 4, 1, 1));
        List<DiagramGridLayout.GridNode> Nodes = [ new() { Id = "D", IsDiamond = true, Rank = 0, Column = 2 },
            new() { Id = "A", Rank = 1, Column = 0 }, new() { Id = "B", Rank = 1, Column = 4 } ];
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.BindCells(Grid);
        }

        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "A" }, new() { SourceId = "D", TargetId = "B" }]);
        return (Contract, Nodes, Grid);
    }
}
