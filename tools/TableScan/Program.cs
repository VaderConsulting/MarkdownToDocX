using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Helpers;
using Markdig.Syntax;
using MDConverter.Options;
using MDConverter.Pipeline;

string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string SpecPath = Path.Combine(RepoRoot, "spec.md");
string Markdown = File.ReadAllText(SpecPath);

MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
MarkdownDocument Document = Markdig.Markdown.Parse(Markdown, Pipeline);

int TableCount = 0;
List<string> PipeParagraphs = new List<string>();
List<string> TableIssues = new List<string>();

foreach (Block Block in Document)
{
    if (Block is Table TableBlock)
    {
        TableCount++;
        int TableIndex = TableCount;
        int? ExpectedColumns = null;
        int RowIndex = 0;
        foreach (TableRow Row in TableBlock.OfType<TableRow>())
        {
            RowIndex++;
            int ColumnCount = 0;
            foreach (TableCell _ in Row.OfType<TableCell>())
            {
                ColumnCount++;
            }
            if (ExpectedColumns == null)
            {
                ExpectedColumns = ColumnCount;
            }
            else if (ColumnCount != ExpectedColumns.Value)
            {
                TableIssues.Add($"Table #{TableIndex} row {RowIndex}: {ColumnCount} cols, expected {ExpectedColumns.Value}");
            }
        }
        continue;
    }

    if (Block is ParagraphBlock Paragraph && Paragraph.Lines.Count > 0)
    {
        StringBuilder LineText = new StringBuilder();
        foreach (StringLine Line in Paragraph.Lines.Lines)
        {
            LineText.Append(Markdown.AsSpan().Slice(Line.Slice.Start, Line.Slice.Length));
            LineText.Append(' ');
        }

        string Text = LineText.ToString().Trim();
        if (Text.StartsWith('|') && Text.Count(Character => Character == '|') >= 2)
        {
            PipeParagraphs.Add(Text.Length > 160 ? Text[..160] + "..." : Text);
        }
    }
}

MarkdownToDocModelBuilder Builder = new MarkdownToDocModelBuilder(new MarkdownOptions { EnableTables = true });
MDConverter.Model.DocModel Model = Builder.Build(Markdown, Path.GetDirectoryName(SpecPath)!);
int ModelTableCount = Model.Blocks.Count(Block => Block is MDConverter.Model.TableBlock);

Console.WriteLine($"Markdig tables: {TableCount}");
Console.WriteLine($"DocModel tables: {ModelTableCount}");
Console.WriteLine($"Pipe paragraphs (broken tables): {PipeParagraphs.Count}");
Console.WriteLine($"Tables with column mismatches: {TableIssues.Count}");
foreach (string Item in TableIssues.Take(40))
{
    Console.WriteLine("  " + Item);
}
foreach (string Item in PipeParagraphs)
{
    Console.WriteLine("  " + Item.Replace('\n', ' '));
}
