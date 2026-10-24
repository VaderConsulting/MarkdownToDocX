namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete whole-cell chain wraps and exact region/gap rollback on hint or aspect rejection.</summary>
[TestFixture]
public sealed class FlowchartCellChainWrappingTests
{
    /// <summary>A long path wraps into exclusive whole-cell rows/columns without changing individual spans.</summary>
    /// <param name="Horizontal">Whether traversal advances across columns first.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void WrapsTheWholePathInOneCellAssignment(bool Horizontal)
    {
        var (Path, Grid) = Fixture(Horizontal);
        Assert.That(FlowchartChainWrapping.ApplyCellPath(Path, Horizontal, 30, 60), Is.True);
        Assert.That(Path.Select(Node => Grid.RegionOf(Node.Id).StartColumn).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Path.Select(Node => Grid.RegionOf(Node.Id).StartRow).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Grid.Capture().Regions.Values.SelectMany((A, Index) => Grid.Capture().Regions.Values.Skip(Index + 1).Select(B => A.Overlaps(B))), Is.All.False);
    }

    /// <summary>A worse aspect ratio rolls back gaps already grown during the trial, along with every region.</summary>
    [Test]
    public void RejectedGapGrowthRestoresTheExactOriginalSnapshot()
    {
        var (Path, Grid) = Fixture(true);
        Assert.That(FlowchartChainWrapping.ApplyCellPath(Path, true, 30, 60), Is.True);
        var Before = Grid.Capture();
        Assert.That(FlowchartChainWrapping.ApplyCellPath(Path, true, 120, 60), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A locked late member rejects all earlier wrap targets and all proposed clearance growth.</summary>
    [Test]
    public void HintedTailRejectsTheWholeWrap()
    {
        var (Path, Grid) = Fixture(true);
        Path[^1].PlacementHintLocked = true;
        var Before = Grid.Capture();
        Assert.That(FlowchartChainWrapping.ApplyCellPath(Path, true, 120, 120), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a twelve-node single-row or single-column path on one authoritative grid.</summary>
    /// <param name="Horizontal">Whether the original path advances across columns.</param>
    /// <returns>Traversal-ordered members and shared grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Path, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Path = new List<DiagramGridLayout.GridNode>();
        for (int Index = 0; Index < 12; Index++)
        {
            string Id = "N" + Index;
            Grid.TryPlace(Id, new(Horizontal ? 0 : Index, Horizontal ? Index : 0, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id }; Node.BindCells(Grid); Path.Add(Node);
        }
        return (Path, Grid);
    }
}
