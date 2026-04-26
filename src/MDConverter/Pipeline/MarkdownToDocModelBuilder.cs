using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MDConverter.Model;
using MDConverter.Options;

using MHeading = Markdig.Syntax.HeadingBlock;
using MParagraph = Markdig.Syntax.ParagraphBlock;
using MFencedCode = Markdig.Syntax.FencedCodeBlock;
using MCodeBlock = Markdig.Syntax.CodeBlock;
using MQuote = Markdig.Syntax.QuoteBlock;
using MList = Markdig.Syntax.ListBlock;
using MListItem = Markdig.Syntax.ListItemBlock;
using MTable = Markdig.Extensions.Tables.Table;
using MTableRow = Markdig.Extensions.Tables.TableRow;
using MTableCell = Markdig.Extensions.Tables.TableCell;
using MEmphasis = Markdig.Syntax.Inlines.EmphasisInline;
using MCodeInline = Markdig.Syntax.Inlines.CodeInline;
using MLink = Markdig.Syntax.Inlines.LinkInline;
using MAutolink = Markdig.Syntax.Inlines.AutolinkInline;
using MTaskList = Markdig.Extensions.TaskLists.TaskList;

namespace MDConverter.Pipeline;

/// <summary>
/// Builds the internal DocModel from Markdown text using Markdig.
/// </summary>
public sealed class MarkdownToDocModelBuilder
{
    private readonly MarkdownOptions options;
    private string rootPath = string.Empty;

    public MarkdownToDocModelBuilder(MarkdownOptions MarkdownOptions) => options = MarkdownOptions ?? new MarkdownOptions();

    /// <summary>
    /// Parses the given Markdown and builds a DocModel. BasePath is used for resolving relative image paths (informational only in the model).
    /// </summary>
    public DocModel Build(string Markdown, string BasePath)
    {
        rootPath = string.IsNullOrWhiteSpace(BasePath) ? Environment.CurrentDirectory : BasePath;
        var Pipeline = CreatePipeline();
        var Document = Markdig.Markdown.Parse(NormalizeMermaidColonFences(Markdown ?? string.Empty), Pipeline);
        var Model = new DocModel();
        foreach (var Block in Document)
        {
            AppendBlock(Model, Block);
        }
        return Model;
    }

    private MarkdownPipeline CreatePipeline()
    {
        var Builder = new MarkdownPipelineBuilder();
        if (options.EnableTables)
        {
            Builder = Builder.UseAdvancedExtensions();
        }
        else
        {
            Builder = Builder.UsePreciseSourceLocation();
        }
        if (options.EnableTaskLists)
        {
            Builder = Builder.UseTaskLists();
        }
        if (options.EnableAutoLinks)
        {
            Builder = Builder.UseAutoLinks();
        }
        if (options.EnableFootnotes)
        {
            Builder = Builder.UseFootnotes();
        }
        return Builder.Build();
    }

    private void AppendBlock(DocModel Model, Block Block)
    {
        if (Block is MHeading Heading)
        {
            var OurHeading = new Model.HeadingBlock(Heading.Level);
            AppendInlines(OurHeading, Heading.Inline);
            Model.AddBlock(OurHeading);
            return;
        }

        if (Block is MParagraph Paragraph)
        {
            var OurParagraph = new Model.ParagraphBlock();
            AppendInlines(OurParagraph, Paragraph.Inline);
            Model.AddBlock(OurParagraph);
            return;
        }

        if (Block is MFencedCode Fenced)
        {
            var Content = ExtractCodeBlockContent(Fenced);
            var Language = Fenced.Info ?? string.Empty;
            if (IsMermaidLanguage(Language))
            {
                Model.AddBlock(new Model.MermaidBlock(Content));
                return;
            }
            Model.AddBlock(new Model.CodeBlock(Content, Language));
            return;
        }

        if (Block is Markdig.Syntax.CodeBlock IndentedCode)
        {
            var Content = ExtractCodeBlockContent(IndentedCode);
            Model.AddBlock(new Model.CodeBlock(Content, ""));
            return;
        }

        if (Block is MQuote Quote)
        {
            var OurQuote = new Model.QuoteBlock();
            foreach (var Child in Quote)
            {
                AppendBlockToQuote(OurQuote, Child);
            }
            Model.AddBlock(OurQuote);
            return;
        }

        if (Block is MList List)
        {
            var OurList = new Model.ListBlock(List.IsOrdered);
            if (List.IsOrdered && int.TryParse(List.OrderedStart, out var StartNumber) && StartNumber != 1)
            {
                OurList.StartNumber = StartNumber;
            }
            foreach (var Item in List.OfType<MListItem>())
            {
                var OurItem = new Model.ListItemBlock();
                foreach (var Child in Item)
                {
                    AppendBlockToListItem(OurItem, Child);
                }
                OurList.AddItem(OurItem);
            }
            Model.AddBlock(OurList);
            return;
        }

        if (Block is MTable Table)
        {
            var OurTable = new TableBlock { HasHeaderRow = true };
            IReadOnlyList<TableColumnDefinition> Columns = Table.ColumnDefinitions;
            var Alignments = new List<TableAlignment>();
            for (var I = 0; I < Columns.Count; I++)
            {
                Alignments.Add(ToTableAlignment(Columns[I].Alignment ?? Markdig.Extensions.Tables.TableColumnAlign.Left));
            }
            OurTable.ColumnAlignments = Alignments;
            foreach (var Tr in Table.OfType<MTableRow>())
            {
                var OurRow = new Model.TableRow();
                foreach (var Tc in Tr.OfType<MTableCell>())
                {
                    var OurCell = new Model.TableCell();
                    foreach (var CellBlock in Tc.OfType<Block>())
                    {
                        AppendBlockToTableCell(OurCell, CellBlock);
                    }
                    OurRow.AddCell(OurCell);
                }
                OurTable.AddRow(OurRow);
            }
            Model.AddBlock(OurTable);
            return;
        }

        if (Block is ThematicBreakBlock)
        {
            Model.AddBlock(new HorizontalRuleBlock());
            return;
        }

        if (Block is ContainerBlock Container)
        {
            foreach (var Child in Container)
            {
                AppendBlock(Model, Child);
            }
            return;
        }

        if (Block is LeafBlock Leaf && Leaf.Inline != null)
        {
            var Fallback = new Model.ParagraphBlock();
            AppendInlines(Fallback, Leaf.Inline);
            Model.AddBlock(Fallback);
        }
    }

    private static TableAlignment ToTableAlignment(Markdig.Extensions.Tables.TableColumnAlign Align)
    {
        switch (Align)
        {
            case Markdig.Extensions.Tables.TableColumnAlign.Center:
                return TableAlignment.Center;
            case Markdig.Extensions.Tables.TableColumnAlign.Right:
                return TableAlignment.Right;
            default:
                return TableAlignment.Left;
        }
    }

    private void AppendBlockToQuote(Model.QuoteBlock OurQuote, Block Block)
    {
        if (Block is MParagraph Paragraph)
        {
            var OurParagraph = new Model.ParagraphBlock();
            AppendInlines(OurParagraph, Paragraph.Inline);
            OurQuote.AddBlock(OurParagraph);
            return;
        }
        if (Block is MHeading Heading)
        {
            var OurHeading = new Model.HeadingBlock(Heading.Level);
            AppendInlines(OurHeading, Heading.Inline);
            OurQuote.AddBlock(OurHeading);
            return;
        }
        if (Block is MFencedCode Fenced)
        {
            if (IsMermaidLanguage(Fenced.Info ?? string.Empty))
            {
                OurQuote.AddBlock(new Model.MermaidBlock(ExtractCodeBlockContent(Fenced)));
                return;
            }
            OurQuote.AddBlock(new Model.CodeBlock(ExtractCodeBlockContent(Fenced), Fenced.Info ?? ""));
            return;
        }
        if (Block is MList List)
        {
            OurQuote.AddBlock(BuildListBlock(List));
            return;
        }
        if (Block is LeafBlock Leaf && Leaf.Inline != null)
        {
            var OurParagraph = new Model.ParagraphBlock();
            AppendInlines(OurParagraph, Leaf.Inline);
            OurQuote.AddBlock(OurParagraph);
        }
    }

    private void AppendBlockToListItem(Model.ListItemBlock OurItem, Block Block)
    {
        if (Block is MParagraph Paragraph)
        {
            var OurParagraph = new Model.ParagraphBlock();
            AppendInlines(OurParagraph, Paragraph.Inline);
            OurItem.AddBlock(OurParagraph);
            return;
        }
        if (Block is MList NestedList)
        {
            OurItem.AddBlock(BuildListBlock(NestedList));
            return;
        }
        if (Block is MFencedCode Fenced)
        {
            if (IsMermaidLanguage(Fenced.Info ?? string.Empty))
            {
                OurItem.AddBlock(new Model.MermaidBlock(ExtractCodeBlockContent(Fenced)));
                return;
            }
            OurItem.AddBlock(new Model.CodeBlock(ExtractCodeBlockContent(Fenced), Fenced.Info ?? ""));
            return;
        }
        if (Block is Markdig.Syntax.CodeBlock IndentedCode)
        {
            OurItem.AddBlock(new Model.CodeBlock(ExtractCodeBlockContent(IndentedCode), ""));
            return;
        }
        if (Block is LeafBlock Leaf && Leaf.Inline != null)
        {
            var OurParagraph = new Model.ParagraphBlock();
            AppendInlines(OurParagraph, Leaf.Inline);
            OurItem.AddBlock(OurParagraph);
        }
    }

    private Model.ListBlock BuildListBlock(MList List)
    {
        var OurList = new Model.ListBlock(List.IsOrdered);
        if (List.IsOrdered && int.TryParse(List.OrderedStart, out var StartNumber) && StartNumber != 1)
        {
            OurList.StartNumber = StartNumber;
        }

        foreach (var Item in List.OfType<MListItem>())
        {
            var OurItem = new Model.ListItemBlock();
            foreach (var Child in Item)
            {
                AppendBlockToListItem(OurItem, Child);
            }
            OurList.AddItem(OurItem);
        }

        return OurList;
    }

    private void AppendBlockToTableCell(Model.TableCell OurCell, Block Block)
    {
        if (Block is MParagraph Paragraph)
        {
            var OurParagraph = new Model.ParagraphBlock();
            AppendInlines(OurParagraph, Paragraph.Inline);
            OurCell.AddBlock(OurParagraph);
            return;
        }
        if (Block is LeafBlock Leaf && Leaf.Inline != null)
        {
            var OurParagraph = new Model.ParagraphBlock();
            AppendInlines(OurParagraph, Leaf.Inline);
            OurCell.AddBlock(OurParagraph);
        }
    }

    private void AppendInlines(Model.ParagraphBlock Paragraph, Inline? Inline)
    {
        if (Inline == null)
        {
            return;
        }
        foreach (var Node in CollectInlines(Inline))
        {
            Paragraph.AddInline(Node);
        }
    }

    private void AppendInlines(Model.HeadingBlock Heading, Inline? Inline)
    {
        if (Inline == null)
        {
            return;
        }
        foreach (var Node in CollectInlines(Inline))
        {
            Heading.AddInline(Node);
        }
    }

    private List<InlineNode> CollectInlines(Inline Inline)
    {
        var Result = new List<InlineNode>();
        CollectInlines(Inline, Result);
        return Result;
    }

    private void CollectInlines(Inline Inline, List<InlineNode> Result)
    {
        if (Inline == null)
        {
            return;
        }

        if (Inline is LiteralInline Literal)
        {
            Result.Add(new TextRun(Literal.Content.ToString()));
            return;
        }

        if (Inline is MEmphasis Emphasis)
        {
            if (Emphasis.DelimiterChar == '~')
            {
                var OurStrikethrough = new Model.StrikethroughInline();
                foreach (var Child in Emphasis)
                {
                    foreach (var Node in CollectInlines(Child))
                    {
                        OurStrikethrough.AddInline(Node);
                    }
                }
                Result.Add(OurStrikethrough);
                return;
            }

            if (Emphasis.DelimiterCount >= 3)
            {
                var OurStrong = new Model.StrongInline();
                var NestedEmphasis = new Model.EmphasisInline();
                foreach (var Child in Emphasis)
                {
                    foreach (var Node in CollectInlines(Child))
                    {
                        NestedEmphasis.AddInline(Node);
                    }
                }
                OurStrong.AddInline(NestedEmphasis);
                Result.Add(OurStrong);
                return;
            }

            if (Emphasis.DelimiterCount >= 2)
            {
                var OurStrong = new Model.StrongInline();
                foreach (var Child in Emphasis)
                {
                    foreach (var Node in CollectInlines(Child))
                    {
                        OurStrong.AddInline(Node);
                    }
                }
                Result.Add(OurStrong);
                return;
            }

            var OurEmphasis = new Model.EmphasisInline();
            foreach (var Child in Emphasis)
            {
                foreach (var Node in CollectInlines(Child))
                {
                    OurEmphasis.AddInline(Node);
                }
            }
            Result.Add(OurEmphasis);
            return;
        }

        if (Inline is LineBreakInline)
        {
            Result.Add(new BreakInline());
            return;
        }

        if (Inline is MTaskList TaskList)
        {
            Result.Add(new TaskCheckboxInline(TaskList.Checked));
            return;
        }

        if (Inline is MCodeInline Code)
        {
            Result.Add(new Model.CodeInline(Code.Content.ToString()));
            return;
        }

        if (Inline is MAutolink AutoLink)
        {
            var Url = AutoLink.IsEmail ? "mailto:" + AutoLink.Url : AutoLink.Url;
            var OurLink = new Model.LinkInline(Url);
            OurLink.AddInline(new TextRun(AutoLink.Url ?? string.Empty));
            Result.Add(OurLink);
            return;
        }

        if (Inline is MLink Link)
        {
            if (Link.IsImage)
            {
                var AltText = GetFirstLiteralText(Link);
                Result.Add(new Model.ImageInline(ResolveImageSource(Link.Url ?? string.Empty), AltText ?? string.Empty, Link.Title ?? string.Empty));
                return;
            }

            var Url = Link.Url ?? string.Empty;
            var OurLink = new Model.LinkInline(Url);
            foreach (var Child in Link)
            {
                foreach (var Node in CollectInlines(Child))
                {
                    OurLink.AddInline(Node);
                }
            }
            Result.Add(OurLink);
            return;
        }

        if (Inline is ContainerInline Container)
        {
            foreach (var Child in Container)
            {
                CollectInlines(Child, Result);
            }
            return;
        }

        if (Inline is LiteralInline Lit)
        {
            Result.Add(new TextRun(Lit.Content.ToString()));
        }
    }

    private static string GetFirstLiteralText(Inline Container)
    {
        if (Container == null)
        {
            return string.Empty;
        }

        if (Container is LiteralInline Literal)
        {
            return Literal.Content.ToString();
        }

        if (Container is ContainerInline Cont)
        {
            foreach (var Child in Cont)
            {
                if (Child is LiteralInline Lit)
                {
                    return Lit.Content.ToString();
                }

                var Nested = GetFirstLiteralText(Child);
                if (!string.IsNullOrEmpty(Nested))
                {
                    return Nested;
                }
            }
        }

        return string.Empty;
    }

    private string ResolveImageSource(string Url)
    {
        if (string.IsNullOrWhiteSpace(Url)
            || Uri.TryCreate(Url, UriKind.Absolute, out _)
            || Path.IsPathRooted(Url))
        {
            return Url;
        }

        return Path.GetFullPath(Path.Combine(rootPath, Url));
    }

    private static bool IsMermaidLanguage(string Language)
    {
        var FirstToken = (Language ?? string.Empty).Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return string.Equals(FirstToken, "mermaid", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractCodeBlockContent(MCodeBlock CodeBlock)
    {
        if (CodeBlock?.Lines.Lines == null)
        {
            return string.Empty;
        }

        var Builder = new StringBuilder();
        foreach (var Line in CodeBlock.Lines.Lines)
        {
            if (Line.Slice.Text != null)
            {
                Builder.Append(Line.Slice.ToString());
            }
            Builder.AppendLine();
        }

        return Builder.ToString();
    }

    private static string NormalizeMermaidColonFences(string Markdown)
    {
        var Normalized = (Markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var Lines = Normalized.Split('\n');
        var Builder = new StringBuilder(Markdown?.Length ?? 0);
        var InMermaidColonFence = false;

        for (var I = 0; I < Lines.Length; I++)
        {
            var Line = Lines[I];
            var TrimmedStart = Line.TrimStart();

            if (!InMermaidColonFence && TrimmedStart.StartsWith(":::mermaid", StringComparison.OrdinalIgnoreCase))
            {
                Builder.Append("```mermaid");
                Builder.AppendLine();
                InMermaidColonFence = true;
                continue;
            }

            if (InMermaidColonFence && TrimmedStart == ":::")
            {
                Builder.Append("```");
                Builder.AppendLine();
                InMermaidColonFence = false;
                continue;
            }

            Builder.Append(Line);
            if (I < Lines.Length - 1)
            {
                Builder.AppendLine();
            }
        }

        return Builder.ToString();
    }
}
