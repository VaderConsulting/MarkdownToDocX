using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Catalogue fixture render gates (F11–F24 shape/edge parity).
/// </summary>
[TestFixture]
public sealed class FlowchartCatalogueRenderTests
{
    [Test]
    public void TryResolveLayoutGuidePoints_F113_ParallelBranchFanIn_UsesExteriorCorridor()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            Start[Start] --> B1 --> End
            Start --> B2 --> End
            Start --> B3 --> End
            Start --> B4 --> End
            Start --> B5 --> End
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        EdgeContract B1ToEnd = Contract.Edges.First(Edge => Edge.SourceId == "B1" && Edge.TargetId == "End");
        FlowchartLayoutNodeBounds B1Bounds = BoundsById["B1"];
        FlowchartLayoutNodeBounds B2Bounds = BoundsById["B2"];

        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                B1ToEnd,
                Contract,
                BoundsById,
                NodeById,
                FlowchartDirection.LeftToRight,
                out List<(double X, double Y)> GuidePoints,
                out FlowchartEdgeRouteKind RouteKind),
            Is.True);

        double B1RightEdge = B1Bounds.CenterX + (B1Bounds.Width / 2.0);
        double CorridorX = GuidePoints.Max(Point => Point.X);
        Assert.That(CorridorX, Is.GreaterThan(B1RightEdge + 8.0));
        Assert.That(GuidePoints[0].Y, Is.EqualTo(B1Bounds.CenterY).Within(0.5));

        foreach ((double PointX, double PointY) in GuidePoints)
        {
            bool InsideB2 =
                PointX > B2Bounds.CenterX - (B2Bounds.Width / 2.0)
                && PointX < B2Bounds.CenterX + (B2Bounds.Width / 2.0)
                && PointY > B2Bounds.CenterY - (B2Bounds.Height / 2.0)
                && PointY < B2Bounds.CenterY + (B2Bounds.Height / 2.0);
            Assert.That(InsideB2, Is.False, $"Guide point ({PointX}, {PointY}) must stay outside node B2.");
        }
    }

    [Test]
    public void RenderToSvg_F81_TrapezoidBracketShapes_UseCorrectGeometry()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Top[/Wide top/] --> Bottom[\Wide bottom/]
            """);

        Assert.That(Contract.Nodes.Single(Node => Node.Id == "Top").Shape, Is.EqualTo("lean_right"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "Bottom").Shape, Is.EqualTo("inv_trapezoid"));

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("Wide top"));
        Assert.That(Svg, Does.Contain("Wide bottom"));
        Assert.That(Svg, Does.Not.Contain("/Wide top/"));
        Assert.That(Svg, Does.Not.Contain(@"\Wide bottom"));

        List<string> NodeFillPaths = ExtractNodeFillPaths(Svg);
        Assert.That(NodeFillPaths, Has.Count.EqualTo(2));
        string LeanRightPath = NodeFillPaths
            .Single(Path => FlowchartTrapezoidBracketPathAnalyser.ValidateRenderedBracketGeometry("lean_right", Path));
        string InvTrapezoidPath = NodeFillPaths
            .Single(Path => FlowchartTrapezoidBracketPathAnalyser.ValidateRenderedBracketGeometry("inv_trapezoid", Path));
        Assert.That(
            FlowchartTrapezoidBracketPathAnalyser.GetHorizontalSpanAtBottomEdge(InvTrapezoidPath),
            Is.GreaterThan(FlowchartTrapezoidBracketPathAnalyser.GetHorizontalSpanAtTopEdge(InvTrapezoidPath)));
        Assert.That(
            FlowchartTrapezoidBracketPathAnalyser.GetLeftEdgeXAtExtremeY(LeanRightPath, UseMaxY: false),
            Is.GreaterThan(FlowchartTrapezoidBracketPathAnalyser.GetLeftEdgeXAtExtremeY(LeanRightPath, UseMaxY: true)));
    }

    [Test]
    public void RenderToSvg_F82_FourTrapezoidBracketVariants_UseDistinctGeometry()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            LeanRight[/lean_right/]
            Trapezoid[/trapezoid \]
            InvTrap[\inv_trapezoid /]
            LeanLeft[\lean_left \]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        List<string> NodeFillPaths = ExtractNodeFillPaths(Svg);
        Assert.That(NodeFillPaths, Has.Count.EqualTo(4));
        Assert.That(
            NodeFillPaths.Count(Path => FlowchartTrapezoidBracketPathAnalyser.ValidateRenderedBracketGeometry("lean_right", Path)),
            Is.EqualTo(1));
        Assert.That(
            NodeFillPaths.Count(Path => FlowchartTrapezoidBracketPathAnalyser.ValidateRenderedBracketGeometry("trapezoid", Path)),
            Is.EqualTo(1));
        Assert.That(
            NodeFillPaths.Count(Path => FlowchartTrapezoidBracketPathAnalyser.ValidateRenderedBracketGeometry("inv_trapezoid", Path)),
            Is.EqualTo(1));
        Assert.That(
            NodeFillPaths.Count(Path => FlowchartTrapezoidBracketPathAnalyser.ValidateRenderedBracketGeometry("lean_left", Path)),
            Is.EqualTo(1));
    }

    [Test]
    public void Layout_F81_TrapezoidBracketGuidePointsTerminateOnVisibleBoundary()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Top[/Wide top/] --> Bottom[\Wide bottom/]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        EdgeContract Edge = Contract.Edges[0];

        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                FlowchartDirection.TopToBottom,
                out List<(double X, double Y)> GuidePoints,
                out FlowchartEdgeRouteKind RouteKind),
            Is.True);

        Dictionary<string, FlowchartEdgeValidationFactsBuilder.FlowchartEdgeValidationFacts> FactsByEdgeId =
            FlowchartEdgeValidationFactsBuilder.BuildEdgeFacts(
                Contract,
                Layout,
                FlowchartDirection.TopToBottom,
                new Dictionary<string, string>());

        string EdgeKey = string.IsNullOrWhiteSpace(Edge.Id) ? Edge.SourceId + "->" + Edge.TargetId : Edge.Id;
        Assert.That(FactsByEdgeId[EdgeKey].TerminateAtVisibleBoundary, Is.True,
            $"Guide points {GuidePoints[0]} -> {GuidePoints[1]}");
    }

    [Test]
    public void RenderToSvg_F11_LR_Chain_PreservesNodeOrder()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Rectangle] --> B(Rounded rectangle) --> C{Diamond decision} --> D((Circle))
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double Ax = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A").CenterX;
        double Bx = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B").CenterX;
        double Cx = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterX;
        double Dx = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D").CenterX;

        Assert.That(Ax, Is.LessThan(Bx));
        Assert.That(Bx, Is.LessThan(Cx));
        Assert.That(Cx, Is.LessThan(Dx));
    }

    [Test]
    public void RenderToSvg_F13_ArrowTipMeetsCylinderTopBoundary()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Store[(Database / cylinder)]
            App[Application] --> Store
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        FlowchartLayoutNodeBounds StoreBounds = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "Store");
        EdgeContract AppToStoreEdge = Contract.Edges.First(Edge => Edge.SourceId == "App");
        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                AppToStoreEdge,
                Contract,
                BoundsById,
                NodeById,
                FlowchartDirection.TopToBottom,
                out List<(double X, double Y)> GuidePoints,
                out FlowchartEdgeRouteKind _),
            Is.True);
        Assert.That(GuidePoints.Count == 2 || FlowchartEdgeConnectionGeometry.IsCollinearStubStraightRoute(GuidePoints), Is.True, "App to Store should be one straight connector, with or without terminal stubs.");

        double ExpectedTopY = StoreBounds.CenterY + (StoreBounds.Height / 2.0);
        Assert.That(GuidePoints[GuidePoints.Count - 1].Y, Is.EqualTo(ExpectedTopY).Within(0.5));

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Match CylinderOutline = Regex.Match(Svg, @"<path d=""([^""]+)""[^>]*fill-opacity=""0""");
        if (!CylinderOutline.Success)
        {
            CylinderOutline = Regex.Match(Svg, @"fill-opacity=""0""[^>]*d=""([^""]+)""");
        }

        Assert.That(CylinderOutline.Success, Is.True);

        Match FirstMove = Regex.Match(CylinderOutline.Groups[1].Value, @"M([\d.]+),([\d.]+)");
        Assert.That(FirstMove.Success, Is.True);

        double CylinderRy = FlowchartShapePathGenerator.GetCylinderEllipseRadiusY(StoreBounds.Width);
        double CylinderApexSvgY = double.Parse(FirstMove.Groups[2].Value, CultureInfo.InvariantCulture) - CylinderRy;

        Match ArrowMatch = Regex.Match(Svg, @"<polygon points=""([^""]+)""");
        Assert.That(ArrowMatch.Success, Is.True);

        double ArrowTipY = ParsePolygonTip(ArrowMatch.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])[1];
        Assert.That(ArrowTipY, Is.EqualTo(CylinderApexSvgY).Within(1.5));
    }

    [Test]
    public void RenderToSvg_F13_Cylinder_OutlineMatchesMermaidWinding()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Store[(Database / cylinder)]
            App[Application] --> Store
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Match Outline = Regex.Match(Svg, @"<path d=""([^""]+)""[^>]*fill-opacity=""0""");
        if (!Outline.Success)
        {
            Outline = Regex.Match(Svg, @"fill-opacity=""0""[^>]*d=""([^""]+)""");
        }

        Assert.That(Outline.Success, Is.True);

        string PathData = Outline.Groups[1].Value;
        int ArcCount = Regex.Matches(PathData, @"\sA[\d\.,]+").Count;
        Assert.That(ArcCount, Is.EqualTo(3), "Cylinder outline should expose back rim, front rim, and bottom arc");
        Assert.That(PathData, Does.StartWith("M"), "Cylinder outline should start at the left body corner");
        Assert.That(PathData, Does.Contain(" 0 0 0 "), "Cylinder arcs should use Mermaid sweep-0 winding");

        Match FillPath = Regex.Match(Svg, @"<path d=""([^""]+)""[^>]*stroke=""none""");
        Assert.That(FillPath.Success, Is.True);
        int FillArcCount = Regex.Matches(FillPath.Groups[1].Value, @"\sA[\d\.,]+").Count;
        Assert.That(FillArcCount, Is.EqualTo(3), "Cylinder fill should use both top arcs and the bottom arc");
    }

    [Test]
    public void TryLayout_F18_DisconnectedRows_FirstRowAboveSecond()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[A] --> B[B]
            C[C] --- D[D]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double AY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A").CenterY;
        double CY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterY;

        Assert.That(AY, Is.GreaterThan(CY), "First declared row (A-B) should sit above the second row (C-D)");
    }

    [Test]
    public void RenderToSvg_F22_BidirectionalArrowTipsMeetNodeBoundaries()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[A] <--> B[B]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        List<SvgStraightConnector> Connectors = SvgStraightConnectorReader.Read(Svg);
        MatchCollection Polygons = Regex.Matches(Svg, @"<polygon points=""([^""]+)""");
        Assert.That(Connectors, Has.Count.EqualTo(1));
        Assert.That(Polygons.Count, Is.GreaterThanOrEqualTo(2));

        double LineStartX = Math.Min(Connectors[0].X1, Connectors[0].X2);
        double LineEndX = Math.Max(Connectors[0].X1, Connectors[0].X2);

        double[] FirstTip = ParsePolygonTip(Polygons[0].Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        double[] SecondTip = ParsePolygonTip(Polygons[1].Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        double LeftTipX = Math.Min(FirstTip[0], SecondTip[0]);
        double RightTipX = Math.Max(FirstTip[0], SecondTip[0]);

        // Default look does not trim the stroke for markers (spec §12): the line runs to each node
        // boundary under the arrowhead, and each tip sits 1 px inside its node.
        Assert.That(RightTipX, Is.EqualTo(LineEndX + 1.0).Within(0.5));
        Assert.That(LeftTipX, Is.EqualTo(LineStartX - 1.0).Within(0.5));
    }

    /// <summary>
    /// Checks that a label box lies wholly on one side of a straight connector and its near edge is within
    /// 40 px of it (spec §15.0C.8). Coordinates are measured across the flow.
    /// </summary>
    /// <param name="LabelLow">The lower edge of the label box across the flow.</param>
    /// <param name="LabelHigh">The upper edge of the label box across the flow.</param>
    /// <param name="LineCoordinate">The connector position across the flow.</param>
    internal static void AssertLabelEdgeBesideLine(double LabelLow, double LabelHigh, double LineCoordinate)
    {
        const double MAXIMUM_LABEL_GAP_PX = 40.0;
        bool ClearOfLine = LabelLow >= LineCoordinate || LabelHigh <= LineCoordinate;
        Assert.That(ClearOfLine, Is.True, $"Label box {LabelLow:0.##}..{LabelHigh:0.##} covers the connector at {LineCoordinate:0.##}.");
        double Gap = Math.Min(Math.Abs(LabelLow - LineCoordinate), Math.Abs(LabelHigh - LineCoordinate));
        Assert.That(Gap, Is.LessThanOrEqualTo(MAXIMUM_LABEL_GAP_PX), "Label should stay close to its connector.");
    }

    private static double[] ParsePolygonTip(string PointToken)
    {
        string[] Coordinates = PointToken.Split(',');
        return new double[]
        {
            double.Parse(Coordinates[0], CultureInfo.InvariantCulture),
            double.Parse(Coordinates[1], CultureInfo.InvariantCulture)
        };
    }

    [Test]
    public void RenderToSvg_F15_Asymmetric_FitsLeftNotchInViewBox()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A>Asymmetric flag] --> B[Target]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Match ViewBox = Regex.Match(Svg, @"viewBox=""0 0 ([\d\.]+) ([\d\.]+)""");
        Match Path = Regex.Match(Svg, @"<path d=""M([\d\.]+),");
        Assert.That(ViewBox.Success, Is.True);
        Assert.That(Path.Success, Is.True);

        double ViewBoxWidth = double.Parse(ViewBox.Groups[1].Value, CultureInfo.InvariantCulture);
        double PathStartX = double.Parse(Path.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.That(PathStartX, Is.GreaterThan(0.0));
        Assert.That(PathStartX, Is.LessThan(ViewBoxWidth));
    }

    [Test]
    public void RenderToSvg_F21_CircleAndCrossMarkers_ArePresent()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[A] --o B[B]
            C[C] --x D[D]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Regex.Matches(Svg, "<circle").Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(Regex.Matches(Svg, @"<line[^>]+stroke-linecap=""round""").Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void RenderToSvg_F22_BidirectionalMarkers_ArePresent()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[A] <--> B[B]
            C[C] o--o D[D]
            E[E] x--x F[F]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Regex.Matches(Svg, "<polygon").Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(Regex.Matches(Svg, "<circle").Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void MeasureWrappedLabel_F26_LongEdgeLabel_WrapsToMultipleLines()
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(MermaidRecognizer.Parse("flowchart LR"));
        string LongLabel =
            "This edge label is intentionally long so wrapping, clearance from arrowheads, and node borders can be verified in Word.";

        FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(LongLabel, Options);

        Assert.That(Dimensions.DisplayText.Split('\n').Length, Is.GreaterThan(1));
        Assert.That(Dimensions.Width, Is.LessThanOrEqualTo(Options.WrappingWidthPx + 1.0));
    }

    [Test]
    public void RenderToSvg_F26_LongEdgeLabel_IsCentredOnConnectorLine()
    {
        string LongLabel =
            "This edge label is intentionally long so wrapping, clearance from arrowheads, and node borders can be verified in Word.";
        DiagramContract Contract = MermaidRecognizer.Parse($"""
            flowchart LR
            A[Producer] -->|"{LongLabel}"| B[Consumer]
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        List<SvgStraightConnector> HorizontalSpineLines = SvgStraightConnectorReader.Read(Svg)
            .Where(Connector => Math.Abs(Connector.Y1 - Connector.Y2) < 0.5)
            .ToList();
        Assert.That(HorizontalSpineLines, Is.Not.Empty, "Expected horizontal connector segments for the labelled edge.");

        double SpineMidSvgY = (HorizontalSpineLines[0].Y1 + HorizontalSpineLines[0].Y2) / 2.0;
        double ConnectorMinSvgX = HorizontalSpineLines.Min(Connector => Math.Min(Connector.X1, Connector.X2));
        double ConnectorMaxSvgX = HorizontalSpineLines.Max(Connector => Math.Max(Connector.X1, Connector.X2));
        double ConnectorMidSvgX = (ConnectorMinSvgX + ConnectorMaxSvgX) / 2.0;

        Match? LabelText = Regex.Matches(
                Svg,
                @"<text x=""([^""]+)"" y=""([^""]+)""[^>]*>(.*?)</text>",
                RegexOptions.Singleline)
            .Cast<Match>()
            .FirstOrDefault(Candidate => Candidate.Groups[3].Value.Contains("intentionally", StringComparison.Ordinal));
        Assert.That(LabelText, Is.Not.Null, "Could not locate wrapped edge-label <text> in SVG.");

        double FirstLineSvgY = double.Parse(LabelText!.Groups[2].Value, CultureInfo.InvariantCulture);
        int LineCount = Regex.Matches(LabelText.Groups[3].Value, "<tspan", RegexOptions.IgnoreCase).Count;
        if (LineCount < 1)
        {
            LineCount = 1;
        }

        double LabelCentreSvgY = FlowchartRichLabelSvgWriter.ComputeLabelBlockCentreY(FirstLineSvgY, LineCount, Options.EdgeLabelFontSizePx, 1.5);
        double LabelHalfHeight = (LineCount * Options.EdgeLabelFontSizePx * 1.5) / 2.0;
        double LabelCentreSvgX = double.Parse(LabelText.Groups[1].Value, CultureInfo.InvariantCulture);

        Assert.That(LabelCentreSvgY, Is.EqualTo(SpineMidSvgY).Within(1.5),
            "Rule B13: wrapped edge labels sit centred on the connector.");
        Assert.That(LabelCentreSvgX, Is.EqualTo(ConnectorMidSvgX).Within(2.0));
        FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            LongLabel,
            Options,
            Options.EdgeLabelFontSizePx);
        double HalfGap = FlowchartEdgeLabelGapHelper.ComputeHalfGapWidthPx(
            LabelDimensions,
            Options.EdgeLabelFontSizePx);
        FlowchartEdgeLabelGapTests.AssertLabelHasStrokeGapAt(
            Svg,
            LabelCentreSvgX,
            LabelCentreSvgY,
            HalfGap,
            LongLabel);
    }

    [Test]
    public void RenderToSvg_F25_SpineLabels_AreCentredOnNodeMidspan()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[Source] -->|"Step 1: validate input"| B[Validator]
            B -->|"Step 2: persist (async)"| C[Store]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Source = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        FlowchartLayoutNodeBounds Validator = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B");
        FlowchartLayoutNodeBounds Store = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C");
        FlowchartDirection Direction = FlowchartDirection.TopToBottom;

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        double SpineX = (Source.CenterX + Validator.CenterX + Store.CenterX) / 3.0;
        Assert.That(Math.Abs(Source.CenterX - SpineX), Is.LessThan(1.0));
        Assert.That(Svg, Does.Contain("Step 1: validate input"));
        Assert.That(Svg, Does.Contain("Step 2: persist (async)"));

        Match Step1Text = Regex.Match(
            Svg,
            @"<text x=""([^""]+)"" y=""([^""]+)""[^>]*>.*?Step 1: validate input.*?</text>",
            RegexOptions.Singleline);
        Match Step2Text = Regex.Match(
            Svg,
            @"<text x=""([^""]+)"" y=""([^""]+)""[^>]*>.*?Step 2: persist \(async\).*?</text>",
            RegexOptions.Singleline);
        Assert.That(Step1Text.Success, Is.True, () => "Could not locate Step 1 edge-label <text> in SVG.");
        Assert.That(Step2Text.Success, Is.True, () => "Could not locate Step 2 edge-label <text> in SVG.");

        double Step1TextX = double.Parse(Step1Text.Groups[1].Value, CultureInfo.InvariantCulture);
        double Step2TextX = double.Parse(Step2Text.Groups[1].Value, CultureInfo.InvariantCulture);

        List<double> VerticalSpineLineXValues = SvgStraightConnectorReader.Read(Svg)
            .Where(Connector => Math.Abs(Connector.X1 - Connector.X2) < 0.5)
            .Select(Connector => Connector.X1)
            .Distinct()
            .ToList();

        Assert.That(VerticalSpineLineXValues, Is.Not.Empty);
        double SpineSvgX = VerticalSpineLineXValues
            .OrderBy(X => Math.Abs(X - Step1TextX))
            .First();
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);

        Assert.That(Step1TextX, Is.EqualTo(SpineSvgX).Within(2.0),
            "Rule B13: Step 1 label is centred on the spine connector.");
        Assert.That(Step2TextX, Is.EqualTo(SpineSvgX).Within(2.0),
            "Rule B13: Step 2 label is centred on the spine connector.");
        FlowchartEdgeLabelGapTests.AssertLabelHasStrokeGap(Svg, "Step 1: validate input", Options);
        FlowchartEdgeLabelGapTests.AssertLabelHasStrokeGap(Svg, "Step 2: persist (async)", Options);

        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        EdgeContract Step1Edge = Contract.Edges.First(Edge => Edge.SourceId == "A");
        EdgeContract Step2Edge = Contract.Edges.First(Edge => Edge.SourceId == "B");
        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Step1Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> Step1GuidePoints,
                out FlowchartEdgeRouteKind _),
            Is.True);
        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Step2Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> Step2GuidePoints,
                out FlowchartEdgeRouteKind _),
            Is.True);

        double ExpectedStep1PortMidLayoutY =
            (Step1GuidePoints[0].Y + Step1GuidePoints[Step1GuidePoints.Count - 1].Y) / 2.0;
        double ExpectedStep2PortMidLayoutY =
            (Step2GuidePoints[0].Y + Step2GuidePoints[Step2GuidePoints.Count - 1].Y) / 2.0;

        Assert.That(ExpectedStep1PortMidLayoutY, Is.GreaterThan(Validator.CenterY));
        Assert.That(ExpectedStep2PortMidLayoutY, Is.GreaterThan(Store.CenterY));
    }

    [Test]
    public void ResolveEdgeLabelPosition_F29_DecisionBranch_LabelHorizontallyCentredOnConnector()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        var BoundsById = Layout.NodeBounds.ToDictionary(Node => Node.NodeId);
        EdgeContract Edge = Contract.Edges.Single(Candidate => Candidate.Label == "Yes");
        Assert.That(Layout.TryCopyDrawnRoutePoints(Edge, out var Points), Is.True);
        var Options = FlowchartRenderOptions.FromContract(Contract);
        var Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel("Yes", Options, Options.EdgeLabelFontSizePx);
        double Gap = FlowchartEdgeLabelGapHelper.ComputeCharacterGapPx(Options.EdgeLabelFontSizePx);
        double Height = FlowchartEdgeLabelPlacementHelper.ComputeRenderedEdgeLabelHeight(Dimensions, Options.EdgeLabelFontSizePx);
        var Runs = Enumerable.Range(0, Points.Count - 1).Select(Index =>
        {
            var Start = Points[Index];
            var End = Points[Index + 1];
            bool Horizontal = Math.Abs(Start.Y - End.Y) < 0.5;
            double Length = Math.Abs(End.X - Start.X) + Math.Abs(End.Y - Start.Y);
            double TextExtent = Horizontal ? Dimensions.Width : Height;
            return new { Index, Start, End, Horizontal, Length, TextExtent };
        }).Where(Run => Run.Length >= Run.TextExtent + 2 * Gap + 40)
          .OrderByDescending(Run => Run.Length).ThenBy(Run => Run.Index).ToArray();
        Assert.That(Runs, Is.Not.Empty, "A branch must provide a fitting straight run.");
        var Longest = Runs[0];
        var Centre = FlowchartSugiyamaSvgRenderer.ResolveDrawnEdgeLabelCentre(
            Edge, Contract, BoundsById, BoundsById["Check"], BoundsById["OK"], "diamond",
            Points, FlowchartDirection.TopToBottom, "LocalDecisionBranch", Options, FlowchartEdgeRouteKind.Grid);
        Assert.That(Centre.X, Is.EqualTo((Longest.Start.X + Longest.End.X) / 2).Within(0.5));
        Assert.That(Centre.Y, Is.EqualTo((Longest.Start.Y + Longest.End.Y) / 2).Within(0.5));
        Assert.That(FlowchartEdgeLabelGapHelper.TryComputeBrokenSegmentSpansForLabelCentre(
            Longest.Start.X, Longest.Start.Y, Longest.End.X, Longest.End.Y,
            Centre.X, Centre.Y, Dimensions, Options.EdgeLabelFontSizePx,
            out var Before, out bool HasBefore, out var After, out bool HasAfter), Is.True);
        Assert.That(HasBefore && HasAfter, Is.True, "Both sides retain a straight stroke.");
        double BeforeDistance = Math.Abs(Before.X2 - Centre.X) + Math.Abs(Before.Y2 - Centre.Y);
        double AfterDistance = Math.Abs(After.X1 - Centre.X) + Math.Abs(After.Y1 - Centre.Y);
        Assert.That(BeforeDistance - Longest.TextExtent / 2, Is.EqualTo(Gap).Within(0.5));
        Assert.That(AfterDistance - Longest.TextExtent / 2, Is.EqualTo(Gap).Within(0.5));
    }

    [Test]
    public void RenderToSvg_F29_DecisionBranchLabels_HorizontallyCentredOnConnector()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        foreach (EdgeContract Edge in Contract.Edges.Where(
                     Candidate => string.Equals(Candidate.SourceId, "Check", StringComparison.Ordinal)
                         && !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            (double ExpectedSvgX, double ExpectedSvgY) = DrawnEdgeLabelLocator.ResolveSvgCentre(Contract, Layout, Edge);

            Match? LabelText = Regex.Matches(
                    Svg,
                    @"<text x=""([^""]+)"" y=""([^""]+)""[^>]*>(.*?)</text>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Cast<Match>()
                .FirstOrDefault(Candidate => Candidate.Groups[3].Value.Contains(Edge.Label!, StringComparison.Ordinal));

            Assert.That(LabelText, Is.Not.Null);
            double LabelSvgX = double.Parse(LabelText!.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(LabelSvgX, Is.EqualTo(ExpectedSvgX).Within(2.0),
                Edge.Label + " should be centred on the branch straight run");
        }
    }

    [Test]
    public void TryLayout_F28_MinLength_IncreasesRankGap()
    {
        DiagramContract TwoDash = MermaidRecognizer.Parse("""
            flowchart TB
            A[A] --> B[B]
            """);
        DiagramContract ThreeDash = MermaidRecognizer.Parse("""
            flowchart TB
            C[C] ---> D[D]
            """);
        DiagramContract FourDash = MermaidRecognizer.Parse("""
            flowchart TB
            E[E] ----> F[F]
            """);

        Assert.That(TwoDash.Edges[0].MinLength, Is.EqualTo(1));
        Assert.That(ThreeDash.Edges[0].MinLength, Is.EqualTo(2));
        Assert.That(FourDash.Edges[0].MinLength, Is.EqualTo(3));

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(TwoDash, out FlowchartLayoutResult LayoutTwo), Is.True);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(ThreeDash, out FlowchartLayoutResult LayoutThree), Is.True);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(FourDash, out FlowchartLayoutResult LayoutFour), Is.True);

        double SpanTwo = LayoutTwo.NodeBounds.Max(Bounds => Bounds.CenterY) - LayoutTwo.NodeBounds.Min(Bounds => Bounds.CenterY);
        double SpanThree = LayoutThree.NodeBounds.Max(Bounds => Bounds.CenterY) - LayoutThree.NodeBounds.Min(Bounds => Bounds.CenterY);
        double SpanFour = LayoutFour.NodeBounds.Max(Bounds => Bounds.CenterY) - LayoutFour.NodeBounds.Min(Bounds => Bounds.CenterY);

        Assert.That(SpanThree, Is.GreaterThan(SpanTwo));
        Assert.That(SpanFour, Is.GreaterThan(SpanThree));
    }

    [Test]
    public void TryLayout_F28_CombinedDiagram_PerPairMinLengthDiffers()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[A] --> B[B]
            C[C] ---> D[D]
            E[E] ----> F[F]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double GapAb = GetFlowAxisGap(Layout, "A", "B");
        double GapCd = GetFlowAxisGap(Layout, "C", "D");
        double GapEf = GetFlowAxisGap(Layout, "E", "F");

        Assert.That(GapCd, Is.GreaterThan(GapAb));
        Assert.That(GapEf, Is.GreaterThan(GapCd));
    }

    [Test]
    public void TryLayout_F28_CombinedDiagram_SourcesAlignedAndDestinationsStagger()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[A] --> B[B]
            C[C] ---> D[D]
            E[E] ----> F[F]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds A = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        FlowchartLayoutNodeBounds B = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B");
        FlowchartLayoutNodeBounds C = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C");
        FlowchartLayoutNodeBounds D = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D");
        FlowchartLayoutNodeBounds E = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "E");
        FlowchartLayoutNodeBounds F = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "F");

        Assert.That(Math.Abs(A.CenterY - C.CenterY), Is.LessThan(1.0));
        Assert.That(Math.Abs(C.CenterY - E.CenterY), Is.LessThan(1.0));
        Assert.That(B.CenterY, Is.GreaterThan(D.CenterY));
        Assert.That(D.CenterY, Is.GreaterThan(F.CenterY));

        Assert.That(A.CenterX, Is.LessThan(C.CenterX));
        Assert.That(C.CenterX, Is.LessThan(E.CenterX));
        Assert.That(Math.Abs(A.CenterX - B.CenterX), Is.LessThan(1.0));
        Assert.That(Math.Abs(C.CenterX - D.CenterX), Is.LessThan(1.0));
        Assert.That(Math.Abs(E.CenterX - F.CenterX), Is.LessThan(1.0));
    }

    [Test]
    public void TryLayout_F28_CombinedDiagram_ColumnsOrderedLeftToRight()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[A] --> B[B]
            C[C] ---> D[D]
            E[E] ----> F[F]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double Ax = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A").CenterX;
        double Cx = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterX;
        double Ex = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "E").CenterX;

        Assert.That(Ax, Is.LessThan(Cx));
        Assert.That(Cx, Is.LessThan(Ex));
    }

    [Test]
    public void TryLayout_F32_FanInSourcesRemainSeparated()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart BT
            W1[Worker 1] --> Agg[Aggregate]
            W2[Worker 2] --> Agg
            W3[Worker 3] --> Agg
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double W1X = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "W1").CenterX;
        double W2X = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "W2").CenterX;
        double W3X = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "W3").CenterX;
        double AggX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "Agg").CenterX;
        double AverageWorkerX = (W1X + W2X + W3X) / 3.0;

        Assert.That(Math.Abs(W1X - W2X), Is.GreaterThan(10.0));
        Assert.That(Math.Abs(W2X - W3X), Is.GreaterThan(10.0));
        Assert.That(Math.Abs(AggX - AverageWorkerX), Is.LessThan(1.0));
    }

    [Test]
    public void TryLayout_F29_DecisionBranchesOrderedByCreationIndex()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double OkX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "OK").CenterX;
        double FailX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "Fail").CenterX;

        Assert.That(OkX, Is.LessThan(FailX));
    }

    [Test]
    public void TryLayout_F33_JoinRowAlignedWithIngressSpine()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Ingress] --> B[Enqueue]
            B --> C[Worker 1]
            B --> D[Worker 2]
            C --> E[Aggregate]
            D --> E
            E --> F[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double IngressY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A").CenterY;
        double EnqueueY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B").CenterY;
        double AggregateY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "E").CenterY;
        double OutY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "F").CenterY;
        double Worker1Y = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterY;
        double Worker2Y = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D").CenterY;

        Assert.That(Math.Abs(IngressY - EnqueueY), Is.LessThan(1.0));
        Assert.That(Math.Abs(OutY - AggregateY), Is.LessThan(1.0));
        Assert.That(Math.Abs(AggregateY - IngressY), Is.LessThan(1.0));
        Assert.That(Math.Abs(Worker1Y - IngressY), Is.GreaterThan(10.0));
        Assert.That(Math.Abs(Worker2Y - IngressY), Is.GreaterThan(10.0));
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F31_HubFanOut_SelectsPortsByTargetPosition()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Hub[Hub] --> W1[Worker 1]
            Hub --> W2[Worker 2]
            Hub --> W3[Worker 3]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartLayoutNodeBounds HubBounds = BoundsById["Hub"];

        foreach (string WorkerId in new[] { "W1", "W2", "W3" })
        {
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Contract.Edges.Single(Edge => Edge.SourceId == "Hub" && Edge.TargetId == WorkerId),
                Contract, BoundsById, NodeById, Direction,
                out var Points, out _, Layout);
            Assert.That(Resolved, Is.True);
            var ExpectedSourceFace = WorkerId switch
            {
                "W1" => FlowchartEdgeAttachFace.Left,
                "W2" => FlowchartEdgeAttachFace.Bottom,
                _ => FlowchartEdgeAttachFace.Right
            };
            Assert.That(FlowchartPerpendicularAttachValidator.ResolveAttachFace(
                HubBounds, Points[0].X, Points[0].Y), Is.EqualTo(ExpectedSourceFace));
            Assert.That(FlowchartPerpendicularAttachValidator.ResolveAttachFace(
                BoundsById[WorkerId], Points[^1].X, Points[^1].Y), Is.EqualTo(FlowchartEdgeAttachFace.Top));
            for (int Index = 1; Index < Points.Count; Index++)
            {
                Assert.That(Math.Abs(Points[Index].X - Points[Index - 1].X) < 0.5
                    || Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5, Is.True);
            }
            if (WorkerId == "W2")
            {
                Assert.That(Points.All(Point => Math.Abs(Point.X - HubBounds.CenterX) < 0.5), Is.True);
            }
        }
        Assert.That(HubBounds.CenterX, Is.EqualTo(BoundsById["W2"].CenterX).Within(0.5));
        Assert.That(BoundsById["W1"].CenterX + BoundsById["W3"].CenterX,
            Is.EqualTo(2 * HubBounds.CenterX).Within(0.5));
    }

    [Test]
    public void RenderToSvg_F31_HubFanOut_CentreSpineStraight_SideBranchesCurved()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Hub[Hub] --> W1[Worker 1]
            Hub --> W2[Worker 2]
            Hub --> W3[Worker 3]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        string CentrePath = OrthogonalRouteTestHelper.ExtractEdgePath(Svg, "Hub", "W2");
        string SidePathTwo = OrthogonalRouteTestHelper.ExtractEdgePath(Svg, "Hub", "W1");
        string SidePathThree = OrthogonalRouteTestHelper.ExtractEdgePath(Svg, "Hub", "W3");

        OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(CentrePath);
        OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(SidePathTwo);
        OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(SidePathThree);
        Assert.That(
            OrthogonalRouteTestHelper.LongestHorizontalRunMidpoint(SidePathThree),
            Is.Not.Null,
            "Outermost fan branch should include a horizontal run before turning down");
    }

    [Test]
    public void RenderToSvg_F31_SideBranches_UseBoundarySRouteWithoutHookCubics()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Hub[Hub] --> W1[Worker 1]
            Hub --> W2[Worker 2]
            Hub --> W3[Worker 3]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        foreach (string TargetId in new[] { "W1", "W3" })
        {
            string PathData = OrthogonalRouteTestHelper.ExtractEdgePath(Svg, "Hub", TargetId);
            OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(PathData);
            Assert.That(PathData, Does.Match(@"^M[^""]+L[^""]+"),
                "Offset fan-out branches begin with a perpendicular source stub");
        }
    }

    [Test]
    public void RenderToSvg_F32_FanIn_UsesCurvedPathsForOffsetWorkers()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            W1[Worker 1] --> Agg[Aggregate]
            W2[Worker 2] --> Agg
            W3[Worker 3] --> Agg
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int CurvedPathCount = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<path d=""M[^""C]+L[^""C]+C[^""]+L[^""C]+"" fill=""none""").Count;
        int StraightLineCount = SvgStraightConnectorReader.Read(Svg).Count;

        Assert.That(StraightLineCount, Is.EqualTo(1), "Centre Worker 2→Aggregate should be a straight vertical line");
        Assert.That(CurvedPathCount, Is.GreaterThanOrEqualTo(2),
            "Offset fan-in edges should use smooth curves, not orthogonal elbow paths");
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F32_FanIn_SelectsTopFaceSourcePortsAndBottomCentreTarget()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart BT
            W1[Worker 1] --> Agg[Aggregate]
            W2[Worker 2] --> Agg
            W3[Worker 3] --> Agg
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        AssertEdgeSourceFace(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "W1",
            "Agg",
            FlowchartEdgeAttachFace.Top,
            FlowchartEdgeRouteKind.Curved);

        AssertEdgeSourceFace(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "W2",
            "Agg",
            FlowchartEdgeAttachFace.Top,
            FlowchartEdgeRouteKind.Straight);

        AssertEdgeSourceFace(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "W3",
            "Agg",
            FlowchartEdgeAttachFace.Top,
            FlowchartEdgeRouteKind.Curved);

        foreach (string WorkerId in new[] { "W1", "W2", "W3" })
        {
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Contract.Edges.First(Edge => Edge.SourceId == WorkerId && Edge.TargetId == "Agg"),
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out _);

            Assert.That(Resolved, Is.True);
            FlowchartEdgeAttachFace TargetFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
                BoundsById["Agg"],
                LayoutPoints[LayoutPoints.Count - 1].X,
                LayoutPoints[LayoutPoints.Count - 1].Y);

            // Spec §15.0C.1: a worker wholly beside the hub joins the middle of the hub's facing side;
            // the aligned worker stays a straight vertical into the bottom face.
            FlowchartLayoutNodeBounds WorkerBounds = BoundsById[WorkerId];
            FlowchartLayoutNodeBounds HubBounds = BoundsById["Agg"];
            FlowchartEdgeAttachFace ExpectedFace = FlowchartEdgeAttachFace.Bottom;
            if (WorkerBounds.CenterX + (WorkerBounds.Width / 2.0) < HubBounds.CenterX - (HubBounds.Width / 2.0))
            {
                ExpectedFace = FlowchartEdgeAttachFace.Left;
            }
            else if (WorkerBounds.CenterX - (WorkerBounds.Width / 2.0) > HubBounds.CenterX + (HubBounds.Width / 2.0))
            {
                ExpectedFace = FlowchartEdgeAttachFace.Right;
            }

            Assert.That(TargetFace, Is.EqualTo(ExpectedFace), $"{WorkerId} should enter Aggregate on the face that faces it (spec §15.0C.1)");
        }
    }

    [Test]
    public void RenderToSvg_F32_SideBranches_UseBoundarySRouteWithoutHookCubics()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart BT
            W1[Worker 1] --> Agg[Aggregate]
            W2[Worker 2] --> Agg
            W3[Worker 3] --> Agg
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        System.Text.RegularExpressions.MatchCollection CurvedPaths = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<path d=""([^""]+)"" fill=""none""");

        int SideBranchPathCount = 0;
        foreach (System.Text.RegularExpressions.Match Match in CurvedPaths)
        {
            string PathData = Match.Groups[1].Value;
            if (!PathData.Contains('C'))
            {
                continue;
            }

            SideBranchPathCount++;
            int CubicCount = System.Text.RegularExpressions.Regex.Matches(PathData, @"C").Count;
            Assert.That(CubicCount, Is.EqualTo(1),
                "Side fan-in branches join the hub side with one quarter curve between the stubs (spec §15.0C.1)");
            Assert.That(PathData, Does.Match(@"M[^""]+L[^""]+C[^""]+L[^""]+$"),
                "Pure curveBasis fan routes must end with a straight line to the target boundary");
        }

        Assert.That(SideBranchPathCount, Is.EqualTo(2), "Worker 1→Agg and Worker 3→Agg should be curved side branches");
    }

    [Test]
    public void RenderToSvg_F33_SpineEdgesRemainStraight()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Ingress] --> B[Enqueue]
            B --> C[Worker 1]
            B --> D[Worker 2]
            C --> E[Aggregate]
            D --> E
            E --> F[Out]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int StraightLineCount = SvgStraightConnectorReader.Read(Svg).Count;

        Assert.That(StraightLineCount, Is.EqualTo(2),
            "Only Ingress→Enqueue and Aggregate→Out should remain straight; fan branches are curved");
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F33_LrFan_SelectsInboardSidePorts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Ingress] --> B[Enqueue]
            B --> C[Worker 1]
            B --> D[Worker 2]
            C --> E[Aggregate]
            D --> E
            E --> F[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        AssertEdgeSourceFace(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "B",
            "C",
            FlowchartEdgeAttachFace.Right,
            FlowchartEdgeRouteKind.Curved);

        AssertEdgeSourceFace(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "B",
            "D",
            FlowchartEdgeAttachFace.Right,
            FlowchartEdgeRouteKind.Curved);

        AssertEdgeSourceFace(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "C",
            "E",
            FlowchartEdgeAttachFace.Right,
            FlowchartEdgeRouteKind.Curved);

        AssertEdgeSourceFace(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "D",
            "E",
            FlowchartEdgeAttachFace.Right,
            FlowchartEdgeRouteKind.Curved);

        foreach (string WorkerId in new[] { "C", "D" })
        {
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Contract.Edges.First(Edge => Edge.SourceId == WorkerId && Edge.TargetId == "E"),
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out _);

            Assert.That(Resolved, Is.True);
            FlowchartEdgeAttachFace TargetFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
                BoundsById["E"],
                LayoutPoints[LayoutPoints.Count - 1].X,
                LayoutPoints[LayoutPoints.Count - 1].Y);
            Assert.That(TargetFace, Is.EqualTo(FlowchartEdgeAttachFace.Left),
                WorkerId + " should enter Aggregate on the inward left face");
        }

        bool Worker1Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "C" && Edge.TargetId == "E"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> Worker1Points,
            out _);
        bool Worker2Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "D" && Edge.TargetId == "E"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> Worker2Points,
            out _);

        Assert.That(Worker1Resolved, Is.True);
        Assert.That(Worker2Resolved, Is.True);
        Assert.That(Math.Abs(BoundsById["D"].CenterY - BoundsById["C"].CenterY), Is.GreaterThan(1.0), "The two workers should sit in different rows");
        bool Worker1IsUpper = BoundsById["C"].CenterY > BoundsById["D"].CenterY;
        List<(double X, double Y)> UpperPoints = Worker1IsUpper ? Worker1Points : Worker2Points;
        List<(double X, double Y)> LowerPoints = Worker1IsUpper ? Worker2Points : Worker1Points;
        Assert.That(
            UpperPoints[UpperPoints.Count - 1].Y,
            Is.GreaterThan(LowerPoints[LowerPoints.Count - 1].Y),
            "Upper worker should enter Aggregate on the upper left slot, not cross beneath the lower worker");
    }

    [Test]
    public void RenderToSvg_F33_SideFanRoutes_UseBoundarySRouteWithoutHookCubics()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Ingress] --> B[Enqueue]
            B --> C[Worker 1]
            B --> D[Worker 2]
            C --> E[Aggregate]
            D --> E
            E --> F[Out]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        System.Text.RegularExpressions.MatchCollection CurvedPaths = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<path d=""([^""]+)"" fill=""none""");

        int SideFanPathCount = 0;
        foreach (System.Text.RegularExpressions.Match Match in CurvedPaths)
        {
            string PathData = Match.Groups[1].Value;
            if (!PathData.Contains('C'))
            {
                continue;
            }

            SideFanPathCount++;
            int CubicCount = System.Text.RegularExpressions.Regex.Matches(PathData, @"C").Count;
            Assert.That(CubicCount, Is.EqualTo(1),
                "LR side fan routes are stub-bridge routes: one cubic between the stubs (spec §15.0C.3)");
            Assert.That(PathData, Does.Match(@"M[^""]+L[^""]+C[^""]+L[^""]+$"),
                "Pure curveBasis fan routes must end with a straight line to the target boundary");
        }

        Assert.That(SideFanPathCount, Is.EqualTo(4),
            "Enqueue→Worker 1/2 and Worker 1/2→Aggregate side branches should be curved");
    }

    [Test]
    public void RenderToSvg_F34_RetryLoop_UsesCurveBasisPath()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Match(@"<path d=""M[^""]+L[^""]+C[^""]+C[^""]+"" fill=""none"""),
            "Multi-point back-edge routes must curve in the middle with straight terminal segments at both ends");
    }

    [Test]
    public void RenderToSvg_F34_RetryLoop_RoutesAboveNodes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int LineCount = Regex.Matches(Svg, "<line ").Count;
        Assert.That(LineCount, Is.LessThan(6), "Retry loop should not render as orthogonal line segments");
    }

    [Test]
    public void TryLayout_F29_BranchTargetsHaveMinimumCrossAxisSeparation()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Ok = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "OK");
        FlowchartLayoutNodeBounds Fail = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "Fail");
        double Gap = (Fail.CenterX - (Fail.Width / 2.0)) - (Ok.CenterX + (Ok.Width / 2.0));
        double MinimumGapPx = FlowchartRenderOptions.FromContract(Contract).NodeSpacingPx;

        Assert.That(Gap, Is.GreaterThanOrEqualTo(MinimumGapPx - 1.0), "Same-rank nodes must respect flowchart.nodeSpacing on the cross axis");
    }

    [Test]
    public void RenderToSvg_F30_MultiWayDecision_SideBranchesUseCurvedPaths()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(OrthogonalRouteTestHelper.IsStraightVerticalConnector(Svg, "C", "E"), Is.True,
            "Centre branch should be a straight vertical connector");

        foreach (string TargetId in new[] { "D", "F" })
        {
            string PathData = OrthogonalRouteTestHelper.ExtractEdgePath(Svg, "C", TargetId);
            OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(PathData);
            Assert.That(PathData, Does.Match(@"^M[^""]+L[^""]+"),
                "Side branches leave the diamond horizontally before turning down");
        }
    }

    [Test]
    public void TryLayout_F29_StartCentredAboveDecision()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Start = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "Start");
        FlowchartLayoutNodeBounds Check = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "Check");

        Assert.That(Math.Abs(Start.CenterX - Check.CenterX), Is.LessThan(1.0));
    }

    [Test]
    public void RenderToSvg_F21_CircleMarkerLeadingEdgeMeetsTargetBoundary()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[A] --o B[B]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Match CircleMatch = Regex.Match(Svg, @"<circle cx=""([^""]+)""");
        Assert.That(CircleMatch.Success, Is.True);
        Assert.That(TryReadConnectorEndX(Svg, out double LineEndX), Is.True);

        double CircleCentreX = double.Parse(CircleMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double CircleLeadingEdgeX = CircleCentreX + 5.0;

        Assert.That(CircleLeadingEdgeX, Is.EqualTo(LineEndX).Within(1.0));
    }

    [Test]
    public void RenderToSvg_F21_CrossMarkerLeadingEdgeMeetsTargetBoundary()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            C[C] --x D[D]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        MatchCollection CrossLines = Regex.Matches(Svg, @"<line[^>]+stroke-linecap=""round""");
        Assert.That(TryReadConnectorEndX(Svg, out double LineEndX), Is.True);
        Assert.That(CrossLines.Count, Is.GreaterThanOrEqualTo(2));
        double CrossMaxX = CrossLines
            .Cast<Match>()
            .SelectMany(Match => new[]
            {
                double.Parse(Regex.Match(Match.Value, @"x1=""([^""]+)""").Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(Regex.Match(Match.Value, @"x2=""([^""]+)""").Groups[1].Value, CultureInfo.InvariantCulture)
            })
            .Max();

        Assert.That(CrossMaxX, Is.EqualTo(LineEndX).Within(1.5));
    }

    [Test]
    public void RenderToSvg_F24_EdgeLabelsHaveNoBackgroundRect()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Request] -->|approve| B[Approved]
            A -->|reject| C[Rejected]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int RectCountBeforeText = Regex.Matches(Svg, @"<rect[^>]*fill=""rgba\(232,232,232").Count;
        Assert.That(RectCountBeforeText, Is.EqualTo(0));
        Assert.That(Svg, Does.Contain("font-size=\"12\""));
    }

    private static double GetFlowAxisGap(FlowchartLayoutResult Layout, string SourceId, string TargetId)
    {
        FlowchartLayoutNodeBounds Source = Layout.NodeBounds.First(Bounds => Bounds.NodeId == SourceId);
        FlowchartLayoutNodeBounds Target = Layout.NodeBounds.First(Bounds => Bounds.NodeId == TargetId);
        return Math.Abs(Target.CenterY - Source.CenterY);
    }

    [Test]
    public void RenderToSvg_F24_FanOut_CentresSourceAndRendersEdgeLabels()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Request] -->|approve| B[Approved]
            A -->|reject| C[Rejected]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Request = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        FlowchartLayoutNodeBounds Approved = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B");
        FlowchartLayoutNodeBounds Rejected = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C");
        double ExpectedY = (Approved.CenterY + Rejected.CenterY) / 2.0;

        Assert.That(Math.Abs(Request.CenterY - ExpectedY), Is.LessThan(1.0));

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("approve"));
        Assert.That(Svg, Does.Contain("reject"));
    }

    [Test]
    public void ResolveEdgeLabelPosition_F30_DecisionBranchesUseHalfPolylineMidspan()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirection.TopToBottom;
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartLayoutNodeBounds HubBounds = BoundsById["C"];

        foreach (EdgeContract Edge in Contract.Edges.Where(
                     Candidate => string.Equals(Candidate.SourceId, "C", StringComparison.Ordinal)
                         && !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(
                FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> GuidePoints,
                    out FlowchartEdgeRouteKind _),
                Is.True);

            FlowchartLayoutNodeBounds TargetBounds = BoundsById[Edge.TargetId];
            (double ExpectedX, double ExpectedY) =
                FlowchartEdgeLabelPlacementHelper.ComputeLocalDecisionBranchLabelExpectedPosition(
                    GuidePoints,
                    HubBounds,
                    Edge.Label,
                    Direction);
            (double ActualX, double ActualY) = FlowchartEdgeLabelPlacementHelper.ResolveEdgeLabelPosition(
                HubBounds,
                TargetBounds,
                GuidePoints,
                Direction,
                "diamond",
                Edge.Label,
                Edge,
                Contract,
                BoundsById,
                "LocalDecisionBranch");

            Assert.That(ActualX, Is.EqualTo(ExpectedX).Within(0.5));
            Assert.That(ActualY, Is.EqualTo(ExpectedY).Within(0.5));
        }
    }

    [Test]
    public void ResolveEdgeLabelPosition_F24_UsesTargetRowMidspan()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Request] -->|approve| B[Approved]
            A -->|reject| C[Rejected]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirection.LeftToRight;
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        foreach (EdgeContract Edge in Contract.Edges.Where(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(
                FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> GuidePoints,
                    out FlowchartEdgeRouteKind _),
                Is.True);

            FlowchartLayoutNodeBounds SourceBounds = BoundsById[Edge.SourceId];
            FlowchartLayoutNodeBounds TargetBounds = BoundsById[Edge.TargetId];
            (double ExpectedX, double ExpectedY) =
                FlowchartEdgeLabelPlacementHelper.ComputeLrLabelledRectFanOutEdgeLabelPosition(
                    GuidePoints,
                    SourceBounds,
                    TargetBounds);
            (double ActualX, double ActualY) = FlowchartEdgeLabelPlacementHelper.ResolveEdgeLabelPosition(
                SourceBounds,
                TargetBounds,
                GuidePoints,
                Direction,
                "rect",
                Edge.Label,
                Edge,
                Contract,
                BoundsById,
                "ShortAdjacent");

            Assert.That(ActualX, Is.EqualTo(ExpectedX).Within(0.5));
            Assert.That(ActualY, Is.EqualTo(ExpectedY).Within(0.5));
            Assert.That(
                Math.Abs(ExpectedY - TargetBounds.CenterY),
                Is.GreaterThan(8.0),
                "Fan-out labels sit on the horizontal elbow segment, not the target row.");
        }
    }

    [Test]
    public void RenderToSvg_ShapeCatalogueRow_NodeLabelsUseMiddleAnchor()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            R[Rect] --> RN(Rounded) --> D{Diamond} --> C((Circle))
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain(">Rect</text>"));
        Assert.That(Svg, Does.Contain("text-anchor=\"middle\" fill="));
        Assert.That(Svg, Does.Not.Contain("dominant-baseline"));
    }

    [Test]
    public void TryLayout_F35_MergeDecisionCentredUnderMiddlePath()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            D -->|yes| E[Continue]
            D -->|no| F[Stop]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double PathAX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A").CenterX;
        double PathBX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B").CenterX;
        double PathCX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterX;
        double MergeX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D").CenterX;

        Assert.That(PathAX, Is.LessThan(PathBX));
        Assert.That(PathBX, Is.LessThan(PathCX));
        Assert.That(Math.Abs(MergeX - PathBX), Is.LessThan(1.0));
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F35_MultiIncoming_UsesBottomCentreSourcesAndDistinctDiamondPorts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            D -->|yes| E[Continue]
            D -->|no| F[Stop]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        foreach (string PathId in new[] { "A", "B", "C" })
        {
            AssertEdgeSourceFace(
                Contract,
                BoundsById,
                NodeById,
                Direction,
                PathId,
                "D",
                FlowchartEdgeAttachFace.Bottom,
                FlowchartEdgeRouteKind.Decision);
        }

        bool PathBResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "B" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathBPoints,
            out _);
        Assert.That(PathBResolved, Is.True);
        Assert.That(PathBPoints.Count, Is.EqualTo(2), "Path B should remain a straight vertical connector");

        bool PathAResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "A" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathAPoints,
            out _);
        bool PathCResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "C" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathCPoints,
            out _);

        Assert.That(PathAResolved, Is.True);
        Assert.That(PathCResolved, Is.True);
        Assert.That(
            PathAPoints[PathAPoints.Count - 1].X,
            Is.LessThan(BoundsById["D"].CenterX),
            "Path A should enter the diamond upper-left perimeter");
        Assert.That(
            PathCPoints[PathCPoints.Count - 1].X,
            Is.GreaterThan(BoundsById["D"].CenterX),
            "Path C should enter the diamond upper-right perimeter");

        (double PathAGuideX, _) = PathAPoints[2];
        (double PathCGuideX, _) = PathCPoints[2];
        Assert.That(
            PathAGuideX,
            Is.EqualTo(PathAPoints[0].X).Within(1.0),
            "Path A guide mirrors yes/no via swapped decision-branch attach (source-column corridor)");
        Assert.That(
            PathCGuideX,
            Is.EqualTo(PathCPoints[0].X).Within(1.0),
            "Path C guide mirrors no/yes via swapped decision-branch attach (source-column corridor)");
    }

    [Test]
    public void RenderToSvg_F35_SideIncomingArrowsUseGuideTangentNotStub()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            D -->|yes| E[Continue]
            D -->|no| F[Stop]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "A" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out _);

        Assert.That(
            FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                LayoutPoints,
                FlowchartEdgeRouteKind.Decision,
                Direction,
                AtTargetEnd: true,
                out string TargetFailure),
            Is.True,
            TargetFailure);

        foreach (string PathId in new[] { "B", "C" })
        {
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Contract.Edges.First(Edge => Edge.SourceId == PathId && Edge.TargetId == "D"),
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> PathPoints,
                out FlowchartEdgeRouteKind PathRouteKind);

            Assert.That(
                FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                    PathPoints,
                    PathRouteKind,
                    Direction,
                    AtTargetEnd: true,
                    out string PathFailure),
                Is.True,
                PathId + "->" + PathFailure);
        }
    }

    [Test]
    public void RenderToSvg_F36_Subgraph_RendersClusterBoxAndTitle()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph SG["Processing"]
              A[Parse] --> B[Transform] --> C[Emit]
            end
            IN[In] --> A
            C --> OUT[Out]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("fill=\"#ffffde\""));
        Assert.That(Svg, Does.Contain("Processing"));
    }

    [Test]
    public void TryLayout_HorizontalRetry_DoneSharesDecisionRowAndRetrySitsBelow()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double HubY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B").CenterY;
        double DoneY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterY;
        double RetryY = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D").CenterY;
        Assert.That(DoneY, Is.EqualTo(HubY).Within(0.5), "Yes branch continues straight ahead");
        Assert.That(RetryY, Is.LessThan(HubY - 8.0), "No branch should sit below the decision hub");
    }

    [Test]
    public void TryLayout_HorizontalRetry_DoneStaysAheadAndRetrySharesDecisionColumn()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double HubX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B").CenterX;
        double HubHalfWidth = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B").Width / 2.0;
        double DoneX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterX;
        double RetryX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D").CenterX;

        Assert.That(DoneX, Is.GreaterThan(HubX + HubHalfWidth + 20.0));
        Assert.That(RetryX, Is.EqualTo(HubX).Within(0.5));
        Assert.That(DoneX - RetryX, Is.GreaterThan(HubHalfWidth + 20.0), "The outcomes remain horizontally separated");
    }

    [Test]
    public void RenderToSvg_F43_NestedSubgraph_RendersInnerAndOuterBoxes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Outer["Outer subgraph"]
              direction TB
              A["Top node"] --> B["Middle node"]
              subgraph Inner["Inner subgraph"]
                direction LR
                C["Left"] --> D["Right"]
              end
              B --> C
              D --> E["Bottom node"]
            end
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds NodeC = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C");
        FlowchartLayoutNodeBounds NodeD = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D");
        Assert.That(Math.Abs(NodeC.CenterX - NodeD.CenterX), Is.LessThan(1.0), "Cross-boundary Inner members share a column");
        Assert.That(NodeC.CenterY, Is.GreaterThan(NodeD.CenterY + 20.0), "Inner members follow parent TB direction");

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> SubgraphRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect InnerRect = SubgraphRects.Single(Rect => Rect.GroupId == "Inner");
        FlowchartSubgraphLayoutRect OuterRect = SubgraphRects.Single(Rect => Rect.GroupId == "Outer");
        Assert.That(InnerRect.Right - InnerRect.Left, Is.GreaterThan(40.0), "Inner cluster has visible width");
        Assert.That(InnerRect.Top - InnerRect.Bottom, Is.GreaterThan(40.0), "Inner cluster has visible height");
        Assert.That(InnerRect.Left, Is.GreaterThanOrEqualTo(OuterRect.Left - 1.0));
        Assert.That(InnerRect.Right, Is.LessThanOrEqualTo(OuterRect.Right + 1.0));

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("Outer subgraph"));
        Assert.That(Svg, Does.Contain("Inner subgraph"));
        Assert.That(Regex.Matches(Svg, "fill=\"#ffffde\"").Count, Is.GreaterThanOrEqualTo(2));
        int InnerTitleIndex = Svg.IndexOf("Inner subgraph", StringComparison.Ordinal);
        int OuterTitleIndex = Svg.IndexOf("Outer subgraph", StringComparison.Ordinal);
        Assert.That(OuterTitleIndex, Is.LessThan(InnerTitleIndex), "Outer cluster paints before Inner so nested box stays visible");
    }

    [Test]
    public void TryLayout_F36_InNodeCentredWithProcessingSubgraph()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph SG["Processing"]
              A[Parse] --> B[Transform] --> C[Emit]
            end
            IN[In] --> A
            C --> OUT[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double InX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "IN").CenterX;
        double OutX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "OUT").CenterX;
        double ParseX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A").CenterX;
        double EmitX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "C").CenterX;
        double SubgraphCenterX = (ParseX + EmitX) / 2.0;

        Assert.That(Math.Abs(InX - SubgraphCenterX), Is.LessThan(1.0));
        Assert.That(Math.Abs(OutX - SubgraphCenterX), Is.LessThan(1.0));
    }

    [Test]
    public void TryLayout_F36_ExternalNodesRespectSubgraphMinimumGap()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph SG["Processing"]
              A[Parse] --> B[Transform] --> C[Emit]
            end
            IN[In] --> A
            C --> OUT[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        FlowchartSubgraphLayoutRect SubgraphRect = FlowchartSubgraphBoundsBuilder
            .BuildOrderedRects(Contract, BoundsById)
            .Single(Rect => Rect.GroupId == "SG");

        FlowchartLayoutNodeBounds InBounds = BoundsById["IN"];
        FlowchartLayoutNodeBounds OutBounds = BoundsById["OUT"];

        double IngressGap = (InBounds.CenterY - (InBounds.Height / 2.0)) - SubgraphRect.Top;
        double EgressGap = SubgraphRect.Bottom - (OutBounds.CenterY + (OutBounds.Height / 2.0));
        double MinimumGap = FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX;

        Assert.That(IngressGap + 0.5, Is.GreaterThanOrEqualTo(MinimumGap));
        Assert.That(EgressGap + 0.5, Is.GreaterThanOrEqualTo(MinimumGap));
    }

    [Test]
    public void TryLayout_F37_CrossBoundaryLinks_UseDiagramLrDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            subgraph Box["Vertical stack inside horizontal diagram"]
              direction TB
              S1[Step 1] --> S2[Step 2] --> S3[Step 3]
            end
            Start[Start] --> S1
            S3 --> End[End]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds S1 = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "S1");
        FlowchartLayoutNodeBounds S2 = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "S2");
        FlowchartLayoutNodeBounds S3 = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "S3");

        Assert.That(Math.Abs(S1.CenterY - S2.CenterY), Is.LessThan(1.0), "Cross-boundary LR members share a row");
        Assert.That(Math.Abs(S2.CenterY - S3.CenterY), Is.LessThan(1.0), "Cross-boundary LR members share a row");
        Assert.That(S2.CenterX - S1.CenterX, Is.GreaterThan(40.0), "Step 1 sits left of Step 2");
        Assert.That(S3.CenterX - S2.CenterX, Is.GreaterThan(40.0), "Step 2 sits left of Step 3");
    }

    [Test]
    public void TryLayout_F37_ExternalNodesRespectSubgraphMinimumGap()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            subgraph Box["Vertical stack inside horizontal diagram"]
              direction TB
              S1[Step 1] --> S2[Step 2] --> S3[Step 3]
            end
            Start[Start] --> S1
            S3 --> End[End]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        FlowchartSubgraphLayoutRect SubgraphRect = FlowchartSubgraphBoundsBuilder
            .BuildOrderedRects(Contract, BoundsById)
            .Single(Rect => Rect.GroupId == "Box");

        FlowchartLayoutNodeBounds StartBounds = BoundsById["Start"];
        FlowchartLayoutNodeBounds EndBounds = BoundsById["End"];

        double StartTrailing = StartBounds.CenterX + (StartBounds.Width / 2.0);
        double EndLeading = EndBounds.CenterX - (EndBounds.Width / 2.0);
        double IngressGap = SubgraphRect.Left - StartTrailing;
        double EgressGap = EndLeading - SubgraphRect.Right;
        double MinimumGap = FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX;

        Assert.That(IngressGap + 0.5, Is.GreaterThanOrEqualTo(MinimumGap));
        Assert.That(EgressGap + 0.5, Is.GreaterThanOrEqualTo(MinimumGap));
    }

    [Test]
    public void TryLayout_F38_CrossBoundaryLinks_UseDiagramTbDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Pipeline["Pipeline"]
              direction LR
              P1[Read] --> P2[Map] --> P3[Write]
            end
            Ingress[Ingress] --> P1
            P3 --> Egress[Egress]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds P1 = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "P1");
        FlowchartLayoutNodeBounds P2 = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "P2");
        FlowchartLayoutNodeBounds P3 = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "P3");

        Assert.That(Math.Abs(P1.CenterX - P2.CenterX), Is.LessThan(1.0), "Cross-boundary TB members share a column");
        Assert.That(Math.Abs(P2.CenterX - P3.CenterX), Is.LessThan(1.0), "Cross-boundary TB members share a column");
        Assert.That(P1.CenterY, Is.GreaterThan(P2.CenterY + 20.0), "Read sits above Map");
        Assert.That(P2.CenterY, Is.GreaterThan(P3.CenterY + 20.0), "Map sits above Write");
    }

    [Test]
    public void TryLayout_F40_SiblingSubgraphsHaveMinimumGap()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            subgraph S0["Stage A"]
              direction TB
              a0[A0] --> a1[A1]
            end
            subgraph S1["Stage B"]
              direction TB
              b0[B0] --> b1[B1]
            end
            IN[In] --> a0
            a1 --> b0
            b1 --> OUT[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> Rects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect StageA = Rects.Single(Rect => Rect.GroupId == "S0");
        FlowchartSubgraphLayoutRect StageB = Rects.Single(Rect => Rect.GroupId == "S1");

        Assert.That(StageB.Left - StageA.Right, Is.GreaterThanOrEqualTo(20.0));
    }

    [Test]
    public void TryLayout_F111_SiblingSubgraphsHaveMinimumGap()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph L["Left"]
              L1[L1]
              L2[L2]
              L3[L3]
            end
            subgraph R["Right"]
              R1[R1]
              R2[R2]
              R3[R3]
            end
            L1 --> R1
            L1 --> R2
            L1 --> R3
            L2 --> R1
            L2 --> R2
            L2 --> R3
            L3 --> R1
            L3 --> R2
            L3 --> R3
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> Rects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect Left = Rects.Single(Rect => Rect.GroupId == "L");
        FlowchartSubgraphLayoutRect Right = Rects.Single(Rect => Rect.GroupId == "R");

        Assert.That(Left.Bottom - Right.Top, Is.GreaterThanOrEqualTo(20.0));
    }

    [Test]
    public void TryLayout_F111_SubgraphMembersAreCenteredWithMinimumInsets()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph L["Left"]
              L1[L1]
              L2[L2]
              L3[L3]
            end
            subgraph R["Right"]
              R1[R1]
              R2[R2]
              R3[R3]
            end
            L1 --> R1
            L1 --> R2
            L1 --> R3
            L2 --> R1
            L2 --> R2
            L2 --> R3
            L3 --> R1
            L3 --> R2
            L3 --> R3
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> Rects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect Left = Rects.Single(Rect => Rect.GroupId == "L");
        FlowchartSubgraphLayoutRect Right = Rects.Single(Rect => Rect.GroupId == "R");

        foreach (string NodeId in new[] { "L1", "L2", "L3" })
        {
            FlowchartLayoutNodeBounds Node = BoundsById[NodeId];
            double NodeLeft = Node.CenterX - (Node.Width / 2.0);
            double NodeRight = Node.CenterX + (Node.Width / 2.0);
            double NodeBottom = Node.CenterY - (Node.Height / 2.0);
            double NodeTop = Node.CenterY + (Node.Height / 2.0);

            Assert.That(NodeLeft - Left.Left, Is.GreaterThanOrEqualTo(FlowchartSubgraphBoundsBuilder.PADDING_PX - 1.0));
            Assert.That(Left.Right - NodeRight, Is.GreaterThanOrEqualTo(FlowchartSubgraphBoundsBuilder.PADDING_PX - 1.0));
            Assert.That(
                NodeBottom - Left.Bottom,
                Is.GreaterThanOrEqualTo(
                    FlowchartSubgraphBoundsBuilder.PADDING_PX
                    + FlowchartSubgraphBoundsBuilder.VERTICAL_CONTENT_SLACK_PX
                    - 1.0));
            Assert.That(
                Left.Top - NodeTop,
                Is.GreaterThanOrEqualTo(
                    FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX
                    + FlowchartSubgraphBoundsBuilder.PADDING_PX
                    - 1.0));
        }

        double LeftContentCenterY = BoundsById.Values
            .Where(Bounds => Bounds.NodeId.StartsWith("L", StringComparison.Ordinal))
            .Average(Bounds => Bounds.CenterY);
        double LeftRegionCenterY = (Left.Bottom
            + FlowchartSubgraphBoundsBuilder.PADDING_PX
            + FlowchartSubgraphBoundsBuilder.VERTICAL_CONTENT_SLACK_PX
            + Left.Top
            - FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX
            - FlowchartSubgraphBoundsBuilder.PADDING_PX) / 2.0;
        Assert.That(Math.Abs(LeftContentCenterY - LeftRegionCenterY), Is.LessThan(2.0));

        double RightContentCenterY = BoundsById.Values
            .Where(Bounds => Bounds.NodeId.StartsWith("R", StringComparison.Ordinal))
            .Average(Bounds => Bounds.CenterY);
        double RightRegionCenterY = (Right.Bottom
            + FlowchartSubgraphBoundsBuilder.PADDING_PX
            + FlowchartSubgraphBoundsBuilder.VERTICAL_CONTENT_SLACK_PX
            + Right.Top
            - FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX
            - FlowchartSubgraphBoundsBuilder.PADDING_PX) / 2.0;
        Assert.That(Math.Abs(RightContentCenterY - RightRegionCenterY), Is.LessThan(2.0));
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F59_NoBranch_UsesSidePortFacingWaitAndWaitCentreTarget()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Worker["Worker pool"]
              direction TB
              Q[Queue] --> W{Worker free?}
              W -->|Yes| P[Process job]
              W -->|No| Wait[Wait]
              Wait --> Q
            end
            Client[Client] --> Q
            P --> Out[Output]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        EdgeContract NoEdge = Contract.Edges.Single(Edge => Edge.SourceId == "W" && Edge.TargetId == "Wait");
        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                NoEdge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> NoPoints,
                out _),
            Is.True);

        double HubCentreX = BoundsById["W"].CenterX;
        double WaitSide = Math.Sign(BoundsById["Wait"].CenterX - HubCentreX);
        Assert.That(WaitSide, Is.Not.EqualTo(0.0), "Wait should sit beside the decision, not on its spine.");
        Assert.That((NoPoints[0].X - HubCentreX) * WaitSide, Is.GreaterThan(8.0), "The No branch should leave the diamond slope that faces Wait.");
        Assert.That(NoPoints[NoPoints.Count - 1].X, Is.EqualTo(BoundsById["Wait"].CenterX).Within(1.0));
    }

    [Test]
    public void TryLayout_F73_ParallelColumnsShareAlignedRows()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph one
              a1[Alpha] --> a2[Beta]
            end
            subgraph two
              b1[Gamma] --> b2[Delta]
            end
            Ingress[Ingress] --> a1
            b2 --> Egress[Egress]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);

        Assert.That(Math.Abs(BoundsById["Ingress"].CenterY - BoundsById["b1"].CenterY), Is.LessThan(2.0));
        Assert.That(Math.Abs(BoundsById["a1"].CenterY - BoundsById["b2"].CenterY), Is.LessThan(2.0));
        Assert.That(Math.Abs(BoundsById["a2"].CenterY - BoundsById["Egress"].CenterY), Is.LessThan(2.0));
        Assert.That(BoundsById["b1"].CenterX - BoundsById["Ingress"].CenterX, Is.GreaterThan(40.0));
    }

    [Test]
    public void TryLayout_F60_SiblingSubgraphsShareCrossBoundarySpineAndHorizontalPrimaryFlow()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            subgraph Ingest["Ingest"]
              direction TB
              I1[Receive drop] --> I2[Verify manifest]
            end
            subgraph Transform["Transform"]
              direction TB
              T1[Normalise] --> T2[Translate]
            end
            subgraph Publish["Publish"]
              direction TB
              P1[Package] --> P2[Release]
            end
            Source[Source] --> I1
            I2 --> T1
            T2 --> P1
            P2 --> Sink[Sink]
            I2 -.->|audit copy| P2
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);

        Assert.That(Math.Abs(BoundsById["I1"].CenterY - BoundsById["I2"].CenterY), Is.LessThan(1.0));
        Assert.That(Math.Abs(BoundsById["T1"].CenterY - BoundsById["T2"].CenterY), Is.LessThan(1.0));
        Assert.That(Math.Abs(BoundsById["P1"].CenterY - BoundsById["P2"].CenterY), Is.LessThan(1.0));
        Assert.That(Math.Abs(BoundsById["I2"].CenterY - BoundsById["T1"].CenterY), Is.LessThan(2.0));
        Assert.That(Math.Abs(BoundsById["T2"].CenterY - BoundsById["P1"].CenterY), Is.LessThan(2.0));
        Assert.That(Math.Abs(BoundsById["Source"].CenterY - BoundsById["I1"].CenterY), Is.LessThan(2.0));
        Assert.That(Math.Abs(BoundsById["P2"].CenterY - BoundsById["Sink"].CenterY), Is.LessThan(2.0));
    }

    [Test]
    public void RenderToSvg_F46_NestedSubgraph_RendersInnerAndOuterBoxes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            subgraph OuterLR["Outer LR"]
              direction LR
              O1[O1] --> O2[O2]
              subgraph InnerLR["Inner LR"]
                direction LR
                I1[I1] --> I2[I2]
              end
              O2 --> I1
              I2 --> O3[O3]
            end
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("Outer LR"));
        Assert.That(Svg, Does.Contain("Inner LR"));
        Assert.That(Regex.Matches(Svg, "fill=\"#ffffde\"").Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void TryLayout_F46_NestedInnerSubgraph_ContainedInOuterWithMinimumSeparation()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            subgraph OuterLR["Outer LR"]
              direction LR
              O1[O1] --> O2[O2]
              subgraph InnerLR["Inner LR"]
                direction LR
                I1[I1] --> I2[I2]
              end
              O2 --> I1
              I2 --> O3[O3]
            end
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> SubgraphRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect OuterRect = SubgraphRects.Single(Rect => Rect.GroupId == "OuterLR");
        FlowchartSubgraphLayoutRect InnerRect = SubgraphRects.Single(Rect => Rect.GroupId == "InnerLR");

        double MinimumGap = FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX;

        Assert.That(InnerRect.Bottom, Is.GreaterThanOrEqualTo(OuterRect.Bottom + MinimumGap - 0.5),
            "Inner cluster must sit inside Outer with vertical separation");
        Assert.That(InnerRect.Top, Is.LessThanOrEqualTo(OuterRect.Top - MinimumGap + 0.5),
            "Inner cluster must sit inside Outer with vertical separation");

        FlowchartLayoutNodeBounds NodeO2 = BoundsById["O2"];
        FlowchartLayoutNodeBounds NodeO3 = BoundsById["O3"];
        double O2Trailing = NodeO2.CenterX + (NodeO2.Width / 2.0);
        double O3Leading = NodeO3.CenterX - (NodeO3.Width / 2.0);

        Assert.That(InnerRect.Left - O2Trailing, Is.GreaterThanOrEqualTo(MinimumGap - 0.5),
            "O2 must keep minimum flow-axis gap before Inner cluster");
        Assert.That(O3Leading - InnerRect.Right, Is.GreaterThanOrEqualTo(MinimumGap - 0.5),
            "O3 must keep minimum flow-axis gap after Inner cluster");
    }

    [Test]
    public void TryLayout_PipelineWithEarlyExit_PreservesSpineAndSeparatesFinalBranches()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Ingest[Ingest file]
            Ingest --> Validate{Valid markdown?}
            Validate -->|No| Reject[Reject with error]
            Validate -->|Yes| Parse[Parse blocks]
            Parse --> HasMermaid{Mermaid fence?}
            HasMermaid -->|Yes| Render[Render SVG]
            HasMermaid -->|No| Word[Build DOCX]
            Render --> Word
            DoArchive{Archive?}
            Word --> DoArchive
            DoArchive -->|Yes| Persist
            DoArchive -->|No| Done
            Reject --> Done
            Persist[(Optional archive)] --> Done
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        double SpineX = BoundsById["Start"].CenterX;
        HashSet<double> DistinctColumns = Layout.NodeBounds
            .Select(Bounds => Math.Round(Bounds.CenterX / 30.0) * 30.0)
            .ToHashSet();

        Assert.That(DistinctColumns.Count, Is.GreaterThanOrEqualTo(3), "Diagram should use several vertical columns");

        foreach (string SpineNodeId in new[] { "Start", "Ingest", "Validate", "Parse", "HasMermaid", "Word", "DoArchive" })
        {
            Assert.That(BoundsById[SpineNodeId].CenterX, Is.EqualTo(SpineX).Within(1.5),
                SpineNodeId + " should stay on the spine column");
        }

        Assert.That(BoundsById["Reject"].CenterX, Is.LessThan(BoundsById["Render"].CenterX - 40.0),
            "Early exit should sit in the outermost side column");
        Assert.That(BoundsById["Render"].CenterX, Is.LessThan(SpineX - 40.0),
            "Detour should sit in the column beside the spine");
        Assert.That(BoundsById["Reject"].CenterX, Is.LessThan(SpineX - 40.0),
            "Early exit should sit left of the spine");

        Assert.That(BoundsById["Persist"].CenterY, Is.GreaterThan(BoundsById["Done"].CenterY + 40.0),
            "Optional archive sits above the terminal collector");
        AssertBranchesOnBothSides(BoundsById, "DoArchive", "Done", "Persist");
        Assert.That(BoundsById["Done"].CenterX, Is.LessThan(SpineX - 40.0),
            "Terminal collector sits beside the final decision");
        Assert.That(BoundsById["Persist"].CenterX, Is.GreaterThan(SpineX + 40.0),
            "Optional detour sits opposite the terminal collector");
        Assert.That(BoundsById["Reject"].CenterY, Is.EqualTo(BoundsById["Done"].CenterY).Within(1.5),
            "Early exit sits beside the terminal collector");
        double EarlyExitRight = BoundsById["Reject"].CenterX + BoundsById["Reject"].Width / 2.0;
        double CollectorLeft = BoundsById["Done"].CenterX - BoundsById["Done"].Width / 2.0;
        Assert.That(CollectorLeft - EarlyExitRight, Is.GreaterThanOrEqualTo(30.0 - 0.01),
            "Early exit and terminal collector retain a grid step of clearance");
        Assert.That(BoundsById["Validate"].CenterY, Is.GreaterThan(BoundsById["Reject"].CenterY + 40.0),
            "Validate stays above the early-exit branch row");
    }

    private static void AssertBranchesOnBothSides(Dictionary<string, FlowchartLayoutNodeBounds> BoundsById, string DecisionId, string LeftTargetId, string RightTargetId)
    {
        double DecisionX = BoundsById[DecisionId].CenterX;
        Assert.That(BoundsById[LeftTargetId].CenterX, Is.LessThan(DecisionX - 8.0), $"{LeftTargetId} should sit left of {DecisionId}");
        Assert.That(BoundsById[RightTargetId].CenterX, Is.GreaterThan(DecisionX + 8.0), $"{RightTargetId} should sit right of {DecisionId}");
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F58_TerminalCollectorDoneFanIn_UsesSidePorts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Ingest[Ingest file]
            Ingest --> Validate{Valid markdown?}
            Validate -->|No| Reject[Reject with error]
            Validate -->|Yes| Parse[Parse blocks]
            Parse --> HasMermaid{Mermaid fence?}
            HasMermaid -->|Yes| Render[Render SVG]
            HasMermaid -->|No| Word[Build DOCX]
            Render --> Word
            DoArchive{Archive?}
            Word --> DoArchive
            DoArchive -->|Yes| Persist
            DoArchive -->|No| Done
            Reject --> Done
            Persist[(Optional archive)] --> Done
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        // Spec §15.0C.1: sources wholly beside Done join the middle of Done's facing side.
        AssertEdgeTargetFace(
            Contract,
            Layout,
            BoundsById,
            NodeById,
            Direction,
            "Reject",
            "Done",
            FlowchartEdgeAttachFace.Left);

        FlowchartLayoutNodeBounds PersistBounds = BoundsById["Persist"];
        FlowchartLayoutNodeBounds DoneBounds = BoundsById["Done"];
        FlowchartEdgeAttachFace PersistEntryFace = Math.Abs(PersistBounds.CenterX - DoneBounds.CenterX) <= 4.0
            ? FlowchartEdgeAttachFace.Top
            : FlowchartEdgeAttachFace.Right;
        AssertEdgeTargetFace(
            Contract,
            Layout,
            BoundsById,
            NodeById,
            Direction,
            "Persist",
            "Done",
            PersistEntryFace);
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F58_HasMermaidBranches_UseCorrectRouteStyles()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Ingest[Ingest file]
            Ingest --> Validate{Valid markdown?}
            Validate -->|No| Reject[Reject with error]
            Validate -->|Yes| Parse[Parse blocks]
            Parse --> HasMermaid{Mermaid fence?}
            HasMermaid -->|Yes| Render[Render SVG]
            HasMermaid -->|No| Word[Build DOCX]
            Render --> Word
            DoArchive{Archive?}
            Word --> DoArchive
            DoArchive -->|Yes| Persist
            DoArchive -->|No| Done
            Reject --> Done
            Persist[(Optional archive)] --> Done
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        AssertHasMermaidBranchRoute(
            Contract,
            Layout,
            BoundsById,
            NodeById,
            Direction,
            "Render",
            ExpectStraight: false);

        AssertHasMermaidBranchRoute(
            Contract,
            Layout,
            BoundsById,
            NodeById,
            Direction,
            "Word",
            ExpectStraight: false);
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F58_DecisionBranches_UseCorrectSourcePorts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Ingest[Ingest file]
            Ingest --> Validate{Valid markdown?}
            Validate -->|No| Reject[Reject with error]
            Validate -->|Yes| Parse[Parse blocks]
            Parse --> HasMermaid{Mermaid fence?}
            HasMermaid -->|Yes| Render[Render SVG]
            HasMermaid -->|No| Word[Build DOCX]
            Render --> Word
            DoArchive{Archive?}
            Word --> DoArchive
            DoArchive -->|Yes| Persist
            DoArchive -->|No| Done
            Reject --> Done
            Persist[(Optional archive)] --> Done
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        // Two-way decisions leave from the shoulder that faces each target, never the bottom vertex.
        AssertDecisionSourceAttachLeftOfHub(Contract, Layout, BoundsById, NodeById, Direction, "Validate", "Reject");
        AssertDecisionSourceAttachRightOfHub(Contract, Layout, BoundsById, NodeById, Direction, "Validate", "Parse");
        AssertDecisionSourceAttachLeftOfHub(Contract, Layout, BoundsById, NodeById, Direction, "HasMermaid", "Render");
        AssertCollinearDecisionBranch(Contract, Layout, BoundsById, NodeById, Direction, "HasMermaid", "Word");
        AssertDecisionSourceAttachLeftOfHub(Contract, Layout, BoundsById, NodeById, Direction, "DoArchive", "Persist");
        AssertDecisionSourceAttachLeftOfHub(Contract, Layout, BoundsById, NodeById, Direction, "DoArchive", "Done");
    }

    [Test]
    public void RenderToSvg_F58_FullPipeline_RendersShapesAndWrappedLabels()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Ingest[Ingest file]
            Ingest --> Validate{Valid markdown?}
            Validate -->|No| Reject[Reject with error]
            Validate -->|Yes| Parse[Parse blocks]
            Parse --> HasMermaid{Mermaid fence?}
            HasMermaid -->|Yes| Render[Render SVG]
            HasMermaid -->|No| Word[Build DOCX]
            Render --> Word
            DoArchive{Archive?}
            Word --> DoArchive
            DoArchive -->|Yes| Persist
            DoArchive -->|No| Done
            Reject --> Done
            Persist[(Optional archive)] --> Done
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("Ingest file"));
        Assert.That(Svg, Does.Contain("Valid markdown?"));
        Assert.That(Svg, Does.Contain("Optional archive"));
        Assert.That(Regex.Matches(Svg, @"\sA[\d\.,]+").Count, Is.GreaterThanOrEqualTo(3),
            "Persist cylinder and stadium Start should expose arc segments");
        Assert.That(Svg, Does.Contain(@"data-edge-source=""Validate"" data-edge-target=""Reject"""));
        Assert.That(Svg, Does.Contain(@"data-edge-source=""Reject"" data-edge-target=""Done"""));
        Assert.That(Svg, Does.Contain(@"data-edge-source=""Persist"" data-edge-target=""Done"""));
        Assert.That(Regex.IsMatch(Svg, @"<path d=""[^""]*S[^""]*"""), Is.False,
            "Routes must not use smooth cubic continuation");
        Assert.That(Regex.Matches(Svg, @"<path d=""[^""]+L[^""]+"" fill=""none""").Count,
            Is.GreaterThanOrEqualTo(2),
            "Decision branches and collector routes should use orthogonal straight runs");

        int PersistLabelIndex = Svg.IndexOf("Optional archive", StringComparison.Ordinal);
        int DoneNodeIndex = Svg.LastIndexOf("Done", StringComparison.Ordinal);
        Assert.That(PersistLabelIndex, Is.GreaterThan(0));
        Assert.That(DoneNodeIndex, Is.GreaterThan(PersistLabelIndex),
            "Optional archive node should be painted before Done in SVG document order");
    }

    [Test]
    public void F44_ForwardReferenceIntoNestedSubgraph_MovesNodeToInnerOnExplicitDeclaration()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Outer["Outer"]
              direction TB
              Start[Start] --> InnerTop
              subgraph Inner["Inner TB"]
                direction TB
                InnerTop[Inner top] --> InnerBottom[Inner bottom]
              end
              InnerBottom --> End[End]
            end
            """);

        NodeContract InnerTop = Contract.Nodes.Single(Node => Node.Id == "InnerTop");
        NodeContract InnerBottom = Contract.Nodes.Single(Node => Node.Id == "InnerBottom");
        GroupContract Inner = Contract.Groups.Single(Group => Group.Id == "Inner");
        GroupContract Outer = Contract.Groups.Single(Group => Group.Id == "Outer");

        Assert.That(InnerTop.ParentGroupId, Is.EqualTo("Inner"));
        Assert.That(InnerBottom.ParentGroupId, Is.EqualTo("Inner"));
        Assert.That(Inner.ChildNodeIds, Is.EquivalentTo(new[] { "InnerTop", "InnerBottom" }));
        Assert.That(Outer.ChildNodeIds, Is.EquivalentTo(new[] { "Start", "End" }));
    }

    [Test]
    public void TryLayout_F44_NestedInnerSubgraph_StacksMembersInsideInnerCluster()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Outer["Outer"]
              direction TB
              Start[Start] --> InnerTop
              subgraph Inner["Inner TB"]
                direction TB
                InnerTop[Inner top] --> InnerBottom[Inner bottom]
              end
              InnerBottom --> End[End]
            end
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Start = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "Start");
        FlowchartLayoutNodeBounds InnerTop = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "InnerTop");
        FlowchartLayoutNodeBounds InnerBottom = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "InnerBottom");
        FlowchartLayoutNodeBounds End = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "End");

        Assert.That(Math.Abs(InnerTop.CenterX - InnerBottom.CenterX), Is.LessThan(1.0));
        Assert.That(InnerTop.CenterY, Is.GreaterThan(InnerBottom.CenterY + 20.0));
        Assert.That(Start.CenterY, Is.GreaterThan(InnerTop.CenterY + 20.0));
        Assert.That(InnerBottom.CenterY, Is.GreaterThan(End.CenterY + 20.0));

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        FlowchartSubgraphLayoutRect InnerRect =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById)
                .Single(Rect => Rect.GroupId == "Inner");

        Assert.That(InnerTop.CenterY + (InnerTop.Height / 2.0), Is.LessThanOrEqualTo(InnerRect.Top + 1.0));
        Assert.That(InnerTop.CenterY - (InnerTop.Height / 2.0), Is.GreaterThanOrEqualTo(InnerRect.Bottom - 1.0));
        Assert.That(InnerBottom.CenterY + (InnerBottom.Height / 2.0), Is.LessThanOrEqualTo(InnerRect.Top + 1.0));
        Assert.That(InnerBottom.CenterY - (InnerBottom.Height / 2.0), Is.GreaterThanOrEqualTo(InnerRect.Bottom - 1.0));
    }

    [Test]
    public void TryLayout_F44_NestedInnerSubgraph_ContainedInOuterWithMinimumSeparation()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Outer["Outer"]
              direction TB
              Start[Start] --> InnerTop
              subgraph Inner["Inner TB"]
                direction TB
                InnerTop[Inner top] --> InnerBottom[Inner bottom]
              end
              InnerBottom --> End[End]
            end
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        List<FlowchartSubgraphLayoutRect> SubgraphRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        FlowchartSubgraphLayoutRect OuterRect = SubgraphRects.Single(Rect => Rect.GroupId == "Outer");
        FlowchartSubgraphLayoutRect InnerRect = SubgraphRects.Single(Rect => Rect.GroupId == "Inner");

        double MinimumGap = FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX;

        Assert.That(InnerRect.Left, Is.GreaterThanOrEqualTo(OuterRect.Left + MinimumGap - 0.5),
            "Inner cluster must sit inside Outer with lateral separation");
        Assert.That(InnerRect.Right, Is.LessThanOrEqualTo(OuterRect.Right - MinimumGap + 0.5),
            "Inner cluster must sit inside Outer with lateral separation");

        FlowchartLayoutNodeBounds Start = BoundsById["Start"];
        FlowchartLayoutNodeBounds End = BoundsById["End"];
        double StartBottom = Start.CenterY - (Start.Height / 2.0);
        double EndTop = End.CenterY + (End.Height / 2.0);

        Assert.That(StartBottom - InnerRect.Top, Is.GreaterThanOrEqualTo(MinimumGap - 0.5),
            "Start must keep minimum flow-axis gap above Inner cluster");
        Assert.That(InnerRect.Bottom - EndTop, Is.GreaterThanOrEqualTo(MinimumGap - 0.5),
            "End must keep minimum flow-axis gap below Inner cluster");
    }

    [Test]
    public void RenderToSvg_F44_NestedInnerSubgraph_RendersBothClusterBoxes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Outer["Outer"]
              direction TB
              Start[Start] --> InnerTop
              subgraph Inner["Inner TB"]
                direction TB
                InnerTop[Inner top] --> InnerBottom[Inner bottom]
              end
              InnerBottom --> End[End]
            end
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("Outer"));
        Assert.That(Svg, Does.Contain("Inner TB"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(Svg, "fill=\"#ffffde\"").Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void TryLayout_F45_TwoLevelNesting_PlacesDirectMembersOutsideNestedClusters()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph L0["Level 0"]
              direction TB
              A[A] --> B[B]
              subgraph L1["Level 1"]
                direction TB
                subgraph L2["Level 2"]
                  direction TB
                  C[C] --> D[D]
                end
              end
              B --> C
              D --> E[E]
            end
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSubgraphBoundsRequest BoundsRequest =
            FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        List<FlowchartSubgraphLayoutRect> SubgraphRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest);
        FlowchartSubgraphLayoutRect L1Rect = SubgraphRects.Single(Rect => Rect.GroupId == "L1");
        FlowchartSubgraphLayoutRect L2Rect = SubgraphRects.Single(Rect => Rect.GroupId == "L2");

        FlowchartLayoutNodeBounds NodeA = BoundsById["A"];
        FlowchartLayoutNodeBounds NodeB = BoundsById["B"];
        FlowchartLayoutNodeBounds NodeC = BoundsById["C"];
        FlowchartLayoutNodeBounds NodeD = BoundsById["D"];
        FlowchartLayoutNodeBounds NodeE = BoundsById["E"];

        Assert.That(NodeA.CenterY, Is.GreaterThan(NodeB.CenterY + 20.0));
        Assert.That(NodeB.CenterY, Is.GreaterThan(NodeC.CenterY + 20.0));
        Assert.That(NodeC.CenterY, Is.GreaterThan(NodeD.CenterY + 20.0));
        Assert.That(NodeD.CenterY, Is.GreaterThan(NodeE.CenterY + 20.0));

        double MinimumDirectMemberGap = FlowchartSubgraphGapThresholds.ResolveDirectMemberNestedGapPx(Options);
        Assert.That(
            NodeB.CenterY - (NodeB.Height / 2.0),
            Is.GreaterThanOrEqualTo(L1Rect.Top + MinimumDirectMemberGap - 1.0),
            "B must sit above the Level 1 cluster border");
        Assert.That(
            NodeE.CenterY + (NodeE.Height / 2.0),
            Is.LessThanOrEqualTo(L1Rect.Bottom - MinimumDirectMemberGap + 1.0),
            "E must sit below the Level 1 cluster border");

        double MinimumNestedFlowAxisGap = FlowchartSubgraphGapThresholds.ResolveNestedFlowAxisGapPx(Options);
        Assert.That(L1Rect.Top - L2Rect.Top, Is.GreaterThanOrEqualTo(MinimumNestedFlowAxisGap - 1.0));
        Assert.That(L2Rect.Bottom - L1Rect.Bottom, Is.GreaterThanOrEqualTo(MinimumNestedFlowAxisGap - 1.0));

        Assert.That(NodeC.CenterY + (NodeC.Height / 2.0), Is.LessThanOrEqualTo(L2Rect.Top + 1.0));
        Assert.That(NodeC.CenterY - (NodeC.Height / 2.0), Is.GreaterThanOrEqualTo(L2Rect.Bottom - 1.0));
        Assert.That(NodeD.CenterY + (NodeD.Height / 2.0), Is.LessThanOrEqualTo(L2Rect.Top + 1.0));
        Assert.That(NodeD.CenterY - (NodeD.Height / 2.0), Is.GreaterThanOrEqualTo(L2Rect.Bottom - 1.0));
    }

    [Test]
    public void RenderToSvg_F45_TwoLevelNesting_RendersAllClusterBoxes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph L0["Level 0"]
              direction TB
              A[A] --> B[B]
              subgraph L1["Level 1"]
                direction TB
                subgraph L2["Level 2"]
                  direction TB
                  C[C] --> D[D]
                end
              end
              B --> C
              D --> E[E]
            end
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("Level 0"));
        Assert.That(Svg, Does.Contain("Level 1"));
        Assert.That(Svg, Does.Contain("Level 2"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(Svg, "fill=\"#ffffde\"").Count, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void RenderToSvg_F89_HtmlLabel_RendersBoldAndItalic()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["<b>Bold</b> <i>Italic</i> <span style='color:red'>Red</span>"] --> B[Next]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("font-weight=\"bold\""));
        Assert.That(Svg, Does.Contain("font-style=\"italic\""));
        Assert.That(Svg, Does.Contain("fill=\"red\""));
        Assert.That(Svg, Does.Not.Contain("&lt;b&gt;"));
    }

    [Test]
    public void RenderToSvg_F89_NodeLabels_AreHorizontallyCentredInNodeBounds()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["<b>Bold</b> <i>Italic</i> <span style='color:red'>Red</span>"] --> B[Next]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        List<(double NodeCentreX, string LabelText)> NodeLabelPairs =
        [
            (ExtractRectangleCentreXFromSvg(Svg, 0), "Bold Italic Red"),
            (ExtractRectangleCentreXFromSvg(Svg, 1), "Next")
        ];

        Match RichLabelMatch = Regex.Match(
            Svg,
            @"<text[^>]*text-anchor=""start""[^>]*><tspan x=""([-\d.]+)""");
        Assert.That(RichLabelMatch.Success, Is.True, "HTML node label should be written as left-anchored tspans.");
        double RichLineWidthPx = FlowchartRichLabelSvgWriter.EstimateLineWidthPx(NodeLabelPairs[0].LabelText, 16.0);
        double RichLabelMidpointX = double.Parse(RichLabelMatch.Groups[1].Value, CultureInfo.InvariantCulture) + (RichLineWidthPx / 2.0);
        Assert.That(RichLabelMidpointX, Is.EqualTo(NodeLabelPairs[0].NodeCentreX).Within(1.5));

        Match PlainLabelMatch = Regex.Match(
            Svg,
            @"<text x=""([-\d.]+)""[^>]*text-anchor=""middle""[^>]*>Next</text>");
        Assert.That(PlainLabelMatch.Success, Is.True, "Plain node label should be a single middle-anchored text element.");
        double PlainLabelX = double.Parse(PlainLabelMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.That(PlainLabelX, Is.EqualTo(NodeLabelPairs[1].NodeCentreX).Within(1.5));
    }

    private static double ExtractRectangleCentreXFromSvg(string Svg, int RectangleIndex)
    {
        Match RectangleMatch = ExtractRectangleMatchFromSvg(Svg, RectangleIndex);
        double Left = double.Parse(RectangleMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double Right = double.Parse(RectangleMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        return (Left + Right) / 2.0;
    }

    private static double ExtractRectangleWidthFromSvg(string Svg, int RectangleIndex)
    {
        Match RectangleMatch = ExtractRectangleMatchFromSvg(Svg, RectangleIndex);
        double Left = double.Parse(RectangleMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double Right = double.Parse(RectangleMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        return Right - Left;
    }

    private static double ExtractRectangleHeightFromSvg(string Svg, int RectangleIndex)
    {
        Match RectangleMatch = ExtractRectangleMatchFromSvg(Svg, RectangleIndex);
        double Top = double.Parse(RectangleMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double Bottom = double.Parse(RectangleMatch.Groups[4].Value, CultureInfo.InvariantCulture);
        return Bottom - Top;
    }

    private static Match ExtractRectangleMatchFromSvg(string Svg, int RectangleIndex)
    {
        MatchCollection RectangleMatches = Regex.Matches(
            Svg,
            @"<path d=""M([-\d.]+),([-\d.]+) H([-\d.]+) V([-\d.]+)");
        Assert.That(RectangleMatches.Count, Is.GreaterThan(RectangleIndex));
        return RectangleMatches[RectangleIndex];
    }

    [Test]
    public void RenderToSvg_F91_MixedMarkdownHtmlLabel_IsHorizontallyCentredInNodeBounds()
    {
        const string MixedLabel = "**Bold** and <i>italic</i> and `code`";
        DiagramContract Contract = MermaidRecognizer.Parse($"""
            flowchart LR
            A["{MixedLabel}"] --> B[Next]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        double NodeCentreX = ExtractRectangleCentreXFromSvg(Svg, 0);
        Match LabelMatch = Regex.Match(Svg, @"<text[^>]*text-anchor=""start""[^>]*><tspan x=""([-\d.]+)""");
        Assert.That(LabelMatch.Success, Is.True);

        double LineWidthPx = FlowchartRichLabelSvgWriter.EstimateStyledLabelMaxWidthPx(MixedLabel, 16.0);
        double LabelMidpointX = double.Parse(LabelMatch.Groups[1].Value, CultureInfo.InvariantCulture)
            + (LineWidthPx / 2.0);
        double NodeWidth = ExtractRectangleWidthFromSvg(Svg, 0);
        Assert.That(LabelMidpointX, Is.EqualTo(NodeCentreX).Within(1.5));
        Assert.That(NodeWidth, Is.GreaterThanOrEqualTo(LineWidthPx));
    }

    [Test]
    public void RenderToSvg_F93_MarkdownListLabel_IsBlockCentredWithAlignedBullets()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["- Item 1<br/>- Item 2<br/>- Item 3"] --> B[Next]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        double NodeCentreX = ExtractRectangleCentreXFromSvg(Svg, 0);
        double NodeWidth = ExtractRectangleWidthFromSvg(Svg, 0);
        double NodeHeight = ExtractRectangleHeightFromSvg(Svg, 0);

        MatchCollection LineTspans = Regex.Matches(Svg, @"<tspan x=""([-\d.]+)""[^>]*>- Item");
        Assert.That(LineTspans, Has.Count.EqualTo(3));

        double BlockWidthPx = FlowchartRichLabelSvgWriter.EstimateStyledLabelMaxWidthPx(
            "- Item 1<br/>- Item 2<br/>- Item 3",
            16.0);
        double BlockStartX = double.Parse(LineTspans[0].Groups[1].Value, CultureInfo.InvariantCulture);
        double BlockMidpointX = BlockStartX + (BlockWidthPx / 2.0);

        Assert.That(BlockMidpointX, Is.EqualTo(NodeCentreX).Within(1.5));
        Assert.That(NodeWidth, Is.EqualTo(NodeHeight).Within(1.0));
    }

    [Test]
    public void TryLayout_F90_HtmlTableLabel_NodeIsRoughlySquare()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A["<table><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></table>"] --> B[Out]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds TableNode = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        double AspectRatio = TableNode.Width / TableNode.Height;
        Assert.That(AspectRatio, Is.InRange(0.55, 1.75));
        Assert.That(TableNode.Width, Is.LessThan(140.0));
    }

    [Test]
    public void RenderToSvg_F90_HtmlTableLabel_RendersNormalisedRows()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A["<table><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></table>"] --> B[Out]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain(">A  B</tspan>"));
        Assert.That(Svg, Does.Contain(">C  D</tspan>"));
        Assert.That(Svg, Does.Not.Contain("<table"));

        double TableNodeWidth = ExtractRectangleWidthFromSvg(Svg, 0);
        double TableNodeHeight = ExtractRectangleHeightFromSvg(Svg, 0);
        Assert.That(TableNodeWidth / TableNodeHeight, Is.InRange(0.55, 1.75));
    }

    [Test]
    public void RenderToSvg_F95_HtmlEdgeLabel_RendersBoldAndItalic()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A -->|"<b>Approve</b> or <i>Reject</i>"| B
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("font-weight=\"bold\""));
        Assert.That(Svg, Does.Contain("font-style=\"italic\""));
    }

    [Test]
    public void RenderToSvg_F95_ArrowTipInsetsIntoTargetNodeAndLineMeetsBoundary()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A -->|"<b>Approve</b> or <i>Reject</i>"| B
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        List<SvgStraightConnector> Connectors = SvgStraightConnectorReader.Read(Svg);
        Assert.That(Connectors, Has.Count.EqualTo(1), Svg);

        double LineEndX = Connectors[0].X2;

        Match ArrowMatch = Regex.Match(Svg, @"<polygon points=""([^""]+)""");
        Assert.That(ArrowMatch.Success, Is.True);

        string[] TipToken = ArrowMatch.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Split(',');
        double ArrowTipX = double.Parse(TipToken[0], CultureInfo.InvariantCulture);

        Match TargetNodeMatch = Regex.Matches(Svg, @"<path d=""M([\d.]+),([\d.]+) H([\d.]+)")
            .Cast<Match>()
            .OrderByDescending(Match => double.Parse(Match.Groups[1].Value, CultureInfo.InvariantCulture))
            .First();

        double TargetNodeLeftX = double.Parse(TargetNodeMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        Assert.That(ArrowTipX, Is.EqualTo(TargetNodeLeftX + 1.0).Within(0.5));
        Assert.That(LineEndX, Is.EqualTo(TargetNodeLeftX).Within(0.5));
    }

    private static void AssertEdgeSourceFace(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId,
        FlowchartEdgeAttachFace ExpectedSourceFace,
        FlowchartEdgeRouteKind ExpectedRouteKind)
    {
        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);

        bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out FlowchartEdgeRouteKind RouteKind);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");
        Assert.That(RouteKind, Is.EqualTo(ExpectedRouteKind), SourceId + "->" + TargetId + " route kind");

        FlowchartEdgeAttachFace SourceFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            BoundsById[SourceId],
            LayoutPoints[0].X,
            LayoutPoints[0].Y);
        Assert.That(SourceFace, Is.EqualTo(ExpectedSourceFace), SourceId + "->" + TargetId + " source port");
    }

    private static void AssertDecisionSourceAttachLeftOfHub(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId)
    {
        (double AttachX, _) = ResolveDecisionSourceAttach(Contract, Layout, BoundsById, NodeById, Direction, SourceId, TargetId);
        double HubCentreX = BoundsById[SourceId].CenterX;
        Assert.That(AttachX, Is.LessThan(HubCentreX - 4.0), SourceId + "->" + TargetId + " should exit the left diamond port");
    }

    private static void AssertDecisionSourceAttachRightOfHub(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId)
    {
        (double AttachX, _) = ResolveDecisionSourceAttach(Contract, Layout, BoundsById, NodeById, Direction, SourceId, TargetId);
        double HubCentreX = BoundsById[SourceId].CenterX;
        Assert.That(AttachX, Is.GreaterThan(HubCentreX + 4.0), SourceId + "->" + TargetId + " should exit the right diamond port");
    }

    private static void AssertDecisionSourceAttachBelowHub(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId)
    {
        (_, double AttachY) = ResolveDecisionSourceAttach(Contract, Layout, BoundsById, NodeById, Direction, SourceId, TargetId);
        double HubCentreY = BoundsById[SourceId].CenterY;
        Assert.That(AttachY, Is.LessThan(HubCentreY - 4.0), SourceId + "->" + TargetId + " should exit the bottom diamond port");
    }

    private static (double X, double Y) ResolveDecisionSourceAttach(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId)
    {
        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);

        bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out FlowchartEdgeRouteKind RouteKind,
            Layout);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");
        Assert.That(
            RouteKind,
            Is.EqualTo(FlowchartEdgeRouteKind.Decision).Or.EqualTo(FlowchartEdgeRouteKind.Straight),
            SourceId + "->" + TargetId + " route kind");

        return LayoutPoints[0];
    }

    private static void AssertHasMermaidBranchRoute(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string TargetId,
        bool ExpectStraight)
    {
        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == "HasMermaid" && Candidate.TargetId == TargetId);

        bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out FlowchartEdgeRouteKind RouteKind,
            Layout);

        Assert.That(Resolved, Is.True, "HasMermaid->" + TargetId + " should resolve");
        if (ExpectStraight)
        {
            Assert.That(RouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Straight), "HasMermaid->" + TargetId + " route kind");
        }
        else
        {
            Assert.That(
                RouteKind,
                Is.EqualTo(FlowchartEdgeRouteKind.Decision).Or.EqualTo(FlowchartEdgeRouteKind.Straight),
                "HasMermaid->" + TargetId + " route kind");
        }

        if (ExpectStraight)
        {
            Assert.That(LayoutPoints.Count, Is.EqualTo(2), "HasMermaid->" + TargetId + " should be a straight spine connector");
            Assert.That(
                Math.Abs(LayoutPoints[0].X - LayoutPoints[1].X),
                Is.LessThan(1.0),
                "HasMermaid->" + TargetId + " should stay on the spine column");

            FlowchartLayoutNodeBounds HubBounds = BoundsById["HasMermaid"];
            double HalfHeight = HubBounds.Height / 2.0;
            Assert.That(
                Math.Abs(LayoutPoints[0].Y - (HubBounds.CenterY - HalfHeight - 2.0)),
                Is.LessThan(1.0),
                "HasMermaid->" + TargetId + " layout attach should sit just outside the diamond bottom boundary");
        }
        else
        {
            Assert.That(LayoutPoints.Count, Is.GreaterThanOrEqualTo(2).And.LessThanOrEqualTo(3),
                "HasMermaid->" + TargetId + " should use a single-elbow orthogonal branch (B12)");
        }
    }

    private static void AssertCollinearDecisionBranch(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId)
    {
        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);

        bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out FlowchartEdgeRouteKind RouteKind,
            Layout);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");
        Assert.That(
            RouteKind,
            Is.EqualTo(FlowchartEdgeRouteKind.Straight).Or.EqualTo(FlowchartEdgeRouteKind.Decision),
            SourceId + "->" + TargetId + " collinear branch");
        Assert.That(LayoutPoints.Count, Is.LessThanOrEqualTo(3), SourceId + "->" + TargetId + " uses at most one elbow");
        FlowchartLayoutNodeBounds TargetBounds = BoundsById[TargetId];
        Assert.That(
            Math.Abs(TargetBounds.CenterX - BoundsById[SourceId].CenterX),
            Is.LessThan(4.0),
            SourceId + "->" + TargetId + " target shares the hub column");
    }

    private static void AssertEdgeTargetFace(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId,
        FlowchartEdgeAttachFace ExpectedTargetFace)
    {
        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);

        bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out _,
            Layout);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");

        FlowchartEdgeAttachFace TargetFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            BoundsById[TargetId],
            LayoutPoints[LayoutPoints.Count - 1].X,
            LayoutPoints[LayoutPoints.Count - 1].Y);
        Assert.That(TargetFace, Is.EqualTo(ExpectedTargetFace), SourceId + "->" + TargetId + " target port");
    }

    private static List<string> ExtractNodeFillPaths(string Svg)
    {
        List<string> FillPaths = [];
        MatchCollection PathMatches = Regex.Matches(
            Svg,
            @"<path d=""([^""]+)"" fill=""([^""]+)""",
            RegexOptions.CultureInvariant);

        foreach (Match Match in PathMatches)
        {
            if (Match.Groups.Count > 2
                && !string.Equals(Match.Groups[2].Value, "none", StringComparison.OrdinalIgnoreCase))
            {
                FillPaths.Add(Match.Groups[1].Value);
            }
        }

        return FillPaths;
    }

    private static bool TryReadConnectorEndX(string Svg, out double EndX)
    {
        EndX = 0.0;
        Match ButtLineMatch = Regex.Match(
            Svg,
            @"<line x1=""[^""]+"" y1=""[^""]+"" x2=""([^""]+)""[^>]*stroke-linecap=""butt""",
            RegexOptions.CultureInvariant);
        if (ButtLineMatch.Success)
        {
            return double.TryParse(ButtLineMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out EndX);
        }

        Match EdgePathMatch = Regex.Match(
            Svg,
            @"<path d=""([^""]+)"" fill=""none"" stroke=""",
            RegexOptions.CultureInvariant);
        if (!EdgePathMatch.Success)
        {
            return false;
        }

        MatchCollection SegmentMatches = Regex.Matches(
            EdgePathMatch.Groups[1].Value,
            @"L([\d.]+),",
            RegexOptions.CultureInvariant);
        if (SegmentMatches.Count == 0)
        {
            return false;
        }

        return double.TryParse(
            SegmentMatches[SegmentMatches.Count - 1].Groups[1].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out EndX);
    }
}
