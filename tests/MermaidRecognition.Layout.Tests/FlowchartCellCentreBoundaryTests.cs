using System.Reflection;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that legacy centre snapping never changes a cell-derived reservation or its geometry.</summary>
[TestFixture]
public sealed class FlowchartCellCentreBoundaryTests
{
    /// <summary>Odd cell spans can have half-cell centres; preserve those exact centres at every snapping boundary.</summary>
    /// <param name="Routing">Whether the existing routing coordinate adapter is active.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void SnapBoundaryPreservesAuthoritativeHalfCellCentre(bool Routing)
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(-2, 1, 1, 1));
        DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = "A" }; Node.BindCells(Grid);
        if (Routing)
        {
            Node.CellNode!.UseRoutingCoordinates(600);
        }

        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); (double X, double Y) Position = (Node.X, Node.Y);
        DiagramGridLayout.GridNode Legacy = new DiagramGridLayout.GridNode { Id = "legacy", X = 17, Y = 47 };
        typeof(DiagramGridLayout).GetMethod("SnapGridNodeCentres", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [new[] { Node, Legacy }]);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That((Node.X, Node.Y), Is.EqualTo(Position));
        Assert.That((Legacy.X, Legacy.Y), Is.EqualTo((30d, 60d)));
    }
}
