using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks whole-chain decision input alignment and exact rejected-plan ownership.</summary>
[TestFixture]
public sealed class FlowchartCellDecisionAlignmentTests
{
    /// <summary>The real dispatcher leaves each upstream region untouched on either cross axis.</summary>
    /// <param name="Horizontal">Whether to align logical rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void BypassesForcedInputChainAlignment(bool Horizontal)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(Horizontal);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        DiagramGridLayout.AlignUnbranchedDecisionInputs(Contract, Nodes, Horizontal);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A blocked early input rejects the whole chain, including members planned before the conflict was discovered.</summary>
    [Test]
    public void OccupiedChainTargetRejectsEveryMove()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(false);
        Grid.TryPlace("obstacle", new(0, 2, 1, 1));
        DiagramGridLayout.GridNode Obstacle = new DiagramGridLayout.GridNode { Id = "obstacle" }; Obstacle.BindCells(Grid); Nodes.Add(Obstacle);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        DiagramGridLayout.AlignUnbranchedDecisionInputs(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates two upstream members and a downstream decision in distinct columns.</summary>
    /// <param name="Horizontal">Whether to transpose region starts.</param>
    /// <returns>Graph, nodes and shared cell ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        DiagramContract Contract = new DiagramContract();
        Contract.EdgesMutable.AddRange([new() { SourceId = "A", TargetId = "B" }, new() { SourceId = "B", TargetId = "D" }]);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        int Index = 0;
        foreach (string Id in new[] { "A", "B", "D" })
        {
            Grid.TryPlace(Id, new(Horizontal ? Index : Index * 2, Horizontal ? Index * 2 : Index, Id == "D" ? 2 : 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D" }; Node.BindCells(Grid); Nodes.Add(Node); Index++;
        }
        return (Contract, Nodes, Grid);
    }
}
