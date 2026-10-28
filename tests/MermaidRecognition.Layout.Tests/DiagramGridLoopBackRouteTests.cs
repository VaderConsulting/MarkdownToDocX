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
    public void TryLayout_RoutingLoop_ForwardChainUsesStraightWhereAllocatedPortsAlign()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        foreach ((string SourceId, string TargetId) in new[] { ("A", "B"), ("B", "C") })
        {
            FlowchartLayoutEdgeRoute Route = FindRoute(Layout, SourceId, TargetId);
            Assert.That(Route.Points, Has.Count.EqualTo(2));
            Assert.That(Route.Points[0].X, Is.EqualTo(Route.Points[1].X).Within(1.0));
        }

        FlowchartLayoutEdgeRoute ToDiamond = FindRoute(Layout, "C", "D");
        AssertDiamondPort(ToDiamond, Find(Layout, "D"), IsSource: false);
        Assert.That(ToDiamond.Points.Count, Is.GreaterThan(2));
    }

    [Test]
    public void TryLayout_RoutingLoop_NoBranchUsesSymmetricDiamondEdgePort()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "D", "E");
        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        AssertDiamondPort(NoRoute, Diamond, IsSource: true);
        Assert.That(NoRoute.Points.Count, Is.GreaterThan(2));
    }

    [Test]
    public void TryLayout_RoutingLoop_YesBranchUsesDiamondEdgeNormalAndEntersTargetRightSide()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(RoutingLoopSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute YesRoute = FindRoute(Layout, "D", "B");
        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        FlowchartLayoutNodeBounds Target = Find(Layout, "B");
        double TargetRightX = Target.CenterX + (Target.Width / 2.0);

        Assert.That(YesRoute.EntrySide, Is.EqualTo("right").IgnoreCase);
        AssertDiamondPort(YesRoute, Diamond, IsSource: true);
        Assert.That(YesRoute.Points[^1].X, Is.EqualTo(TargetRightX).Within(2.0));
        Assert.That(YesRoute.Points[^1].Y, Is.EqualTo(Target.CenterY).Within(2.0));
        AssertRouteIsOrthogonalAfterDiamondNormal(YesRoute.Points);
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
            double PreviousX = Points[Index - 1].X - Points[Index - 2].X;
            double PreviousY = Points[Index - 1].Y - Points[Index - 2].Y;
            double NextX = Points[Index].X - Points[Index - 1].X;
            double NextY = Points[Index].Y - Points[Index - 1].Y;
            if (Math.Abs((PreviousX * NextY) - (PreviousY * NextX)) > 0.5)
            {
                Corners++;
            }
        }

        return Corners;
    }

    private static void AssertRouteIsOrthogonalAfterDiamondNormal(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        for (int Index = 2; Index < Points.Count; Index++)
        {
            bool Horizontal = Math.Abs(Points[Index - 1].Y - Points[Index].Y) <= 0.5;
            bool Vertical = Math.Abs(Points[Index - 1].X - Points[Index].X) <= 0.5;
            Assert.That(Horizontal || Vertical, Is.True, $"segment {Index - 1}->{Index} is diagonal");
        }
    }

    private static void AssertDiamondPort(FlowchartLayoutEdgeRoute Route, FlowchartLayoutNodeBounds Diamond, bool IsSource)
    {
        FlowchartLayoutPoint Port = IsSource ? Route.Points[0] : Route.Points[^1];
        FlowchartLayoutPoint Adjacent = IsSource ? Route.Points[1] : Route.Points[^2];
        double HalfWidth = Diamond.Width / 2, HalfHeight = Diamond.Height / 2;
        double Outline = Math.Abs((Port.X - Diamond.CenterX) / HalfWidth)
            + Math.Abs((Port.Y - Diamond.CenterY) / HalfHeight);
        Assert.That(Outline, Is.EqualTo(1).Within(0.02));
        Assert.That(Math.Abs(Port.X - Diamond.CenterX), Is.LessThan(HalfWidth - 1), "diamond ports are on edges, not vertices");
        Assert.That(Math.Abs(Port.Y - Diamond.CenterY), Is.LessThan(HalfHeight - 1), "diamond ports are on edges, not vertices");
        double NormalX = IsSource ? Route.ExitNormalX : Route.EntryNormalX;
        double NormalY = IsSource ? Route.ExitNormalY : Route.EntryNormalY;
        double Vx = Adjacent.X - Port.X, Vy = Adjacent.Y - Port.Y;
        double Magnitude = Math.Sqrt((NormalX * NormalX) + (NormalY * NormalY));
        Assert.That(Magnitude, Is.GreaterThan(0));
        Assert.That(Math.Abs((Vx * NormalY) - (Vy * NormalX)) / Magnitude, Is.LessThan(1.5), "terminal follows edge normal");
        Assert.That((Vx * NormalX) + (Vy * NormalY), Is.GreaterThan(0), "terminal points outward");
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
