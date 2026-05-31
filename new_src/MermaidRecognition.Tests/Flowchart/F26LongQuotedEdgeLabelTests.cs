using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class F26LongQuotedEdgeLabelTests
{
    [Test]
    public void F26_LongQuotedPipeEdgeLabel_StripDelimiterQuotes()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A[Producer] -->|"This edge label is intentionally long so wrapping, clearance from arrowheads, and node borders can be verified in Word."| B[Consumer]
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Label, Does.StartWith("This edge label is intentionally long"));
        Assert.That(Edge.Label, Does.EndWith("verified in Word."));
        Assert.That(Edge.Label, Does.Not.Contain("\""));
    }
}
