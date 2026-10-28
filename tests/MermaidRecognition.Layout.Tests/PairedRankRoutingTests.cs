using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class PairedRankRoutingTests
{
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void Layout_TwoParentsWithSharedChildren_AlignsDropsAndSeparatesCrossingTracks(string Direction, bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nInputOne & InputTwo --> OutputOne & OutputTwo");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        FlowchartLayoutNodeBounds[] Parents = [.. Layout.NodeBounds.Where(Node => Node.NodeId.StartsWith("Input")).OrderBy(Cross)];
        FlowchartLayoutNodeBounds[] Children = [.. Layout.NodeBounds.Where(Node => Node.NodeId.StartsWith("Output")).OrderBy(Cross)];
        for (int Index = 0; Index < 2; Index++)
        {
            Assert.That(Cross(Children[Index]), Is.EqualTo(Cross(Parents[Index])).Within(0.5));
            FlowchartLayoutEdgeRoute Straight = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == Parents[Index].NodeId && Edge.TargetId == Children[Index].NodeId);
            Assert.That(Straight.Points, Has.Count.EqualTo(2));
            FlowchartLayoutEdgeRoute Crossed = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == Parents[Index].NodeId && Edge.TargetId == Children[1 - Index].NodeId);
            Assert.That(Crossed.Points, Has.Count.EqualTo(4));
            Assert.That(Horizontal ? Crossed.Points[0].Y : Crossed.Points[0].X,
                Is.EqualTo(Horizontal ? Crossed.Points[1].Y : Crossed.Points[1].X).Within(0.5));
            Assert.That(Horizontal ? Crossed.Points[^1].Y : Crossed.Points[^1].X,
                Is.EqualTo(Horizontal ? Crossed.Points[^2].Y : Crossed.Points[^2].X).Within(0.5));
            Assert.That(Crossed.Points.Zip(Crossed.Points.Skip(1)).All(Pair => Math.Abs(Pair.First.X - Pair.Second.X) < 0.5
                || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.5), Is.True);
        }
    }
}
