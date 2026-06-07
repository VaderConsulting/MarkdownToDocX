using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// R1 native render gate: Sugiyama SVG on chain, decision fan-out, and subgraph membership fixtures.
/// </summary>
[TestFixture]
public sealed class FlowchartR1RenderGateTests
{
    [Test]
    public void RenderToSvg_Chain_Fixture_ContainsThreeNodesAndTwoEdges()
    {
        string MmdPath = Path.Combine(FindFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.StartWith("<svg"));
        Assert.That(Svg, Does.Contain("fill=\"#ECECFF\""));
        Assert.That(Svg, Does.Contain("stroke=\"#9370DB\""));
        Assert.That(CountMatches(Svg, "<rect"), Is.EqualTo(1));
        Assert.That(CountMatches(Svg, "<path"), Is.EqualTo(3));
        Assert.That(CountMatches(Svg, "<line"), Is.EqualTo(2));
        Assert.That(CountMatches(Svg, "<text"), Is.EqualTo(3));
    }

    [Test]
    public void RenderToSvg_F11BasicShapes_RendersDistinctShapePaths()
    {
        string Source = """
            flowchart TD
            A[Rectangle]
            B(Rounded rectangle)
            C{Diamond decision}
            D((Circle))
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Contract.Nodes.First(Node => Node.Id == "B").Shape, Is.EqualTo("rounded"));
        Assert.That(Svg, Does.Contain("Q"));
        Assert.That(Svg, Does.Contain("A63.6,63.6"));
        Assert.That(CountMatches(Svg, "<path"), Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void RenderToSvg_F19DottedEdge_UsesStrokeDasharray()
    {
        string Source = """
            flowchart LR
            A[A] -.-> B[B]
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Contract.Edges[0].Stroke, Is.EqualTo("dotted"));
        Assert.That(Svg, Does.Contain("stroke-dasharray=\"3 3\""));
    }

    [Test]
    public void RenderToSvg_F54ClassDef_UsesAuthorStrokeColours()
    {
        string Source = """
            flowchart LR
            A[Good]:::good --> B[Bad]:::bad
            classDef good fill:#d4edda,stroke:#28a745
            classDef bad fill:#f8d7da,stroke:#dc3545
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("stroke=\"#28a745\""));
        Assert.That(Svg, Does.Contain("stroke=\"#dc3545\""));
    }

    [Test]
    public void RenderToSvg_F55LinkStyle_UsesAuthorEdgeColours()
    {
        string Source = """
            flowchart LR
            A[A] --> B[B]
            B --> C[C]
            C --> D[D]
            linkStyle 0 stroke:#ff0000,stroke-width:2px
            linkStyle 2 stroke:#0000ff,stroke-dasharray:5 5
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("stroke=\"#ff0000\""));
        Assert.That(Svg, Does.Contain("stroke=\"#0000ff\""));
        Assert.That(Svg, Does.Contain("stroke-dasharray=\"5 5\""));
    }

    [Test]
    public void RenderToSvg_F12Stadium_UsesArcInPath()
    {
        string Source = """
            flowchart LR
            A([Stadium / pill])
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Contract.Nodes[0].Shape, Is.EqualTo("stadium"));
        Assert.That(Svg, Does.Match(@"<path[^>]+d=""[^""]*\sA[^""]*"""));
    }

    [Test]
    public void RenderToSvg_Decision_FanOut_ContainsDiamondAndBranches()
    {
        string Source = """
            flowchart TD
                Choose{Choose}
                Choose -->|Yes| Approve[Approved]
                Choose -->|No| Reject[Rejected]
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("<path"));
        Assert.That(Regex.Matches(Svg, @"<path d=""M[^""]+L[^""]+"" fill=""none""").Count, Is.EqualTo(2));
        Assert.That(CountMatches(Svg, "<text"), Is.EqualTo(5));
    }

    [Test]
    public void RenderToSvg_SubgraphMembership_Fixture_LaysOutInnerNodes()
    {
        string MmdPath = Path.Combine(FindFlowchartFixtureDirectory(), "subgraph-membership.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        Assert.That(Layout.NodeBounds.Count, Is.EqualTo(Contract.Nodes.Count));

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.StartWith("<svg"));
        Assert.That(CountMatches(Svg, "<line"), Is.EqualTo(1));
    }

    [Test]
    public void TryResolveEdgeRoute_F53A_TerminalCollectorRoutingApplies()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Success --> End([End])
            Error --> End
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        NodeContract EndNode = Contract.Nodes.Single(Node => Node.Id == "End");
        Assert.That(EndNode.Shape, Is.EqualTo("stadium"));
        Assert.That(
            FlowchartTerminalCollectorPortAllocator.ShouldApplyTerminalCollectorRouting(Contract, "End"),
            Is.True);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        EdgeContract SuccessEdge = Contract.Edges.Single(
            Edge => Edge.SourceId == "Success" && Edge.TargetId == "End");
        bool Resolved = FlowchartTerminalCollectorPortAllocator.TryResolveEdgeRoute(
            SuccessEdge,
            Contract,
            BoundsById,
            "rect",
            EndNode.Shape,
            FlowchartDirection.TopToBottom,
            out List<(double X, double Y)> Points);

        Assert.That(Resolved, Is.True);
        string PointTrace = string.Join(
            " | ",
            Points.Select(Point => $"({Point.X:F2},{Point.Y:F2})"));
        Assert.That(
            Math.Abs(Points[0].X - BoundsById["Success"].CenterX),
            Is.LessThan(1.0),
            $"Success centreX={BoundsById["Success"].CenterX:F2}; points={PointTrace}");
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F53A_StadiumMerge_UsesCentreSourceSlots()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Success --> End([End])
            Error --> End

            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartLayoutNodeBounds EndBounds = BoundsById["End"];

        foreach (string SourceId in new[] { "Success", "Error" })
        {
            EdgeContract Edge = Contract.Edges.Single(
                Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == "End");
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> Points,
                out _);

            Assert.That(Resolved, Is.True, SourceId);
            Assert.That(Points.Count, Is.GreaterThanOrEqualTo(2), SourceId);

            double SourceCentreX = BoundsById[SourceId].CenterX;
            Assert.That(
                Math.Abs(Points[0].X - SourceCentreX),
                Is.LessThan(1.0),
                $"{SourceId} should exit from the centre bottom slot.");
            Assert.That(
                Math.Abs(Points[0].X - Points[1].X),
                Is.LessThan(1.0),
                $"{SourceId} should keep a vertical stub at source centre X.");

            (double TargetX, double TargetY) = Points[Points.Count - 1];
            Assert.That(
                Math.Abs(TargetX - EndBounds.CenterX),
                Is.GreaterThan(4.0),
                $"{SourceId} should land on the stadium shoulder, not the top-centre slot.");
            Assert.That(
                TargetY,
                Is.LessThan(EndBounds.CenterY),
                $"{SourceId} should attach on the upper stadium cap.");
        }
    }

    [Test]
    public void TryLayout_F83_AmpersandFanIn_FirstDeclaredSourceAboveSecond()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A & B e1@--> C
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds NodeA = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "A");
        FlowchartLayoutNodeBounds NodeB = Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "B");

        Assert.That(
            NodeA.CenterY,
            Is.GreaterThan(NodeB.CenterY),
            "A is listed first and should sit above B in LR fan-in stacks.");
        Assert.That(NodeA.CenterX, Is.LessThan(Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "C").CenterX));
        Assert.That(NodeB.CenterX, Is.LessThan(Layout.NodeBounds.Single(Bounds => Bounds.NodeId == "C").CenterX));
    }

    [Test]
    public void RenderToSvg_F53A_StadiumMerge_SvgPathStartsAtCentreSourceSlot()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Success --> End([End])
            Error --> End

            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        foreach (string SourceId in new[] { "Success", "Error" })
        {
            EdgeContract Edge = Contract.Edges.Single(
                Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == "End");
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out _);

            Assert.That(Resolved, Is.True, SourceId);
            Assert.That(
                FlowchartCurvePathBuilder.IsStadiumMergeGuideRoute(LayoutPoints),
                Is.True,
                $"{SourceId} should use the stadium merge hook route.");
        }

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        List<string> EdgePathData = ExtractRenderedEdgePathData(Svg);
        Assert.That(EdgePathData.Count, Is.EqualTo(2));

        AssertSvgEdgeStartsAtNodeCentre(Svg, EdgePathData[0], "#d1ecf1", "Success");
        AssertSvgEdgeStartsAtNodeCentre(Svg, EdgePathData[1], "#f8d7da", "Error");
    }

    [Test]
    public void RenderToSvg_F53StyleDirective_UsesAuthorFillColours()
    {
        string Source = """
            flowchart TD
            Start([Start]) --> End([End])
            style Start fill:#e1f5e1
            style End fill:#f8d7da
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("fill=\"#e1f5e1\""));
        Assert.That(Svg, Does.Contain("fill=\"#f8d7da\""));
    }

    [Test]
    public void RenderToSvg_SequenceContract_ReturnsEmpty()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("sequenceDiagram\n    A->>B: hi");
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Is.EqualTo(string.Empty));
    }

    [Test]
    public void RenderToSvg_EmptyFlowchart_ReturnsEmpty()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TD");
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Is.EqualTo(string.Empty));
    }

    [Test]
    public void RenderToSvg_F10_QuotedLabels_NodeWidthFitsLabelText()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["Label with spaces"] --> B["Second label"]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        FlowchartNodeDimensions NodeA = FlowchartNodeSizeEstimator.Estimate(
            Contract.Nodes.First(Node => Node.Id == "A"),
            FlowchartRenderOptions.FromContract(Contract));

        Match NodePath = Regex.Match(Svg, @"<path d=""M([\d\.]+),([\d\.]+) H([\d\.]+)");
        Assert.That(NodePath.Success, Is.True);

        double Left = double.Parse(NodePath.Groups[1].Value, CultureInfo.InvariantCulture);
        double Right = double.Parse(NodePath.Groups[3].Value, CultureInfo.InvariantCulture);
        double RenderedWidth = Right - Left;

        Assert.That(RenderedWidth, Is.GreaterThanOrEqualTo(NodeA.Width * 0.9));
        Assert.That(Svg, Does.Contain("Label with spaces"));
        Assert.That(Svg, Does.Contain("Second label"));
    }

    [Test]
    public void RenderToSvg_Chain_ArrowTipsMeetTargetNodeBorders()
    {
        string MmdPath = Path.Combine(FindFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        List<double> NodeTops = new List<double>();
        foreach (Match NodeMatch in Regex.Matches(Svg, @"<path d=""M([\d\.]+),([\d\.]+)"))
        {
            NodeTops.Add(double.Parse(NodeMatch.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        NodeTops.Sort();

        List<double> ArrowTipYs = new List<double>();
        foreach (Match ArrowMatch in Regex.Matches(Svg, @"<polygon points=""([\d\.]+),([\d\.]+)"))
        {
            ArrowTipYs.Add(double.Parse(ArrowMatch.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        ArrowTipYs.Sort();

        Assert.That(NodeTops, Has.Count.EqualTo(3));
        Assert.That(ArrowTipYs, Has.Count.EqualTo(2));
        Assert.That(ArrowTipYs[0], Is.EqualTo(NodeTops[1] + 1.0).Within(0.5));
        Assert.That(ArrowTipYs[1], Is.EqualTo(NodeTops[2] + 1.0).Within(0.5));
    }

    private static int CountMatches(string Text, string Pattern)
    {
        return Regex.Matches(Text, Regex.Escape(Pattern), RegexOptions.IgnoreCase).Count;
    }

    private static string FindFlowchartFixtureDirectory()
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, "FlowchartRecognition");
            if (Directory.Exists(Candidate))
            {
                return Candidate;
            }

            Current = Current.Parent;
        }

        throw new InvalidOperationException("Could not locate FlowchartRecognition fixtures.");
    }

    private static List<string> ExtractRenderedEdgePathData(string Svg)
    {
        List<string> PathData = new List<string>();
        MatchCollection Matches = Regex.Matches(
            Svg,
            @"<path d=""([^""]+)"" fill=""none"" stroke=""",
            RegexOptions.CultureInvariant);

        foreach (Match Match in Matches)
        {
            PathData.Add(Match.Groups[1].Value);
        }

        return PathData;
    }

    private static void AssertSvgEdgeStartsAtNodeCentre(
        string Svg,
        string EdgePathData,
        string NodeFillColour,
        string NodeLabel)
    {
        Match NodeShapeMatch = Regex.Match(
            Svg,
            $@"<path d=""M([\d\.-]+),([\d\.-]+) H([\d\.-]+)[^""]*"" fill=""{Regex.Escape(NodeFillColour)}""",
            RegexOptions.CultureInvariant);
        Assert.That(NodeShapeMatch.Success, Is.True, $"Expected styled node shape for {NodeLabel}.");

        double NodeLeft = double.Parse(NodeShapeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double NodeRight = double.Parse(NodeShapeMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        double NodeCentreX = (NodeLeft + NodeRight) / 2.0;
        double HalfWidth = (NodeRight - NodeLeft) / 2.0;
        double SpreadLeftX = NodeCentreX - (HalfWidth / 2.0);
        double SpreadRightX = NodeCentreX + (HalfWidth / 2.0);

        Match EdgeStartMatch = Regex.Match(
            EdgePathData,
            @"^M\s*([\d\.-]+)\s*,\s*([\d\.-]+)",
            RegexOptions.CultureInvariant);
        Assert.That(EdgeStartMatch.Success, Is.True, $"Expected edge path start for {NodeLabel}.");

        double EdgeStartX = double.Parse(EdgeStartMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double CentreTolerance = Math.Max(4.0, HalfWidth * 0.12);

        Assert.That(
            Math.Abs(EdgeStartX - NodeCentreX),
            Is.LessThan(CentreTolerance),
            $"{NodeLabel} SVG path should start at the centre bottom slot.");
        Assert.That(
            Math.Abs(EdgeStartX - SpreadLeftX),
            Is.GreaterThan(CentreTolerance),
            $"{NodeLabel} should not use the left spread fan-out slot.");
        Assert.That(
            Math.Abs(EdgeStartX - SpreadRightX),
            Is.GreaterThan(CentreTolerance),
            $"{NodeLabel} should not use the right spread fan-out slot.");
    }
}
