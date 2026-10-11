using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MDConverter.Options;
using MermaidRecognition.Layout.Enterprise;

namespace MDConverter.Tests;

[TestClass]
public sealed class EnterpriseDocxTests
{
    [TestMethod]
    public void ConvertText_EnterpriseAccessibility_ReachesWordPictureProperties()
    {
        string Markdown = "```enterprise\nenterprise\n accTitle: Application view\n accDescr {\n One component.\n }\n application-component a \"Component\"\n```";
        byte[] Bytes = new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertText(Markdown, Environment.CurrentDirectory);
        using MemoryStream Stream = new(Bytes);
        using WordprocessingDocument Document = WordprocessingDocument.Open(Stream, false);
        var Picture = Document.MainDocumentPart!.Document!.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Single();
        Assert.AreEqual("Application view", Picture.Title!.Value);
        Assert.AreEqual("One component.", Picture.Description!.Value);
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg("enterprise\n accTitle: Application view\n accDescr: One component.\n application-component a \"Component\"");
        StringAssert.Contains(Svg, "<title>Application view</title>");
        StringAssert.Contains(Svg, "<desc>One component.</desc>");
    }

    [TestMethod]
    public void ConvertText_UnsupportedEnterpriseStatement_PreservesSourceAndDiagnostic()
    {
        string Source = "enterprise\n unsupported-feature xyz";
        byte[] Bytes = new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertText("```enterprise\n" + Source + "\n```", Environment.CurrentDirectory);
        using MemoryStream Stream = new(Bytes);
        using WordprocessingDocument Document = WordprocessingDocument.Open(Stream, false);
        string Text = Document.MainDocumentPart!.Document!.InnerText;
        StringAssert.Contains(Text, "Mermaid diagram not rendered");
        StringAssert.Contains(Text, "unsupported-feature xyz");
        Assert.AreEqual(0, Document.MainDocumentPart.ImageParts.Count());
    }

    [TestMethod]
    public void ConvertText_MermaidFenceWithEnterprise_EmbedsImage()
    {
        string Markdown = """
            # Shop

            ```mermaid
            enterprise
              title Web shop
              application-component webShop "Web Shop"
              application-service ordering "Online Ordering"
              webShop -assignment-> ordering
            ```
            """;

        AssertEmbeddedDiagram(Markdown);
    }

    [TestMethod]
    public void ConvertText_EnterpriseFence_EmbedsImage()
    {
        string Markdown = """
            ```enterprise
            enterprise
              application-component webShop "Web Shop"
              data-object order "Order"
              webShop -access(write)-> order
            ```
            """;

        AssertEmbeddedDiagram(Markdown);
    }

    [TestMethod]
    public void ConvertText_InvalidEnterprise_WritesFallbackMessage()
    {
        string SamplePath = FindSample("04-invalid-relationships.mmd");
        string Markdown = "```enterprise\n" + File.ReadAllText(SamplePath) + "\n```";
        MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions());
        byte[] Bytes = Converter.ConvertText(Markdown, Environment.CurrentDirectory);
        using MemoryStream Stream = new MemoryStream(Bytes);
        using WordprocessingDocument Document = WordprocessingDocument.Open(Stream, false);
        string Text = Document.MainDocumentPart!.Document!.InnerText;
        StringAssert.Contains(Text, "Mermaid diagram not rendered");
        StringAssert.Contains(Text, "line 12");
    }

    [TestMethod]
    public void ConvertText_MultipleEnterpriseErrors_UseOneVisibleLineEach()
    {
        string Source = """
            enterprise
              application-component shop "Web Shop"
              application-service shop "Second Web Shop"
              application-service ordering "Online Ordering"
              basket -serving-> shop
              ordering -serving-> checkout
              data-object class "Class list"
            """;
        byte[] Bytes = new MarkdownToDocxConverter(new MarkdownToDocxOptions())
            .ConvertText("```mermaid\n" + Source + "\n```", Environment.CurrentDirectory);
        using MemoryStream Stream = new(Bytes);
        using WordprocessingDocument Document = WordprocessingDocument.Open(Stream, false);
        Paragraph Fallback = Document.MainDocumentPart!.Document!.Descendants<Paragraph>()
            .Single(Item => Item.InnerText.Contains("Mermaid diagram not rendered", StringComparison.Ordinal));
        List<Text> Diagnostics = Fallback.Descendants<Text>()
            .Where(Item => Item.Text.StartsWith("line ", StringComparison.Ordinal)).ToList();
        Assert.HasCount(4, Diagnostics);
        foreach (Text Diagnostic in Diagnostics)
        {
            Run Run = (Run)Diagnostic.Parent!;
            int Index = Run.ChildElements.ToList().IndexOf(Diagnostic);
            Assert.IsGreaterThan(0, Index);
            Assert.IsInstanceOfType<Break>(Run.ChildElements[Index - 1]);
        }
    }

    [TestMethod]
    public void Rasterize_ApplicationSample_ProducesNonEmptyPng()
    {
        string Source = File.ReadAllText(FindSample("01-application-layer.mmd"));
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(Source);
        Assert.IsFalse(Svg.Contains("<marker", StringComparison.Ordinal));
        Assert.IsFalse(Svg.Contains("<foreignObject", StringComparison.Ordinal));
        byte[]? Png = MermaidDiagramExport.TryRasterizeSvg(Svg);
        Assert.IsNotNull(Png);
        Assert.IsGreaterThan(100, Png!.Length);
        CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, Png.Take(4).ToArray());
    }

    [TestMethod]
    public void RasterizeSource_EnterpriseBlock_ProducesNonEmptyPng()
    {
        string Source = File.ReadAllText(FindSample("03-cross-layer-webshop.mmd"));
        byte[]? Png = MermaidDiagramExport.TryRenderPng(Source);
        Assert.IsNotNull(Png);
        Assert.IsGreaterThan(100, Png!.Length);
    }

    private static void AssertEmbeddedDiagram(string Markdown)
    {
        MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions());
        byte[] Bytes = Converter.ConvertText(Markdown, Environment.CurrentDirectory);
        using MemoryStream Stream = new MemoryStream(Bytes);
        using ZipArchive Archive = new ZipArchive(Stream, ZipArchiveMode.Read);
        List<ZipArchiveEntry> Media = Archive.Entries
            .Where(Entry =>
                (Entry.FullName.Contains("media/", StringComparison.OrdinalIgnoreCase)
                    || Entry.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase))
                && (Entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || Entry.FullName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.IsGreaterThan(0, Media.Count, "Expected an embedded diagram image in the DOCX.");
        ZipArchiveEntry Image = Media[0];
        using Stream ImageStream = Image.Open();
        using MemoryStream Buffer = new MemoryStream();
        ImageStream.CopyTo(Buffer);
        Assert.IsGreaterThan(100, Buffer.Length);

        ZipArchiveEntry? DocumentEntry = Archive.GetEntry("word/document.xml");
        Assert.IsNotNull(DocumentEntry);
        using Stream DocumentStream = DocumentEntry.Open();
        XDocument Document = XDocument.Load(DocumentStream);
        Assert.IsFalse(
            Document.Root!.Value.Contains("Mermaid diagram not rendered", StringComparison.Ordinal),
            Document.Root.Value);
    }

    private static string FindSample(string FileName)
    {
        DirectoryInfo? DirectoryCursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (DirectoryCursor != null)
        {
            string Candidate = Path.Combine(DirectoryCursor.FullName, "docs", "enterprise", "samples", FileName);
            if (File.Exists(Candidate))
            {
                return Candidate;
            }

            DirectoryCursor = DirectoryCursor.Parent;
        }

        throw new FileNotFoundException("Sample not found: " + FileName);
    }
}
