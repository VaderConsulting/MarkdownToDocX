using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseHeaderDirectionTests
{
    [TestCase("TB", EnterpriseDiagramDirection.Tb)]
    [TestCase("TD", EnterpriseDiagramDirection.Tb)]
    [TestCase("LR", EnterpriseDiagramDirection.Lr)]
    [TestCase("BT", EnterpriseDiagramDirection.Bt)]
    [TestCase("RL", EnterpriseDiagramDirection.Rl)]
    public void HeaderDirection_IsDetectedAndParsedOnSingleLine(string Direction, EnterpriseDiagramDirection Expected)
    {
        string Source = $"enterprise {Direction}; application-component first; application-service second; first -realization-> second;";
        Assert.That(EnterpriseParser.IsEnterpriseSource(Source), Is.True);
        var Result = EnterpriseParser.Parse(Source);
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Direction, Is.EqualTo(Expected));
        Assert.That(Result.Document.Elements.Values.All(Item => Item.LineNumber == 1), Is.True);
    }

    [TestCase("enterprise sideways")]
    [TestCase("enterprise LR garbage")]
    public void UnsupportedHeader_IsRejected(string Source)
    {
        Assert.That(EnterpriseParser.IsEnterpriseSource(Source), Is.False);
        Assert.That(EnterpriseParser.Parse(Source).HasErrors, Is.True);
    }
}
