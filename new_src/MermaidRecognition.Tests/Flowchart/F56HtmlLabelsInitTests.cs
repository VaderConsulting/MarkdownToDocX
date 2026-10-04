using MermaidRecognition.Config;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class F56HtmlLabelsInitTests
{
    const string F56_SOURCE = """
        %%{init: {"flowchart": {"htmlLabels": false}} }%%
        flowchart TB
        A["Native SVG labels"] --> B["Wrap without HTML foreignObject."]
        B --> C["Final"]
        """;

    [Test]
    public void F56_QuotedNodeLabels_StripDelimiterQuotes()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(F56_SOURCE);

        NodeContract NodeA = Contract.Nodes.Single(Node => Node.Id == "A");
        NodeContract NodeB = Contract.Nodes.Single(Node => Node.Id == "B");
        NodeContract NodeC = Contract.Nodes.Single(Node => Node.Id == "C");

        Assert.That(NodeA.Label, Is.EqualTo("Native SVG labels"));
        Assert.That(NodeB.Label, Is.EqualTo("Wrap without HTML foreignObject."));
        Assert.That(NodeC.Label, Is.EqualTo("Final"));
        Assert.That(NodeA.Label, Does.Not.Contain("\""));
        Assert.That(NodeB.Label, Does.Not.Contain("\""));
        Assert.That(NodeC.Label, Does.Not.Contain("\""));
    }

    [Test]
    public void F56_InitBlock_ParsesHtmlLabelsFalse()
    {
        Dictionary<string, string> InitValues = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(F56_SOURCE);

        Assert.That(InitValues.TryGetValue("flowchart.htmlLabels", out string? HtmlLabels), Is.True);
        Assert.That(HtmlLabels, Is.EqualTo("false").IgnoreCase);
    }

    [Test]
    public void F56_InitBlock_ResolvesEffectiveHtmlLabelsFalse()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(F56_SOURCE);

        Assert.That(Contract.Configuration.EffectiveValues["htmlLabels"], Is.EqualTo("false").IgnoreCase);
        Assert.That(Contract.Configuration.RawDirectiveValues["flowchart.htmlLabels"], Is.EqualTo("false").IgnoreCase);
    }
}
