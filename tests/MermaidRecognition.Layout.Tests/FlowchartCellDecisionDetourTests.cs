using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic detour/merge lane placement and exact rejection at either member.</summary>
[TestFixture]
public sealed class FlowchartCellDecisionDetourTests
{
    /// <summary>A continuing merge keeps its original region while the detour receives a whole-cell side lane.</summary>
    /// <param name="Horizontal">Whether the cross axis is rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void DetourAndMergePublishTogether(bool Horizontal)
    {
        var (Contract, Nodes, Grid) = Fixture(Horizontal);
        var Before = Grid.RegionOf("M");
        Assert.That(DiagramGridLayout.AlignCellDecisionDetours(Contract, Nodes, Horizontal), Is.True);
        int Centre = Horizontal ? Grid.RegionOf("D").StartRow : Grid.RegionOf("D").StartColumn;
        Assert.That(Grid.RegionOf("M"), Is.SameAs(Before));
        Assert.That(Horizontal ? Grid.RegionOf("B").StartRow : Grid.RegionOf("B").StartColumn, Is.LessThan(Centre));
    }

    /// <summary>A blocked or hinted merge keeps both selected members unchanged.</summary>
    /// <param name="Hint">Whether a hint rather than an occupied target rejects the automatic move.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RejectedMergeKeepsTheEntireDetourPlan(bool Hint)
    {
        var (Contract, Nodes, Grid) = Fixture(false);
        if (Hint) Nodes.Single(Node => Node.Id == "M").PlacementHintLocked = true;
        else
        {
            Grid.TryPlace("O", new(2, 1, 1, 1));
            var O = new DiagramGridLayout.GridNode { Id = "O" }; O.BindCells(Grid); Nodes.Add(O);
        }
        var Before = Grid.Capture();
        DiagramGridLayout.AlignCellDecisionDetours(Contract, Nodes, false);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Creates a generic decision with one bypass and a merge that has a continuing tail.</summary>
    /// <param name="Horizontal">Whether to transpose region starts.</param>
    /// <returns>Graph, bound members and parent grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Rank, Cross) in new[] { ("D", 0, 2), ("B", 1, 4), ("M", 2, 4), ("T", 3, 4) })
        {
            Grid.TryPlace(Id, new(Horizontal ? Cross : Rank * 2, Horizontal ? Rank * 2 : Cross, Id == "D" ? 2 : 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D", Rank = Rank }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "B" }, new() { SourceId = "D", TargetId = "M" },
            new() { SourceId = "B", TargetId = "M" }, new() { SourceId = "M", TargetId = "T" }]);
        return (Contract, Nodes, Grid);
    }
}
