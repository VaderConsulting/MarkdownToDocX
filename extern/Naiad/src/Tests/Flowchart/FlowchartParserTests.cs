using MermaidSharp.Models;
using MermaidSharp.Diagrams.Flowchart;

public class FlowchartParserTests
{
    [Test]
    public void F81_TrapezoidBracketShapes_ParseLabelsAndShapes()
    {
        const string Input =
            """
            flowchart TD
            Top[/Wide top/] --> Bottom[\Wide bottom/]
            """;

        FlowchartParser Parser = new FlowchartParser();
        var Result = Parser.Parse(Input);
        Assert.That(Result.Success, Is.True);

        Node Top = Result.Value.Nodes.Single(Node => Node.Id == "Top");
        Node Bottom = Result.Value.Nodes.Single(Node => Node.Id == "Bottom");
        Assert.That(Top.Label, Is.EqualTo("Wide top"));
        Assert.That(Top.Shape, Is.EqualTo(NodeShape.ParallelogramAlt));
        Assert.That(Bottom.Label, Is.EqualTo("Wide bottom"));
        Assert.That(Bottom.Shape, Is.EqualTo(NodeShape.TrapezoidAlt));
    }

    [Test]
    public void F61_GroupedVertexExpansion_ExpandsToAllPairs()
    {
        const string Input =
            """
            flowchart TB
            A & B --> C & D
            """;

        FlowchartParser Parser = new FlowchartParser();
        var Result = Parser.Parse(Input);
        Assert.That(Result.Success, Is.True);

        Assert.That(Result.Value.Nodes.Count, Is.EqualTo(4));
        Assert.That(Result.Value.Edges.Count, Is.EqualTo(4));
        Assert.That(Result.Value.Edges.Any(Edge => Edge.SourceId == "A" && Edge.TargetId == "C"), Is.True);
        Assert.That(Result.Value.Edges.Any(Edge => Edge.SourceId == "A" && Edge.TargetId == "D"), Is.True);
        Assert.That(Result.Value.Edges.Any(Edge => Edge.SourceId == "B" && Edge.TargetId == "C"), Is.True);
        Assert.That(Result.Value.Edges.Any(Edge => Edge.SourceId == "B" && Edge.TargetId == "D"), Is.True);
    }

    [Test]
    public void F48_MermaidEntityCodes_DecodeInNodeLabels()
    {
        const string Input =
            """
            flowchart TB
            A["Symbols: star#42; underscore#95; backtick#96; brackets#91;#93; pipe A#124;B"] --> B["Quotes: double#quot; and apostrophe#39;s"]
            B --> C["Comparison: a#lt; b #38;#38; c#gt; d"]
            """;

        FlowchartParser Parser = new FlowchartParser();
        var Result = Parser.Parse(Input);
        Assert.That(Result.Success, Is.True);

        Node NodeA = Result.Value.Nodes.Single(Node => Node.Id == "A");
        Node NodeB = Result.Value.Nodes.Single(Node => Node.Id == "B");
        Node NodeC = Result.Value.Nodes.Single(Node => Node.Id == "C");

        Assert.That(NodeB.Label, Is.EqualTo("Quotes: double\" and apostrophe's"));
        Assert.That(NodeB.Label, Does.Not.Contain("#quot;"));
        Assert.That(NodeC.Label, Is.EqualTo("Comparison: a< b && c> d"));
    }

    [Test]
    public void F25_F26_QuotedPipeEdgeLabels_StripDelimiterQuotes()
    {
        const string Input =
            """
            flowchart LR
            A[Producer] -->|"This edge label is intentionally long so wrapping, clearance from arrowheads, and node borders can be verified in Word."| B[Consumer]
            """;

        FlowchartParser Parser = new FlowchartParser();
        var Result = Parser.Parse(Input);
        Assert.That(Result.Success, Is.True);
        Edge Edge = Result.Value.Edges.Single();
        Assert.That(Edge.Label, Does.StartWith("This edge label is intentionally long"));
        Assert.That(Edge.Label, Does.Not.Contain("\""));
    }

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
