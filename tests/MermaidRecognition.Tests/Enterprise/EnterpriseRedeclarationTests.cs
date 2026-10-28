using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseRedeclarationTests
{
    [Test]
    public void SameTypeRedeclaration_MergesLabelAndContainerWithoutDuplicatingNode()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument("enterprise\napplication-component first\ngroup container\napplication-component first 'Intermediate' in container\napplication-component first \"`**Final**`\"\napplication-component first");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Elements, Has.Count.EqualTo(2));
        Assert.That(Result.Document.Elements["first"].Label, Is.EqualTo("**Final**"));
        Assert.That(Result.Document.Elements["first"].IsMarkdownLabel, Is.True);
        Assert.That(Result.Document.Elements["first"].ParentId, Is.EqualTo("container"));
    }

    [TestCase("class")]
    [TestCase("TITLE")]
    [TestCase("direction")]
    [TestCase("accDescr")]
    public void ReservedIdentifier_IsRejectedWithoutLosingOtherDiagnostics(string Reserved)
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument($"enterprise\napplication-component first\napplication-service first\nmissing -serving-> first\nfirst -serving-> absent\ndata-object {Reserved}");
        Assert.That(Result.Diagnostics, Has.Count.EqualTo(4));
        Assert.That(Result.Diagnostics.Select(Item => Item.LineNumber), Is.EquivalentTo([3, 4, 5, 6]));
    }
}
