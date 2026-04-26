namespace MermaidSharp.Diagrams.Block;

public class BlockParser : IDiagramParser<BlockModel>
{
    public DiagramType DiagramType => DiagramType.Block;

    /// <summary>
    /// Block node identifiers must not begin with '-' so edge lines such as <c>a --> b</c> cannot be parsed as two
    /// element ids followed by stray punctuation.
    /// </summary>
    static Parser<char, string> Identifier =
        Token(static Character => char.IsLetter(Character) || char.IsDigit(Character) || Character == '_')
            .Then(
                Token(static Character => char.IsLetterOrDigit(Character) || Character is '_' or '-').ManyString(),
                (char FirstCharacter, string Rest) => FirstCharacter.ToString() + Rest);

    // Label content (text inside shape brackets)
    static Parser<char, string> labelContent =
        Token(_ => _ != '"' && _ != ']' && _ != ')' && _ != '}').ManyString();

    static Parser<char, string> quotedLabel =
        Char('"').Then(Token(_ => _ != '"').ManyString()).Before(Char('"'));

    static Parser<char, int> columnsParser =
        from inlineWhitespace in CommonParsers.InlineWhitespace
        from columns in CIString("columns")
        from rRequiredWhitespace in CommonParsers.RequiredWhitespace
        from num in Digit.AtLeastOnceString().Select(int.Parse)
        from innerInlineWhitespace in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        select num;

    // Rectangle shape: ["label"] or [label]
    static Parser<char, (string label, BlockShape shape)> rectangleShape =
        from left in Char('[')
        from label in quotedLabel.Or(labelContent)
        from right in Char(']')
        select (label.Trim(), BlockShape.Rectangle);

    // Rounded shape: ("label") or (label)
    static Parser<char, (string label, BlockShape shape)> roundedShape =
        from left in Char('(')
        from label in quotedLabel.Or(Token(_ => _ != ')').ManyString())
        from right in Char(')')
        select (label.Trim(), BlockShape.Rounded);

    // Stadium shape: (["label"]) or ([label])
    static Parser<char, (string label, BlockShape shape)> stadiumShape =
        from left in String("([")
        from label in quotedLabel.Or(Token(_ => _ != ']').ManyString())
        from right in String("])")
        select (label.Trim(), BlockShape.Stadium);

    // Circle shape: (("label")) or ((label))
    static Parser<char, (string label, BlockShape shape)> circleShape =
        from left in String("((")
        from label in quotedLabel.Or(Token(_ => _ != ')').ManyString())
        from right in String("))")
        select (label.Trim(), BlockShape.Circle);

    // Diamond shape: {"label"} or {label}
    static Parser<char, (string label, BlockShape shape)> diamondShape =
        from left in Char('{')
        from notDouble in Lookahead(AnyCharExcept('{'))
        from label in quotedLabel.Or(Token(_ => _ != '}').ManyString())
        from right in Char('}')
        select (label.Trim(), BlockShape.Diamond);

    // Hexagon shape: {{"label"}} or {{label}}
    static Parser<char, (string label, BlockShape shape)> hexagonShape =
        from left in String("{{")
        from label in quotedLabel.Or(Token(_ => _ != '}').ManyString())
        from right in String("}}")
        select (label.Trim(), BlockShape.Hexagon);

    // Shape parser (order matters - more specific first)
    static Parser<char, (string label, BlockShape shape)> shapeParser =
        OneOf(
            Try(stadiumShape),
            Try(circleShape),
            Try(hexagonShape),
            Try(diamondShape),
            Try(roundedShape),
            Try(rectangleShape)
        );

    // Span: :N
    static Parser<char, int> spanParser =
        from _ in Char(':')
        from num in Digit.AtLeastOnceString().Select(int.Parse)
        select num;

    // Block element: id["label"]:2
    static Parser<char, BlockElement> elementParser =
        from id in Identifier
        from shape in shapeParser.Optional()
        from span in spanParser.Optional()
        select new BlockElement
        {
            Id = id,
            Label = shape.HasValue ? shape.Value.label : id,
            Shape = shape.HasValue ? shape.Value.shape : BlockShape.Rectangle,
            Span = span.GetValueOrDefault(1)
        };

    // Elements on a line (space separated)
    static Parser<char, List<BlockElement>> elementsLineParser =
        from _ in CommonParsers.InlineWhitespace
        from elements in elementParser.SeparatedAtLeastOnce(CommonParsers.RequiredInlineWhitespace)
        from __ in CommonParsers.InlineWhitespace
        from ___ in CommonParsers.LineEnd
        select elements.ToList();

    // Edge: id --> id or id --- id (Mermaid block diagram)
    static Parser<char, bool> edgeConnectorParser =
        Try(Char('-').Then(Char('-')).Then(Char('>')).ThenReturn(true))
            .Or(Char('-').Then(Char('-')).Then(Char('-')).ThenReturn(false));

    static Parser<char, BlockEdge> edgeLineParser =
        from _ in CommonParsers.InlineWhitespace
        from fromId in Identifier
        from __ in CommonParsers.InlineWhitespace
        from directed in edgeConnectorParser
        from ___ in CommonParsers.InlineWhitespace
        from toId in Identifier
        from ____ in CommonParsers.InlineWhitespace
        from _____ in CommonParsers.LineEnd
        select new BlockEdge { FromId = fromId, ToId = toId, Directed = directed };

    // Skip line (comments, empty lines)
    static Parser<char, Unit> skipLine =
        Try(CommonParsers.InlineWhitespace.Then(CommonParsers.Comment))
            .Or(Try(CommonParsers.InlineWhitespace.Then(CommonParsers.Newline)));

    // Content item (edges before element lines so "a --> b" is never treated as starting a second element id).
    static Parser<char, object?> ContentItem =>
        OneOf(
            Try(columnsParser.Select(_ => (object?)("columns", _))),
            Try(edgeLineParser.Select(_ => (object?)("edge", _))),
            Try(elementsLineParser.Select(_ => (object?)("elements", _))),
            skipLine.ThenReturn((object?)null)
        );

    public static Parser<char, BlockModel> Parser =>
        from _ in CommonParsers.InlineWhitespace
        from __ in OneOf(CIString("block-beta"), CIString("block"))
        from ___ in CommonParsers.InlineWhitespace
        from ____ in CommonParsers.LineEnd
        from result in ContentItem.ManyThen(End)
        select BuildModel(result.Item1.Where(_ => _ != null).ToList());

    static BlockModel BuildModel(List<object?> content)
    {
        var model = new BlockModel();

        foreach (var item in content)
        {
            switch (item)
            {
                case ("columns", int columns):
                    model.Columns = columns;
                    break;

                case ("elements", List<BlockElement> elements):
                    model.Elements.AddRange(elements);
                    break;

                case ("edge", BlockEdge edge):
                    model.Edges.Add(edge);
                    break;
            }
        }

        return model;
    }

    public Result<char, BlockModel> Parse(string input) => Parser.Parse(input);
}
