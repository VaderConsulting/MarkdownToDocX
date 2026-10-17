using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks one complete centred fan/input-chain plan and exact rejection of late collisions.</summary>
[TestFixture]
public sealed class FlowchartCellFanAlignmentTests
{
    /// <summary>An odd fan centres its parent and sole upstream chain on the middle child's whole-cell start.</summary>
    [Test]
    public void CentresParentAndChainTogether()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Assert.That(DiagramGridLayout.CentreCellTerminalFans(Contract, Nodes, false), Is.True);
        Assert.That(Grid.RegionOf("P").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("U").StartColumn, Is.EqualTo(1));
        Assert.That(Nodes.Where(Node => Node.Id is "P" or "U").Select(Node => Node.Column), Is.All.EqualTo(1));
    }

    /// <summary>A blocked upstream target rejects the centred parent too, preserving ownership and ordering metadata.</summary>
    [Test]
    public void BlockedChainRejectsTheWholeFanPlan()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Grid.TryPlace("O", new(0, 1, 1, 1));
        var Obstacle = new DiagramGridLayout.GridNode { Id = "O" }; Obstacle.BindCells(Grid); Nodes.Add(Obstacle);
        var Before = Grid.Capture(); var Columns = Nodes.Select(Node => Node.Column).ToArray();
        Assert.That(DiagramGridLayout.CentreCellTerminalFans(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
    }

    /// <summary>Creates a three-way terminal fan and an unbranched upstream input.</summary>
    /// <returns>Contract, bound members and grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Rank, Column) in new[] { ("U", 0, 0), ("P", 1, 0), ("A", 2, 0), ("B", 2, 1), ("C", 2, 2) })
        {
            Grid.TryPlace(Id, new(Rank, Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Rank, Column = Column }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "U", TargetId = "P" }, new() { SourceId = "P", TargetId = "A" },
            new() { SourceId = "P", TargetId = "B" }, new() { SourceId = "P", TargetId = "C" }]);
        return (Contract, Nodes, Grid);
    }
}
