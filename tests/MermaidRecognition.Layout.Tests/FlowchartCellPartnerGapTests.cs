using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic partner-channel gap growth and exact rejection around pinned ownership.</summary>
[TestFixture]
public sealed class FlowchartCellPartnerGapTests
{
    /// <summary>Measured track demand grows an external gap, retaining all reservations and restoring exact state on rollback.</summary>
    [Test]
    public void GrowsExternalChannelWithoutMovingReservations()
    {
        var (Blocks, Nodes, Grid) = Fixture(); var Before = Grid.Capture();
        Assert.That(FlowchartPartnerRankLayout.GrowCellGaps(new(), Nodes, [], Blocks, false), Is.True);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Grid.Capture().RowGaps[0], Is.GreaterThan(Grid.Configuration.RowGap));
        Grid.Restore(Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A pinned participant rejects the whole proposed gap and preserves the exact frame cache as well.</summary>
    [Test]
    public void HintedGapParticipantRejectsAllGrowth()
    {
        var (Blocks, Nodes, Grid) = Fixture(); Nodes[^1].PlacementHintLocked = true;
        var Before = Grid.Capture(); var Frame = new DiagramGridLayout.GridFrame { Id = "sentinel" };
        var Frames = new List<DiagramGridLayout.GridFrame> { Frame };
        Assert.That(FlowchartPartnerRankLayout.GrowCellGaps(new(), Nodes, Frames, Blocks, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Frames.Single(), Is.SameAs(Frame));
    }

    /// <summary>Two already satisfied channels do not grow gaps merely because a many-to-many block exists.</summary>
    [Test]
    public void SufficientChannelLeavesStateUntouched()
    {
        var (Blocks, Nodes, Grid) = Fixture();
        Grid.GrowGap(false, 0, 600, 30);
        var Before = Grid.Capture();
        Assert.That(FlowchartPartnerRankLayout.GrowCellGaps(new(), Nodes, [], Blocks, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates one complete three-by-three block on adjacent rows.</summary>
    /// <returns>Partner demand, bound nodes and parent grid.</returns>
    private static (List<FlowchartPartnerRankLayout.Partners> Blocks, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (string Prefix in new[] { "P", "C" })
        for (int Index = 0; Index < 3; Index++)
        {
            string Id = Prefix + Index; int Row = Prefix == "P" ? 0 : 1;
            Grid.TryPlace(Id, new(Row, Index * 2, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        return ([new(Nodes.Take(3).ToArray(), Nodes.Skip(3).ToArray(), 18,
            new Dictionary<int, int> { [0] = 0, [1] = 1, [2] = 2 })], Nodes, Grid);
    }
}
