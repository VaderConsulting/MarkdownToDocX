using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// Verified JSON contract tests for §11.11 fixtures.
/// </summary>
public class FlowchartRecognitionFixtureTests
{
    private static readonly string[] FixtureBaseNames =
    {
        "chain-a-b-c",
        "grouped-a-b-to-c",
        "chained-a-b-c-d",
        "edge-identity",
        "metadata-shape-data",
        "node-bracket-shapes",
        "subgraph-membership",
        "config-init",
        "label-types",
        "label-html",
        "class-def-style",
        "interactions",
        "flowchart-accessibility",
        "accDescr-block",
        "unsupported-diagnostics",
        "arrow-variants",
        "invisible-link",
        "subgraph-direction",
        "subgraph-nested",
        "edge-metadata",
        "bidirectional-arrows",
        "edge-label-inline",
        "linkStyle-interpolate",
        "link-minlen",
        "edge-label-pipe",
        "edge-animate",
        "edge-curve",
        "config-front-matter",
        "config-front-matter-spacing",
        "config-front-matter-theme",
        "edge-class-animate",
        "edge-metadata-f87",
    };

    [TestCaseSource(nameof(FixtureBaseNameCases))]
    public void Fixture_Matches_Verified_Json(string FixtureBaseName)
    {
        string JsonPath = GetFixturePath(FixtureBaseName + ".verified.json");
        string MmdPath = GetFixturePath(FixtureBaseName + ".mmd");
        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))
        {
            Assert.Ignore($"Fixture {FixtureBaseName} not generated yet. Run {nameof(FlowchartRecognitionFixtureWriter)}.{nameof(FlowchartRecognitionFixtureWriter.Write_All_Recognition_Fixtures)}.");
        }

        string Source = File.ReadAllText(MmdPath);
        DiagramContract Actual = FlowchartRecognizer.Parse(Source);
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(JsonPath));
        ContractSnapshot ActualSnapshot = ContractSnapshot.FromDiagram(Actual);
        Assert.That(ActualSnapshot.ToJson(), Is.EqualTo(Expected.ToJson()));
    }

    static IEnumerable<string> FixtureBaseNameCases()
    {
        foreach (string Name in FixtureBaseNames)
        {
            yield return Name;
        }
    }

    static string GetFixturePath(string FileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", FileName);
    }
}
