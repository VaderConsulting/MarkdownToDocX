using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseStatementBoundaryTests
{
    [Test]
    public void Separators_PreserveQuotedPunctuationAndSourceLines()
    {
        var Result = EnterpriseParser.Parse("enterprise;\napplication-component first \"Text; %% text\"; application-service second;\nfirst -realization-> second; invalid statement here;");
        Assert.That(Result.Document.Elements["first"].Label, Is.EqualTo("Text; %% text"));
        Assert.That(Result.Document.Elements["second"].LineNumber, Is.EqualTo(2));
        Assert.That(Result.Document.Relationships.Single().LineNumber, Is.EqualTo(3));
        Assert.That(Result.Diagnostics.Single().LineNumber, Is.EqualTo(3));
    }

    [Test]
    public void Comment_EndsPhysicalLineAfterSeparatedStatement()
    {
        var Result = EnterpriseParser.Parse("enterprise; application-component first; %% ignored; invalid\napplication-service second");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Elements, Has.Count.EqualTo(2));
    }

    [Test]
    public void RestOfLineDirectives_KeepSemicolons()
    {
        var Result = EnterpriseParser.Parse("enterprise\ntitle A; B\naccTitle: C; D\naccDescr: E; F\napplication-component first");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Title, Is.EqualTo("A; B"));
        Assert.That(Result.Document.AccessibilityTitle, Is.EqualTo("C; D"));
        Assert.That(Result.Document.AccessibilityDescription, Is.EqualTo("E; F"));
    }
}
