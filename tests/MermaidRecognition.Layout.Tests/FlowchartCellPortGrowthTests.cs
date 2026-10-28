namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks measured whole-cell port-capacity growth and complete rejection of occupied spans.</summary>
[TestFixture]
public sealed class FlowchartCellPortGrowthTests
{
    /// <summary>Actual crowded ports grow just the affected node by whole cells.</summary>
    [Test]
    public void MeasuredShortfallGrowsOneReservation()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(150);
        FlowchartCellRegion Other = Grid.RegionOf("B");
        Assert.That(DiagramGridLayout.GrowCellCrowdedSides(Nodes, new Dictionary<string, int> { ["A|top"] = 7 }, 150, 90), Is.True);
        Assert.That(Grid.RegionOf("A").ColumnSpan, Is.EqualTo(2));
        Assert.That(Grid.RegionOf("B"), Is.SameAs(Other));
    }

    /// <summary>The same allocation count does not grow an already sufficient connection boundary.</summary>
    [Test]
    public void NeighbourCountAloneDoesNotGrowAnAvailableBoundary()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(330);
        FlowchartCellRegion Before = Grid.RegionOf("A");
        DiagramGridLayout.GrowCellCrowdedSides(Nodes, new Dictionary<string, int> { ["A|top"] = 7 }, 150, 90);
        Assert.That(Grid.RegionOf("A"), Is.SameAs(Before));
    }

    /// <summary>A resize that would reserve a neighbour's cells leaves the complete grid and gaps unchanged.</summary>
    [Test]
    public void OccupiedGrowthRejectsTheCompletePlan()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(150);
        Grid.TryApply(new Dictionary<string, FlowchartCellRegion> { ["B"] = new(0, 1, 1, 1) });
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(DiagramGridLayout.GrowCellCrowdedSides(Nodes, new Dictionary<string, int> { ["A|top"] = 7 }, 150, 90), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a potentially crowded node and a distant unaffected reservation on fixed configured cells.</summary>
    /// <param name="Width">Configured cell width, held fixed throughout each trial.</param>
    /// <returns>Bound members and grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(double Width)
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules() with { CellWidth = Width });
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(0, 4, 1, 1));
        List<DiagramGridLayout.GridNode> Nodes = [new() { Id = "A" }, new() { Id = "B" }];
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.BindCells(Grid);
        }

        return (Nodes, Grid);
    }
}
