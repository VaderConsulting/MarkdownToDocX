using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Gantt;

/// <summary>
/// Confirms gantt G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class GanttL4AxisGateTests
{
    [Test]
    public void G12_Section_Task_Fixture_Produces_Group_And_Task_Node()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGanttFixtureDirectory(), "section.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Groups.Count, Is.EqualTo(1));
        Assert.That(Contract.Nodes.Count, Is.EqualTo(1));
        Assert.That(Contract.Nodes[0].ParentGroupId, Is.EqualTo(Contract.Groups[0].Id));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.section"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.task"),
            Is.True);
    }

    [Test]
    public void G12_DateFormat_Fixture_Classifies_DateFormat_And_Configures_Format()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGanttFixtureDirectory(), "dateFormat.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.dateFormat"),
            Is.True);
        Assert.That(
            Contract.Configuration.RawDirectiveValues.TryGetValue("gantt.dateFormat", out string? DateFormat),
            Is.True);
        Assert.That(DateFormat, Is.EqualTo("YYYY-MM-DD"));
    }

    [Test]
    public void G12_Excludes_Fixture_Classifies_Excludes_And_Configures_Rule()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGanttFixtureDirectory(), "excludes.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.excludes"),
            Is.True);
        Assert.That(
            Contract.Configuration.RawDirectiveValues.TryGetValue("gantt.excludes", out string? Excludes),
            Is.True);
        Assert.That(Excludes, Is.EqualTo("weekends"));
    }

    [Test]
    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGanttFixtureDirectory(), "init-leading.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string? Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("forest"));
    }

    [Test]
    public void G12_InclusiveEnd_Fixture_Classifies_InclusiveEndDates_And_Configures_Flag()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetGanttFixtureDirectory(), "inclusive-end.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "gantt.inclusiveEndDates"),
            Is.True);
        Assert.That(
            Contract.Configuration.RawDirectiveValues.TryGetValue("gantt.inclusiveEndDates", out string? Flag),
            Is.True);
        Assert.That(Flag, Is.EqualTo("true"));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "gantt.gate-manifest.json"),
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            "gantt");
    }
}
