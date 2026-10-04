using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.State;

/// <summary>
/// Confirms state G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class StateL4AxisGateTests
{
    [Test]
    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetStateFixtureDirectory(), "init-leading.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string? Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("base"));
    }

    [Test]
    public void G12_Composite_Fixture_Produces_Composite_Group()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetStateFixtureDirectory(), "composite.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        GroupContract CompositeGroup = Contract.Groups.Single(Group => Group.Id == "First");
        Assert.That(CompositeGroup.ChildNodeIds, Has.Count.GreaterThan(0));
        Assert.That(Contract.Nodes.Any(Node => Node.ParentGroupId == "First"), Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "stateDiagram.state"),
            Is.True);
    }

    [Test]
    public void G12_Choice_Fixture_Produces_Choice_Pseudo_State()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetStateFixtureDirectory(), "choice.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        NodeContract ChoiceNode = Contract.Nodes.Single(Node => Node.Id == "choice_state");
        Assert.That(ChoiceNode.Shape, Is.EqualTo("choice"));
        Assert.That(Contract.Edges.Any(Edge => Edge.Label == "if n > 0"), Is.True);
    }

    [Test]
    public void G12_Note_Fixture_Produces_Note_Node()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetStateFixtureDirectory(), "note.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        NodeContract NoteNode = Contract.Nodes.Single(Node => Node.Shape == "note");
        Assert.That(NoteNode.Label, Is.EqualTo("Important"));
        Assert.That(NoteNode.Metadata.ContainsKey("stateNoteAnchor"), Is.True);
        Assert.That(NoteNode.Metadata.ContainsKey("stateNotePlacement"), Is.True);
        RawMetadata AnchorMetadata = (RawMetadata)NoteNode.Metadata["stateNoteAnchor"];
        RawMetadata PlacementMetadata = (RawMetadata)NoteNode.Metadata["stateNotePlacement"];
        Assert.That(AnchorMetadata.RawValue, Is.EqualTo("State2"));
        Assert.That(PlacementMetadata.RawValue, Is.EqualTo("right"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "stateDiagram.note"),
            Is.True);
    }

    [Test]
    public void G12_ClassDefStyle_Fixture_Applies_ClassDef_To_State()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetStateFixtureDirectory(), "classDef-style.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.ClassDefs.Any(ClassDef => ClassDef.ClassId == "highlight"), Is.True);
        NodeContract ActiveNode = Contract.Nodes.Single(Node => Node.Id == "Active");
        Assert.That(ActiveNode.Classes, Contains.Item("highlight"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "stateDiagram.classDef"),
            Is.True);
    }

    [Test]
    public void G12_TransitionColons_Fixture_Preserves_Colons_In_Label()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetStateFixtureDirectory(), "transition-colons.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        EdgeContract RecoverableEdge = Contract.Edges.Single(Edge => Edge.SourceId == "locked" && Edge.TargetId == "pending");
        Assert.That(RecoverableEdge.Label, Is.EqualTo("recoverable error (ex: timeout)"));

        EdgeContract MultiColonEdge = Contract.Edges.Single(Edge => Edge.SourceId == "A" && Edge.TargetId == "B");
        Assert.That(MultiColonEdge.Label, Is.EqualTo("info: key: value"));
    }

    [Test]
    public void G12_Accessibility_Fixture_Populates_Title_And_Description()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetStateFixtureDirectory(), "accessibility.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Process lifecycle"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Core state transitions"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "stateDiagram.accTitle"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "stateDiagram.accDescr"),
            Is.True);
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
