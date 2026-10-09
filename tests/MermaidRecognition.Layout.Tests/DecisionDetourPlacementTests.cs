using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DecisionDetourPlacementTests
{
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void Layout_Detour_RejoinsContinuationFromSide(string Direction, bool Horizontal)
    {
        var Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            Root[Root] --> First{First}
            First -->|detour| Side[Side]
            First -->|direct| Join[Join]
            Side --> Join
            Join --> Last{Last}
            Last -->|save| Store[(Store)]
            Last -->|finish| End([End])
            Store --> End
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var First = Layout.NodeBounds.Single(Node => Node.NodeId == "First");
        var Join = Layout.NodeBounds.Single(Node => Node.NodeId == "Join");
        var Last = Layout.NodeBounds.Single(Node => Node.NodeId == "Last");
        var Store = Layout.NodeBounds.Single(Node => Node.NodeId == "Store");
        var End = Layout.NodeBounds.Single(Node => Node.NodeId == "End");
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        Assert.That(Cross(Join), Is.EqualTo(Cross(First)).Within(0.5));
        Assert.That((Cross(Store) - Cross(Last)) * (Cross(End) - Cross(Last)), Is.LessThan(0));
        var Direct = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "First" && Edge.TargetId == "Join");
        Assert.That(Direct.Points, Has.Count.EqualTo(2));
        foreach (string Source in new[] { "Side", "Store" })
        {
            var Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == Source);
            var Target = Source == "Side" ? Join : End;
            Assert.That(Route.Points, Has.Count.EqualTo(3));
            Assert.That(Horizontal ? Route.Points[^1].X : Route.Points[^1].Y,
                Is.EqualTo(Horizontal ? Target.CenterX : Target.CenterY).Within(0.5));
            Assert.That(Horizontal ? Route.Points[^2].X : Route.Points[^2].Y,
                Is.EqualTo(Horizontal ? Route.Points[^1].X : Route.Points[^1].Y).Within(0.5));
        }
    }
}
