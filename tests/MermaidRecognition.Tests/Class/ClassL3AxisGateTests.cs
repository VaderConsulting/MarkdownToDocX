using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Class;

/// <summary>
/// Confirms class G4–G9 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class ClassL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Classes()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            ["simple", "members", "multiple-classes"],
            Contract => Contract.Nodes.Count > 0);
    }

    [Test]
    public void G5_Containment_Fixtures_Produce_Groups()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            ["namespace"],
            Contract => Contract.Groups.Count > 0);
    }

    [Test]
    public void G6_Styles_Fixtures_Produce_ClassDefs_Or_InlineStyles()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            ["classDef-style", "inline-style"],
            Contract =>
                Contract.ClassDefs.Count > 0
                || Contract.Nodes.Any(Node => Node.InlineStyle.Fill is not null || Node.InlineStyle.Stroke is not null));
    }

    [Test]
    public void G7_Metadata_Fixtures_Attach_Stereotype_Declarations()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            ["interface-annotation", "abstract-stereotype"],
            Contract => Contract.Nodes.Any(Node => Node.Declarations.Any(Declaration => Declaration.Kind == "stereotype")));
    }

    [Test]
    public void G8_Interactions_Fixtures_Produce_Interactions()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            ["click-href"],
            Contract => Contract.Interactions.Count > 0);
    }

    [Test]
    public void G9_Accessibility_Fixtures_Produce_Accessibility_Fields()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            ["accessibility-inline", "accessibility"],
            Contract =>
                !string.IsNullOrEmpty(Contract.Accessibility.Title)
                || !string.IsNullOrEmpty(Contract.Accessibility.Description));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "class.gate-manifest.json"),
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            "classDiagram");
    }
}
