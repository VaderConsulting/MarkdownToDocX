using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class F48EntityCodeTests
{
    [Test]
    public void F48_MermaidEntityCodes_DecodeInNodeLabels()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TB
            A["Symbols: star#42; underscore#95; backtick#96; brackets#91;#93; pipe A#124;B"] --> B["Quotes: double#quot; and apostrophe#39;s"]
            B --> C["Comparison: a#lt; b #38;#38; c#gt; d"]
            """);

        NodeContract NodeA = Contract.Nodes.Single(Node => Node.Id == "A");
        NodeContract NodeB = Contract.Nodes.Single(Node => Node.Id == "B");
        NodeContract NodeC = Contract.Nodes.Single(Node => Node.Id == "C");

        Assert.That(NodeA.Label, Does.Contain("*"));
        Assert.That(NodeA.Label, Does.Not.Contain("#42;"));
        Assert.That(NodeA.Label, Does.Contain("[]"));
        Assert.That(NodeA.Label, Does.Contain("A|B"));

        Assert.That(NodeB.Label, Is.EqualTo("Quotes: double\" and apostrophe's"));
        Assert.That(NodeB.Label, Does.Not.Contain("#quot;"));

        Assert.That(NodeC.Label, Is.EqualTo("Comparison: a< b && c> d"));
        Assert.That(NodeC.Label, Does.Not.Contain("#lt;"));
    }

    [Test]
    public void FlowchartEntityCodeDecoder_DecodesNumericAndNamedCodes()
    {
        Assert.That(
            FlowchartEntityCodeDecoder.Decode("star#42; quote#quot; lt#lt;"),
            Is.EqualTo("star* quote\" lt<"));
    }
}
