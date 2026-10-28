using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class GroupedLoopBranchTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    public void Layout_LoopAndContinuation_StraddleDecisionInsideFrame(string Direction)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            subgraph Pool[Pool]
            Entry[Entry] --> Check{Check}
            Check -->|proceed| Work[Work]
            Check -->|again| Delay[Delay]
            Delay --> Entry
            end
            Before[Before] --> Entry
            Work --> After[After]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds Check = Layout.NodeBounds.Single(Node => Node.NodeId == "Check");
        FlowchartLayoutNodeBounds Work = Layout.NodeBounds.Single(Node => Node.NodeId == "Work");
        FlowchartLayoutNodeBounds Delay = Layout.NodeBounds.Single(Node => Node.NodeId == "Delay");
        FlowchartLayoutNodeBounds Frame = Layout.ClusterBounds.Single();
        Assert.That(Work.CenterX, Is.LessThan(Check.CenterX));
        Assert.That(Delay.CenterX, Is.GreaterThan(Check.CenterX));
        Assert.That(Work.CenterX - (Work.Width / 2), Is.GreaterThan(Frame.CenterX - (Frame.Width / 2)));
        Assert.That(Delay.CenterX + (Delay.Width / 2), Is.LessThan(Frame.CenterX + (Frame.Width / 2)));
        Assert.That(Layout.NodeBounds.Single(Node => Node.NodeId == "After").CenterX, Is.EqualTo(Work.CenterX).Within(0.5));
        foreach (FlowchartLayoutNodeBounds? Child in new[] { Work, Delay })
        {
            FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Check" && Edge.TargetId == Child.NodeId);
            Assert.That(Route.Points, Has.Count.EqualTo(3));
            Assert.That(Route.Points[0].Y, Is.EqualTo(Check.CenterY).Within(0.5));
            Assert.That(Route.Points[^1].X, Is.EqualTo(Child.CenterX).Within(0.5));
            Assert.That(Route.Points[^2].X, Is.EqualTo(Child.CenterX).Within(0.5));
        }
    }
}
