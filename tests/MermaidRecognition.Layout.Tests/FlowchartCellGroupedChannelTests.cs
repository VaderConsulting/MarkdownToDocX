using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete channel-aware group placement and individually measured capacity spans.</summary>
[TestFixture]
public sealed class FlowchartCellGroupedChannelTests
{
    /// <summary>Only members with insufficient actual side capacity grow; groups occupy separate columns with nonoverlapping derived frames.</summary>
    [Test]
    public void GroupsUseIndividuallyMeasuredSpansAndACompleteChannel()
    {
        var (Contract, Nodes, Sources, Targets, Grid) = Fixture();
        Assert.That(FlowchartGroupedChannelPlacement.ApplyCellBlock(Nodes, Sources, Targets), Is.True);
        Assert.That(Sources.Select(Node => Grid.RegionOf(Node.Id).RowSpan), Is.All.EqualTo(2));
        Assert.That(Targets.Select(Node => Grid.RegionOf(Node.Id).RowSpan), Is.All.EqualTo(1));
        Assert.That(Grid.RegionOf("other").RowSpan, Is.EqualTo(1));
        Assert.That(Sources.Max(Node => Grid.RegionOf(Node.Id).EndColumn), Is.LessThanOrEqualTo(Targets.Min(Node => Grid.RegionOf(Node.Id).StartColumn)));
        var Frames = new List<DiagramGridLayout.GridFrame>(); DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Frames);
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Contract, Frames), Is.Zero);
    }

    /// <summary>A hinted source resize rejects every source, target and channel-gap proposal unchanged.</summary>
    [Test]
    public void HintedSourceRejectsTheWholeGroupPair()
    {
        var (_, Nodes, Sources, Targets, Grid) = Fixture(); Sources[0].PlacementHintLocked = true;
        var Before = Grid.Capture();
        Assert.That(FlowchartGroupedChannelPlacement.ApplyCellBlock(Nodes, Sources, Targets), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A pinned unrelated gap participant selects a legal whole-column alternative without shifting its geometry.</summary>
    [Test]
    public void HintedGapParticipantRemainsExactThroughChannelSelection()
    {
        var (_, Nodes, Sources, Targets, Grid) = Fixture(); var Other = Nodes.Single(Node => Node.Id == "other"); Other.PlacementHintLocked = true;
        var Region = Grid.RegionOf("other"); var Pixels = (Other.X, Other.Y, Other.Width, Other.Height);
        Assert.That(FlowchartGroupedChannelPlacement.ApplyCellBlock(Nodes, Sources, Targets), Is.True);
        Assert.That(Grid.RegionOf("other"), Is.SameAs(Region));
        Assert.That((Other.X, Other.Y, Other.Width, Other.Height), Is.EqualTo(Pixels));
    }

    /// <summary>The same complete group pair does not inherit obsolete vertical partner extents before channel placement.</summary>
    [Test]
    public void GroupChannelOwnsItsBlockBeforePartnerPreparation()
    {
        var (Contract, Nodes, Sources, Targets, _) = Fixture();
        Assert.That(FlowchartGroupedChannelPlacement.OwnsCompleteCellBlock(Contract, Nodes, Sources, Targets), Is.True);
        Assert.That(FlowchartPartnerRankLayout.Find(Contract, Nodes, false), Is.Empty);
    }

    /// <summary>Creates a complete two-by-three group pair and one distant unrelated node.</summary>
    /// <returns>Graph, all nodes, declared source/target membership and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, DiagramGridLayout.GridNode[] Sources,
        DiagramGridLayout.GridNode[] Targets, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract(); Contract.GroupsMutable.AddRange([new() { Id = "A" }, new() { Id = "B" }]);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Prefix, Count, Column, Rank) in new[] { ("A", 2, 0, 0), ("B", 3, 5, 1) })
        for (int Index = 0; Index < Count; Index++)
        {
            string Id = Prefix + Index; Grid.TryPlace(Id, new(Index, Column, 1, 1));
            Contract.NodesMutable.Add(new NodeContract { Id = Id, Shape = "rect", ParentGroupId = Prefix });
            Contract.Groups.Single(Group => Group.Id == Prefix).ChildNodeIdsMutable.Add(Id);
            var Node = new DiagramGridLayout.GridNode { Id = Id, Shape = "rect", GroupId = Prefix, Rank = Rank }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        var Sources = Nodes.Take(2).ToArray(); var Targets = Nodes.Skip(2).ToArray();
        foreach (var Source in Sources) foreach (var Target in Targets)
            Contract.EdgesMutable.Add(new EdgeContract { SourceId = Source.Id, TargetId = Target.Id });
        Grid.TryPlace("other", new(10, 10, 1, 1)); Contract.NodesMutable.Add(new NodeContract { Id = "other", Shape = "rect" });
        var Other = new DiagramGridLayout.GridNode { Id = "other", Shape = "rect", Rank = 5 }; Other.BindCells(Grid); Nodes.Add(Other);
        return (Contract, Nodes, Sources, Targets, Grid);
    }
}
