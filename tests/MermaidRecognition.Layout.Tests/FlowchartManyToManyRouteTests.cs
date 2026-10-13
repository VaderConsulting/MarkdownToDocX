using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartManyToManyRouteTests
{
    [Test]
    public void Layout_F136_CompleteTwoByThreeBlockUsesOnlyForcedCrossingsAndClearRoutes()
    {
        var Contract = MermaidRecognizer.Parse("""
            flowchart LR
            Web[Web feed]
            Api[API feed]
            Index[Search index]
            Cache[Cache]
            Audit[Audit log]
            Web --> Index
            Web --> Cache
            Web --> Audit
            Api --> Index
            Api --> Cache
            Api --> Audit
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        int Crossings = 0;
        for (int First = 0; First < Layout.EdgeRoutes.Count; First++)
        for (int Second = First + 1; Second < Layout.EdgeRoutes.Count; Second++)
        {
            var A = Layout.EdgeRoutes[First];
            var B = Layout.EdgeRoutes[Second];
            var Interaction = FlowchartBranchRouteOrder.Intersections(
                A.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                B.Points.Select(Point => (Point.X, Point.Y)).ToList());
            Crossings += Interaction.Crossings;
            Assert.That(Interaction.Overlap, Is.LessThanOrEqualTo(0.5), $"{A.EdgeId} overlaps {B.EdgeId}");
        }
        Assert.That(Crossings, Is.EqualTo(3), "K2,3 has a forced minimum of three crossings. "
            + string.Join("; ", Layout.EdgeRoutes.Select(Route => Route.SourceId + "->" + Route.TargetId + ": "
                + string.Join(" ", Route.Points.Select(Point => $"({Point.X},{Point.Y})")))) + " bounds: "
            + string.Join("; ", Layout.NodeBounds.Select(Box => $"{Box.NodeId}=({Box.CenterX},{Box.CenterY},{Box.Width},{Box.Height})")));

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Where(Box =>
                     Box.NodeId != Route.SourceId && Box.NodeId != Route.TargetId))
        foreach (var Segment in Route.Points.Zip(Route.Points.Skip(1)))
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Segment.First.X, Segment.First.Y, Segment.Second.X, Segment.Second.Y, Box), Is.False,
                $"{Route.EdgeId} passes through {Box.NodeId}");
    }

    [Test]
    public void Layout_UnlabelledAdjacentRanks_OptimisesLinesThatShareAHub()
    {
        var Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Hub --> W1
            Hub --> W2
            Hub --> W3
            W1 --> Out1
            W1 --> Out2
            W2 --> Out2
            W2 --> Out3
            W3 --> Out1
            W3 --> Out3
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        AssertNoInteraction(Layout, "Hub", "W1", "Hub", "W2");
        AssertNoInteraction(Layout, "Hub", "W1", "Hub", "W3");
        AssertNoInteraction(Layout, "Hub", "W2", "Hub", "W3");
        AssertNoInteraction(Layout, "W1", "Out2", "W1", "Out1");
        AssertNoInteraction(Layout, "W2", "Out2", "W2", "Out3");
        AssertNoInteraction(Layout, "W3", "Out1", "W3", "Out3");
    }

    private static void AssertNoInteraction(FlowchartLayoutResult Layout,
        string SourceA, string TargetA, string SourceB, string TargetB)
    {
        FlowchartLayoutEdgeRoute A = Layout.EdgeRoutes.Single(Route => Route.SourceId == SourceA && Route.TargetId == TargetA);
        FlowchartLayoutEdgeRoute B = Layout.EdgeRoutes.Single(Route => Route.SourceId == SourceB && Route.TargetId == TargetB);
        var Interaction = FlowchartBranchRouteOrder.Intersections(
            A.Points.Select(Point => (Point.X, Point.Y)).ToList(),
            B.Points.Select(Point => (Point.X, Point.Y)).ToList());
        Assert.That(Interaction.Crossings, Is.Zero);
        Assert.That(Interaction.Overlap, Is.Zero);
    }
}
