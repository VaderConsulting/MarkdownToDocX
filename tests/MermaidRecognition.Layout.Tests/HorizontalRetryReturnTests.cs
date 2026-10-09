using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class HorizontalRetryReturnTests
{
    private const string RetrySource = """
        flowchart LR
        A[Receive] --> B{OK?}
        B -->|Yes| C[Done]
        B -->|No| D[Retry]
        D --> A
        """;

    [Test]
    public void TryLayout_HorizontalRetry_RanksAreEqualSpacedGridColumns()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RetrySource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double ReceiveX = Find(Layout, "A").CenterX;
        double DecisionX = Find(Layout, "B").CenterX;
        double DoneX = Find(Layout, "C").CenterX;
        double RetryX = Find(Layout, "D").CenterX;

        Assert.That(RetryX, Is.EqualTo(DecisionX).Within(1.0), "Retry sits directly below the decision");
        Assert.That(DecisionX - ReceiveX, Is.EqualTo(DoneX - DecisionX).Within(2.0), "Equal rank-column gaps");
    }

    [Test]
    public void TryLayout_HorizontalRetry_RetryLoop_LeavesBottomAndEntersTargetBottom()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RetrySource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "D", "A");
        Assert.That(Route.ExitSide, Is.EqualTo("bottom"));
        Assert.That(Route.EntrySide, Is.EqualTo("bottom"));
        Assert.That(Route.Points, Has.Count.EqualTo(4));
        Assert.That(Route.Points[0].X, Is.EqualTo(Route.Points[1].X).Within(0.5));
        Assert.That(Route.Points[^1].X, Is.EqualTo(Route.Points[^2].X).Within(0.5));
    }

    [Test]
    public void TryLayout_HorizontalRetry_RetryLoop_RunsBelowAllNodes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RetrySource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double LowestNodeBottom = Layout.NodeBounds
            .Select(Bounds => Bounds.CenterY - (Bounds.Height / 2.0))
            .Min();
        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "D", "A");

        double CorridorY = Route.Points
            .Select(Point => Point.Y)
            .Min();
        Assert.That(CorridorY, Is.LessThan(LowestNodeBottom - 20.0));
    }

    [Test]
    public void TryLayout_HorizontalRetry_DecisionBranches_UseVertexExits()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RetrySource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Decision = Find(Layout, "B");
        double HalfHeight = Decision.Height / 2.0;
        FlowchartLayoutEdgeRoute YesRoute = FindRoute(Layout, "B", "C");
        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "B", "D");

        Assert.That(YesRoute.Points[0].Y, Is.EqualTo(Decision.CenterY).Within(0.5));
        Assert.That(YesRoute.Points[0].X, Is.EqualTo(Decision.CenterX + Decision.Width / 2).Within(0.5));
        Assert.That(NoRoute.Points[0].Y, Is.EqualTo(Decision.CenterY - HalfHeight).Within(0.5));
        Assert.That(NoRoute.Points[0].X, Is.EqualTo(Decision.CenterX).Within(0.5));
    }

    [Test]
    public void RenderToSvg_HorizontalRetry_RetryLoop_RoutesBelowNodes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RetrySource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Match RetryPath = Regex.Match(
            Svg,
            @"<path d=""([^""]+)"" fill=""none"" stroke=""[^""]+""[^>]*data-edge-source=""D"" data-edge-target=""A""");
        Assert.That(RetryPath.Success, Is.True);
        Assert.That(RetryPath.Groups[1].Value, Does.Contain("C"), "Return bends stay rounded");

        MatchCollection Numbers = Regex.Matches(RetryPath.Groups[1].Value, @"-?\d+(?:\.\d+)?");
        List<double> YValues = [];
        for (int Index = 1; Index < Numbers.Count; Index += 2)
        {
            YValues.Add(double.Parse(Numbers[Index].Value));
        }

        Assert.That(YValues.Max(), Is.GreaterThan(300.0), "Return track should run below the node band in SVG space");
    }

    private static FlowchartLayoutNodeBounds Find(FlowchartLayoutResult Layout, string Id)
    {
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            if (string.Equals(Bounds.NodeId, Id, StringComparison.Ordinal))
            {
                return Bounds;
            }
        }

        Assert.Fail("Missing node " + Id);
        return new FlowchartLayoutNodeBounds();
    }

    private static FlowchartLayoutEdgeRoute FindRoute(FlowchartLayoutResult Layout, string SourceId, string TargetId)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (string.Equals(Route.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Route.TargetId, TargetId, StringComparison.Ordinal))
            {
                return Route;
            }
        }

        Assert.Fail("Missing route " + SourceId + "->" + TargetId);
        return new FlowchartLayoutEdgeRoute();
    }
}
