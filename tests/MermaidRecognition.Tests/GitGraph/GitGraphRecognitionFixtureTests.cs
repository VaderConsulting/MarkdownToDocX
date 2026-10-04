using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.GitGraph;

[TestFixture]
public sealed class GitGraphRecognitionFixtureTests
{
    private static readonly string[] FixtureNames =
    {
        "simple",
        "init-leading",
        "branch-history",
        "merge",
        "commit-args",
        "accessibility",
    };

    [TestCaseSource(nameof(FixtureNameCases))]
    public void Fixture_HasNoBodyFallback(string FixtureName)
    {
        string Source = File.ReadAllText(GetFixturePath(FixtureName + ".mmd"));
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gitGraph.body"),
            Is.False,
            "Fixture " + FixtureName + " must not use gitGraph.body fallback.");
    }

    [TestCaseSource(nameof(FixtureNameCases))]
    public void Fixture_Matches_Verified_Json(string FixtureName)
    {
        string JsonPath = GetFixturePath(FixtureName + ".verified.json");
        string MmdPath = GetFixturePath(FixtureName + ".mmd");
        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))
        {
            Assert.Ignore("Run BacklogKindsFixtureWriter.Write_All_GitGraph_Fixtures first.");
        }

        string Source = File.ReadAllText(MmdPath);
        ContractSnapshot Actual = ContractSnapshot.FromDiagram(MermaidRecognizer.Parse(Source));
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(JsonPath));
        Assert.That(Actual.ToJson(), Is.EqualTo(Expected.ToJson()));
    }

    static IEnumerable<string> FixtureNameCases()
    {
        foreach (string Name in FixtureNames)
        {
            yield return Name;
        }
    }

    static string GetFixturePath(string FileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "GitGraphFixtures", FileName);
    }
}
