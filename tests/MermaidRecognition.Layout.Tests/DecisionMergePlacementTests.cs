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
        var Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            Origin[Origin] --> Gate{Gate}
            Gate -->|first| First[First]
            Gate -->|second| Second[Second]
            First --> Collector([Collector])
            Second --> Collector
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var Decision = Layout.NodeBounds.Single(Node => Node.NodeId == "Gate");
        var Children = Layout.NodeBounds.Where(Node => Node.NodeId is "First" or "Second").ToArray();
        var Merge = Layout.NodeBounds.Single(Node => Node.NodeId == "Collector");
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        Assert.That(Children.Min(Cross), Is.LessThan(Cross(Decision)));
        Assert.That(Children.Max(Cross), Is.GreaterThan(Cross(Decision)));
        Assert.That(Children.Sum(Cross), Is.EqualTo(2 * Cross(Decision)).Within(0.5));
        var SideChild = Children.Single(Node => Math.Abs(Cross(Node) - Cross(Merge)) > 0.5);
        var Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == SideChild.NodeId && Edge.TargetId == "Collector");
        var Start = Route.Points[0];
        var End = Route.Points[^1];
        var Before = Route.Points[^2];
        Assert.That(Horizontal ? Start.Y : Start.X, Is.EqualTo(Cross(SideChild)).Within(0.5));
        Assert.That(Horizontal ? End.X : End.Y, Is.EqualTo(Horizontal ? Merge.CenterX : Merge.CenterY).Within(0.5));
        Assert.That(Horizontal ? Before.X : Before.Y, Is.EqualTo(Horizontal ? End.X : End.Y).Within(0.5));
        Assert.That(Horizontal ? End.Y : End.X,
            Is.EqualTo(Cross(Merge) + Math.Sign(Cross(SideChild) - Cross(Merge)) * (Horizontal ? Merge.Height : Merge.Width) / 2).Within(0.5));
        Assert.That(Route.Points, Has.Count.EqualTo(3), "The side branch should reach the merge with one elbow.");
    }
}
