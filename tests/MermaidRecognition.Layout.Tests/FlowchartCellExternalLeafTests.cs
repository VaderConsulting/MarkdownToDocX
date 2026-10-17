using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks the shared planner's complete outside-leaf regions, external clearances and coordinate views.</summary>
[TestFixture]
public sealed class FlowchartCellExternalLeafTests
{
    /// <summary>An outside input receives one whole-cell region and the mandatory external frame gap.</summary>
    [Test]
    public void OutsideLeafUsesAuthoritativeCellsAndFrameClearance()
    {
        var (Contract, Nodes, Grid) = Fixture(false, false); var Frames = new List<DiagramGridLayout.GridFrame>();
        Assert.That(FlowchartCellLeafPlacement.Apply(Contract, Nodes, Frames, false, External: true), Is.True);
        var Parent = Nodes.Single(Node => Node.Id == "P"); var Leaf = Nodes.Single(Node => Node.Id == "L"); var Frame = Frames.Single();
        Assert.That(Grid.RegionOf("L").StartRow, Is.EqualTo(Grid.RegionOf("P").StartRow));
        double Gap = Leaf.X < Parent.X ? Frame.Left - Leaf.X - Leaf.Width / 2d : Leaf.X - Leaf.Width / 2d - Frame.Right;
        Assert.That(Gap, Is.GreaterThanOrEqualTo(60));
    }

    /// <summary>A hinted outside leaf retains exact ownership, rank and frame-cache references.</summary>
    [Test]
    public void HintedOutsideLeafKeepsExactState()
    {
        var (Contract, Nodes, Grid) = Fixture(false, false); Nodes.Single(Node => Node.Id == "L").PlacementHintLocked = true;
        var Before = Grid.Capture(); var Frame = new DiagramGridLayout.GridFrame { Id = "sentinel" }; var Frames = new List<DiagramGridLayout.GridFrame> { Frame };
        Assert.That(FlowchartCellLeafPlacement.Apply(Contract, Nodes, Frames, false, External: true), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before)); Assert.That(Frames.Single(), Is.SameAs(Frame));
    }

    /// <summary>A north title band is never crossed by the preferred straight chord, in either existing coordinate view.</summary>
    /// <param name="Routing">Whether Y-up routing coordinates are active.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void TitleBandKeepsTheLeafOnTheLegalSouthSide(bool Routing)
    {
        var (Contract, Nodes, Grid) = Fixture(true, Routing);
        Assert.That(FlowchartCellLeafPlacement.Apply(Contract, Nodes, [], true, External: true), Is.True);
        Assert.That(Grid.RegionOf("L").StartRow, Is.GreaterThan(Grid.RegionOf("P").StartRow));
        var Parent = Nodes.Single(Node => Node.Id == "P"); var Leaf = Nodes.Single(Node => Node.Id == "L");
        Assert.That(Routing ? Leaf.Y < Parent.Y : Leaf.Y > Parent.Y, Is.True);
    }

    /// <summary>Creates an outside terminal input and an internal continuing input to the same grouped endpoint.</summary>
    /// <param name="Horizontal">Whether to transpose the logical ownership.</param>
    /// <param name="Routing">Whether to activate the existing routing coordinate adapter.</param>
    /// <returns>Contract, bound members and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal, bool Routing)
    {
        var Contract = new DiagramContract(); var Group = new GroupContract { Id = "G" }; Group.ChildNodeIdsMutable.AddRange(["P", "I"]); Contract.GroupsMutable.Add(Group);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); var Context = new FlowchartRenderContext();
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, GroupId, Row, Column) in new[] { ("P", (string?)"G", 0, 0), ("I", "G", 2, 0), ("L", null, 0, 4) })
        {
            Contract.NodesMutable.Add(new NodeContract { Id = Id, ParentGroupId = GroupId }); Grid.TryPlace(Id, new(Horizontal ? Column : Row, Horizontal ? Row : Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = GroupId, Rank = Row }; Node.BindCells(Grid, Context);
            if (Routing) Node.CellNode!.UseRoutingCoordinates(1000); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "L", TargetId = "P" }, new() { SourceId = "I", TargetId = "P" }]);
        return (Contract, Nodes, Grid);
    }
}
