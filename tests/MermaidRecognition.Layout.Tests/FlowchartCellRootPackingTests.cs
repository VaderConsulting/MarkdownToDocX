using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete parent-grid root packing and exact rejection without local pixel reflow.</summary>
[TestFixture]
public sealed class FlowchartCellRootPackingTests
{
    /// <summary>Interleaved containers receive disjoint complete reservations and derived nonoverlapping frames.</summary>
    [Test]
    public void InterleavedRootGroupsPackOnTheParentGrid()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        List<DiagramGridLayout.GridFrame> Frames = [];
        Assert.That(DiagramGridLayout.PackCellRootGroups(Contract, Nodes, Frames), Is.True);
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Contract, Frames), Is.Zero);
        Assert.That(Nodes.All(Node => ReferenceEquals(Node.CellNode!.Grid, Grid)), Is.True);
        Assert.That(Grid.Capture().Regions.Values.SelectMany((A, Index) => Grid.Capture().Regions.Values.Skip(Index + 1).Select(B => A.Overlaps(B))), Is.All.False);
    }

    /// <summary>Unmovable groups reject all trial candidates and keep the exact parent ownership, gaps and frame cache.</summary>
    [Test]
    public void HintedRootsRejectPackingWithoutPartialApplication()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.PlacementHintLocked = true;
        }

        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        DiagramGridLayout.GridFrame Frame = new DiagramGridLayout.GridFrame { Id = "sentinel", Left = 0, Right = 30, Top = 0, Bottom = 30 };
        List<DiagramGridLayout.GridFrame> Frames = [Frame];
        Assert.That(DiagramGridLayout.PackCellRootGroups(Contract, Nodes, Frames), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Frames.Single(), Is.SameAs(Frame));
    }

    /// <summary>Creates two interleaved root groups on one authoritative diagram grid.</summary>
    /// <returns>Contract, bound members and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract();
        Contract.GroupsMutable.AddRange([new() { Id = "G1" }, new() { Id = "G2" }]);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, string? Group, int Row, int Column) in new[] { ("A", "G1", 0, 0), ("B", "G1", 2, 2), ("C", "G2", 0, 2), ("D", "G2", 2, 0) })
        {
            Contract.NodesMutable.Add(new NodeContract { Id = Id, ParentGroupId = Group });
            Contract.Groups.Single(Item => Item.Id == Group).ChildNodeIdsMutable.Add(Id);
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = Group }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        return (Contract, Nodes, Grid);
    }
}
