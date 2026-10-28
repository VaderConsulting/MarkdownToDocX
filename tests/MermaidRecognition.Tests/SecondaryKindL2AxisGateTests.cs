using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests;

/// <summary>
/// L2 axis gates for secondary diagram kinds (pie, gantt, mindmap, gitGraph).
/// </summary>
[TestFixture]
public sealed class SecondaryKindL2AxisGateTests
{
    [Test]
    public void Pie_Manifest_Fixtures_Parse_Without_Body_Fallback()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "pie.gate-manifest.json"),
            RecognitionFixturePaths.GetPieFixtureDirectory(),
            "pie");

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetPieFixtureDirectory(),
            ["init-leading"],
            Contract => Contract.Configuration.RawDirectiveValues.ContainsKey("theme"));

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetPieFixtureDirectory(),
            ["comment", "simple", "title-showData", "init-leading"],
            Contract => !Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "pie.body"));
    }

    [Test]
    public void Gantt_Manifest_Fixtures_Parse_Without_Body_Fallback()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "gantt.gate-manifest.json"),
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            "gantt");

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["excludes"],
            Contract => Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.excludes"));

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["init-leading"],
            Contract => Contract.Configuration.RawDirectiveValues.ContainsKey("theme"));
    }

    [Test]
    public void Mindmap_Manifest_Fixtures_Parse_Without_Body_Fallback()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "mindmap.gate-manifest.json"),
            RecognitionFixturePaths.GetMindmapFixtureDirectory(),
            "mindmap");

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetMindmapFixtureDirectory(),
            ["simple", "comment"],
            Contract => Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "mindmap.node"));

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetMindmapFixtureDirectory(),
            ["init-leading"],
            Contract => Contract.Configuration.RawDirectiveValues.ContainsKey("theme"));
    }

    [Test]
    public void GitGraph_Manifest_Fixtures_Parse_Without_Body_Fallback()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "gitGraph.gate-manifest.json"),
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            "gitGraph");

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            ["branch-history", "simple"],
            Contract =>
                Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gitGraph.branch")
                && Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gitGraph.commit"));

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            ["init-leading"],
            Contract => Contract.Configuration.RawDirectiveValues.ContainsKey("theme"));
    }
}
