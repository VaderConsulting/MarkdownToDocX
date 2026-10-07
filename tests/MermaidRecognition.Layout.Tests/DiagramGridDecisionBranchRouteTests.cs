using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridDecisionBranchRouteTests
{
    private const string ValidDecisionSource = """
        flowchart LR
            A["Incoming message"] --> B{"Valid?"}
            B -- Yes --> C["Normal channel"]
            B -- No --> D["Invalid message channel"]
        """;

    private const string InspectContentSource = """
        flowchart TD
            A["Incoming message"] --> B{"Inspect content"}
            B -- Rule A --> C["Destination A"]
            B -- Rule B --> D["Destination B"]
        """;

    [Test]
    public void TryLayout_ValidDecision_UsesFacingSidePorts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(ValidDecisionSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute YesRoute = FindRoute(Layout, "B", "C");
        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "B", "D");
        FlowchartLayoutNodeBounds Diamond = Find(Layout, "B");

        double TopRightY = Diamond.CenterY + (Diamond.Height / 4.0);
        double LowerRightY = Diamond.CenterY - (Diamond.Height / 4.0);
        Assert.That(YesRoute.Points[0].Y, Is.GreaterThan(Diamond.CenterY));
        Assert.That(NoRoute.Points[0].Y, Is.LessThan(Diamond.CenterY));
        Assert.That(YesRoute.Points[0].Y, Is.EqualTo(TopRightY).Within(2.0));
        Assert.That(NoRoute.Points[0].Y, Is.EqualTo(LowerRightY).Within(2.0));
    }

    [Test]
    public void TryLayout_ValidDecision_BranchesUseSingleElbowOrthogonalPaths()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(ValidDecisionSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (!string.Equals(Route.SourceId, "B", StringComparison.Ordinal))
            {
                continue;
            }

            AssertRouteIsOrthogonal(Route.Points);
            Assert.That(CountCorners(Route.Points), Is.LessThanOrEqualTo(1));
        }
    }

    [Test]
    public void RenderToSvg_ValidDecision_BranchesUseOrthogonalRunsWithDiamondStub()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(ValidDecisionSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("data-edge-source=\"B\" data-edge-target=\"C\""));
        Assert.That(Svg, Does.Contain("data-edge-source=\"B\" data-edge-target=\"D\""));
        Assert.That(Svg, Does.Contain("path d=\"M413,"), "B12 branches leave the diamond on a vertical run");
        Assert.That(Svg, Does.Contain("y2=\"53\""), "Yes branch runs horizontally into the target");
        Assert.That(Svg, Does.Contain("y2=\"383\""), "No branch runs horizontally into the target");
    }

    [Test]
    public void TryLayout_InspectContent_UsesFacingSidePorts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(InspectContentSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute RuleARoute = FindRoute(Layout, "B", "C");
        FlowchartLayoutEdgeRoute RuleBRoute = FindRoute(Layout, "B", "D");
        FlowchartLayoutNodeBounds Diamond = Find(Layout, "B");

        double LowerLeftY = Diamond.CenterY - (Diamond.Height / 4.0);
        double LowerRightY = Diamond.CenterY - (Diamond.Height / 4.0);
        double BottomVertexY = Diamond.CenterY - (Diamond.Height / 2.0);
        Assert.That(RuleARoute.Points[0].X, Is.LessThan(Diamond.CenterX));
        Assert.That(RuleBRoute.Points[0].X, Is.GreaterThan(Diamond.CenterX));
        Assert.That(RuleARoute.Points[0].Y, Is.EqualTo(LowerLeftY).Within(2.0));
        Assert.That(RuleBRoute.Points[0].Y, Is.EqualTo(LowerRightY).Within(2.0));
        Assert.That(RuleARoute.Points[0].Y, Is.GreaterThan(BottomVertexY));
        Assert.That(RuleBRoute.Points[0].Y, Is.GreaterThan(BottomVertexY));
    }

    [Test]
    public void TryLayout_InspectContent_BranchesUseSingleElbowOrthogonalPaths()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(InspectContentSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (!string.Equals(Route.SourceId, "B", StringComparison.Ordinal))
            {
                continue;
            }

            AssertRouteIsOrthogonal(Route.Points);
            Assert.That(CountCorners(Route.Points), Is.LessThanOrEqualTo(1));
            Assert.That(Route.Points[^1].X, Is.EqualTo(Find(Layout, Route.TargetId).CenterX).Within(2.0));
        }
    }

    [Test]
    public void RenderToSvg_InspectContent_BranchesUseOrthogonalRunsWithDiamondStub()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(InspectContentSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        AssertInspectContentBranchRendering(Svg, "C", ExpectedDropX: 98);
        AssertInspectContentBranchRendering(Svg, "D", ExpectedDropX: 518);
    }

    private static void AssertInspectContentBranchRendering(string Svg, string TargetId, int ExpectedDropX)
    {
        string Marker = $"data-edge-source=\"B\" data-edge-target=\"{TargetId}\"";
        Assert.That(Svg, Does.Contain(Marker));
        Assert.That(Svg, Does.Contain($"y1=\"278\""), "B12a horizontal run before the elbow");
        Assert.That(
            Svg,
            Does.Contain(
                "path d=\"M"
                + ExpectedDropX
                + ",278L"
                + ExpectedDropX
                + ",413\""
                + " fill=\"none\" stroke=\"#333333\" stroke-width=\"1\" stroke-linejoin=\"round\" stroke-linecap=\"round\" "
                + Marker));
    }

    private static void AssertRoundedOrthogonalBranchPath(string PathData)
    {
        Assert.That(PathData, Does.Contain('L'), "B12 branches use straight horizontal/vertical runs");
        int CubicCount = PathData.Count(Character => Character == 'C' || Character == 'c');
        Assert.That(CubicCount, Is.LessThanOrEqualTo(1), "B12 allows at most one rounded elbow, not curveBasis splines");
        Assert.That(PathData, Does.Not.Contain('S'), "B12 must not use smooth cubic continuation from curveBasis");
    }

    private static int CountCorners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Corners = 0;
        for (int Index = 2; Index < Points.Count; Index++)
        {
            bool PrevHorizontal = Math.Abs(Points[Index - 2].Y - Points[Index - 1].Y) <= 0.5;
            bool NextHorizontal = Math.Abs(Points[Index - 1].Y - Points[Index].Y) <= 0.5;
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

    private static string ExtractEdgePath(string Svg, string SourceId, string TargetId)
    {
        string Marker = $"data-edge-source=\"{SourceId}\" data-edge-target=\"{TargetId}\"";
        int Index = Svg.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        Assert.That(Index, Is.GreaterThan(0), "missing edge path");
        int PathStart = Svg.LastIndexOf("d=\"", Index);
        int PathEnd = Svg.IndexOf('"', PathStart + 3);
        return Svg.Substring(PathStart + 3, PathEnd - PathStart - 3);
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
