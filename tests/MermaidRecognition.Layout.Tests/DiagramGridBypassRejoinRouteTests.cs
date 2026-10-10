using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridBypassRejoinRouteTests
{
    private const string DetourFlagSource = """
        flowchart TD
            M[Message] --> D{Detour flag?}
            D -->|on| X[Inspection / shadow]
            X --> R[Rejoin main path]
            D -->|off| R
        """;

    [Test]
    public void TryLayout_DetourFlag_RejoinStaysOnSpineColumn()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Message = Find(Layout, "M");
        FlowchartLayoutNodeBounds Decision = Find(Layout, "D");
        FlowchartLayoutNodeBounds Inspection = Find(Layout, "X");
        FlowchartLayoutNodeBounds Rejoin = Find(Layout, "R");

        Assert.That(Rejoin.CenterX, Is.EqualTo(Decision.CenterX).Within(2.0));
        Assert.That(Rejoin.CenterX, Is.EqualTo(Message.CenterX).Within(2.0));
        Assert.That(Rejoin.CenterY, Is.LessThan(Decision.CenterY - 20.0));
        Assert.That(Math.Abs(Inspection.CenterX - Decision.CenterX), Is.GreaterThan(50.0));
        Assert.That(Inspection.CenterY, Is.LessThan(Decision.CenterY - 20.0));
        Assert.That(Inspection.CenterY, Is.GreaterThan(Rejoin.CenterY + 20.0));
    }

    [Test]
    public void TryLayout_DetourFlag_InspectionToRejoinIsOrthogonal()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "X", "R");
        Assert.That(Route.Points, Has.Count.GreaterThanOrEqualTo(2));
        AssertRouteIsOrthogonal(Route.Points);
    }

    [Test]
    public void TryLayout_DetourFlag_DecisionBranchesUseCorrectVertices()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        double HalfWidth = Diamond.Width / 2.0;
        double HalfHeight = Diamond.Height / 2.0;
        double BottomY = Diamond.CenterY - HalfHeight;

        FlowchartLayoutEdgeRoute OnRoute = FindRoute(Layout, "D", "X");
        FlowchartLayoutEdgeRoute OffRoute = FindRoute(Layout, "D", "R");

        Assert.That(OnRoute.Points[0].X, Is.LessThan(Diamond.CenterX - 20.0));
        AssertRouteIsOrthogonal(OnRoute.Points);

        Assert.That(OffRoute.Points[0].X, Is.EqualTo(Diamond.CenterX).Within(2.0));
        Assert.That(OffRoute.Points[0].Y, Is.EqualTo(BottomY).Within(2.0));
        Assert.That(OffRoute.Points, Has.Count.EqualTo(2));
        Assert.That(OffRoute.Points[0].X, Is.EqualTo(OffRoute.Points[1].X).Within(1.0));
    }

    [Test]
    public void RenderToSvg_DetourFlag_ProducesBypassRejoinLayout()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("data-edge-source=\"D\" data-edge-target=\"X\""));
        Assert.That(Svg, Does.Contain("data-edge-source=\"D\" data-edge-target=\"R\""));
        Assert.That(Svg, Does.Contain("data-edge-source=\"X\" data-edge-target=\"R\""));
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
