using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks exact frame projection for complete candidates without modifying authoritative nodes or hint state.</summary>
[TestFixture]
public sealed class FlowchartCellFrameProjectionTests
{
    /// <summary>Empty frames and their hint dependents use the explicit reflection origin even though they have no occupied node region.</summary>
    /// <param name="Hinted">Whether the first empty frame is positioned relative to a node.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void EmptyFrameChainsReflectTheOriginalPageProjection(bool Hinted)
    {
        var Contract = new DiagramContract();
        Contract.NodesMutable.Add(new() { Id = "A" });
        Contract.GroupsMutable.AddRange([new() { Id = "empty", PlacementHint = Hinted
            ? new() { Kind = PlacementHintKind.RightOf, PeerId = "A", IsActive = true } : null },
            new() { Id = "dependent", PlacementHint = new() { Kind = PlacementHintKind.Below, PeerId = "empty", IsActive = true } }]);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1));
        var Node = new DiagramGridLayout.GridNode { Id = "A" }; Node.BindCells(Grid);
        var Before = Grid.Capture();
        var Page = DiagramGridLayout.ProjectCellFrames(Contract, [Node], Before);
        Node.CellNode!.UseRoutingCoordinates(1000);
        var Routing = DiagramGridLayout.ProjectCellFrames(Contract, [Node], Before);
        foreach (var Frame in Page)
        {
            var Actual = Routing.Single(Item => Item.Id == Frame.Id);
            Assert.That((Actual.Left, Actual.Right, Actual.Top, Actual.Bottom),
                Is.EqualTo((Frame.Left, Frame.Right, 1000 - Frame.Top, 1000 - Frame.Bottom)));
        }
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Nested and empty frames project exactly like accepted geometry in either existing coordinate view.</summary>
    /// <param name="Routing">Whether the existing routing reflection is active.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void PureFramesMatchAppliedRegionsAndGaps(bool Routing)
    {
        var Contract = new DiagramContract();
        var Outer = new GroupContract { Id = "outer" }; Outer.ChildGroupIdsMutable.AddRange(["G", "H"]);
        var G = new GroupContract { Id = "G", ParentGroupId = "outer" }; G.ChildNodeIdsMutable.Add("A");
        var H = new GroupContract { Id = "H", ParentGroupId = "outer" }; H.ChildNodeIdsMutable.Add("B");
        var Empty = new GroupContract { Id = "empty", PlacementHint = new() { Kind = PlacementHintKind.RightOf, PeerId = "outer", Offset = 1 } };
        Contract.GroupsMutable.AddRange([Outer, G, H, Empty]);
        Contract.NodesMutable.AddRange([new() { Id = "A", ParentGroupId = "G" }, new() { Id = "B", ParentGroupId = "H" }]);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(2, 2, 1, 1));
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "A", GroupId = "G" }, new() { Id = "B", GroupId = "H" } };
        foreach (var Node in Nodes) { Node.BindCells(Grid); if (Routing) Node.CellNode!.UseRoutingCoordinates(1000); }
        var Before = Grid.Capture(); var HintState = (Empty.PlacementHint.IsActive, Empty.PlacementHint.UnhonouredReason);
        var Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(2, 4, 1, 1) }, [new(true, 0, 60, 30)])!;
        var Projected = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Plan.After);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That((Empty.PlacementHint.IsActive, Empty.PlacementHint.UnhonouredReason), Is.EqualTo(HintState));
        Assert.That(Plan.TryApply(), Is.True);
        var Actual = new List<DiagramGridLayout.GridFrame>(); DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Actual);
        Assert.That(Projected.Select(Frame => (Frame.Id, Frame.Left, Frame.Top, Frame.Right, Frame.Bottom, Frame.TitleStrip)),
            Is.EqualTo(Actual.Select(Frame => (Frame.Id, Frame.Left, Frame.Top, Frame.Right, Frame.Bottom, Frame.TitleStrip))));
        Plan.Rollback(); Assert.That(Grid.Capture(), Is.SameAs(Before));
    }
}
