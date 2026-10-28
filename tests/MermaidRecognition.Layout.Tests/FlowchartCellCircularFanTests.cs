using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic circular fan/collector alignment with complete chain rollback.</summary>
[TestFixture]
public sealed class FlowchartCellCircularFanTests
{
    /// <summary>The real placement dispatcher leaves junction and chain regions for routed scoring.</summary>
    /// <param name="FanOut">Whether the junction fans out or collects inputs.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void BypassesForcedJunctionAndChainAlignment(bool FanOut)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(FanOut);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        FlowchartCircularJunctionLayout.AlignFans(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A late chain collision rejects the junction move as well as every other planned region.</summary>
    [Test]
    public void BlockedChainRejectsEveryAlignment()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(true);
        Grid.TryPlace("O", new(-1, 1, 1, 1));
        DiagramGridLayout.GridNode O = new DiagramGridLayout.GridNode { Id = "O", Rank = 99 }; O.BindCells(Grid); Nodes.Add(O);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        FlowchartCircularJunctionLayout.AlignFans(Contract, Nodes, false);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a three-way junction with one chain on its opposite side.</summary>
    /// <param name="FanOut">Whether links point towards or away from the neighbours.</param>
    /// <returns>Graph, bound nodes and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool FanOut)
    {
        DiagramContract Contract = new DiagramContract(); FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach ((string? Id, int Row, int Column) in new[] { ("J", 0, 0), ("chain", FanOut ? -1 : 1, 0), ("A", FanOut ? 1 : -1, 0),
            ("B", FanOut ? 1 : -1, 1), ("C", FanOut ? 1 : -1, 2) })
        {
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row, Shape = Id == "J" ? "circle" : "rect" }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        foreach (string Id in new[] { "A", "B", "C" })
        {
            Contract.EdgesMutable.Add(new EdgeContract
            { SourceId = FanOut ? "J" : Id, TargetId = FanOut ? Id : "J" });
        }

        Contract.EdgesMutable.Add(new EdgeContract { SourceId = FanOut ? "chain" : "J", TargetId = FanOut ? "J" : "chain" });
        return (Contract, Nodes, Grid);
    }
}
