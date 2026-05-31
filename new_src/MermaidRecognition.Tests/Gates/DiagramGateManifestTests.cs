using System.Text.Json;

namespace MermaidRecognition.Tests.Gates;

/// <summary>
/// Validates §11.1.14.2 gate manifests reference existing fixture files.
/// </summary>
[TestFixture]
public sealed class DiagramGateManifestTests
{
    [TestCase("flowchart.gate-manifest.json")]
    [TestCase("sequence.gate-manifest.json")]
    [TestCase("class.gate-manifest.json")]
    [TestCase("state.gate-manifest.json")]
    [TestCase("er.gate-manifest.json")]
    [TestCase("pie.gate-manifest.json")]
    [TestCase("gantt.gate-manifest.json")]
    [TestCase("mindmap.gate-manifest.json")]
    [TestCase("gitGraph.gate-manifest.json")]
    [TestCase("timeline.gate-manifest.json")]
    [TestCase("journey.gate-manifest.json")]
    [TestCase("quadrantChart.gate-manifest.json")]
    [TestCase("xychart.gate-manifest.json")]
    [TestCase("requirement.gate-manifest.json")]
    [TestCase("architecture.gate-manifest.json")]
    [TestCase("kanban.gate-manifest.json")]
    [TestCase("sankey.gate-manifest.json")]
    [TestCase("block.gate-manifest.json")]
    [TestCase("packet.gate-manifest.json")]
    [TestCase("radar.gate-manifest.json")]
    [TestCase("treemap.gate-manifest.json")]
    [TestCase("c4.gate-manifest.json")]
    [TestCase("mermaid-only.gate-manifest.json")]
    public void Manifest_Fixtures_Exist_On_Disk(string ManifestFileName)
    {
        string ManifestPath = Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), ManifestFileName);
        Assert.That(File.Exists(ManifestPath), Is.True, "Missing manifest: " + ManifestFileName);

        string Json = File.ReadAllText(ManifestPath);
        GateManifestDocument? Document = JsonSerializer.Deserialize<GateManifestDocument>(Json);
        Assert.That(Document, Is.Not.Null);
        Assert.That(Document!.FixtureDirectory, Is.Not.Null.And.Not.Empty);
        Assert.That(Document.Fixtures, Is.Not.Empty);

        string FixtureRoot = Path.Combine(
            RecognitionFixturePaths.GetProjectDirectory(),
            Document.FixtureDirectory);

        foreach (string FixtureName in Document.Fixtures)
        {
            string MmdPath = Path.Combine(FixtureRoot, FixtureName + ".mmd");
            string VerifiedPath = Path.Combine(FixtureRoot, FixtureName + ".verified.json");
            Assert.That(File.Exists(MmdPath), Is.True, "Missing .mmd for " + FixtureName);
            Assert.That(File.Exists(VerifiedPath), Is.True, "Missing .verified.json for " + FixtureName);
        }
    }

    sealed class GateManifestDocument
    {
        public string DiagramKind { get; set; } = string.Empty;

        public string GateLevel { get; set; } = string.Empty;

        public string FixtureDirectory { get; set; } = string.Empty;

        public List<string> Fixtures { get; set; } = new List<string>();
    }
}
