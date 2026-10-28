using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class AlignedReturnPortTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    public void VerticalCyclicChainReturn_UsesClearOrthogonalRunsAndSquareAttachments(string Direction)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\na --> b\nb --> c\nd --> a\nc --- a");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Route => Route.SourceId == "c" && Route.TargetId == "a");
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.ToDictionary(Node => Node.NodeId);
        Assert.That(Route.Points.Count, Is.InRange(2, 5));
        foreach ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair in Route.Points.Zip(Route.Points.Skip(1)))
        {
            Assert.That(Math.Abs(Pair.First.X - Pair.Second.X) < 0.01
                || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.01, Is.True,
                "The return must contain no diagonal segments.");
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Bounds, Route.SourceId, Route.TargetId), Is.False);
        }
        CheckEnd(Bounds[Route.SourceId], Route.Points[0], Route.Points[1], Route.ExitSide, 40);
        CheckEnd(Bounds[Route.TargetId], Route.Points[^1], Route.Points[^2], Route.EntrySide, 30);
        if (Route.Points.Count > 2)
        {
            Assert.That(Route.EntrySide, Is.EqualTo(Route.ExitSide), "An aligned blocked return uses one side corridor.");
        }
    }

    private static void CheckEnd(FlowchartLayoutNodeBounds Box, FlowchartLayoutPoint End,
        FlowchartLayoutPoint Neighbour, string? Side, double Minimum)
    {
        bool HorizontalNormal = Side is "left" or "right";
        double Sign = Side is "left" or "bottom" ? -1 : 1;
        if (HorizontalNormal)
        {
            Assert.That(End.X, Is.EqualTo(Box.CenterX + (Sign * Box.Width / 2)).Within(0.01));
            Assert.That(Neighbour.Y, Is.EqualTo(End.Y).Within(0.01));
            Assert.That(Sign * (Neighbour.X - End.X), Is.GreaterThanOrEqualTo(Minimum));
        }
        else
        {
            Assert.That(End.Y, Is.EqualTo(Box.CenterY + (Sign * Box.Height / 2)).Within(0.01));
            Assert.That(Neighbour.X, Is.EqualTo(End.X).Within(0.01));
            Assert.That(Sign * (Neighbour.Y - End.Y), Is.GreaterThanOrEqualTo(Minimum));
        }
    }
}
