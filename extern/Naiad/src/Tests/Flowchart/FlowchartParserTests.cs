using MermaidSharp.Models;
using MermaidSharp.Diagrams.Flowchart;

public class FlowchartParserTests
{
    [Test]
    public void Simple_ReturnsNodes()
    {
        const string input =
            """
            flowchart LR
                A[Start] --> B[End]
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Nodes.Count, Is.EqualTo(2));
        Assert.That(result.Value.Edges.Count, Is.EqualTo(1));
    }

    [Test]
    public void QuotedRectangle_StripsDelimiterQuotes()
    {
        const string input =
            """
            flowchart TD
                A["Markdown"] --> B["Detect Mermaid fence"]
                B --> C[Render SVG]
                B --> D["Render normal Markdown"]
                C --> E["Embed in DOCX"]
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        var nodeById = result.Value.Nodes.ToDictionary(Item => Item.Id);
        Assert.That(nodeById["A"].Label, Is.EqualTo("Markdown"));
        Assert.That(nodeById["B"].Label, Is.EqualTo("Detect Mermaid fence"));
        Assert.That(nodeById["C"].Label, Is.EqualTo("Render SVG"));
        Assert.That(nodeById["D"].Label, Is.EqualTo("Render normal Markdown"));
        Assert.That(nodeById["E"].Label, Is.EqualTo("Embed in DOCX"));
    }

    [Test]
    public void Direction_ParsesDirection()
    {
        const string input =
            """
            flowchart TD
                A --> B
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Direction, Is.EqualTo(Direction.TopToBottom));
    }

    [Test]
    public void RoundedNodes_ParsesShape()
    {
        const string input =
            """
            flowchart LR
                A(Rounded)
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Nodes[0].Shape, Is.EqualTo(NodeShape.RoundedRectangle));
        Assert.That(result.Value.Nodes[0].Label, Is.EqualTo("Rounded"));
    }

    [Test]
    public void Diamond_ParsesShape()
    {
        const string input =
            """
            flowchart LR
                A{Decision}
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Nodes[0].Shape, Is.EqualTo(NodeShape.Diamond));
    }

    [Test]
    public void Circle_ParsesShape()
    {
        const string input =
            """
            flowchart LR
                A((Circle))
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Nodes[0].Shape, Is.EqualTo(NodeShape.Circle));
    }

    [Test]
    public void ChainedNodes_CreatesMultipleEdges()
    {
        const string input =
            """
            flowchart LR
                A --> B --> C --> D
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Nodes.Count, Is.EqualTo(4));
        Assert.That(result.Value.Edges.Count, Is.EqualTo(3));
    }

    [Test]
    public void DottedArrow_ParsesEdgeStyle()
    {
        const string input =
            """
            flowchart LR
                A -.-> B
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Edges[0].LineStyle, Is.EqualTo(EdgeStyle.Dotted));
    }

    [Test]
    public void NestedSubgraph_CrossBoundaryEdgesDoNotReassignInnerMembersToParent()
    {
        const string input =
            """
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
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Subgraphs.Count, Is.EqualTo(1));

        var outer = result.Value.Subgraphs[0];
        Assert.That(outer.NodeIds, Is.EquivalentTo(new[] { "A", "B", "E" }));
        Assert.That(outer.NestedSubgraphs.Count, Is.EqualTo(1));
        Assert.That(outer.NestedSubgraphs[0].NodeIds, Is.EquivalentTo(new[] { "C", "D" }));
    }

    [Test]
    public void ThickArrow_ParsesEdgeStyle()
    {
        const string input =
            """
            flowchart LR
                A ==> B
            """;

        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value.Edges[0].LineStyle, Is.EqualTo(EdgeStyle.Thick));
    }
}
