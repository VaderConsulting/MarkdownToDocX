using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartManyToManyRouteTests
{
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
