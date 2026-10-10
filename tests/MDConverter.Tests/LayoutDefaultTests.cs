using System.IO.Compression;
using MDConverter.Options;
using MermaidRecognition.Config;

namespace MDConverter.Tests;

[TestClass]
public sealed class LayoutDefaultTests
{
    [TestMethod]
    public void RunWideLayoutDefault_ReachesDiagramAndFrontmatterWins()
    {
        string Source = "enterprise\napplication-component first\napplication-component second\napplication-component third\nfirst -flow-> second\nfirst -flow-> third";
        byte[] DefaultOpen = Image(Source, DiagramLayoutMode.Open);
        byte[] ExplicitOpen = Image("---\nconfig:\n  layout: open\n---\n" + Source, null);
        CollectionAssert.AreEqual(DefaultOpen, ExplicitOpen);
        byte[] OverrideGrid = Image("---\nconfig:\n  layout: grid\n---\n" + Source, DiagramLayoutMode.Open);
        CollectionAssert.AreEqual(OverrideGrid, Image(Source, null));
    }

    private static byte[] Image(string Source, DiagramLayoutMode? Default)
    {
        var Converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions
            { Mermaid = new MermaidOptions { DefaultLayout = Default } });
        byte[] Bytes = Converter.ConvertText("```mermaid\n" + Source + "\n```", Environment.CurrentDirectory);
        using var Stream = new MemoryStream(Bytes);
        using var Archive = new ZipArchive(Stream, ZipArchiveMode.Read);
        using var Input = Archive.Entries.First(Entry => Entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).Open();
        using var Output = new MemoryStream();
        Input.CopyTo(Output);
        return Output.ToArray();
    }
}
