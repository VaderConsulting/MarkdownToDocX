using System.Reflection;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that the superseded median-child centring pass cannot reposition cell-bound fan inputs.</summary>
[TestFixture]
public sealed class FlowchartCellFanAlignmentTests
{
    /// <summary>Both legacy phases preserve the original fan and its input chain instead of assigning a median column.</summary>
    /// <param name="Horizontal">Whether to transpose the logical initial layout.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void PreservesParentAndChainCells(bool Horizontal)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(Horizontal);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); var Ordering = Nodes.Select(Node => Node.Column).ToArray();
        Invoke(Contract, Nodes, Horizontal);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Ordering));
    }

    /// <summary>A blocked upstream target rejects the centred parent too, preserving ownership and ordering metadata.</summary>
    [Test]
    public void BlockedChainRejectsTheWholeFanPlan()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Grid.TryPlace("O", new(0, 1, 1, 1));
        DiagramGridLayout.GridNode Obstacle = new DiagramGridLayout.GridNode { Id = "O" }; Obstacle.BindCells(Grid); Nodes.Add(Obstacle);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); var Columns = Nodes.Select(Node => Node.Column).ToArray();
        Invoke(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
    }

    /// <summary>Calls both real legacy phases, verifying the bypass also precedes ordering metadata changes.</summary>
    /// <param name="Contract">Terminal fan relationships.</param>
    /// <param name="Nodes">Complete cell-bound input chain and children.</param>
    /// <param name="Horizontal">Flow orientation.</param>
    private static void Invoke(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        MethodInfo Method = typeof(DiagramGridLayout).GetMethod("CentreTerminalFans", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        Method.Invoke(null, [Contract, Nodes, Horizontal, false]);
        Method.Invoke(null, [Contract, Nodes, Horizontal, true]);
    }

    /// <summary>Creates a three-way terminal fan and an unbranched upstream input.</summary>
    /// <param name="Horizontal">Whether ranks advance along columns.</param>
    /// <returns>Contract, bound members and grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal = false)
    {
        DiagramContract Contract = new DiagramContract(); FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Rank, int Column) in new[] { ("U", 0, 0), ("P", 1, 0), ("A", 2, 0), ("B", 2, 1), ("C", 2, 2) })
        {
            Grid.TryPlace(Id, new(Horizontal ? Column : Rank, Horizontal ? Rank : Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank, Column = Column }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "U", TargetId = "P" }, new() { SourceId = "P", TargetId = "A" },
            new() { SourceId = "P", TargetId = "B" }, new() { SourceId = "P", TargetId = "C" }]);
        return (Contract, Nodes, Grid);
    }
}
