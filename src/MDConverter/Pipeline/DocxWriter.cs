using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MermaidSharp;
using MDConverter.Model;
using MDConverter.Options;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace MDConverter.Pipeline;

/// <summary>
/// Writes the internal DocModel to a DOCX stream using the Open XML SDK.
/// </summary>
public sealed class DocxWriter
{
    private const int BulletAbstractNumId = 1;
    private const int DecimalAbstractNumId = 2;
    private const int BulletNumId = 1;
    private const int DecimalNumId = 2;

    private readonly EffectiveOptions _Options;
    private MainDocumentPart? _MainPart;
    private NumberingDefinitionsPart? _NumberingPart;
    private int _NextNumberingId = 3;
    private int _NextAbstractNumberingId = 3;
    private uint _NextDrawingId = 1;

    public DocxWriter(EffectiveOptions Options) => this._Options = Options ?? throw new ArgumentNullException(nameof(Options));

    /// <summary>
    /// Writes the document model to the output stream.
    /// </summary>
    public void Write(DocModel Model, Stream OutputStream)
    {
        if (Model == null)
        {
            throw new ArgumentNullException(nameof(Model));
        }
        if (OutputStream == null || !OutputStream.CanWrite)
        {
            throw new ArgumentException("OutputStream must be writable.", nameof(OutputStream));
        }

        using (var Document = CreateDocument(OutputStream))
        {
            var MainPart = Document.MainDocumentPart ?? Document.AddMainDocumentPart();
            _MainPart = MainPart;
            _NextDrawingId = 1;

            var Styles = _Options.Docx.Styles ?? new StyleMapping();
            EnsureStylesPart(MainPart, Styles);
            _NumberingPart = EnsureNumberingPart(MainPart);
            _NextNumberingId = GetNextNumberingId(_NumberingPart.Numbering!);
            _NextAbstractNumberingId = GetNextAbstractNumberingId(_NumberingPart.Numbering!);

            if (MainPart.Document == null)
            {
                MainPart.Document = new Document(new Body());
            }

            var Body = MainPart.Document.Body ?? MainPart.Document.AppendChild(new Body());
            Body.RemoveAllChildren();

            foreach (var Block in Model.Blocks)
            {
                AppendBlock(Body, Block, Styles, 0);
            }

            MainPart.Document.Save();
            _MainPart = null;
            _NumberingPart = null;
        }
    }

    private WordprocessingDocument CreateDocument(Stream OutputStream)
    {
        if (_Options.Docx.UseBuiltinTemplate == false)
        {
            using (var TemplateStream = File.OpenRead(_Options.Docx.TemplatePath))
            {
                TemplateStream.CopyTo(OutputStream);
            }

            if (OutputStream.CanSeek)
            {
                OutputStream.Position = 0;
            }

            return WordprocessingDocument.Open(OutputStream, true);
        }

        return WordprocessingDocument.Create(OutputStream, WordprocessingDocumentType.Document);
    }

    private static void EnsureStylesPart(MainDocumentPart MainPart, StyleMapping Styles)
    {
        var StylePart = MainPart.StyleDefinitionsPart ?? MainPart.AddNewPart<StyleDefinitionsPart>();
        if (MainPart.StyleDefinitionsPart?.Styles != null)
        {
            EnsureGeneratedStyles(MainPart.StyleDefinitionsPart.Styles, Styles);
            MainPart.StyleDefinitionsPart.Styles.Save();
            return;
        }

        var Root = new DocumentFormat.OpenXml.Wordprocessing.Styles();
        Root.AppendChild(new DocDefaults(
            new RunPropertiesDefault(new RunProperties(new RunFonts() { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "Calibri" }, new FontSize() { Val = "22" }, new FontSizeComplexScript() { Val = "22" }))));

        EnsureGeneratedStyles(Root, Styles);
        StylePart.Styles = Root;
    }

    private static void EnsureGeneratedStyles(DocumentFormat.OpenXml.Wordprocessing.Styles Root, StyleMapping Styles)
    {
        AddParagraphStyle(Root, "Normal", "Normal", true, 0);
        AddHeadingParagraphStyle(Root, EnsureStyleId(Styles.Heading1Style, "Heading1"), "Heading 1", 1);
        AddHeadingParagraphStyle(Root, EnsureStyleId(Styles.Heading2Style, "Heading2"), "Heading 2", 2);
        AddHeadingParagraphStyle(Root, EnsureStyleId(Styles.Heading3Style, "Heading3"), "Heading 3", 3);
        AddHeadingParagraphStyle(Root, EnsureStyleId(Styles.Heading4Style, "Heading4"), "Heading 4", 4);
        AddHeadingParagraphStyle(Root, EnsureStyleId(Styles.Heading5Style, "Heading5"), "Heading 5", 5);
        AddHeadingParagraphStyle(Root, EnsureStyleId(Styles.Heading6Style, "Heading6"), "Heading 6", 6);
        AddQuoteParagraphStyle(Root, EnsureStyleId(Styles.QuoteStyle, "IntenseQuote"), "Intense Quote");
        AddCodeBlockParagraphStyle(Root, EnsureStyleId(Styles.CodeBlockStyle, "MdCodeBlock"), "Md Code Block");
        AddCharacterStyle(Root, EnsureStyleId(Styles.InlineCodeStyle, "MdInlineCode"), "Md Inline Code");
        AddHyperlinkStyle(Root);
    }

    private static string EnsureStyleId(string? Value, string Fallback)
    {
        return string.IsNullOrWhiteSpace(Value) ? Fallback : Value.Trim();
    }

    private static void AddParagraphStyle(DocumentFormat.OpenXml.Wordprocessing.Styles Root, string StyleId, string DisplayName, bool Default, int HeadingLevel)
    {
        if (Root.Elements<Style>().Any(Style => Style.StyleId == StyleId))
        {
            return;
        }

        var Style = new Style()
        {
            Type = StyleValues.Paragraph,
            StyleId = StyleId,
            CustomStyle = true,
            Default = Default
        };
        Style.Append(new StyleName() { Val = DisplayName });
        if (!Default)
        {
            Style.Append(new BasedOn() { Val = "Normal" });
            Style.Append(new NextParagraphStyle() { Val = "Normal" });
        }
        if (Default)
        {
            Style.Append(new PrimaryStyle());
        }
        Style.Append(new UIPriority() { Val = Default ? 0 : 9 });
        Style.Append(new UnhideWhenUsed());
        Style.Append(new StyleRunProperties(
            new RunFonts() { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "Calibri" },
            new FontSize() { Val = "22" },
            new FontSizeComplexScript() { Val = "22" }));
        Root.AppendChild(Style);
    }

    private static void AddHeadingParagraphStyle(DocumentFormat.OpenXml.Wordprocessing.Styles Root, string StyleId, string DisplayName, int Level)
    {
        if (Root.Elements<Style>().Any(Style => Style.StyleId == StyleId))
        {
            return;
        }

        var FontSizeVal = Level switch { 1 => "32", 2 => "26", 3 => "24", 4 => "22", 5 => "20", _ => "20" };
        var Style = new Style()
        {
            Type = StyleValues.Paragraph,
            StyleId = StyleId,
            CustomStyle = true,
            Default = false
        };
        Style.Append(new StyleName() { Val = DisplayName });
        Style.Append(new BasedOn() { Val = "Normal" });
        Style.Append(new NextParagraphStyle() { Val = "Normal" });
        Style.Append(new UIPriority() { Val = (int)Level });
        Style.Append(new UnhideWhenUsed());
        Style.Append(new StyleRunProperties(
            new Bold(),
            new RunFonts() { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "Calibri" },
            new FontSize() { Val = FontSizeVal },
            new FontSizeComplexScript() { Val = FontSizeVal }));
        Root.AppendChild(Style);
    }

    private static void AddCharacterStyle(DocumentFormat.OpenXml.Wordprocessing.Styles Root, string StyleId, string DisplayName)
    {
        if (Root.Elements<Style>().Any(Style => Style.StyleId == StyleId))
        {
            return;
        }

        var Style = new Style()
        {
            Type = StyleValues.Character,
            StyleId = StyleId,
            CustomStyle = true
        };
        Style.Append(new StyleName() { Val = DisplayName });
        Style.Append(new UIPriority() { Val = 99 });
        Style.Append(new UnhideWhenUsed());
        Style.Append(new StyleRunProperties(
            new RunFonts() { Ascii = "Consolas", HighAnsi = "Consolas" },
            new FontSize() { Val = "20" },
            new FontSizeComplexScript() { Val = "20" }));
        Root.AppendChild(Style);
    }

    private static void AddQuoteParagraphStyle(DocumentFormat.OpenXml.Wordprocessing.Styles Root, string StyleId, string DisplayName)
    {
        if (Root.Elements<Style>().Any(Style => Style.StyleId == StyleId))
        {
            return;
        }

        var Style = new Style()
        {
            Type = StyleValues.Paragraph,
            StyleId = StyleId,
            CustomStyle = true
        };
        Style.Append(new StyleName() { Val = DisplayName });
        Style.Append(new BasedOn() { Val = "Normal" });
        Style.Append(new NextParagraphStyle() { Val = "Normal" });
        Style.Append(new UIPriority() { Val = 29 });
        Style.Append(new UnhideWhenUsed());
        Style.Append(new StyleParagraphProperties(
            new Indentation() { Left = "720", Right = "360" },
            new SpacingBetweenLines() { Before = "120", After = "120" }));
        Style.Append(new StyleRunProperties(new Italic(), new Color() { Val = "404040" }));
        Root.AppendChild(Style);
    }

    private static void AddCodeBlockParagraphStyle(DocumentFormat.OpenXml.Wordprocessing.Styles Root, string StyleId, string DisplayName)
    {
        if (Root.Elements<Style>().Any(Style => Style.StyleId == StyleId))
        {
            return;
        }

        var Style = new Style()
        {
            Type = StyleValues.Paragraph,
            StyleId = StyleId,
            CustomStyle = true
        };
        Style.Append(new StyleName() { Val = DisplayName });
        Style.Append(new BasedOn() { Val = "Normal" });
        Style.Append(new NextParagraphStyle() { Val = "Normal" });
        Style.Append(new UIPriority() { Val = 99 });
        Style.Append(new UnhideWhenUsed());
        Style.Append(new StyleParagraphProperties(
            new SpacingBetweenLines() { Before = "120", After = "120" },
            new Indentation() { Left = "360" },
            new Shading() { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }));
        Style.Append(new StyleRunProperties(
            new RunFonts() { Ascii = "Consolas", HighAnsi = "Consolas" },
            new FontSize() { Val = "20" },
            new FontSizeComplexScript() { Val = "20" },
            new NoProof()));
        Root.AppendChild(Style);
    }

    private static void AddHyperlinkStyle(DocumentFormat.OpenXml.Wordprocessing.Styles Root)
    {
        if (Root.Elements<Style>().Any(Style => Style.StyleId == "Hyperlink"))
        {
            return;
        }

        var Style = new Style()
        {
            Type = StyleValues.Character,
            StyleId = "Hyperlink"
        };
        Style.Append(new StyleName() { Val = "Hyperlink" });
        Style.Append(new UIPriority() { Val = 99 });
        Style.Append(new UnhideWhenUsed());
        Style.Append(new StyleRunProperties(
            new Color() { Val = "0563C1" },
            new Underline() { Val = UnderlineValues.Single }));
        Root.AppendChild(Style);
    }

    private static NumberingDefinitionsPart EnsureNumberingPart(MainDocumentPart MainPart)
    {
        var NumberingPart = MainPart.NumberingDefinitionsPart ?? MainPart.AddNewPart<NumberingDefinitionsPart>();
        if (NumberingPart.Numbering != null)
        {
            return NumberingPart;
        }

        NumberingPart.Numbering = new Numbering(
            CreateAbstractNumber(BulletAbstractNumId, NumberFormatValues.Bullet),
            CreateAbstractNumber(DecimalAbstractNumId, NumberFormatValues.Decimal),
            new NumberingInstance(new AbstractNumId() { Val = BulletAbstractNumId }) { NumberID = BulletNumId },
            new NumberingInstance(new AbstractNumId() { Val = DecimalAbstractNumId }) { NumberID = DecimalNumId });
        return NumberingPart;
    }

    private static int GetNextNumberingId(Numbering Numbering)
    {
        var MaxNumberingId = Numbering.Elements<NumberingInstance>()
            .Select(Instance => Instance.NumberID?.Value ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return MaxNumberingId + 1;
    }

    private static int GetNextAbstractNumberingId(Numbering Numbering)
    {
        var MaxAbstractNumberingId = Numbering.Elements<AbstractNum>()
            .Select(AbstractNum => AbstractNum.AbstractNumberId?.Value ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return MaxAbstractNumberingId + 1;
    }

    private static AbstractNum CreateAbstractNumber(int AbstractNumberId, NumberFormatValues Format, int StartNumber = 1, int StartLevel = 0)
    {
        var AbstractNum = new AbstractNum() { AbstractNumberId = AbstractNumberId };
        AbstractNum.Append(
            new Nsid() { Val = AbstractNumberId.ToString("X8") },
            new MultiLevelType() { Val = MultiLevelValues.Multilevel },
            new TemplateCode() { Val = (AbstractNumberId + 4096).ToString("X8") });
        for (var LevelIndex = 0; LevelIndex < 9; LevelIndex++)
        {
            var Level = new Level(
                new StartNumberingValue() { Val = LevelIndex == StartLevel ? StartNumber : 1 },
                new NumberingFormat() { Val = Format },
                new LevelText() { Val = Format == NumberFormatValues.Bullet ? "\uF0B7" : "%" + (LevelIndex + 1) + "." },
                new ParagraphProperties(
                    new Indentation() { Left = (720 + (LevelIndex * 360)).ToString(), Hanging = "360" }))
            { LevelIndex = LevelIndex };
            if (Format == NumberFormatValues.Bullet)
            {
                Level.Append(new RunProperties(new RunFonts() { Ascii = "Symbol", HighAnsi = "Symbol" }));
            }
            AbstractNum.Append(Level);
        }
        return AbstractNum;
    }

    private void AppendBlock(OpenXmlElement Parent, BlockNode Block, StyleMapping Styles, int ListLevel)
    {
        if (Block is ParagraphBlock Paragraph)
        {
            var P = CreateParagraph(Paragraph, Styles.ParagraphStyle, Styles);
            Parent.AppendChild(P);
            return;
        }

        if (Block is HeadingBlock Heading)
        {
            var StyleId = GetHeadingStyle(Heading.Level, Styles);
            var P = CreateParagraphFromInlines(Heading.Inlines, StyleId, Styles);
            Parent.AppendChild(P);
            return;
        }

        if (Block is CodeBlock Code)
        {
            var P = CreateCodeBlockParagraph(Code, Styles, null);
            Parent.AppendChild(P);
            return;
        }

        if (Block is MermaidBlock Mermaid)
        {
            Parent.AppendChild(CreateMermaidParagraph(Mermaid, Styles, null));
            return;
        }

        if (Block is QuoteBlock Quote)
        {
            foreach (var Child in Quote.Blocks)
            {
                AppendQuoteBlock(Parent, Child, Styles, ListLevel);
            }
            return;
        }

        if (Block is ListBlock List)
        {
            var NumberingId = GetNumberingId(List, ListLevel);
            foreach (var Item in List.Items)
            {
                var IsFirstListParagraph = true;
                foreach (var Child in Item.Blocks)
                {
                    AppendListItemBlock(Parent, Child, Styles, NumberingId, ListLevel, ref IsFirstListParagraph, Styles.ParagraphStyle);
                }
            }
            return;
        }

        if (Block is TableBlock TableBlock)
        {
            var T = new DocumentFormat.OpenXml.Wordprocessing.Table();
            T.AppendChild(CreateTableProperties(Styles));
            foreach (var Row in TableBlock.Rows)
            {
                var Tr = new DocumentFormat.OpenXml.Wordprocessing.TableRow();
                foreach (var Cell in Row.Cells)
                {
                    var Tc = new DocumentFormat.OpenXml.Wordprocessing.TableCell();
                    Tc.AppendChild(new TableCellProperties(new TableCellWidth() { Type = TableWidthUnitValues.Auto }));
                    var ColumnIndex = Tr.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>().Count();
                    foreach (var CellBlock in Cell.Blocks)
                    {
                        var P = new Paragraph(CreateTableCellParagraphProperties(Styles, TableBlock, ColumnIndex));
                        AppendBlockContent(P, CellBlock, Styles);
                        Tc.AppendChild(P);
                    }
                    Tr.AppendChild(Tc);
                }
                T.AppendChild(Tr);
            }
            Parent.AppendChild(T);
            return;
        }

        if (Block is HorizontalRuleBlock)
        {
            Parent.AppendChild(CreateHorizontalRuleParagraph());
        }
    }

    private static Paragraph CreateHorizontalRuleParagraph()
    {
        return new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines() { Before = "180", After = "180" },
                new ParagraphBorders(
                    new BottomBorder() { Val = BorderValues.Single, Size = 6, Space = 1, Color = "808080" })),
            new Run(CreateText(string.Empty)));
    }

    private static TableProperties CreateTableProperties(StyleMapping Styles)
    {
        var Properties = new TableProperties(
            new TableStyle() { Val = Styles.TableStyle },
            new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder() { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                new BottomBorder() { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                new LeftBorder() { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                new RightBorder() { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" }),
            new TableCellMarginDefault(
                new TopMargin() { Width = "80", Type = TableWidthUnitValues.Dxa },
                new BottomMargin() { Width = "80", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin() { Width = 120, Type = TableWidthValues.Dxa },
                new TableCellRightMargin() { Width = 120, Type = TableWidthValues.Dxa }));
        return Properties;
    }

    private void AppendQuoteBlock(OpenXmlElement Parent, BlockNode Block, StyleMapping Styles, int ListLevel)
    {
        if (Block is ListBlock List)
        {
            var QuoteListLevel = 0;
            var NumberingId = GetNumberingId(List, QuoteListLevel);
            foreach (var Item in List.Items)
            {
                var IsFirstListParagraph = true;
                foreach (var Child in Item.Blocks)
                {
                    AppendListItemBlock(Parent, Child, Styles, NumberingId, QuoteListLevel, ref IsFirstListParagraph, Styles.QuoteStyle);
                }
            }
            return;
        }

        if (Block is CodeBlock Code)
        {
            Parent.AppendChild(CreateCodeBlockParagraph(Code, Styles, ListLevel + 1));
            return;
        }

        if (Block is MermaidBlock Mermaid)
        {
            Parent.AppendChild(CreateMermaidParagraph(Mermaid, Styles, ListLevel + 1));
            return;
        }

        var P = new Paragraph(new ParagraphProperties(new ParagraphStyleId() { Val = Styles.QuoteStyle }));
        AppendBlockContent(P, Block, Styles);
        Parent.AppendChild(P);
    }

    private ParagraphProperties CreateTableCellParagraphProperties(StyleMapping Styles, TableBlock TableBlock, int ColumnIndex)
    {
        var Properties = new ParagraphProperties(new ParagraphStyleId() { Val = Styles.ParagraphStyle });
        if (_Options.Tables.EnableAlignment && ColumnIndex < TableBlock.ColumnAlignments.Count)
        {
            JustificationValues? Alignment = TableBlock.ColumnAlignments[ColumnIndex] switch
            {
                TableAlignment.Center => JustificationValues.Center,
                TableAlignment.Right => JustificationValues.Right,
                _ => null
            };
            if (Alignment != null)
            {
                Properties.Append(new Justification() { Val = Alignment.Value });
            }
        }
        return Properties;
    }

    private int GetNumberingId(ListBlock List, int ListLevel)
    {
        var AbstractNumberingId = _NextAbstractNumberingId++;
        var NumberingId = _NextNumberingId++;

        var AbstractNum = CreateAbstractNumber(
            AbstractNumberingId,
            List.IsOrdered ? NumberFormatValues.Decimal : NumberFormatValues.Bullet,
            List.IsOrdered && List.StartNumber.HasValue ? List.StartNumber.Value : 1,
            ListLevel);
        var Instance = new NumberingInstance(new AbstractNumId() { Val = AbstractNumberingId })
        {
            NumberID = NumberingId
        };

        var Numbering = _NumberingPart!.Numbering!;
        OpenXmlElement? FirstNumberingInstance = Numbering.Elements<NumberingInstance>().FirstOrDefault();
        if (FirstNumberingInstance != null)
        {
            Numbering.InsertBefore(AbstractNum, FirstNumberingInstance);
        }
        else
        {
            Numbering.Append(AbstractNum);
        }
        _NumberingPart!.Numbering!.Append(Instance);
        return NumberingId;
    }

    private void AppendListItemBlock(OpenXmlElement Parent, BlockNode Block, StyleMapping Styles, int NumberingId, int ListLevel, ref bool IsFirstListParagraph, string ParagraphStyleId)
    {
        if (Block is ListBlock NestedList)
        {
            AppendBlock(Parent, NestedList, Styles, ListLevel + 1);
            return;
        }

        if (Block is CodeBlock Code)
        {
            Parent.AppendChild(CreateCodeBlockParagraph(Code, Styles, ListLevel));
            return;
        }

        if (Block is MermaidBlock Mermaid)
        {
            Parent.AppendChild(CreateMermaidParagraph(Mermaid, Styles, ListLevel));
            return;
        }

        var Properties = new ParagraphProperties(new ParagraphStyleId() { Val = ParagraphStyleId });
        if (IsFirstListParagraph)
        {
            Properties.Append(new NumberingProperties(
                new NumberingLevelReference() { Val = ListLevel },
                new NumberingId() { Val = NumberingId }));
            IsFirstListParagraph = false;
        }
        else
        {
            Properties.Append(new Indentation() { Left = (720 + (ListLevel * 360)).ToString() });
        }

        var P = new Paragraph(Properties);
        AppendBlockContent(P, Block, Styles);
        Parent.AppendChild(P);
    }

    private static Paragraph CreateCodeBlockParagraph(CodeBlock Code, StyleMapping Styles, int? ListLevel)
    {
        var Properties = new ParagraphProperties(new ParagraphStyleId() { Val = Styles.CodeBlockStyle });
        if (ListLevel.HasValue)
        {
            Properties.Append(new Indentation() { Left = (1080 + (ListLevel.Value * 360)).ToString() });
        }

        var Paragraph = new Paragraph(Properties);
        AppendCodeText(Paragraph, Code.Content);
        return Paragraph;
    }

    private Paragraph CreateMermaidParagraph(MermaidBlock MermaidBlock, StyleMapping Styles, int? ListLevel)
    {
        var Properties = new ParagraphProperties(new ParagraphStyleId() { Val = Styles.ParagraphStyle });
        if (ListLevel.HasValue)
        {
            Properties.Append(new Indentation() { Left = (1080 + (ListLevel.Value * 360)).ToString() });
        }
        Properties.Append(new Justification() { Val = JustificationValues.Center });

        var Paragraph = new Paragraph(Properties);
        try
        {
            var Svg = Mermaid.Render(MermaidBlock.Source, RenderOptions.Default);
            if (TryAppendSvg(Paragraph, Svg, "Mermaid diagram"))
            {
                return Paragraph;
            }

            AppendMermaidFallback(Paragraph, "Mermaid diagram could not be embedded as SVG.", MermaidBlock.Source);
        }
        catch (Exception Ex)
        {
            AppendMermaidFallback(Paragraph, "Mermaid render failed: " + Ex.Message, MermaidBlock.Source);
        }

        return Paragraph;
    }

    private static void AppendMermaidFallback(Paragraph Paragraph, string Message, string Source)
    {
        Paragraph.AppendChild(new Run(new RunProperties(new Bold()), CreateText("[Mermaid diagram not rendered] ")));
        Paragraph.AppendChild(new Run(CreateText(Message)));
        Paragraph.AppendChild(new Run(new Break()));
        AppendCodeText(Paragraph, Source);
    }

    private static void AppendCodeText(Paragraph Paragraph, string Content)
    {
        var NormalizedContent = (Content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
        var Lines = NormalizedContent.Split('\n');
        var Run = new Run(new RunProperties(new RunFonts() { Ascii = "Consolas", HighAnsi = "Consolas" }, new NoProof()));
        for (var I = 0; I < Lines.Length; I++)
        {
            if (I > 0)
            {
                Run.Append(new Break());
            }
            Run.Append(CreateText(Lines[I]));
        }
        Paragraph.AppendChild(Run);
    }

    private void AppendBlockContent(Paragraph Parent, BlockNode Block, StyleMapping Styles)
    {
        if (Block is ParagraphBlock Paragraph)
        {
            foreach (var Inline in Paragraph.Inlines)
            {
                AppendInline(Parent, Inline, Styles);
            }
        }
    }

    private Paragraph CreateParagraph(ParagraphBlock Paragraph, string StyleId, StyleMapping Styles)
    {
        var P = new Paragraph(new ParagraphProperties(new ParagraphStyleId() { Val = StyleId }));
        foreach (var Inline in Paragraph.Inlines)
        {
            AppendInline(P, Inline, Styles);
        }
        return P;
    }

    private Paragraph CreateParagraphFromInlines(IReadOnlyList<InlineNode> Inlines, string StyleId, StyleMapping Styles)
    {
        var P = new Paragraph(new ParagraphProperties(new ParagraphStyleId() { Val = StyleId }));
        foreach (var Inline in Inlines)
        {
            AppendInline(P, Inline, Styles);
        }
        return P;
    }

    private static string GetHeadingStyle(int Level, StyleMapping Styles)
    {
        switch (Level)
        {
            case 1: return Styles.Heading1Style;
            case 2: return Styles.Heading2Style;
            case 3: return Styles.Heading3Style;
            case 4: return Styles.Heading4Style;
            case 5: return Styles.Heading5Style;
            case 6: return Styles.Heading6Style;
            default: return Styles.ParagraphStyle;
        }
    }

    private void AppendInline(Paragraph Parent, InlineNode Inline, StyleMapping Styles)
    {
        if (Inline is TextRun Text)
        {
            Parent.AppendChild(new Run(CreateText(Text.Text)));
            return;
        }
        if (Inline is CodeInline Code)
        {
            var Style = Styles != null ? Styles.InlineCodeStyle : "MdInlineCode";
            Parent.AppendChild(new Run(new RunProperties(new RunStyle() { Val = Style }), CreateText(Code.Content)));
            return;
        }
        if (Inline is EmphasisInline Emphasis)
        {
            var Run = new Run(new RunProperties(new Italic()));
            foreach (var Child in Emphasis.Inlines)
            {
                AppendInlineToRun(Run, Child, Styles);
            }
            Parent.AppendChild(Run);
            return;
        }
        if (Inline is StrongInline Strong)
        {
            var Run = new Run(new RunProperties(new Bold()));
            foreach (var Child in Strong.Inlines)
            {
                AppendInlineToRun(Run, Child, Styles);
            }
            Parent.AppendChild(Run);
            return;
        }
        if (Inline is StrikethroughInline Strikethrough)
        {
            var Run = new Run(new RunProperties(new Strike()));
            foreach (var Child in Strikethrough.Inlines)
            {
                AppendInlineToRun(Run, Child, Styles);
            }
            Parent.AppendChild(Run);
            return;
        }
        if (Inline is LinkInline Link)
        {
            if (ShouldWriteExternalLink(Link.Url))
            {
                var RelationshipId = _MainPart!.AddHyperlinkRelationship(new Uri(Link.Url, UriKind.Absolute), true).Id!;
                var H = new Hyperlink() { History = true, Id = RelationshipId };
                foreach (var Child in Link.Inlines)
                {
                    AppendInlineToHyperlink(H, Child, Styles);
                }
                Parent.AppendChild(H);
                return;
            }

            foreach (var Child in Link.Inlines)
            {
                AppendInline(Parent, Child, Styles);
            }
            return;
        }
        if (Inline is ImageInline Image)
        {
            if (!TryAppendImage(Parent, Image))
            {
                var Placeholder = FormatMissingImagePlaceholder(Image.AltText, Image.SourceUri ?? Image.Url, _Options.Images);
                Parent.AppendChild(new Run(CreateText(Placeholder)));
            }
            return;
        }

        if (Inline is BreakInline)
        {
            Parent.AppendChild(new Run(new Break()));
            return;
        }

        if (Inline is TaskCheckboxInline TaskCheckbox)
        {
            var CheckboxText = TaskCheckbox.Checked ? "[x]" : "[ ]";
            Parent.AppendChild(new Run(CreateText(CheckboxText)));
            return;
        }
    }

    private bool ShouldWriteExternalLink(string Url)
    {
        if (!_Options.Links.EnableExternalLinks || string.IsNullOrWhiteSpace(Url))
        {
            return false;
        }

        return Uri.TryCreate(Url, UriKind.Absolute, out var ParsedUri)
            && (ParsedUri.Scheme == Uri.UriSchemeHttp || ParsedUri.Scheme == Uri.UriSchemeHttps || ParsedUri.Scheme == Uri.UriSchemeMailto);
    }

    private static void AppendInlineToHyperlink(Hyperlink Parent, InlineNode Inline, StyleMapping Styles)
    {
        if (Inline is TextRun Text)
        {
            Parent.AppendChild(new Run(CreateHyperlinkRunProperties(), CreateText(Text.Text)));
            return;
        }
        if (Inline is CodeInline Code)
        {
            Parent.AppendChild(new Run(new RunProperties(new RunStyle() { Val = Styles.InlineCodeStyle }, new Color() { Val = "0563C1" }, new Underline() { Val = UnderlineValues.Single }), CreateText(Code.Content)));
            return;
        }
        if (Inline is EmphasisInline Emphasis)
        {
            var Run = new Run(CreateHyperlinkRunProperties(new Italic()));
            foreach (var Child in Emphasis.Inlines)
            {
                AppendInlineToRun(Run, Child, Styles);
            }
            Parent.AppendChild(Run);
            return;
        }
        if (Inline is StrongInline Strong)
        {
            var Run = new Run(CreateHyperlinkRunProperties(new Bold()));
            foreach (var Child in Strong.Inlines)
            {
                AppendInlineToRun(Run, Child, Styles);
            }
            Parent.AppendChild(Run);
            return;
        }
        if (Inline is StrikethroughInline Strikethrough)
        {
            var Run = new Run(CreateHyperlinkRunProperties(new Strike()));
            foreach (var Child in Strikethrough.Inlines)
            {
                AppendInlineToRun(Run, Child, Styles);
            }
            Parent.AppendChild(Run);
            return;
        }
    }

    private static void AppendInlineToRun(Run Parent, InlineNode Inline, StyleMapping Styles)
    {
        if (Inline is TextRun Text)
        {
            Parent.AppendChild(CreateText(Text.Text));
            return;
        }
        if (Inline is CodeInline Code)
        {
            Parent.AppendChild(CreateText(Code.Content));
            return;
        }
        if (Inline is BreakInline)
        {
            Parent.AppendChild(new Break());
            return;
        }
        if (Inline is TaskCheckboxInline TaskCheckbox)
        {
            Parent.AppendChild(CreateText(TaskCheckbox.Checked ? "[x]" : "[ ]"));
            return;
        }
        if (Inline is EmphasisInline Emphasis)
        {
            var RunProperties = Parent.GetFirstChild<RunProperties>();
            if (RunProperties != null && !RunProperties.Elements<Italic>().Any())
            {
                RunProperties.Append(new Italic());
            }
            foreach (var Child in Emphasis.Inlines)
            {
                AppendInlineToRun(Parent, Child, Styles);
            }
            return;
        }
        if (Inline is StrongInline Strong)
        {
            var RunProperties = Parent.GetFirstChild<RunProperties>();
            if (RunProperties != null && !RunProperties.Elements<Bold>().Any())
            {
                RunProperties.Append(new Bold());
            }
            foreach (var Child in Strong.Inlines)
            {
                AppendInlineToRun(Parent, Child, Styles);
            }
            return;
        }
        if (Inline is StrikethroughInline Strikethrough)
        {
            var RunProperties = Parent.GetFirstChild<RunProperties>();
            if (RunProperties != null && !RunProperties.Elements<Strike>().Any())
            {
                RunProperties.Append(new Strike());
            }
            foreach (var Child in Strikethrough.Inlines)
            {
                AppendInlineToRun(Parent, Child, Styles);
            }
            return;
        }
    }

    private static string FormatMissingImagePlaceholder(string AltText, string Uri, ImageOptions ImageOptions)
    {
        var Format = ImageOptions?.MissingImagePlaceholderFormat ?? "[Missing image: {AltText}]";
        return Format
            .Replace("{AltText}", AltText ?? "unknown")
            .Replace("{Uri}", Uri ?? "unknown");
    }

    private static Text CreateText(string Value)
    {
        return new Text(Value ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve };
    }

    private bool TryAppendImage(Paragraph Parent, ImageInline Image)
    {
        var Source = Image.SourceUri ?? Image.Url;
        var MainPart = _MainPart!;
        if (MainPart == null
            || string.IsNullOrWhiteSpace(Source)
            || Uri.TryCreate(Source, UriKind.Absolute, out var ParsedUri) && !ParsedUri.IsFile
            || !File.Exists(Source))
        {
            return false;
        }

        var ImagePartType = GetImagePartType(Source);
        if (!ImagePartType.HasValue)
        {
            return false;
        }

        var ImagePart = MainPart.AddImagePart(ImagePartType.Value);
        using (var ImageStream = File.OpenRead(Source))
        {
            ImagePart.FeedData(ImageStream);
        }

        var RelationshipId = MainPart.GetIdOfPart(ImagePart);
        var Dimensions = GetImageDimensions(Source);
        (var Cx, var Cy) = CalculateImageExtents(Dimensions);
        var DrawingId = _NextDrawingId++;

        var Picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties() { Id = 0U, Name = Path.GetFileName(Source), Description = Image.AltText ?? string.Empty },
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip() { Embed = RelationshipId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset() { X = 0L, Y = 0L },
                    new A.Extents() { Cx = Cx, Cy = Cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        var GraphicData = new A.GraphicData(Picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        var Inline = new DW.Inline(
            new DW.Extent() { Cx = Cx, Cy = Cy },
            new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties() { Id = DrawingId, Name = "Picture " + DrawingId, Description = Image.AltText ?? string.Empty },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
            new A.Graphic(GraphicData))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };
        Parent.AppendChild(new Run(new Drawing(Inline)));
        return true;
    }

    private bool TryAppendSvg(Paragraph Parent, string Svg, string AltText)
    {
        var MainPart = _MainPart!;
        if (MainPart == null || string.IsNullOrWhiteSpace(Svg))
        {
            return false;
        }

        var ImagePart = MainPart.AddImagePart(ImagePartType.Svg);
        var SvgBytes = Encoding.UTF8.GetBytes(Svg);
        using (var Stream = new MemoryStream(SvgBytes))
        {
            ImagePart.FeedData(Stream);
        }

        var RelationshipId = MainPart.GetIdOfPart(ImagePart);
        var Dimensions = GetSvgDimensions(Svg);
        (var Cx, var Cy) = CalculateSvgExtents(Dimensions);
        var DrawingId = _NextDrawingId++;

        var Picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "Mermaid diagram.svg", Description = AltText ?? string.Empty },
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip() { Embed = RelationshipId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset() { X = 0L, Y = 0L },
                    new A.Extents() { Cx = Cx, Cy = Cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        var GraphicData = new A.GraphicData(Picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        var Inline = new DW.Inline(
            new DW.Extent() { Cx = Cx, Cy = Cy },
            new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties() { Id = DrawingId, Name = "Mermaid diagram " + DrawingId, Description = AltText ?? string.Empty },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
            new A.Graphic(GraphicData))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };
        Parent.AppendChild(new Run(new Drawing(Inline)));
        return true;
    }

    private (long Cx, long Cy) CalculateSvgExtents(SvgDimensions Dimensions)
    {
        const double EmusPerCm = 360000.0;
        const double DefaultDpi = 96.0;
        var MaxWidthCm = _Options.Images.MaxWidthCm > 0 ? _Options.Images.MaxWidthCm : 16.0;
        var TargetWidthCm = Math.Min(MaxWidthCm, 14.5);
        const double MaxHeightCm = 18.0;
        var AspectRatio = Dimensions.WidthPixels > 0 && Dimensions.HeightPixels > 0
            ? (double)Dimensions.HeightPixels / Dimensions.WidthPixels
            : 0.5625;

        var NaturalWidthCm = Dimensions.WidthPixels > 0
            ? Dimensions.WidthPixels / DefaultDpi * 2.54
            : TargetWidthCm;
        var WidthCm = NaturalWidthCm;
        if (WidthCm <= 0)
        {
            WidthCm = TargetWidthCm;
        }
        WidthCm = Math.Min(WidthCm, TargetWidthCm);
        var HeightCm = WidthCm * AspectRatio;
        if (WidthCm > MaxWidthCm)
        {
            WidthCm = MaxWidthCm;
            HeightCm = WidthCm * AspectRatio;
        }

        if (HeightCm > MaxHeightCm)
        {
            HeightCm = MaxHeightCm;
            WidthCm = HeightCm / AspectRatio;
        }

        return ((long)Math.Round(WidthCm * EmusPerCm), (long)Math.Round(HeightCm * EmusPerCm));
    }

    private static SvgDimensions GetSvgDimensions(string Svg)
    {
        try
        {
            var Document = XDocument.Parse(Svg);
            var Root = Document.Root;
            if (Root == null)
            {
                return new SvgDimensions(800, 450);
            }

            var Width = ParseSvgLength(Root.Attribute("width")?.Value);
            var Height = ParseSvgLength(Root.Attribute("height")?.Value);
            if ((Width <= 0 || Height <= 0) && TryParseViewBox(Root.Attribute("viewBox")?.Value, out var ViewBoxWidth, out var ViewBoxHeight))
            {
                Width = Width > 0 ? Width : ViewBoxWidth;
                Height = Height > 0 ? Height : ViewBoxHeight;
            }

            if (Width <= 0 || Height <= 0)
            {
                return new SvgDimensions(800, 450);
            }

            return new SvgDimensions((int)Math.Ceiling(Width), (int)Math.Ceiling(Height));
        }
        catch
        {
            return new SvgDimensions(800, 450);
        }
    }

    private static double ParseSvgLength(string? Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            return 0;
        }

        var Trimmed = Value.Trim();
        if (Trimmed.EndsWith('%'))
        {
            return 0;
        }

        var End = 0;
        while (End < Trimmed.Length && (char.IsDigit(Trimmed[End]) || Trimmed[End] == '.' || Trimmed[End] == '-'))
        {
            End++;
        }

        if (End == 0)
        {
            return 0;
        }

        ReadOnlySpan<char> TrimmedSpan = Trimmed.AsSpan(0, End);
        return double.TryParse(TrimmedSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var Result) ? Result : 0;
    }

    private static bool TryParseViewBox(string? Value, out double Width, out double Height)
    {
        Width = 0;
        Height = 0;
        if (string.IsNullOrWhiteSpace(Value))
        {
            return false;
        }

        var Parts = Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (Parts.Length != 4)
        {
            return false;
        }

        return double.TryParse(Parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out Width)
            && double.TryParse(Parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out Height);
    }

    private static PartTypeInfo? GetImagePartType(string Path)
    {
        return System.IO.Path.GetExtension(Path).ToLowerInvariant() switch
        {
            ".png" => ImagePartType.Png,
            ".jpg" => ImagePartType.Jpeg,
            ".jpeg" => ImagePartType.Jpeg,
            ".gif" => ImagePartType.Gif,
            ".bmp" => ImagePartType.Bmp,
            ".tif" => ImagePartType.Tiff,
            ".tiff" => ImagePartType.Tiff,
            _ => null
        };
    }

    private (long Cx, long Cy) CalculateImageExtents(ImageDimensions Dimensions)
    {
        const double EmusPerCm = 360000.0;
        const double DefaultDpi = 96.0;

        var MaxWidthCm = _Options.Images.MaxWidthCm > 0 ? _Options.Images.MaxWidthCm : 16.0;
        var WidthCm = MaxWidthCm;
        double HeightCm;

        if (Dimensions.WidthPixels > 0 && Dimensions.HeightPixels > 0)
        {
            var NaturalWidthCm = Dimensions.WidthPixels / DefaultDpi * 2.54;
            WidthCm = _Options.Images.ScaleDownOnly ? Math.Min(NaturalWidthCm, MaxWidthCm) : MaxWidthCm;
            HeightCm = WidthCm * Dimensions.HeightPixels / Dimensions.WidthPixels;
        }
        else
        {
            HeightCm = WidthCm * 0.5625;
        }

        return ((long)Math.Round(WidthCm * EmusPerCm), (long)Math.Round(HeightCm * EmusPerCm));
    }

    private static ImageDimensions GetImageDimensions(string Path)
    {
        try
        {
            using (var Stream = File.OpenRead(Path))
            using (var Reader = new BinaryReader(Stream))
            {
                var Header = Reader.ReadBytes(24);
                if (Header.Length >= 24
                    && Header[0] == 0x89
                    && Header[1] == 0x50
                    && Header[2] == 0x4E
                    && Header[3] == 0x47)
                {
                    return new ImageDimensions(ReadBigEndianInt32(Header, 16), ReadBigEndianInt32(Header, 20));
                }

                if (Header.Length >= 10 && Header[0] == 0x47 && Header[1] == 0x49 && Header[2] == 0x46)
                {
                    return new ImageDimensions(Header[6] + (Header[7] << 8), Header[8] + (Header[9] << 8));
                }

                if (Header.Length >= 26 && Header[0] == 0x42 && Header[1] == 0x4D)
                {
                    return new ImageDimensions(BitConverter.ToInt32(Header, 18), Math.Abs(BitConverter.ToInt32(Header, 22)));
                }

                Stream.Position = 0;
                if (Reader.ReadByte() == 0xFF && Reader.ReadByte() == 0xD8)
                {
                    while (Stream.Position < Stream.Length)
                    {
                        if (Reader.ReadByte() != 0xFF)
                        {
                            continue;
                        }

                        var Marker = Reader.ReadByte();
                        var Length = ReadBigEndianUInt16(Reader);
                        if (Marker is >= 0xC0 and <= 0xC3)
                        {
                            Reader.ReadByte();
                            int Height = ReadBigEndianUInt16(Reader);
                            int Width = ReadBigEndianUInt16(Reader);
                            return new ImageDimensions(Width, Height);
                        }

                        Stream.Position += Length - 2;
                    }
                }
            }
        }
        catch
        {
            return default;
        }

        return default;
    }

    private static int ReadBigEndianInt32(byte[] Bytes, int Offset)
    {
        return (Bytes[Offset] << 24) | (Bytes[Offset + 1] << 16) | (Bytes[Offset + 2] << 8) | Bytes[Offset + 3];
    }

    private static ushort ReadBigEndianUInt16(BinaryReader Reader)
    {
        var High = Reader.ReadByte();
        var Low = Reader.ReadByte();
        return (ushort)((High << 8) | Low);
    }

    private static RunProperties CreateHyperlinkRunProperties(params OpenXmlElement[] ExtraProperties)
    {
        var Properties = new RunProperties(
            new RunStyle() { Val = "Hyperlink" },
            new Color() { Val = "0563C1" },
            new Underline() { Val = UnderlineValues.Single });
        foreach (var ExtraProperty in ExtraProperties)
        {
            Properties.Append(ExtraProperty);
        }
        return Properties;
    }

    private readonly struct ImageDimensions
    {
        public int WidthPixels { get; }

        public int HeightPixels { get; }

        public ImageDimensions(int WidthPixels, int HeightPixels)
        {
            this.WidthPixels = WidthPixels;
            this.HeightPixels = HeightPixels;
        }
    }

    private readonly struct SvgDimensions
    {
        public int WidthPixels { get; }

        public int HeightPixels { get; }

        public SvgDimensions(int WidthPixels, int HeightPixels)
        {
            this.WidthPixels = WidthPixels;
            this.HeightPixels = HeightPixels;
        }
    }
}
