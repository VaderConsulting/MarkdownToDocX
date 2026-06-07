using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class FlowchartRenderOptionsTests
{
    [Test]
    public void FromContract_DefaultTheme_UsesPinnedNodeAndEdgeColours()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A --> B
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartResolvedStyle NodeStyle = Options.ResolveNodeStyle(Contract.Nodes[0]);
        FlowchartResolvedStyle EdgeStyle = Options.ResolveEdgeStyle(Contract.Edges[0]);

        Assert.That(NodeStyle.Fill, Is.EqualTo("#ECECFF"));
        Assert.That(NodeStyle.Stroke, Is.EqualTo("#9370DB"));
        Assert.That(NodeStyle.TextColor, Is.EqualTo("#333"));
        Assert.That(EdgeStyle.Stroke, Is.EqualTo("#333333"));
    }

    [Test]
    public void FromContract_DefaultLayout_UsesPinnedSpacingAndTypography()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A --> B
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);

        Assert.That(Options.NodeSpacingPx, Is.EqualTo(50.0));
        Assert.That(Options.RankSpacingPx, Is.EqualTo(50.0));
        Assert.That(Options.DiagramPaddingPx, Is.EqualTo(8.0));
        Assert.That(Options.LabelPaddingPx, Is.EqualTo(15.0));
        Assert.That(Options.WrappingWidthPx, Is.EqualTo(200.0));
        Assert.That(Options.FontSizePx, Is.EqualTo(16.0));
        Assert.That(Options.FontWeight, Is.EqualTo("normal"));
        Assert.That(Options.HtmlLabels, Is.True);
        Assert.That(Options.FontFamily, Does.Contain("trebuchet ms"));
    }

    [Test]
    public void FromContract_InitDirective_AppliesFlowchartSpacingOverrides()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            %%{init: {"flowchart": {"nodeSpacing": 80, "rankSpacing": 70, "diagramPadding": 12}}}%%
            flowchart TD
            A --> B
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);

        Assert.That(Options.NodeSpacingPx, Is.EqualTo(80.0));
        Assert.That(Options.RankSpacingPx, Is.EqualTo(70.0));
        Assert.That(Options.DiagramPaddingPx, Is.EqualTo(12.0));
    }

    [Test]
    public void ResolveNodeStyle_StyleDirective_AppliesFillOverride()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> End([End])
            style Start fill:#e1f5e1
            style End fill:#f8d7da
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract StartNode = Contract.Nodes.First(Node => Node.Id == "Start");
        NodeContract EndNode = Contract.Nodes.First(Node => Node.Id == "End");

        Assert.That(Options.ResolveNodeStyle(StartNode).Fill, Is.EqualTo("#e1f5e1"));
        Assert.That(Options.ResolveNodeStyle(EndNode).Fill, Is.EqualTo("#f8d7da"));
    }

    [Test]
    public void ResolveEdgeStyle_LinkStyle_AppliesStrokeAndDasharray()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A --> B --> C --> D
            linkStyle 0 stroke:#ff0000,stroke-width:2px
            linkStyle 2 stroke:#0000ff,stroke-dasharray:5 5
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        EdgeContract Edge0 = Contract.Edges.First(Edge => Edge.CreationIndex == 0);
        EdgeContract Edge2 = Contract.Edges.First(Edge => Edge.CreationIndex == 2);

        FlowchartResolvedStyle Style0 = Options.ResolveEdgeStyle(Edge0);
        FlowchartResolvedStyle Style2 = Options.ResolveEdgeStyle(Edge2);

        Assert.That(Style0.Stroke, Is.EqualTo("#ff0000"));
        Assert.That(Style0.StrokeWidth, Is.EqualTo(2.0));
        Assert.That(Style2.Stroke, Is.EqualTo("#0000ff"));
        Assert.That(Style2.StrokeDasharray, Is.EqualTo("5 5"));
    }

    [Test]
    public void ResolveNodeStyle_ClassDef_AppliesClassColours()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Good]:::good --> B[Bad]:::bad
            classDef good fill:#d4edda,stroke:#28a745
            classDef bad fill:#f8d7da,stroke:#dc3545
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract GoodNode = Contract.Nodes.First(Node => Node.Id == "A");
        NodeContract BadNode = Contract.Nodes.First(Node => Node.Id == "B");

        Assert.That(Options.ResolveNodeStyle(GoodNode).Fill, Is.EqualTo("#d4edda"));
        Assert.That(Options.ResolveNodeStyle(GoodNode).Stroke, Is.EqualTo("#28a745"));
        Assert.That(Options.ResolveNodeStyle(BadNode).Fill, Is.EqualTo("#f8d7da"));
        Assert.That(Options.ResolveNodeStyle(BadNode).Stroke, Is.EqualTo("#dc3545"));
    }
}
