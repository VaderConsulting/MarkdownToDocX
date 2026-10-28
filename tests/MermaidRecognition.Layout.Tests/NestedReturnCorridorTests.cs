using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class NestedReturnCorridorTests
{
    [TestCase("TB")]
    public void AlignedReturnsToOneAncestor_KeepTheLongestOutsideTheShorterReturn(string Direction)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\na --> b\nb --> c\nc --> d\nd --> e\ne --> f\nf --> a\nc --> a");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutEdgeRoute Long = Layout.EdgeRoutes.Single(Route => Route.SourceId == "f" && Route.TargetId == "a");
        FlowchartLayoutEdgeRoute Short = Layout.EdgeRoutes.Single(Route => Route.SourceId == "c" && Route.TargetId == "a");
        Assert.That(Long.ExitSide, Is.EqualTo("left"));
        Assert.That(Long.EntrySide, Is.EqualTo("left"));
        Assert.That(Short.ExitSide, Is.EqualTo("right"));
        Assert.That(Short.EntrySide, Is.EqualTo("right"));
        CheckOrthogonal(Long);
        CheckOrthogonal(Short);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.ToDictionary(Node => Node.NodeId);
        foreach ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair in Long.Points.Zip(Long.Points.Skip(1)))
        {
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(Pair.First.X, Pair.First.Y,
                Pair.Second.X, Pair.Second.Y, Bounds, Long.SourceId, Long.TargetId), Is.False);
        }
    }

    [TestCase("TB")]
    [TestCase("BT")]
    public void CircularTwoCycleReturn_UsesASideCorridorWithNormalTerminalRuns(string Direction)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\na --> b((Join))\nb --> c\nb --> a");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutEdgeRoute Return = Layout.EdgeRoutes.Single(Route => Route.SourceId == "b" && Route.TargetId == "a");
        CheckOrthogonal(Return);
        FlowchartLayoutNodeBounds Circle = Layout.NodeBounds.Single(Node => Node.NodeId == "b");
        Assert.That(Return.ExitSide, Is.EqualTo("right"));
        Assert.That(Return.EntrySide, Is.EqualTo("right"));
        Assert.That(Return.Points[0].X, Is.EqualTo(Circle.CenterX + (Math.Min(Circle.Width, Circle.Height) / 2)).Within(1));
        Assert.That(Return.Points[0].Y, Is.EqualTo(Circle.CenterY).Within(1));
        Assert.That(Return.Points[1].Y, Is.EqualTo(Return.Points[0].Y).Within(0.01));
        Assert.That(Return.Points[^2].Y, Is.EqualTo(Return.Points[^1].Y).Within(0.01));
    }

    private static void CheckOrthogonal(FlowchartLayoutEdgeRoute Route)
    {
        foreach ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair in Route.Points.Zip(Route.Points.Skip(1)))
        {
            Assert.That(Math.Abs(Pair.First.X - Pair.Second.X) < 0.01 || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.01,
                Is.True, "Return segments must be orthogonal.");
        }
    }
}
