using System.Xml.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Guards scored straight and edge-normal route candidates without relaxing the routing checker.</summary>
[TestFixture]
public sealed class DiamondStraightCandidateTests
{
    /// <summary>Exercises F17's full layout and painting path, including all four mirrored flow directions.</summary>
    /// <param name="Direction">The flow direction; LR is the unchanged F17 source.</param>
    [TestCase("LR")]
    [TestCase("RL")]
    [TestCase("TB")]
    [TestCase("BT")]
    public void F17_AlignedShapeChain_UsesStraightLinesAtBothDiamondTips(string Direction)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nR[Rect] --> RN(Rounded) --> D{{Diamond}} --> C((Circle))");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds Diamond = Layout.NodeBounds.Single(Node => Node.NodeId == "D");
        FlowchartLayoutEdgeRoute Incoming = Layout.EdgeRoutes.Single(Route => Route.TargetId == "D");
        FlowchartLayoutEdgeRoute Outgoing = Layout.EdgeRoutes.Single(Route => Route.SourceId == "D");
        bool Horizontal = Direction is "LR" or "RL";
        int Sign = Direction is "LR" or "BT" ? 1 : -1;
        Assert.Multiple(() =>
        {
            Assert.That(Incoming.Points, Has.Count.EqualTo(2));
            Assert.That(Outgoing.Points, Has.Count.EqualTo(2));
            Assert.That(Incoming.Points[^1].X, Is.EqualTo(Diamond.CenterX - (Horizontal ? Sign * Diamond.Width / 2 : 0)).Within(0.001));
            Assert.That(Incoming.Points[^1].Y, Is.EqualTo(Diamond.CenterY - (Horizontal ? 0 : Sign * Diamond.Height / 2)).Within(0.001));
            Assert.That(Outgoing.Points[0].X, Is.EqualTo(Diamond.CenterX + (Horizontal ? Sign * Diamond.Width / 2 : 0)).Within(0.001));
            Assert.That(Outgoing.Points[0].Y, Is.EqualTo(Diamond.CenterY + (Horizontal ? 0 : Sign * Diamond.Height / 2)).Within(0.001));
            Assert.That(Incoming.Points.Select(Point => Horizontal ? Point.Y : Point.X), Is.All.EqualTo(Horizontal ? Diamond.CenterY : Diamond.CenterX).Within(0.001));
            Assert.That(Outgoing.Points.Select(Point => Horizontal ? Point.Y : Point.X), Is.All.EqualTo(Horizontal ? Diamond.CenterY : Diamond.CenterX).Within(0.001));
        });
        XDocument Svg = XDocument.Parse(FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract));
        XElement[] DiamondLines = [.. Svg.Descendants().Where(Element =>
            (string?)Element.Attribute("data-edge-target") == "D" || (string?)Element.Attribute("data-edge-source") == "D")];
        Assert.That(DiamondLines, Has.Length.EqualTo(2));
        Assert.That(DiamondLines.Select(Element => Element.Name.LocalName), Is.All.EqualTo("line"),
            "Painting must use the selected straight routes, not stale curved guides.");
    }

    /// <summary>Checks F30's bend counts, square diamond terminals, and correspondence between drawn lines and source links.</summary>
    [Test]
    public void F30_ThreeWayDecision_UsesMinimumBendsAndOnlySourceLinks()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        Assert.That(Layout.EdgeRoutes.Select(Route => Route.EdgeId), Is.EquivalentTo(Contract.Edges.Select(Edge => Edge.Id)));
        Assert.That(Layout.EdgeRoutes.All(Route => Route.SourceId != Route.TargetId), Is.True);
        FlowchartLayoutNodeBounds Diamond = Layout.NodeBounds.Single(Node => Node.NodeId == "C");
        foreach (FlowchartLayoutEdgeRoute? Route in Layout.EdgeRoutes.Where(Route => Route.SourceId == "C"))
        {
            // The centre branch's allocated lower-right port requires a return
            // track: its two-bend alternative reverses the departure ray and
            // is rejected by the unchanged endpoint-hook gate.
            Assert.That(CountBends(Route.Points), Route.TargetId == "E"
                ? Is.EqualTo(3) : Is.LessThanOrEqualTo(2), Route.EdgeId);
            FlowchartLayoutPoint Port = Route.Points[0]; FlowchartLayoutPoint Next = Route.Points[1];
            Assert.That(FlowchartDiamondAttachHelper.TryResolveOutwardNormal(Diamond, Port.X, Port.Y, out double Nx, out double Ny), Is.True);
            double Dx = Next.X - Port.X, Dy = Next.Y - Port.Y;
            Assert.That(Math.Abs((Dx * Ny) - (Dy * Nx)) / Math.Sqrt((Dx * Dx) + (Dy * Dy)), Is.LessThan(0.005), Route.EdgeId);
            Assert.That((Dx * Nx) + (Dy * Ny), Is.GreaterThan(0), Route.EdgeId);
            for (int Index = 2; Index < Route.Points.Count; Index++)
            {
                Assert.That(Math.Abs(Route.Points[Index].X - Route.Points[Index - 1].X) < 0.001
                    || Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y) < 0.001, Is.True, Route.EdgeId);
            }
        }
        FlowchartLayoutEdgeRoute Incoming = Layout.EdgeRoutes.Single(Route => Route.TargetId == "C");
        Assert.That(CountBends(Incoming.Points), Is.Zero, "The aligned incoming link must not curl around the diamond.");
        XDocument Svg = XDocument.Parse(FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract));
        XElement[] Lines = [.. Svg.Descendants().Where(Element => Element.Attribute("data-edge-source") != null)];
        (string?, string?)[] DrawnLinks = [.. Lines.Select(Element => ((string?)Element.Attribute("data-edge-source"),
            (string?)Element.Attribute("data-edge-target"))).Distinct()];
        Assert.That(DrawnLinks, Is.EquivalentTo(Contract.Edges.Select(Edge => ((string?)Edge.SourceId, (string?)Edge.TargetId))));
        XElement[] IncomingDrawing = [.. Lines.Where(Element => (string?)Element.Attribute("data-edge-source") == "B"
            && (string?)Element.Attribute("data-edge-target") == "C")];
        Assert.That(IncomingDrawing, Has.Length.EqualTo(1));
        Assert.That(IncomingDrawing[0].Name.LocalName, Is.EqualTo("line"), "The old hooked guide must not survive in SVG.");
    }

    /// <summary>Checks that the existing weighted cost prefers a legal two-bend diamond route to three bends.</summary>
    [Test]
    public void DiamondNormalBends_CostScorePrefersTheTwoBendCandidate()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nS{Choose} --> T[Target]");
        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new() { NodeId = "S", CenterX = 0, CenterY = 0, Width = 200, Height = 200 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "T", CenterX = 300, CenterY = -350, Width = 100, Height = 100 });
        FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute
        {
            EdgeId = Contract.Edges.Single().Id!, SourceId = "S", TargetId = "T",
            ExitSide = "right", EntrySide = "top", ExitNormalX = 1, ExitNormalY = -1, EntryNormalY = 1
        };
        foreach ((double, double) Point in new[] { (50.0, -50.0), (100.0, -100.0), (100.0, -200.0), (300.0, -200.0), (300.0, -300.0) })
        {
            Route.Points.Add(new() { X = Point.Item1, Y = Point.Item2 });
        }

        Layout.EdgeRoutesMutable.Add(Route);
        FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
        Assert.That(FlowchartLayoutScore.CountCorners(Route.Points), Is.EqualTo(3));
        Route.Points.Clear();
        foreach ((double, double) Point in new[] { (50.0, -50.0), (200.0, -200.0), (300.0, -200.0), (300.0, -300.0) })
        {
            Route.Points.Add(new() { X = Point.Item1, Y = Point.Item2 });
        }

        FlowchartLayoutScore.Result Full = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
        Assert.Multiple(() =>
        {
            Assert.That(Full.Bends, Is.EqualTo(2));
            Assert.That(Full.OptimizationScore, Is.LessThanOrEqualTo(Before.OptimizationScore - FlowchartLayoutRules.BendScore));
        });
    }

    /// <summary>Counts changes of direction, including the allowed diagonal diamond terminal.</summary>
    /// <param name="Points">The complete model polyline.</param>
    /// <returns>The number of genuine bends, ignoring collinear intermediate points.</returns>
    private static int CountBends(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Bends = 0;
        for (int Index = 1; Index + 1 < Points.Count; Index++)
        {
            FlowchartLayoutPoint A = Points[Index - 1]; FlowchartLayoutPoint B = Points[Index]; FlowchartLayoutPoint C = Points[Index + 1];
            if (Math.Abs(((B.X - A.X) * (C.Y - B.Y)) - ((B.Y - A.Y) * (C.X - B.X))) > 0.001)
            {
                Bends++;
            }
        }
        return Bends;
    }
}
