using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseQuotedLabelTests
{
    [TestCase("Before %% after", "Before %% after")]
    [TestCase("Before \\\"quote\\\" %% after", "Before \"quote\" %% after")]
    [TestCase(@"Folder\\", @"Folder\")]
    public void Comments_RespectQuotedLabelsAndEscapesAcrossStatementKinds(string Raw, string Expected)
    {
        string Source = $"enterprise\n group g \"{Raw}\" %% ignored\n application-component a \"{Raw}\" in g %% ignored\n application-component b \"Target\"\n a -flow-> b \"{Raw}\" %% ignored";
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(Source);
        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Diagnostic => Diagnostic.Message)));
        Assert.That(Result.Document.Groups["g"].Label, Is.EqualTo(Expected));
        Assert.That(Result.Document.Elements["a"].Label, Is.EqualTo(Expected));
        Assert.That(Result.Document.Relationships.Single().Label, Is.EqualTo(Expected));
    }

    [Test]
    public void TitleComment_StartsAtPercentPairEvenInsideTitleQuotes()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument("enterprise\n title A \"quoted %% trailing comment\"\n application-component a \"Value %% retained\"");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Title, Is.EqualTo("A \"quoted"));
        Assert.That(Result.Document.Elements["a"].Label, Is.EqualTo("Value %% retained"));
    }

    [TestCase("#quot;text#quot; #amp; #lt;x#gt;", "\"text\" & <x>")]
    [TestCase("#169; #copy; #9829; #128578;", "© © ♥ 🙂")]
    [TestCase("#35;12 #+65;", "#12 A")]
    [TestCase("#notAnEntity; #99999999999999999; #55296; #1114112; #0;", "#notAnEntity; #99999999999999999; #55296; #1114112; #0;")]
    [TestCase("#amp;lt; #lt;br#gt; #92;n", @"&lt; <br> \n")]
    public void EntityCodes_DecodeOnceAndLeaveInvalidValuesVisible(string Raw, string Expected)
    {
        Assert.That(EnterpriseLabelText.DecodeQuoted(Raw), Is.EqualTo(Expected));
    }
}
