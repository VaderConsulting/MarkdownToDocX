using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Sequence;

/// <summary>
/// Confirms sequence G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class SequenceL4AxisGateTests
{
    [Test]
    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "init-leading.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("base"));
    }

    [Test]
    public void G12_SequenceAccessibility_Fixture_Populates_Title_And_Description()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "sequence-accessibility.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Auth"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Login flow"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.accTitle"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.accDescr"),
            Is.True);
    }

    [Test]
    public void G12_Autonumber_Fixture_Sets_SequenceAutonumber_In_Config()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "autonumber.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(
            Contract.Configuration.RawDirectiveValues.TryGetValue("sequence.autonumber", out string AutonumberFlag),
            Is.True);
        Assert.That(AutonumberFlag, Is.EqualTo("true"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.autonumber"),
            Is.True);
    }

    [Test]
    public void G12_ParOver_Fixture_Produces_ParOver_Block_Group()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "par-over.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(
            Contract.SourceStatements.Any(Statement =>
                Statement.LineTypeId == "sequence.block"
                && Statement.RawLine.Contains("par_over", StringComparison.OrdinalIgnoreCase)),
            Is.True);
        GroupContract ParOverGroup = Contract.Groups.Single(Group => Group.Id.StartsWith("par_over-", StringComparison.Ordinal));
        Assert.That(ParOverGroup.Title, Is.EqualTo("Parallel overlap"));
    }

    [Test]
    public void G12_AltElseBlocks_Fixture_Produces_Alt_And_Else_Groups()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "alt-else-blocks.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Groups.Any(Group => Group.Id.StartsWith("alt-", StringComparison.Ordinal)), Is.True);
        Assert.That(Contract.Groups.Any(Group => Group.Id.StartsWith("else-", StringComparison.Ordinal)), Is.True);
        Assert.That(
            Contract.SourceStatements.Count(Statement => Statement.LineTypeId == "sequence.block"),
            Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void G12_BoxRgb_Fixture_Produces_Box_Group_With_Participants()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "box-rgb.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        GroupContract BoxGroup = Contract.Groups.Single(Group => Group.Id.StartsWith("box-", StringComparison.Ordinal));
        Assert.That(BoxGroup.Title, Is.EqualTo("Internal"));
        Assert.That(Contract.Nodes.Count(Node => Node.ParentGroupId == BoxGroup.Id), Is.EqualTo(2));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.box"),
            Is.True);
    }

    [Test]
    public void G12_LifecycleCreate_Fixture_Classifies_Create_And_Destroy()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "lifecycle-create.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        List<SourceStatementContract> LifecycleStatements = Contract.SourceStatements
            .Where(Statement => Statement.LineTypeId == "sequence.lifecycle")
            .ToList();
        Assert.That(LifecycleStatements.Count, Is.EqualTo(2));
        Assert.That(
            LifecycleStatements.Any(Statement => Statement.RawLine.Contains("create", StringComparison.OrdinalIgnoreCase)),
            Is.True);
        Assert.That(
            LifecycleStatements.Any(Statement => Statement.RawLine.Contains("destroy", StringComparison.OrdinalIgnoreCase)),
            Is.True);
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "B"), Is.True);
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
