using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class NestedScopeRouteTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void ParentToNestedScope_UsesAtMostOneClearCorner(string Direction)
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze($"""
            enterprise
            direction {Direction}
            group outer "Outer"
            group inner "Inner" in outer
            application-component source "Source" in outer
            application-component target "Target" in inner
            application-component peer "Peer" in inner
            source -flow-> target "first entry"
            peer -flow-> target "second entry"
            """).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Edges = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        EnterpriseRoutedEdge Route = Edges.Single(Edge => Edge.Relationship.SourceId == "source");
        Assert.That(Route.SvgPoints.Count, Is.InRange(2, 3));
        EnterpriseLayoutNode Target = Layout.Nodes["target"];
        (double X, double Y) End = Route.SvgPoints[^1]; (double X, double Y) Previous = Route.SvgPoints[^2];
        if (Direction == "TB")
        {
            Assert.That(End.Y, Is.EqualTo(Target.AbsoluteY).Within(0.01));
            Assert.That(Previous.X, Is.EqualTo(End.X).Within(0.01));
        }
        foreach (EnterpriseRoutedEdge Edge in Edges)
        {
            foreach (((double X, double Y) First, (double X, double Y) Second) Segment in Edge.SvgPoints.Zip(Edge.SvgPoints.Skip(1)))
            {
                foreach (EnterpriseLayoutNode? Node in Layout.Nodes.Values.Where(Node => !Node.IsCompound))
                {
                    Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X,
                Segment.First.Y, Segment.Second.X, Segment.Second.Y, new FlowchartLayoutNodeBounds
                { NodeId = Node.Id, CenterX = Node.AbsoluteX + (Node.Width / 2),
                    CenterY = Node.AbsoluteY + (Node.Height / 2), Width = Node.Width, Height = Node.Height }), Is.False);
                }
            }
        }

        EnterpriseRoutedEdge Other = Edges.Single(Edge => Edge.Relationship.SourceId == "peer");
        (int Crossings, double Overlap) Intersections = FlowchartBranchRouteOrder.Intersections(Route.SvgPoints, Other.SvgPoints);
        Assert.That(Intersections.Crossings, Is.Zero);
        Assert.That(Intersections.Overlap, Is.Zero);
        Assert.That(Route.LabelBreak, Is.Not.Null);
    }
}
