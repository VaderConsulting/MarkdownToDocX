using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.State;

namespace MermaidRecognition.Tests.State;

[TestFixture]
public sealed class StateContractMapperTests
{
    [Test]
    public void Apply_SimpleTransitions_CreatesNodesAndEdgesWithStartNode()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> Still
                Still --> Moving
                Moving --> Still
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(3));
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "start1" && Node.Shape == "start"), Is.True);
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "Still"), Is.True);
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "Moving"), Is.True);
        Assert.That(Contract.Edges, Has.Count.EqualTo(3));
        Assert.That(Contract.Edges[0].SourceId, Is.EqualTo("start1"));
        Assert.That(Contract.Edges[0].TargetId, Is.EqualTo("Still"));
    }

    [Test]
    public void Apply_TransitionLabels_PopulatesEdgeLabelsAndEndNode()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> Active
                Active --> Inactive : timeout
                Inactive --> Active : reset
                Active --> [*] : shutdown
            """);

        Assert.That(Contract.Edges, Has.Count.EqualTo(4));
        Assert.That(Contract.Edges[1].Label, Is.EqualTo("timeout"));
        Assert.That(Contract.Edges[3].TargetId, Is.EqualTo("end2"));
        Assert.That(Contract.Edges[3].Label, Is.EqualTo("shutdown"));
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "end2" && Node.Shape == "end"), Is.True);
    }

    [Test]
    public void Apply_CompositeState_AssignsParentGroupIdAndGroupContract()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> First
                state First {
                    [*] --> Second
                    Second --> [*]
                }
            """);

        Assert.That(Contract.Groups, Has.Count.EqualTo(1));
        GroupContract First = Contract.Groups[0];
        Assert.That(First.Id, Is.EqualTo("First"));
        Assert.That(First.ChildNodeIds, Is.EquivalentTo(new[] { "First_start", "Second", "First_end" }));

        NodeContract Second = Contract.Nodes.Single(Node => Node.Id == "Second");
        Assert.That(Second.ParentGroupId, Is.EqualTo("First"));
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "First_start" && Node.Shape == "start"), Is.True);
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "First_end" && Node.Shape == "end"), Is.True);
    }

    [Test]
    public void Apply_ForkJoinChoice_SetsShapeAndMetadata()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                state fork_state <<fork>>
                state join_state <<join>>
                state choice_state <<choice>>
            """);

        NodeContract Fork = Contract.Nodes.Single(Node => Node.Id == "fork_state");
        NodeContract Join = Contract.Nodes.Single(Node => Node.Id == "join_state");
        NodeContract Choice = Contract.Nodes.Single(Node => Node.Id == "choice_state");
        Assert.That(Fork.Shape, Is.EqualTo("fork"));
        Assert.That(Join.Shape, Is.EqualTo("join"));
        Assert.That(Choice.Shape, Is.EqualTo("choice"));
        Assert.That(Fork.Metadata["stateType"], Is.InstanceOf<RawMetadata>());
    }

    [Test]
    public void Apply_DescriptionAs_SetsLabelFromDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                state "Processing" as P
            """);

        NodeContract State = Contract.Nodes.Single(Node => Node.Id == "P");
        Assert.That(State.Label, Is.EqualTo("Processing"));
    }

    [Test]
    public void Apply_ClassDefAndApply_PopulatesClassDefsAndNodeClasses()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> Active
                classDef highlight fill:#f9f,stroke:#333
                class Active highlight
            """);

        Assert.That(Contract.ClassDefs, Has.Count.EqualTo(1));
        Assert.That(Contract.ClassDefs[0].ClassId, Is.EqualTo("highlight"));
        Assert.That(Contract.ClassDefs[0].Style.Stroke, Is.EqualTo("#333"));

        NodeContract Active = Contract.Nodes.Single(Node => Node.Id == "Active");
        Assert.That(Active.Classes, Contains.Item("highlight"));
        Assert.That(Active.InlineStyle.Stroke, Is.EqualTo("#333"));
    }

    [Test]
    public void Apply_Style_AppliesInlineStyleToState()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> Active
                style Active stroke:#333
            """);

        NodeContract Active = Contract.Nodes.Single(Node => Node.Id == "Active");
        Assert.That(Active.InlineStyle.Stroke, Is.EqualTo("#333"));
    }

    [Test]
    public void Apply_AccessibilityInline_PopulatesTitleAndDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                accTitle: Lifecycle
                accDescr: Core state transitions
                [*] --> Active
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Lifecycle"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Core state transitions"));
    }

    [Test]
    public void Apply_AccessibilityBlock_PopulatesMultilineDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                accTitle: Lifecycle
                accDescr {
                    Describes core
                    state transitions
                }
                [*] --> Active
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Lifecycle"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Describes core\r\nstate transitions"));
    }

    [Test]
    public void Apply_ClickHref_PopulatesInteraction()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> State1
                click State1 href "https://example.com" "Docs"
            """);

        Assert.That(Contract.Interactions, Has.Count.EqualTo(1));
        InteractionDirective Interaction = Contract.Interactions[0];
        Assert.That(Interaction.TargetId, Is.EqualTo("State1"));
        Assert.That(Interaction.Url, Is.EqualTo("https://example.com"));
        Assert.That(Interaction.Tooltip, Is.EqualTo("Docs"));
    }

    [Test]
    public void Apply_NoteRightOf_CreatesNoteNodeWithPlacementMetadata()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                State1 --> State2
                note right of State2 : Important
            """);

        NodeContract NoteNode = Contract.Nodes.Single(Node => Node.Id == "note-0");
        Assert.That(NoteNode.Shape, Is.EqualTo("note"));
        Assert.That(NoteNode.Label, Is.EqualTo("Important"));
        RawMetadata Placement = (RawMetadata)NoteNode.Metadata["stateNotePlacement"];
        Assert.That(Placement.RawValue, Is.EqualTo("right"));
        RawMetadata Anchor = (RawMetadata)NoteNode.Metadata["stateNoteAnchor"];
        Assert.That(Anchor.RawValue, Is.EqualTo("State2"));
    }

    [Test]
    public void Apply_NoteLeftOf_CreatesNoteNodeWithLeftPlacement()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> Error
                Error --> [*]
                note left of Error : Error handling
            """);

        NodeContract NoteNode = Contract.Nodes.Single(Node => Node.Id == "note-0");
        Assert.That(NoteNode.Label, Is.EqualTo("Error handling"));
        RawMetadata Placement = (RawMetadata)NoteNode.Metadata["stateNotePlacement"];
        Assert.That(Placement.RawValue, Is.EqualTo("left"));
        RawMetadata Anchor = (RawMetadata)NoteNode.Metadata["stateNoteAnchor"];
        Assert.That(Anchor.RawValue, Is.EqualTo("Error"));
    }

    [Test]
    public void Apply_StandaloneStructOpen_CreatesCompositeGroup()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> Outer
                state Outer
                {
                    [*] --> Inner
                    Inner --> [*]
                }
            """);

        GroupContract OuterGroup = Contract.Groups.Single(Group => Group.Id == "Outer");
        Assert.That(OuterGroup.ChildNodeIds, Does.Contain("Inner"));
        NodeContract Inner = Contract.Nodes.Single(Node => Node.Id == "Inner");
        Assert.That(Inner.ParentGroupId, Is.EqualTo("Outer"));
    }

    [Test]
    public void Apply_Direction_SetsContractDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                direction LR
                [*] --> Still
                Still --> [*]
            """);

        Assert.That(Contract.Direction, Is.EqualTo("LeftToRight"));
    }

    [Test]
    public void Apply_StateDescriptionAs_SetsLabelFromDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                state "Processing" as P
                [*] --> P
                P --> [*]
            """);

        NodeContract State = Contract.Nodes.Single(Node => Node.Id == "P");
        Assert.That(State.Label, Is.EqualTo("Processing"));
    }

    [Test]
    public void Apply_InlineStyle_AppliesFillAndStrokeToTarget()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> State1
                State1 --> State2
                style State2 fill:#bbf,stroke:#333
            """);

        NodeContract State2 = Contract.Nodes.Single(Node => Node.Id == "State2");
        Assert.That(State2.InlineStyle.Fill, Is.EqualTo("#bbf"));
        Assert.That(State2.InlineStyle.Stroke, Is.EqualTo("#333"));
    }

    [Test]
    public void Apply_LeavesSourceStatementsUnchanged()
    {
        DiagramContract Contract = new DiagramContract
        {
            Kind = "stateDiagram"
        };
        SourceStatementContract Statement = new SourceStatementContract
        {
            LineNumber = 2,
            LineTypeId = "stateDiagram.transition",
            RawLine = "    [*] --> Still"
        };
        Contract.SourceStatementsMutable.Add(Statement);

        int StatementCount = Contract.SourceStatements.Count;
        string LineTypeId = Contract.SourceStatements[0].LineTypeId;
        string RawLine = Contract.SourceStatements[0].RawLine;

        StateContractMapper.Apply(Contract);

        Assert.That(Contract.SourceStatements.Count, Is.EqualTo(StatementCount));
        Assert.That(Contract.SourceStatements[0].LineTypeId, Is.EqualTo(LineTypeId));
        Assert.That(Contract.SourceStatements[0].RawLine, Is.EqualTo(RawLine));
    }
}
