namespace MermaidSharp.Diagrams.Kanban;

public class KanbanParser : IDiagramParser<KanbanModel>
{
    public DiagramType DiagramType => DiagramType.Kanban;

    sealed record KanbanRow(int Indent, string Id, string Name);

    // Identifier
    static Parser<char, string> Identifier =
        Token(_ => char.IsLetterOrDigit(_) || _ == '_' || _ == '-').AtLeastOnceString();

    // Label in brackets: [Label Text]
    static Parser<char, string> LabelParser =
        from _ in Char('[')
        from label in Token(_ => _ != ']').ManyString()
        from __ in Char(']')
        select label.Trim();

    /// <summary>
    /// One Kanban row: indentation + id[name].
    /// </summary>
    static Parser<char, KanbanRow> KanbanLineParser =
        from indent in CommonParsers.Indentation
        from id in Identifier
        from name in LabelParser
        from __ in CommonParsers.InlineWhitespace
        from ___ in CommonParsers.LineEnd
        select new KanbanRow(indent, id, name);

    // Skip line (comments, empty lines)
    static Parser<char, Unit> SkipLine =
        Try(CommonParsers.InlineWhitespace.Then(CommonParsers.Comment))
            .Or(Try(CommonParsers.InlineWhitespace.Then(CommonParsers.Newline)));

    // Content item
    static Parser<char, object?> ContentItem =>
        OneOf(
            Try(KanbanLineParser.Select(static r => (object?)r)),
            SkipLine.ThenReturn((object?)null)
        );

    public static Parser<char, KanbanModel> Parser =>
        from _ in CommonParsers.InlineWhitespace
        from __ in CIString("kanban")
        from ___ in CommonParsers.InlineWhitespace
        from ____ in CommonParsers.LineEnd
        from result in ContentItem.ManyThen(End)
        select BuildModel(result.Item1.ToList());

    static KanbanModel BuildModel(List<object?> items)
    {
        var lines = new List<(int indent, string id, string name)>();
        foreach (var item in items)
        {
            if (item is KanbanRow row)
            {
                lines.Add((row.Indent, row.Id, row.Name));
            }
        }

        var model = new KanbanModel();
        if (lines.Count == 0)
        {
            return model;
        }

        var minIndent = lines.Min(_ => _.indent);
        KanbanColumn? currentColumn = null;

        foreach (var line in lines)
        {
            if (line.indent == minIndent)
            {
                currentColumn = new KanbanColumn { Id = line.id, Name = line.name };
                model.Columns.Add(currentColumn);
            }
            else if (line.indent > minIndent && currentColumn != null)
            {
                currentColumn.Tasks.Add(new KanbanTask { Id = line.id, Name = line.name });
            }
        }

        return model;
    }

    public Result<char, KanbanModel> Parse(string input) => Parser.Parse(input);
}
