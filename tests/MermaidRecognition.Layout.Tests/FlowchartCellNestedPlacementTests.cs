using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks the nested midpoint bypass without removing membership, frame growth or explicit hint protection.</summary>
[TestFixture]
public sealed class FlowchartCellNestedPlacementTests
{
    /// <summary>A lone nested group keeps its declared regions instead of moving to the direct members' midpoint.</summary>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void PreservesAllNestedMembers(bool Horizontal)
    {
        var (Contract, Nodes, Grid) = Fixture();
        var Before = Grid.Capture();
        Invoke(Contract, Nodes, Horizontal);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>An occupied late member target rejects the whole nested-group move and every gap change.</summary>
    [Test]
    public void OccupiedTargetRejectsAllNestedMembers()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Grid.TryPlace("O", new(2, 3, 1, 1));
        var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        var Before = Grid.Capture();
        Invoke(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A hint on one nested member anchors the complete automatic moving unit.</summary>
    [Test]
    public void HintedMemberKeepsItsWholeNestedGroupFixed()
    {
        var (Contract, Nodes, Grid) = Fixture();
        Nodes.Single(Node => Node.Id == "A").PlacementHintLocked = true;
        var Before = Grid.Capture();
        Invoke(Contract, Nodes, false);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Calls the real legacy group dispatcher, keeping the regression independent of a replacement helper.</summary>
    /// <param name="Contract">Nested membership.</param>
    /// <param name="Nodes">Authoritative members.</param>
    /// <param name="Horizontal">Flow orientation.</param>
    private static void Invoke(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal) =>
        typeof(DiagramGridLayout).GetMethod("CenterLoneNestedGroups", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [Contract, Nodes, Horizontal]);

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
