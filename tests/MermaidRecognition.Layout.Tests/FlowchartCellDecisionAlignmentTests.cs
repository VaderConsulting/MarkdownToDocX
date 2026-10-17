using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks whole-chain decision input alignment and exact rejected-plan ownership.</summary>
[TestFixture]
public sealed class FlowchartCellDecisionAlignmentTests
{
    /// <summary>A chain moves in one transaction on either physical cross axis.</summary>
    /// <param name="Horizontal">Whether to align logical rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void AlignsEveryUpstreamChainMemberTogether(bool Horizontal)
    {
        var (Contract, Nodes, Grid) = Fixture(Horizontal);
        Assert.That(DiagramGridLayout.AlignCellDecisionInputs(Contract, Nodes, Horizontal), Is.True);
        Assert.That(Nodes.Select(Node => Horizontal ? Grid.RegionOf(Node.Id).StartRow : Grid.RegionOf(Node.Id).StartColumn), Is.All.EqualTo(2));
    }

    /// <summary>A blocked early input rejects the whole chain, including members planned before the conflict was discovered.</summary>
    [Test]
    public void OccupiedChainTargetRejectsEveryMove()
    {
        var (Contract, Nodes, Grid) = Fixture(false);
        Grid.TryPlace("obstacle", new(0, 2, 1, 1));
        var Obstacle = new DiagramGridLayout.GridNode { Id = "obstacle" }; Obstacle.BindCells(Grid); Nodes.Add(Obstacle);
        var Before = Grid.Capture();
        Assert.That(DiagramGridLayout.AlignCellDecisionInputs(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates two upstream members and a downstream decision in distinct columns.</summary>
    /// <param name="Horizontal">Whether to transpose region starts.</param>
    /// <returns>Graph, nodes and shared cell ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        var Contract = new DiagramContract();
        Contract.EdgesMutable.AddRange([new() { SourceId = "A", TargetId = "B" }, new() { SourceId = "B", TargetId = "D" }]);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        int Index = 0;
        foreach (string Id in new[] { "A", "B", "D" })
        {
            Grid.TryPlace(Id, new(Horizontal ? Index : Index * 2, Horizontal ? Index * 2 : Index, Id == "D" ? 2 : 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D" }; Node.BindCells(Grid); Nodes.Add(Node); Index++;
        }
        return (Contract, Nodes, Grid);
    }
}
