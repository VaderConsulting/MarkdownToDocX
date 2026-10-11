using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class AlignedCounterflowTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void OpposingRelationships_UseDistinctStraightTracks(string Direction)
    {
        var Document = EnterpriseValidator.Analyze($"""
            enterprise {Direction}
            application-component component "Component"
            application-service service "Service"
            component -realization-> service
            service -serving-> component
            """).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        Assert.That(Routes, Has.Count.EqualTo(2));
        Assert.That(Routes.All(Route => Route.SvgPoints.Count == 2), Is.True);
        var Intersections = FlowchartBranchRouteOrder.Intersections(Routes[0].SvgPoints, Routes[1].SvgPoints);
        Assert.That(Intersections.Crossings, Is.Zero);
        Assert.That(Intersections.Overlap, Is.Zero);
        foreach (var Route in Routes)
        {
            var A = Route.SvgPoints[0]; var B = Route.SvgPoints[1];
            Assert.That(Math.Abs(A.X - B.X) < 0.01 || Math.Abs(A.Y - B.Y) < 0.01, Is.True);
            foreach (var Node in Layout.Nodes.Values)
                Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y,
                    new FlowchartLayoutNodeBounds { NodeId = Node.Id, CenterX = Node.AbsoluteX + Node.Width / 2,
                        CenterY = Node.AbsoluteY + Node.Height / 2, Width = Node.Width, Height = Node.Height }), Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BlockedOrLabelledPair_PreservesItsExistingDetours(bool Labelled)
    {
        var Contract = MermaidRecognizer.Parse(Labelled ? "flowchart TB\na -->|caption| b\nb --> a" : "flowchart TB\na --> b\nb --> a\nc");
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>
        {
            ["a"] = new() { NodeId = "a", CenterY = 150, Width = 150, Height = 60 },
            ["b"] = new() { NodeId = "b", CenterY = 0, Width = 150, Height = 60 },
            ["c"] = new() { NodeId = "c", CenterY = 75, Width = 120, Height = 30 }
        };
        var Layout = new FlowchartLayoutResult();
        foreach (var Edge in Contract.Edges)
        {
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
            foreach (var Point in new[] { (75d, Bounds[Edge.SourceId].CenterY), (240d, Bounds[Edge.SourceId].CenterY),
                (240d, Bounds[Edge.TargetId].CenterY), (75d, Bounds[Edge.TargetId].CenterY) })
                Route.Points.Add(new() { X = Point.Item1, Y = Point.Item2 });
            Layout.EdgeRoutesMutable.Add(Route);
        }
        FlowchartAlignedCounterflowRoutes.Apply(Layout, Contract, Bounds);
        Assert.That(Layout.EdgeRoutes.All(Route => Route.Points.Count == 4), Is.True);
    }
}
