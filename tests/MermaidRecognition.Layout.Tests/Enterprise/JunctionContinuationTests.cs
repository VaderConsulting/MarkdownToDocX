using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class JunctionContinuationTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    public void CompactedCollector_KeepsItsTailAlignedAndItsSplitUsesOneBend(string Direction)
    {
        var Document = EnterpriseValidator.Analyze($"""
            enterprise {Direction}
            business-process first
            business-process second
            and-junction joined
            or-junction split
            business-process left
            business-process right
            or-junction collected
            business-event tail
            first -triggering-> joined
            second -triggering-> joined
            joined -triggering-> split
            split -triggering-> left
            split -triggering-> right
            left -triggering-> collected
            right -triggering-> collected
            collected -triggering-> tail
            """).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        double X(string Id) => Layout.Nodes[Id].AbsoluteX + Layout.Nodes[Id].Width / 2;
        Assert.That(X("tail"), Is.EqualTo(X("collected")).Within(0.01));
        var Tail = Routes.Single(Route => Route.Relationship.SourceId == "collected");
        Assert.That(Tail.SvgPoints, Has.Count.EqualTo(2));
        var Branch = Routes.Single(Route => Route.Relationship.SourceId == "split" && Route.Relationship.TargetId == "left");
        Assert.That(Branch.SvgPoints, Has.Count.EqualTo(3));
        Assert.That(Branch.SvgPoints[1].X, Is.EqualTo(Branch.SvgPoints[2].X).Within(0.01));
        Assert.That(Branch.SvgPoints[0].Y, Is.EqualTo(Branch.SvgPoints[1].Y).Within(0.01));
        foreach (var Route in Routes)
        foreach (var Segment in Route.SvgPoints.Zip(Route.SvgPoints.Skip(1)))
        {
            Assert.That(Math.Abs(Segment.First.X - Segment.Second.X) < 0.01
                || Math.Abs(Segment.First.Y - Segment.Second.Y) < 0.01, Is.True);
            foreach (var Node in Layout.Nodes.Values.Where(Node => Node.Id != Route.Relationship.SourceId && Node.Id != Route.Relationship.TargetId))
                Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X, Segment.First.Y,
                    Segment.Second.X, Segment.Second.Y, new FlowchartLayoutNodeBounds { NodeId = Node.Id,
                        CenterX = Node.AbsoluteX + Node.Width / 2, CenterY = Node.AbsoluteY + Node.Height / 2,
                        Width = Node.Width, Height = Node.Height }), Is.False);
        }
        for (int I = 0; I < Routes.Count; I++)
        for (int J = I + 1; J < Routes.Count; J++)
        {
            var Intersections = FlowchartBranchRouteOrder.Intersections(Routes[I].SvgPoints, Routes[J].SvgPoints);
            Assert.That(Intersections.Crossings, Is.Zero);
            Assert.That(Intersections.Overlap, Is.Zero);
        }
    }
}
