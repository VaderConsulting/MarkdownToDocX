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
        Assert.That(Svg, Does.Match(@"x1=""477"" y1=""\d+"" x2=""477"""),
            "pass vertical run sits at the rank-gap midpoint");
        Assert.That(Svg, Does.Contain(@"path d=""M477,38L537,38"""),
            "pass finishes with a horizontal run into Core consumer left side");
        Assert.That(Svg, Does.Contain(@"path d=""M477,218L537,218"""),
            "drop finishes with a horizontal run into Telemetry only left side");

        string ArtifactDir = "/opt/cursor/artifacts";
        if (Directory.Exists(ArtifactDir))
        {
            File.WriteAllText(Path.Combine(ArtifactDir, "message-filter.svg"), Svg);
        }
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
