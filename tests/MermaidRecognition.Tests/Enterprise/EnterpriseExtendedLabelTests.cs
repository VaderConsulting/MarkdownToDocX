using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseExtendedLabelTests
{
    [Test]
    public void SingleQuotedLabels_PreserveCommentsSeparatorsAndEscapedQuotes()
    {
        var Result = EnterpriseParser.Parse("enterprise; application-component first 'It\\'s a; %% label'; application-service second 'Other'; first -realization-> second 'quoted \\\"text\\\"'");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Elements["first"].Label, Is.EqualTo("It's a; %% label"));
        Assert.That(Result.Document.Relationships.Single().Label, Is.EqualTo("quoted \"text\""));
    }

    [Test]
    public void MultilineMarkdownLabel_PreservesBreakAndStatementStartLine()
    {
        var Result = EnterpriseParser.Parse("enterprise\napplication-component first \"`**Bold**\n*Italic*`\"; application-service second");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Elements["first"].Label, Is.EqualTo("**Bold**\n*Italic*"));
        Assert.That(Result.Document.Elements["first"].IsMarkdownLabel, Is.True);
        Assert.That(Result.Document.Elements["first"].LineNumber, Is.EqualTo(2));
        Assert.That(Result.Document.Elements["second"].LineNumber, Is.EqualTo(3));
    }
}
