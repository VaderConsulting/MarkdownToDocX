using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class NestedChainAlignmentTests
{
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    public void Layout_ChainThroughOnlyNestedGroup_KeepsOneCrossCoordinate(string Direction, bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            subgraph Outer[Outer frame]
                direction {{Direction}}
                Before --> Entry
                subgraph Inner[Inner frame]
                    direction {{Direction}}
                    First --> Last
                end
                Entry --> First
                Last --> After
            end
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        var Coordinates = Layout.NodeBounds.Select(Node => Horizontal ? Node.CenterY : Node.CenterX).ToArray();
        Assert.That(Coordinates.Max() - Coordinates.Min(), Is.LessThan(0.5));
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            Assert.That(Route.Points.All(Point => Math.Abs((Horizontal ? Point.Y : Point.X) - Coordinates[0]) < 0.5), Is.True,
                "Each connection remains straight through the group border.");
        }
        foreach (FlowchartLayoutNodeBounds Frame in Layout.ClusterBounds)
        {
            IEnumerable<FlowchartLayoutNodeBounds> Members = Layout.NodeBounds.Where(Node => Frame.NodeId == "Outer" || Node.NodeId is "First" or "Last");
            foreach (FlowchartLayoutNodeBounds? Member in Members)
            {
                Assert.That(Member.CenterX - (Member.Width / 2), Is.GreaterThanOrEqualTo(Frame.CenterX - (Frame.Width / 2) + 29.5));
                Assert.That(Member.CenterX + (Member.Width / 2), Is.LessThanOrEqualTo(Frame.CenterX + (Frame.Width / 2) - 29.5));
                Assert.That(Member.CenterY - (Member.Height / 2), Is.GreaterThanOrEqualTo(Frame.CenterY - (Frame.Height / 2) + 29.5));
                Assert.That(Member.CenterY + (Member.Height / 2), Is.LessThanOrEqualTo(Frame.CenterY + (Frame.Height / 2) - 29.5));
            }
        }
    }
}