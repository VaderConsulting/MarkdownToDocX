namespace MermaidSharp.Diagrams.Flowchart;

public class FlowchartParser : IDiagramParser<FlowchartModel>
{
    public DiagramType DiagramType => DiagramType.Flowchart;

    /// <summary>
    /// Normalises flowchart node text when bracket contents use Mermaid quoted delimiter syntax (double quotes wrapping the visible label).
    /// </summary>
    /// <param name="text">Raw text captured between shape delimiters.</param>
    /// <returns>Display text without outer delimiter quotes.</returns>
    static string NormalizeFlowchartLabel(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var trimmed = text.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
        {
            var inner = trimmed.Substring(1, trimmed.Length - 2);
            var unescaped = inner.Replace("\\\"", "\"").Replace("\\\\", "\\");
            return unescaped.Trim();
        }

        return trimmed;
    }

    // Node shape parsers - returns (label, shape)
    static Parser<char, (string Label, NodeShape Shape)> doubleCircleShape =
        String("(((")
            .Then(Token(_ => _ != ')').ManyString())
            .Before(String(")))"))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.DoubleCircle));

    static Parser<char, (string Label, NodeShape Shape)> circleShape =
        String("((")
            .Then(Token(_ => _ != ')').ManyString())
            .Before(String("))"))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Circle));

    static Parser<char, (string Label, NodeShape Shape)> stadiumShape =
        String("([")
            .Then(Token(_ => _ != ']').ManyString())
            .Before(String("])"))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Stadium));

    static Parser<char, (string Label, NodeShape Shape)> subroutineShape =
        String("[[")
            .Then(Token(_ => _ != ']').ManyString())
            .Before(String("]]"))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Subroutine));

    static Parser<char, (string Label, NodeShape Shape)> cylinderShape =
        String("[(")
            .Then(Token(_ => _ != ')').ManyString())
            .Before(String(")]"))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Cylinder));

    static Parser<char, (string Label, NodeShape Shape)> hexagonShape =
        String("{{")
            .Then(Token(_ => _ != '}').ManyString())
            .Before(String("}}"))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Hexagon));

    static Parser<char, (string Label, NodeShape Shape)> diamondShape =
        Char('{')
            .Then(Token(_ => _ != '}').ManyString())
            .Before(Char('}'))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Diamond));

    static Parser<char, (string Label, NodeShape Shape)> roundedShape =
        Char('(')
            .Then(Token(_ => _ != ')').ManyString())
            .Before(Char(')'))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.RoundedRectangle));

    static Parser<char, (string Label, NodeShape Shape)> rectangleShape =
        Char('[')
            .Then(Token(_ => _ != ']').ManyString())
            .Before(Char(']'))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Rectangle));

    static Parser<char, (string Label, NodeShape Shape)> asymmetricShape =
        Char('>')
            .Then(Token(_ => _ != ']').ManyString())
            .Before(Char(']'))
            .Select(text => (NormalizeFlowchartLabel(text), NodeShape.Asymmetric));

    static Parser<char, (string Label, NodeShape Shape)> nodeShapeParser =
        OneOf(
            Try(doubleCircleShape),
            Try(circleShape),
            Try(stadiumShape),
            Try(subroutineShape),
            Try(cylinderShape),
            Try(hexagonShape),
            Try(diamondShape),
            Try(roundedShape),
            Try(asymmetricShape),
            rectangleShape
        );

    // Node parser: identifier optionally followed by shape
    static Parser<char, Node> nodeParser =
        from id in CommonParsers.Identifier
        from shape in nodeShapeParser.Optional()
        select new Node
        {
            Id = id,
            Label = shape.HasValue ? shape.Value.Label : null,
            Shape = shape.HasValue ? shape.Value.Shape : NodeShape.Rectangle
        };

    /// <summary>
    /// Solid arrow: <c>--</c> plus any number of extra <c>-</c> before <c>&gt;</c>. <see cref="MermaidSharp.Models.Edge.MinRankSpan"/> uses
    /// <c>1 +</c> (extra hyphen count), matching Mermaid-style longer rendered links for longer arrow glyphs.
    /// </summary>
    static Parser<char, (EdgeType Type, EdgeStyle Style, int MinRankSpan)> solidArrowFlexibleParser =
        String("--")
            .Then(Char('-').ManyString())
            .Before(Char('>'))
            .Select(extra => (EdgeType.Arrow, EdgeStyle.Solid, 1 + extra.Length));

    static Parser<char, (EdgeType Type, EdgeStyle Style, int MinRankSpan)> arrowTypeParser =
        OneOf(
            Try(String("<-->")).Select(_ => (EdgeType.BiDirectional, EdgeStyle.Solid, 1)),
            Try(String("o--o")).Select(_ => (EdgeType.BiDirectionalCircle, EdgeStyle.Solid, 1)),
            Try(String("x--x")).Select(_ => (EdgeType.BiDirectionalCross, EdgeStyle.Solid, 1)),
            Try(String("-.->")).Select(_ => (EdgeType.DottedArrow, EdgeStyle.Dotted, 1)),
            Try(String("-.-")).Select(_ => (EdgeType.Dotted, EdgeStyle.Dotted, 1)),
            Try(String("==>")).Select(_ => (EdgeType.ThickArrow, EdgeStyle.Thick, 1)),
            Try(String("===")).Select(_ => (EdgeType.Thick, EdgeStyle.Thick, 1)),
            Try(String("--o")).Select(_ => (EdgeType.CircleEnd, EdgeStyle.Solid, 1)),
            Try(String("--x")).Select(_ => (EdgeType.CrossEnd, EdgeStyle.Solid, 1)),
            Try(solidArrowFlexibleParser),
            String("---").Select(_ => (EdgeType.Open, EdgeStyle.Solid, 1))
        );

    // Mermaid allows link text embedded in the arrow token, e.g. `A -. text .-> B`.
    // For now we accept and treat that embedded text as the edge label.
    static Parser<char, (EdgeType Type, EdgeStyle Style, int MinRankSpan, string? EmbeddedLabel)> arrowWithOptionalEmbeddedLabelParser =
        OneOf(
            Try(
                String("-.")
                    .Then(Token(_ => _ != '.' && _ != '\r' && _ != '\n').ManyString())
                    .Before(String(".->"))
                    .Select(text => text.Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Select(text => (EdgeType.DottedArrow, EdgeStyle.Dotted, 1, (string?) text))
            ),
            arrowTypeParser.Select(arrow => (arrow.Type, arrow.Style, arrow.MinRankSpan, (string?) null))
        );

    // Edge label: |text|
    static Parser<char, string> edgeLabelParser =
        Char('|')
            .Then(Token(_ => _ != '|').ManyString())
            .Before(Char('|'));

    static Parser<char, Direction> flowchartDirection =
        OneOf(
            Try(String("TB")).ThenReturn(Direction.TopToBottom),
            Try(String("TD")).ThenReturn(Direction.TopToBottom),
            Try(String("BT")).ThenReturn(Direction.BottomToTop),
            Try(String("LR")).ThenReturn(Direction.LeftToRight),
            String("RL").ThenReturn(Direction.RightToLeft)
        );

    // Statement: A --> B --> C (chain of nodes with edges)
    static Parser<char, (List<Node> Nodes, List<(EdgeType Type, EdgeStyle Style, string? Label, int MinRankSpan)> Edges)> StatementParser =>
        from first in nodeParser
        from rest in (
            from _1 in CommonParsers.InlineWhitespace
            from label1 in edgeLabelParser.Optional()
            from _2 in CommonParsers.InlineWhitespace.Optional()
            from arrow in arrowWithOptionalEmbeddedLabelParser
            from _3 in CommonParsers.InlineWhitespace.Optional()
            from label2 in edgeLabelParser.Optional()
            from _4 in CommonParsers.InlineWhitespace
            from node in nodeParser
            select (
                Node: node,
                Type: arrow.Type,
                Style: arrow.Style,
                Label: arrow.EmbeddedLabel ?? (label1.HasValue ? label1.Value : label2.HasValue ? label2.Value : null),
                MinRankSpan: arrow.MinRankSpan)
        ).Many()
        select (
            new List<Node>([first, .. rest.Select(t => t.Node)]),
            rest.Select(t => (t.Type, t.Style, t.Label, t.MinRankSpan)).ToList()
        );

    // Style directive: style NodeName fill:#color,stroke:#color
    static Parser<char, Unit> styleDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("style")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString() // consume rest of line
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // Class definition: classDef className fill:#color
    static Parser<char, Unit> classDefDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("classDef")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // Class application: class nodeId className
    static Parser<char, Unit> classDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("class")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // Click directive: click nodeId callback
    static Parser<char, Unit> clickDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("click")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // Subgraph start: subgraph name[Label] or subgraph name
    static Parser<char, Unit> subgraphStart =
        from _ in CommonParsers.InlineWhitespace
        from subGraph in String("subgraph")
        from ___ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // Subgraph end: end
    static Parser<char, Unit> subgraphEnd =
        from _ in CommonParsers.InlineWhitespace
        from end in String("end")
        from ___ in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // Subgraph direction line: direction LR / TB / ...
    // Mermaid allows `direction` inside subgraphs to override local layout direction.
    // Naiad currently does not apply per-subgraph direction; we accept and ignore it for parsing compatibility.
    static Parser<char, Unit> subgraphDirectionDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("direction")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in flowchartDirection
        from _____ in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // Skip empty lines, comments, and directives
    static Parser<char, Unit> skipLine =
        OneOf(
            Try(styleDirective),
            Try(classDefDirective),
            Try(classDirective),
            Try(clickDirective),
            Try(subgraphStart),
            Try(subgraphDirectionDirective),
            Try(subgraphEnd),
            CommonParsers.InlineWhitespace.Then(Try(CommonParsers.Comment).Or(CommonParsers.Newline))
        );

    public static Parser<char, FlowchartModel> Parser =>
        from _ in CommonParsers.InlineWhitespace
        from keyword in Try(String("flowchart")).Or(String("graph"))
        from __ in CommonParsers.InlineWhitespace
        from direction in flowchartDirection.Optional()
        from ___ in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        from statements in ParseStatements()
        select BuildModel(direction.GetValueOrDefault(Direction.TopToBottom), statements);

    static Parser<char, List<(List<Node> Nodes, List<(EdgeType Type, EdgeStyle Style, string? Label, int MinRankSpan)> Edges)>> ParseStatements()
    {
        var statement =
            CommonParsers.InlineWhitespace
                .Then(StatementParser)
                .Before(CommonParsers.InlineWhitespace.Then(CommonParsers.LineEnd));

        var skipLine = FlowchartParser.skipLine.ThenReturn((new List<Node>(),
            new List<(EdgeType, EdgeStyle, string?, int)>()));

        return Try(statement).Or(skipLine).Many()
            .Select(_ => _.Where(__ => __.Nodes.Count > 0).ToList());
    }

    static FlowchartModel BuildModel(
        Direction direction,
        List<(List<Node> Nodes, List<(EdgeType Type, EdgeStyle Style, string? Label, int MinRankSpan)> Edges)> statements)
    {
        var model = new FlowchartModel { Direction = direction };
        var nodeDict = new Dictionary<string, Node>();

        foreach (var (nodes, edges) in statements)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                // Add or update node
                if (!nodeDict.TryGetValue(node.Id, out var existingNode))
                {
                    nodeDict[node.Id] = node;
                    model.Nodes.Add(node);
                }
                else if (node.Label != null && existingNode.Label == null)
                {
                    existingNode.Label = node.Label;
                    existingNode.Shape = node.Shape;
                }

                // Add edge to next node
                if (i < edges.Count)
                {
                    var edge = edges[i];
                    model.Edges.Add(new()
                    {
                        SourceId = nodes[i].Id,
                        TargetId = nodes[i + 1].Id,
                        Type = edge.Type,
                        LineStyle = edge.Style,
                        Label = edge.Label,
                        MinRankSpan = Math.Max(1, edge.MinRankSpan)
                    });
                }
            }
        }

        return model;
    }

    public Result<char, FlowchartModel> Parse(string input) => Parser.Parse(input);
}
