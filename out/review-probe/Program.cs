using MDConverter;
using MDConverter.Options;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
var root = Path.GetFullPath("out/review-probe");
var template = Path.Combine(root,"template.docx");
using(var doc=WordprocessingDocument.Create(template,DocumentFormat.OpenXml.WordprocessingDocumentType.Document)) {
var part=doc.AddMainDocumentPart(); part.Document=new Document(new Body(new Paragraph(new Run(new Text("template"))),new SectionProperties(new PageSize {Width=16838,Height=11906},new PageMargin {Left=720,Right=720,Top=720,Bottom=720}))); part.Document.Save(); }
var converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions { Docx=new DocxOptions {UseBuiltinTemplate=false,TemplatePath=template}});
using(var s=new MemoryStream(converter.ConvertText("# Heading\n\n[Go](#heading)",root))) using(var doc=WordprocessingDocument.Open(s,false)) {
Console.WriteLine("Template sections retained: "+doc.MainDocumentPart.Document.Descendants<SectionProperties>().Count());
Console.WriteLine("Internal hyperlinks: "+doc.MainDocumentPart.Document.Descendants<Hyperlink>().Count()); }
var md=Path.Combine(root,"input.md"); var output=Path.Combine(root,"existing.docx"); File.WriteAllText(md,"Hello"); File.WriteAllText(output,"existing content");
try { new MarkdownToDocxConverter(new MarkdownToDocxOptions {Docx=new DocxOptions {UseBuiltinTemplate=false,TemplatePath=Path.Combine(root,"missing.docx")}}).ConvertFile(md,output); } catch(Exception ex) { Console.WriteLine("Failed conversion: "+ex.Message); }
Console.WriteLine("Existing output length after failed conversion: "+new FileInfo(output).Length);
using(var s=new MemoryStream(new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertText("**[Go](https://example.com)**",root))) using(var doc=WordprocessingDocument.Open(s,false)) {
Console.WriteLine("Bold link output text: "+doc.MainDocumentPart.Document.InnerText); Console.WriteLine("Bold link hyperlinks: "+doc.MainDocumentPart.HyperlinkRelationships.Count()); }
