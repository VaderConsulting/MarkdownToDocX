using System.IO.Compression;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using MermaidSharp;
using MermaidSharp.Diagrams.Block;
using MDConverter.Options;
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace MDConverter.Tests;

[TestClass]
public sealed class DocxPackageAssertionsTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    [TestMethod]
    public void GeneratedDocx_UsesNativeDecimalAndBulletNumbering()
    {
        using TestPackage Package = CreatePackage(
            """
            Ordered list:

            1. First step.
            2. Second step.

            Ordered list with start number:

            3. Start from three.
            4. Then four.

            Unordered list:

            - Bullet one
            - Bullet two

            Nested:

            - Parent one
              1. Nested ordered A
              2. Nested ordered B
            - Parent two
              1. Nested ordered C

            Quote:

            > Quoted list:
            > - Item one
            > - Item two
            > 1. Numbered inside quote
            > 2. Second
            """);

        AssertAbstractNumberingPrecedesNumberingInstances(Package.Numbering);
        AssertParagraphNumberFormat(Package, "First step.", "decimal", "1");
        AssertParagraphNumberFormat(Package, "Second step.", "decimal", "1");
        AssertParagraphNumberFormat(Package, "Start from three.", "decimal", "3");
        AssertParagraphNumberFormat(Package, "Then four.", "decimal", "3");
        AssertParagraphNumberFormat(Package, "Bullet one", "bullet", "1");
        AssertParagraphNumberFormat(Package, "Nested ordered A", "decimal", "1");
        AssertParagraphNumberFormat(Package, "Nested ordered C", "decimal", "1");
        AssertParagraphNumberFormat(Package, "Item one", "bullet", "1");
        AssertParagraphNumberFormat(Package, "Numbered inside quote", "decimal", "1");
    }

    [TestMethod]
    public void GeneratedDocx_WritesHyperlinksImagesTablesCodeAndHorizontalRules()
    {
        string BasePath = CreateTempImageFolder();

        using TestPackage Package = CreatePackage(
            """
            [Example](https://example.com)

            ![Logo](images/logo.png)

            ~~deleted text~~

            Footnote marker[^note].

            [^note]: Footnote body text.

            ---

            ```csharp
            public void Method()
            {
                return;
            }
            ```

            | Left | Center | Right |
            | :--- | :---: | ---: |
            | L1 | C1 | R1 |
            """,
            BasePath);

        Assert.IsTrue(Package.Relationships.Descendants(Rel + "Relationship")
            .Any(Relationship =>
                Relationship.Attribute("Type")?.Value.EndsWith("/hyperlink", StringComparison.Ordinal) == true
                && Relationship.Attribute("Target")?.Value == "https://example.com"
                && Relationship.Attribute("TargetMode")?.Value == "External"),
            "Expected an external hyperlink relationship to https://example.com.");

        Assert.IsTrue(Package.Document.Descendants(W + "hyperlink")
            .Any(Hyperlink => Hyperlink.Attribute(R + "id") != null),
            "Expected document hyperlink elements to reference relationship ids.");

        Assert.IsTrue(Package.EntryNames.Any(Name => Name.Contains("media/", StringComparison.OrdinalIgnoreCase)),
            "Expected an embedded image media part.");

        Assert.IsTrue(Package.Document.Descendants(W + "drawing").Any(),
            "Expected an inline drawing for the embedded image.");

        Assert.IsFalse(DocumentText(Package.Document).Contains("Missing image: Logo", StringComparison.Ordinal),
            "The existing local image should not render as a missing-image placeholder.");

        Assert.IsTrue(Package.Document.Descendants(W + "tblBorders").Any(),
            "Expected generated tables to have visible borders.");

        Assert.IsTrue(Package.Document.Descendants(W + "jc")
            .Any(Justification => Justification.Attribute(W + "val")?.Value == "center"),
            "Expected center table alignment to be written.");

        Assert.IsTrue(Package.Document.Descendants(W + "jc")
            .Any(Justification => Justification.Attribute(W + "val")?.Value == "right"),
            "Expected right table alignment to be written.");

        Assert.IsTrue(Package.Document.Descendants(W + "pStyle")
            .Any(Style => Style.Attribute(W + "val")?.Value == "MdCodeBlock"),
            "Expected fenced code to use the code block paragraph style.");

        Assert.IsTrue(Package.Document.Descendants(W + "noProof").Any(),
            "Expected code runs/styles to disable spell checking.");

        Assert.IsTrue(Package.Document.Descendants(W + "strike").Any(),
            "Expected strikethrough Markdown to become Word strike formatting.");

        Assert.IsTrue(DocumentText(Package.Document).Contains("Footnote body text.", StringComparison.Ordinal),
            "Expected footnote body text to be preserved.");

        Assert.IsTrue(Package.Document.Descendants(W + "pBdr").Any(),
            "Expected horizontal rules to be paragraph borders, not text underscores.");

        Assert.IsFalse(DocumentText(Package.Document).Contains("____", StringComparison.Ordinal),
            "Horizontal rules should not be emitted as underscore text.");
    }

    [TestMethod]
    public void GeneratedDocx_RendersMermaidFencesAsEmbeddedSvgImages()
    {
        using TestPackage Package = CreatePackage(
            """
            Backtick fence:

            ```mermaid
            flowchart TD
                A[Start] --> B[Done]
            ```

            Colon fence:

            :::mermaid
            sequenceDiagram
                Alice->>Bob: Hello
                Bob-->>Alice: Hi
            :::
            """);

        Assert.IsGreaterThanOrEqualTo(2, CountEmbeddedMermaidImages(Package),
            "Expected both Mermaid diagrams to be embedded as PNG or SVG media parts.");

        Assert.IsGreaterThanOrEqualTo(Package.Document.Descendants(W + "drawing").Count(), 2,
            "Expected both Mermaid fences to become Word drawing elements.");

        string Text = DocumentText(Package.Document);
        Assert.IsFalse(Text.Contains("flowchart TD", StringComparison.Ordinal),
            "Mermaid source should not be emitted as visible code text when SVG rendering succeeds.");
        Assert.IsFalse(Text.Contains("sequenceDiagram", StringComparison.Ordinal),
            "Colon-fenced Mermaid source should not be emitted as visible code text when SVG rendering succeeds.");

        string FlowchartSvg = Mermaid.Render(
            """
            flowchart TD
                A[Start] --> B[Done]
            """,
            RenderOptions.Default);
        string SequenceSvg = Mermaid.Render(
            """
            sequenceDiagram
                Alice->>Bob: Hello
                Bob-->>Alice: Hi
            """,
            RenderOptions.Default);
        Assert.IsFalse(FlowchartSvg.Contains("<foreignObject", StringComparison.OrdinalIgnoreCase),
            "Flowchart SVG should not use foreignObject labels because Word does not reliably render them.");
        Assert.IsFalse(SequenceSvg.Contains("<foreignObject", StringComparison.OrdinalIgnoreCase),
            "Sequence SVG should not use foreignObject labels because Word does not reliably render them.");
    }

    [TestMethod]
    public void GeneratedDocx_RendersSupportedMermaidExampleSyntax()
    {
        const string ClassDiagramSource = """
            classDiagram
            class Animal {
            +String name
            +move()
            }
            class Dog {
            +bark()
            }
            Animal <|-- Dog
            """;

        const string MindmapSource = """
            mindmap
              MarkdownToDocX
                Markdown
                Mermaid
            """;

        const string KanbanSource = """
            kanban
            todo[To do]
            docs[Add docs]
            doing[Doing]
            mermaid[Render Mermaid]
            done[Done]
            tables[Tables]
            """;

        const string GitGraphSource = """
            gitGraph
                commit id: "start"
                branch diagrams
                checkout diagrams
                commit id: "svg"
                checkout main
                merge diagrams id: "merge"
            """;

        using TestPackage Package = CreatePackage(
            $"""
            ```mermaid
            {ClassDiagramSource}
            ```

            ```mermaid
            {MindmapSource}
            ```

            ```mermaid
            {KanbanSource}
            ```

            ```mermaid
            {GitGraphSource}
            ```
            """);

        Assert.IsGreaterThanOrEqualTo(4, CountEmbeddedMermaidImages(Package),
            "Expected four Mermaid diagrams to be embedded as PNG or SVG media parts.");

        string ClassText = RenderFlattenedMermaidText(ClassDiagramSource);
        Assert.IsTrue(ClassText.Contains("Animal", StringComparison.Ordinal) && ClassText.Contains("Dog", StringComparison.Ordinal),
            "Expected class diagram SVG to include both classes.");

        string MindmapText = RenderFlattenedMermaidText(MindmapSource);
        Assert.IsTrue(MindmapText.Contains("MarkdownToDocX", StringComparison.Ordinal) && !MindmapText.Contains("root((", StringComparison.Ordinal),
            "Expected mindmap SVG to render the root label, not literal shape syntax.");

        string KanbanText = RenderFlattenedMermaidText(KanbanSource);
        Assert.IsTrue(KanbanText.Contains("To do", StringComparison.Ordinal) && KanbanText.Contains("Render Mermaid", StringComparison.Ordinal) && !KanbanText.Contains("Empty board", StringComparison.Ordinal),
            "Expected kanban SVG to render board columns/items.");

        string GitGraphText = RenderFlattenedMermaidText(GitGraphSource);
        Assert.IsTrue(GitGraphText.Contains("diagrams", StringComparison.Ordinal) && GitGraphText.Contains("svg", StringComparison.Ordinal) && GitGraphText.Contains("merge", StringComparison.Ordinal),
            "Expected git graph SVG to render the intended branch and commit labels.");
    }

    [TestMethod]
    public void Naiad_BlockWithDirectedEdge_RendersNodeLabelsInSvg()
    {
        Result<char, string> ArrowProbe = String("-->").Parse("--> ");
        Assert.IsTrue(ArrowProbe.Success, "Pidgin String(\"-->\") smoke test.");

        string BlockInput = string.Join("\n", "block-beta", "columns 3", "a[\"Source\"] b[\"Sink\"]", "a --> b");

        Parser<char, string> LocalIdentifier =
            Token(static Character => char.IsLetter(Character) || char.IsDigit(Character) || Character == '_')
                .Then(
                    Token(static Character => char.IsLetterOrDigit(Character) || Character is '_' or '-').ManyString(),
                    (FirstCharacter, Rest) => FirstCharacter.ToString() + Rest);
        Parser<char, bool> LocalDirected =
            Try(String("-->").ThenReturn(true)).Or(String("---").ThenReturn(false));
        Parser<char, (string FromId, bool Directed, string ToId)> LocalEdge =
            from _ in MermaidSharp.CommonParsers.InlineWhitespace
            from FromId in LocalIdentifier
            from __ in MermaidSharp.CommonParsers.InlineWhitespace
            from Directed in LocalDirected
            from ___ in MermaidSharp.CommonParsers.InlineWhitespace
            from ToId in LocalIdentifier
            from ____ in MermaidSharp.CommonParsers.InlineWhitespace
            from _____ in MermaidSharp.CommonParsers.LineEnd
            select (FromId, Directed, ToId);

        Result<char, (string FromId, bool Directed, string ToId)> LocalEdgeParsed = LocalEdge.Parse("a --> b");
        if (!LocalEdgeParsed.Success)
        {
            Assert.Fail(LocalEdgeParsed.Error?.ToString() ?? "Edge parsing failed.");
        }

        BlockParser Parser = new BlockParser();
        Result<char, BlockModel> Parsed = Parser.Parse(BlockInput);
        if (!Parsed.Success)
        {
            string EdgeLine = BlockInput.Split('\n').Last();
            List<int> CodePoints = [];
            foreach (char Character in EdgeLine)
            {
                CodePoints.Add((int)Character);
            }
            Assert.Fail((Parsed.Error?.ToString() ?? "parse") + " | EdgeLine='" + EdgeLine + "' | CodePoints=" + string.Join(",", CodePoints));
        }

        string BlockSvg = Mermaid.Render(BlockInput, RenderOptions.Default);
        Assert.IsTrue(BlockSvg.Contains("Source", StringComparison.Ordinal) && BlockSvg.Contains("Sink", StringComparison.Ordinal),
            "Naiad block diagram should render quoted labels in SVG text elements.");
    }

    [TestMethod]
    public void Mermaid_Render_MindmapRootCircleSyntax_ExposesInnerLabel()
    {
        string MindmapText = RenderFlattenedMermaidText(
            """
            mindmap
              root((RingRoot))
                LeafA
            """);

        Assert.IsTrue(MindmapText.Contains("RingRoot", StringComparison.Ordinal),
            "Mindmap root((...)) should render the inner label text.");
        Assert.IsFalse(MindmapText.Contains("root((", StringComparison.Ordinal),
            "Mindmap SVG should not retain the raw root(( token sequence.");
        Assert.IsTrue(MindmapText.Contains("LeafA", StringComparison.Ordinal),
            "Mindmap child labels should render.");
    }

    [TestMethod]
    public void GeneratedDocx_RendersMindmapRootKwIndentedKanbanAndBlockEdges()
    {
        using TestPackage Package = CreatePackage(
            """
            ```mermaid
            mindmap
              root((RingRoot))
                LeafA
            ```

            ```mermaid
            kanban
                todo[Column one]
                    ta[Task Alpha]
                doing[Column two]
            ```

            ```mermaid
            block-beta
            columns 3
            a["Source"] b["Sink"]
            a --> b
            ```
            """);

        Assert.IsGreaterThanOrEqualTo(3, CountEmbeddedMermaidImages(Package),
            "Expected three Mermaid diagrams to be embedded as PNG or SVG media parts.");

        const string MindmapSource = """
            mindmap
              root((RingRoot))
                LeafA
            """;

        const string KanbanSource = """
            kanban
                todo[Column one]
                    ta[Task Alpha]
                doing[Column two]
            """;

        const string BlockSource = """
            block-beta
            columns 3
            a["Source"] b["Sink"]
            a --> b
            """;

        string MindmapText = RenderFlattenedMermaidText(MindmapSource);
        Assert.IsTrue(MindmapText.Contains("RingRoot", StringComparison.Ordinal) && !MindmapText.Contains("root((", StringComparison.Ordinal),
            "Mindmap root((...)) syntax should expose the inner label text, not the raw root(( token sequence.");

        string KanbanText = RenderFlattenedMermaidText(KanbanSource);
        Assert.IsTrue(KanbanText.Contains("Task Alpha", StringComparison.Ordinal) && !KanbanText.Contains("Empty board", StringComparison.Ordinal),
            "Indented Kanban tasks should nest under indented columns.");

        string BlockSvg = Mermaid.Render(BlockSource, RenderOptions.Default);
        Assert.IsTrue(BlockSvg.Contains("Source", StringComparison.Ordinal) && BlockSvg.Contains("Sink", StringComparison.Ordinal),
            "Block diagram elements should render.");
        Assert.IsTrue(BlockSvg.Contains("stroke=\"#555\"", StringComparison.Ordinal),
            "Block diagram edges (-->) should emit connector path markup.");
    }

    private static int CountEmbeddedMermaidImages(TestPackage Package) => Package.EntryNames.Count(Name =>
                                                                                   Name.Contains("media/", StringComparison.OrdinalIgnoreCase)
                                                                                   && (Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                                                                       || Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)));

    private static string RenderFlattenedMermaidText(string MermaidSource)
    {
        string Svg = Mermaid.Render(MermaidSource.Trim(), RenderOptions.Default);
        return FlattenSvgText(Svg);
    }

    private static string FlattenSvgText(string Svg) => Regex.Replace(
            string.Concat(XDocument.Parse(Svg).Descendants().Select(Element => Element.Value)),
            @"\s+",
            " ");

    private static TestPackage CreatePackage(string Markdown, string? BasePath = null)
    {
        MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions());
        byte[] Bytes = Converter.ConvertText(Markdown, BasePath ?? Environment.CurrentDirectory);
        return TestPackage.Open(Bytes);
    }

    private static string CreateTempImageFolder()
    {
        string DirectoryPath = Path.Combine(Path.GetTempPath(), "MDConverter.Tests." + Guid.NewGuid().ToString("N"));
        string ImageDirectoryPath = Path.Combine(DirectoryPath, "images");
        Directory.CreateDirectory(ImageDirectoryPath);
        File.WriteAllBytes(Path.Combine(ImageDirectoryPath, "logo.png"), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAGUlEQVR4nGP8z0A+YKJA76jmUc2jmkc1U0EzACKcAhF9M2bsAAAAAElFTkSuQmCC"));
        return DirectoryPath;
    }

    private static void AssertAbstractNumberingPrecedesNumberingInstances(XDocument Numbering)
    {
        List<string> Children = [.. Numbering.Root!.Elements().Select(Element => Element.Name.LocalName)];
        int FirstNumberingInstanceIndex = Children.IndexOf("num");
        int LastAbstractNumberingIndex = Children.LastIndexOf("abstractNum");

        Assert.IsGreaterThanOrEqualTo(0, FirstNumberingInstanceIndex, "Expected numbering instances.");
        Assert.IsGreaterThanOrEqualTo(0, LastAbstractNumberingIndex, "Expected abstract numbering definitions.");
        Assert.IsLessThan(FirstNumberingInstanceIndex, LastAbstractNumberingIndex,
            "All w:abstractNum elements must appear before any w:num element, or Word may repair numbering incorrectly.");
    }

    private static void AssertParagraphNumberFormat(TestPackage Package, string ParagraphText, string ExpectedFormat, string ExpectedStart)
    {
        XElement Paragraph = FindParagraph(Package.Document, ParagraphText);
        XElement? NumberingProperties = Paragraph.Descendants(W + "numPr").FirstOrDefault();
        Assert.IsNotNull(NumberingProperties, $"Expected paragraph '{ParagraphText}' to use native Word numbering.");

        string NumberingId = NumberingProperties.Descendants(W + "numId").First().Attribute(W + "val")!.Value;
        string LevelIndex = NumberingProperties.Descendants(W + "ilvl").First().Attribute(W + "val")!.Value;

        XElement NumberingInstance = Package.Numbering.Descendants(W + "num")
            .First(Num => Num.Attribute(W + "numId")?.Value == NumberingId);
        string AbstractNumberingId = NumberingInstance.Descendants(W + "abstractNumId").First().Attribute(W + "val")!.Value;
        XElement AbstractNumbering = Package.Numbering.Descendants(W + "abstractNum")
            .First(Num => Num.Attribute(W + "abstractNumId")?.Value == AbstractNumberingId);
        XElement Level = AbstractNumbering.Descendants(W + "lvl")
            .First(Lvl => Lvl.Attribute(W + "ilvl")?.Value == LevelIndex);

        Assert.AreEqual(ExpectedFormat, Level.Descendants(W + "numFmt").First().Attribute(W + "val")!.Value,
            $"Unexpected numbering format for paragraph '{ParagraphText}'.");
        Assert.AreEqual(ExpectedStart, Level.Descendants(W + "start").First().Attribute(W + "val")!.Value,
            $"Unexpected numbering start for paragraph '{ParagraphText}'.");
    }

    private static XElement FindParagraph(XDocument Document, string Text)
    {
        XElement? Paragraph = Document.Descendants(W + "p")
            .FirstOrDefault(Paragraph => ParagraphText(Paragraph) == Text);
        Assert.IsNotNull(Paragraph, $"Could not find paragraph with text '{Text}'.");
        return Paragraph;
    }

    private static string ParagraphText(XElement Paragraph)
    {
        return string.Concat(Paragraph.Descendants(W + "t").Select(Text => Text.Value));
    }

    private static string DocumentText(XDocument Document)
    {
        return string.Concat(Document.Descendants(W + "t").Select(Text => Text.Value));
    }

    private sealed class TestPackage : IDisposable
    {
        private readonly MemoryStream _Stream;
        private readonly ZipArchive _Archive;

        private TestPackage(MemoryStream Stream, ZipArchive Archive, XDocument Document, XDocument Numbering, XDocument Relationships, List<string> EntryNames)
        {
            _Stream = Stream;
            _Archive = Archive;
            this.Document = Document;
            this.Numbering = Numbering;
            this.Relationships = Relationships;
            this.EntryNames = EntryNames;
        }

        public XDocument Document { get; }

        public XDocument Numbering { get; }

        public XDocument Relationships { get; }

        public IReadOnlyList<string> EntryNames { get; }

        public IReadOnlyList<string> ReadEntries(string Suffix)
        {
            return _Archive.Entries
                .Where(Entry => Entry.FullName.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
                .Select(Entry =>
                {
                    using Stream Stream = Entry.Open();
                    using StreamReader Reader = new StreamReader(Stream);
                    return Reader.ReadToEnd();
                })
                .ToList();
        }

        public static TestPackage Open(byte[] Bytes)
        {
            MemoryStream Stream = new MemoryStream(Bytes);
            ZipArchive Archive = new ZipArchive(Stream, ZipArchiveMode.Read, leaveOpen: false);
            return new TestPackage(
                Stream,
                Archive,
                ReadXml(Archive, "word/document.xml"),
                ReadXml(Archive, "word/numbering.xml"),
                ReadXml(Archive, "word/_rels/document.xml.rels"),
                [.. Archive.Entries.Select(Entry => Entry.FullName)]);
        }

        public void Dispose()
        {
            _Archive.Dispose();
            _Stream.Dispose();
        }

        private static XDocument ReadXml(ZipArchive Archive, string EntryName)
        {
            ZipArchiveEntry Entry = Archive.GetEntry(EntryName)
                ?? throw new AssertFailedException("Missing DOCX package entry: " + EntryName);
            using Stream Stream = Entry.Open();
            return XDocument.Load(Stream);
        }
    }
}
