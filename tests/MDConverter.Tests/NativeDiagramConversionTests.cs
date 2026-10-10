using DocumentFormat.OpenXml.Packaging;
using MDConverter.Options;
using MermaidRecognition.Layout;

namespace MDConverter.Tests;

[TestClass]
public sealed class NativeDiagramConversionTests
{
    [TestMethod]
    public void PieAccessibility_EmbedsTheNativeImageWithWordMetadata()
    {
        string Source = "pie showData title Workload\n accTitle: Work split\n accDescr: Two categories.\n\"First\":3\n\"Second\":1";
        Assert.IsTrue(NativeDiagramRenderBridge.TryRenderSvg(Source, out string Svg));
        StringAssert.Contains(Svg, "data-native-renderer=\"pie\"");
        using var Document = Convert(Source);
        var Picture = Document.MainDocumentPart!.Document!.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Single();
        Assert.AreEqual("Work split", Picture.Title!.Value);
        Assert.AreEqual("Two categories.", Picture.Description!.Value);
        Assert.IsGreaterThan(0, Document.MainDocumentPart.ImageParts.Count());
    }

    [TestMethod]
    public void KindWithoutANativeAdapter_RetainsTheLegacyRenderPath()
    {
        const string Source = "sequenceDiagram\n Alice->>Bob: Hello";
        Assert.IsFalse(NativeDiagramRenderBridge.TryRenderSvg(Source, out _));
        using var Document = Convert(Source);
        Assert.IsGreaterThan(0, Document.MainDocumentPart!.ImageParts.Count());
        Assert.IsFalse(Document.MainDocumentPart.Document!.InnerText.Contains("Mermaid diagram not rendered", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MalformedPie_RetainsTheDiagnosticAndSource()
    {
        const string Source = "pie\nunsupported statement";
        Assert.ThrowsExactly<InvalidOperationException>(() => NativeDiagramRenderBridge.TryRenderSvg(Source, out _));
        using var Document = Convert(Source);
        StringAssert.Contains(Document.MainDocumentPart!.Document!.InnerText, "Mermaid diagram not rendered");
        StringAssert.Contains(Document.MainDocumentPart.Document.InnerText, "unsupported statement");
        Assert.AreEqual(0, Document.MainDocumentPart.ImageParts.Count());
    }

    private static WordprocessingDocument Convert(string Source)
    {
        byte[] Bytes = new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertText("```mermaid\n" + Source + "\n```", Environment.CurrentDirectory);
        return WordprocessingDocument.Open(new MemoryStream(Bytes), false);
    }
}
