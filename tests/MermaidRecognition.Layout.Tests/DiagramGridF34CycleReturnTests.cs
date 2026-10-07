using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridF34CycleReturnTests
{
    private const string F34Source = """
        flowchart LR
        A[Receive] --> B{OK?}
        B -->|Yes| C[Done]
        B -->|No| D[Retry]
        D --> A
        """;

    [Test]
    public void TryLayout_F34_RanksAreEqualSpacedGridColumns()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F34Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double ReceiveX = Find(Layout, "A").CenterX;
        double DecisionX = Find(Layout, "B").CenterX;
        double DoneX = Find(Layout, "C").CenterX;
        double RetryX = Find(Layout, "D").CenterX;

        Assert.That(DoneX, Is.EqualTo(RetryX).Within(1.0), "Done and Retry share the target rank column");
        Assert.That(DecisionX - ReceiveX, Is.EqualTo(DoneX - DecisionX).Within(2.0), "Equal rank-column gaps");
    }

    [Test]
    public void TryLayout_F34_RetryLoop_LeavesRightAndEntersTargetBottom()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F34Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "D", "A");
        Assert.That(Route.ExitSide, Is.EqualTo("right"));
        Assert.That(Route.EntrySide, Is.EqualTo("bottom"));
        Assert.That(Route.Points, Has.Count.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void TryLayout_F34_RetryLoop_RunsBelowAllNodes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F34Source);
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
    public void TryLayout_F34_DecisionBranches_UseVertexExits()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F34Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Decision = Find(Layout, "B");
        double HalfHeight = Decision.Height / 2.0;
        FlowchartLayoutEdgeRoute YesRoute = FindRoute(Layout, "B", "C");
        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "B", "D");

        Assert.That(YesRoute.Points[0].Y, Is.GreaterThan(Decision.CenterY + HalfHeight - 4.0));
        Assert.That(NoRoute.Points[0].Y, Is.LessThan(Decision.CenterY - HalfHeight + 4.0));
    }

    [Test]
    public void RenderToSvg_F34_RetryLoop_RoutesBelowNodes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F34Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Match RetryPath = Regex.Match(
            Svg,
            @"<path d=""([^""]+)"" fill=""none"" stroke=""[^""]+""[^>]*data-edge-source=""D"" data-edge-target=""A""");
        Assert.That(RetryPath.Success, Is.True);

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
