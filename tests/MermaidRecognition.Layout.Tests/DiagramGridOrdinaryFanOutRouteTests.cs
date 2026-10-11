using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridOrdinaryFanOutRouteTests
{
    private const string MessageFilterSource = """
        flowchart LR
            In[Topic] --> F[Message filter]
            F -->|pass| OK[Core consumer]
            F -->|drop| T[Telemetry only]
        """;

    [Test]
    public void TryLayout_MessageFilter_UsesDownstreamExitAndUpstreamEntry()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(MessageFilterSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute PassRoute = FindRoute(Layout, "F", "OK");
        FlowchartLayoutEdgeRoute DropRoute = FindRoute(Layout, "F", "T");

        Assert.That(PassRoute.ExitSide, Is.EqualTo("right"), "B14: leave downstream side");
        Assert.That(DropRoute.ExitSide, Is.EqualTo("right"), "B14: leave downstream side");
        Assert.That(PassRoute.EntrySide, Is.EqualTo("left"), "B14: enter upstream side");
        Assert.That(DropRoute.EntrySide, Is.EqualTo("left"), "B14: enter upstream side");
    }

    [Test]
    public void TryLayout_MessageFilter_BranchesEnterLeftSideCentre()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(MessageFilterSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds OkBounds = Find(Layout, "OK");
        FlowchartLayoutNodeBounds TeleBounds = Find(Layout, "T");
        double OkLeft = OkBounds.CenterX - (OkBounds.Width / 2.0);
        double TeleLeft = TeleBounds.CenterX - (TeleBounds.Width / 2.0);

        FlowchartLayoutEdgeRoute PassRoute = FindRoute(Layout, "F", "OK");
        FlowchartLayoutEdgeRoute DropRoute = FindRoute(Layout, "F", "T");

        FlowchartLayoutPoint PassEnd = PassRoute.Points[^1];
        FlowchartLayoutPoint DropEnd = DropRoute.Points[^1];
        Assert.That(PassEnd.X, Is.EqualTo(OkLeft).Within(2.0));
        Assert.That(PassEnd.Y, Is.EqualTo(OkBounds.CenterY).Within(2.0));
        Assert.That(DropEnd.X, Is.EqualTo(TeleLeft).Within(2.0));
        Assert.That(DropEnd.Y, Is.EqualTo(TeleBounds.CenterY).Within(2.0));
    }

    [Test]
    public void TryLayout_MessageFilter_BranchesUseRoundedZShape()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(MessageFilterSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds FilterBounds = Find(Layout, "F");
        double SourceRight = FilterBounds.CenterX + (FilterBounds.Width / 2.0);

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (!string.Equals(Route.SourceId, "F", StringComparison.Ordinal))
            {
                continue;
            }

            FlowchartLayoutNodeBounds TargetBounds = Find(Layout, Route.TargetId);
            double TargetLeft = TargetBounds.CenterX - (TargetBounds.Width / 2.0);
            double ExpectedCorridorX = (SourceRight + TargetLeft) / 2.0;

            AssertRouteIsOrthogonal(Route.Points);
            Assert.That(CountCorners(Route.Points), Is.EqualTo(2), "B14: rounded Z has two corners");
            Assert.That(Route.Points.Any(Point => Math.Abs(Point.X - ExpectedCorridorX) <= 1.0), Is.True,
                "vertical segment uses the rank-gap midpoint");
        }
    }

    [Test]
    public void RenderToSvg_MessageFilter_BranchesEnterLeftSideWithRoundedZ()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(MessageFilterSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain($"data-edge-source=\"F\" data-edge-target=\"OK\""));
        Assert.That(Svg, Does.Contain($"data-edge-source=\"F\" data-edge-target=\"T\""));
        Assert.That(Svg, Does.Not.Contain("curveBasis"), "B14 must not use curved fan routing");
        AssertRoundedStraightRuns(ExtractEdgePath(Svg, "F", "OK"));
        AssertRoundedStraightRuns(ExtractEdgePath(Svg, "F", "T"));
    }

    private static void AssertRoundedStraightRuns(string PathData)
    {
        Assert.That(PathData, Does.Contain('L'), "B14 branches use straight orthogonal runs");
        Assert.That(PathData.Count(Character => Character == 'C' || Character == 'c'), Is.InRange(1, 2),
            "B14 rounds the Z-route elbows without a smoothing spline");
        Assert.That(PathData, Does.Not.Contain('S'), "B14 does not use a smoothing spline");
    }

    private static string ExtractEdgePath(string Svg, string SourceId, string TargetId)
    {
        string Marker = $"data-edge-source=\"{SourceId}\" data-edge-target=\"{TargetId}\"";
        int Index = Svg.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        Assert.That(Index, Is.GreaterThan(0), "missing edge path");
        int PathStart = Svg.LastIndexOf("d=\"", Index);
        int PathEnd = Svg.IndexOf('"', PathStart + 3);
        return Svg.Substring(PathStart + 3, PathEnd - PathStart - 3);
    }

    private static int CountCorners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        if (Points.Count < 3)
        {
            return 0;
        }

        int Corners = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            bool PrevHorizontal = Math.Abs(Points[Index - 1].Y - Points[Index].Y) <= 0.5;
            bool NextHorizontal = Math.Abs(Points[Index].Y - Points[Index + 1].Y) <= 0.5;
            if (PrevHorizontal != NextHorizontal)
            {
                Corners++;
            }
        }

        return Corners;
    }

    private static void AssertRouteIsOrthogonal(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            bool Horizontal = Math.Abs(Points[Index - 1].Y - Points[Index].Y) <= 0.5;
            bool Vertical = Math.Abs(Points[Index - 1].X - Points[Index].X) <= 0.5;
            Assert.That(Horizontal || Vertical, Is.True, $"segment {Index - 1}->{Index} is diagonal");
        }
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
