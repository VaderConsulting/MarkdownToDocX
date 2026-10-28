using System.Reflection;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that provisional native route coordinates cannot corrupt the finite cell-to-routing coordinate boundary.</summary>
[TestFixture]
public sealed class FlowchartCellCoordinateBoundaryTests
{
    /// <summary>NaN placeholders cannot collapse the reflection origin to zero or change authoritative ownership.</summary>
    /// <param name="NativeY">A finite native guide or a second invalid placeholder.</param>
    /// <param name="ExpectedFlip">Reflection origin from finite node/frame/guide geometry.</param>
    [TestCase(double.NaN, 120)]
    [TestCase(230, 240)]
    public void IgnoresNonfiniteNativeGuideCoordinates(double NativeY, double ExpectedFlip)
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1));
        DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = "A" }; Node.BindCells(Grid);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        List<DiagramGridLayout.GridFrame> Frames = [new() { Id = "G", Left = -30, Right = 180, Top = -30, Bottom = 120 }];
        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = "native-placeholder" };
        Route.Points.Add(new() { X = 0, Y = double.NaN }); Route.Points.Add(new() { X = 0, Y = NativeY });
        Layout.EdgeRoutesMutable.Add(Route);
        typeof(DiagramGridLayout).GetMethod("FlipToYUp", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new List<DiagramGridLayout.GridNode> { Node }, Frames, Layout]);
        Assert.That(Node.Y, Is.EqualTo(ExpectedFlip - 45));
        Assert.That(Frames[0].Top, Is.EqualTo(ExpectedFlip + 30));
        Assert.That(Frames[0].Bottom, Is.EqualTo(ExpectedFlip - 120));
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }
}
