namespace MermaidRecognition.Tests.Class;

[TestFixture]
public sealed class ClassRecognitionTests
{
    [Test]
    public void Parse_Simple_ClassifiesHeaderAndClass()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal
            """);

        Assert.That(Contract.Kind, Is.EqualTo("classDiagram"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "classDiagram.header"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "classDiagram.class"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "classDiagram.body"),
            Is.False);
    }

    [Test]
    public void Parse_Members_ClassifiesMemberLines()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            classDiagram
                class Animal {
                    +String name
                }
            """);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "classDiagram.class"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(
                Statement => Statement.LineTypeId == "classDiagram.class"
                    || Statement.LineTypeId == "classDiagram.structOpen"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "classDiagram.member"),
            Is.True);
    }
}
