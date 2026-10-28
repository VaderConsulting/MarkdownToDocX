using MermaidRecognition.Kinds.Class;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Class;

[TestFixture]
public sealed class ClassContractMapperTests
{
    [Test]
    public void Apply_SimpleClass_CreatesNode()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(1));
        Assert.That(Contract.Nodes[0].Id, Is.EqualTo("Animal"));
        Assert.That(Contract.Nodes[0].Label, Is.EqualTo("Animal"));
        Assert.That(Contract.Edges, Is.Empty);
    }

    [Test]
    public void Apply_Relations_CreatesNodesAndEdgesWithTokens()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                Animal <|-- Duck
                Duck <|-- Mallard
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(3));
        Assert.That(Contract.Edges, Has.Count.EqualTo(2));

        EdgeContract FirstEdge = Contract.Edges[0];
        Assert.That(FirstEdge.SourceId, Is.EqualTo("Animal"));
        Assert.That(FirstEdge.TargetId, Is.EqualTo("Duck"));
        Assert.That(FirstEdge.Metadata["classRelation"], Is.InstanceOf<RawMetadata>());
        RawMetadata FirstMetadata = (RawMetadata)FirstEdge.Metadata["classRelation"];
        Assert.That(FirstMetadata.RawValue, Is.EqualTo("<|--"));

        EdgeContract SecondEdge = Contract.Edges[1];
        Assert.That(SecondEdge.SourceId, Is.EqualTo("Duck"));
        Assert.That(SecondEdge.TargetId, Is.EqualTo("Mallard"));
        RawMetadata SecondMetadata = (RawMetadata)SecondEdge.Metadata["classRelation"];
        Assert.That(SecondMetadata.RawValue, Is.EqualTo("<|--"));
    }

    [Test]
    public void Apply_Members_AttachesDeclarationsToClass()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal {
                    +String name
                    +int age
                }
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(1));
        NodeContract Animal = Contract.Nodes[0];
        Assert.That(Animal.Id, Is.EqualTo("Animal"));
        Assert.That(Animal.Declarations, Has.Count.EqualTo(2));
        Assert.That(Animal.Declarations[0].Kind, Is.EqualTo("member"));
        Assert.That(Animal.Declarations[0].RawText, Is.EqualTo("+String name"));
        Assert.That(Animal.Declarations[1].RawText, Is.EqualTo("+int age"));
    }

    [Test]
    public void Apply_ClassDefAndApply_PopulatesClassDefsAndNodeClasses()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal
                classDef highlight fill:#f9f,stroke:#333
                class Animal highlight
            """);

        Assert.That(Contract.ClassDefs, Has.Count.EqualTo(1));
        Assert.That(Contract.ClassDefs[0].ClassId, Is.EqualTo("highlight"));
        Assert.That(Contract.ClassDefs[0].Style.Stroke, Is.EqualTo("#333"));

        Assert.That(Contract.Nodes, Has.Count.EqualTo(1));
        NodeContract Animal = Contract.Nodes[0];
        Assert.That(Animal.Classes, Contains.Item("highlight"));
        Assert.That(Animal.InlineStyle.Stroke, Is.EqualTo("#333"));
    }

    [Test]
    public void Apply_LeavesSourceStatementsUnchanged()
    {
        DiagramContract Contract = new DiagramContract
        {
            Kind = "classDiagram"
        };
        SourceStatementContract Statement = new SourceStatementContract
        {
            LineNumber = 2,
            LineTypeId = "classDiagram.class",
            RawLine = "    class Animal"
        };
        Contract.SourceStatementsMutable.Add(Statement);

        int StatementCount = Contract.SourceStatements.Count;
        string LineTypeId = Contract.SourceStatements[0].LineTypeId;
        string RawLine = Contract.SourceStatements[0].RawLine;

        ClassContractMapper.Apply(Contract);

        Assert.That(Contract.SourceStatements.Count, Is.EqualTo(StatementCount));
        Assert.That(Contract.SourceStatements[0].LineTypeId, Is.EqualTo(LineTypeId));
        Assert.That(Contract.SourceStatements[0].RawLine, Is.EqualTo(RawLine));
    }

    [Test]
    public void Apply_Namespace_AssignsParentGroupIdAndGroupContract()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                namespace animals {
                    class Animal
                    class Dog
                }
            """);

        Assert.That(Contract.Groups, Has.Count.EqualTo(1));
        GroupContract Animals = Contract.Groups[0];
        Assert.That(Animals.Id, Is.EqualTo("animals"));
        Assert.That(Animals.ChildNodeIds, Is.EquivalentTo(["Animal", "Dog"]));

        NodeContract Animal = Contract.Nodes.Single(Node => Node.Id == "Animal");
        NodeContract Dog = Contract.Nodes.Single(Node => Node.Id == "Dog");
        Assert.That(Animal.ParentGroupId, Is.EqualTo("animals"));
        Assert.That(Dog.ParentGroupId, Is.EqualTo("animals"));
    }

    [Test]
    public void Apply_AccessibilityInline_PopulatesTitleAndDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                accTitle: Domain model
                accDescr: Core entities
                class Animal
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Domain model"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Core entities"));
    }

    [Test]
    public void Apply_AccessibilityBlock_PopulatesMultilineDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                accTitle: Domain model
                accDescr {
                    Describes core
                    domain entities
                }
                class Animal
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Domain model"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Describes core\r\ndomain entities"));
    }

    [Test]
    public void Apply_NoteForClass_AttachesNoteDeclaration()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal
                note for Animal "Base type"
            """);

        NodeContract Animal = Contract.Nodes.Single(Node => Node.Id == "Animal");
        MermaidDeclaration Note = Animal.Declarations.Single(Declaration => Declaration.Kind == "note");
        Assert.That(Note.RawText, Is.EqualTo("Base type"));
    }

    [Test]
    public void Apply_StandaloneNote_CreatesSyntheticNoteNode()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                note "Diagram note"
            """);

        NodeContract NoteNode = Contract.Nodes.Single(Node => Node.Id == "note-0");
        Assert.That(NoteNode.Label, Is.EqualTo("Diagram note"));
        Assert.That(
            NoteNode.Declarations.Single(Declaration => Declaration.Kind == "note").RawText,
            Is.EqualTo("Diagram note"));
    }

    [Test]
    public void Apply_ClickHref_RecordsInteraction()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal
                click Animal href "https://example.com" "Docs"
            """);

        InteractionDirective Interaction = Contract.Interactions.Single();
        Assert.That(Interaction.TargetId, Is.EqualTo("Animal"));
        Assert.That(Interaction.Url, Is.EqualTo("https://example.com"));
        Assert.That(Interaction.Tooltip, Is.EqualTo("Docs"));
        Assert.That(Contract.Diagnostics, Is.Empty);
    }

    [Test]
    public void Apply_ClickCallback_EmitsDiagnostic()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal
                click Animal myCallback
            """);

        Assert.That(Contract.Interactions.Single().Callback, Is.EqualTo("myCallback"));
        Assert.That(
            Contract.Diagnostics.Select(Diagnostic => Diagnostic.Code),
            Contains.Item("class.click-callback-not-exported"));
    }

    [Test]
    public void Apply_InlineStyle_AppliesStyleToClass()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal
                style Animal fill:#bbf,stroke:#333
            """);

        NodeContract Animal = Contract.Nodes.Single(Node => Node.Id == "Animal");
        Assert.That(Animal.InlineStyle.Fill, Is.EqualTo("#bbf"));
        Assert.That(Animal.InlineStyle.Stroke, Is.EqualTo("#333"));
    }

    [Test]
    public void Apply_Direction_SetsContractDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                direction LR
                Animal <|-- Dog
            """);

        Assert.That(Contract.Direction, Is.EqualTo("LeftToRight"));
    }

    [Test]
    public void Apply_InterfaceStereotype_AttachesStereotypeDeclaration()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class IFlyable {
                    <<interface>>
                    +fly() void
                }
            """);

        NodeContract Flyable = Contract.Nodes.Single(Node => Node.Id == "IFlyable");
        MermaidDeclaration Stereotype = Flyable.Declarations.Single(Declaration => Declaration.Kind == "stereotype");
        Assert.That(Stereotype.RawText, Is.EqualTo("<<interface>>"));
        Assert.That(Flyable.Declarations.Single(Declaration => Declaration.Kind == "member").RawText, Is.EqualTo("+fly() void"));
    }

    [Test]
    public void Apply_AbstractStereotype_AttachesStereotypeDeclaration()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Shape {
                    <<abstract>>
                    +area() double
                }
            """);

        NodeContract Shape = Contract.Nodes.Single(Node => Node.Id == "Shape");
        MermaidDeclaration Stereotype = Shape.Declarations.Single(Declaration => Declaration.Kind == "stereotype");
        Assert.That(Stereotype.RawText, Is.EqualTo("<<abstract>>"));
    }

    [Test]
    public void Apply_EnumAndServiceStereotypes_AttachStereotypeDeclarations()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Status {
                    <<enumeration>>
                    ACTIVE
                }
                class UserService {
                    <<service>>
                    +findUser(id: int) User
                }
            """);

        NodeContract Status = Contract.Nodes.Single(Node => Node.Id == "Status");
        NodeContract UserService = Contract.Nodes.Single(Node => Node.Id == "UserService");
        Assert.That(Status.Declarations.Single(Declaration => Declaration.Kind == "stereotype").RawText, Is.EqualTo("<<enumeration>>"));
        Assert.That(UserService.Declarations.Single(Declaration => Declaration.Kind == "stereotype").RawText, Is.EqualTo("<<service>>"));
    }

    [Test]
    public void Apply_RelationCardinality_PopulatesEdgeMetadataAndLabels()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                User "1" --> "1..*" Address : has
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.SourceId, Is.EqualTo("User"));
        Assert.That(Edge.TargetId, Is.EqualTo("Address"));
        Assert.That(Edge.Label, Is.EqualTo("has"));
        RawMetadata SourceCardinality = (RawMetadata)Edge.Metadata["classSourceCardinality"];
        RawMetadata TargetCardinality = (RawMetadata)Edge.Metadata["classTargetCardinality"];
        Assert.That(SourceCardinality.RawValue, Is.EqualTo("1"));
        Assert.That(TargetCardinality.RawValue, Is.EqualTo("1..*"));
    }

    [Test]
    public void Apply_HeaderV2_RecordsClassDiagramV2Header()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram-v2
                class Animal
            """);

        Assert.That(Contract.Header, Is.EqualTo("classDiagram-v2"));
    }

    [Test]
    public void Apply_GenericClass_ParsesTildeTypeParameterInId()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Repository~T~ {
                    +get(id: int) T
                }
            """);

        NodeContract Repository = Contract.Nodes.Single(Node => Node.Id == "Repository~T~");
        Assert.That(Repository.Declarations, Has.Count.EqualTo(1));
    }

    [Test]
    public void Apply_LeadingInitBlock_PopulatesThemeConfiguration()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            %%{init: {"theme": "base"}}%%
            classDiagram
                class Animal
            """);

        Assert.That(Contract.Configuration.RawDirectiveValues["theme"], Is.EqualTo("base"));
    }
}
