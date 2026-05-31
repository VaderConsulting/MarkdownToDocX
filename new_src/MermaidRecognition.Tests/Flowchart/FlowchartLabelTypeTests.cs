using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartLabelTypeTests
{
    [Test]
    public void MetadataLabelType_Markdown_IsPreserved()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                N@{ label: "**bold**", labelType: markdown }
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "N");
        Assert.That(Node.Label, Is.EqualTo("**bold**"));
        Assert.That(Node.LabelType, Is.EqualTo(MermaidLabelType.Markdown));
    }
}
