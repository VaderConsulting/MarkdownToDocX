using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks component-preserving, scored whole-cell packing with exact hint priority and rollback.</summary>
[TestFixture]
public sealed class FlowchartCellDisconnectedPackingTests
{
    /// <summary>Disconnected pieces compact through one complete assignment while each connected component keeps its internal offsets.</summary>
    [Test]
    public void ComponentPackingPreservesRelativeCellsAndExactRollback()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartDisconnectedPacking.ApplyCells(Contract, Nodes), Is.True);
        Assert.That(Grid.RegionOf("B").StartRow - Grid.RegionOf("A").StartRow, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("B").StartColumn - Grid.RegionOf("A").StartColumn, Is.EqualTo(0));
        Assert.That(Grid.Capture().Regions.Values.SelectMany((A, Index) => Grid.Capture().Regions.Values.Skip(Index + 1).Select(B => A.Overlaps(B))), Is.All.False);
        Grid.Restore(Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hint on one member anchors its entire component, including otherwise unhinted peers.</summary>
    [Test]
    public void HintedComponentRemainsFixedAsAWhole()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Nodes[0].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        FlowchartDisconnectedPacking.ApplyCells(Contract, Nodes);
        Assert.That(Grid.RegionOf("A"), Is.SameAs(Before.Regions["A"]));
        Assert.That(Grid.RegionOf("B"), Is.SameAs(Before.Regions["B"]));
    }

    /// <summary>Creates three distant components, including one two-node chain.</summary>
    /// <returns>Graph, bound nodes and their authoritative grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract(); FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Row, int Column) in new[] { ("A", 0, 0), ("B", 1, 0), ("C", 10, 10), ("D", 20, 20) })
        {
            Contract.NodesMutable.Add(new NodeContract { Id = Id }); Grid.TryPlace(Id, new(Row, Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.Add(new EdgeContract { SourceId = "A", TargetId = "B" });
        return (Contract, Nodes, Grid);
    }
}
