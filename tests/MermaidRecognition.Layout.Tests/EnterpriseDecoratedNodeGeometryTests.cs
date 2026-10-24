using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Core;
using MermaidRecognition.Contract;
using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks the connection body against the unchanged enterprise decorative outlines.</summary>
[TestFixture]
public sealed class EnterpriseDecoratedNodeGeometryTests
{
    /// <summary>A busy decorated node allocates eligible edges without leaving stale visual-box ports.</summary>
    /// <param name="Direction">Main flow direction.</param>
    /// <param name="Kind">Renderer-owned decorated shape.</param>
    [TestCase("TB", EnterpriseBoxKind.Contract)]
    [TestCase("BT", EnterpriseBoxKind.Contract)]
    [TestCase("LR", EnterpriseBoxKind.Contract)]
    [TestCase("RL", EnterpriseBoxKind.Contract)]
    [TestCase("TB", EnterpriseBoxKind.Representation)]
    [TestCase("BT", EnterpriseBoxKind.Representation)]
    [TestCase("LR", EnterpriseBoxKind.Representation)]
    [TestCase("RL", EnterpriseBoxKind.Representation)]
    public void BusyDecoratedNodeUsesEligibleConnectionSegments(string Direction, EnterpriseBoxKind Kind)
    {
        var Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nA --> B\nB --> C\nB --> D\nB --> E");
        Contract.Nodes.Single(Node => Node.Id == "B").GeometryProvider = new EnterpriseDecoratedNodeGeometry(Kind);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(4));
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1"), Is.Empty);
    }

    /// <summary>An approach through the contract's top decoration loses to legality and is reported.</summary>
    [Test]
    public void IneligibleDecorativeEdgeCannotBeForcedByAHint()
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nA[Before] --> B[Body]");
        Contract.Nodes.Single(Node => Node.Id == "B").GeometryProvider = new EnterpriseDecoratedNodeGeometry(EnterpriseBoxKind.Contract);
        var Hint = new RoutingHintContract { Enter = LayoutHintDirection.North };
        Contract.Edges[0].RoutingHint = Hint;
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(Hint.IsActive, Is.False);
            Assert.That(Hint.UnhonouredReason, Is.Not.Null.And.Not.Empty);
            Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(1));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
                .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1"), Is.Empty);
        });
    }

    /// <summary>The painted enterprise guide retains the representation midline port through the SVG conversion.</summary>
    [Test]
    public void EnterpriseOutputKeepsTheHorizontalWaveMidline()
    {
        var Document = EnterpriseValidator.Analyze("""
            enterprise BT
              application-component caller "Caller"
              representation body "Representation"
              caller -flow-> body [enter S]
            """).Document!;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Route = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes).Single();
        var Body = Layout.Nodes["body"];
        Assert.Multiple(() =>
        {
            Assert.That(Route.SvgPoints[^1].Y, Is.EqualTo(Body.AbsoluteY + Body.Height
                - EnterpriseDecoratedNodeGeometry.WAVE_INSET).Within(0.001));
            Assert.That(Route.SvgPoints[^1].X, Is.EqualTo(Body.AbsoluteX + Body.Width / 2).Within(0.001));
            Assert.That(Route.SvgPoints[^2].X, Is.EqualTo(Route.SvgPoints[^1].X).Within(0.001));
            Assert.That(Route.SvgPoints[^2].Y, Is.GreaterThan(Route.SvgPoints[^1].Y));
        });
    }

    /// <summary>Every painted arrowhead edge stays outside the wave while its tip touches the endpoint line.</summary>
    /// <param name="Marker">The renderer's arrowhead key.</param>
    [TestCase("arrowFilled")]
    [TestCase("triangleHollow")]
    [TestCase("arrowOpen")]
    [TestCase("arrowOpenSmall")]
    [TestCase("halfArrow")]
    public void RepresentationArrowheadTouchesButDoesNotCrossTheWave(string Marker)
    {
        var Geometry = new EnterpriseDecoratedNodeGeometry(EnterpriseBoxKind.Representation).Create(150, 90);
        Assert.That(Geometry.ConnectionBoundary.TryPort("bottom", 0.5, out var Port), Is.True);
        string Svg = EnterpriseNotationSvg.EndSvg(Marker, Port.X, Port.Y, 0, -1).Svg;
        var Points = Regex.Matches(Svg, @"[ML](-?[\d.]+),(-?[\d.]+)")
            .Select(Match => (X: double.Parse(Match.Groups[1].Value, CultureInfo.InvariantCulture),
                Y: double.Parse(Match.Groups[2].Value, CultureInfo.InvariantCulture))).ToArray();
        Assert.That(Points.Any(Point => Point == (Port.X, Port.Y)), Is.True);
        for (int Index = 1; Index < Points.Length; Index++)
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(Points[Index - 1].X,
                Points[Index - 1].Y, Points[Index].X, Points[Index].Y), Is.False);
        if (Svg.Contains(" Z", StringComparison.Ordinal))
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(Points[^1].X,
                Points[^1].Y, Points[0].X, Points[0].Y), Is.False);
    }

    /// <summary>Legal explicit sides still use the supplied body ports and retain their vertical or horizontal terminals.</summary>
    /// <param name="Kind">Decorated renderer geometry.</param>
    [TestCase(EnterpriseBoxKind.Contract)]
    [TestCase(EnterpriseBoxKind.Representation)]
    public void LegalHintsRetainTheConnectionBody(EnterpriseBoxKind Kind)
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA[Before] --> B[Body]\nB --> C[After]");
        Contract.Nodes.Single(Node => Node.Id == "B").GeometryProvider = new EnterpriseDecoratedNodeGeometry(Kind);
        Contract.Edges[0].RoutingHint = new RoutingHintContract { Enter = LayoutHintDirection.West };
        Contract.Edges[1].RoutingHint = new RoutingHintContract { Leave = LayoutHintDirection.South };
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        Assert.That(Layout.EdgeRoutes.Single(Route => Route.TargetId == "B").EntrySide, Is.EqualTo("left"));
        Assert.That(Layout.EdgeRoutes.Single(Route => Route.SourceId == "B").ExitSide, Is.EqualTo("bottom"));
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1"), Is.Empty);
    }

    /// <summary>Shared allocation and routing consume renderer geometry in every flow direction.</summary>
    /// <param name="Direction">Main flow direction.</param>
    /// <param name="Kind">The decorated renderer geometry.</param>
    [TestCase("TB", EnterpriseBoxKind.Contract)]
    [TestCase("BT", EnterpriseBoxKind.Contract)]
    [TestCase("LR", EnterpriseBoxKind.Contract)]
    [TestCase("RL", EnterpriseBoxKind.Contract)]
    [TestCase("TB", EnterpriseBoxKind.Representation)]
    [TestCase("BT", EnterpriseBoxKind.Representation)]
    [TestCase("LR", EnterpriseBoxKind.Representation)]
    [TestCase("RL", EnterpriseBoxKind.Representation)]
    public void SharedRouterUsesConnectionAndObstacleGeometry(string Direction, EnterpriseBoxKind Kind)
    {
        var Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nA[Before] --> B[Body]\nB --> C[After]");
        Contract.Nodes.Single(Node => Node.Id == "B").GeometryProvider = new EnterpriseDecoratedNodeGeometry(Kind);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(2));
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1"), Is.Empty);
    }

    /// <summary>The representation bottom is the horizontal endpoint line, with a vertical normal.</summary>
    [Test]
    public void RepresentationBottomUsesTheEndpointLineAndVerticalNormal()
    {
        var Geometry = new EnterpriseDecoratedNodeGeometry(EnterpriseBoxKind.Representation).Create(150, 90);
        Assert.That(Geometry.ConnectionBoundary.TryPort("bottom", 0.5, out var Port), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(Port.X, Is.Zero);
            Assert.That(Port.Y, Is.EqualTo(45 - EnterpriseDecoratedNodeGeometry.WAVE_INSET));
            Assert.That(Port.NormalX, Is.Zero);
            Assert.That(Port.NormalY, Is.EqualTo(1));
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(Port.X, Port.Y + 30, Port.X, Port.Y), Is.False);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(Port.X, Port.Y + 30, Port.X, Port.Y - 1), Is.True);
        });
    }

    /// <summary>The protruding half-wave is occupied even though it is below the connection midline.</summary>
    [Test]
    public void RepresentationRejectsAnApproachThatCrossesTheWave()
    {
        var Geometry = new EnterpriseDecoratedNodeGeometry(EnterpriseBoxKind.Representation).Create(150, 90);
        Assert.Multiple(() =>
        {
            Assert.That(Geometry.ConnectionBoundary.TryPort("bottom", 0.75, out _), Is.False);
            Assert.That(Geometry.ConnectionBoundary.Contains(37.5, 37, 0), Is.False);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(37.5, 80, 37.5, 37), Is.True);
            Assert.That(Geometry.ConnectionBoundary.TryPort("bottom", 0.25, out var Port), Is.True);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(Port.X, 80, Port.X, Port.Y), Is.False);
        });
    }

    /// <summary>The contract overhang is an obstacle, while only the inset main body receives lines.</summary>
    [Test]
    public void ContractDecorationIsNotAConnectionEdge()
    {
        var Geometry = new EnterpriseDecoratedNodeGeometry(EnterpriseBoxKind.Contract).Create(150, 90);
        Assert.Multiple(() =>
        {
            Assert.That(Geometry.ConnectionBoundary.TryPort("right", 0.5, out var Port), Is.True);
            Assert.That(Port.X, Is.EqualTo(69));
            Assert.That(Geometry.ConnectionBoundary.Contains(75, 0, 0), Is.False);
            Assert.That(Geometry.ConnectionBoundary.TryPort("top", 0.5, out _), Is.False);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(0, -80, 0, -40), Is.True);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(69, 0, 100, 0), Is.False);
            Assert.That(Geometry.ObstacleGeometry.IntersectsSegmentInterior(-100, -42, 100, -42), Is.True);
        });
    }
}
