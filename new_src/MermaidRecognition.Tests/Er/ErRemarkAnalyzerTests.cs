using MermaidRecognition;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Er;

/// <summary>
/// ER catalogue-remarked blocks (§9.1) — E10 entity alias from <c>entityrelationship.md</c>.
/// </summary>
[TestFixture]
public sealed class ErRemarkAnalyzerTests
{
    [Test]
    public void Parse_CatalogueRemarkedEntityAlias_E10_RecognizesShadowSource()
    {
        string Source = File.ReadAllText(
            Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "catalogue-remarked-entity-alias.mmd"));

        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.RemarkStatus, Is.EqualTo(MermaidDiagramRemarkStatus.CatalogueRemarked));
        Assert.That(Contract.Kind, Is.EqualTo("erDiagram"));
        Assert.That(
            Contract.Diagnostics.Any(Diagnostic => Diagnostic.Code == "recognition.catalogue-remarked"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.entityAlias"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.body"),
            Is.False);
    }

    [Test]
    public void Fixture_CatalogueRemarkedEntityAlias_Matches_Verified_Json()
    {
        string BaseDir = RecognitionFixturePaths.GetErFixtureDirectory();
        string MmdPath = Path.Combine(BaseDir, "catalogue-remarked-entity-alias.mmd");
        string JsonPath = Path.Combine(BaseDir, "catalogue-remarked-entity-alias.verified.json");

        string Source = File.ReadAllText(MmdPath);
        ContractSnapshot Actual = ContractSnapshot.FromDiagram(MermaidRecognizer.Parse(Source));
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(JsonPath));
        Assert.That(Actual.ToJson(), Is.EqualTo(Expected.ToJson()));
    }
}
