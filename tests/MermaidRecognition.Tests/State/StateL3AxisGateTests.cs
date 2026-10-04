using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.State;

/// <summary>
/// Confirms state G4–G9 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class StateL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_States_And_Transitions()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetStateFixtureDirectory(),
            new[] { "simple", "fork-join", "choice" },
            Contract => Contract.Nodes.Count > 0 && Contract.Edges.Count > 0);
    }

    [Test]
    public void G5_Containment_Fixtures_Produce_Composite_Groups()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetStateFixtureDirectory(),
            new[] { "composite", "struct-open" },
            Contract => Contract.Groups.Count > 0);
    }

    [Test]
    public void G6_Styles_Fixtures_Produce_ClassDefs_Or_InlineStyles()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetStateFixtureDirectory(),
            new[] { "classDef-style", "inline-style" },
            Contract =>
                Contract.ClassDefs.Count > 0
                || Contract.Nodes.Any(Node => Node.InlineStyle.Fill is not null || Node.InlineStyle.Stroke is not null));
    }

    [Test]
    public void G7_Metadata_Fixtures_Set_State_Labels()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetStateFixtureDirectory(),
            new[] { "state-description" },
            Contract => Contract.Nodes.Any(Node => Node.Label != Node.Id));
    }

    [Test]
    public void G8_Interactions_Fixtures_Produce_Interactions()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetStateFixtureDirectory(),
            new[] { "click-href" },
            Contract => Contract.Interactions.Count > 0);
    }

    [Test]
    public void G9_Accessibility_Fixtures_Produce_Accessibility_Fields()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetStateFixtureDirectory(),
            new[] { "accessibility", "accDescr-block" },
            Contract =>
                !string.IsNullOrEmpty(Contract.Accessibility.Title)
                || !string.IsNullOrEmpty(Contract.Accessibility.Description));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "state.gate-manifest.json"),
            RecognitionFixturePaths.GetStateFixtureDirectory(),
            "stateDiagram");
    }
}
