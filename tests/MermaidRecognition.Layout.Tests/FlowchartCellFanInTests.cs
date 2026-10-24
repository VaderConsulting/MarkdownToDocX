using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete neighbouring-cell input fans and hard anchor priority.</summary>
[TestFixture]
public sealed class FlowchartCellFanInTests
{
    /// <summary>Independent input order and positions survive the real dispatcher without forced fan symmetry.</summary>
    [Test]
    public void CompactsEveryIndependentInputTogether()
    {
        var (Contract, Nodes, Grid) = Fixture();
        var Before = Grid.Capture();
        DiagramGridLayout.CompactIndependentFanIn(Contract, Nodes, false, DiagramGridMetrics.NODE_GAP_PX);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes.Take(3).Select(Node => Grid.RegionOf(Node.Id).StartColumn), Is.EqualTo(new[] { 0, 3, 6 }));
    }

    /// <summary>A hinted outside input preserves the full anchored pitch instead of moving the hint to a smaller fan.</summary>
    [Test]
    public void HintedInputRetainsTheAnchoredFanPitch()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Nodes[0].PlacementHintLocked = true;
        var Before = Grid.Capture();
        DiagramGridLayout.CompactIndependentFanIn(Contract, Nodes, false, DiagramGridMetrics.NODE_GAP_PX);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>A colliding input reservation rejects all other members rather than publishing a partial fan.</summary>
    [Test]
    public void BlockedInputRejectsTheCompleteFan()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Grid.TryPlace("O", new(0, 2, 1, 1));
        var O = new DiagramGridLayout.GridNode { Id = "O", Rank = 99 }; O.BindCells(Grid); Nodes.Add(O);
        var Before = Grid.Capture();
        DiagramGridLayout.CompactIndependentFanIn(Contract, Nodes, false, DiagramGridMetrics.NODE_GAP_PX);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a widely spaced odd fan into one downstream target.</summary>
    /// <returns>Contract, bound nodes and grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        for (int Index = 0; Index < 3; Index++)
        {
            string Id = "P" + Index; Grid.TryPlace(Id, new(0, Index * 3, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = 0 }; Node.BindCells(Grid); Nodes.Add(Node);
            Contract.EdgesMutable.Add(new EdgeContract { SourceId = Id, TargetId = "T" });
        }
        Grid.TryPlace("T", new(1, 3, 1, 1));
        var Target = new DiagramGridLayout.GridNode { Id = "T", Rank = 1 }; Target.BindCells(Grid); Nodes.Add(Target);
        return (Contract, Nodes, Grid);
    }
}
