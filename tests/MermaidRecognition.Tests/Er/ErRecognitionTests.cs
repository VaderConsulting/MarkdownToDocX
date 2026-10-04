namespace MermaidRecognition.Tests.Er;

[TestFixture]
public sealed class ErRecognitionTests
{
    [TestCase("CUSTOMER ||--o{ ORDER : places", "E01")]
    [TestCase("ENTITY_A ||--|| ENTITY_B : one-to-one", "E03")]
    [TestCase("ENTITY_A |o--o| ENTITY_B : zero-or-one", "E04")]
    [TestCase("ENTITY_A ||..o{ ENTITY_B : non-identifying", "E05")]
    [TestCase("ORDER ||--|{ LINE-ITEM : contains", "E06")]
    [TestCase("ENTITY_A }o--|| ENTITY_B : zero-or-more-to-one", "E20")]
    [TestCase("ENTITY_A |o..o{ ENTITY_B : dotted-zero-or-one", "E22")]
    [TestCase("ENTITY_A }o--o{ ENTITY_B : zero-or-more-both", "E23")]
    [TestCase("ENTITY_A }|..o{ ENTITY_B : one-or-more-dotted", "E24")]
    [TestCase("ENTITY_A }o..o{ ENTITY_B : dotted-zero-or-more", "E25")]
    [TestCase("ENTITY_A o|--o{ ENTITY_B : zero-or-one-left", "E26")]
    [TestCase("ENTITY_A }o..|| ENTITY_B : dotted-to-only-one", "E27")]
    public void Parse_RelationshipCardinality_ClassifiesRelationship(string RelationshipLine, string ScenarioId)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            $"""
            erDiagram
                {RelationshipLine}
            """);

        Assert.That(Contract.Kind, Is.EqualTo("erDiagram"), ScenarioId);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.relationship"),
            Is.True,
            ScenarioId);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.body"),
            Is.False,
            ScenarioId);
    }

    [Test]
    public void Parse_Simple_ClassifiesRelationship()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER ||--o{ ORDER : places
            """);

        Assert.That(Contract.Kind, Is.EqualTo("erDiagram"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.relationship"),
            Is.True);
    }

    [Test]
    public void Parse_Attributes_ClassifiesEntityBlock()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            erDiagram
                CUSTOMER {
                    int id PK
                }
            """);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.entityBlockOpen"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.attribute"),
            Is.True);
    }
}
