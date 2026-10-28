using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseDirectionStatementTests
{
    [TestCase("TB", EnterpriseDiagramDirection.Tb)]
    [TestCase("TD", EnterpriseDiagramDirection.Tb)]
    [TestCase("BT", EnterpriseDiagramDirection.Bt)]
    [TestCase("LR", EnterpriseDiagramDirection.Lr)]
    [TestCase("RL", EnterpriseDiagramDirection.Rl)]
    public void LastDirectionStatement_Wins(string Token, EnterpriseDiagramDirection Expected)
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument($"enterprise\ndirection LR\ndirection {Token}\napplication-component first");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Direction, Is.EqualTo(Expected));
    }

    [Test]
    public void GroupDirection_IsScopedAndMembersKeepTheirContainer()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument("enterprise LR\ngroup outer {\ndirection BT\ngroup inner {\ndirection RL\napplication-component child\n}\n}\napplication-component outside");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Direction, Is.EqualTo(EnterpriseDiagramDirection.Lr));
        Assert.That(Result.Document.Groups["outer"].Direction, Is.EqualTo(EnterpriseDiagramDirection.Bt));
        Assert.That(Result.Document.Groups["inner"].Direction, Is.EqualTo(EnterpriseDiagramDirection.Rl));
        Assert.That(Result.Document.Groups["inner"].ParentId, Is.EqualTo("outer"));
        Assert.That(Result.Document.Elements["child"].ParentId, Is.EqualTo("inner"));
        Assert.That(Result.Document.Elements["outside"].ParentId, Is.Null);
    }
}
