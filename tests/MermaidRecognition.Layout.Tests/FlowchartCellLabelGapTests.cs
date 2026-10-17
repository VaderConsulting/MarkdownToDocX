using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks measured label-room repair through complete external gaps and exact rejected state.</summary>
[TestFixture]
public sealed class FlowchartCellLabelGapTests
{
    /// <summary>A labelled terminal shortfall grows the shared external gap while retaining the complete region reservations.</summary>
    /// <param name="Horizontal">Whether the cross axis is rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void MeasuredLabelRoomUsesAuthoritativeExternalGaps(bool Horizontal)
    {
        var (Contract, Nodes, Grid) = Fixture(Horizontal); var Before = Grid.Capture();
        Assert.That(DiagramGridLayout.GrowCellTerminalSameRankLabelGaps(Contract, Nodes, Horizontal), Is.True);
        var Options = FlowchartRenderOptions.FromContract(Contract);
        var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Contract.Edges[0].Label!, Options, Options.EdgeLabelFontSizePx);
        double Required = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(Size, Options.EdgeLabelFontSizePx, Horizontal: !Horizontal)
            + DiagramGridRouteBuilder.CORNER_RADIUS_PX;
        double Have = Math.Abs(Horizontal ? Nodes[1].Y - Nodes[0].Y : Nodes[1].X - Nodes[0].X)
            - (Horizontal ? Nodes[0].Height + Nodes[1].Height : Nodes[0].Width + Nodes[1].Width) / 2d;
        Assert.That(Have + 0.5, Is.GreaterThanOrEqualTo(Required));
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Grid.Restore(Before); Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A pinned target rejects both shared-gap and whole-cell alternatives without changing any ownership or gaps.</summary>
    [Test]
    public void HintedTerminalKeepsExactStateOnRejection()
    {
        var (Contract, Nodes, Grid) = Fixture(false); Nodes[1].PlacementHintLocked = true; var Before = Grid.Capture();
        Assert.That(DiagramGridLayout.GrowCellTerminalSameRankLabelGaps(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Private cumulative candidates retain actual rounded growth rather than losing the first repair's unused rounding allowance.</summary>
    [Test]
    public void CumulativeGapPlansPreserveRoundedGrowthExactly()
    {
        var (_, Nodes, Grid) = Fixture(false); var Before = Grid.Capture();
        var First = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(), [new(true, 0, 3, 30)])!;
        var Growth = FlowchartCellRepair.GrownGapsSince(Before, First.After, true);
        Growth[0] += 20;
        var Second = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(),
            Growth.Select(Pair => new FlowchartCellGapGrowth(true, Pair.Key, Pair.Value, 30)).ToArray())!;
        Assert.That(Second.After.ColumnGaps[0], Is.EqualTo(90));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a labelled same-rank terminal pair on adjacent whole cells.</summary>
    /// <param name="Horizontal">Whether to transpose ownership.</param>
    /// <returns>Graph, bound nodes and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Contract = new DiagramContract(); Contract.EdgesMutable.Add(new EdgeContract { SourceId = "A", TargetId = "B", Label = "one\ntwo\nthree\nfour\nfive\nsix" });
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(Horizontal ? 1 : 0, Horizontal ? 0 : 1, 1, 1));
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "A" }, new() { Id = "B" } };
        foreach (var Node in Nodes) Node.BindCells(Grid, new FlowchartRenderContext());
        return (Contract, Nodes, Grid);
    }
}
