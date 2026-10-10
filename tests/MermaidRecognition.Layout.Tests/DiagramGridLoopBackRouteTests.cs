using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridLoopBackRouteTests
{
    private const string RoutingLoopSource = """
        flowchart TD
            A["Incoming message"] --> B["Determine next destination"] --> C["Send"] --> D{"More routing?"}
            D -- Yes --> B
            D -- No --> E["Complete"]
        """;

    [Test]
    public void TryLayout_RoutingLoop_KeepsMainChainInOneColumn()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds A = Find(Layout, "A");
        FlowchartLayoutNodeBounds B = Find(Layout, "B");
        FlowchartLayoutNodeBounds C = Find(Layout, "C");
        FlowchartLayoutNodeBounds D = Find(Layout, "D");
        FlowchartLayoutNodeBounds E = Find(Layout, "E");

        Assert.That(B.CenterX, Is.EqualTo(A.CenterX).Within(1.0));
        Assert.That(C.CenterX, Is.EqualTo(A.CenterX).Within(1.0));
        Assert.That(D.CenterX, Is.EqualTo(A.CenterX).Within(1.0));
        Assert.That(E.CenterX, Is.EqualTo(A.CenterX).Within(1.0));
    }

    [Test]
    public void TryLayout_RoutingLoop_ForwardChainUsesStraightVerticalRoutes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        foreach ((string SourceId, string TargetId) in new[] { ("A", "B"), ("B", "C"), ("C", "D") })
        {
            FlowchartLayoutEdgeRoute Route = FindRoute(Layout, SourceId, TargetId);
            Assert.That(Route.Points, Has.Count.EqualTo(2));
            Assert.That(Route.Points[0].X, Is.EqualTo(Route.Points[1].X).Within(1.0));
        }
    }

    [Test]
    public void TryLayout_RoutingLoop_NoBranchIsStraightFromBottomVertex()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "D", "E");
        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        double BottomVertexY = Diamond.CenterY - (Diamond.Height / 2.0);

        Assert.That(NoRoute.Points, Has.Count.EqualTo(2));
        Assert.That(NoRoute.Points[0].X, Is.EqualTo(Diamond.CenterX).Within(2.0));
        Assert.That(NoRoute.Points[0].Y, Is.EqualTo(BottomVertexY).Within(2.0));
        Assert.That(NoRoute.Points[0].X, Is.EqualTo(NoRoute.Points[1].X).Within(1.0));
    }

    [Test]
    public void TryLayout_RoutingLoop_YesBranchLeavesRightVertexAndEntersTargetRightSide()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute YesRoute = FindRoute(Layout, "D", "B");
        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        FlowchartLayoutNodeBounds Target = Find(Layout, "B");
        double RightVertexX = Diamond.CenterX + (Diamond.Width / 2.0);
        double TargetRightX = Target.CenterX + (Target.Width / 2.0);

        Assert.That(YesRoute.ExitSide, Is.EqualTo("right").IgnoreCase);
        Assert.That(YesRoute.EntrySide, Is.EqualTo("right").IgnoreCase);
        Assert.That(YesRoute.Points[0].X, Is.EqualTo(RightVertexX).Within(2.0));
        Assert.That(YesRoute.Points[0].Y, Is.EqualTo(Diamond.CenterY).Within(2.0));
        Assert.That(YesRoute.Points[^1].X, Is.EqualTo(TargetRightX).Within(2.0));
        Assert.That(YesRoute.Points[^1].Y, Is.EqualTo(Target.CenterY).Within(2.0));
        AssertRouteIsOrthogonal(YesRoute.Points);
        Assert.That(CountCorners(YesRoute.Points), Is.EqualTo(2));
    }

    [Test]
    public void RenderToSvg_RoutingLoop_YesBranchUsesRightSideCorridor()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("data-edge-source=\"D\" data-edge-target=\"B\""));
        Assert.That(Svg, Does.Contain("data-edge-source=\"D\" data-edge-target=\"E\""));
        Assert.That(Svg, Does.Not.Contain("data-edge-source=\"D\" data-edge-target=\"B\" fill=\"none\" stroke=\"#333333\" stroke-width=\"1\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/><path d=\"M"),
            "Yes loop should not leave from the diamond top vertex");
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
