namespace MermaidRecognition.Tests.Sequence;

[TestFixture]
public sealed class SequenceContractMapperTests
{
    [Test]
    public void Apply_SimpleMessages_CreatesImplicitParticipantsAndEdges()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                Alice->>Bob: Hello Bob
                Bob-->>Alice: Hi Alice
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(2));
        Assert.That(Contract.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "Alice", "Bob" }));
        Assert.That(Contract.Edges, Has.Count.EqualTo(2));

        EdgeContract FirstEdge = Contract.Edges[0];
        Assert.That(FirstEdge.SourceId, Is.EqualTo("Alice"));
        Assert.That(FirstEdge.TargetId, Is.EqualTo("Bob"));
        Assert.That(FirstEdge.Label, Is.EqualTo("Hello Bob"));
        Assert.That(FirstEdge.CreationIndex, Is.EqualTo(0));
        Assert.That(FirstEdge.Metadata["sequenceArrow"], Is.InstanceOf<RawMetadata>());
        RawMetadata FirstArrow = (RawMetadata)FirstEdge.Metadata["sequenceArrow"];
        Assert.That(FirstArrow.RawValue, Is.EqualTo("->>"));

        EdgeContract SecondEdge = Contract.Edges[1];
        Assert.That(SecondEdge.SourceId, Is.EqualTo("Bob"));
        Assert.That(SecondEdge.TargetId, Is.EqualTo("Alice"));
        Assert.That(SecondEdge.Label, Is.EqualTo("Hi Alice"));
        RawMetadata SecondArrow = (RawMetadata)SecondEdge.Metadata["sequenceArrow"];
        Assert.That(SecondArrow.RawValue, Is.EqualTo("-->>"));
    }

    [Test]
    public void Apply_ParticipantsWithAliases_UsesAliasLabels()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                participant A as Alice
                participant B as Bob
                A->>B: Hello
            """);

        NodeContract Alice = Contract.Nodes.Single(Node => Node.Id == "A");
        NodeContract Bob = Contract.Nodes.Single(Node => Node.Id == "B");
        Assert.That(Alice.Label, Is.EqualTo("Alice"));
        Assert.That(Bob.Label, Is.EqualTo("Bob"));
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
        Assert.That(Contract.Edges[0].SourceId, Is.EqualTo("A"));
        Assert.That(Contract.Edges[0].TargetId, Is.EqualTo("B"));
    }

    [Test]
    public void Apply_ParticipantBeforeActor_PreservesDeclarationOrder()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                participant API
                actor User
                User->>API: Call
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(2));
        Assert.That(Contract.Nodes[0].Id, Is.EqualTo("API"));
        Assert.That(Contract.Nodes[0].Shape, Is.EqualTo("participant"));
        Assert.That(Contract.Nodes[1].Id, Is.EqualTo("User"));
        Assert.That(Contract.Nodes[1].Shape, Is.EqualTo("actor"));
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
        Assert.That(Contract.Edges[0].SourceId, Is.EqualTo("User"));
        Assert.That(Contract.Edges[0].TargetId, Is.EqualTo("API"));
    }

    [Test]
    public void Apply_MultiHopMessages_CreatesThreePartyChain()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                A->>B: Forward
                B->>C: Relay
                C-->>A: Reply
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(3));
        Assert.That(Contract.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "A", "B", "C" }));
        Assert.That(Contract.Edges, Has.Count.EqualTo(3));
        Assert.That(Contract.Edges[0].Label, Is.EqualTo("Forward"));
        Assert.That(Contract.Edges[1].Label, Is.EqualTo("Relay"));
        Assert.That(Contract.Edges[2].Label, Is.EqualTo("Reply"));
        RawMetadata ReplyArrow = (RawMetadata)Contract.Edges[2].Metadata["sequenceArrow"];
        Assert.That(ReplyArrow.RawValue, Is.EqualTo("-->>"));
    }

    [Test]
    public void Apply_InlineActivation_MapsActivationMetadataOnEdges()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                Alice->>+Bob: Hello
                Bob-->>-Alice: Hi
            """);

        EdgeContract ActivateEdge = Contract.Edges[0];
        Assert.That(ActivateEdge.Metadata["sequenceActivateTarget"], Is.InstanceOf<RawMetadata>());
        RawMetadata ActivateTarget = (RawMetadata)ActivateEdge.Metadata["sequenceActivateTarget"];
        Assert.That(ActivateTarget.RawValue, Is.EqualTo("Bob"));

        EdgeContract DeactivateEdge = Contract.Edges[1];
        Assert.That(DeactivateEdge.Metadata["sequenceDeactivateSource"], Is.InstanceOf<RawMetadata>());
        RawMetadata DeactivateSource = (RawMetadata)DeactivateEdge.Metadata["sequenceDeactivateSource"];
        Assert.That(DeactivateSource.RawValue, Is.EqualTo("Bob"));

        NodeContract Bob = Contract.Nodes.Single(Node => Node.Id == "Bob");
        Assert.That(Bob.Metadata["sequenceActive"], Is.InstanceOf<RawMetadata>());
        Assert.That(((RawMetadata)Bob.Metadata["sequenceActive"]).RawValue, Is.EqualTo("false"));
    }

    [Test]
    public void Apply_ExplicitActivateDeactivate_UpdatesParticipantActiveState()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                activate A
                deactivate A
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(1));
        NodeContract Participant = Contract.Nodes[0];
        Assert.That(Participant.Id, Is.EqualTo("A"));
        Assert.That(Participant.Metadata["sequenceActive"], Is.InstanceOf<RawMetadata>());
        Assert.That(((RawMetadata)Participant.Metadata["sequenceActive"]).RawValue, Is.EqualTo("false"));
        Assert.That(Contract.Edges, Is.Empty);
    }

    [Test]
    public void Apply_ActorParticipant_SetsShapeToActor()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                actor User
                User->>API: Request
            """);

        NodeContract User = Contract.Nodes.Single(Node => Node.Id == "User");
        Assert.That(User.Shape, Is.EqualTo("actor"));
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "API"), Is.True);
    }

    [Test]
    public void Apply_AltElseBlocks_CreatesGroupsAndTagsMessagesWithParentGroup()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                actor User
                participant API
                User->>API: Request
                alt Data found
                    API-->>User: OK
                else Not found
                    API-->>User: 404
                end
            """);

        Assert.That(Contract.Groups, Has.Count.EqualTo(2));

        GroupContract AltGroup = Contract.Groups.Single(Group => Group.Id == "alt-0");
        Assert.That(AltGroup.Title, Is.EqualTo("Data found"));
        Assert.That(AltGroup.ChildGroupIds, Is.EquivalentTo(new[] { "else-1" }));

        GroupContract ElseGroup = Contract.Groups.Single(Group => Group.Id == "else-1");
        Assert.That(ElseGroup.Title, Is.EqualTo("Not found"));
        Assert.That(ElseGroup.ParentGroupId, Is.EqualTo("alt-0"));

        EdgeContract RequestEdge = Contract.Edges.Single(Edge => Edge.Label == "Request");
        Assert.That(RequestEdge.Metadata.ContainsKey("sequenceParentGroup"), Is.False);

        EdgeContract OkEdge = Contract.Edges.Single(Edge => Edge.Label == "OK");
        Assert.That(OkEdge.Metadata["sequenceParentGroup"], Is.InstanceOf<RawMetadata>());
        Assert.That(((RawMetadata)OkEdge.Metadata["sequenceParentGroup"]).RawValue, Is.EqualTo("alt-0"));

        EdgeContract NotFoundEdge = Contract.Edges.Single(Edge => Edge.Label == "404");
        Assert.That(((RawMetadata)NotFoundEdge.Metadata["sequenceParentGroup"]).RawValue, Is.EqualTo("else-1"));
    }

    [Test]
    public void Apply_LoopBlock_CreatesGroupAndTagsNestedMessage()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                loop Every minute
                    A->>B: Ping
                end
            """);

        Assert.That(Contract.Groups, Has.Count.EqualTo(1));
        GroupContract LoopGroup = Contract.Groups[0];
        Assert.That(LoopGroup.Id, Is.EqualTo("loop-0"));
        Assert.That(LoopGroup.Title, Is.EqualTo("Every minute"));

        EdgeContract PingEdge = Contract.Edges.Single();
        Assert.That(((RawMetadata)PingEdge.Metadata["sequenceParentGroup"]).RawValue, Is.EqualTo("loop-0"));
    }

    [Test]
    public void Apply_BoxGroup_AssignsParticipantParentGroupId()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                box rgb(200,200,200) Internal
                    participant A
                    participant B
                end
                A->>B: Hi
            """);

        Assert.That(Contract.Groups, Has.Count.EqualTo(1));
        GroupContract BoxGroup = Contract.Groups[0];
        Assert.That(BoxGroup.Id, Is.EqualTo("box-0"));
        Assert.That(BoxGroup.Title, Is.EqualTo("Internal"));
        Assert.That(BoxGroup.ChildNodeIds, Is.EquivalentTo(new[] { "A", "B" }));

        NodeContract ParticipantA = Contract.Nodes.Single(Node => Node.Id == "A");
        NodeContract ParticipantB = Contract.Nodes.Single(Node => Node.Id == "B");
        Assert.That(ParticipantA.ParentGroupId, Is.EqualTo("box-0"));
        Assert.That(ParticipantB.ParentGroupId, Is.EqualTo("box-0"));

        EdgeContract MessageEdge = Contract.Edges.Single();
        Assert.That(MessageEdge.Metadata.ContainsKey("sequenceParentGroup"), Is.False);
    }

    [Test]
    public void Apply_OptParCriticalBreakRectBlocks_CreateGroupsAndTagMessages()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                opt Optional
                    A->>B: Maybe
                end
                par Branch A
                    A->>B: A1
                and Branch B
                    B->>A: B1
                end
                critical Region
                    A->>B: Critical
                option Timeout
                    B->>A: Retry
                end
                break On error
                    A-x>>B: Stop
                end
                rect rgb(200,200,200) Frame
                    A->>B: Inside
                end
            """);

        Assert.That(Contract.Groups.Select(Group => Group.Id), Is.EquivalentTo(new[]
        {
            "opt-0",
            "par-1",
            "and-2",
            "critical-3",
            "option-4",
            "break-5",
            "rect-6"
        }));

        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "Maybe").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("opt-0"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "A1").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("par-1"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "B1").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("and-2"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "Critical").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("critical-3"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "Retry").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("option-4"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "Stop").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("break-5"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "Inside").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("rect-6"));
    }

    [Test]
    public void Apply_NoteRightOf_CreatesNoteNodeWithPlacementMetadata()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                Alice->>Bob: Hello
                Note right of Bob: Bob thinks
                Bob-->>Alice: Hi
            """);

        NodeContract NoteNode = Contract.Nodes.Single(Node => Node.Id == "note-0");
        Assert.That(NoteNode.Shape, Is.EqualTo("note"));
        Assert.That(NoteNode.Label, Is.EqualTo("Bob thinks"));
        RawMetadata Placement = (RawMetadata)NoteNode.Metadata["sequenceNotePlacement"];
        Assert.That(Placement.RawValue, Is.EqualTo("right"));
        RawMetadata Anchor = (RawMetadata)NoteNode.Metadata["sequenceNoteAnchor"];
        Assert.That(Anchor.RawValue, Is.EqualTo("Bob"));
    }

    [Test]
    public void Apply_AccessibilityInline_PopulatesTitleAndDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                accTitle: Auth
                accDescr: Login flow
                Client->>Server: Login
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Auth"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Login flow"));
    }

    [Test]
    public void Apply_TitleAndAutonumber_PopulatesConfigurationFacts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                title Authentication Flow
                autonumber
                Client->>Server: Login
            """);

        Assert.That(Contract.Configuration.RawDirectiveValues["sequence.title"], Is.EqualTo("Authentication Flow"));
        Assert.That(Contract.Configuration.RawDirectiveValues["sequence.autonumber"], Is.EqualTo("true"));
    }

    [Test]
    public void Apply_AccessibilityBlock_PopulatesMultilineDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                accTitle: Auth
                accDescr {
                    Describes login
                    and session flow
                }
                Client->>Server: Login
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Auth"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Describes login\r\nand session flow"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.body"),
            Is.True);
    }

    [Test]
    public void Apply_CommentLine_ClassifiesAsCommentWithoutExtraModelFacts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                %% This is a comment
                A->>B: Visible message
            """);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.comment"),
            Is.True);
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
        Assert.That(Contract.Edges[0].Label, Is.EqualTo("Visible message"));
    }

    [Test]
    public void Apply_LeadingInitBlock_PopulatesThemeConfiguration()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            %%{init: {"theme": "base"}}%%
            sequenceDiagram
                A->>B: Themed message
            """);

        Assert.That(Contract.Configuration.RawDirectiveValues["theme"], Is.EqualTo("base"));
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
    }

    [Test]
    public void Apply_BreakBlock_CreatesGroupAndTagsNestedMessage()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                A->>B: Start
                break On error
                    B-->>A: Abort
                end
                A->>B: Continue
            """);

        GroupContract BreakGroup = Contract.Groups.Single(Group => Group.Id == "break-0");
        Assert.That(BreakGroup.Title, Is.EqualTo("On error"));
        EdgeContract AbortEdge = Contract.Edges.Single(Edge => Edge.Label == "Abort");
        Assert.That(((RawMetadata)AbortEdge.Metadata["sequenceParentGroup"]).RawValue, Is.EqualTo("break-0"));
        EdgeContract StartEdge = Contract.Edges.Single(Edge => Edge.Label == "Start");
        Assert.That(StartEdge.Metadata.ContainsKey("sequenceParentGroup"), Is.False);
    }

    [Test]
    public void Apply_RectBlock_CreatesGroupAndTagsNestedMessage()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                rect rgb(240, 240, 240)
                    A->>B: Inside rect
                end
            """);

        GroupContract RectGroup = Contract.Groups.Single(Group => Group.Id == "rect-0");
        Assert.That(RectGroup.Title, Is.EqualTo("rect"));
        EdgeContract InsideEdge = Contract.Edges.Single();
        Assert.That(InsideEdge.Label, Is.EqualTo("Inside rect"));
        Assert.That(((RawMetadata)InsideEdge.Metadata["sequenceParentGroup"]).RawValue, Is.EqualTo("rect-0"));
    }

    [Test]
    public void Apply_ClosedArrows_MapsSolidAndDottedTokens()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                A->>B: Solid
                A-->>B: Dotted
            """);

        Assert.That(Contract.Edges, Has.Count.EqualTo(2));
        RawMetadata SolidArrow = (RawMetadata)Contract.Edges[0].Metadata["sequenceArrow"];
        RawMetadata DottedArrow = (RawMetadata)Contract.Edges[1].Metadata["sequenceArrow"];
        Assert.That(SolidArrow.RawValue, Is.EqualTo("->>"));
        Assert.That(DottedArrow.RawValue, Is.EqualTo("-->>"));
    }

    [Test]
    public void Apply_NestedAltInsideLoop_TagsMessagesWithNestedGroups()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                loop Retry up to 3
                    A->>B: Attempt
                    alt Success
                        B-->>A: OK
                    else Failure
                        B-->>A: Retry
                    end
                end
            """);

        GroupContract LoopGroup = Contract.Groups.Single(Group => Group.Id == "loop-0");
        GroupContract AltGroup = Contract.Groups.Single(Group => Group.Id == "alt-1");
        GroupContract ElseGroup = Contract.Groups.Single(Group => Group.Id == "else-2");
        Assert.That(AltGroup.ParentGroupId, Is.EqualTo("loop-0"));
        Assert.That(ElseGroup.ParentGroupId, Is.EqualTo("alt-1"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "Attempt").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("loop-0"));
        Assert.That(
            ((RawMetadata)Contract.Edges.Single(Edge => Edge.Label == "OK").Metadata["sequenceParentGroup"]).RawValue,
            Is.EqualTo("alt-1"));
    }

    [Test]
    public void Apply_AltWithoutElseBranch_CreatesSingleBranchFrame()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                A->>B: Check
                alt OK
                    B-->>A: Proceed
                end
            """);

        Assert.That(Contract.Groups, Has.Count.EqualTo(1));
        GroupContract AltGroup = Contract.Groups.Single(Group => Group.Id == "alt-0");
        Assert.That(AltGroup.Title, Is.EqualTo("OK"));
        EdgeContract ProceedEdge = Contract.Edges.Single(Edge => Edge.Label == "Proceed");
        Assert.That(((RawMetadata)ProceedEdge.Metadata["sequenceParentGroup"]).RawValue, Is.EqualTo("alt-0"));
    }

    [Test]
    public void Apply_QuotedMessageLabelWithColon_ParsesLabelAfterArrow()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                A->>B: "Step 1: validate input"
            """);

        Assert.That(Contract.Edges.Single().Label, Is.EqualTo("Step 1: validate input"));
    }
}
