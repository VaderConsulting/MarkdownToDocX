using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.GitGraph;

/// <summary>
/// Confirms git graph G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class GitGraphL4AxisGateTests
{
    [Test]
    public void G12_Simple_Fixture_Produces_Commit_Nodes_And_Edges()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGitGraphFixtureDirectory(),
            new[] { "simple" },
            Contract =>
                Contract.Nodes.Count(Node => Node.Shape == "commit") >= 2
                && Contract.Edges.Count > 0);
    }

    [Test]
    public void G12_BranchHistory_Fixture_Classifies_Checkout_And_Branch_Lines()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGitGraphFixtureDirectory(), "branch-history.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gitGraph.branch"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement =>
                Statement.LineTypeId == "gitGraph.branch"
                && Statement.RawLine.Contains("checkout", StringComparison.OrdinalIgnoreCase)),
            Is.True);
        Assert.That(Contract.Nodes.Any(Node => Node.Shape == "branch"), Is.True);
    }

    [Test]
    public void G12_Merge_Fixture_Classifies_Merge_As_Commit_And_Produces_Topology()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGitGraphFixtureDirectory(), "merge.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(
            Contract.SourceStatements.Any(Statement =>
                Statement.LineTypeId == "gitGraph.commit"
                && Statement.RawLine.Contains("merge", StringComparison.OrdinalIgnoreCase)),
            Is.True);
        Assert.That(Contract.Nodes.Any(Node => Node.Id.StartsWith("merge-", StringComparison.Ordinal)), Is.True);
        Assert.That(Contract.Edges.Count, Is.GreaterThan(0));
    }

    [Test]
    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGitGraphFixtureDirectory(), "init-leading.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string? Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("base"));
    }

    [Test]
    public void G12_Accessibility_Fixture_Populates_Title_And_Description()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGitGraphFixtureDirectory(), "accessibility.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Accessibility.Title, Is.EqualTo("This is a title"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("This is a description"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gitGraph.accTitle"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gitGraph.accDescr"),
            Is.True);
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
