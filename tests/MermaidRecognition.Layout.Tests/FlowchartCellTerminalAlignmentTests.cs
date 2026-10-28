using System.Reflection;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Protects cell regions and ordering from the removed terminal-child centre-alignment policy.</summary>
[TestFixture]
public sealed class FlowchartCellTerminalAlignmentTests
{
    /// <summary>Neither legacy placement phase may straighten a cell-bound terminal child, including unequal spans.</summary>
    /// <param name="Horizontal">Whether ranks advance along columns rather than rows.</param>
    /// <param name="CoordinatesPlaced">Whether this is the pre-placement or post-placement call.</param>
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void CellTerminalChildKeepsDeclaredRegionAndOrdering(bool Horizontal, bool CoordinatesPlaced)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(Horizontal);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        var Columns = Nodes.Select(Node => Node.Column).ToArray();
        Invoke(Contract, Nodes, Horizontal, CoordinatesPlaced);
        Assert.That(Grid.Capture(), Is.SameAs(Before), "Regions and grown gaps must retain their exact snapshot.");
        Assert.That(Nodes.Select(Node => Node.Column), Is.EqualTo(Columns));
    }

    /// <summary>Hinted terminal ownership stays exact through both placement phases.</summary>
    /// <param name="Horizontal">Whether the flow uses columns for ranks.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void HintedTerminalChildKeepsExactSnapshot(bool Horizontal)
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(Horizontal);
        Nodes[1].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); int Column = Nodes[1].Column;
        Invoke(Contract, Nodes, Horizontal, false);
        Invoke(Contract, Nodes, Horizontal, true);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes[1].Column, Is.EqualTo(Column));
    }

    /// <summary>Calls the real dispatcher without adding a production API solely for testing.</summary>
    /// <param name="Contract">Sole visible terminal relationship.</param>
    /// <param name="Nodes">Bound placement nodes.</param>
    /// <param name="Horizontal">Flow orientation.</param>
    /// <param name="CoordinatesPlaced">Placement phase being checked.</param>
    private static void Invoke(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        bool Horizontal, bool CoordinatesPlaced) => typeof(DiagramGridLayout)
            .GetMethod("AlignSingleTerminalChildren", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Contract, Nodes, Horizontal, CoordinatesPlaced]);

    /// <summary>Creates a terminal edge with unequal regions and different cross-axis starts and centres.</summary>
    /// <param name="Horizontal">Whether to transpose the separated rank and lane starts.</param>
    /// <returns>Graph, bound nodes and shared ownership.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture(bool Horizontal)
    {
        DiagramContract Contract = new DiagramContract();
        Contract.EdgesMutable.Add(new EdgeContract { SourceId = "A", TargetId = "B" });
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 2, 3));
        Grid.TryPlace("B", new(Horizontal ? 4 : 3, Horizontal ? 4 : 5, 1, 1));
        List<DiagramGridLayout.GridNode> Nodes =
        [
            new() { Id = "A", Rank = 0, Column = 0 },
            new() { Id = "B", Rank = 1, Column = 2 }
        ];
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.BindCells(Grid);
        }

        return (Contract, Nodes, Grid);
    }
}
