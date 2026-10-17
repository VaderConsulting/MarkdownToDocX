namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic partner-rank span/placement preparation and safe rejection to ordinary allocation.</summary>
[TestFixture]
public sealed class FlowchartCellPartnerPreparationTests
{
    /// <summary>Both ranks grow and move together, preserving aligned partner starts without sharing cells.</summary>
    [Test]
    public void PartnerExtentsAndBothRanksPublishTogether()
    {
        var (Blocks, Nodes, Grid) = Fixture();
        Assert.That(FlowchartPartnerRankLayout.PrepareCells(Blocks, false), Is.True);
        Assert.That(Nodes.Select(Node => Grid.RegionOf(Node.Id).ColumnSpan), Is.All.EqualTo(2));
        Assert.That(Grid.RegionOf("P1").StartColumn, Is.EqualTo(2));
        Assert.That(Grid.RegionOf("C1").StartColumn, Is.EqualTo(Grid.RegionOf("P1").StartColumn));
    }

    /// <summary>A fixed unrelated reservation rejects both ranks and all span changes without partial preparation.</summary>
    [Test]
    public void OccupiedPartnerTargetLeavesExactOwnershipUnchanged()
    {
        var (Blocks, _, Grid) = Fixture();
        Grid.TryPlace("O", new(0, 3, 1, 1));
        var Before = Grid.Capture();
        Assert.That(FlowchartPartnerRankLayout.PrepareCells(Blocks, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hinted resize rejects specialised preparation, leaving the ordinary allocator responsible for unchanged declared links.</summary>
    [Test]
    public void HintedPreparationFallsBackWithoutStaleSpecialisedBlocks()
    {
        var (Blocks, Nodes, Grid) = Fixture(); Nodes[0].PlacementHintLocked = true;
        var Before = Grid.Capture();
        FlowchartPartnerRankLayout.Prepare(Blocks, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Blocks, Is.Empty);
    }

    /// <summary>Creates a complete two-by-two block whose measured track ports require wider whole-cell reservations.</summary>
    /// <returns>Detected partner block, its members and shared grid.</returns>
    private static (List<FlowchartPartnerRankLayout.Partners> Blocks, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Row, Column) in new[] { ("P0", 0, 0), ("P1", 0, 1), ("C0", 2, 0), ("C1", 2, 1) })
        {
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        var Block = new FlowchartPartnerRankLayout.Partners(Nodes.Take(2).ToArray(), Nodes.Skip(2).ToArray(), 50,
            new Dictionary<int, int> { [0] = 0, [1] = 1 });
        return ([Block], Nodes, Grid);
    }
}
