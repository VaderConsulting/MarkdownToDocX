using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class JunctionContinuationTests
{
    [Test]
    public void LongestForwardMainRun_OwnsTheSharedColumn()
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze("""
            enterprise TB
            technology-event releaseReady "Release Ready"
            technology-process deploy "Deploy Release"
            technology-function hosting "Application Hosting"
            technology-interaction replicate "Session Replication"
            technology-service hostingSvc "Hosting Service"
            releaseReady -triggering-> deploy
            deploy -flow-> hosting "release"
            hosting -realization-> hostingSvc
            replicate -flow-> hosting
            """).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        double[] MainRun = [.. new[] { "releaseReady", "deploy", "hosting", "hostingSvc" }.Select(Id => Layout.Nodes[Id].AbsoluteX + (Layout.Nodes[Id].Width / 2))];
        Assert.That(MainRun.Max() - MainRun.Min(), Is.LessThanOrEqualTo(1.0));
    }

    [Test]
    public void SplitBranch_EntersLowerOffsetTargetFromTheFacingRightSide()
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze("""
            enterprise TB
            business-process assess "Assess Return"
            or-junction choice "Refund or replace?"
            business-process refund "Issue Refund"
            business-process replace "Send Replacement"
            junction-or choice2
            junction plainJoin
            junction-and andJoin
            business-process close "Close Case"
            assess -flow-> choice
            choice -flow-> refund
            choice -flow-> replace
            refund -flow-> choice2
            replace -flow-> choice2
            choice2 -flow-> plainJoin
            plainJoin -flow-> andJoin
            andJoin -flow-> close
            """).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        EnterpriseRoutedEdge Branch = Routes.Single(Route => Route.Relationship.SourceId == "choice"
            && Route.Relationship.TargetId == "refund");
        EnterpriseLayoutNode Target = Layout.Nodes["refund"];

        Assert.That(Branch.SvgPoints.Count, Is.LessThanOrEqualTo(4));
        Assert.That(Branch.SvgPoints[^1].X, Is.EqualTo(Target.AbsoluteX + Target.Width).Within(0.01));
        Assert.That(Branch.SvgPoints[^1].Y, Is.EqualTo(Target.AbsoluteY + (Target.Height / 2)).Within(0.01));
    }

    [TestCase("TB")]
    [TestCase("BT")]
    public void CompactedCollector_KeepsItsTailAlignedAndItsSplitUsesOneBend(string Direction)
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze($"""
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
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        double X(string Id) => Layout.Nodes[Id].AbsoluteX + (Layout.Nodes[Id].Width / 2);
        Assert.That(X("tail"), Is.EqualTo(X("collected")).Within(0.01));
        EnterpriseRoutedEdge Tail = Routes.Single(Route => Route.Relationship.SourceId == "collected");
        Assert.That(Tail.SvgPoints, Has.Count.EqualTo(2));
        EnterpriseRoutedEdge Branch = Routes.Single(Route => Route.Relationship.SourceId == "split" && Route.Relationship.TargetId == "left");
        Assert.That(Branch.SvgPoints, Has.Count.EqualTo(3));
        Assert.That(Branch.SvgPoints[1].X, Is.EqualTo(Branch.SvgPoints[2].X).Within(0.01));
        Assert.That(Branch.SvgPoints[0].Y, Is.EqualTo(Branch.SvgPoints[1].Y).Within(0.01));
        foreach (EnterpriseRoutedEdge Route in Routes)
        {
            foreach (((double X, double Y) First, (double X, double Y) Second) Segment in Route.SvgPoints.Zip(Route.SvgPoints.Skip(1)))
        {
            Assert.That(Math.Abs(Segment.First.X - Segment.Second.X) < 0.01
                || Math.Abs(Segment.First.Y - Segment.Second.Y) < 0.01, Is.True);
            foreach (EnterpriseLayoutNode? Node in Layout.Nodes.Values.Where(Node => Node.Id != Route.Relationship.SourceId && Node.Id != Route.Relationship.TargetId))
                {
                    Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X, Segment.First.Y,
                    Segment.Second.X, Segment.Second.Y, new FlowchartLayoutNodeBounds { NodeId = Node.Id,
                        CenterX = Node.AbsoluteX + (Node.Width / 2), CenterY = Node.AbsoluteY + (Node.Height / 2),
                        Width = Node.Width, Height = Node.Height }), Is.False);
                }
            }
        }

        for (int I = 0; I < Routes.Count; I++)
        {
            for (int J = I + 1; J < Routes.Count; J++)
        {
                (int Crossings, double Overlap) Intersections = FlowchartBranchRouteOrder.Intersections(Routes[I].SvgPoints, Routes[J].SvgPoints);
            Assert.That(Intersections.Crossings, Is.Zero);
            Assert.That(Intersections.Overlap, Is.Zero);
        }
        }
    }
}
