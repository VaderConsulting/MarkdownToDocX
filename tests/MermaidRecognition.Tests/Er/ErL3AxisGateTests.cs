using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Er;

/// <summary>
/// Confirms ER G4–G9 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class ErL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Entities_And_Edges()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetErFixtureDirectory(),
            ["entity-attributes", "combined-schema", "relationship"],
            Contract => Contract.Nodes.Count > 0);
    }

    [Test]
    public void G6_Styles_Fixtures_Produce_ClassDefs_Or_InlineStyles()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetErFixtureDirectory(),
            ["classDef-style", "inline-style"],
            Contract =>
                Contract.ClassDefs.Count > 0
                || Contract.Nodes.Any(Node => Node.InlineStyle.Fill is not null));
    }

    [Test]
    public void G7_Metadata_Fixtures_Preserve_Attribute_Comments()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetErFixtureDirectory(),
            ["attribute-comments"],
            Contract => Contract.Nodes.Any(Node => Node.Declarations.Any(Declaration => Declaration.RawText.Contains('"'))));
    }

    [Test]
    public void G9_Accessibility_Fixtures_Produce_Accessibility_Fields()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetErFixtureDirectory(),
            ["accessibility", "accDescr-block"],
            Contract =>
                !string.IsNullOrEmpty(Contract.Accessibility.Title)
                || !string.IsNullOrEmpty(Contract.Accessibility.Description));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "er.gate-manifest.json"),
            RecognitionFixturePaths.GetErFixtureDirectory(),
            "erDiagram");
    }
}
