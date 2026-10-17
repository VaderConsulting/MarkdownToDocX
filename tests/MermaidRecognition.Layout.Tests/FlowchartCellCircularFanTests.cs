using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks atomic circular fan/collector alignment with complete chain rollback.</summary>
[TestFixture]
public sealed class FlowchartCellCircularFanTests
{
    /// <summary>A junction and its sole chain align together to the middle whole-cell neighbour.</summary>
    /// <param name="FanOut">Whether the junction fans out or collects inputs.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void AlignsJunctionAndChainTogether(bool FanOut)
    {
        var (Contract, Nodes, Grid) = Fixture(FanOut);
        Assert.That(FlowchartCircularJunctionLayout.AlignCellFans(Contract, Nodes, false), Is.True);
        Assert.That(Grid.RegionOf("J").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("chain").StartColumn, Is.EqualTo(1));
    }

    /// <summary>A late chain collision rejects the junction move as well as every other planned region.</summary>
    [Test]
    public void BlockedChainRejectsEveryAlignment()
    {
        var (Contract, Nodes, Grid) = Fixture(true);
        Grid.TryPlace("O", new(-1, 1, 1, 1));
        var O = new DiagramGridLayout.GridNode { Id = "O", Rank = 99 }; O.BindCells(Grid); Nodes.Add(O);
        var Before = Grid.Capture();
        Assert.That(FlowchartCircularJunctionLayout.AlignCellFans(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates a three-way junction with one chain on its opposite side.</summary>
    /// <param name="FanOut">Whether links point towards or away from the neighbours.</param>
    /// <returns>Graph, bound nodes and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool FanOut)
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Row, Column) in new[] { ("J", 0, 0), ("chain", FanOut ? -1 : 1, 0), ("A", FanOut ? 1 : -1, 0),
            ("B", FanOut ? 1 : -1, 1), ("C", FanOut ? 1 : -1, 2) })
        {
            Grid.TryPlace(Id, new(Row, Column, 1, 1));
            var Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Row, Shape = Id == "J" ? "circle" : "rect" }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        foreach (string Id in new[] { "A", "B", "C" }) Contract.EdgesMutable.Add(new EdgeContract
            { SourceId = FanOut ? "J" : Id, TargetId = FanOut ? Id : "J" });
        Contract.EdgesMutable.Add(new EdgeContract { SourceId = FanOut ? "chain" : "J", TargetId = FanOut ? "J" : "chain" });
        return (Contract, Nodes, Grid);
    }
}
