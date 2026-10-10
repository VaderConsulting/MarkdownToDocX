using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridF11StraightChordTests
{
    private const string F11Source = """
        flowchart LR
            A[Rectangle] --> B(Rounded rectangle) --> C{Diamond decision} --> D((Circle))
        """;

    [Test]
    public void TryLayout_F11_AllNodesShareRankCentreLine()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F11Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double CentreY = Layout.NodeBounds[0].CenterY;
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            Assert.That(Bounds.CenterY, Is.EqualTo(CentreY).Within(1.0), Bounds.NodeId);
        }
    }

    [Test]
    public void TryLayout_F11_DiamondToCircle_IsSingleStraightSegment()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F11Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "C", "D");
        Assert.That(Route.Points, Has.Count.EqualTo(2));
        Assert.That(Route.Points[0].Y, Is.EqualTo(Route.Points[1].Y).Within(1.0));
        Assert.That(Route.Points[0].X, Is.LessThan(Route.Points[1].X));
        Assert.That(Route.ExitSide, Is.EqualTo("right"));
        Assert.That(Route.EntrySide, Is.EqualTo("left"));
    }

    [Test]
    public void TryLayout_F11_DiamondToCircle_AttachesOnVisibleBoundaries()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F11Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Diamond = Find(Layout, "C");
        FlowchartLayoutNodeBounds Circle = Find(Layout, "D");
        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "C", "D");

        double RightVertexX = Diamond.CenterX + (Diamond.Width / 2.0);
        double CircleRadius = Math.Min(Circle.Width, Circle.Height) / 2.0;
        double CircleLeftX = Circle.CenterX - CircleRadius;

        Assert.That(Route.Points[0].X, Is.EqualTo(RightVertexX).Within(2.0));
        Assert.That(Route.Points[0].Y, Is.EqualTo(Diamond.CenterY).Within(2.0));
        Assert.That(Route.Points[1].X, Is.EqualTo(CircleLeftX).Within(2.0));
        Assert.That(Route.Points[1].Y, Is.EqualTo(Circle.CenterY).Within(2.0));
    }

    [Test]
    public void RenderToSvg_F11_DiamondToCircle_UsesStraightLine()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F11Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("data-edge-source=\"C\" data-edge-target=\"D\""));
        Match LineMatch = Regex.Match(
            Svg,
            @"<line[^>]+data-edge-source=""C"" data-edge-target=""D""[^>]*/>");
        Assert.That(LineMatch.Success, Is.True);
        Assert.That(LineMatch.Value, Does.Match(@"y1=""\d+""[^>]+y2=""\d+"""));
        Match YMatch = Regex.Match(LineMatch.Value, @"y1=""(\d+)""[^>]+y2=""(\d+)""");
        Assert.That(YMatch.Groups[1].Value, Is.EqualTo(YMatch.Groups[2].Value));
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
