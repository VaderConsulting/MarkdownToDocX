using MermaidRecognition.Contract;
using MermaidRecognition.Er;

namespace MermaidRecognition.Tests.Er;

[TestFixture]
public sealed class ErContractMapperTests
{
    [Test]
    public void Apply_Relationship_CreatesNodesAndEdgesWithCardinalityMetadata()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER ||--o{ ORDER : places
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(2));
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "CUSTOMER"), Is.True);
        Assert.That(Contract.Nodes.Any(Node => Node.Id == "ORDER"), Is.True);
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));

        EdgeContract Edge = Contract.Edges[0];
        Assert.That(Edge.SourceId, Is.EqualTo("CUSTOMER"));
        Assert.That(Edge.TargetId, Is.EqualTo("ORDER"));
        Assert.That(Edge.Label, Is.EqualTo("places"));
        Assert.That(Edge.Metadata["erLeftMarker"], Is.InstanceOf<RawMetadata>());
        RawMetadata LeftMarker = (RawMetadata)Edge.Metadata["erLeftMarker"];
        Assert.That(LeftMarker.RawValue, Is.EqualTo("||"));
        RawMetadata Link = (RawMetadata)Edge.Metadata["erLink"];
        Assert.That(Link.RawValue, Is.EqualTo("--"));
        RawMetadata RightMarker = (RawMetadata)Edge.Metadata["erRightMarker"];
        Assert.That(RightMarker.RawValue, Is.EqualTo("o{"));
        RawMetadata RoleLabel = (RawMetadata)Edge.Metadata["erRoleLabel"];
        Assert.That(RoleLabel.RawValue, Is.EqualTo("places"));
    }

    [Test]
    public void Apply_EntityAttributes_AttachesAttributeDeclarations()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER {
                    string name
                    string email PK
                }
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(1));
        NodeContract Customer = Contract.Nodes[0];
        Assert.That(Customer.Id, Is.EqualTo("CUSTOMER"));
        Assert.That(Customer.Declarations, Has.Count.EqualTo(2));
        Assert.That(Customer.Declarations[0].Kind, Is.EqualTo("attribute"));
        Assert.That(Customer.Declarations[0].RawText, Is.EqualTo("string name"));
        Assert.That(Customer.Declarations[1].RawText, Is.EqualTo("string email PK"));
        Assert.That(Contract.Edges, Is.Empty);
    }

    [Test]
    public void Apply_LeavesSourceStatementsUnchanged()
    {
        DiagramContract Contract = new DiagramContract();
        Contract.Kind = "erDiagram";
        SourceStatementContract Statement = new SourceStatementContract();
        Statement.LineNumber = 2;
        Statement.LineTypeId = "erDiagram.relationship";
        Statement.RawLine = "    CUSTOMER ||--o{ ORDER : places";
        Contract.SourceStatementsMutable.Add(Statement);

        int StatementCount = Contract.SourceStatements.Count;
        string LineTypeId = Contract.SourceStatements[0].LineTypeId;
        string RawLine = Contract.SourceStatements[0].RawLine;

        ErContractMapper.Apply(Contract);

        Assert.That(Contract.SourceStatements.Count, Is.EqualTo(StatementCount));
        Assert.That(Contract.SourceStatements[0].LineTypeId, Is.EqualTo(LineTypeId));
        Assert.That(Contract.SourceStatements[0].RawLine, Is.EqualTo(RawLine));
    }

    [Test]
    public void Apply_ClassDefAndClassApply_PopulatesClassDefsAndNodeClasses()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER ||--o{ ORDER : places
                classDef highlight fill:#f9f
                class CUSTOMER highlight
            """);

        Assert.That(Contract.ClassDefs, Has.Count.EqualTo(1));
        Assert.That(Contract.ClassDefs[0].ClassId, Is.EqualTo("highlight"));

        NodeContract Customer = Contract.Nodes.Single(Node => Node.Id == "CUSTOMER");
        Assert.That(Customer.Classes, Contains.Item("highlight"));
    }

    [Test]
    public void Apply_InlineStyle_AppliesStyleToEntity()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER {
                    string name
                }
                style CUSTOMER fill:#bbf
            """);

        NodeContract Customer = Contract.Nodes.Single(Node => Node.Id == "CUSTOMER");
        Assert.That(Customer.InlineStyle.Fill, Is.EqualTo("#bbf"));
    }

    [Test]
    public void Apply_AccessibilityInline_PopulatesTitleAndDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                accTitle: Sales schema
                accDescr: Customer and order entities
                CUSTOMER ||--o{ ORDER : places
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Sales schema"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Customer and order entities"));
    }

    [Test]
    public void Apply_QuotedRelationshipLabel_StripsQuotesFromRoleLabel()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                USER ||--o{ SESSION : "logs in"
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Label, Is.EqualTo("logs in"));
        RawMetadata RoleLabel = (RawMetadata)Edge.Metadata["erRoleLabel"];
        Assert.That(RoleLabel.RawValue, Is.EqualTo("logs in"));
    }

    [Test]
    public void Apply_Direction_SetsContractDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                direction LR
                A ||--|| B : links
            """);

        Assert.That(Contract.Direction, Is.EqualTo("LeftToRight"));
    }

    [Test]
    public void Apply_AttributeComments_PreservesQuotedCommentText()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER {
                    int id PK "Primary key"
                    string name "Customer name"
                }
            """);

        NodeContract Customer = Contract.Nodes.Single();
        Assert.That(Customer.Declarations, Has.Count.EqualTo(2));
        Assert.That(Customer.Declarations[0].RawText, Is.EqualTo("int id PK \"Primary key\""));
        Assert.That(Customer.Declarations[1].RawText, Is.EqualTo("string name \"Customer name\""));
    }

    [Test]
    public void Apply_AccessibilityBlock_PopulatesMultilineDescription()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                accTitle: Sales schema
                accDescr {
                    Entity relationship model
                    for the sales domain
                }
                CUSTOMER ||--o{ ORDER : places
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Sales schema"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Entity relationship model\r\nfor the sales domain"));
    }

    [Test]
    public void Apply_CombinedSchema_AttributesAndRelationship()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER {
                    int customer_id PK
                    string email UK
                }
                ORDER {
                    int order_id PK
                    int customer_id FK
                    date order_date
                }
                CUSTOMER ||--o{ ORDER : places
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(2));
        NodeContract Customer = Contract.Nodes.Single(Node => Node.Id == "CUSTOMER");
        NodeContract Order = Contract.Nodes.Single(Node => Node.Id == "ORDER");
        Assert.That(Customer.Declarations, Has.Count.EqualTo(2));
        Assert.That(Order.Declarations, Has.Count.EqualTo(3));
        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.SourceId, Is.EqualTo("CUSTOMER"));
        Assert.That(Edge.TargetId, Is.EqualTo("ORDER"));
        Assert.That(Edge.Label, Is.EqualTo("places"));
    }

    [Test]
    public void Apply_RecursiveRelationship_LinksEntityToItself()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                NODE ||--o{ NODE : "leads to"
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(1));
        Assert.That(Contract.Nodes[0].Id, Is.EqualTo("NODE"));
        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.SourceId, Is.EqualTo("NODE"));
        Assert.That(Edge.TargetId, Is.EqualTo("NODE"));
        Assert.That(Edge.Label, Is.EqualTo("leads to"));
    }

    [Test]
    public void Apply_DuplicateRoleLabels_CreatesDistinctEdgesForSameEntityPair()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CAR ||--o{ PERSON : "insured for"
                CAR ||--o{ PERSON : "owns"
            """);

        Assert.That(Contract.Nodes, Has.Count.EqualTo(2));
        Assert.That(Contract.Edges, Has.Count.EqualTo(2));
        Assert.That(Contract.Edges.Select(Edge => Edge.Label), Is.EquivalentTo(new[] { "insured for", "owns" }));
        Assert.That(Contract.Edges.All(Edge => Edge.SourceId == "CAR" && Edge.TargetId == "PERSON"), Is.True);
    }

    [Test]
    public void Apply_LeadingInitBlock_PopulatesThemeConfiguration()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            %%{init: {"theme": "base"}}%%
            erDiagram
                A ||--|| B : relates
            """);

        Assert.That(Contract.Configuration.RawDirectiveValues["theme"], Is.EqualTo("base"));
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
    }

    [Test]
    public void Apply_HyphenatedEntityNames_ParsesRelationshipEndpoints()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                LINE-ITEM ||--o{ WAREHOUSE-STOCK : stores
            """);

        Assert.That(Contract.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "LINE-ITEM", "WAREHOUSE-STOCK" }));
        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.SourceId, Is.EqualTo("LINE-ITEM"));
        Assert.That(Edge.TargetId, Is.EqualTo("WAREHOUSE-STOCK"));
        Assert.That(Edge.Label, Is.EqualTo("stores"));
    }

    [Test]
    public void Apply_ClassApplyWithoutClassDef_AssignsClassNameToEntity()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER {
                    string name
                }
                class CUSTOMER important
            """);

        NodeContract Customer = Contract.Nodes.Single(Node => Node.Id == "CUSTOMER");
        Assert.That(Customer.Classes, Contains.Item("important"));
    }

    [Test]
    public void Apply_RelationshipBeforeEntityBlock_ParsesBothInOrder()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CAR ||--o{ DRIVER : "insured for"
                CAR {
                    string model
                }
            """);

        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
        NodeContract Car = Contract.Nodes.Single(Node => Node.Id == "CAR");
        Assert.That(Car.Declarations.Single().RawText, Is.EqualTo("string model"));
    }
}
