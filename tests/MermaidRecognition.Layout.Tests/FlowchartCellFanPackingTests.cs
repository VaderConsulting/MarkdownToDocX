namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete grid fan placement, directional ownership and rejection without live trial mutations.</summary>
[TestFixture]
public sealed class FlowchartCellFanPackingTests
{
    [TestCase(false), TestCase(true)]
    public void MandatoryCapacityGrowthReportsPublicationEvenWhenTheFanWidens(bool Horizontal)
    {
        var (Nodes, Hub, Leaves, Grid) = Fixture(20);
        var Before = Grid.Capture();
        double Width = FlowchartCellCandidateScore.MeasureWidth(Nodes, Before);
        Assert.That(FlowchartFanPacking.ApplyCellFan(Nodes, Hub, Leaves, true, Horizontal, false, 120, 6), Is.True);
        var After = Grid.Capture();
        Assert.That(After, Is.Not.SameAs(Before));
        Assert.That(FlowchartCellCandidateScore.MeasureWidth(Nodes, After), Is.GreaterThan(Width));
        Assert.That(FlowchartFanPacking.ApplyCellFan(Nodes, Hub, Leaves, true, Horizontal, false, 120, 6), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(After));
    }

    [Test]
    public void UnrelatedWideComponentCannotMaskTheLocalFanPackOrItsSuccess()
    {
        var (Nodes, Hub, Leaves, Grid) = Fixture(8);
        Assert.That(Grid.TryPlace("Independent", new(0, 100, 1, 1)), Is.True);
        var Other = new DiagramGridLayout.GridNode { Id = "Independent" }; Other.BindCells(Grid); Nodes.Add(Other);
        var Before = Grid.Capture();
        var OtherGeometry = (Other.X, Other.Y, Other.Width, Other.Height);
        double Width = FlowchartCellCandidateScore.MeasureWidth(Nodes, Before);
        Assert.That(FlowchartFanPacking.ApplyCellFan(Nodes, Hub, Leaves, true, false, false, 60, 6), Is.True);
        Assert.That(Grid.Capture(), Is.Not.SameAs(Before));
        Assert.That(FlowchartCellCandidateScore.MeasureWidth(Nodes, Grid.Capture()), Is.EqualTo(Width));
        Assert.That((Other.X, Other.Y, Other.Width, Other.Height), Is.EqualTo(OtherGeometry));
        Assert.That(Grid.RegionOf("Independent"), Is.EqualTo(Before.Regions["Independent"]));
        Assert.That(Leaves.Select(Node => Grid.RegionOf(Node.Id).StartColumn).Distinct().Count(), Is.GreaterThan(1));
    }

    /// <summary>Large fans occupy both rows and columns while only measured hub capacity changes its spans.</summary>
    /// <param name="Count">Declared simple fan size.</param>
    /// <param name="Horizontal">Whether primary flow uses columns.</param>
    [TestCase(20, false)]
    [TestCase(30, false)]
    [TestCase(20, true)]
    [TestCase(30, true)]
    public void LargeFanUsesWholeCellColumnsAndRows(int Count, bool Horizontal)
    {
        (List<DiagramGridLayout.GridNode>? Nodes, DiagramGridLayout.GridNode? Hub, List<DiagramGridLayout.GridNode>? Leaves, FlowchartLogicalGrid? Grid) = Fixture(Count);
        int TrackGap = ((Count + DiagramGridMetrics.TRACKS_PER_GAP - 1) / DiagramGridMetrics.TRACKS_PER_GAP) * DiagramGridMetrics.GRID_UNIT_PX;
        Assert.That(FlowchartFanPacking.ApplyCellFan(Nodes, Hub, Leaves, true, Horizontal, false, TrackGap, 6), Is.True);
        Assert.That(Leaves.Select(Node => Grid.RegionOf(Node.Id).StartColumn).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Leaves.Select(Node => Grid.RegionOf(Node.Id).StartRow).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Leaves.Select(Node => Grid.RegionOf(Node.Id).RowSpan), Is.All.EqualTo(1));
        Assert.That(Leaves.Select(Node => Grid.RegionOf(Node.Id).ColumnSpan), Is.All.EqualTo(1));
        Assert.That(Horizontal ? Grid.RegionOf(Hub.Id).RowSpan : Grid.RegionOf(Hub.Id).ColumnSpan, Is.GreaterThan(1));
    }

    /// <summary>Both fan-in and fan-out preserve physical flow direction in all four diagram orientations.</summary>
    /// <param name="Horizontal">Whether primary flow uses columns.</param>
    /// <param name="FanOut">Whether links leave rather than enter the hub.</param>
    /// <param name="Reverse">Whether physical flow runs towards smaller coordinates.</param>
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    public void FanGridKeepsDeclaredDirection(bool Horizontal, bool FanOut, bool Reverse)
    {
        (List<DiagramGridLayout.GridNode>? Nodes, DiagramGridLayout.GridNode? Hub, List<DiagramGridLayout.GridNode>? Leaves, FlowchartLogicalGrid _) = Fixture(8);
        Assert.That(FlowchartFanPacking.ApplyCellFan(Nodes, Hub, Leaves, FanOut, Horizontal, Reverse, 60, 6), Is.True);
        double Delta = Leaves.Average(Node => Horizontal ? Node.X : Node.Y) - (Horizontal ? Hub.X : Hub.Y);
        Assert.That(Math.Sign(Delta), Is.EqualTo(Reverse == FanOut ? -1 : 1));
    }

    /// <summary>A required hinted hub resize rejects every candidate without changing live cells or gaps during scoring.</summary>
    [Test]
    public void RejectedFanCandidatesLeaveExactLiveStateUntouched()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, DiagramGridLayout.GridNode? Hub, List<DiagramGridLayout.GridNode>? Leaves, FlowchartLogicalGrid? Grid) = Fixture(20); Hub.PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartFanPacking.ApplyCellFan(Nodes, Hub, Leaves, true, false, false, 120, 6), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a tall single-column fan with independent one-cell leaves.</summary>
    /// <param name="Count">Leaf count.</param>
    /// <returns>All nodes, hub, leaves and their shared grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Nodes, DiagramGridLayout.GridNode Hub,
        List<DiagramGridLayout.GridNode> Leaves, FlowchartLogicalGrid Grid) Fixture(int Count)
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("Hub", new(0, 0, 1, 1));
        DiagramGridLayout.GridNode Hub = new DiagramGridLayout.GridNode { Id = "Hub" }; Hub.BindCells(Grid);
        List<DiagramGridLayout.GridNode> Nodes = [Hub]; List<DiagramGridLayout.GridNode> Leaves = [];
        for (int Index = 0; Index < Count; Index++)
        {
            string Id = "L" + Index; Grid.TryPlace(Id, new(Index + 1, 0, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id }; Node.BindCells(Grid); Nodes.Add(Node); Leaves.Add(Node);
        }
        return (Nodes, Hub, Leaves, Grid);
    }
}
