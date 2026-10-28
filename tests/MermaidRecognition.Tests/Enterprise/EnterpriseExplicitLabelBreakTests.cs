using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseExplicitLabelBreakTests
{
    [TestCase("<br>")]
    [TestCase("<br/>")]
    [TestCase("<BR />")]
    [TestCase("\\n")]
    public void QuotedLabels_DecodeExplicitBreaksForEveryStatementKind(string Separator)
    {
        string Label = "Alpha" + Separator + "Beta";
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument($"enterprise\n group g \"{Label}\"\n application-component a \"{Label}\" in g\n application-component b \"Target\"\n a -flow-> b \"{Label}\"");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Groups["g"].Label, Is.EqualTo("Alpha\nBeta"));
        Assert.That(Result.Document.Elements["a"].Label, Is.EqualTo("Alpha\nBeta"));
        Assert.That(Result.Document.Relationships.Single().Label, Is.EqualTo("Alpha\nBeta"));
    }

    [TestCase(@"Alpha\\nBeta", @"Alpha\nBeta")]
    [TestCase(@"C:\temp\folder", @"C:\temp\folder")]
    [TestCase("Alpha\\\"Beta", "Alpha\"Beta")]
    public void Escapes_AreDecodedOnceAndUnknownBackslashesSurvive(string Input, string Expected)
    {
        Assert.That(EnterpriseLabelText.DecodeQuoted(Input), Is.EqualTo(Expected));
        Assert.That(EnterpriseLabelText.Wrap(EnterpriseLabelText.DecodeQuoted(Input), 100), Is.EqualTo([Expected]));
    }

    [Test]
    public void ExplicitLines_WrapIndependentlyAndRetainBlankLines()
    {
        Assert.That(EnterpriseLabelText.Wrap("one two three\n\nfour five six", 8),
            Is.EqualTo(["one two", "three", "", "four", "five six"]));
    }
}
