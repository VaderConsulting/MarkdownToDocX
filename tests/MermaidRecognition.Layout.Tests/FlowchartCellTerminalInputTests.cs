using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic logical placement of independent terminal inputs and accepted-only rank updates.</summary>
[TestFixture]
public sealed class FlowchartCellTerminalInputTests
{
    /// <summary>A side input receives one exclusive whole-cell region beside its collector.</summary>
    [Test]
    public void PlacesInputBesideCollectorBeforeUpdatingItsRank()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Assert.That(FlowchartTerminalInputPlacement.ApplyCells(Contract, Nodes, false, 30), Is.True);
        Assert.That(Grid.RegionOf("R"), Is.EqualTo(new FlowchartCellRegion(2, 1, 1, 1)));
        Assert.That(Nodes.Single(Node => Node.Id == "R").Rank, Is.EqualTo(2));
    }

    /// <summary>An occupied closest target selects the next legal whole-cell track without sharing cells.</summary>
    [Test]
    public void OccupiedTrackUsesTheNextWholeCell()
    {
        var (Contract, Nodes, Grid) = Fixture(); Grid.TryPlace("O", new(2, 1, 1, 1));
        var O = new DiagramGridLayout.GridNode { Id = "O", Rank = 2 }; O.BindCells(Grid); Nodes.Add(O);
        FlowchartTerminalInputPlacement.ApplyCells(Contract, Nodes, false, 30);
        Assert.That(Grid.RegionOf("R"), Is.EqualTo(new FlowchartCellRegion(2, 2, 1, 1)));
    }

    /// <summary>A hinted input retains its exact reservation and original rank.</summary>
    [Test]
    public void HintedInputRetainsItsOwnershipAndRank()
    {
        var (Contract, Nodes, Grid) = Fixture(); var Root = Nodes.Single(Node => Node.Id == "R"); Root.PlacementHintLocked = true;
        var Before = Grid.RegionOf("R"); int Rank = Root.Rank;
        FlowchartTerminalInputPlacement.ApplyCells(Contract, Nodes, false, 30);
        Assert.That(Grid.RegionOf("R"), Is.SameAs(Before)); Assert.That(Root.Rank, Is.EqualTo(Rank));
    }

    /// <summary>Creates an independent input and a collector fed by a branching upstream path.</summary>
    /// <returns>Contract, bound nodes and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Row, Column) in new[] { ("R", 0, 0), ("I", 0, -2), ("P", 1, -2), ("T", 2, 0), ("S", 2, -2) })
        {
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "R", TargetId = "T" }, new() { SourceId = "I", TargetId = "P" },
            new() { SourceId = "P", TargetId = "T" }, new() { SourceId = "P", TargetId = "S" }]);
        return (Contract, Nodes, Grid);
    }
}
