using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Sequence;

/// <summary>
/// Confirms sequence G4–G9 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class SequenceL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Participants_And_Messages()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetSequenceFixtureDirectory(),
            new[] { "simple", "participants", "self-message" },
            Contract => Contract.Nodes.Count > 0 && Contract.Edges.Count > 0);
    }

    [Test]
    public void G5_Containment_Fixtures_Produce_Groups()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetSequenceFixtureDirectory(),
            new[] { "alt-else-blocks", "nested-blocks", "box-rgb" },
            Contract => Contract.Groups.Count > 0);
    }

    [Test]
    public void G6_Styles_Fixtures_Produce_Box_Or_Rect_Groups()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetSequenceFixtureDirectory(),
            new[] { "box-rgb", "rect-block" },
            Contract => Contract.Groups.Any(Group => Group.Id.StartsWith("box-", StringComparison.Ordinal)
                || Group.Id.StartsWith("rect-", StringComparison.Ordinal)));
    }

    [Test]
    public void G7_Metadata_Fixtures_Populate_Configuration_Or_Lifecycle()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetSequenceFixtureDirectory(),
            new[] { "title", "autonumber", "lifecycle-create" },
            Contract =>
                Contract.Configuration.RawDirectiveValues.Count > 0
                || Contract.Nodes.Any(Node => Node.Metadata.ContainsKey("sequenceActive")));
    }

    [Test]
    public void G9_Accessibility_Fixtures_Produce_Accessibility_Fields()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetSequenceFixtureDirectory(),
            new[] { "sequence-accessibility", "accDescr-block" },
            Contract =>
                !string.IsNullOrEmpty(Contract.Accessibility.Title)
                || !string.IsNullOrEmpty(Contract.Accessibility.Description));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "sequence.gate-manifest.json"),
            RecognitionFixturePaths.GetSequenceFixtureDirectory(),
            "sequence");
    }
}
