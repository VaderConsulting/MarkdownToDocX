using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks ordering-only source representatives and unpublished whole-cell routing alternatives.</summary>
[TestFixture]
public sealed class FlowchartCellRouteCandidateTests
{
    /// <summary>Even spans retain fractional ordering values without creating fractional occupied cells.</summary>
    /// <param name="Horizontal">Whether rows are the cross axis.</param>
    /// <param name="Expected">Representative of the inclusive occupied interval.</param>
    [TestCase(false, 5.5)]
    [TestCase(true, 3.5)]
    public void RepresentativeUsesInclusiveOccupiedInterval(bool Horizontal, double Expected) =>
        Assert.That(DiagramGridLayout.CellSourceRepresentative(new(2, 4, 4, 4), Horizontal), Is.EqualTo(Expected));

    /// <summary>Each source supplies one representative before averaging; a wide source is not counted as several sources.</summary>
    [Test]
    public void SourceTieUsesMeanOfSeparateRepresentatives()
    {
        var Contract = new DiagramContract(); Contract.EdgesMutable.AddRange([new() { SourceId = "A", TargetId = "T" }, new() { SourceId = "B", TargetId = "T" }]);
        var Regions = new Dictionary<string, FlowchartCellRegion> { ["A"] = new(0, 0, 1, 4), ["B"] = new(0, 10, 1, 1), ["T"] = new(2, 20, 1, 1) };
        Assert.That(DiagramGridLayout.CellSourceOrder(Contract, Regions, "T", false), Is.EqualTo(5.75));
        Contract.EdgesMutable.Add(new() { SourceId = "A", TargetId = "T" });
        Assert.That(DiagramGridLayout.CellSourceOrder(Contract, Regions, "T", false), Is.EqualTo(1.5), "The source supplying most lines takes precedence.");
    }

    /// <summary>Peer starts and far boundaries are offered as whole-cell candidates without moving the target or writing ordering metadata.</summary>
    [Test]
    public void PeerBoundariesAreProposalsOnly()
    {
        var (Contract, Nodes, Grid) = Fixture(); var Before = Grid.Capture();
        var Options = DiagramGridLayout.CellRouteMoveCandidates(Contract, Nodes, false, ["B"]).ToArray();
        Assert.That(Options.Any(Option => Option.Nodes.Count == 1 && Option.Rows == 0 && Option.Columns == -8), Is.True);
        Assert.That(Options.Any(Option => Option.Nodes.Count == 1 && Option.Rows == 0 && Option.Columns == -6), Is.True);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(8));
    }

    /// <summary>Hard hints exclude complete units before a candidate can reach routed scoring.</summary>
    [Test]
    public void HintedUnitIsNotProposed()
    {
        var (Contract, Nodes, Grid) = Fixture(); Nodes[1].PlacementHintLocked = true; var Before = Grid.Capture();
        Assert.That(DiagramGridLayout.CellRouteMoveCandidates(Contract, Nodes, false, ["B"]), Is.Empty);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Independent nominated endpoints receive alternatives before one endpoint consumes the full candidate budget.</summary>
    [Test]
    public void NominatedUnitsAreInterleaved()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Grid.TryPlace("C", new(4, 12, 1, 1)); var C = new DiagramGridLayout.GridNode { Id = "C", Rank = 2 }; C.BindCells(Grid); Nodes.Add(C);
        Contract.NodesMutable.Add(new() { Id = "C" }); Contract.EdgesMutable.Add(new() { SourceId = "B", TargetId = "C" });
        var First = DiagramGridLayout.CellRouteMoveCandidates(Contract, Nodes, false, ["B", "C"]).Take(3).Select(Option => Option.Subject).ToArray();
        Assert.That(First, Does.Contain("B")); Assert.That(First, Does.Contain("C"));
    }

    /// <summary>Creates an unequal-span edge with a displaced terminal region.</summary>
    /// <returns>Complete source, authoritative nodes and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); Contract.NodesMutable.AddRange([new() { Id = "A" }, new() { Id = "B" }]); Contract.EdgesMutable.Add(new() { SourceId = "A", TargetId = "B" });
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); Grid.TryPlace("A", new(0, 0, 1, 3)); Grid.TryPlace("B", new(2, 8, 1, 1));
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "A", Rank = 0 }, new() { Id = "B", Rank = 1 } };
        foreach (var Node in Nodes) Node.BindCells(Grid);
        return (Contract, Nodes, Grid);
    }
}
