using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.AllKinds;

/// <summary>
/// Verified JSON tests for all line-pattern programme kinds.
/// </summary>
[TestFixture]
public sealed class AllKindsFixtureTests
{
    private static readonly KindFixtureCase[] Cases =
    {
        new KindFixtureCase("TimelineRecognition", "timeline", "simple"),
        new KindFixtureCase("JourneyRecognition", "journey", "simple"),
        new KindFixtureCase("QuadrantRecognition", "quadrantChart", "simple"),
        new KindFixtureCase("XychartRecognition", "xychart", "simple"),
        new KindFixtureCase("RequirementRecognition", "requirement", "simple"),
        new KindFixtureCase("ArchitectureRecognition", "architecture", "simple"),
        new KindFixtureCase("KanbanRecognition", "kanban", "simple"),
        new KindFixtureCase("SankeyRecognition", "sankey", "simple"),
        new KindFixtureCase("BlockRecognition", "block", "simple"),
        new KindFixtureCase("PacketRecognition", "packet", "simple"),
        new KindFixtureCase("RadarRecognition", "radar", "simple"),
        new KindFixtureCase("TreemapRecognition", "treemap", "simple"),
        new KindFixtureCase("C4Recognition", "c4", "simple"),
        new KindFixtureCase("GanttRecognition", "gantt", "simple"),
        new KindFixtureCase("GanttRecognition", "gantt", "section"),
        new KindFixtureCase("MindmapRecognition", "mindmap", "simple"),
        new KindFixtureCase("GitGraphRecognition", "gitGraph", "simple"),
    };

    [TestCaseSource(nameof(FixtureCases))]
    public void Fixture_HasNoBodyFallback(KindFixtureCase Case)
    {
        string Source = File.ReadAllText(GetFixturePath(Case.RecognitionFolder, Case.FixtureName + ".mmd"));
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        string BodyLineTypeId = Case.ProgrammeKindId + ".body";
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == BodyLineTypeId),
            Is.False,
            "Fixture " + Case.FixtureName + " must not use " + BodyLineTypeId + " fallback.");
    }

    [TestCaseSource(nameof(FixtureCases))]
    public void Fixture_Matches_Verified_Json(KindFixtureCase Case)
    {
        string JsonPath = GetFixturePath(Case.RecognitionFolder, Case.FixtureName + ".verified.json");
        string MmdPath = GetFixturePath(Case.RecognitionFolder, Case.FixtureName + ".mmd");
        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))
        {
            Assert.Ignore("Run AllKindsFixtureWriter.Write_All_Remaining_Kind_Fixtures first.");
        }

        string Source = File.ReadAllText(MmdPath);
        ContractSnapshot Actual = ContractSnapshot.FromDiagram(MermaidRecognizer.Parse(Source));
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(JsonPath));
        Assert.That(Actual.ToJson(), Is.EqualTo(Expected.ToJson()));
    }

    static IEnumerable<KindFixtureCase> FixtureCases()
    {
        foreach (KindFixtureCase Case in Cases)
        {
            yield return Case;
        }
    }

    static string GetFixturePath(string RecognitionFolder, string FileName)
    {
        return Path.Combine(RecognitionFixturePaths.GetKindFixtureDirectory(RecognitionFolder), FileName);
    }

    public readonly struct KindFixtureCase
    {
        public KindFixtureCase(string RecognitionFolderValue, string ProgrammeKindIdValue, string FixtureNameValue)
        {
            RecognitionFolder = RecognitionFolderValue;
            ProgrammeKindId = ProgrammeKindIdValue;
            FixtureName = FixtureNameValue;
        }

        public string RecognitionFolder { get; }

        public string ProgrammeKindId { get; }

        public string FixtureName { get; }
    }
}
