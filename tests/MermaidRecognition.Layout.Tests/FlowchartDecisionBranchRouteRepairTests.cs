using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartDecisionBranchRouteRepairTests
{
    [Test]
    public void Layout_OffsetDecisionBranches_LeaveFacingVerticesWithOneCorner()
    {
        var Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Start --> Check{Valid?}
            Check -->|Yes| Accept
            Check -->|No| Reject
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutNodeBounds Diamond = Layout.NodeBounds.Single(Node => Node.NodeId == "Check");

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.SourceId == "Check"))
        {
            FlowchartLayoutNodeBounds Target = Layout.NodeBounds.Single(Node => Node.NodeId == Route.TargetId);
            Assert.That(Route.Points, Has.Count.EqualTo(3));
            Assert.That(Route.Points[0].Y, Is.EqualTo(Diamond.CenterY).Within(0.5));
            Assert.That(Math.Abs(Route.Points[0].X - Diamond.CenterX), Is.EqualTo(Diamond.Width / 2).Within(0.5));
            Assert.That(Route.Points[^1].X, Is.EqualTo(Target.CenterX).Within(0.5));
            Assert.That(Route.Points[^1].Y, Is.EqualTo(Target.CenterY + Target.Height / 2).Within(0.5));
        }
    }
}
