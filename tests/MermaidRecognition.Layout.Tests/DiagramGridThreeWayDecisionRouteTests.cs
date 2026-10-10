using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridThreeWayDecisionRouteTests
{
    private const string TypeDecisionSource = """
        flowchart TD
            M[Message] --> D{Type?}
            D -->|Create| H1[Create handler]
            D -->|Update| H2[Update handler]
            D -->|else| U[Unknown handler]
        """;

    [Test]
    public void TryLayout_TypeDecision_CentreChildStaysInDiamondColumn()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(TypeDecisionSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        FlowchartLayoutNodeBounds CentreChild = Find(Layout, "H2");
        FlowchartLayoutNodeBounds LeftChild = Find(Layout, "H1");
        FlowchartLayoutNodeBounds RightChild = Find(Layout, "U");

        Assert.That(CentreChild.CenterX, Is.EqualTo(Diamond.CenterX).Within(2.0));
        Assert.That(LeftChild.CenterX, Is.LessThan(CentreChild.CenterX - 50.0));
        Assert.That(RightChild.CenterX, Is.GreaterThan(CentreChild.CenterX + 50.0));
    }

    [Test]
    public void TryLayout_TypeDecision_UsesBottomAndSideVertices()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(TypeDecisionSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        double LeftVertexX = Diamond.CenterX - (Diamond.Width / 2.0);
        double RightVertexX = Diamond.CenterX + (Diamond.Width / 2.0);
        double BottomVertexY = Diamond.CenterY - (Diamond.Height / 2.0);

        FlowchartLayoutEdgeRoute CreateRoute = FindRoute(Layout, "D", "H1");
        FlowchartLayoutEdgeRoute UpdateRoute = FindRoute(Layout, "D", "H2");
        FlowchartLayoutEdgeRoute UnknownRoute = FindRoute(Layout, "D", "U");

        Assert.That(CreateRoute.Points[0].X, Is.EqualTo(LeftVertexX).Within(2.0));
        Assert.That(CreateRoute.Points[0].Y, Is.EqualTo(Diamond.CenterY).Within(2.0));
        Assert.That(UpdateRoute.Points[0].X, Is.EqualTo(Diamond.CenterX).Within(2.0));
        Assert.That(UpdateRoute.Points[0].Y, Is.EqualTo(BottomVertexY).Within(2.0));
        Assert.That(UnknownRoute.Points[0].X, Is.EqualTo(RightVertexX).Within(2.0));
        Assert.That(UnknownRoute.Points[0].Y, Is.EqualTo(Diamond.CenterY).Within(2.0));
    }

    [Test]
    public void TryLayout_TypeDecision_BranchesUseIndependentOrthogonalPaths()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(TypeDecisionSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute UpdateRoute = FindRoute(Layout, "D", "H2");
        Assert.That(UpdateRoute.Points, Has.Count.EqualTo(2));
        Assert.That(UpdateRoute.Points[0].X, Is.EqualTo(UpdateRoute.Points[1].X).Within(1.0));

        foreach (string TargetId in new[] { "H1", "U" })
        {
            FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "D", TargetId);
            AssertRouteIsOrthogonal(Route.Points);
            Assert.That(CountCorners(Route.Points), Is.EqualTo(1));
        }
    }

    [Test]
    public void RenderToSvg_TypeDecision_NoSharedBottomStub()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(TypeDecisionSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("data-edge-source=\"D\" data-edge-target=\"H2\""));
        Assert.That(Svg, Does.Match(@"x1=""308"" y1=""\d+"" x2=""308"""), "Update uses a straight vertical spine");
        Assert.That(Svg, Does.Contain(@"path d=""M218,248L98,248"""), "Create leaves the left vertex horizontally");
        Assert.That(Svg, Does.Contain(@"path d=""M398,248L518,248"""), "Unknown leaves the right vertex horizontally");
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
