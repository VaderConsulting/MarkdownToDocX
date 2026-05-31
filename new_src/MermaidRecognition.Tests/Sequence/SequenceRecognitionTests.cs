namespace MermaidRecognition.Tests.Sequence;

[TestFixture]
public sealed class SequenceRecognitionTests
{
    [Test]
    public void Parse_ParticipantsAndMessages_ClassifiesLineTypes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                participant A as Alice
                A->>B: Hello
            """);

        Assert.That(Contract.Kind, Is.EqualTo("sequence"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.participant"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.message"),
            Is.True);
    }

    [Test]
    public void Parse_ControlBlocks_ClassifiesLineTypes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                loop Every minute
                    A->>B: Ping
                end
            """);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.block"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.end"),
            Is.True);
    }

    [Test]
    public void Parse_ActivateNoteTitle_ClassifiesLineTypes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            sequenceDiagram
                title Demo
                activate A
                note right of A: Note
            """);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.title"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.activate"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.note"),
            Is.True);
    }
}
