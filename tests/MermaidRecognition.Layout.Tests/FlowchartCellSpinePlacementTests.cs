using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic spine lanes and collector rows without a drawing-coordinate round trip.</summary>
[TestFixture]
public sealed class FlowchartCellSpinePlacementTests
{
    /// <summary>Labelled side ordering remains while spine and collector members keep their original regions.</summary>
    [Test]
    public void KeepsLabelledSideOrderingWithoutSpineOrCollectorAlignment()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(FlowchartSpineColumnPostProcessor.ApplyCells(Contract, Nodes, FlowchartDirection.TopToBottom), Is.True);
        Assert.That(Grid.RegionOf("A").StartColumn, Is.EqualTo(3));
        Assert.That(Grid.RegionOf("T").StartColumn, Is.EqualTo(6));
        Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(0));
        Assert.That(Grid.RegionOf("B").StartRow, Is.EqualTo(4));
    }

    /// <summary>A blocked late target rejects the entire plan and restores neither cached pixels nor a partially moved prefix.</summary>
    [Test]
    public void OccupiedSpineTargetRejectsAllMembers()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(Grid.TryPlace("obstacle", new(4, 0, 1, 1)), Is.True);
        DiagramGridLayout.GridNode Obstacle = new DiagramGridLayout.GridNode { Id = "obstacle" }; Obstacle.BindCells(Grid); Nodes.Add(Obstacle);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartSpineColumnPostProcessor.ApplyCells(Contract, Nodes, FlowchartDirection.TopToBottom), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Explicit hint ownership wins over automatic spine lanes.</summary>
    [Test]
    public void HintedBranchRemainsFixed()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Nodes.Single(Node => Node.Id == "B").PlacementHintLocked = true;
        FlowchartCellRegion Before = Grid.RegionOf("B");
        FlowchartSpineColumnPostProcessor.ApplyCells(Contract, Nodes, FlowchartDirection.TopToBottom);
        Assert.That(Grid.RegionOf("B"), Is.SameAs(Before));
    }

    /// <summary>Creates one labelled detour with a three-source terminal collector and unrelated original columns.</summary>
    /// <returns>Graph, bound nodes and their shared ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract();
        foreach (string Id in new[] { "D", "A", "B", "C", "T" })
        {
            Contract.NodesMutable.Add(new NodeContract { Id = Id, Shape = Id == "D" ? "diamond" : "rect" });
        }

        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "A", Label = "Yes" },
            new() { SourceId = "D", TargetId = "B", Label = "No" }, new() { SourceId = "A", TargetId = "T" },
            new() { SourceId = "B", TargetId = "T" }, new() { SourceId = "C", TargetId = "T" }]);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        int Index = 0;
        foreach (NodeContract Node in Contract.Nodes)
        {
            Assert.That(Grid.TryPlace(Node.Id, new(Index * 2, Index + 2, 1, 1)), Is.True);
            DiagramGridLayout.GridNode Bound = new DiagramGridLayout.GridNode { Id = Node.Id }; Bound.BindCells(Grid); Nodes.Add(Bound); Index++;
        }
        return (Contract, Nodes, Grid);
    }
}
