namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic partner-rank span/placement preparation and safe rejection to ordinary allocation.</summary>
[TestFixture]
public sealed class FlowchartCellPartnerPreparationTests
{
    /// <summary>Capacity clearance packs each rank independently rather than aligning partners that started in different columns.</summary>
    [Test]
    public void CapacityGrowthDoesNotForcePartnerAlignment()
    {
        (List<FlowchartPartnerRankLayout.Partners>? Blocks, List<DiagramGridLayout.GridNode> _, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(Grid.TryMove(["C0", "C1"], 0, 1), Is.True);
        Assert.That(FlowchartPartnerRankLayout.PrepareCells(Blocks, false), Is.True);
        Assert.That(Grid.RegionOf("P0").StartColumn, Is.EqualTo(0));
        Assert.That(Grid.RegionOf("P1").StartColumn, Is.EqualTo(2));
        Assert.That(Grid.RegionOf("C0").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("C1").StartColumn, Is.EqualTo(3));
        Assert.That(Blocks[0].AlignedChildren, Is.Empty);
    }

    /// <summary>Both ranks grow and move together, preserving aligned partner starts without sharing cells.</summary>
    [Test]
    public void PartnerExtentsAndBothRanksPublishTogether()
    {
        (List<FlowchartPartnerRankLayout.Partners>? Blocks, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(FlowchartPartnerRankLayout.PrepareCells(Blocks, false), Is.True);
        Assert.That(Nodes.Select(Node => Grid.RegionOf(Node.Id).ColumnSpan), Is.All.EqualTo(2));
        Assert.That(Grid.RegionOf("P1").StartColumn, Is.EqualTo(2));
        Assert.That(Grid.RegionOf("C1").StartColumn, Is.EqualTo(Grid.RegionOf("P1").StartColumn));
    }

    /// <summary>A fixed unrelated reservation rejects both ranks and all span changes without partial preparation.</summary>
    [Test]
    public void OccupiedPartnerTargetLeavesExactOwnershipUnchanged()
    {
        (List<FlowchartPartnerRankLayout.Partners>? Blocks, List<DiagramGridLayout.GridNode> _, FlowchartLogicalGrid? Grid) = Fixture();
        Grid.TryPlace("O", new(0, 3, 1, 1));
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartPartnerRankLayout.PrepareCells(Blocks, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hinted resize rejects specialised preparation, leaving the ordinary allocator responsible for unchanged declared links.</summary>
    [Test]
    public void HintedPreparationFallsBackWithoutStaleSpecialisedBlocks()
    {
        (List<FlowchartPartnerRankLayout.Partners>? Blocks, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); Nodes[0].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        FlowchartPartnerRankLayout.Prepare(Blocks, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Blocks, Is.Empty);
    }

    /// <summary>Creates a complete two-by-two block whose measured track ports require wider whole-cell reservations.</summary>
    /// <returns>Detected partner block, its members and shared grid.</returns>
    private static (List<FlowchartPartnerRankLayout.Partners> Blocks, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Row, int Column) in new[] { ("P0", 0, 0), ("P1", 0, 1), ("C0", 2, 0), ("C1", 2, 1) })
        {
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        FlowchartPartnerRankLayout.Partners Block = new FlowchartPartnerRankLayout.Partners([.. Nodes.Take(2)], [.. Nodes.Skip(2)], 50,
            new Dictionary<int, int> { [0] = 0, [1] = 1 });
        return ([Block], Nodes, Grid);
    }
}
