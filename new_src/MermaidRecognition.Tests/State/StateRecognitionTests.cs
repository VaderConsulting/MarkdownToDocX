namespace MermaidRecognition.Tests.State;

[TestFixture]
public sealed class StateRecognitionTests
{
    [Test]
    public void Parse_Simple_ClassifiesTransitions()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                [*] --> Still
                Still --> [*]
            """);

        Assert.That(Contract.Kind, Is.EqualTo("stateDiagram"));
        Assert.That(
            Contract.SourceStatements.Count(Statement => Statement.LineTypeId == "stateDiagram.transition"),
            Is.EqualTo(2));
    }

    [Test]
    public void Parse_Description_ClassifiesStateLine()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(
            """
            stateDiagram-v2
                state "Processing" as P
            """);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "stateDiagram.state"),
            Is.True);
    }
}
