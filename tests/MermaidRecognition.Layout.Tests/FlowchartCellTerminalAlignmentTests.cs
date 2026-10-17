using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete terminal-child alignment with hint priority and rejection rollback.</summary>
[TestFixture]
public sealed class FlowchartCellTerminalAlignmentTests
{
    /// <summary>Selected children move along the whole-cell cross axis in either flow orientation.</summary>
    /// <param name="Horizontal">Whether the cross axis is rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void TerminalAlignmentUsesWholeCrossAxisCells(bool Horizontal)
    {
        var (Contract, Nodes, Grid) = Fixture(Horizontal);
        Assert.That(DiagramGridLayout.AlignCellTerminalChildren(Contract, Nodes, Horizontal), Is.True);
        var A = Grid.RegionOf("A"); var B = Grid.RegionOf("B");
        Assert.That(Horizontal ? B.StartRow : B.StartColumn, Is.EqualTo(Horizontal ? A.StartRow : A.StartColumn));
        Assert.That(Nodes[1].Column, Is.EqualTo(Nodes[0].Column));
    }

    /// <summary>A colliding target rejects all cells and preserves logical ordering metadata too.</summary>
    [Test]
    public void BlockedChildLeavesOwnershipAndOrderingUnchanged()
    {
        var (Contract, Nodes, Grid) = Fixture(false);
        Grid.TryPlace("C", new(1, 0, 1, 1));
        var C = new DiagramGridLayout.GridNode { Id = "C" }; C.BindCells(Grid); Nodes.Add(C);
        var Before = Grid.Capture(); int Column = Nodes[1].Column;
        Assert.That(DiagramGridLayout.AlignCellTerminalChildren(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes[1].Column, Is.EqualTo(Column));
    }

    /// <summary>An explicit hinted child remains in its declared region even when automatic straightening would move it.</summary>
    [Test]
    public void HintedChildDoesNotMove()
    {
        var (Contract, Nodes, Grid) = Fixture(false);
        Nodes[1].PlacementHintLocked = true;
        var Before = Grid.RegionOf("B");
        DiagramGridLayout.AlignCellTerminalChildren(Contract, Nodes, false);
        Assert.That(Grid.RegionOf("B"), Is.SameAs(Before));
    }

    /// <summary>Creates a sole visible terminal edge with a displaced cross-axis child.</summary>
    /// <param name="Horizontal">Whether to transpose the initial ownership.</param>
    /// <returns>Graph, bound nodes and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Contract = new DiagramContract();
        Contract.EdgesMutable.Add(new EdgeContract { SourceId = "A", TargetId = "B" });
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(Horizontal ? 2 : 1, Horizontal ? 1 : 2, 1, 1));
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "A", Rank = 0, Column = 0 }, new() { Id = "B", Rank = 1, Column = 2 } };
        foreach (var Node in Nodes) Node.BindCells(Grid);
        return (Contract, Nodes, Grid);
    }
}
