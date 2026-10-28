namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic conversion of measured clearance extents to whole-cell span requirements.</summary>
[TestFixture]
public sealed class FlowchartCellExtentGrowthTests
{
    /// <summary>Measured extents choose complete fitting spans without enlarging fixed base cells or the unaffected node.</summary>
    [Test]
    public void GrowsOnlyRequiredWholeCellSpans()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(DiagramGridLayout.GrowCellMinimumExtents(Nodes, new Dictionary<string, (double, double)> { ["A"] = (301, 121) }), Is.True);
        Assert.That(Grid.RegionOf("A"), Is.EqualTo(new FlowchartCellRegion(0, 0, 2, 2)));
        Assert.That(Grid.RegionOf("B").ColumnSpan, Is.EqualTo(1));
        Assert.That(Grid.Configuration.CellWidth, Is.EqualTo(150));
    }

    /// <summary>A colliding resize rejects every affected node and leaves the exact cells and gaps unchanged.</summary>
    [Test]
    public void OccupiedResizeRejectsEveryMeasuredExtent()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Grid.TryApply(new Dictionary<string, FlowchartCellRegion> { ["B"] = new(0, 1, 1, 1) });
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(DiagramGridLayout.GrowCellMinimumExtents(Nodes, new Dictionary<string, (double, double)>
            { ["A"] = (301, 90), ["B"] = (150, 121) }), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hinted extent change rejects the complete automatic plan.</summary>
    [Test]
    public void HintedNodeKeepsItsReservation()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); Nodes[0].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(DiagramGridLayout.GrowCellMinimumExtents(Nodes, new Dictionary<string, (double, double)> { ["A"] = (301, 90) }), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates two separate reservations with room for a legal first-node resize.</summary>
    /// <returns>Bound nodes and shared grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(0, 4, 1, 1));
        List<DiagramGridLayout.GridNode> Nodes = [new() { Id = "A" }, new() { Id = "B" }];
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.BindCells(Grid);
        }

        return (Nodes, Grid);
    }
}
