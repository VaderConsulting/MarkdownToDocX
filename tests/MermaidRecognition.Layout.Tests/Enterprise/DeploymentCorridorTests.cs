using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class DeploymentCorridorTests
{
    [Test]
    public void DeepDeployment_UsesOrthogonalRoutesClearOfUnrelatedContainers()
    {
        var Source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "EnterpriseFlowchartSamples", "technology-deployment.mmd"));
        var Document = EnterpriseValidator.Analyze(Source).Document;
        var Contract = EnterpriseFlowchartContractMapper.Build(Document);
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        Assert.That(Routes.Count, Is.GreaterThan(20));
        foreach (var Route in Routes)
        foreach (var Segment in Route.SvgPoints.Zip(Route.SvgPoints.Skip(1)))
        {
            Assert.That(Math.Abs(Segment.First.X - Segment.Second.X) < 0.01
                || Math.Abs(Segment.First.Y - Segment.Second.Y) < 0.01, Is.True,
                $"Diagonal: {Route.Relationship.SourceId} -> {Route.Relationship.TargetId}");
            foreach (var Node in Layout.Nodes.Values)
            {
                if (Node.Id == Route.Relationship.SourceId || Node.Id == Route.Relationship.TargetId
                    || Contract.Groups.Any(Group => Group.Id == Node.Id
                        && (FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.Relationship.SourceId)
                            || FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.Relationship.TargetId)))) continue;
                Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X,
                    Segment.First.Y, Segment.Second.X, Segment.Second.Y, new FlowchartLayoutNodeBounds
                    { NodeId = Node.Id, CenterX = Node.AbsoluteX + Node.Width / 2,
                        CenterY = Node.AbsoluteY + Node.Height / 2, Width = Node.Width, Height = Node.Height }), Is.False,
                    $"Body cut: {Route.Relationship.SourceId} -> {Route.Relationship.TargetId} through {Node.Id}");
            }
        }
    }
}
