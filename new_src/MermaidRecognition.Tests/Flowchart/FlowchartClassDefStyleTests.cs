using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartClassDefStyleTests
{
    [Test]
    public void ClassDef_And_Class_ApplyToNode()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                classDef highlight stroke:#f00
                class A highlight
            """);

        ClassDefContract ClassDef = Contract.ClassDefs.Single();
        Assert.That(ClassDef.ClassId, Is.EqualTo("highlight"));
        Assert.That(ClassDef.Style.Stroke, Is.EqualTo("#f00"));
        NodeContract NodeA = Contract.Nodes.Single(Node => Node.Id == "A");
        Assert.That(NodeA.Classes, Contains.Item("highlight"));
        Assert.That(NodeA.InlineStyle.Stroke, Is.EqualTo("#f00"));
    }

    [Test]
    public void Style_AppliesInlineStroke()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                style A stroke:#00f
            """);

        NodeContract NodeA = Contract.Nodes.Single(Node => Node.Id == "A");
        Assert.That(NodeA.InlineStyle.Stroke, Is.EqualTo("#00f"));
    }

    [Test]
    public void NodeClassSuffix_AppliesClass()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                classDef done fill:#eee
                Done:::done
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "Done");
        Assert.That(Node.Classes, Contains.Item("done"));
    }
}
