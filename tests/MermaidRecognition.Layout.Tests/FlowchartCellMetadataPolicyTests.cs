using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that pre-placement column metadata cannot reintroduce centre-only policies into cell ownership.</summary>
[TestFixture]
public sealed class FlowchartCellMetadataPolicyTests
{
    /// <summary>A forward chain retains its independently ordered columns until whole-cell routed scoring.</summary>
    [Test]
    public void ForwardChainDoesNotRewriteColumns()
    {
        var (Contract, Nodes, Grid) = Fixture();
        var Before = Grid.Capture();
        var Columns = Nodes.Select(Node => Node.Column).ToArray();
        DiagramGridLayout.AlignForwardMainChainColumn(Contract, Nodes, false);
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Builds a forward fan with a converging merge and deliberately distinct ordering columns.</summary>
    /// <returns>Contract, bound nodes and authoritative grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        var Contract = new DiagramContract();
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, Rank, Column) in new[] { ("D", 0, 1), ("A", 1, 0), ("B", 1, 2), ("C", 1, 4), ("M", 2, 5) })
        {
            Contract.NodesMutable.Add(new() { Id = Id });
            Assert.That(Grid.TryPlace(Id, new(Rank * 2, Column, Id == "D" ? 2 : 1, 1)), Is.True);
            var Node = new DiagramGridLayout.GridNode { Id = Id, IsDiamond = Id == "D", Rank = Rank, Column = Column, OldX = Column * 180 };
            Node.BindCells(Grid); Nodes.Add(Node);
        }
        Contract.EdgesMutable.AddRange([new() { SourceId = "D", TargetId = "A" }, new() { SourceId = "D", TargetId = "B" },
            new() { SourceId = "D", TargetId = "C" }, new() { SourceId = "A", TargetId = "M" }, new() { SourceId = "B", TargetId = "M" }, new() { SourceId = "C", TargetId = "M" }]);
        return (Contract, Nodes, Grid);
    }
}
