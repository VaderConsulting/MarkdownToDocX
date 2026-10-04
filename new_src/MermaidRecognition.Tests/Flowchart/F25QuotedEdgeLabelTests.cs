using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class F25QuotedEdgeLabelTests
{
    [Test]
    public void F25_And_F26_QuotedPipeEdgeLabels_ShareSameNormalization()
    {
        string F25Source = """
            flowchart TB
            A[Source] -->|"Step 1: validate input"| B[Validator]
            """;

        string F26Source = """
            flowchart LR
            A[Producer] -->|"This edge label is intentionally long so wrapping, clearance from arrowheads, and node borders can be verified in Word."| B[Consumer]
            """;

        EdgeContract F25Edge = FlowchartRecognizer.Parse(F25Source).Edges.Single();
        EdgeContract F26Edge = FlowchartRecognizer.Parse(F26Source).Edges.Single();

        Assert.That(F25Edge.Label, Does.Not.Contain("\""));
        Assert.That(F26Edge.Label, Does.Not.Contain("\""));
        Assert.That(F26Edge.Label, Does.StartWith("This edge label is intentionally long"));
    }

    [Test]
    public void F25_QuotedPipeEdgeLabels_StripDelimiterQuotes()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TB
            A[Source] -->|"Step 1: validate input"| B[Validator]
            B -->|"Step 2: persist (async)"| C[Store]
            """);

        Assert.That(Contract.Edges, Has.Count.EqualTo(2));

        EdgeContract First = Contract.Edges.Single(Edge => Edge.SourceId == "A");
        EdgeContract Second = Contract.Edges.Single(Edge => Edge.SourceId == "B");

        Assert.That(First.Label, Is.EqualTo("Step 1: validate input"));
        Assert.That(Second.Label, Is.EqualTo("Step 2: persist (async)"));
        Assert.That(First.Label, Does.Not.Contain("\""));
        Assert.That(Second.Label, Does.Not.Contain("\""));
    }
}
