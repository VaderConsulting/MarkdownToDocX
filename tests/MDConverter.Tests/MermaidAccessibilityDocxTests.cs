using System.IO.Compression;
using System.Xml.Linq;
using MDConverter.Options;

namespace MDConverter.Tests;

[TestClass]
public sealed class MermaidAccessibilityDocxTests
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

    const string F72_SOURCE = """
        accTitle: Batch ingest overview
        accDescr: Three-step ingest pipeline for recognition regression.
        flowchart LR
        A[Drop] --> B[Verify] --> C[Publish]
        """;

    [TestMethod]
    public void GeneratedDocx_F72Accessibility_SetsPictureTitleAndDescription()
    {
        MarkdownToDocxOptions Options = new MarkdownToDocxOptions();
        MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options);
        string Markdown = "```mermaid\n" + F72_SOURCE + "\n```";
        byte[] DocxBytes = Converter.ConvertText(Markdown, Environment.CurrentDirectory);

        using MemoryStream Stream = new MemoryStream(DocxBytes);
        using ZipArchive Archive = new ZipArchive(Stream, ZipArchiveMode.Read);
        ZipArchiveEntry? DocumentEntry = Archive.GetEntry("word/document.xml");
        Assert.IsNotNull(DocumentEntry);

        XDocument Document;
        using (Stream DocumentStream = DocumentEntry.Open())
        {
            Document = XDocument.Load(DocumentStream);
        }

        XElement? DocProperties = Document
            .Descendants(Wp + "docPr")
            .FirstOrDefault(Element => (string?)Element.Attribute("descr") != null);
        Assert.IsNotNull(DocProperties, "Expected a drawing with Word docPr accessibility attributes.");

        string? Description = DocProperties.Attribute("descr")?.Value;
        string? Title = DocProperties.Attribute("title")?.Value;
        Assert.AreEqual("Three-step ingest pipeline for recognition regression.", Description);
        Assert.AreEqual("Batch ingest overview", Title);

        XElement? PictureProperties = Document
            .Descendants(Pic + "cNvPr")
            .FirstOrDefault(Element => (string?)Element.Attribute("descr") != null);
        Assert.IsNotNull(PictureProperties);
        Assert.AreEqual("Three-step ingest pipeline for recognition regression.", PictureProperties.Attribute("descr")?.Value);
        Assert.AreEqual("Batch ingest overview", PictureProperties.Attribute("title")?.Value);
    }
}
