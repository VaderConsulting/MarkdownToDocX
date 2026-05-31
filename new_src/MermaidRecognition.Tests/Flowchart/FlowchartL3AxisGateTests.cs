using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// Confirms flowchart G4–G9 axis coverage via §11.11 fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class FlowchartL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Nodes_Or_Label_Variants()
    {
        AssertAxis(
            new[] { "node-bracket-shapes", "label-types", "label-html", "metadata-shape-data" },
            Contract => Contract.Nodes.Count > 0);
    }

    [Test]
    public void G5_Containment_Fixtures_Produce_Groups()
    {
        AssertAxis(
            new[] { "subgraph-membership", "subgraph-nested", "subgraph-direction" },
            Contract => Contract.Groups.Count > 0);
    }

    [Test]
    public void G6_Styles_Fixtures_Produce_ClassDefs_Or_LinkStyles()
    {
        AssertAxis(
            new[] { "class-def-style", "linkStyle-interpolate" },
            Contract => Contract.ClassDefs.Count > 0 || Contract.LinkStyles.Count > 0);
    }

    [Test]
    public void G7_Metadata_Fixtures_Produce_Node_Or_Edge_Metadata()
    {
        AssertAxis(
            new[] { "metadata-shape-data", "edge-metadata" },
            Contract =>
                Contract.Nodes.Any(Node => Node.Metadata.Count > 0)
                || Contract.Edges.Any(Edge => Edge.Metadata.Count > 0));
    }

    [Test]
    public void G8_Interactions_Fixtures_Produce_Interactions()
    {
        AssertAxis(
            new[] { "interactions", "unsupported-diagnostics" },
            Contract => Contract.Interactions.Count > 0);
    }

    [Test]
    public void G9_Accessibility_Fixtures_Produce_Accessibility_Fields()
    {
        AssertAxis(
            new[] { "flowchart-accessibility", "accDescr-block" },
            Contract =>
                !string.IsNullOrEmpty(Contract.Accessibility.Title)
                || !string.IsNullOrEmpty(Contract.Accessibility.Description));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        string ManifestPath = Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "flowchart.gate-manifest.json");
        Assert.That(File.Exists(ManifestPath), Is.True);

        string Json = File.ReadAllText(ManifestPath);
        FlowchartGateManifest? Manifest = System.Text.Json.JsonSerializer.Deserialize<FlowchartGateManifest>(Json);
        Assert.That(Manifest, Is.Not.Null);
        Assert.That(Manifest!.Fixtures, Is.Not.Empty);

        string FixtureRoot = RecognitionFixturePaths.GetFlowchartFixtureDirectory();
        foreach (string FixtureName in Manifest.Fixtures)
        {
            string MmdPath = Path.Combine(FixtureRoot, FixtureName + ".mmd");
            Assert.That(File.Exists(MmdPath), Is.True, "Missing fixture " + FixtureName);
            DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
            Assert.That(Contract.Kind, Is.EqualTo("flowchart"));
            Assert.That(
                Contract.Diagnostics.Any(Diagnostic => Diagnostic.Severity == MermaidDiagnosticSeverity.Error),
                Is.False,
                "Fixture " + FixtureName + " must not emit error diagnostics.");
        }
    }

    static void AssertAxis(string[] FixtureNames, Func<DiagramContract, bool> Predicate)
    {
        string FixtureRoot = RecognitionFixturePaths.GetFlowchartFixtureDirectory();
        bool AnyPassed = false;
        List<string> Failures = new List<string>();

        foreach (string FixtureName in FixtureNames)
        {
            string MmdPath = Path.Combine(FixtureRoot, FixtureName + ".mmd");
            if (!File.Exists(MmdPath))
            {
                Failures.Add(FixtureName + " (missing .mmd)");
                continue;
            }

            DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
            if (Predicate(Contract))
            {
                AnyPassed = true;
            }
            else
            {
                Failures.Add(FixtureName);
            }
        }

        Assert.That(AnyPassed, Is.True, "No fixture satisfied axis gate: " + string.Join(", ", Failures));
    }

    sealed class FlowchartGateManifest
    {
        public List<string> Fixtures { get; set; } = new List<string>();
    }
}
