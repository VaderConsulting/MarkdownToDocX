using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ContainedReturnTrackTests
{
    [TestCase("TB", false)]
    [TestCase("TB", true)]
    [TestCase("BT", false)]
    [TestCase("LR", false)]
    [TestCase("LR", true)]
    [TestCase("RL", false)]
    public void InternalBackEdge_RemainsInsideItsCommonFrame(string Direction, bool Branching)
    {
        string Middle = Branching ? "b{Decision}" : "b[Work]";
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\n subgraph g[Container]\n a[Input] --> {Middle}\n b --> c[Retry]\n c --> a\n" + (Branching ? "b --> d[Done]\n" : "") + "end\n z[Outside] --> a");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds Frame = Layout.ClusterBounds.Single(Bounds => Bounds.NodeId == "g");
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "c" && Edge.TargetId == "a");
        Assert.That(Route.Points, Has.Count.GreaterThanOrEqualTo(2));
        foreach (FlowchartLayoutPoint Point in Route.Points)
        {
            Assert.That(Point.X, Is.InRange(Frame.CenterX - (Frame.Width / 2), Frame.CenterX + (Frame.Width / 2)));
            Assert.That(Point.Y, Is.InRange(Frame.CenterY - (Frame.Height / 2), Frame.CenterY + (Frame.Height / 2)));
        }
        foreach (FlowchartLayoutNodeBounds? Node in Layout.NodeBounds.Where(Node => Node.NodeId != Route.SourceId && Node.NodeId != Route.TargetId))
        {
            Assert.That(Route.Points.Zip(Route.Points.Skip(1)).Any(Pair =>
                FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y,
                    new Dictionary<string, FlowchartLayoutNodeBounds> { [Node.NodeId] = Node }, Route.SourceId, Route.TargetId)), Is.False);
        }
    }
}
