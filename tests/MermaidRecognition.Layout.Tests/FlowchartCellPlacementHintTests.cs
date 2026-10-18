using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks explicit cell hints independently of production's unchanged pixel placement.</summary>
[TestFixture]
public sealed class FlowchartCellPlacementHintTests
{
    /// <summary>A member moving off the group's old minimum must translate the complete group back to its anchor, preserving both hints atomically.</summary>
    [Test]
    public void MemberHintCanChangeTheEnvelopeWithoutLosingTheGroupAnchor()
    {
        var Contract = new DiagramContract();
        Contract.NodesMutable.AddRange([new() { Id = "anchor" }, new() { Id = "A", ParentGroupId = "G" },
            new() { Id = "B", ParentGroupId = "G", PlacementHint = new() { Kind = PlacementHintKind.RightOf, PeerId = "A", SourceLine = 2 } }]);
        var Group = new GroupContract { Id = "G", PlacementHint = new() { Kind = PlacementHintKind.Below, PeerId = "anchor", SourceLine = 1 } };
        Group.ChildNodeIdsMutable.AddRange(["A", "B"]); Contract.GroupsMutable.Add(Group);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Row, Column) in new[] { ("anchor", 0, 0), ("A", 1, 4), ("B", 1, 0) })
        {
            Assert.That(Grid.TryPlace(Id, new(Row, Column, 1, 1)), Is.True);
            var Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = Id == "anchor" ? null : "G" };
            Node.BindCells(Grid); Nodes.Add(Node);
        }
        var Before = Grid.Capture();
        Assert.That(FlowchartPlacementHints.Apply(Contract, Nodes), Is.True);
        Assert.That(Group.PlacementHint!.IsActive, Is.True);
        Assert.That(Contract.Nodes[2].PlacementHint!.IsActive, Is.True);
        Assert.That(FlowchartPlacementHints.CellHintsMatch(Contract, Nodes, Grid.Capture().Regions), Is.True);
        Assert.That(Grid.RegionOf("A").StartColumn, Is.EqualTo(0));
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("anchor"), Is.SameAs(Before.Regions["anchor"]));
        Grid.Restore(Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A nested hinted group moves every member together while preserving spans and exact internal row/column offsets.</summary>
    [Test]
    public void NestedGroupPlanMovesEveryMemberTogether()
    {
        var (Contract, Nodes, Grid) = Fixture(PlacementHintKind.RightOf);
        Contract.Nodes[1].PlacementHint = null;
        Contract.GroupsMutable.Add(new GroupContract { Id = "outer", PlacementHint = new() { Kind = PlacementHintKind.Below, PeerId = "A" } });
        Contract.GroupsMutable.Add(new GroupContract { Id = "inner", ParentGroupId = "outer" });
        Nodes[1].GroupId = "inner";
        Assert.That(Grid.TryPlace("C", new(6, 5, 1, 1)), Is.True);
        var C = new DiagramGridLayout.GridNode { Id = "C", GroupId = "outer" }; C.BindCells(Grid); Nodes.Add(C);
        var Before = Grid.Capture();
        FlowchartPlacementHints.Apply(Contract, Nodes);
        var B = Grid.RegionOf("B"); var AfterC = Grid.RegionOf("C");
        Assert.That(B.StartColumn, Is.EqualTo(Grid.RegionOf("A").StartColumn));
        Assert.That(B.StartRow, Is.EqualTo(Grid.RegionOf("A").EndRow));
        Assert.That(AfterC.StartRow - B.StartRow, Is.EqualTo(Before.Regions["C"].StartRow - Before.Regions["B"].StartRow));
        Assert.That(AfterC.StartColumn - B.StartColumn, Is.EqualTo(Before.Regions["C"].StartColumn - Before.Regions["B"].StartColumn));
    }

    /// <summary>A late cyclic hint is rejected without partially moving either unit or changing earlier accepted ownership.</summary>
    [Test]
    public void RejectedCycleLeavesAcceptedRegionsAndGapsIntact()
    {
        var (Contract, Nodes, Grid) = Fixture(PlacementHintKind.RightOf);
        Contract.Nodes[1].PlacementHint!.SourceLine = 1;
        Contract.Nodes[0].PlacementHint = new() { Kind = PlacementHintKind.LeftOf, PeerId = "B", SourceLine = 2 };
        var Before = Grid.Capture();
        FlowchartPlacementHints.Apply(Contract, Nodes);
        Assert.That(Contract.Nodes[0].PlacementHint!.IsActive, Is.False);
        Assert.That(Contract.Nodes[1].PlacementHint!.IsActive, Is.True);
        Assert.That(Grid.RegionOf("A"), Is.SameAs(Before.Regions["A"]));
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(Grid.RegionOf("A").EndColumn));
        Grid.Restore(Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Offsets count whole vacant cells and alignment uses starting rows/columns rather than visual centres.</summary>
    /// <param name="Kind">Relative placement direction.</param>
    [TestCase(PlacementHintKind.Above)]
    [TestCase(PlacementHintKind.Below)]
    [TestCase(PlacementHintKind.LeftOf)]
    [TestCase(PlacementHintKind.RightOf)]
    public void RelativeOffsetsAlignStartsDespiteDifferentSpans(PlacementHintKind Kind)
    {
        var (Contract, Nodes, Grid) = Fixture(Kind);
        Contract.Nodes[1].PlacementHint!.Offset = 2;
        Assert.That(FlowchartPlacementHints.Apply(Contract, Nodes), Is.True);
        var Peer = Grid.RegionOf("A"); var Owner = Grid.RegionOf("B");
        Assert.That(Kind switch
        {
            PlacementHintKind.Above => Owner.EndRow == Peer.StartRow - 2 && Owner.StartColumn == Peer.StartColumn,
            PlacementHintKind.Below => Owner.StartRow == Peer.EndRow + 2 && Owner.StartColumn == Peer.StartColumn,
            PlacementHintKind.LeftOf => Owner.EndColumn == Peer.StartColumn - 2 && Owner.StartRow == Peer.StartRow,
            _ => Owner.StartColumn == Peer.EndColumn + 2 && Owner.StartRow == Peer.StartRow
        }, Is.True);
        Assert.That(Owner.RowSpan, Is.EqualTo(2));
        Assert.That(Contract.Nodes[1].PlacementHint!.IsActive, Is.True);
        Assert.That(FlowchartPlacementHints.Apply(Contract, Nodes), Is.False, "Reapplication is idempotent, including locked owners.");
    }

    /// <summary>A hint moves beyond an obstacle only along its declared direction, keeping exclusive contiguous occupancy.</summary>
    [Test]
    public void HintPlanKeepsAlignmentWhileAvoidingOccupiedCells()
    {
        var (Contract, Nodes, Grid) = Fixture(PlacementHintKind.RightOf);
        Assert.That(Grid.TryPlace("C", new(0, 1, 1, 2)), Is.True);
        var C = new DiagramGridLayout.GridNode { Id = "C" }; C.BindCells(Grid); Nodes.Add(C);
        var Before = Grid.Capture();
        FlowchartPlacementHints.Apply(Contract, Nodes);
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(3));
        Assert.That(Grid.RegionOf("B").StartRow, Is.EqualTo(0));
        Assert.That(Grid.RegionOf("C"), Is.SameAs(Before.Regions["C"]));
        Grid.Restore(Before);
        Assert.That(Grid.Capture(), Is.SameAs(Before), "Rollback restores all cells and gaps exactly.");
    }

    /// <summary>Push directions select a whole-cell boundary without using a 30-pixel translation.</summary>
    /// <param name="Direction">Page direction; north decreases the logical row.</param>
    [TestCase(LayoutHintDirection.North)]
    [TestCase(LayoutHintDirection.South)]
    [TestCase(LayoutHintDirection.East)]
    [TestCase(LayoutHintDirection.West)]
    public void PushUsesCellBoundary(LayoutHintDirection Direction)
    {
        var (Contract, Nodes, Grid) = Fixture(PlacementHintKind.Push);
        Contract.Nodes[1].PlacementHint!.Direction = Direction;
        FlowchartPlacementHints.Apply(Contract, Nodes);
        var A = Grid.RegionOf("A"); var B = Grid.RegionOf("B");
        Assert.That(Direction switch
        {
            LayoutHintDirection.North => B.EndRow == A.StartRow,
            LayoutHintDirection.South => B.StartRow == A.EndRow,
            LayoutHintDirection.West => B.EndColumn == A.StartColumn,
            _ => B.StartColumn == A.EndColumn
        }, Is.True);
    }

    /// <summary>Creates unequal declared spans and an already locked owner to exercise explicit hint authority.</summary>
    /// <param name="Kind">Requested hint operation.</param>
    /// <returns>One contract, bound node list and their authoritative ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(PlacementHintKind Kind)
    {
        var Contract = new DiagramContract();
        Contract.NodesMutable.Add(new NodeContract { Id = "A" });
        Contract.NodesMutable.Add(new NodeContract { Id = "B", PlacementHint = new() { Kind = Kind, PeerId = "A" } });
        var Grid = FlowchartCellInitialPlacement.Create(FlowchartLogicalGridConfiguration.FromRules(),
            [new("A", 0, 0, 1, 1), new("B", 2, 2, 2, 1)], false);
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "A" }, new() { Id = "B" } };
        foreach (var Node in Nodes) Node.BindCells(Grid);
        Nodes[1].PlacementHintLocked = true;
        return (Contract, Nodes, Grid);
    }
}
