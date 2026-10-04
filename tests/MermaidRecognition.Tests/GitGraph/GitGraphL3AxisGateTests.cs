using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.GitGraph;

/// <summary>
/// Confirms git graph G4 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class GitGraphL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Commits_And_Branches()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            new[] { "simple", "branch-history", "merge" },
            Contract =>
                Contract.Nodes.Count > 0
                && Contract.Nodes.Any(Node => Node.Shape == "commit")
                && Contract.Nodes.Any(Node => Node.Shape == "branch"));
    }

    [Test]
    public void G4_LinePatterns_Fixtures_Avoid_Body_Fallback()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            new[] { "branch-history", "merge", "commit-args", "accessibility" },
            Contract => !Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gitGraph.body"));
    }

    [Test]
    public void G4_Topology_Fixtures_Produce_Sequential_Edges()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            new[] { "simple", "branch-history", "merge" },
            Contract => Contract.Edges.Count > 0);
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "gitGraph.gate-manifest.json"),
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            "gitGraph");
    }
}
