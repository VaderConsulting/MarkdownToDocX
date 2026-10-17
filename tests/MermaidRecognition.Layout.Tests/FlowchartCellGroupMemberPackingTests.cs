namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete standalone group grids and pure hinted-candidate rejection.</summary>
[TestFixture]
public sealed class FlowchartCellGroupMemberPackingTests
{
    /// <summary>A tall standalone group receives multiple whole-cell rows and columns in one accepted plan.</summary>
    [Test]
    public void PacksWholeGroupIntoExclusiveCellRowsAndColumns()
    {
        var (Members, Grid) = Fixture();
        Assert.That(FlowchartGroupMemberPacking.ApplyCellMembers(Members), Is.True);
        Assert.That(Members.Select(Node => Grid.RegionOf(Node.Id).StartRow).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Members.Select(Node => Grid.RegionOf(Node.Id).StartColumn).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Grid.Capture().Regions.Values.SelectMany((A, Index) => Grid.Capture().Regions.Values.Skip(Index + 1).Select(B => A.Overlaps(B))), Is.All.False);
    }

    /// <summary>A fixed end member rejects every improving pack without temporarily changing any live regions or gaps.</summary>
    [Test]
    public void RejectedGroupCandidatesKeepExactLiveOwnership()
    {
        var (Members, Grid) = Fixture(); Members[^1].PlacementHintLocked = true;
        var Before = Grid.Capture();
        Assert.That(FlowchartGroupMemberPacking.ApplyCellMembers(Members), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a nine-member standalone group in one tall logical column.</summary>
    /// <returns>Declared-order members and parent grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Members, FlowchartLogicalGrid Grid) Fixture()
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); var Members = new List<DiagramGridLayout.GridNode>();
        for (int Index = 0; Index < 9; Index++)
        {
            string Id = "N" + Index; Grid.TryPlace(Id, new(Index, 0, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = "G" }; Node.BindCells(Grid); Members.Add(Node);
        }
        return (Members, Grid);
    }
}
