using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Gantt;

[TestFixture]
public sealed class GanttRecognitionFixtureTests
{
    private static readonly string[] FixtureNames =
    {
        "simple",
        "section",
        "dateFormat",
        "excludes",
        "init-leading",
        "relative",
        "milestone",
        "accessibility",
        "inclusive-end",
    };

    [TestCaseSource(nameof(FixtureNameCases))]
    public void Fixture_HasNoBodyFallback(string FixtureName)
    {
        string Source = File.ReadAllText(GetFixturePath(FixtureName + ".mmd"));
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.body"),
            Is.False,
            "Fixture " + FixtureName + " must not use gantt.body fallback.");
    }

    [TestCaseSource(nameof(FixtureNameCases))]
    public void Fixture_Matches_Verified_Json(string FixtureName)
    {
        string JsonPath = GetFixturePath(FixtureName + ".verified.json");
        string MmdPath = GetFixturePath(FixtureName + ".mmd");
        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))
        {
            Assert.Ignore("Run BacklogKindsFixtureWriter.Write_All_Gantt_Fixtures first.");
        }

        string Source = File.ReadAllText(MmdPath);
        ContractSnapshot Actual = ContractSnapshot.FromDiagram(MermaidRecognizer.Parse(Source));
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(JsonPath));
        Assert.That(Actual.ToJson(), Is.EqualTo(Expected.ToJson()));
    }

    [Test]
    public void Parse_Gantt_DateFormat_Line_Classified()
    {
        string Source = """
            gantt
                dateFormat YYYY-MM-DD
                title Chart
                Task A :a1, 2024-01-01, 1d
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.dateFormat"),
            Is.True);
    }

    [Test]
    public void Parse_Gantt_Excludes_Line_Classified()
    {
        string Source = """
            gantt
                title Excludes
                excludes weekends
                Task A :a1, 2024-01-01, 1d
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.excludes"),
            Is.True);
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
        return Path.Combine(RecognitionFixturePaths.GetGanttFixtureDirectory(), FileName);
    }
}
