using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete grouped-leaf reservation, frame-cache publication and bounded rejected candidates.</summary>
[TestFixture]
public sealed class FlowchartCellGroupedLeafTests
{
    /// <summary>A local leaf moves beside a grouped boundary endpoint by whole cells and updates its rank only after acceptance.</summary>
    [Test]
    public void LocalLeafAndDerivedFramesPublishTogether()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); List<DiagramGridLayout.GridFrame> Frames = [];
        Assert.That(FlowchartGroupedLeafPlacement.Apply(Contract, Nodes, Frames, false), Is.True);
        Assert.That(Grid.RegionOf("L").StartRow, Is.EqualTo(Grid.RegionOf("P").StartRow));
        Assert.That(Math.Abs(Grid.RegionOf("L").StartColumn - Grid.RegionOf("P").StartColumn), Is.EqualTo(1));
        Assert.That(Nodes.Single(Node => Node.Id == "L").Rank, Is.EqualTo(Nodes.Single(Node => Node.Id == "P").Rank));
        List<DiagramGridLayout.GridFrame> Actual = []; DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Actual);
        Assert.That(Frames.Select(Frame => (Frame.Id, Frame.Left, Frame.Top, Frame.Right, Frame.Bottom)),
            Is.EqualTo(Actual.Select(Frame => (Frame.Id, Frame.Left, Frame.Top, Frame.Right, Frame.Bottom))));
    }

    /// <summary>A hinted leaf retains exact ownership, rank and frame cache.</summary>
    [Test]
    public void HintedLeafLeavesExactStateAndCacheUnchanged()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); DiagramGridLayout.GridNode Leaf = Nodes.Single(Node => Node.Id == "L"); Leaf.PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); int Rank = Leaf.Rank; DiagramGridLayout.GridFrame Frame = new DiagramGridLayout.GridFrame { Id = "sentinel" };
        List<DiagramGridLayout.GridFrame> Frames = [Frame];
        Assert.That(FlowchartGroupedLeafPlacement.Apply(Contract, Nodes, Frames, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before)); Assert.That(Leaf.Rank, Is.EqualTo(Rank)); Assert.That(Frames.Single(), Is.SameAs(Frame));
    }

    /// <summary>A competing frame rejects that side before a move leaks, while the clear side remains available under the named limit.</summary>
    [Test]
    public void CompetingFrameKeepsRejectedSidePrivate()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); GroupContract Group = new GroupContract { Id = "H" }; Group.ChildNodeIdsMutable.Add("O"); Contract.GroupsMutable.Add(Group);
        Contract.NodesMutable.Add(new NodeContract { Id = "O", ParentGroupId = "H" }); Grid.TryPlace("O", new(0, -2, 1, 1));
        DiagramGridLayout.GridNode O = new DiagramGridLayout.GridNode { Id = "O", GroupId = "H" }; O.BindCells(Grid, Nodes[0].PixelWriteContext); Nodes.Add(O);
        Assert.That(FlowchartGroupedLeafPlacement.Apply(Contract, Nodes, [], false), Is.True);
        Assert.That(Grid.RegionOf("L").StartColumn, Is.EqualTo(1));
        Assert.That(Nodes[0].PixelWriteContext!.CellLeafCandidateMoves, Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
    }

    /// <summary>Creates a local terminal leaf and a boundary-crossing continuation from the same grouped parent.</summary>
    /// <returns>Contract, bound nodes and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract(); GroupContract Group = new GroupContract { Id = "G" }; Group.ChildNodeIdsMutable.AddRange(["P", "L"]); Contract.GroupsMutable.Add(Group);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); FlowchartRenderContext Context = new FlowchartRenderContext();
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, string? GroupId, int Row, int Column) in new[] { ("P", (string?)"G", 0, 0), ("L", "G", 3, 0), ("X", null, 0, 4) })
        {
            Contract.NodesMutable.Add(new NodeContract { Id = Id, ParentGroupId = GroupId }); Grid.TryPlace(Id, new(Row, Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = GroupId, Rank = Row }; Node.BindCells(Grid, Context); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "P", TargetId = "L" }, new() { SourceId = "P", TargetId = "X" }]);
        return (Contract, Nodes, Grid);
    }
}
