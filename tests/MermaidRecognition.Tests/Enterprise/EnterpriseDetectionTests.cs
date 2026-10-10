using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseDetectionTests
{
    [Test]
    public void Detect_EnterpriseHeader_UsesEnterpriseKind()
    {
        string Source = "enterprise\n  application-component webShop \"Web Shop\"";
        string Preprocessed = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(Preprocessed, null);
        Assert.That(Detection, Is.Not.Null);
        Assert.That(Detection!.MermaidDetectorId, Is.EqualTo("enterprise"));
        Assert.That(Detection.ProgrammeKindId, Is.EqualTo("enterprise"));
    }

    [Test]
    public void Parse_EnterpriseSource_ProducesEnterpriseContract()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            enterprise
              title Shop
              application-component webShop "Web Shop"
              data-object order "Order"
              webShop -access(write)-> order
            """);

        Assert.That(Contract.Kind, Is.EqualTo("enterprise"));
        Assert.That(Contract.Nodes, Has.Count.EqualTo(2));
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
        Assert.That(Contract.Diagnostics.Any(Item => Item.Severity == MermaidDiagnosticSeverity.Error), Is.False);
    }

    [Test]
    public void IsEnterpriseSource_MatchesDetector()
    {
        Assert.That(EnterpriseParser.IsEnterpriseSource("%% note\nenterprise\n  node n"), Is.True);
        Assert.That(EnterpriseParser.IsEnterpriseSource("architecture-beta\n  group api"), Is.False);
    }
}
