using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DecisionMergePlacementTests
{
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void Layout_IndependentBranchesToTerminal_StraddleDecisionAndMergeFromSide(string Direction, bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            Origin[Origin] --> Gate{Gate}
            Gate -->|first| First[First]
            Gate -->|second| Second[Second]
            First --> Collector([Collector])
            Second --> Collector
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds Decision = Layout.NodeBounds.Single(Node => Node.NodeId == "Gate");
        FlowchartLayoutNodeBounds[] Children = [.. Layout.NodeBounds.Where(Node => Node.NodeId is "First" or "Second")];
        FlowchartLayoutNodeBounds Merge = Layout.NodeBounds.Single(Node => Node.NodeId == "Collector");
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        Assert.That(Children.Min(Cross), Is.LessThan(Cross(Decision)));
        Assert.That(Children.Max(Cross), Is.GreaterThan(Cross(Decision)));
        Assert.That(Children.Sum(Cross), Is.EqualTo(2 * Cross(Decision)).Within(0.5));
        FlowchartLayoutNodeBounds SideChild = Children.Single(Node => Math.Abs(Cross(Node) - Cross(Merge)) > 0.5);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == SideChild.NodeId && Edge.TargetId == "Collector");
        FlowchartLayoutPoint Start = Route.Points[0];
        FlowchartLayoutPoint End = Route.Points[^1];
        FlowchartLayoutPoint Before = Route.Points[^2];
        Assert.That(Horizontal ? Start.Y : Start.X, Is.EqualTo(Cross(SideChild)).Within(0.5));
        Assert.That(Horizontal ? End.X : End.Y, Is.EqualTo(Horizontal ? Merge.CenterX : Merge.CenterY).Within(0.5));
        Assert.That(Horizontal ? Before.X : Before.Y, Is.EqualTo(Horizontal ? End.X : End.Y).Within(0.5));
        Assert.That(Horizontal ? End.Y : End.X,
            Is.EqualTo(Cross(Merge) + (Math.Sign(Cross(SideChild) - Cross(Merge)) * (Horizontal ? Merge.Height : Merge.Width) / 2)).Within(0.5));
        Assert.That(Route.Points, Has.Count.EqualTo(3), "The side branch should reach the merge with one elbow.");
    }
}
