using System.IO.Compression;
using System.Xml.Linq;

namespace MDConverter.Tests;

[TestClass]
public sealed class CommentedMermaidBlockTests
{
    [TestMethod]
    [DataRow("%% flowchart LR\n%% x --> y")]
    [DataRow("  %% deliberately disabled\n\n%% unsupported content")]
    public void Convert_CommentsOnly_PreservesCodeWithoutRenderingWarning(string Source)
    {
        MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(new MDConverter.Options.MarkdownToDocxOptions());
        byte[] Bytes = Converter.ConvertText("```mermaid\n" + Source + "\n```", Environment.CurrentDirectory);
        using MemoryStream Stream = new MemoryStream(Bytes);
        using ZipArchive Archive = new ZipArchive(Stream, ZipArchiveMode.Read);
        using Stream DocumentStream = Archive.GetEntry("word/document.xml")!.Open();
        XDocument Document = XDocument.Load(DocumentStream);
        Assert.IsFalse(Document.ToString().Contains("Mermaid diagram not rendered"));
        Assert.IsFalse(Document.Descendants().Any(Element => Element.Name.LocalName == "drawing"));
        Assert.IsTrue(Document.Root!.Value.Contains("%%"));
    }

    [TestMethod]
    public void Convert_ActiveInvalidContent_StillReportsRenderingFailure()
    {
        byte[] Bytes = new MarkdownToDocxConverter(new MDConverter.Options.MarkdownToDocxOptions()).ConvertText("```mermaid\n%% comment\ninvalid-active-content\n```", Environment.CurrentDirectory);
        using MemoryStream Stream = new MemoryStream(Bytes);
        using ZipArchive Archive = new ZipArchive(Stream, ZipArchiveMode.Read);
        using Stream DocumentStream = Archive.GetEntry("word/document.xml")!.Open();
        Assert.IsTrue(XDocument.Load(DocumentStream).Root!.Value.Contains("Mermaid diagram not rendered"));
    }
}
