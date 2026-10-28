using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using MDConverter.Options;

namespace MDConverter.Tests;

[TestClass]
public sealed class ConversionRegressionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConvertFile_FailurePreservesDestinationAndRemovesTemporaryFile(bool CorruptTemplate) => WithDirectory(Directory =>
                                                                                                             {
                                                                                                                 string Input = Path.Combine(Directory, "input.md");
                                                                                                                 string Output = Path.Combine(Directory, "output.docx");
                                                                                                                 string Template = Path.Combine(Directory, "template.docx");
                                                                                                                 File.WriteAllText(Input, "Hello");
                                                                                                                 byte[] Original = [1, 2, 3, 4];
                                                                                                                 File.WriteAllBytes(Output, Original);
                                                                                                                 if (CorruptTemplate)
                                                                                                                 {
                                                                                                                     File.WriteAllText(Template, "Not a DOCX package");
                                                                                                                 }

                                                                                                                 MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions
                                                                                                                 {
                                                                                                                     Docx = new DocxOptions { UseBuiltinTemplate = false, TemplatePath = Template }
                                                                                                                 });
                                                                                                                 bool Failed = false;
                                                                                                                 try { Converter.ConvertFile(Input, Output); }
                                                                                                                 catch (Exception) { Failed = true; }
                                                                                                                 Assert.IsTrue(Failed, "Invalid template should fail conversion.");
                                                                                                                 CollectionAssert.AreEqual(Original, File.ReadAllBytes(Output));
                                                                                                                 Assert.AreEqual(0, System.IO.Directory.GetFiles(Directory, ".md2docx-*.tmp").Length);
                                                                                                             });

    [TestMethod]
    public void ConvertFile_ReplacesDestinationOnlyAfterSuccessfulConversion() => WithDirectory(Directory =>
                                                                                       {
                                                                                           string Input = Path.Combine(Directory, "input.md");
                                                                                           string Output = Path.Combine(Directory, "nested", "output.docx");
                                                                                           File.WriteAllText(Input, "New content");
                                                                                           System.IO.Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
                                                                                           File.WriteAllText(Output, "Old content");
                                                                                           new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertFile(Input, Output);
                                                                                           using WordprocessingDocument Document = WordprocessingDocument.Open(Output, false);
                                                                                           Assert.AreEqual("New content", Document.MainDocumentPart!.Document!.InnerText);
                                                                                           AssertValid(Document);
                                                                                           Assert.AreEqual(0, System.IO.Directory.GetFiles(Path.GetDirectoryName(Output)!, ".md2docx-*.tmp").Length);
                                                                                       });

    [TestMethod]
    public void ConvertFile_LockedDestinationRemainsIntact() => WithDirectory(Directory =>
                                                                     {
                                                                         string Input = Path.Combine(Directory, "input.md");
                                                                         string Output = Path.Combine(Directory, "output.docx");
                                                                         File.WriteAllText(Input, "New content");
                                                                         File.WriteAllText(Output, "Original content");
                                                                         using (FileStream Locked = File.Open(Output, FileMode.Open, FileAccess.Read, FileShare.None))
                                                                         {
                                                                             bool Failed = false;
                                                                             try { new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertFile(Input, Output); }
                                                                             catch (Exception Ex) when (Ex is IOException or UnauthorizedAccessException) { Failed = true; }
                                                                             Assert.IsTrue(Failed, "Locked destination should prevent replacement.");
                                                                         }
                                                                         Assert.AreEqual("Original content", File.ReadAllText(Output));
                                                                         Assert.AreEqual(0, System.IO.Directory.GetFiles(Directory, ".md2docx-*.tmp").Length);
                                                                     });

    [TestMethod]
    public void Template_PreservesPageSettingsAndHeaderFooterReferences() => WithDirectory(Directory =>
                                                                                  {
                                                                                      string Template = Path.Combine(Directory, "template.docx");
                                                                                      string HeaderId;
                                                                                      string FooterId;
                                                                                      using (WordprocessingDocument Document = WordprocessingDocument.Create(Template, WordprocessingDocumentType.Document))
                                                                                      {
                                                                                          MainDocumentPart Main = Document.AddMainDocumentPart();
                                                                                          HeaderPart Header = Main.AddNewPart<HeaderPart>();
                                                                                          Header.Header = new Header(new Paragraph(new Run(new Text("Header text"))));
                                                                                          FooterPart Footer = Main.AddNewPart<FooterPart>();
                                                                                          Footer.Footer = new Footer(new Paragraph(new Run(new Text("Footer text"))));
                                                                                          HeaderId = Main.GetIdOfPart(Header);
                                                                                          FooterId = Main.GetIdOfPart(Footer);
                                                                                          SectionProperties Section = new SectionProperties(
                                                                                              new HeaderReference { Type = HeaderFooterValues.Default, Id = Main.GetIdOfPart(Header) },
                                                                                              new FooterReference { Type = HeaderFooterValues.Default, Id = Main.GetIdOfPart(Footer) },
                                                                                              new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape },
                                                                                              new PageMargin { Left = 720, Right = 720, Top = 720, Bottom = 720, Header = 360, Footer = 360, Gutter = 0 });
                                                                                          Main.Document = new Document(new Body(new Paragraph(new Run(new Text("Old content"))), Section));
                                                                                      }
                                                                                      MarkdownToDocxOptions Options = new MarkdownToDocxOptions { Docx = new DocxOptions { UseBuiltinTemplate = false, TemplatePath = Template } };
                                                                                      using MemoryStream Stream = new MemoryStream(new MarkdownToDocxConverter(Options).ConvertText("New content", Directory));
                                                                                      using WordprocessingDocument Converted = WordprocessingDocument.Open(Stream, false);
                                                                                      MainDocumentPart MainPart = Converted.MainDocumentPart!;
                                                                                      SectionProperties SectionResult = (SectionProperties)MainPart.Document!.Body!.LastChild!;
                                                                                      Assert.AreEqual(16838U, SectionResult.GetFirstChild<PageSize>()!.Width!.Value);
                                                                                      Assert.AreEqual(11906U, SectionResult.GetFirstChild<PageSize>()!.Height!.Value);
                                                                                      Assert.AreEqual(PageOrientationValues.Landscape, SectionResult.GetFirstChild<PageSize>()!.Orient!.Value);
                                                                                      Assert.AreEqual(720U, SectionResult.GetFirstChild<PageMargin>()!.Left!.Value);
                                                                                      Assert.AreEqual(720, SectionResult.GetFirstChild<PageMargin>()!.Top!.Value);
                                                                                      Assert.AreEqual(HeaderId, SectionResult.GetFirstChild<HeaderReference>()!.Id!.Value);
                                                                                      Assert.AreEqual(FooterId, SectionResult.GetFirstChild<FooterReference>()!.Id!.Value);
                                                                                      Assert.AreEqual("New content", MainPart.Document.Body.InnerText);
                                                                                      Assert.AreEqual("Header text", MainPart.HeaderParts.Single().Header!.InnerText);
                                                                                      Assert.AreEqual("Footer text", MainPart.FooterParts.Single().Footer!.InnerText);
                                                                                      AssertValid(Converted);
                                                                                  });

    [TestMethod]
    public void FormattedLinks_PreserveTextTargetAndFormattingWithoutAffectingSiblings()
    {
        using MemoryStream Stream = new MemoryStream(new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertText(
            "**Before [bold *italic*](https://example.com) after**\n\n*[italic](https://example.org)*\n\n[plain **bold** plain](https://example.net)", ""));
        using WordprocessingDocument Document = WordprocessingDocument.Open(Stream, false);
        MainDocumentPart Main = Document.MainDocumentPart!;
        List<Hyperlink> Links = [.. Main.Document!.Descendants<Hyperlink>()];
        Assert.AreEqual(3, Links.Count);
        Assert.AreEqual("Before bold italic afteritalicplain bold plain", Main.Document!.InnerText);
        Assert.AreEqual(3, Main.HyperlinkRelationships.Count());
        Assert.IsTrue(Links[0].Elements<Run>().All(Run => Run.RunProperties!.Bold != null));
        Assert.IsNotNull(Links[0].Elements<Run>().Last().RunProperties!.Italic);
        Assert.IsNotNull(Links[1].Elements<Run>().Single().RunProperties!.Italic);
        List<Run> Runs = [.. Links[2].Elements<Run>()];
        Assert.IsNull(Runs[0].RunProperties!.Bold);
        Assert.IsNotNull(Runs[1].RunProperties!.Bold);
        Assert.IsNull(Runs[2].RunProperties!.Bold);
        AssertValid(Document);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void InternalLinks_ResolveForwardDuplicateUnicodeAndNestedHeadings(bool Enabled)
    {
        MarkdownToDocxOptions Options = new MarkdownToDocxOptions { Links = new LinkOptions { EnableInternalLinks = Enabled } };
        using MemoryStream Stream = new MemoryStream(new MarkdownToDocxConverter(Options).ConvertText(
            "[first](#hello-world) [second](#hello-world-1) [unicode](#caf%C3%A9) [quote](#quoted) [missing](#absent)\n\n# Hello **World**!\n\n# Hello World!\n\n# Café\n\n> # Quoted", ""));
        using WordprocessingDocument Document = WordprocessingDocument.Open(Stream, false);
        MainDocumentPart Main = Document.MainDocumentPart!;
        List<BookmarkStart> Bookmarks = [.. Main.Document!.Descendants<BookmarkStart>()];
        List<Hyperlink> Links = [.. Main.Document!.Descendants<Hyperlink>()];
        Assert.AreEqual(Enabled ? 4 : 0, Bookmarks.Count);
        Assert.AreEqual(Enabled ? 4 : 0, Links.Count);
        if (Enabled)
        {
            CollectionAssert.AreEqual(Bookmarks.Select(Bookmark => Bookmark.Name!.Value).ToArray(), Links.Select(Link => Link.Anchor!.Value).ToArray());
            Assert.AreEqual(4, Bookmarks.Select(Bookmark => Bookmark.Id!.Value).Distinct().Count());
            Assert.AreEqual(4, Main.Document!.Descendants<BookmarkEnd>().Count());
        }
        Assert.IsTrue(Main.Document!.InnerText.Contains("missing", StringComparison.Ordinal));
        AssertValid(Document);
    }

    [TestMethod]
    public void DisabledExternalLinks_PreserveNestedFormattedText()
    {
        MarkdownToDocxOptions Options = new MarkdownToDocxOptions { Links = new LinkOptions { EnableExternalLinks = false } };
        using MemoryStream Stream = new MemoryStream(new MarkdownToDocxConverter(Options).ConvertText("**[Go](https://example.com)**", ""));
        using WordprocessingDocument Document = WordprocessingDocument.Open(Stream, false);
        Assert.AreEqual("Go", Document.MainDocumentPart!.Document!.InnerText);
        Assert.AreEqual(0, Document.MainDocumentPart.HyperlinkRelationships.Count());
        Assert.IsNotNull(Document.MainDocumentPart.Document!.Descendants<Run>().Single().RunProperties!.Bold);
        AssertValid(Document);
    }

    private static void AssertValid(WordprocessingDocument Document)
    {
        string[] Errors = [.. new OpenXmlValidator().Validate(Document.MainDocumentPart!.Document!).Select(Error => Error.Description)];
        Assert.AreEqual(0, Errors.Length, string.Join(Environment.NewLine, Errors));
    }

    private static void WithDirectory(Action<string> Action)
    {
        string Directory = Path.Combine(Path.GetTempPath(), "md2docx-regression-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        try { Action(Directory); }
        finally { System.IO.Directory.Delete(Directory, recursive: true); }
    }
}
