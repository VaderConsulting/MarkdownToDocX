using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MermaidSharp;
using MDConverter.Model;
using MDConverter.Options;
using MermaidRecognition.NaiadAdapter;
using SkiaSharp;
using Svg.Skia;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace MDConverter.Pipeline;

/// <summary>
/// Writes the internal DocModel to a DOCX stream using the Open XML SDK.
/// </summary>
public sealed class DocxWriter(EffectiveOptions Options, IConversionProgressReporter? ProgressReporter)
{
    private const float MermaidRasterScale = 4.0f;
    private const double FlowchartRasterArrowLengthPx = 8.0;
    private const double FlowchartRasterArrowHalfWidthPx = 5.0;
    private const string FLOWCHART_RASTER_DEFAULT_ARROW_FILL = "#333333";
    private static readonly Regex SvgInlineStrokeColourRegex =
        new(@"stroke\s*:\s*([^;""]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SvgTintedPointMarkerColourSuffixRegex =
        new(@"flowchart-v2-point(?:End|Start)(?:Thick|-margin)?--([0-9a-fA-F]{3,8})\)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SvgPathEndPointRegex =
        new(@"L\s*([-\d.]+)[,\s]+([-\d.]+)\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SvgPathStartPointRegex =
        new(@"M\s*([-\d.]+)[,\s]+([-\d.]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const int BulletAbstractNumId = 1;
    private const int DecimalAbstractNumId = 2;
    private const int BulletNumId = 1;
    private const int DecimalNumId = 2;

    private readonly EffectiveOptions _Options = Options ?? throw new ArgumentNullException(nameof(Options));
    private readonly IConversionProgressReporter? _ProgressReporter = ProgressReporter;
    private MainDocumentPart? _MainPart;
    private NumberingDefinitionsPart? _NumberingPart;
    private int _NextNumberingId = 3;
    private int _NextAbstractNumberingId = 3;
    private uint _NextDrawingId = 1;
    private readonly Dictionary<HeadingBlock, string> _HeadingBookmarks = [];
    private readonly Dictionary<string, string> _AnchorBookmarks = new(StringComparer.Ordinal);
    private int _NextBookmarkId;

    public DocxWriter(EffectiveOptions Options)
        : this(Options, null)
    {
    }

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

        using (WordprocessingDocument Document = CreateDocument(OutputStream))
        {
            MainDocumentPart MainPart = Document.MainDocumentPart ?? Document.AddMainDocumentPart();
            _MainPart = MainPart;
            _NextDrawingId = 1;

            StyleMapping Styles = _Options.Docx.Styles ?? new StyleMapping();
            EnsureStylesPart(MainPart, Styles);
            _NumberingPart = EnsureNumberingPart(MainPart);
            _NextNumberingId = GetNextNumberingId(_NumberingPart.Numbering!);
            _NextAbstractNumberingId = GetNextAbstractNumberingId(_NumberingPart.Numbering!);

            if (MainPart.Document == null)
            {
                MainPart.Document = new Document(new Body());
            }

            Body Body = MainPart.Document.Body ?? MainPart.Document.AppendChild(new Body());
            SectionProperties? TemplateSection = Body.GetFirstChild<SectionProperties>();
            TemplateSection?.Remove();
            Body.RemoveAllChildren();
            PrepareHeadingBookmarks(Model.Blocks);

            int BlockCount = Model.Blocks.Count;
            for (int BlockIndex = 0; BlockIndex < BlockCount; BlockIndex++)
            {
                BlockNode Block = Model.Blocks[BlockIndex];
                if (BlockCount > 0 && _ProgressReporter != null)
                {
                    int WritePercent = 15 + (int)Math.Round(80.0 * (BlockIndex + 1) / BlockCount);
                    _ProgressReporter.Report(WritePercent, DescribeBlockProgress(Block, BlockIndex + 1, BlockCount));
                }

                AppendBlock(Body, Block, Styles, 0);
            }

            if (TemplateSection != null)
            {
                Body.AppendChild(TemplateSection);
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
            using (FileStream TemplateStream = File.OpenRead(_Options.Docx.TemplatePath))
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
        StyleDefinitionsPart StylePart = MainPart.StyleDefinitionsPart ?? MainPart.AddNewPart<StyleDefinitionsPart>();
        if (MainPart.StyleDefinitionsPart?.Styles != null)
        {
            EnsureGeneratedStyles(MainPart.StyleDefinitionsPart.Styles, Styles);
            MainPart.StyleDefinitionsPart.Styles.Save();
            return;
        }

        DocumentFormat.OpenXml.Wordprocessing.Styles Root = new DocumentFormat.OpenXml.Wordprocessing.Styles();
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

        Style Style = new Style()
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

        string FontSizeVal = Level switch { 1 => "32", 2 => "26", 3 => "24", 4 => "22", 5 => "20", _ => "20" };
        Style Style = new Style()
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

        Style Style = new Style()
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

        Style Style = new Style()
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

        Style Style = new Style()
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

        Style Style = new Style()
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
        NumberingDefinitionsPart NumberingPart = MainPart.NumberingDefinitionsPart ?? MainPart.AddNewPart<NumberingDefinitionsPart>();
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
        int MaxNumberingId = Numbering.Elements<NumberingInstance>()
            .Select(Instance => Instance.NumberID?.Value ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return MaxNumberingId + 1;
    }

    private static int GetNextAbstractNumberingId(Numbering Numbering)
    {
        int MaxAbstractNumberingId = Numbering.Elements<AbstractNum>()
            .Select(AbstractNum => AbstractNum.AbstractNumberId?.Value ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return MaxAbstractNumberingId + 1;
    }

    private static AbstractNum CreateAbstractNumber(int AbstractNumberId, NumberFormatValues Format, int StartNumber = 1, int StartLevel = 0)
    {
        AbstractNum AbstractNum = new AbstractNum() { AbstractNumberId = AbstractNumberId };
        AbstractNum.Append(
            new Nsid() { Val = AbstractNumberId.ToString("X8") },
            new MultiLevelType() { Val = MultiLevelValues.Multilevel },
            new TemplateCode() { Val = (AbstractNumberId + 4096).ToString("X8") });
        for (int LevelIndex = 0; LevelIndex < 9; LevelIndex++)
        {
            Level Level = new Level(
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

    private static string DescribeBlockProgress(BlockNode Block, int BlockIndex, int BlockCount)
    {
        string Kind = Block switch
        {
            MermaidBlock => "Mermaid diagram",
            HeadingBlock Heading => "Heading " + Heading.Level.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TableBlock => "Table",
            ListBlock => "List",
            CodeBlock => "Code block",
            QuoteBlock => "Quote",
            HorizontalRuleBlock => "Horizontal rule",
            _ => "Block"
        };

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Writing {Kind} ({BlockIndex}/{BlockCount})");
    }

    private void AppendBlock(OpenXmlElement Parent, BlockNode Block, StyleMapping Styles, int ListLevel)
    {
        if (Block is ParagraphBlock Paragraph)
        {
            Paragraph P = CreateParagraph(Paragraph, Styles.ParagraphStyle, Styles);
            Parent.AppendChild(P);
            return;
        }

        if (Block is HeadingBlock Heading)
        {
            string StyleId = GetHeadingStyle(Heading.Level, Styles);
            Paragraph P = CreateParagraphFromInlines(Heading.Inlines, StyleId, Styles);
            AppendHeadingBookmark(P, Heading);

            Parent.AppendChild(P);
            return;
        }

        if (Block is CodeBlock Code)
        {
            Paragraph P = CreateCodeBlockParagraph(Code, Styles, null);
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
            foreach (BlockNode Child in Quote.Blocks)
            {
                AppendQuoteBlock(Parent, Child, Styles, ListLevel);
            }
            return;
        }

        if (Block is ListBlock List)
        {
            int NumberingId = GetNumberingId(List, ListLevel);
            foreach (ListItemBlock Item in List.Items)
            {
                bool IsFirstListParagraph = true;
                foreach (BlockNode Child in Item.Blocks)
                {
                    AppendListItemBlock(Parent, Child, Styles, NumberingId, ListLevel, ref IsFirstListParagraph, Styles.ParagraphStyle);
                }
            }
            return;
        }

        if (Block is TableBlock TableBlock)
        {
            DocumentFormat.OpenXml.Wordprocessing.Table T = new DocumentFormat.OpenXml.Wordprocessing.Table();
            T.AppendChild(CreateTableProperties(Styles));
            foreach (MDConverter.Model.TableRow Row in TableBlock.Rows)
            {
                DocumentFormat.OpenXml.Wordprocessing.TableRow Tr = new DocumentFormat.OpenXml.Wordprocessing.TableRow();
                foreach (MDConverter.Model.TableCell Cell in Row.Cells)
                {
                    DocumentFormat.OpenXml.Wordprocessing.TableCell Tc = new DocumentFormat.OpenXml.Wordprocessing.TableCell();
                    Tc.AppendChild(new TableCellProperties(new TableCellWidth() { Type = TableWidthUnitValues.Auto }));
                    int ColumnIndex = Tr.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>().Count();
                    foreach (BlockNode CellBlock in Cell.Blocks)
                    {
                        Paragraph P = new Paragraph(CreateTableCellParagraphProperties(Styles, TableBlock, ColumnIndex));
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
        TableProperties Properties = new TableProperties(
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
            int QuoteListLevel = 0;
            int NumberingId = GetNumberingId(List, QuoteListLevel);
            foreach (ListItemBlock Item in List.Items)
            {
                bool IsFirstListParagraph = true;
                foreach (BlockNode Child in Item.Blocks)
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

        Paragraph P = new Paragraph(new ParagraphProperties(new ParagraphStyleId() { Val = Styles.QuoteStyle }));
        AppendBlockContent(P, Block, Styles);
        Parent.AppendChild(P);
    }

    private ParagraphProperties CreateTableCellParagraphProperties(StyleMapping Styles, TableBlock TableBlock, int ColumnIndex)
    {
        ParagraphProperties Properties = new ParagraphProperties(new ParagraphStyleId() { Val = Styles.ParagraphStyle });
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
        int AbstractNumberingId = _NextAbstractNumberingId++;
        int NumberingId = _NextNumberingId++;

        AbstractNum AbstractNum = CreateAbstractNumber(
            AbstractNumberingId,
            List.IsOrdered ? NumberFormatValues.Decimal : NumberFormatValues.Bullet,
            List.IsOrdered && List.StartNumber.HasValue ? List.StartNumber.Value : 1,
            ListLevel);
        NumberingInstance Instance = new NumberingInstance(new AbstractNumId() { Val = AbstractNumberingId })
        {
            NumberID = NumberingId
        };

        Numbering Numbering = _NumberingPart!.Numbering!;
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

        ParagraphProperties Properties = new ParagraphProperties(new ParagraphStyleId() { Val = ParagraphStyleId });
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

        Paragraph P = new Paragraph(Properties);
        AppendBlockContent(P, Block, Styles);
        Parent.AppendChild(P);
    }

    private static Paragraph CreateCodeBlockParagraph(CodeBlock Code, StyleMapping Styles, int? ListLevel)
    {
        ParagraphProperties Properties = new ParagraphProperties(new ParagraphStyleId() { Val = Styles.CodeBlockStyle });
        if (ListLevel.HasValue)
        {
            Properties.Append(new Indentation() { Left = (1080 + (ListLevel.Value * 360)).ToString() });
        }

        Paragraph Paragraph = new Paragraph(Properties);
        AppendCodeText(Paragraph, Code.Content);
        return Paragraph;
    }

    private Paragraph CreateMermaidParagraph(MermaidBlock MermaidBlock, StyleMapping Styles, int? ListLevel)
    {
        ParagraphProperties Properties = new ParagraphProperties(new ParagraphStyleId() { Val = Styles.ParagraphStyle });
        if (ListLevel.HasValue)
        {
            Properties.Append(new Indentation() { Left = (1080 + (ListLevel.Value * 360)).ToString() });
        }

        ApplyCenterAlignmentProperties(Properties);

        Paragraph Paragraph = new Paragraph(Properties);
        try
        {
            string Svg = RenderMermaidSvg(MermaidBlock.Source);
            MermaidImageAccessibility Accessibility = MermaidAccessibilityResolver.Resolve(
                MermaidBlock.Source,
                _Options.Mermaid.UseFlowchartRecognitionParser);
            if (TryAppendRasterizedSvg(Paragraph, Svg, Accessibility))
            {
                return Paragraph;
            }

            if (TryAppendSvg(Paragraph, Svg, Accessibility))
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

    private string RenderMermaidSvg(string MermaidSource)
    {
        RenderOptions RenderOptions = RenderOptions.Default;
        if (_Options.Mermaid.UseFlowchartRecognitionParser
            && FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
                MermaidSource,
                RenderOptions,
                _Options.Mermaid.UseNativeSugiyamaRenderer,
                out string RecognitionSvg))
        {
            return RecognitionSvg;
        }

        return Mermaid.Render(MermaidSource, RenderOptions);
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
        string NormalizedContent = (Content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
        string[] Lines = NormalizedContent.Split('\n');
        Run Run = new Run(new RunProperties(new RunFonts() { Ascii = "Consolas", HighAnsi = "Consolas" }, new NoProof()));
        for (int I = 0; I < Lines.Length; I++)
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
        if (Block is HeadingBlock Heading)
        {
            foreach (InlineNode Inline in Heading.Inlines)
            {
                AppendInline(Parent, Inline, Styles);
            }
            AppendHeadingBookmark(Parent, Heading);
            return;
        }
        if (Block is ParagraphBlock Paragraph)
        {
            foreach (InlineNode Inline in Paragraph.Inlines)
            {
                AppendInline(Parent, Inline, Styles);
            }
        }
    }

    private Paragraph CreateParagraph(ParagraphBlock Paragraph, string StyleId, StyleMapping Styles)
    {
        Paragraph P = new Paragraph(new ParagraphProperties(new ParagraphStyleId() { Val = StyleId }));
        foreach (InlineNode Inline in Paragraph.Inlines)
        {
            AppendInline(P, Inline, Styles);
        }
        return P;
    }

    private Paragraph CreateParagraphFromInlines(IReadOnlyList<InlineNode> Inlines, string StyleId, StyleMapping Styles)
    {
        Paragraph P = new Paragraph(new ParagraphProperties(new ParagraphStyleId() { Val = StyleId }));
        foreach (InlineNode Inline in Inlines)
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

    private void PrepareHeadingBookmarks(IReadOnlyList<BlockNode> Blocks)
    {
        _HeadingBookmarks.Clear();
        _AnchorBookmarks.Clear();
        _NextBookmarkId = _MainPart!.Document!.Descendants<BookmarkStart>()
            .Concat(_MainPart.HeaderParts.SelectMany(Part => Part.Header!.Descendants<BookmarkStart>()))
            .Concat(_MainPart.FooterParts.SelectMany(Part => Part.Footer!.Descendants<BookmarkStart>()))
            .Select(Bookmark => int.TryParse(Bookmark.Id?.Value, out int Id) ? Id : -1)
            .DefaultIfEmpty(-1).Max() + 1;
        if (!_Options.Links.EnableInternalLinks)
        {
            return;
        }

        RegisterHeadings(Blocks);
    }

    private void RegisterHeadings(IReadOnlyList<BlockNode> Blocks)
    {
        foreach (BlockNode Block in Blocks)
        {
            if (Block is HeadingBlock Heading)
            {
                string Text = GetInlineText(Heading.Inlines).ToLowerInvariant();
                string Slug = Regex.Replace(Text, @"[^\p{L}\p{N}\p{M}\s_-]", string.Empty);
                Slug = Regex.Replace(Slug, @"\s", "-");
                string Anchor = Slug;
                int Suffix = 1;
                while (_AnchorBookmarks.ContainsKey(Anchor))
                {
                    Anchor = Slug + "-" + Suffix++;
                }

                string Name = "Md_" + Guid.NewGuid().ToString("N");
                _HeadingBookmarks.Add(Heading, Name);
                _AnchorBookmarks.Add(Anchor, Name);
            }
            else if (Block is QuoteBlock Quote)
            {
                RegisterHeadings(Quote.Blocks);
            }
            else if (Block is ListBlock List)
            {
                foreach (ListItemBlock Item in List.Items)
                {
                    RegisterHeadings(Item.Blocks);
                }
            }
            else if (Block is TableBlock Table)
            {
                foreach (Model.TableRow Row in Table.Rows)
                {
                    foreach (Model.TableCell Cell in Row.Cells)
                    {
                        RegisterHeadings(Cell.Blocks);
                    }
                }
            }
        }
    }

    private static string GetInlineText(IReadOnlyList<InlineNode> Inlines)
    {
        StringBuilder Text = new();
        foreach (InlineNode Inline in Inlines)
        {
            Text.Append(Inline switch
            {
                TextRun Run => Run.Text,
                CodeInline Code => Code.Content,
                EmphasisInline Emphasis => GetInlineText(Emphasis.Inlines),
                StrongInline Strong => GetInlineText(Strong.Inlines),
                StrikethroughInline Strike => GetInlineText(Strike.Inlines),
                LinkInline Link => GetInlineText(Link.Inlines),
                ImageInline Image => Image.AltText,
                BreakInline => " ",
                _ => string.Empty
            });
        }
        return Text.ToString();
    }

    private void AppendHeadingBookmark(Paragraph Paragraph, HeadingBlock Heading)
    {
        if (!_HeadingBookmarks.TryGetValue(Heading, out string? Name))
        {
            return;
        }

        string Id = (_NextBookmarkId++).ToString(CultureInfo.InvariantCulture);
        Paragraph.InsertAfter(new BookmarkStart { Id = Id, Name = Name }, Paragraph.ParagraphProperties);
        Paragraph.AppendChild(new BookmarkEnd { Id = Id });
    }

    private void AppendInline(Paragraph Parent, InlineNode Inline, StyleMapping Styles) => AppendInlineContent(Parent, Inline, Styles, new RunProperties());

    // Each leaf gets its own run so nested formatting cannot affect adjacent text.
    private void AppendInlineContent(OpenXmlCompositeElement Parent, InlineNode Inline, StyleMapping Styles, RunProperties Properties)
    {
        if (Inline is EmphasisInline || Inline is StrongInline || Inline is StrikethroughInline)
        {
            RunProperties Nested = (RunProperties)Properties.CloneNode(true);
            IReadOnlyList<InlineNode> Children;
            if (Inline is EmphasisInline Emphasis)
            {
                Nested.RemoveAllChildren<Italic>();
                Nested.AddChild(new Italic(), true);
                Children = Emphasis.Inlines;
            }
            else if (Inline is StrongInline Strong)
            {
                Nested.RemoveAllChildren<Bold>();
                Nested.AddChild(new Bold(), true);
                Children = Strong.Inlines;
            }
            else
            {
                Nested.RemoveAllChildren<Strike>();
                Nested.AddChild(new Strike(), true);
                Children = ((StrikethroughInline)Inline).Inlines;
            }
            foreach (InlineNode Child in Children)
            {
                AppendInlineContent(Parent, Child, Styles, Nested);
            }

            return;
        }
        if (Inline is LinkInline Link)
        {
            Hyperlink? Hyperlink = null;
            if (ShouldWriteExternalLink(Link.Url))
            {
                string RelationshipId = _MainPart!.AddHyperlinkRelationship(new Uri(Link.Url, UriKind.Absolute), true).Id!;
                Hyperlink = new Hyperlink { History = true, Id = RelationshipId };
            }
            else if (_Options.Links.EnableInternalLinks && Link.Url.StartsWith('#')
                && _AnchorBookmarks.TryGetValue(Uri.UnescapeDataString(Link.Url.Substring(1)), out string? Name))
            {
                Hyperlink = new Hyperlink { History = true, Anchor = Name };
            }
            RunProperties LinkProperties = (RunProperties)Properties.CloneNode(true);
            if (Hyperlink != null)
            {
                LinkProperties.RemoveAllChildren<RunStyle>();
                LinkProperties.PrependChild(new RunStyle { Val = "Hyperlink" });
                Parent.AppendChild(Hyperlink);
            }
            foreach (InlineNode Child in Link.Inlines)
            {
                AppendInlineContent(Hyperlink ?? Parent, Child, Styles, LinkProperties);
            }

            return;
        }
        if (Inline is ImageInline Image)
        {
            Paragraph ImageContainer = new();
            if (!TryAppendImage(ImageContainer, Image))
            {
                ImageContainer.AppendChild(new Run(CreateText(FormatMissingImagePlaceholder(Image.AltText, Image.SourceUri ?? Image.Url, _Options.Images))));
            }
            foreach (OpenXmlElement Child in ImageContainer.ChildElements.ToList())
            {
                Child.Remove();
                Parent.AppendChild(Child);
            }
            return;
        }

        RunProperties LeafProperties = (RunProperties)Properties.CloneNode(true);
        OpenXmlElement Content;
        switch (Inline)
        {
            case TextRun Text: Content = CreateText(Text.Text); break;
            case CodeInline Code:
                LeafProperties.RemoveAllChildren<RunStyle>();
                LeafProperties.PrependChild(new RunStyle { Val = Styles.InlineCodeStyle });
                Content = CreateText(Code.Content);
                break;
            case BreakInline: Content = new Break(); break;
            case TaskCheckboxInline Task: Content = CreateText(Task.Checked ? "[x]" : "[ ]"); break;
            default: return;
        }
        Parent.AppendChild(new Run(LeafProperties, Content));
    }

    private bool ShouldWriteExternalLink(string Url)
    {
        if (!_Options.Links.EnableExternalLinks || string.IsNullOrWhiteSpace(Url))
        {
            return false;
        }

        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? ParsedUri) || ParsedUri == null)
        {
            return false;
        }

        return ParsedUri.Scheme == Uri.UriSchemeHttp
            || ParsedUri.Scheme == Uri.UriSchemeHttps
            || ParsedUri.Scheme == Uri.UriSchemeMailto;
    }

    private static string FormatMissingImagePlaceholder(string AltText, string Uri, ImageOptions ImageOptions)
    {
        string Format = ImageOptions?.MissingImagePlaceholderFormat ?? "[Missing image: {AltText}]";
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
        string? Source = Image.SourceUri ?? Image.Url;
        MainDocumentPart MainPart = _MainPart!;
        if (MainPart == null
            || string.IsNullOrWhiteSpace(Source)
            || (Uri.TryCreate(Source, UriKind.Absolute, out Uri? ParsedUri) && ParsedUri != null && !ParsedUri.IsFile)
            || !File.Exists(Source))
        {
            return false;
        }

        PartTypeInfo? ImagePartType = GetImagePartType(Source);
        if (!ImagePartType.HasValue)
        {
            return false;
        }

        ImagePart ImagePart = MainPart.AddImagePart(ImagePartType.Value);
        using (FileStream ImageStream = File.OpenRead(Source))
        {
            ImagePart.FeedData(ImageStream);
        }

        string RelationshipId = MainPart.GetIdOfPart(ImagePart);
        ImageDimensions Dimensions = GetImageDimensions(Source);
        (long Cx, long Cy) = CalculateImageExtents(Dimensions);
        uint DrawingId = _NextDrawingId++;

        PIC.Picture Picture = new PIC.Picture(
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
        A.GraphicData GraphicData = new A.GraphicData(Picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        DW.Inline Inline = new DW.Inline(
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
        ApplyCenterAlignment(Parent);
        return true;
    }

    private static void ApplyCenterAlignment(Paragraph Paragraph)
    {
        ParagraphProperties? Properties = Paragraph.GetFirstChild<ParagraphProperties>();
        if (Properties == null)
        {
            Properties = new ParagraphProperties();
            Paragraph.PrependChild(Properties);
        }

        ApplyCenterAlignmentProperties(Properties);
    }

    private static void ApplyCenterAlignmentProperties(ParagraphProperties Properties)
    {
        Justification? Existing = Properties.GetFirstChild<Justification>();
        if (Existing == null)
        {
            Properties.Append(new Justification() { Val = JustificationValues.Center });
        }
        else
        {
            Existing.Val = JustificationValues.Center;
        }
    }

    /// <summary>
    /// Embeds a Mermaid diagram as PNG (Skia rasterisation). Flowchart arrowheads are inlined first.
    /// Callers fall back to SVG embedding when rasterisation fails so diagrams are not dropped.
    /// </summary>
    private bool TryAppendRasterizedSvg(Paragraph Parent, string Svg, MermaidImageAccessibility Accessibility)
    {
        MainDocumentPart MainPart = _MainPart!;
        if (MainPart == null || string.IsNullOrWhiteSpace(Svg))
        {
            return false;
        }

        byte[]? PngBytes = TryRasterizeSvgToPng(Svg);
        if (PngBytes == null || PngBytes.Length == 0)
        {
            return false;
        }

        ImagePart ImagePart = MainPart.AddImagePart(ImagePartType.Png);
        using (MemoryStream Stream = new MemoryStream(PngBytes))
        {
            ImagePart.FeedData(Stream);
        }

        string RelationshipId = MainPart.GetIdOfPart(ImagePart);
        SvgDimensions Dimensions = GetSvgDimensions(Svg);
        (long Cx, long Cy) = CalculateSvgExtents(Dimensions);
        uint DrawingId = _NextDrawingId++;

        PIC.NonVisualDrawingProperties NonVisualDrawingProperties = CreateMermaidNonVisualDrawingProperties(
            "Mermaid diagram.png",
            Accessibility);
        DW.DocProperties DocProperties = CreateMermaidDocProperties(DrawingId, Accessibility);

        PIC.Picture Picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                NonVisualDrawingProperties,
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip() { Embed = RelationshipId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset() { X = 0L, Y = 0L },
                    new A.Extents() { Cx = Cx, Cy = Cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        A.GraphicData GraphicData = new A.GraphicData(Picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        DW.Inline Inline = new DW.Inline(
            new DW.Extent() { Cx = Cx, Cy = Cy },
            new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            DocProperties,
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

    private bool TryAppendSvg(Paragraph Parent, string Svg, MermaidImageAccessibility Accessibility)
    {
        MainDocumentPart MainPart = _MainPart!;
        if (MainPart == null || string.IsNullOrWhiteSpace(Svg))
        {
            return false;
        }

        Svg = ExpandSvgFlowchartMarkersForRasterization(Svg);

        ImagePart ImagePart = MainPart.AddImagePart(ImagePartType.Svg);
        byte[] SvgBytes = Encoding.UTF8.GetBytes(Svg);
        using (MemoryStream Stream = new MemoryStream(SvgBytes))
        {
            ImagePart.FeedData(Stream);
        }

        string RelationshipId = MainPart.GetIdOfPart(ImagePart);
        SvgDimensions Dimensions = GetSvgDimensions(Svg);
        (long Cx, long Cy) = CalculateSvgExtents(Dimensions);
        uint DrawingId = _NextDrawingId++;

        PIC.NonVisualDrawingProperties NonVisualDrawingProperties = CreateMermaidNonVisualDrawingProperties(
            "Mermaid diagram.svg",
            Accessibility);
        DW.DocProperties DocProperties = CreateMermaidDocProperties(DrawingId, Accessibility);

        PIC.Picture Picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                NonVisualDrawingProperties,
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip() { Embed = RelationshipId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset() { X = 0L, Y = 0L },
                    new A.Extents() { Cx = Cx, Cy = Cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        A.GraphicData GraphicData = new A.GraphicData(Picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        DW.Inline Inline = new DW.Inline(
            new DW.Extent() { Cx = Cx, Cy = Cy },
            new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            DocProperties,
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

    private static PIC.NonVisualDrawingProperties CreateMermaidNonVisualDrawingProperties(
        string PictureName,
        MermaidImageAccessibility Accessibility)
    {
        PIC.NonVisualDrawingProperties Properties = new PIC.NonVisualDrawingProperties()
        {
            Id = 0U,
            Name = PictureName,
            Description = Accessibility.ResolveEmbeddedDescription()
        };

        string? Title = Accessibility.ResolveEmbeddedTitle();
        if (!string.IsNullOrWhiteSpace(Title))
        {
            Properties.Title = Title;
        }

        return Properties;
    }

    private static DW.DocProperties CreateMermaidDocProperties(uint DrawingId, MermaidImageAccessibility Accessibility)
    {
        DW.DocProperties Properties = new DW.DocProperties()
        {
            Id = DrawingId,
            Name = "Mermaid diagram " + DrawingId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Description = Accessibility.ResolveEmbeddedDescription()
        };

        string? Title = Accessibility.ResolveEmbeddedTitle();
        if (!string.IsNullOrWhiteSpace(Title))
        {
            Properties.Title = Title;
        }

        return Properties;
    }

    /// <summary>
    /// Renders Mermaid source and rasterises to PNG using the DOCX diagram pipeline.
    /// </summary>
    internal static byte[]? TryRasterizeMermaidSourceToPng(string MermaidSource)
    {
        RenderOptions RenderOptions = RenderOptions.Default;

        string Svg;
        if (FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
                MermaidSource,
                RenderOptions,
                UseNativeSugiyamaRenderer: true,
                out string RecognitionSvg))
        {
            Svg = RecognitionSvg;
        }
        else
        {
            Svg = Mermaid.Render(MermaidSource, RenderOptions);
        }

        return TryRasterizeSvgToPng(Svg);
    }

    internal static byte[]? TryRasterizeSvgToPng(string Svg)
    {
        try
        {
            string SvgForRaster = ExpandSvgFlowchartMarkersForRasterization(Svg);
            SKSvg SvgDocument = new SKSvg();
            SvgDocument.FromSvg(SvgForRaster);

            SKPicture? Picture = SvgDocument.Picture;
            if (Picture is null)
            {
                return TryRasterizeSvgToPngViaHarness(SvgForRaster);
            }

            // Use the SVG viewBox (includes diagram padding and marker margin from Naiad). Skia's CullRect
            // is tight to path geometry and clips marker arrowheads at the bitmap edge.
            SKRect DrawBounds = Picture.CullRect;
            if (TryGetSvgViewBoxBounds(Svg, out SKRect ViewBoxBounds))
            {
                DrawBounds = ViewBoxBounds;
            }
            else if (TryGetSvgRootSizeBounds(Svg, out SKRect RootSizeBounds))
            {
                DrawBounds = RootSizeBounds;
            }
            else
            {
                DrawBounds.Inflate(16, 16);
            }

            int Width = (int)Math.Ceiling(Math.Max(1.0, DrawBounds.Width * MermaidRasterScale));
            int Height = (int)Math.Ceiling(Math.Max(1.0, DrawBounds.Height * MermaidRasterScale));

            using SKBitmap Bitmap = new SKBitmap(Width, Height);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);
            Canvas.Scale(MermaidRasterScale);
            Canvas.Translate(-DrawBounds.Left, -DrawBounds.Top);
            Canvas.DrawPicture(Picture);
            Canvas.Flush();

            using SKImage Image = SKImage.FromBitmap(Bitmap);
            using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100);
            return Data.ToArray();
        }
        catch
        {
            return TryRasterizeSvgToPngViaHarness(Svg);
        }
    }

    private readonly struct SvgNodeBounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double MinX { get; } = MinX;
        public double MinY { get; } = MinY;
        public double MaxX { get; } = MaxX;
        public double MaxY { get; } = MaxY;
    }

    private readonly struct SvgStadiumShape(
        double LeftCapCentreX,
        double RightCapCentreX,
        double CapCentreY,
        double Radius,
        double TopY,
        double BottomY)
    {
        public double LeftCapCentreX { get; } = LeftCapCentreX;
        public double RightCapCentreX { get; } = RightCapCentreX;
        public double CapCentreY { get; } = CapCentreY;
        public double Radius { get; } = Radius;
        public double TopY { get; } = TopY;
        public double BottomY { get; } = BottomY;
    }

    /// <summary>Naiad diamond node quad (top, right, bottom, left).</summary>
    private readonly struct SvgDiamondShape(
        double TopX,
        double TopY,
        double RightX,
        double RightY,
        double BottomX,
        double BottomY,
        double LeftX,
        double LeftY)
    {
        public double TopX { get; } = TopX;
        public double TopY { get; } = TopY;
        public double RightX { get; } = RightX;
        public double RightY { get; } = RightY;
        public double BottomX { get; } = BottomX;
        public double BottomY { get; } = BottomY;
        public double LeftX { get; } = LeftX;
        public double LeftY { get; } = LeftY;
    }

    /// <summary>
    /// Skia does not render SVG <c>marker-end</c> reliably. Inline flowchart arrowheads as filled paths with the tip
    /// on the target node boundary (Mermaid trims the stroke for SVG markers; PNG must reach the target).
    /// </summary>
    private static string ExpandSvgFlowchartMarkersForRasterization(string Svg)
    {
        try
        {
            XDocument Document = XDocument.Parse(Svg);
            XElement? Root = Document.Root;
            if (Root == null)
            {
                return Svg;
            }

            XNamespace SvgNamespace = Root.Name.Namespace;
            List<XElement> MarkableElements = Root.Descendants()
                .Where(Element => Element.Attribute("marker-end") != null)
                .ToList();
            List<XElement> OverlayElements = [];

            foreach (XElement Element in MarkableElements)
            {
                XAttribute? MarkerEndAttribute = Element.Attribute("marker-end");
                if (MarkerEndAttribute == null
                    || MarkerEndAttribute.Value.IndexOf("pointEnd", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!TryGetConnectorEndPointAndDirection(Element, out double EndX, out double EndY, out double DirX, out double DirY))
                {
                    continue;
                }

                double Magnitude = Math.Sqrt((DirX * DirX) + (DirY * DirY));
                if (Magnitude < 1e-6)
                {
                    continue;
                }

                double UnitX = DirX / Magnitude;
                double UnitY = DirY / Magnitude;
                double TipX = EndX;
                double TipY = EndY;
                if (TrySnapConnectorEndpointToTargetBoundary(
                        Root,
                        EndX,
                        EndY,
                        UnitX,
                        UnitY,
                        out double AttachX,
                        out double AttachY))
                {
                    TipX = AttachX;
                    TipY = AttachY;
                    ExtendConnectorElementTo(Element, AttachX, AttachY);
                }

                double BackX = TipX - (FlowchartRasterArrowLengthPx * UnitX);
                double BackY = TipY - (FlowchartRasterArrowLengthPx * UnitY);
                double LeftX = BackX + (FlowchartRasterArrowHalfWidthPx * UnitY);
                double LeftY = BackY - (FlowchartRasterArrowHalfWidthPx * UnitX);
                double RightX = BackX - (FlowchartRasterArrowHalfWidthPx * UnitY);
                double RightY = BackY + (FlowchartRasterArrowHalfWidthPx * UnitX);

                string ArrowPathData = string.Create(
                    CultureInfo.InvariantCulture,
                    $"M{TipX:0.##},{TipY:0.##}L{LeftX:0.##},{LeftY:0.##}L{RightX:0.##},{RightY:0.##}Z");

                string ArrowFillColour = ResolveFlowchartRasterArrowFillColour(Element);

                XElement ArrowPathElement = new XElement(
                    SvgNamespace + "path",
                    new XAttribute("d", ArrowPathData),
                    new XAttribute("fill", ArrowFillColour),
                    new XAttribute("stroke", "none"));
                Element.AddAfterSelf(ArrowPathElement);
                MarkerEndAttribute.Remove();
                OverlayElements.Add(Element);
                OverlayElements.Add(ArrowPathElement);
            }

            RaiseFlowchartLinkOverlayElements(Root, OverlayElements);

            return Document.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            return Svg;
        }
    }

    /// <summary>
    /// Resolves the fill colour for an inlined raster arrowhead so it matches the connector stroke (for example
    /// <c>linkStyle default stroke:#c60</c>). Skia does not honour SVG marker styling reliably.
    /// </summary>
    private static string ResolveFlowchartRasterArrowFillColour(XElement ConnectorElement)
    {
        if (TryGetFlowchartConnectorStrokeColour(ConnectorElement, out string StrokeColour))
        {
            return NormalizeRasterArrowFillColour(StrokeColour);
        }

        XElement? PreviousLinkPath = ConnectorElement.ElementsBeforeSelf()
            .LastOrDefault(IsFlowchartLinkConnectorPath);
        if (PreviousLinkPath != null
            && TryGetFlowchartConnectorStrokeColour(PreviousLinkPath, out string PreviousStrokeColour))
        {
            return NormalizeRasterArrowFillColour(PreviousStrokeColour);
        }

        return FLOWCHART_RASTER_DEFAULT_ARROW_FILL;
    }

    private static bool IsFlowchartLinkConnectorPath(XElement Element)
    {
        if (!string.Equals(Element.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string ClassValue = Element.Attribute("class")?.Value ?? string.Empty;
        return ClassValue.Contains("flowchart-link", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFlowchartConnectorStrokeColour(XElement ConnectorElement, out string StrokeColour)
    {
        StrokeColour = string.Empty;

        string? StyleAttribute = ConnectorElement.Attribute("style")?.Value;
        if (!string.IsNullOrWhiteSpace(StyleAttribute))
        {
            Match StyleMatch = SvgInlineStrokeColourRegex.Match(StyleAttribute);
            if (StyleMatch.Success)
            {
                StrokeColour = StyleMatch.Groups[1].Value.Trim();
                return true;
            }
        }

        string? StrokeAttribute = ConnectorElement.Attribute("stroke")?.Value;
        if (!string.IsNullOrWhiteSpace(StrokeAttribute)
            && !string.Equals(StrokeAttribute, "none", StringComparison.OrdinalIgnoreCase))
        {
            StrokeColour = StrokeAttribute.Trim();
            return true;
        }

        string? MarkerEndAttribute = ConnectorElement.Attribute("marker-end")?.Value;
        if (!string.IsNullOrWhiteSpace(MarkerEndAttribute)
            && TryGetTintedFlowchartMarkerStrokeColour(MarkerEndAttribute, out string MarkerStrokeColour))
        {
            StrokeColour = MarkerStrokeColour;
            return true;
        }

        return false;
    }

    private static bool TryGetTintedFlowchartMarkerStrokeColour(string MarkerEndAttribute, out string StrokeColour)
    {
        StrokeColour = string.Empty;
        Match Match = SvgTintedPointMarkerColourSuffixRegex.Match(MarkerEndAttribute);
        if (!Match.Success)
        {
            return false;
        }

        StrokeColour = "#" + Match.Groups[1].Value;
        return true;
    }

    private static string NormalizeRasterArrowFillColour(string Colour)
    {
        string Trimmed = Colour.Trim();
        if (Trimmed.Length == 0)
        {
            return FLOWCHART_RASTER_DEFAULT_ARROW_FILL;
        }

        if (!Trimmed.StartsWith('#'))
        {
            Trimmed = "#" + Trimmed;
        }

        if (Trimmed.Length == 4)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"#{Trimmed[1]}{Trimmed[1]}{Trimmed[2]}{Trimmed[2]}{Trimmed[3]}{Trimmed[3]}");
        }

        return Trimmed;
    }

    /// <summary>
    /// Naiad emits <c>flowchart-link</c> paths before node shapes. Raise links and inlined arrowheads above all node
    /// geometry so the connector and tip are not covered by opaque target fills when rasterised.
    /// </summary>
    private static void RaiseFlowchartLinkOverlayElements(XElement Root, List<XElement> OverlayElements)
    {
        if (OverlayElements.Count == 0)
        {
            return;
        }

        XElement? LastNodeGeometry = null;
        foreach (XElement Child in Root.Elements())
        {
            if (IsFlowchartNodeGeometryElement(Child))
            {
                LastNodeGeometry = Child;
            }
        }

        XElement Anchor = LastNodeGeometry ?? Root;
        for (int Index = 0; Index < OverlayElements.Count; Index++)
        {
            XElement OverlayElement = OverlayElements[Index];
            OverlayElement.Remove();
            Anchor.AddAfterSelf(OverlayElement);
            Anchor = OverlayElement;
        }
    }

    private static bool IsFlowchartNodeGeometryElement(XElement Element)
    {
        string LocalName = Element.Name.LocalName;
        if (!string.Equals(LocalName, "path", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "line", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "rect", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "circle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "ellipse", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "polygon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? ClassValue = Element.Attribute("class")?.Value ?? string.Empty;
        if (ClassValue.Contains("flowchart-link", StringComparison.OrdinalIgnoreCase)
            || ClassValue.Contains("nodeLabel", StringComparison.OrdinalIgnoreCase)
            || ClassValue.Contains("marker", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static List<SvgNodeBounds> CollectFlowchartNodeBounds(XElement Root)
    {
        List<SvgNodeBounds> BoundsList = [];
        HashSet<string> SeenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (XElement Element in Root.Descendants())
        {
            string LocalName = Element.Name.LocalName;
            if (!string.Equals(LocalName, "path", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(LocalName, "line", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(LocalName, "rect", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? ClassValue = Element.Attribute("class")?.Value ?? string.Empty;
            if (ClassValue.Contains("flowchart-link", StringComparison.OrdinalIgnoreCase)
                || ClassValue.Contains("nodeLabel", StringComparison.OrdinalIgnoreCase)
                || ClassValue.Contains("marker", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? PathData = Element.Attribute("d")?.Value;
            if (!string.IsNullOrWhiteSpace(PathData)
                && TryParseNaiadStadiumPath(PathData, out SvgStadiumShape _))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(PathData)
                && TryParseNaiadDiamondPath(PathData, out SvgDiamondShape _))
            {
                continue;
            }

            if (!TryGetElementBounds(Element, out SvgNodeBounds Bounds))
            {
                continue;
            }

            double Width = Bounds.MaxX - Bounds.MinX;
            double Height = Bounds.MaxY - Bounds.MinY;
            if (Width < 8.0 && Height < 8.0)
            {
                continue;
            }

            string Key = string.Create(
                CultureInfo.InvariantCulture,
                $"{Bounds.MinX:0.##},{Bounds.MinY:0.##},{Bounds.MaxX:0.##},{Bounds.MaxY:0.##}");
            if (!SeenKeys.Add(Key))
            {
                continue;
            }

            BoundsList.Add(Bounds);
        }

        return BoundsList;
    }

    private static List<SvgStadiumShape> CollectFlowchartStadiumShapes(XElement Root)
    {
        List<SvgStadiumShape> StadiumShapeList = [];
        HashSet<string> SeenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (XElement Element in Root.Descendants())
        {
            if (!string.Equals(Element.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? ClassValue = Element.Attribute("class")?.Value ?? string.Empty;
            if (ClassValue.Contains("flowchart-link", StringComparison.OrdinalIgnoreCase)
                || ClassValue.Contains("nodeLabel", StringComparison.OrdinalIgnoreCase)
                || ClassValue.Contains("marker", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? PathData = Element.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(PathData))
            {
                continue;
            }

            if (!TryParseNaiadStadiumPath(PathData, out SvgStadiumShape StadiumShape))
            {
                continue;
            }

            string Key = string.Create(
                CultureInfo.InvariantCulture,
                $"{StadiumShape.LeftCapCentreX:0.##},{StadiumShape.TopY:0.##},{StadiumShape.Radius:0.##}");
            if (!SeenKeys.Add(Key))
            {
                continue;
            }

            StadiumShapeList.Add(StadiumShape);
        }

        return StadiumShapeList;
    }

    private static List<SvgDiamondShape> CollectFlowchartDiamondShapes(XElement Root)
    {
        List<SvgDiamondShape> DiamondShapeList = [];
        HashSet<string> SeenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (XElement Element in Root.Descendants())
        {
            if (!string.Equals(Element.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? ClassValue = Element.Attribute("class")?.Value ?? string.Empty;
            if (ClassValue.Contains("flowchart-link", StringComparison.OrdinalIgnoreCase)
                || ClassValue.Contains("nodeLabel", StringComparison.OrdinalIgnoreCase)
                || ClassValue.Contains("marker", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? PathData = Element.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(PathData))
            {
                continue;
            }

            if (!TryParseNaiadDiamondPath(PathData, out SvgDiamondShape DiamondShape))
            {
                continue;
            }

            string Key = string.Create(
                CultureInfo.InvariantCulture,
                $"{DiamondShape.TopX:0.##},{DiamondShape.TopY:0.##},{DiamondShape.RightX:0.##},{DiamondShape.RightY:0.##}");
            if (!SeenKeys.Add(Key))
            {
                continue;
            }

            DiamondShapeList.Add(DiamondShape);
        }

        return DiamondShapeList;
    }

    private static bool TryParseNaiadStadiumPath(string PathData, out SvgStadiumShape StadiumShape)
    {
        StadiumShape = default;
        Match MatchResult = Regex.Match(
            PathData,
            @"M\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s+H\s*([\d\.-]+)\s+A\s*([\d\.-]+)\s*,\s*\4\s+0\s+0\s+1\s+[\d\.-]+\s*,\s*([\d\.-]+)\s+H\s*\1\s+A\s*\4\s*,\s*\4",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!MatchResult.Success)
        {
            return false;
        }

        double LeftFlatX = double.Parse(MatchResult.Groups[1].Value, CultureInfo.InvariantCulture);
        double TopY = double.Parse(MatchResult.Groups[2].Value, CultureInfo.InvariantCulture);
        double RightFlatX = double.Parse(MatchResult.Groups[3].Value, CultureInfo.InvariantCulture);
        double Radius = double.Parse(MatchResult.Groups[4].Value, CultureInfo.InvariantCulture);
        double BottomY = double.Parse(MatchResult.Groups[5].Value, CultureInfo.InvariantCulture);
        if (Radius <= 0.0)
        {
            return false;
        }

        StadiumShape = new SvgStadiumShape(
            LeftFlatX,
            RightFlatX,
            TopY + Radius,
            Radius,
            TopY,
            BottomY);
        return true;
    }

    private static bool TryParseNaiadDiamondPath(string PathData, out SvgDiamondShape DiamondShape)
    {
        DiamondShape = default;
        Match MatchResult = Regex.Match(
            PathData,
            @"M\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s+L\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s+L\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s+L\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s+Z",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!MatchResult.Success)
        {
            return false;
        }

        double TopX = double.Parse(MatchResult.Groups[1].Value, CultureInfo.InvariantCulture);
        double TopY = double.Parse(MatchResult.Groups[2].Value, CultureInfo.InvariantCulture);
        double RightX = double.Parse(MatchResult.Groups[3].Value, CultureInfo.InvariantCulture);
        double RightY = double.Parse(MatchResult.Groups[4].Value, CultureInfo.InvariantCulture);
        double BottomX = double.Parse(MatchResult.Groups[5].Value, CultureInfo.InvariantCulture);
        double BottomY = double.Parse(MatchResult.Groups[6].Value, CultureInfo.InvariantCulture);
        double LeftX = double.Parse(MatchResult.Groups[7].Value, CultureInfo.InvariantCulture);
        double LeftY = double.Parse(MatchResult.Groups[8].Value, CultureInfo.InvariantCulture);
        DiamondShape = new SvgDiamondShape(TopX, TopY, RightX, RightY, BottomX, BottomY, LeftX, LeftY);
        return true;
    }

    private static bool TryIntersectRayWithStadium(
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        SvgStadiumShape StadiumShape,
        out double HitX,
        out double HitY,
        out double Distance)
    {
        HitX = 0;
        HitY = 0;
        Distance = double.PositiveInfinity;
        bool Found = false;

        if (TryIntersectRayWithCircle(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                StadiumShape.LeftCapCentreX,
                StadiumShape.CapCentreY,
                StadiumShape.Radius,
                out double LeftHitX,
                out double LeftHitY,
                out double LeftDistance))
        {
            HitX = LeftHitX;
            HitY = LeftHitY;
            Distance = LeftDistance;
            Found = true;
        }

        if (TryIntersectRayWithCircle(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                StadiumShape.RightCapCentreX,
                StadiumShape.CapCentreY,
                StadiumShape.Radius,
                out double RightHitX,
                out double RightHitY,
                out double RightDistance)
            && RightDistance < Distance)
        {
            HitX = RightHitX;
            HitY = RightHitY;
            Distance = RightDistance;
            Found = true;
        }

        if (TryIntersectRayWithHorizontalSegment(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                StadiumShape.LeftCapCentreX,
                StadiumShape.RightCapCentreX,
                StadiumShape.TopY,
                out double TopHitX,
                out double TopHitY,
                out double TopDistance)
            && TopDistance < Distance)
        {
            HitX = TopHitX;
            HitY = TopHitY;
            Distance = TopDistance;
            Found = true;
        }

        if (TryIntersectRayWithHorizontalSegment(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                StadiumShape.LeftCapCentreX,
                StadiumShape.RightCapCentreX,
                StadiumShape.BottomY,
                out double BottomHitX,
                out double BottomHitY,
                out double BottomDistance)
            && BottomDistance < Distance)
        {
            HitX = BottomHitX;
            HitY = BottomHitY;
            Distance = BottomDistance;
            Found = true;
        }

        return Found;
    }

    private static bool TryIntersectRayWithDiamond(
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        SvgDiamondShape DiamondShape,
        out double HitX,
        out double HitY,
        out double Distance)
    {
        HitX = 0;
        HitY = 0;
        Distance = double.PositiveInfinity;
        bool Found = false;

        if (TryIntersectRayWithSegment(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                DiamondShape.TopX,
                DiamondShape.TopY,
                DiamondShape.RightX,
                DiamondShape.RightY,
                out double TopRightHitX,
                out double TopRightHitY,
                out double TopRightDistance)
            && TopRightDistance < Distance)
        {
            HitX = TopRightHitX;
            HitY = TopRightHitY;
            Distance = TopRightDistance;
            Found = true;
        }

        if (TryIntersectRayWithSegment(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                DiamondShape.RightX,
                DiamondShape.RightY,
                DiamondShape.BottomX,
                DiamondShape.BottomY,
                out double RightBottomHitX,
                out double RightBottomHitY,
                out double RightBottomDistance)
            && RightBottomDistance < Distance)
        {
            HitX = RightBottomHitX;
            HitY = RightBottomHitY;
            Distance = RightBottomDistance;
            Found = true;
        }

        if (TryIntersectRayWithSegment(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                DiamondShape.BottomX,
                DiamondShape.BottomY,
                DiamondShape.LeftX,
                DiamondShape.LeftY,
                out double BottomLeftHitX,
                out double BottomLeftHitY,
                out double BottomLeftDistance)
            && BottomLeftDistance < Distance)
        {
            HitX = BottomLeftHitX;
            HitY = BottomLeftHitY;
            Distance = BottomLeftDistance;
            Found = true;
        }

        if (TryIntersectRayWithSegment(
                OriginX,
                OriginY,
                UnitX,
                UnitY,
                DiamondShape.LeftX,
                DiamondShape.LeftY,
                DiamondShape.TopX,
                DiamondShape.TopY,
                out double LeftTopHitX,
                out double LeftTopHitY,
                out double LeftTopDistance)
            && LeftTopDistance < Distance)
        {
            HitX = LeftTopHitX;
            HitY = LeftTopHitY;
            Distance = LeftTopDistance;
            Found = true;
        }

        return Found;
    }

    private static bool TryIntersectRayWithSegment(
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        double SegmentStartX,
        double SegmentStartY,
        double SegmentEndX,
        double SegmentEndY,
        out double HitX,
        out double HitY,
        out double Distance)
    {
        HitX = 0;
        HitY = 0;
        Distance = 0;

        double SegmentDx = SegmentEndX - SegmentStartX;
        double SegmentDy = SegmentEndY - SegmentStartY;
        double Denominator = (UnitX * SegmentDy) - (UnitY * SegmentDx);
        const double Epsilon = 1e-9;
        if (Math.Abs(Denominator) < Epsilon)
        {
            return false;
        }

        double QueryPx = SegmentStartX - OriginX;
        double QueryPy = SegmentStartY - OriginY;
        double RayParameter = ((QueryPx * SegmentDy) - (QueryPy * SegmentDx)) / Denominator;
        double SegmentParameter = ((QueryPx * UnitY) - (QueryPy * UnitX)) / Denominator;
        const double MinApproach = 0.5;
        if (RayParameter < MinApproach
            || SegmentParameter < -Epsilon
            || SegmentParameter > 1.0 + Epsilon)
        {
            return false;
        }

        HitX = OriginX + (UnitX * RayParameter);
        HitY = OriginY + (UnitY * RayParameter);
        Distance = RayParameter;
        return true;
    }

    private static bool TryIntersectRayWithCircle(
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        double CentreX,
        double CentreY,
        double Radius,
        out double HitX,
        out double HitY,
        out double Distance)
    {
        HitX = 0;
        HitY = 0;
        Distance = 0;

        double DeltaX = OriginX - CentreX;
        double DeltaY = OriginY - CentreY;
        double B = (2.0 * ((UnitX * DeltaX) + (UnitY * DeltaY)));
        double C = (DeltaX * DeltaX) + (DeltaY * DeltaY) - (Radius * Radius);
        double Discriminant = (B * B) - (4.0 * C);
        if (Discriminant < 0.0)
        {
            return false;
        }

        double SqrtDiscriminant = Math.Sqrt(Discriminant);
        double T1 = (-B - SqrtDiscriminant) / 2.0;
        double T2 = (-B + SqrtDiscriminant) / 2.0;
        const double MinApproach = 0.5;
        double BestT = double.PositiveInfinity;

        if (T1 >= MinApproach)
        {
            BestT = T1;
        }

        if (T2 >= MinApproach && T2 < BestT)
        {
            BestT = T2;
        }

        if (double.IsPositiveInfinity(BestT))
        {
            return false;
        }

        HitX = OriginX + (UnitX * BestT);
        HitY = OriginY + (UnitY * BestT);
        Distance = BestT;
        return true;
    }

    private static bool TryIntersectRayWithHorizontalSegment(
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        double SegmentMinX,
        double SegmentMaxX,
        double SegmentY,
        out double HitX,
        out double HitY,
        out double Distance)
    {
        HitX = 0;
        HitY = 0;
        Distance = 0;

        const double Epsilon = 1e-9;
        if (Math.Abs(UnitY) < Epsilon)
        {
            return false;
        }

        double T = (SegmentY - OriginY) / UnitY;
        const double MinApproach = 0.5;
        if (T < MinApproach)
        {
            return false;
        }

        HitX = OriginX + (UnitX * T);
        HitY = SegmentY;
        if (HitX < Math.Min(SegmentMinX, SegmentMaxX) - Epsilon
            || HitX > Math.Max(SegmentMinX, SegmentMaxX) + Epsilon)
        {
            return false;
        }

        Distance = T;
        return true;
    }

    private static bool TryGetElementBounds(XElement Element, out SvgNodeBounds Bounds)
    {
        Bounds = new SvgNodeBounds(0, 0, 0, 0);

        if (string.Equals(Element.Name.LocalName, "rect", StringComparison.OrdinalIgnoreCase))
        {
            if (!double.TryParse(Element.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double X)
                || !double.TryParse(Element.Attribute("y")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Y)
                || !double.TryParse(Element.Attribute("width")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Width)
                || !double.TryParse(Element.Attribute("height")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Height))
            {
                return false;
            }

            Bounds = new SvgNodeBounds(X, Y, X + Width, Y + Height);
            return Width > 0 && Height > 0;
        }

        if (string.Equals(Element.Name.LocalName, "line", StringComparison.OrdinalIgnoreCase))
        {
            if (!double.TryParse(Element.Attribute("x1")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double X1)
                || !double.TryParse(Element.Attribute("y1")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Y1)
                || !double.TryParse(Element.Attribute("x2")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double X2)
                || !double.TryParse(Element.Attribute("y2")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Y2))
            {
                return false;
            }

            Bounds = new SvgNodeBounds(
                Math.Min(X1, X2),
                Math.Min(Y1, Y2),
                Math.Max(X1, X2),
                Math.Max(Y1, Y2));
            return true;
        }

        string? PathData = Element.Attribute("d")?.Value;
        if (string.IsNullOrWhiteSpace(PathData))
        {
            return false;
        }

        return TryGetPathDataBounds(PathData, out Bounds);
    }

    private static bool TryGetPathDataBounds(string PathData, out SvgNodeBounds Bounds)
    {
        Bounds = new SvgNodeBounds(0, 0, 0, 0);
        try
        {
            using SKPath? Path = SKPath.ParseSvgPathData(PathData);
            if (Path == null || Path.IsEmpty)
            {
                return false;
            }

            SKRect Rect = Path.Bounds;
            if (Rect.Width <= 0 && Rect.Height <= 0)
            {
                return false;
            }

            Bounds = new SvgNodeBounds(Rect.Left, Rect.Top, Rect.Right, Rect.Bottom);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Snap only when the stroke end is outside node fills and still short of the target outline (F53: skip re-extending
    /// through a stadium when the path already ends on the shoulder inside the target AABB).
    /// </summary>
    private static bool TrySnapConnectorEndpointToTargetBoundary(
        XElement Root,
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        out double AttachX,
        out double AttachY)
    {
        AttachX = OriginX;
        AttachY = OriginY;

        if (IsPointInsideAnyFlowchartNodeBounds(Root, OriginX, OriginY))
        {
            double BestSnapDistance = double.PositiveInfinity;
            if (TryFindTargetAttachPoint(
                    Root,
                    OriginX,
                    OriginY,
                    UnitX,
                    UnitY,
                    out double ForwardAttachX,
                    out double ForwardAttachY))
            {
                double ForwardDeltaX = ForwardAttachX - OriginX;
                double ForwardDeltaY = ForwardAttachY - OriginY;
                double ForwardDistance = Math.Sqrt((ForwardDeltaX * ForwardDeltaX) + (ForwardDeltaY * ForwardDeltaY));
                if (ForwardDistance < BestSnapDistance)
                {
                    BestSnapDistance = ForwardDistance;
                    AttachX = ForwardAttachX;
                    AttachY = ForwardAttachY;
                }
            }

            if (TryFindTargetAttachPoint(
                    Root,
                    OriginX,
                    OriginY,
                    -UnitX,
                    -UnitY,
                    out double BackAttachX,
                    out double BackAttachY))
            {
                double BackDeltaX = BackAttachX - OriginX;
                double BackDeltaY = BackAttachY - OriginY;
                double BackDistance = Math.Sqrt((BackDeltaX * BackDeltaX) + (BackDeltaY * BackDeltaY));
                if (BackDistance < BestSnapDistance)
                {
                    BestSnapDistance = BackDistance;
                    AttachX = BackAttachX;
                    AttachY = BackAttachY;
                }
            }

            const double MaxInsideSnapDistancePx = 12.0;
            return BestSnapDistance <= MaxInsideSnapDistancePx;
        }

        if (!TryFindTargetAttachPoint(Root, OriginX, OriginY, UnitX, UnitY, out AttachX, out AttachY))
        {
            // For curveBasis paths the tangent direction at the end can be misleading (it is derived from the last
            // control point, not the visual final segment). Fall back to the nearest rectangle boundary point.
            return TryFindNearestFlowchartNodeBoundaryPoint(Root, OriginX, OriginY, out AttachX, out AttachY);
        }

        double DeltaX = AttachX - OriginX;
        double DeltaY = AttachY - OriginY;
        double SnapDistance = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        const double MaxSnapDistancePx = 24.0;
        return SnapDistance <= MaxSnapDistancePx;
    }

    private static bool TryFindNearestFlowchartNodeBoundaryPoint(
        XElement Root,
        double PointX,
        double PointY,
        out double AttachX,
        out double AttachY)
    {
        AttachX = PointX;
        AttachY = PointY;

        List<SvgNodeBounds> NodeBoundsList = CollectFlowchartNodeBounds(Root);
        double BestDistance = double.PositiveInfinity;

        for (int Index = 0; Index < NodeBoundsList.Count; Index++)
        {
            SvgNodeBounds Bounds = NodeBoundsList[Index];

            bool IsInside =
                PointX >= Bounds.MinX
                && PointX <= Bounds.MaxX
                && PointY >= Bounds.MinY
                && PointY <= Bounds.MaxY;

            if (IsInside)
            {
                continue;
            }

            double CandidateX;
            double CandidateY;

            // Closest point on the rectangle boundary (not a ray intersection).
            if (PointY < Bounds.MinY)
            {
                CandidateY = Bounds.MinY;
                CandidateX = Math.Clamp(PointX, Bounds.MinX, Bounds.MaxX);
            }
            else if (PointY > Bounds.MaxY)
            {
                CandidateY = Bounds.MaxY;
                CandidateX = Math.Clamp(PointX, Bounds.MinX, Bounds.MaxX);
            }
            else if (PointX < Bounds.MinX)
            {
                CandidateX = Bounds.MinX;
                CandidateY = Math.Clamp(PointY, Bounds.MinY, Bounds.MaxY);
            }
            else if (PointX > Bounds.MaxX)
            {
                CandidateX = Bounds.MaxX;
                CandidateY = Math.Clamp(PointY, Bounds.MinY, Bounds.MaxY);
            }
            else
            {
                continue;
            }

            double Dx = CandidateX - PointX;
            double Dy = CandidateY - PointY;
            double Distance = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (Distance < BestDistance)
            {
                BestDistance = Distance;
                AttachX = CandidateX;
                AttachY = CandidateY;
            }
        }

        const double MaxSnapDistancePx = 24.0;
        return BestDistance <= MaxSnapDistancePx;
    }

    private static bool IsPointInsideAnyFlowchartNodeBounds(XElement Root, double PointX, double PointY)
    {
        List<SvgNodeBounds> NodeBoundsList = CollectFlowchartNodeBounds(Root);
        const double BoundaryEpsilon = 0.5;
        for (int Index = 0; Index < NodeBoundsList.Count; Index++)
        {
            SvgNodeBounds NodeBounds = NodeBoundsList[Index];
            if (PointX >= NodeBounds.MinX - BoundaryEpsilon
                && PointX <= NodeBounds.MaxX + BoundaryEpsilon
                && PointY >= NodeBounds.MinY - BoundaryEpsilon
                && PointY <= NodeBounds.MaxY + BoundaryEpsilon)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindTargetAttachPoint(
        XElement Root,
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        out double AttachX,
        out double AttachY)
    {
        AttachX = OriginX;
        AttachY = OriginY;
        List<SvgNodeBounds> NodeBoundsList = CollectFlowchartNodeBounds(Root);
        List<SvgStadiumShape> StadiumShapeList = CollectFlowchartStadiumShapes(Root);
        List<SvgDiamondShape> DiamondShapeList = CollectFlowchartDiamondShapes(Root);
        double BestDistance = double.PositiveInfinity;
        bool Found = false;

        for (int StadiumIndex = 0; StadiumIndex < StadiumShapeList.Count; StadiumIndex++)
        {
            SvgStadiumShape StadiumShape = StadiumShapeList[StadiumIndex];
            if (!IsStadiumCandidateForConnectorSnap(StadiumShape, OriginX, OriginY))
            {
                continue;
            }

            if (!TryIntersectRayWithStadium(OriginX, OriginY, UnitX, UnitY, StadiumShape, out double StadiumHitX, out double StadiumHitY, out double StadiumDistance))
            {
                continue;
            }

            if (StadiumDistance < BestDistance)
            {
                BestDistance = StadiumDistance;
                AttachX = StadiumHitX;
                AttachY = StadiumHitY;
                Found = true;
            }
        }

        for (int DiamondIndex = 0; DiamondIndex < DiamondShapeList.Count; DiamondIndex++)
        {
            SvgDiamondShape DiamondShape = DiamondShapeList[DiamondIndex];
            if (!TryIntersectRayWithDiamond(
                    OriginX,
                    OriginY,
                    UnitX,
                    UnitY,
                    DiamondShape,
                    out double DiamondHitX,
                    out double DiamondHitY,
                    out double DiamondDistance))
            {
                continue;
            }

            if (DiamondDistance < BestDistance)
            {
                BestDistance = DiamondDistance;
                AttachX = DiamondHitX;
                AttachY = DiamondHitY;
                Found = true;
            }
        }

        for (int Index = 0; Index < NodeBoundsList.Count; Index++)
        {
            SvgNodeBounds NodeBounds = NodeBoundsList[Index];
            if (!TryIntersectRayWithBounds(OriginX, OriginY, UnitX, UnitY, NodeBounds, out double HitX, out double HitY, out double Distance))
            {
                continue;
            }

            if (Distance < BestDistance)
            {
                BestDistance = Distance;
                AttachX = HitX;
                AttachY = HitY;
                Found = true;
            }
        }

        return Found;
    }

    private static bool IsStadiumCandidateForConnectorSnap(SvgStadiumShape StadiumShape, double OriginX, double OriginY)
    {
        const double VerticalMarginPx = 8.0;
        if (OriginY > StadiumShape.BottomY + VerticalMarginPx)
        {
            return false;
        }

        if (OriginY + VerticalMarginPx < StadiumShape.TopY)
        {
            return false;
        }

        return true;
    }

    private static bool TryIntersectRayWithBounds(
        double OriginX,
        double OriginY,
        double UnitX,
        double UnitY,
        SvgNodeBounds Bounds,
        out double HitX,
        out double HitY,
        out double Distance)
    {
        HitX = 0;
        HitY = 0;
        Distance = 0;

        const double Epsilon = 1e-9;
        double TMin = double.NegativeInfinity;
        double TMax = double.PositiveInfinity;

        if (Math.Abs(UnitX) < Epsilon)
        {
            if (OriginX < Bounds.MinX || OriginX > Bounds.MaxX)
            {
                return false;
            }
        }
        else
        {
            double T1 = (Bounds.MinX - OriginX) / UnitX;
            double T2 = (Bounds.MaxX - OriginX) / UnitX;
            if (T1 > T2)
            {
                double Swap = T1;
                T1 = T2;
                T2 = Swap;
            }

            TMin = Math.Max(TMin, T1);
            TMax = Math.Min(TMax, T2);
        }

        if (Math.Abs(UnitY) < Epsilon)
        {
            if (OriginY < Bounds.MinY || OriginY > Bounds.MaxY)
            {
                return false;
            }
        }
        else
        {
            double T1 = (Bounds.MinY - OriginY) / UnitY;
            double T2 = (Bounds.MaxY - OriginY) / UnitY;
            if (T1 > T2)
            {
                double Swap = T1;
                T1 = T2;
                T2 = Swap;
            }

            TMin = Math.Max(TMin, T1);
            TMax = Math.Min(TMax, T2);
        }

        const double MinApproach = 0.5;
        if (TMax < MinApproach || TMin > TMax)
        {
            return false;
        }

        double T = TMin >= MinApproach ? TMin : TMax;
        if (T < MinApproach)
        {
            return false;
        }

        HitX = OriginX + (UnitX * T);
        HitY = OriginY + (UnitY * T);
        Distance = T;
        return true;
    }

    private static void ExtendConnectorElementTo(XElement Element, double TargetX, double TargetY)
    {
        if (string.Equals(Element.Name.LocalName, "line", StringComparison.OrdinalIgnoreCase))
        {
            Element.SetAttributeValue("x2", TargetX.ToString("0.##", CultureInfo.InvariantCulture));
            Element.SetAttributeValue("y2", TargetY.ToString("0.##", CultureInfo.InvariantCulture));
            return;
        }

        string? PathData = Element.Attribute("d")?.Value;
        if (string.IsNullOrWhiteSpace(PathData))
        {
            return;
        }

        MatchCollection EndMatches = SvgPathEndPointRegex.Matches(PathData);
        if (EndMatches.Count == 0)
        {
            return;
        }

        Match LastMatch = EndMatches[EndMatches.Count - 1];
        string ExtendedPathData = PathData.Remove(LastMatch.Index, LastMatch.Length);
        ExtendedPathData += string.Create(
            CultureInfo.InvariantCulture,
            $"L{TargetX:0.##},{TargetY:0.##}");
        Element.SetAttributeValue("d", ExtendedPathData);
    }

    private static bool TryGetConnectorEndPointAndDirection(
        XElement Element,
        out double EndX,
        out double EndY,
        out double DirX,
        out double DirY)
    {
        EndX = 0;
        EndY = 0;
        DirX = 0;
        DirY = 0;

        if (string.Equals(Element.Name.LocalName, "line", StringComparison.OrdinalIgnoreCase))
        {
            if (!double.TryParse(Element.Attribute("x1")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double StartX)
                || !double.TryParse(Element.Attribute("y1")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double StartY)
                || !double.TryParse(Element.Attribute("x2")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double EndPointX)
                || !double.TryParse(Element.Attribute("y2")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double EndPointY))
            {
                return false;
            }

            EndX = EndPointX;
            EndY = EndPointY;
            DirX = EndPointX - StartX;
            DirY = EndPointY - StartY;
            return true;
        }

        string? PathData = Element.Attribute("d")?.Value;
        if (string.IsNullOrWhiteSpace(PathData))
        {
            return false;
        }

        return TryGetSvgPathEndPointAndTangent(PathData, out EndX, out EndY, out DirX, out DirY);
    }

    /// <summary>
    /// Uses the path tangent at the stroke end so arrowheads align with the final segment (orthogonal elbows, curves).
    /// </summary>
    private static bool TryGetSvgPathEndPointAndTangent(
        string PathData,
        out double EndX,
        out double EndY,
        out double DirX,
        out double DirY)
    {
        EndX = 0;
        EndY = 0;
        DirX = 0;
        DirY = 0;

        try
        {
            using SKPath? Path = SKPath.ParseSvgPathData(PathData);
            if (Path == null || Path.IsEmpty)
            {
                return false;
            }

            using SKPathMeasure Measure = new SKPathMeasure(Path, false, 1f);
            float PathLength = Measure.Length;
            if (PathLength < 1e-6f)
            {
                return false;
            }

            if (!Measure.GetPositionAndTangent(PathLength, out SKPoint Position, out SKPoint Tangent))
            {
                return false;
            }

            EndX = Position.X;
            EndY = Position.Y;
            DirX = Tangent.X;
            DirY = Tangent.Y;

            // curveBasis ends often have a degenerate tangent at the stroke tip; use the incoming chord instead.
            const float IncomingDirectionSamplePx = 8f;
            float SampleDistance = Math.Min(IncomingDirectionSamplePx, PathLength * 0.2f);
            if (SampleDistance >= 1f
                && Measure.GetPosition(PathLength - SampleDistance, out SKPoint EarlierPoint))
            {
                DirX = Position.X - EarlierPoint.X;
                DirY = Position.Y - EarlierPoint.Y;
            }

            if (Math.Abs(DirX) < 1e-6 && Math.Abs(DirY) < 1e-6)
            {
                DirX = Tangent.X;
                DirY = Tangent.Y;
            }

            if (Math.Abs(DirX) < 1e-6 && Math.Abs(DirY) < 1e-6)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetSvgRootSizeBounds(string Svg, out SKRect Bounds)
    {
        Bounds = SKRect.Empty;
        try
        {
            XDocument Document = XDocument.Parse(Svg);
            XElement? Root = Document.Root;
            if (Root == null)
            {
                return false;
            }

            double Width = ParseSvgLength(Root.Attribute("width")?.Value);
            double Height = ParseSvgLength(Root.Attribute("height")?.Value);
            if (Width <= 0 || Height <= 0)
            {
                return false;
            }

            Bounds = SKRect.Create(0, 0, (float)Width, (float)Height);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetSvgViewBoxBounds(string Svg, out SKRect Bounds)
    {
        Bounds = SKRect.Empty;
        try
        {
            XDocument Document = XDocument.Parse(Svg);
            XElement? Root = Document.Root;
            if (Root == null)
            {
                return false;
            }

            if (!TryParseViewBox(
                    Root.Attribute("viewBox")?.Value,
                    out double ViewBoxX,
                    out double ViewBoxY,
                    out double ViewBoxWidth,
                    out double ViewBoxHeight))
            {
                return false;
            }

            if (ViewBoxWidth <= 0 || ViewBoxHeight <= 0)
            {
                return false;
            }

            Bounds = SKRect.Create(
                (float)ViewBoxX,
                (float)ViewBoxY,
                (float)ViewBoxWidth,
                (float)ViewBoxHeight);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[]? TryRasterizeSvgToPngViaHarness(string Svg)
    {
        string? HarnessProjectPath = FindSvgHarnessProjectPath();
        if (string.IsNullOrWhiteSpace(HarnessProjectPath) || !File.Exists(HarnessProjectPath))
        {
            return null;
        }

        string TempDirectory = Path.Combine(Path.GetTempPath(), "md2docx-svg-raster");
        Directory.CreateDirectory(TempDirectory);
        string InputSvgPath = Path.Combine(TempDirectory, Guid.NewGuid().ToString("N") + ".svg");
        string OutputPngPath = Path.Combine(TempDirectory, Guid.NewGuid().ToString("N") + ".png");

        try
        {
            string SvgForRaster = ExpandSvgFlowchartMarkersForRasterization(Svg);
            File.WriteAllText(InputSvgPath, SvgForRaster, new UTF8Encoding(false));

            ProcessStartInfo StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{HarnessProjectPath}\" -- --rasterise-svg \"{InputSvgPath}\" \"{OutputPngPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? Process = System.Diagnostics.Process.Start(StartInfo);
            if (Process == null)
            {
                return null;
            }

            Process.WaitForExit(120000);
            if (Process.ExitCode != 0 || !File.Exists(OutputPngPath))
            {
                return null;
            }

            return File.ReadAllBytes(OutputPngPath);
        }
        catch
        {
            return null;
        }
        finally
        {
            TryDeleteFile(InputSvgPath);
            TryDeleteFile(OutputPngPath);
        }
    }

    private static string? FindSvgHarnessProjectPath()
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, "src", "SvgHarness", "SvgHarness.csproj");
            if (File.Exists(Candidate))
            {
                return Candidate;
            }
            Current = Current.Parent;
        }

        return null;
    }

    private static void TryDeleteFile(string PathValue)
    {
        try
        {
            if (File.Exists(PathValue))
            {
                File.Delete(PathValue);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private (long Cx, long Cy) CalculateSvgExtents(SvgDimensions Dimensions) => CalculateEmbeddedExtentsFromPixels(Dimensions.WidthPixels, Dimensions.HeightPixels);

    private (long Cx, long Cy) CalculateEmbeddedExtentsFromPixels(int WidthPixels, int HeightPixels)
    {
        const double EmusPerCm = 360000.0;
        const double DefaultDpi = 96.0;
        const double MaxHeightCm = 18.0;

        double MaxWidthCm = _Options.Images.MaxWidthCm > 0 ? _Options.Images.MaxWidthCm : 16.0;
        double TargetWidthCm = Math.Min(MaxWidthCm, 14.5);

        if (WidthPixels <= 0 || HeightPixels <= 0)
        {
            double FallbackWidthCm = TargetWidthCm;
            double FallbackHeightCm = FallbackWidthCm * 0.5625;
            return (
                (long)Math.Round(FallbackWidthCm * EmusPerCm),
                (long)Math.Round(FallbackHeightCm * EmusPerCm));
        }

        double NaturalWidthCm = WidthPixels / DefaultDpi * 2.54;
        double AspectRatio = (double)HeightPixels / WidthPixels;

        double WidthCm = NaturalWidthCm;
        if (WidthCm <= 0)
        {
            WidthCm = TargetWidthCm;
        }
        else if (_Options.Images.ScaleDownOnly)
        {
            WidthCm = Math.Min(NaturalWidthCm, TargetWidthCm);
        }
        else
        {
            WidthCm = TargetWidthCm;
        }

        double HeightCm = WidthCm * AspectRatio;

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
            XDocument Document = XDocument.Parse(Svg);
            XElement? Root = Document.Root;
            if (Root == null)
            {
                return new SvgDimensions(800, 450);
            }

            double Width = ParseSvgLength(Root.Attribute("width")?.Value);
            double Height = ParseSvgLength(Root.Attribute("height")?.Value);
            if ((Width <= 0 || Height <= 0)
                && TryParseViewBox(
                    Root.Attribute("viewBox")?.Value,
                    out double ViewBoxOriginX,
                    out double ViewBoxOriginY,
                    out double ViewBoxWidth,
                    out double ViewBoxHeight))
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

        string Trimmed = Value.Trim();
        if (Trimmed.EndsWith('%'))
        {
            return 0;
        }

        int End = 0;
        while (End < Trimmed.Length && (char.IsDigit(Trimmed[End]) || Trimmed[End] == '.' || Trimmed[End] == '-'))
        {
            End++;
        }

        if (End == 0)
        {
            return 0;
        }

        ReadOnlySpan<char> TrimmedSpan = Trimmed.AsSpan(0, End);
        return double.TryParse(TrimmedSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double Result) ? Result : 0;
    }

    private static bool TryParseViewBox(string? Value, out double X, out double Y, out double Width, out double Height)
    {
        X = 0;
        Y = 0;
        Width = 0;
        Height = 0;
        if (string.IsNullOrWhiteSpace(Value))
        {
            return false;
        }

        string[] Parts = Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (Parts.Length != 4)
        {
            return false;
        }

        return double.TryParse(Parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out X)
            && double.TryParse(Parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out Y)
            && double.TryParse(Parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out Width)
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

    private (long Cx, long Cy) CalculateImageExtents(ImageDimensions Dimensions) => CalculateEmbeddedExtentsFromPixels(Dimensions.WidthPixels, Dimensions.HeightPixels);

    private static ImageDimensions GetImageDimensions(string Path)
    {
        try
        {
            using (FileStream Stream = File.OpenRead(Path))
            using (BinaryReader Reader = new BinaryReader(Stream))
            {
                byte[] Header = Reader.ReadBytes(24);
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

                        byte Marker = Reader.ReadByte();
                        ushort Length = ReadBigEndianUInt16(Reader);
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
        byte High = Reader.ReadByte();
        byte Low = Reader.ReadByte();
        return (ushort)((High << 8) | Low);
    }

    private static RunProperties CreateHyperlinkRunProperties(params OpenXmlElement[] ExtraProperties)
    {
        RunProperties Properties = new RunProperties(
            new RunStyle() { Val = "Hyperlink" },
            new Color() { Val = "0563C1" },
            new Underline() { Val = UnderlineValues.Single });
        foreach (OpenXmlElement ExtraProperty in ExtraProperties)
        {
            Properties.Append(ExtraProperty);
        }
        return Properties;
    }

    private readonly struct ImageDimensions(int WidthPixels, int HeightPixels)
    {
        public int WidthPixels { get; } = WidthPixels;

        public int HeightPixels { get; } = HeightPixels;
    }

    private readonly struct SvgDimensions(int WidthPixels, int HeightPixels)
    {
        public int WidthPixels { get; } = WidthPixels;

        public int HeightPixels { get; } = HeightPixels;
    }
}
