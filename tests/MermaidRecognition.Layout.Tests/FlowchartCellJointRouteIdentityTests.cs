using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that recognition aliases for group endpoints never become additional cell-move units.</summary>
[TestFixture]
public sealed class FlowchartCellJointRouteIdentityTests
{
    /// <summary>Builds a joint search without treating the group's recognition alias as a second leaf owner.</summary>
    /// <param name="GroupId">Group identity reproduced by the nested-title and WebShop fixtures.</param>
    /// <param name="Horizontal">Whether the candidate's main flow axis is horizontal.</param>
    [TestCase("inner", true)]
    [TestCase("appNode", false)]
    public void GroupEndpointAliasUsesOnlyItsMemberRegions(string GroupId, bool Horizontal)
    {
        var Contract = MermaidRecognizer.Parse($"flowchart LR\nsubgraph {GroupId}[Boundary]\nb --> c\nend\na --> {GroupId}");
        Assert.That(Contract.Nodes.Any(Node => Node.Id == GroupId), Is.True,
            "Recognition must retain the group endpoint alias and its declarations.");
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Assert.That(Grid.TryPlace("a", new(0, 0, 1, 1)), Is.True);
        Assert.That(Grid.TryPlace("b", new(0, 3, 1, 1)), Is.True);
        Assert.That(Grid.TryPlace("c", new(2, 3, 1, 1)), Is.True);
        var Placed = new List<DiagramGridLayout.GridNode>
        {
            new() { Id = "a" }, new() { Id = "b", GroupId = GroupId }, new() { Id = "c", GroupId = GroupId }
        };
        foreach (var Node in Placed) Node.BindCells(Grid);
        var Before = Grid.Capture();
        var Nominated = new HashSet<string>(StringComparer.Ordinal) { "a", GroupId };
        var Aliased = DiagramGridLayout.CellJointRouteAssignments(Contract, Placed, Horizontal, Nominated).ToArray();
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Grid.Capture().ColumnGaps, Is.EquivalentTo(Before.ColumnGaps));
        Assert.That(Grid.Capture().RowGaps, Is.EquivalentTo(Before.RowGaps));

        // Removing recognition-only metadata cannot change ownership or the
        // complete region candidates. It is never removed by production code.
        Contract.NodesMutable.RemoveAll(Node => Node.Id == GroupId);
        var Canonical = DiagramGridLayout.CellJointRouteAssignments(Contract, Placed, Horizontal, Nominated).ToArray();
        Assert.That(Aliased.Select(Option => Option.Key).Order(),
            Is.EqualTo(Canonical.Select(Option => Option.Key).Order()));
        Assert.That(Grid.Capture().Regions.Keys, Is.EquivalentTo(new[] { "a", "b", "c" }));
        Assert.Throws<ArgumentException>(() => DiagramGridLayout.CellJointRouteAssignments(
            Contract, [.. Placed, Placed[0]], Horizontal, Nominated).ToArray(),
            "A repeated physical owner must still fail instead of being merged.");
        Assert.That(Grid.TryPlace("extra", Before.Regions["a"]), Is.False,
            "Two different nodes must never share the same occupied cells.");
    }
}
