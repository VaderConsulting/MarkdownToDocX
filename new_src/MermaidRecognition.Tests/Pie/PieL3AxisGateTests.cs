using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Pie;

/// <summary>
/// Confirms pie G4 and G11 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class PieL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Slice_Nodes()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetPieFixtureDirectory(),
            new[] { "simple" },
            Contract => Contract.Nodes.Count > 0);
    }

    [Test]
    public void G7_Metadata_Fixtures_Set_ShowData_In_RawDirectiveValues()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetPieFixtureDirectory(),
            new[] { "title-showData" },
            Contract =>
                Contract.Configuration.RawDirectiveValues.TryGetValue("pie.showData", out string ShowData)
                && ShowData == "true");
    }

    [Test]
    public void G9_Accessibility_Fixtures_Produce_Accessibility_Fields()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetPieFixtureDirectory(),
            new[] { "pie-accessibility" },
            Contract =>
                !string.IsNullOrEmpty(Contract.Accessibility.Title)
                || !string.IsNullOrEmpty(Contract.Accessibility.Description));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "pie.gate-manifest.json"),
            RecognitionFixturePaths.GetPieFixtureDirectory(),
            "pie");
    }
}
