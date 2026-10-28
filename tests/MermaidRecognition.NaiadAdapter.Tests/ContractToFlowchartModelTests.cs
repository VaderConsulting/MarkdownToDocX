using MermaidRecognition.Core;

using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Models;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class ContractToFlowchartModelTests
{
    [Test]
    public void Map_SimpleFlowchart_ProducesNodesAndEdges()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TD\n    A --> B");
        FlowchartModel Model = ContractToFlowchartModel.Map(Contract);

        Assert.That(Model.Nodes, Has.Count.EqualTo(2));
        Assert.That(Model.Edges, Has.Count.EqualTo(1));
        Assert.That(Model.Edges[0].SourceId, Is.EqualTo("A"));
        Assert.That(Model.Edges[0].TargetId, Is.EqualTo("B"));
    }

    [Test]
    public void Map_SubgraphFixture_ProducesGroupNodes()
    {
        string Source = """
            flowchart TD
                subgraph G1
                    A --> B
                end
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        FlowchartModel Model = ContractToFlowchartModel.Map(Contract);

        Assert.That(Model.Subgraphs, Is.Not.Empty);
        Node? GroupNode = Model.GetNode("G1");
        Assert.That(GroupNode, Is.Not.Null);
        Assert.That(GroupNode!.IsGroup, Is.True);
    }

    [Test]
    public void Map_NonFlowchart_Throws()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("sequenceDiagram\n    A->>B: Hi");
        Assert.Throws<InvalidOperationException>(() => ContractToFlowchartModel.Map(Contract));
    }
}

[TestFixture]
public sealed class FlowchartRecognitionRenderBridgeTests
{
    [Test]
    public void TryRenderFlowchartSvg_SimpleFlowchart_ReturnsSvg()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            "flowchart TD\n    A --> B",
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("<svg"));
        Assert.That(Svg, Does.Contain("A"));
    }

    [Test]
    public void TryRenderFlowchartSvg_Sequence_ReturnsFalse()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            "sequenceDiagram\n    A->>B: Hi",
            RenderOptions.Default,
            out string _);

        Assert.That(Rendered, Is.False);
    }

    [Test]
    public void TryRenderFlowchartSvg_QuotedBracketLabels_OmitDelimiterQuotesInSvg()
    {
        string Source = """
            flowchart LR
            A["Label with spaces"] --> B["Second label"]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("Label with spaces"));
        Assert.That(Svg, Does.Contain("Second label"));
        Assert.That(Svg, Does.Not.Contain("&quot;Label with spaces&quot;"));
        Assert.That(Svg, Does.Not.Contain("&quot;Second label&quot;"));
    }

    [Test]
    public void TryRenderFlowchartSvg_HexagonBracket_RendersHexagonPath()
    {
        string Source = """
            flowchart LR
            A{{Hexagon}} --> B[Next]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("Hexagon"));
        Assert.That(Svg, Does.Not.Contain("diamond"));
    }

    [Test]
    public void TryRenderFlowchartSvg_AsymmetricBracket_RendersFlagPath()
    {
        string Source = """
            flowchart LR
            A>Asymmetric flag] --> B[Target]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("Asymmetric flag"));
        Assert.That(Svg, Does.Contain("M0,35L-35,0"));
        Assert.That(Svg, Does.Contain("<line x1=\"-35\" y1=\"70\" x2=\"0\" y2=\"35\""));
    }

    [Test]
    public void TryRenderFlowchartSvg_DoubleCircleBracket_RendersDoubleRingPath()
    {
        string Source = """
            flowchart TD
            Start(((Start))) --> Stop(((Stop)))
            """;

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "Start").Shape, Is.EqualTo("doublecircle"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "Stop").Shape, Is.EqualTo("doublecircle"));

        FlowchartModel Model = ContractToFlowchartModel.Map(Contract);
        Assert.That(Model.GetNode("Start")!.Shape, Is.EqualTo(NodeShape.DoubleCircle));
        Assert.That(Model.GetNode("Stop")!.Shape, Is.EqualTo(NodeShape.DoubleCircle));

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain(" Z M"));
    }

    [Test]
    public void TryRenderFlowchartSvg_OpenSolidLine_OmitsArrowMarkerOnOpenEdge()
    {
        string Source = """
            flowchart LR
            A[A] --> B[B]
            C[C] --- D[D]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Edge OpenEdge = Model.Edges.Single(Edge => Edge.SourceId == "C");
        Edge ArrowEdge = Model.Edges.Single(Edge => Edge.SourceId == "A");

        Assert.That(OpenEdge.Type, Is.EqualTo(EdgeType.Open));
        Assert.That(ArrowEdge.Type, Is.EqualTo(EdgeType.Arrow));
        Assert.That(Svg, Does.Not.Match("C\\[C\\][\\s\\S]*marker-end=\"url\\(#mermaid-svg_flowchart-v2-pointEnd\\)\""));
    }

    [Test]
    public void TryRenderFlowchartSvg_DottedOpenLine_RendersDottedEdgeWithoutArrow()
    {
        string Source = """
            flowchart LR
            A[A] -.-> B[B]
            C[C] -.- D[D]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Edge OpenEdge = Model.Edges.Single(Edge => Edge.SourceId == "C");
        Edge ArrowEdge = Model.Edges.Single(Edge => Edge.SourceId == "A");

        Assert.That(OpenEdge.Type, Is.EqualTo(EdgeType.Dotted));
        Assert.That(OpenEdge.LineStyle, Is.EqualTo(EdgeStyle.Dotted));
        Assert.That(ArrowEdge.Type, Is.EqualTo(EdgeType.DottedArrow));
        Assert.That(Svg, Does.Contain("edge-pattern-dotted"));
    }

    [Test]
    public void TryRenderFlowchartSvg_F23EdgeStyles_MapAndRenderCorrectly()
    {
        string Source = """
            flowchart LR
            A1[A] --> B1[B]
            C1[C] --- D1[D]
            E1[E] -.-> F1[F]
            G1[G] -.- H1[H]
            I1[I] ==> J1[J]
            K1[K] === L1[L]
            M1[M] --o N1[N]
            O1[O] --x P1[P]
            Q1[Q] <--> R1[R]
            """;

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));

        Assert.That(Model.Edges.Single(Edge => Edge.SourceId == "G1").Type, Is.EqualTo(EdgeType.Dotted));
        Assert.That(Model.Edges.Single(Edge => Edge.SourceId == "I1").Type, Is.EqualTo(EdgeType.ThickArrow));
        Assert.That(Model.Edges.Single(Edge => Edge.SourceId == "K1").Type, Is.EqualTo(EdgeType.Thick));

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("edge-pattern-dotted"));

        List<string> LinkPaths = [.. System.Text.RegularExpressions.Regex.Matches(
                Svg,
                "<path[^>]*class=\"[^\"]*flowchart-link[^\"]*\"[^>]*>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(Match => Match.Value)];

        Assert.That(LinkPaths, Has.Count.GreaterThanOrEqualTo(6));
        Assert.That(LinkPaths[3], Does.Contain("stroke-dasharray"));
        Assert.That(LinkPaths[3], Does.Not.Contain("marker-end"));
        Assert.That(LinkPaths[4], Does.Contain("stroke-width=\"3.5\""));
        Assert.That(LinkPaths[4], Does.Contain("marker-end"));
        Assert.That(LinkPaths[5], Does.Contain("stroke-width=\"3.5\""));
        Assert.That(LinkPaths[5], Does.Not.Contain("marker-end"));
    }

    [Test]
    public void TryRenderFlowchartSvg_DoubleCircleAndCrossLinks_RenderStartMarkers()
    {
        string Source = """
            flowchart LR
            A[A] <--> B[B]
            C[C] o--o D[D]
            E[E] x--x F[F]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Edge CircleEdge = Model.Edges.Single(Edge => Edge.SourceId == "C");
        Edge CrossEdge = Model.Edges.Single(Edge => Edge.SourceId == "E");

        Assert.That(CircleEdge.Type, Is.EqualTo(EdgeType.BiDirectionalCircle));
        Assert.That(CrossEdge.Type, Is.EqualTo(EdgeType.BiDirectionalCross));
        Assert.That(Svg, Does.Contain("marker-start=\"url(#mermaid-svg_flowchart-v2-circleStart"));
        Assert.That(Svg, Does.Contain("marker-start=\"url(#mermaid-svg_flowchart-v2-crossStart"));
    }

    [Test]
    public void TryRenderFlowchartSvg_F26LongQuotedEdgeLabel_RendersWithoutQuoteCharacters()
    {
        string Source = """
            flowchart LR
            A[Producer] -->|"This edge label is intentionally long so wrapping, clearance from arrowheads, and node borders can be verified in Word."| B[Consumer]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("intentionally long"));
        Assert.That(Svg, Does.Not.Contain("&quot;"));
        Assert.That(Svg, Does.Not.Contain("\"This edge"));

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Edge Edge = Model.Edges.Single();
        Assert.That(Edge.Label, Does.Not.Contain("\""));
    }

    [Test]
    public void TryRenderFlowchartSvg_F36Subgraph_RendersClusterBoxAndMemberLayout()
    {
        string Source = """
            flowchart TB
            subgraph SG["Processing"]
              A[Parse] --> B[Transform] --> C[Emit]
            end
            IN[In] --> A
            C --> OUT[Out]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("fill=\"#ffffde\""));
        Assert.That(Svg, Does.Contain("Processing"));

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Assert.That(Model.Subgraphs, Has.Count.EqualTo(1));
        Node? NodeA = Model.GetNode("A");
        Assert.That(NodeA, Is.Not.Null);
        Assert.That(NodeA!.ParentId, Is.EqualTo("SG"));
    }

    [Test]
    public void TryRenderFlowchartSvg_F48EntityCodes_RendersDecodedCharacters()
    {
        string Source = """
            flowchart TB
            A["Symbols: star#42; underscore#95; backtick#96; brackets#91;#93; pipe A#124;B"] --> B["Quotes: double#quot; and apostrophe#39;s"]
            B --> C["Comparison: a#lt; b #38;#38; c#gt; d"]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("double&quot;"));
        Assert.That(Svg, Does.Not.Contain("#quot;"));
        Assert.That(Svg, Does.Not.Contain("#lt;"));

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Node NodeB = Model.Nodes.Single(Node => Node.Id == "B");
        Assert.That(NodeB.Label, Is.EqualTo("Quotes: double\" and apostrophe's"));
    }

    [Test]
    public void TryRenderFlowchartSvg_ThickOpenLine_RendersThickEdgeWithoutArrow()
    {
        string Source = """
            flowchart LR
            A[A] ==> B[B]
            C[C] === D[D]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Edge OpenEdge = Model.Edges.Single(Edge => Edge.SourceId == "C");
        Edge ArrowEdge = Model.Edges.Single(Edge => Edge.SourceId == "A");

        Assert.That(OpenEdge.Type, Is.EqualTo(EdgeType.Thick));
        Assert.That(OpenEdge.LineStyle, Is.EqualTo(EdgeStyle.Thick));
        Assert.That(ArrowEdge.Type, Is.EqualTo(EdgeType.ThickArrow));

        string OpenEdgePath = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"<path d=""M127\.2,155[^""]+""[^/]*/>")
            .Value;
        Assert.That(OpenEdgePath, Does.Not.Contain("marker-end"));
        Assert.That(OpenEdgePath, Does.Contain("stroke-width=\"3.5\""));
    }

    [Test]
    public void Map_F53StyleDirective_PopulatesStyleByElementId()
    {
        string Source = """
            flowchart TD
            Start([Start]) --> Process[Process]
            Process --> Decision{Valid?}
            Decision -->|Yes| Success[Success]
            Decision -->|No| Error[Error]
            Success --> End([End])
            Error --> End

            style Start fill:#e1f5e1
            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """;

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));

        Assert.That(Model.StyleByElementId["Start"].Fill, Is.EqualTo("#e1f5e1"));
        Assert.That(Model.StyleByElementId["End"].Fill, Is.EqualTo("#e1f5e1"));
        Assert.That(Model.StyleByElementId["Error"].Fill, Is.EqualTo("#f8d7da"));
        Assert.That(Model.StyleByElementId["Success"].Fill, Is.EqualTo("#d1ecf1"));
    }

    [Test]
    public void TryRenderFlowchartSvg_F53StyleDirective_RendersCustomFills()
    {
        string Source = """
            flowchart TD
            Start([Start]) --> Process[Process]
            Process --> Decision{Valid?}
            Decision -->|Yes| Success[Success]
            Decision -->|No| Error[Error]
            Success --> End([End])
            Error --> End

            style Start fill:#e1f5e1
            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("fill=\"#e1f5e1\""));
        Assert.That(Svg, Does.Contain("fill=\"#f8d7da\""));
        Assert.That(Svg, Does.Contain("fill=\"#d1ecf1\""));
    }

    [Test]
    public void TryRenderFlowchartSvg_F56QuotedNodeLabels_RendersWithoutDelimiterQuotes()
    {
        string Source = """
            %%{init: {"flowchart": {"htmlLabels": false}} }%%
            flowchart TB
            A["Native SVG labels"] --> B["Wrap without HTML foreignObject."]
            B --> C["Final"]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("Native SVG labels"));
        Assert.That(Svg, Does.Not.Match(@"<tspan[^>]*>[^<]*""[^<]*</tspan>"));

        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(Source));
        Node NodeA = Model.Nodes.Single(Node => Node.Id == "A");
        Assert.That(NodeA.Label, Is.EqualTo("Native SVG labels"));
        Assert.That(NodeA.Label, Does.Not.Contain("\""));
    }

    [Test]
    public void TryRenderFlowchartSvg_F53AStyleDirective_RendersCustomFills()
    {
        string Source = """
            flowchart TD
            Success --> End([End])
            Error --> End

            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("fill=\"#e1f5e1\""));
        Assert.That(Svg, Does.Contain("fill=\"#f8d7da\""));
        Assert.That(Svg, Does.Contain("fill=\"#d1ecf1\""));
    }
}

[TestFixture]
public sealed class FlowchartRecognitionParityTests
{
    [TestCase("flowchart TD\n    A --> B")]
    [TestCase("flowchart LR\n    A[Start] --> B{Choice}")]
    [TestCase("flowchart TD\n    subgraph G\n        A --> B\n    end")]
    public void RecognitionPath_NodeAndEdgeCounts_MatchLegacyParser(string Source)
    {
        FlowchartParser LegacyParser = new FlowchartParser();
        Pidgin.Result<char, FlowchartModel> LegacyResult = LegacyParser.Parse(Source);
        Assert.That(LegacyResult.Success, Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        FlowchartModel RecognitionModel = ContractToFlowchartModel.Map(Contract);

        Assert.That(
            RecognitionModel.Nodes.Count,
            Is.EqualTo(LegacyResult.Value!.Nodes.Count),
            "Node count parity");
        Assert.That(
            RecognitionModel.Edges.Count,
            Is.EqualTo(LegacyResult.Value.Edges.Count),
            "Edge count parity");
    }
}
