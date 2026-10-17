namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks whole empty-strip compaction, complete hint rejection and exact ownership/gap rollback.</summary>
[TestFixture]
public sealed class FlowchartCellCompactionTests
{
    /// <summary>Only globally empty strips disappear; regions spanning several cells remain contiguous and do not share cells.</summary>
    /// <param name="Horizontal">Whether compaction operates on rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void CompactionRemovesWholeStripsAndRestoresGapsExactly(bool Horizontal)
    {
        var (Nodes, Grid) = Fixture(Horizontal);
        var Before = Grid.Capture();
        Assert.That(FlowchartGridCompaction.ApplyCells(Nodes, Horizontal, 90), Is.True);
        var A = Grid.RegionOf("A"); var B = Grid.RegionOf("B");
        Assert.That(Horizontal ? B.StartRow : B.StartColumn, Is.EqualTo(2));
        Assert.That(A.Overlaps(B), Is.False);
        Assert.That(Horizontal ? Grid.Capture().RowGaps[1] : Grid.Capture().ColumnGaps[1], Is.EqualTo(90));
        FlowchartCellRepair.Restore(Nodes, Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hinted target rejects the complete move and any gap enlargement without a partially compacted prefix.</summary>
    [Test]
    public void HintedCompactionRejectsRegionsAndGapsTogether()
    {
        var (Nodes, Grid) = Fixture(false);
        Nodes[1].PlacementHintLocked = true;
        var Before = Grid.Capture();
        Assert.That(FlowchartGridCompaction.ApplyCells(Nodes, false, 90), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates an empty strip after a two-cell reservation on the selected cross axis.</summary>
    /// <param name="Horizontal">Whether the empty strip is a row.</param>
    /// <returns>Bound members and their shared grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, Horizontal ? 2 : 1, Horizontal ? 1 : 2));
        Grid.TryPlace("B", new(Horizontal ? 5 : 0, Horizontal ? 0 : 5, 1, 1));
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "A" }, new() { Id = "B" } };
        foreach (var Node in Nodes) Node.BindCells(Grid);
        return (Nodes, Grid);
    }
}
