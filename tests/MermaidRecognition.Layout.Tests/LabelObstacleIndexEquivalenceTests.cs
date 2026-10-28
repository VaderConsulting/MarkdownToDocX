using System.Reflection;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that indexed canvas-label obstacles retain the exhaustive collision result, including strict boundaries.</summary>
[TestFixture]
public sealed class LabelObstacleIndexEquivalenceTests
{
    /// <summary>Compares the accelerated query with the existing exhaustive query over the identical sampled connectors.</summary>
    /// <param name="Bent">Whether the own connector contains several turns instead of one straight run.</param>
    /// <param name="CheckNodes">Whether node rectangles also participate in the query.</param>
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void IndexedOtherSamplesMatchExhaustiveQueries(bool Bent, bool CheckNodes)
    {
        double Step = DiagramGridMetrics.GRID_UNIT_PX;
        List<(double X, double Y)> Own = FlowchartEdgeLabelClearanceHelper.SampleConnector(Bent
            ? [(0, -3 * Step), (0, 0), (3 * Step, 0), (3 * Step, 3 * Step)]
            : new List<(double X, double Y)> { (-3 * Step, 0), (3 * Step, 0) });
        List<(double X, double Y)> Other = FlowchartEdgeLabelClearanceHelper.SampleConnector(
            [(-2 * Step, -3 * Step), (-2 * Step, 2 * Step), (2 * Step, 2 * Step)]);
        List<(double X, double Y)> All = [.. Own, .. Other];
        FlowchartSvgSpatialIndex Index = new FlowchartSvgSpatialIndex();
        foreach ((double X, double Y) Point in Other)
        {
            Index.AddPoint(Point.X, Point.Y);
        }

        FlowchartSvgRect[] Nodes = [new FlowchartSvgRect(-Step, -Step, Step, Step)];
        MethodInfo Query = typeof(FlowchartEdgeLabelClearanceHelper).GetMethod("IsClear", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (int X in Enumerable.Range(-4, 9))
        {
            foreach (int Y in Enumerable.Range(-4, 9))
            {
                foreach (double Width in new[] { Step / 2, Step, 2 * Step })
        {
            var Expected = (bool)Query.Invoke(null, [X * Step, Y * Step, Width, Step, All, Nodes, CheckNodes, null])!;
            var Actual = (bool)Query.Invoke(null, [X * Step, Y * Step, Width, Step, Own, Nodes, CheckNodes, Index])!;
            Assert.That(Actual, Is.EqualTo(Expected), $"query ({X}, {Y}), width {Width}");
        }
            }
        }
    }
}
