using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DecisionInputPlacementTests
{
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void Layout_RetryLoop_AlignsInputWithDecision(string Direction, bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            Input[Receive] --> Check{OK?}
            Check -->|Yes| Finish[Done]
            Check -->|No| Retry[Retry]
            Retry --> Input
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds Input = Layout.NodeBounds.Single(Node => Node.NodeId == "Input");
        FlowchartLayoutNodeBounds Decision = Layout.NodeBounds.Single(Node => Node.NodeId == "Check");
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        Assert.That(Cross(Input), Is.EqualTo(Cross(Decision)).Within(0.5));
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Input" && Edge.TargetId == "Check");
        Assert.That(Route.Points.All(Point => Math.Abs((Horizontal ? Point.Y : Point.X) - Cross(Decision)) < 0.5), Is.True);
        double Facing = Horizontal ? Math.Sign(Input.CenterX - Decision.CenterX) : Math.Sign(Input.CenterY - Decision.CenterY);
        Assert.That(Horizontal ? Route.Points[^1].X : Route.Points[^1].Y,
            Is.EqualTo((Horizontal ? Decision.CenterX : Decision.CenterY) + (Facing * (Horizontal ? Decision.Width : Decision.Height) / 2)).Within(0.5));
    }
}