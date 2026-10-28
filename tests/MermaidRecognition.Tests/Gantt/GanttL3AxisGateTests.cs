using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Gantt;

/// <summary>
/// Confirms gantt G4 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class GanttL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Tasks_And_Sections()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["simple", "section"],
            Contract => Contract.Nodes.Count > 0);

        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["section"],
            Contract => Contract.Groups.Count > 0);
    }

    [Test]
    public void G7_Metadata_Fixtures_Set_Title_In_Configuration()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["simple", "excludes"],
            Contract => Contract.Configuration.RawDirectiveValues.ContainsKey("gantt.title"));
    }

    [Test]
    public void G9_Accessibility_Fixture_Populates_Title_And_Description()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["accessibility"],
            Contract =>
                !string.IsNullOrEmpty(Contract.Accessibility.Title)
                && !string.IsNullOrEmpty(Contract.Accessibility.Description));
    }

    [Test]
    public void G4_Relative_Task_Fixture_Produces_Four_Task_Nodes()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["relative"],
            Contract => Contract.Nodes.Count == 4);
    }

    [Test]
    public void G4_Milestone_Fixture_Produces_Milestone_Shaped_Node()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetGanttFixtureDirectory(),
            ["milestone"],
            Contract => Contract.Nodes.Any(Node => Node.Shape == "ganttMilestone"));
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
