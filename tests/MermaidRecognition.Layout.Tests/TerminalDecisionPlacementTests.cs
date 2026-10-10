using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class TerminalDecisionPlacementTests
{
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void Layout_TwoTerminalOutcomes_StraddleDecisionAndEnterUpstreamFace(string Direction, bool Horizontal)
    {
        var Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            Input([Input]) --> Choice{Choose}
            Choice -->|first| One[First outcome]
            Choice -->|second| Two[Second outcome]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        var Parent = Layout.NodeBounds.Single(Node => Node.NodeId == "Choice");
        var Children = Layout.NodeBounds.Where(Node => Node.NodeId is "One" or "Two").ToArray();
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        double Main(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterX : Node.CenterY;
        Assert.That(Children.Select(Cross).Min(), Is.LessThan(Cross(Parent)));
        Assert.That(Children.Select(Cross).Max(), Is.GreaterThan(Cross(Parent)));
        Assert.That(Cross(Children[0]) + Cross(Children[1]), Is.EqualTo(2 * Cross(Parent)).Within(0.5));
        Assert.That(Main(Children[0]), Is.EqualTo(Main(Children[1])).Within(0.5));
        foreach (var Child in Children)
        {
            var Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Choice" && Edge.TargetId == Child.NodeId);
            var End = Route.Points[^1];
            var Before = Route.Points[^2];
            var Start = Route.Points[0];
            double Side = Math.Sign(Cross(Child) - Cross(Parent));
            Assert.That(Horizontal ? Start.X : Start.Y, Is.EqualTo(Main(Parent)).Within(0.5));
            Assert.That(Horizontal ? Start.Y : Start.X,
                Is.EqualTo(Cross(Parent) + Side * (Horizontal ? Parent.Height : Parent.Width) / 2).Within(0.5),
                "The branch must leave the facing diamond vertex.");
            double FacingSign = Math.Sign(Main(Parent) - Main(Child));
            if (Horizontal)
            {
                Assert.That(End.Y, Is.EqualTo(Child.CenterY).Within(0.5));
                Assert.That(End.X, Is.EqualTo(Child.CenterX + FacingSign * Child.Width / 2).Within(0.5));
                Assert.That(Before.Y, Is.EqualTo(End.Y).Within(0.5));
            }
            else
            {
                Assert.That(End.X, Is.EqualTo(Child.CenterX).Within(0.5));
                Assert.That(End.Y, Is.EqualTo(Child.CenterY + FacingSign * Child.Height / 2).Within(0.5));
                Assert.That(Before.X, Is.EqualTo(End.X).Within(0.5));
            }
            int Corners = 0;
            for (int Index = 1; Index < Route.Points.Count - 1; Index++)
            {
                var A = Route.Points[Index - 1];
                var B = Route.Points[Index];
                var C = Route.Points[Index + 1];
                if (Math.Abs((B.X - A.X) * (C.Y - B.Y) - (B.Y - A.Y) * (C.X - B.X)) > 0.5)
                {
                    Corners++;
                }
            }
            Assert.That(Corners, Is.EqualTo(1), "Each terminal branch needs one elbow and no return to the spine.");
        }
    }
}
