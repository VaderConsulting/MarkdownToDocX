using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks whole-cell sparse-lane reuse and atomic corridor-gap rejection.</summary>
[TestFixture]
public sealed class FlowchartCellSparseCompactionTests
{
    /// <summary>Disjoint rank strips may reuse one whole-cell column without sharing occupancy.</summary>
    [Test]
    public void DisjointRanksReuseOneLogicalColumn()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(FlowchartSparseRankCompaction.ApplyCells(new(), Nodes, false), Is.True);
        Assert.That(Nodes.Select(Node => Grid.RegionOf(Node.Id).StartColumn), Is.All.EqualTo(0));
    }

    /// <summary>A rank-skipping relationship retains a separate cell lane and grows its measured external clearance.</summary>
    [Test]
    public void RankSkippingConnectorRetainsItsSideCorridor()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); DiagramContract Contract = new DiagramContract();
        Contract.EdgesMutable.Add(new EdgeContract { SourceId = "N0", TargetId = "N2" });
        Assert.That(FlowchartSparseRankCompaction.ApplyCells(Contract, Nodes, false), Is.True);
        Assert.That(Grid.RegionOf("N2").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.Capture().ColumnGaps[0], Is.EqualTo(60));
    }

    /// <summary>A hinted gap participant rejects complete compaction and all proposed corridor growth.</summary>
    [Test]
    public void HintedCorridorParticipantKeepsExactState()
    {
        (List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); DiagramContract Contract = new DiagramContract();
        Contract.EdgesMutable.Add(new EdgeContract { SourceId = "N0", TargetId = "N2" });
        Nodes[^1].PlacementHintLocked = true; FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartSparseRankCompaction.ApplyCells(Contract, Nodes, false), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Creates three separated cross-axis bands on distinct native ranks.</summary>
    /// <returns>Bound nodes and shared grid.</returns>
    private static (List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); List<DiagramGridLayout.GridNode> Nodes = [];
        for (int Index = 0; Index < 3; Index++)
        {
            string Id = "N" + Index; Grid.TryPlace(Id, new(Index, Index * 3, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Id, Rank = Index }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        return (Nodes, Grid);
    }
}
