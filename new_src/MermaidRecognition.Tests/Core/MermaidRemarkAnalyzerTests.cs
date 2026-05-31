namespace MermaidRecognition.Tests.Core;

using MermaidRecognition;
using MermaidRecognition.Contract;

[TestFixture]
public sealed class MermaidRemarkAnalyzerTests
{
    [Test]
    public void IsInitDirectiveLine_DistinguishesFromComment()
    {
        Assert.That(
            MermaidRemarkAnalyzer.IsInitDirectiveLine("%%{init: {'theme': 'base'}}%%"),
            Is.True);
        Assert.That(
            MermaidRemarkAnalyzer.IsFullLineRemark("%%{init: {'theme': 'base'}}%%"),
            Is.False);
    }

    [Test]
    public void IsFullLineRemark_DetectsCatalogueLines()
    {
        Assert.That(
            MermaidRemarkAnalyzer.IsFullLineRemark("%%     classDiagram"),
            Is.True);
        Assert.That(
            MermaidRemarkAnalyzer.IsFullLineRemark("    %% Domain: sales"),
            Is.True);
    }

    [Test]
    public void StripTrailingRemark_PreservesActiveCode()
    {
        string Active = MermaidRemarkAnalyzer.StripTrailingRemark("A --> B %% hand-off");
        Assert.That(Active, Is.EqualTo("A --> B"));
    }

    [Test]
    public void Analyze_CatalogueRemarkedBlock()
    {
        string Source =
            """
            %%     classDiagram
            %%     class Animal
            %%     classDef highlight fill:#f9f
            %%     cssClass Animal highlight
            """;

        MermaidRemarkAnalyzer.MermaidRemarkAnalysis Analysis = MermaidRemarkAnalyzer.Analyze(Source);
        Assert.That(Analysis.Status, Is.EqualTo(MermaidDiagramRemarkStatus.CatalogueRemarked));
        Assert.That(Analysis.FullLineRemarkCount, Is.EqualTo(4));
        Assert.That(Analysis.CatalogueUnremarkedSource, Does.Contain("classDiagram"));
        Assert.That(Analysis.CatalogueUnremarkedSource, Does.Contain("cssClass Animal highlight"));
    }

    [Test]
    public void Analyze_ActiveDiagramWithCommentLine()
    {
        string Source =
            """
            erDiagram
                %% Domain: sales
                CUSTOMER ||--o{ ORDER : places
            """;

        MermaidRemarkAnalyzer.MermaidRemarkAnalysis Analysis = MermaidRemarkAnalyzer.Analyze(Source);
        Assert.That(Analysis.Status, Is.EqualTo(MermaidDiagramRemarkStatus.Active));
        Assert.That(Analysis.FullLineRemarkCount, Is.EqualTo(1));
    }

    [Test]
    public void Parse_CatalogueRemarkedClassDiagram_RecognizesShadowSource()
    {
        string Source =
            """
            %%     classDiagram
            %%     class Animal
            %%     classDef highlight fill:#f9f
            %%     cssClass Animal highlight
            """;

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Assert.That(Contract.RemarkStatus, Is.EqualTo(MermaidDiagramRemarkStatus.CatalogueRemarked));
        Assert.That(Contract.Kind, Is.EqualTo("classDiagram"));
        Assert.That(
            Contract.Diagnostics.Any(Diagnostic => Diagnostic.Code == "recognition.catalogue-remarked"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "classDiagram.cssClass"),
            Is.True);
    }

    [Test]
    public void Parse_ActiveDiagramWithInlineComment_ClassifiesCommentLine()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                %% Domain: sales
                CUSTOMER ||--o{ ORDER : places
            """);

        Assert.That(Contract.RemarkStatus, Is.EqualTo(MermaidDiagramRemarkStatus.Active));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.comment"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.relationship"),
            Is.True);
    }
}
