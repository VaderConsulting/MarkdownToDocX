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
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, DiagramGridLayout.GridNode[]? Sources, DiagramGridLayout.GridNode[]? Targets, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(FlowchartGroupedChannelPlacement.ApplyCellBlock(Nodes, Sources, Targets), Is.True);
        Assert.That(Sources.Select(Node => Grid.RegionOf(Node.Id).RowSpan), Is.All.EqualTo(2));
        Assert.That(Targets.Select(Node => Grid.RegionOf(Node.Id).RowSpan), Is.All.EqualTo(1));
        Assert.That(Grid.RegionOf("other").RowSpan, Is.EqualTo(1));
        Assert.That(Sources.Max(Node => Grid.RegionOf(Node.Id).EndColumn), Is.LessThanOrEqualTo(Targets.Min(Node => Grid.RegionOf(Node.Id).StartColumn)));
        List<DiagramGridLayout.GridFrame> Frames = []; DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Frames);
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Contract, Frames), Is.Zero);
    }

    /// <summary>A hinted source resize rejects every source, target and channel-gap proposal unchanged.</summary>
    [Test]
    public void HintedSourceRejectsTheWholeGroupPair()
    {
        (DiagramContract _, List<DiagramGridLayout.GridNode>? Nodes, DiagramGridLayout.GridNode[]? Sources, DiagramGridLayout.GridNode[]? Targets, FlowchartLogicalGrid? Grid) = Fixture(); Sources[0].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartGroupedChannelPlacement.ApplyCellBlock(Nodes, Sources, Targets), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A pinned unrelated gap participant selects a legal whole-column alternative without shifting its geometry.</summary>
    [Test]
    public void HintedGapParticipantRemainsExactThroughChannelSelection()
    {
        (DiagramContract _, List<DiagramGridLayout.GridNode>? Nodes, DiagramGridLayout.GridNode[]? Sources, DiagramGridLayout.GridNode[]? Targets, FlowchartLogicalGrid? Grid) = Fixture(); DiagramGridLayout.GridNode Other = Nodes.Single(Node => Node.Id == "other"); Other.PlacementHintLocked = true;
        FlowchartCellRegion Region = Grid.RegionOf("other"); (double X, double Y, int Width, int Height) Pixels = (Other.X, Other.Y, Other.Width, Other.Height);
        Assert.That(FlowchartGroupedChannelPlacement.ApplyCellBlock(Nodes, Sources, Targets), Is.True);
        Assert.That(Grid.RegionOf("other"), Is.SameAs(Region));
        Assert.That((Other.X, Other.Y, Other.Width, Other.Height), Is.EqualTo(Pixels));
    }

    /// <summary>The same complete group pair does not inherit obsolete vertical partner extents before channel placement.</summary>
    [Test]
    public void GroupChannelOwnsItsBlockBeforePartnerPreparation()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, DiagramGridLayout.GridNode[]? Sources, DiagramGridLayout.GridNode[]? Targets, FlowchartLogicalGrid _) = Fixture();
        Assert.That(FlowchartGroupedChannelPlacement.OwnsCompleteCellBlock(Contract, Nodes, Sources, Targets), Is.True);
        Assert.That(FlowchartPartnerRankLayout.Find(Contract, Nodes, false), Is.Empty);
    }

    /// <summary>Creates a complete two-by-three group pair and one distant unrelated node.</summary>
    /// <returns>Graph, all nodes, declared source/target membership and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, DiagramGridLayout.GridNode[] Sources,
        DiagramGridLayout.GridNode[] Targets, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract(); Contract.GroupsMutable.AddRange([new() { Id = "A" }, new() { Id = "B" }]);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Prefix, int Count, int Column, int Rank) in new[] { ("A", 2, 0, 0), ("B", 3, 5, 1) })
        {
            for (int Index = 0; Index < Count; Index++)
        {
            string Id = Prefix + Index; Grid.TryPlace(Id, new(Index, Column, 1, 1));
            Contract.NodesMutable.Add(new NodeContract { Id = Id, Shape = "rect", ParentGroupId = Prefix });
            Contract.Groups.Single(Group => Group.Id == Prefix).ChildNodeIdsMutable.Add(Id);
                DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Shape = "rect", GroupId = Prefix, Rank = Rank }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        }

        DiagramGridLayout.GridNode[] Sources = [.. Nodes.Take(2)]; DiagramGridLayout.GridNode[] Targets = [.. Nodes.Skip(2)];
        foreach (DiagramGridLayout.GridNode? Source in Sources)
        {
            foreach (DiagramGridLayout.GridNode? Target in Targets)
            {
                Contract.EdgesMutable.Add(new EdgeContract { SourceId = Source.Id, TargetId = Target.Id });
            }
        }

        Grid.TryPlace("other", new(10, 10, 1, 1)); Contract.NodesMutable.Add(new NodeContract { Id = "other", Shape = "rect" });
        DiagramGridLayout.GridNode Other = new DiagramGridLayout.GridNode { Id = "other", Shape = "rect", Rank = 5 }; Other.BindCells(Grid); Nodes.Add(Other);
        return (Contract, Nodes, Sources, Targets, Grid);
    }
}
