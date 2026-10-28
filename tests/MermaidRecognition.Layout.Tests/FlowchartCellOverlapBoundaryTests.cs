using System.Reflection;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that legacy rank-band separation cannot overwrite legal authoritative cell ownership.</summary>
[TestFixture]
public sealed class FlowchartCellOverlapBoundaryTests
{
    /// <summary>Different native ranks may legally share one row in separate columns after a relative placement hint.</summary>
    /// <param name="Horizontal">Whether to transpose the logical ownership.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void DisjointRegionsDoNotMoveToFitLegacyRankBands(bool Horizontal)
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(Horizontal ? 1 : 0, Horizontal ? 0 : 1, 1, 1));
        DiagramGridLayout.GridNode A = new DiagramGridLayout.GridNode { Id = "A", Rank = 0, PlacementHintLocked = true };
        DiagramGridLayout.GridNode B = new DiagramGridLayout.GridNode { Id = "B", Rank = 1 };
        A.BindCells(Grid); B.BindCells(Grid);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); (double, double, double, double) Pixels = (A.X, A.Y, B.X, B.Y);
        typeof(DiagramGridLayout).GetMethod("SeparateOverlaps", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [new List<DiagramGridLayout.GridNode> { A, B }, Horizontal]);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That((A.X, A.Y, B.X, B.Y), Is.EqualTo(Pixels));
        Assert.That(Grid.RegionOf("A").Overlaps(Grid.RegionOf("B")), Is.False);
    }
}
