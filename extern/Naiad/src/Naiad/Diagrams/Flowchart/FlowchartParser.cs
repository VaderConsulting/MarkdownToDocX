namespace MermaidSharp.Diagrams.Flowchart;

public class FlowchartParser : IDiagramParser<FlowchartModel>
{
    public DiagramType DiagramType => DiagramType.Flowchart;

#pragma warning disable IDE0007
    private sealed class FlowchartParseState
    {
        public List<Subgraph> RootSubgraphs { get; } = new();
        public Stack<Subgraph> SubgraphStack { get; } = new();
        public Dictionary<string, string> NodeIdToCurrentSubgraphId { get; } = new();
    }
#pragma warning restore IDE0007

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
    static Parser<char, (string SubgraphId, string? Title)> subgraphStart =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("subgraph")
        from ___ in CommonParsers.RequiredWhitespace
        from id in CommonParsers.Identifier
        from tail in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select (id, ParseSubgraphTitle(tail));

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
    static Parser<char, Direction> subgraphDirectionDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("direction")
        from ___ in CommonParsers.RequiredWhitespace
        from dir in flowchartDirection
        from _____ in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        select dir;

    // Skip empty lines, comments, and directives
    static Parser<char, Unit> skipLine =
        OneOf(
            Try(styleDirective),
            Try(classDefDirective),
            Try(classDirective),
            Try(clickDirective),
            CommonParsers.InlineWhitespace.Then(Try(CommonParsers.Comment).Or(CommonParsers.Newline))
        );

    // Content item (either a subgraph directive, a statement, or a skip line)
    static Parser<char, object?> ContentItem =>
        OneOf(
            Try(subgraphStart.Select(value => (object?) ("subgraphStart", value.SubgraphId, value.Title))),
            Try(subgraphDirectionDirective.Select(value => (object?) ("subgraphDirection", value))),
            Try(subgraphEnd.ThenReturn((object?) ("subgraphEnd"))),
            Try(CommonParsers.InlineWhitespace.Then(StatementParser).Before(CommonParsers.InlineWhitespace.Then(CommonParsers.LineEnd)).Select(value => (object?) value)),
            skipLine.ThenReturn((object?) null)
        );

    public static Parser<char, FlowchartModel> Parser =>
        from _ in CommonParsers.InlineWhitespace
        from keyword in Try(String("flowchart")).Or(String("graph"))
        from __ in CommonParsers.InlineWhitespace
        from direction in flowchartDirection.Optional()
        from ___ in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        from content in ContentItem.Many()
        select BuildModel(direction.GetValueOrDefault(Direction.TopToBottom), content.Where(value => value != null).ToList());

    static FlowchartModel BuildModel(
        Direction direction,
        List<object?> content)
    {
#pragma warning disable IDE0007
        var model = new FlowchartModel { Direction = direction };
        var nodeDict = new Dictionary<string, Node>();
        FlowchartParseState state = new FlowchartParseState();
#pragma warning restore IDE0007

#pragma warning disable IDE0007
        foreach (object? item in content)
        {
            if (item is ValueTuple<string, string, string?> start
                && string.Equals(start.Item1, "subgraphStart", StringComparison.Ordinal))
            {
                string SubgraphId = start.Item2;
                string? Title = start.Item3;

                Subgraph subgraph = new Subgraph
                {
                    Id = SubgraphId,
                    Title = Title,
                    Direction = Direction.TopToBottom
                };

                if (state.SubgraphStack.Count > 0)
                {
                    state.SubgraphStack.Peek().NestedSubgraphs.Add(subgraph);
                }
                else
                {
                    state.RootSubgraphs.Add(subgraph);
                }

                state.SubgraphStack.Push(subgraph);
                continue;
            }

            if (item is ValueTuple<string, Direction> dirItem
                && string.Equals(dirItem.Item1, "subgraphDirection", StringComparison.Ordinal))
            {
                if (state.SubgraphStack.Count > 0)
                {
                    state.SubgraphStack.Peek().Direction = dirItem.Item2;
                }
                continue;
            }

            if (item is string endItem && string.Equals(endItem, "subgraphEnd", StringComparison.Ordinal))
            {
                if (state.SubgraphStack.Count > 0)
                {
                    state.SubgraphStack.Pop();
                }
                continue;
            }

            if (item is ValueTuple<List<Node>, List<(EdgeType Type, EdgeStyle Style, string? Label, int MinRankSpan)>> statement)
            {
                List<Node> nodes = statement.Item1;
                List<(EdgeType Type, EdgeStyle Style, string? Label, int MinRankSpan)> edges = statement.Item2;

                for (var i = 0; i < nodes.Count; i++)
                {
                    Node node = nodes[i];

                    if (!nodeDict.TryGetValue(node.Id, out Node? existingNode))
                    {
                        nodeDict[node.Id] = node;
                        model.Nodes.Add(node);
                    }
                    else if (node.Label != null && existingNode.Label == null)
                    {
                        existingNode.Label = node.Label;
                        existingNode.Shape = node.Shape;
                    }

                    if (state.SubgraphStack.Count > 0)
                    {
                        Subgraph current = state.SubgraphStack.Peek();
                        if (!current.NodeIds.Contains(node.Id, StringComparer.Ordinal))
                        {
                            current.NodeIds.Add(node.Id);
                        }
                        state.NodeIdToCurrentSubgraphId[node.Id] = current.Id;
                    }

                    if (i < edges.Count)
                    {
                        var edge = edges[i];
                        model.Edges.Add(new Edge
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
        }
#pragma warning restore IDE0007

        #pragma warning disable IDE0007
        foreach (Subgraph subgraph in state.RootSubgraphs)
        {
            model.Subgraphs.Add(subgraph);
        }
        #pragma warning restore IDE0007

        return model;
    }

    static string? ParseSubgraphTitle(string tail)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrWhiteSpace(tail))
        {
            return null;
        }

        string trimmed = tail.Trim();

        // Mermaid subgraph title can be `["Title"]` or `[Title]` (we accept both).
        int OpenIndex = trimmed.IndexOf('[');
        int CloseIndex = trimmed.LastIndexOf(']');
        if (OpenIndex >= 0 && CloseIndex > OpenIndex)
        {
            string inner = trimmed.Substring(OpenIndex + 1, CloseIndex - OpenIndex - 1).Trim();
            inner = NormalizeFlowchartLabel(inner);
            return inner;
        }

        return NormalizeFlowchartLabel(trimmed);
#pragma warning restore IDE0007
    }

    public Result<char, FlowchartModel> Parse(string input) => Parser.Parse(input);
}
