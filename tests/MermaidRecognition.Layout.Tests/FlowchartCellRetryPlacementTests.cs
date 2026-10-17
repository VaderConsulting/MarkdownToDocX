using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic horizontal success/retry placement and accepted-only rank updates.</summary>
[TestFixture]
public sealed class FlowchartCellRetryPlacementTests
{
    /// <summary>The retry occupies whole cells below the decision with measured clearance, while success retains the forward lane.</summary>
    [Test]
    public void RetryAndSuccessPublishWithMeasuredClearance()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Assert.That(DiagramGridLayout.AlignCellHorizontalRetryOutcomes(Contract, Nodes), Is.True);
        Assert.That(Grid.RegionOf("R").StartColumn, Is.EqualTo(Grid.RegionOf("D").StartColumn));
        Assert.That(Grid.RegionOf("F").StartRow, Is.EqualTo(Grid.RegionOf("D").StartRow));
        var R = Nodes.Single(Node => Node.Id == "R"); var D = Nodes.Single(Node => Node.Id == "D");
        Assert.That(R.Y - R.Height / 2d - D.Y - D.Height / 2d, Is.GreaterThanOrEqualTo(90));
        Assert.That(R.Rank, Is.EqualTo(D.Rank));
    }

    /// <summary>A hint or occupied retry target rejects the complete automatic move and leaves rank metadata untouched.</summary>
    /// <param name="Hint">Whether rejection comes from a hint rather than occupied cells.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RejectedRetryLeavesAllRegionsAndRanksUnchanged(bool Hint)
    {
        var (Contract, Nodes, Grid) = Fixture(); var Retry = Nodes.Single(Node => Node.Id == "R");
        if (Hint) Retry.PlacementHintLocked = true;
        else
        {
            Grid.TryPlace("O", new(2, 2, 1, 1)); var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        }
        var Before = Grid.Capture(); int Rank = Retry.Rank;
        DiagramGridLayout.AlignCellHorizontalRetryOutcomes(Contract, Nodes);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions)); Assert.That(Retry.Rank, Is.EqualTo(Rank));
    }

    /// <summary>Creates a generic decision with one successful terminal and one retry edge to its sole input.</summary>
    /// <returns>Graph, bound nodes and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Row, Column, Rank) in new[] { ("I", 0, 0, 0), ("D", 0, 2, 1), ("R", 4, 4, 2), ("F", 0, 4, 2) })
        {
            Grid.TryPlace(Id, new(Row, Column, Id == "D" ? 2 : 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D", Rank = Rank }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "I", TargetId = "D" }, new() { SourceId = "D", TargetId = "R" },
            new() { SourceId = "D", TargetId = "F" }, new() { SourceId = "R", TargetId = "I" }]);
        return (Contract, Nodes, Grid);
    }
}
