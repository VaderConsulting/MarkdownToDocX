using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Pie;

/// <summary>
/// Confirms pie G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class PieL4AxisGateTests
{
    [Test]
    public void G12_SimplePie_Fixture_Produces_Slice_Nodes_With_Values()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetPieFixtureDirectory(), "simple.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Nodes.Count, Is.EqualTo(2));
        Assert.That(Contract.Nodes.All(Node => Node.Metadata.ContainsKey("pieValue")), Is.True);
    }

    [Test]
    public void G12_ShowData_Fixture_Sets_PieShowData_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetPieFixtureDirectory(), "title-showData.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("pie.showData", out string ShowData), Is.True);
        Assert.That(ShowData, Is.EqualTo("true"));
    }

    [Test]
    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetPieFixtureDirectory(), "init-leading.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("base"));
    }

    [Test]
    public void G12_Accessibility_Fixture_Populates_Title_And_Description()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetPieFixtureDirectory(), "pie-accessibility.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Accessibility.Title, Is.EqualTo("a neat acc title"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("a neat description"));
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
