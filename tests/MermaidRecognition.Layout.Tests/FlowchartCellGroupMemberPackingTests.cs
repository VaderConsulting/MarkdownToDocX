namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete standalone group grids and pure hinted-candidate rejection.</summary>
[TestFixture]
public sealed class FlowchartCellGroupMemberPackingTests
{
    /// <summary>A tall standalone group receives multiple whole-cell rows and columns in one accepted plan.</summary>
    /// <param name="Count">Number of members, including a block taller than one wrap length.</param>
    [TestCase(9), TestCase(10), TestCase(30)]
    public void PacksWholeGroupIntoExclusiveCellRowsAndColumns(int Count)
    {
        (List<DiagramGridLayout.GridNode>? Members, FlowchartLogicalGrid? Grid) = Fixture(Count);
        var Before = Grid.Capture();
        double BeforeArea = FlowchartCellCandidateScore.Measure(Members, Before, false).Area;
        double BeforeWidth = FlowchartCellCandidateScore.MeasureWidth(Members, Before);
        Assert.That(FlowchartGroupMemberPacking.ApplyCellMembers(Members), Is.True);
        var After = Grid.Capture();
        double AfterArea = FlowchartCellCandidateScore.Measure(Members, After, false).Area;
        double AfterWidth = FlowchartCellCandidateScore.MeasureWidth(Members, After);
        Assert.That(AfterWidth, Is.LessThanOrEqualTo(FlowchartLayoutRules.DiagramUsableWidthPx));
        Assert.That(AfterArea / AfterWidth, Is.LessThan(BeforeArea / BeforeWidth));
        Assert.That(Members.All(Node => Grid.RegionOf(Node.Id).RowSpan == Before.Regions[Node.Id].RowSpan
            && Grid.RegionOf(Node.Id).ColumnSpan == Before.Regions[Node.Id].ColumnSpan), Is.True);
        Assert.That(Members.Select(Node => Grid.RegionOf(Node.Id).StartRow).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Members.Select(Node => Grid.RegionOf(Node.Id).StartColumn).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Grid.Capture().Regions.Values.SelectMany((A, Index) => Grid.Capture().Regions.Values.Skip(Index + 1).Select(B => A.Overlaps(B))), Is.All.False);
        Assert.That(FlowchartGroupMemberPacking.ApplyCellMembers(Members), Is.False, "an accepted pack must be stable on repetition");
        Assert.That(Grid.Capture(), Is.SameAs(After));
    }

    /// <summary>A fixed end member rejects every improving pack without temporarily changing any live regions or gaps.</summary>
    [Test]
    public void RejectedGroupCandidatesKeepExactLiveOwnership()
    {
        (List<DiagramGridLayout.GridNode>? Members, FlowchartLogicalGrid? Grid) = Fixture(); Members[^1].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartGroupMemberPacking.ApplyCellMembers(Members), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a standalone group in one tall logical column.</summary>
    /// <param name="Count">Number of independent cell owners.</param>
    /// <returns>Declared-order members and parent grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Members, FlowchartLogicalGrid Grid) Fixture(int Count = 9)
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); List<DiagramGridLayout.GridNode> Members = [];
        for (int Index = 0; Index < Count; Index++)
        {
            string Id = "N" + Index; Grid.TryPlace(Id, new(Index, 0, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = "G" }; Node.BindCells(Grid); Members.Add(Node);
        }
        return (Members, Grid);
    }
}
