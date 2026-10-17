using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks whole-group centring, complete collision rejection and explicit hint priority.</summary>
[TestFixture]
public sealed class FlowchartCellNestedPlacementTests
{
    /// <summary>Every nested member translates by the same whole-cell offset, retaining its span and internal separation.</summary>
    [Test]
    public void CentresAllNestedMembersTogether()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Assert.That(DiagramGridLayout.CentreCellNestedGroups(Contract, Nodes, false), Is.True);
        Assert.That(Grid.RegionOf("A").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(3));
    }

    /// <summary>An occupied late member target rejects the whole nested-group move and every gap change.</summary>
    [Test]
    public void OccupiedTargetRejectsAllNestedMembers()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Grid.TryPlace("O", new(2, 3, 1, 1));
        var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        var Before = Grid.Capture();
        Assert.That(DiagramGridLayout.CentreCellNestedGroups(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hint on one nested member anchors the complete automatic moving unit.</summary>
    [Test]
    public void HintedMemberKeepsItsWholeNestedGroupFixed()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Nodes.Single(Node => Node.Id == "A").PlacementHintLocked = true;
        var Before = Grid.Capture();
        DiagramGridLayout.CentreCellNestedGroups(Contract, Nodes, false);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Creates a lone child container with two members and one direct parent member.</summary>
    /// <returns>Contract, bound members and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract();
        var Parent = new GroupContract { Id = "outer" }; Parent.ChildGroupIdsMutable.Add("inner");
        Contract.GroupsMutable.AddRange([Parent, new() { Id = "inner", ParentGroupId = "outer" }]);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Group, Row, Column) in new[] { ("P", "outer", 0, 2), ("A", "inner", 2, 0), ("B", "inner", 2, 2) })
        {
            Contract.NodesMutable.Add(new NodeContract { Id = Id, ParentGroupId = Group });
            Contract.Groups.Single(Item => Item.Id == Group).ChildNodeIdsMutable.Add(Id);
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, GroupId = Group }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        return (Contract, Nodes, Grid);
    }
}
