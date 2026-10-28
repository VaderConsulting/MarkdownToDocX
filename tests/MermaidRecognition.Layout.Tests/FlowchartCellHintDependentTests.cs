using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete hint-dependent moving sets without any live placement mutation.</summary>
[TestFixture]
public sealed class FlowchartCellHintDependentTests
{
    /// <summary>Group peers, node peers, nested membership and chains join the same moving unit, while inactive hints do not.</summary>
    [Test]
    public void IncludesEveryTransitiveDependentAndNestedMember()
    {
        DiagramContract Contract = new DiagramContract();
        GroupContract G = new GroupContract { Id = "G" }; G.ChildNodeIdsMutable.Add("A");
        GroupContract H = new GroupContract { Id = "H", PlacementHint = new() { Kind = PlacementHintKind.Below, PeerId = "B", IsActive = true } };
        H.ChildNodeIdsMutable.Add("C");
        Contract.GroupsMutable.AddRange([G, H]);
        Contract.NodesMutable.AddRange([new() { Id = "A", ParentGroupId = "G" },
            new() { Id = "B", PlacementHint = new() { Kind = PlacementHintKind.RightOf, PeerId = "G", IsActive = true } },
            new() { Id = "C", ParentGroupId = "H" },
            new() { Id = "D", PlacementHint = new() { Kind = PlacementHintKind.Below, PeerId = "C", IsActive = true } },
            new() { Id = "E", PlacementHint = new() { Kind = PlacementHintKind.RightOf, PeerId = "D", IsActive = false } }]);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) =>
        {
            Grid.TryPlace(Node.Id, new(Index, 0, 1, 1));
            DiagramGridLayout.GridNode Bound = new DiagramGridLayout.GridNode { Id = Node.Id, GroupId = Node.ParentGroupId }; Bound.BindCells(Grid); return Bound;
        })];
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(DiagramGridLayout.CellHintDependentUnit(Contract, Nodes, "G").Select(Node => Node.Id),
            Is.EqualTo(["A", "B", "C", "D"]));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A complete closure translates atomically with every hint honoured and restores exact ownership on rejection rollback.</summary>
    [Test]
    public void CompleteClosureMovePreservesHintsAndExactRollback()
    {
        DiagramContract Contract = new DiagramContract();
        GroupContract G = new GroupContract { Id = "G" }; G.ChildNodeIdsMutable.Add("A"); Contract.GroupsMutable.Add(G);
        Contract.NodesMutable.AddRange([new() { Id = "A", ParentGroupId = "G" },
            new() { Id = "B", PlacementHint = new() { Kind = PlacementHintKind.RightOf, PeerId = "G", IsActive = true } },
            new() { Id = "C", PlacementHint = new() { Kind = PlacementHintKind.Below, PeerId = "B", IsActive = true } }]);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Row, int Column) in new[] { ("A", 0, 0), ("B", 0, 1), ("C", 1, 1) })
        {
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = Id == "A" ? "G" : null };
            Node.BindCells(Grid); Nodes.Add(Node);
        }
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.PlacementHintLocked = true;
            Node.CellHintValidator = Regions => FlowchartPlacementHints.CellHintsMatch(Contract, Nodes, Regions);
        }
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Dictionary<string, FlowchartCellRegion> Targets = DiagramGridLayout.CellHintDependentUnit(Contract, Nodes, "G").ToDictionary(Node => Node.Id,
            Node => new FlowchartCellRegion(Node.CellNode!.Region.StartRow + 1, Node.CellNode.Region.StartColumn + 1, 1, 1));
        FlowchartCellRepair.Assignment Plan = FlowchartCellRepair.PlanAssignment(Nodes, Targets, AllowHintedRegionRepair: true)!;
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Plan.TryApply(), Is.True);
        Assert.That(FlowchartPlacementHints.CellHintsMatch(Contract, Nodes, Grid.Capture().Regions), Is.True);
        Plan.Rollback();
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }
}
