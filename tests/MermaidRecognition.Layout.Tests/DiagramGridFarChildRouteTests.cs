using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridFarChildRouteTests
{
    private const string LayeredViewSource = """
        flowchart TD
            M[Message] --> H[Headers / metadata]
            M --> P[Payload]
            H --> C[Correlation id]
            H --> F[Format / version]
            H --> T[Type: command / document / event]
        """;

    [Test]
    public void TryLayout_LayeredView_FarChildLeavesFromFacingSide()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(LayeredViewSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute FarChild = FindRoute(Layout, "H", "T");
        Assert.That(FarChild.ExitSide, Is.EqualTo("right"), "B11: exit from free side facing far child");
        Assert.That(FarChild.EntrySide, Is.EqualTo("top").Or.EqualTo("left"),
            "B11: enter top centre or near side at centre height");
        Assert.That(FarChild.Points, Has.Count.GreaterThanOrEqualTo(4));

        FlowchartLayoutNodeBounds Source = Find(Layout, "H");
        FlowchartLayoutNodeBounds Target = Find(Layout, "T");
        double RightEdge = Source.CenterX + (Source.Width / 2.0);
        double TopEdge = Target.CenterY + (Target.Height / 2.0);
        double LeftEdge = Target.CenterX - (Target.Width / 2.0);

        FlowchartLayoutPoint Start = FarChild.Points[0];
        Assert.That(Start.X, Is.EqualTo(RightEdge).Within(1.0), "start on right side centre, not corner");
        Assert.That(Start.Y, Is.EqualTo(Source.CenterY).Within(1.0));

        if (string.Equals(FarChild.EntrySide, "top", StringComparison.Ordinal))
        {
            FlowchartLayoutPoint End = FarChild.Points[FarChild.Points.Count - 1];
            Assert.That(End.X, Is.EqualTo(Target.CenterX).Within(1.0));
            Assert.That(End.Y, Is.EqualTo(TopEdge).Within(1.0));
            AssertRouteIsOrthogonal(FarChild.Points);
        }
        else
        {
            FlowchartLayoutPoint End = FarChild.Points[FarChild.Points.Count - 1];
            Assert.That(End.X, Is.EqualTo(LeftEdge).Within(1.0));
            Assert.That(End.Y, Is.EqualTo(Target.CenterY).Within(1.0));
            AssertRouteIsOrthogonal(FarChild.Points);
        }
    }

    [Test]
    public void ResolveGuidePoints_LayeredView_FarChildUsesGridRoute()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(LayeredViewSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = new(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        Dictionary<string, NodeContract> NodeById = new(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            NodeById[Node.Id] = Node;
        }

        EdgeContract FarEdge = Contract.Edges.First(Edge =>
            string.Equals(Edge.SourceId, "H", StringComparison.Ordinal)
            && string.Equals(Edge.TargetId, "T", StringComparison.Ordinal));
        Assert.That(Layout.TryCopyDrawnRoutePoints(FarEdge, out List<(double X, double Y)> Drawn), Is.True);
        Assert.That(Drawn, Has.Count.GreaterThanOrEqualTo(4));

        FlowchartResolvedGuide? Guide = FlowchartEdgeRouteResolver
            .ResolveAllVisibleGuidePoints(Contract, Layout, BoundsById, NodeById, FlowchartDirection.TopToBottom)
            .FirstOrDefault(Guide => string.Equals(Guide.Edge.SourceId, "H", StringComparison.Ordinal)
                && string.Equals(Guide.Edge.TargetId, "T", StringComparison.Ordinal));
        Assert.That(Guide, Is.Not.Null);
        Assert.That(Guide!.RouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Grid));
        Assert.That(Guide.Points, Has.Count.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void RenderToSvg_LayeredView_FarChildRouteIsOrthogonal()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(LayeredViewSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("data-edge-source=\"H\" data-edge-target=\"T\""));

        string Path = ExtractEdgePath(Svg, "H", "T");
        Assert.That(Path, Does.Contain('C'), "Grid bends must have rounded corners.");
        OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(Path);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutEdgeRoute Route = FindRoute(Layout, "H", "T");
        FlowchartLayoutNodeBounds Target = Find(Layout, "T");
        double[] Start = ReadCoordinates(Regex.Match(Path, @"^M([^LC]+)").Groups[1].Value);
        double OffsetX = Start[0] - Route.Points[0].X;
        double OffsetY = Start[1] + Route.Points[0].Y;
        Match Terminal = Regex.Match(Path, @"C([^LC]+)L([^LC]+)$");
        Assert.That(Terminal.Success, Is.True, "The last rounded corner must end in a straight run.");
        double[] CornerEnd = ReadCoordinates(Terminal.Groups[1].Value);
        double[] End = ReadCoordinates(Terminal.Groups[2].Value);
        Assert.That(End[0], Is.EqualTo(Target.CenterX + OffsetX).Within(0.5));
        Assert.That(End[1], Is.EqualTo(-(Target.CenterY + (Target.Height / 2)) + OffsetY).Within(0.5),
            "The connector must reach the actual child top, independent of diagram translation.");
        Assert.That(CornerEnd[^2], Is.EqualTo(End[0]).Within(0.5), "The terminal run must be vertical.");
        Assert.That(End[1] - CornerEnd[^1], Is.GreaterThanOrEqualTo(10),
            "The arrowhead needs a straight approach to the child top.");
    }

    private static double[] ReadCoordinates(string Text) => [.. Regex.Matches(Text, @"-?\d+(?:\.\d+)?").Select(Value => double.Parse(Value.Value, CultureInfo.InvariantCulture))];

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
