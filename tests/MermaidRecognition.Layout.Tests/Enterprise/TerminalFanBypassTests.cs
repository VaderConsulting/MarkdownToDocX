using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class TerminalFanBypassTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    public void CrossedTerminalBranch_UsesAClearTrackBeyondTheDataRow(string Direction)
    {
        var Document = EnterpriseValidator.Analyze($"""
            enterprise {Direction}
            application-component hub
            application-component first
            application-component second
            application-component third
            application-service left
            application-service middle
            application-service right
            hub -flow-> first
            hub -flow-> second
            hub -flow-> third
            first -serving-> left
            first -serving-> middle
            second -serving-> middle
            second -serving-> right
            third -serving-> left
            third -serving-> right
            """).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        var Bypass = Routes.Single(Route => Route.Relationship.SourceId == "first" && Route.Relationship.TargetId == "middle");
        var Source = Layout.Nodes["first"]; var Target = Layout.Nodes["middle"];
        Assert.That(Bypass.SvgPoints, Has.Count.EqualTo(4));
        double TargetSide = Direction == "TB" ? Target.AbsoluteY + Target.Height : Target.AbsoluteY;
        double SourceSide = Direction == "TB" ? Source.AbsoluteY + Source.Height : Source.AbsoluteY;
        Assert.That(Bypass.SvgPoints[0].Y, Is.EqualTo(SourceSide).Within(0.01));
        Assert.That(Bypass.SvgPoints[^1].Y, Is.EqualTo(TargetSide).Within(0.01));
        Assert.That(Direction == "TB" ? Bypass.SvgPoints[1].Y > TargetSide : Bypass.SvgPoints[1].Y < TargetSide, Is.True);
        foreach (var Peer in Routes.Where(Route => Route != Bypass))
        {
            var Crossing = FlowchartBranchRouteOrder.Intersections(Bypass.SvgPoints, Peer.SvgPoints);
            Assert.That(Crossing.Crossings, Is.Zero);
            Assert.That(Crossing.Overlap, Is.Zero);
        }
        foreach (var Segment in Bypass.SvgPoints.Zip(Bypass.SvgPoints.Skip(1)))
        foreach (var Node in Layout.Nodes.Values)
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X, Segment.First.Y,
                Segment.Second.X, Segment.Second.Y, new FlowchartLayoutNodeBounds { NodeId = Node.Id,
                    CenterX = Node.AbsoluteX + Node.Width / 2, CenterY = Node.AbsoluteY + Node.Height / 2,
                    Width = Node.Width, Height = Node.Height }), Is.False);
    }
}
