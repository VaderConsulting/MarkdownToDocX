using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic two-branch decision/terminal-merge placement.</summary>
[TestFixture]
public sealed class FlowchartCellSimpleMergeTests
{
    /// <summary>Preset symmetric lanes and right-branch merge alignment cannot reposition authoritative cell regions.</summary>
    /// <param name="Horizontal">Whether the legacy cross axis is rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void KeepsBranchesAndMergeForScoredCandidates(bool Horizontal)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Invoke(Contract, Nodes, Horizontal);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A blocked or hinted merge retains both original branch reservations.</summary>
    /// <param name="Hint">Whether the merge is hinted rather than blocked.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RejectedMergeLeavesBothBranchesUnchanged(bool Hint)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        if (Hint)
        {
            Nodes.Single(Node => Node.Id == "M").PlacementHintLocked = true;
        }
        else
        {
            Grid.TryPlace("O", new(4, 3, 1, 1)); DiagramGridLayout.GridNode O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        }
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Invoke(Contract, Nodes, false);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Calls the real legacy dispatcher so metadata and position bypasses are both covered.</summary>
    /// <param name="Contract">Diamond and merge topology.</param>
    /// <param name="Nodes">Authoritative members.</param>
    /// <param name="Horizontal">Flow orientation.</param>
    private static void Invoke(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal) =>
        typeof(DiagramGridLayout).GetMethod("AlignSimpleDecisionMerges", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [Contract, Nodes, Horizontal, DiagramGridMetrics.GRID_UNIT_PX]);

    /// <summary>Creates a generic diamond whose two independent branches meet at a terminal merge.</summary>
    /// <returns>Graph, bound members and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract(); FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Rank, int Column) in new[] { ("D", 0, 2), ("L", 1, 4), ("R", 1, 6), ("M", 2, 6) })
        {
            Grid.TryPlace(Id, new(Rank * 2, Column, Id == "D" ? 2 : 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D", Rank = Rank, OldX = Column * 180 }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "L" }, new() { SourceId = "D", TargetId = "R" },
            new() { SourceId = "L", TargetId = "M" }, new() { SourceId = "R", TargetId = "M" }]);
        return (Contract, Nodes, Grid);
    }
}
