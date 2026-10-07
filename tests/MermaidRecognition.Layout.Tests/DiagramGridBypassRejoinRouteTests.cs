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
    public void TryLayout_DetourFlag_RejoinSharesInspectionRank()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Inspection = Find(Layout, "X");
        FlowchartLayoutNodeBounds Rejoin = Find(Layout, "R");

        Assert.That(Rejoin.CenterY, Is.EqualTo(Inspection.CenterY).Within(2.0));
        Assert.That(Rejoin.CenterX, Is.GreaterThan(Inspection.CenterX + 50.0));
        Assert.That(Inspection.CenterX, Is.LessThan(Find(Layout, "D").CenterX - 50.0));
    }

    [Test]
    public void TryLayout_DetourFlag_InspectionToRejoinIsStraightHorizontal()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "X", "R");
        Assert.That(Route.Points, Has.Count.EqualTo(2));
        Assert.That(Route.Points[0].Y, Is.EqualTo(Route.Points[1].Y).Within(1.0));
        Assert.That(Route.ExitSide, Is.EqualTo("right"));
        Assert.That(Route.EntrySide, Is.EqualTo("left"));
    }

    [Test]
    public void TryLayout_DetourFlag_DecisionBranchesUseSlantedEdgesAndOrthogonalPaths()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Diamond = Find(Layout, "D");
        double HalfWidth = Diamond.Width / 2.0;
        double HalfHeight = Diamond.Height / 2.0;
        double LowerLeftX = Diamond.CenterX - (HalfWidth / 2.0);
        double LowerRightX = Diamond.CenterX + (HalfWidth / 2.0);
        double LowerEdgeY = Diamond.CenterY - (HalfHeight / 2.0);

        FlowchartLayoutEdgeRoute OnRoute = FindRoute(Layout, "D", "X");
        FlowchartLayoutEdgeRoute OffRoute = FindRoute(Layout, "D", "R");

        Assert.That(OnRoute.Points[0].X, Is.EqualTo(LowerLeftX).Within(2.0));
        Assert.That(OnRoute.Points[0].Y, Is.EqualTo(LowerEdgeY).Within(2.0));
        Assert.That(OffRoute.Points[0].X, Is.EqualTo(LowerRightX).Within(2.0));
        Assert.That(OffRoute.Points[0].Y, Is.EqualTo(LowerEdgeY).Within(2.0));

        AssertRouteIsOrthogonal(OnRoute.Points);
        AssertRouteIsOrthogonal(OffRoute.Points);
        Assert.That(OffRoute.Points[0].X, Is.LessThan(OffRoute.Points[1].X - 40.0),
            "Off branch runs outward before dropping");
    }

    [Test]
    public void RenderToSvg_DetourFlag_ProducesBypassRejoinLayout()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(DetourFlagSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("data-edge-source=\"D\" data-edge-target=\"X\""));
        Assert.That(Svg, Does.Contain("data-edge-source=\"D\" data-edge-target=\"R\""));
        Assert.That(Svg, Does.Contain("data-edge-source=\"X\" data-edge-target=\"R\""));
        Assert.That(Svg, Does.Contain(@"path d=""M406,368L456,368"""),
            "Off branch runs outward before dropping");
        Assert.That(Svg, Does.Contain(@"data-edge-source=""X"" data-edge-target=""R"""));
        Assert.That(Svg, Does.Contain(@"x1=""218"" y1=""593"" x2=""239"" y2=""593"""),
            "Inspection to Rejoin runs straight across");
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
