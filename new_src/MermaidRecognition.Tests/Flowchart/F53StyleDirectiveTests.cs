using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class F53StyleDirectiveTests
{
    [Test]
    public void F53_StyleDirective_PopulatesNodeInlineStyle()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            Start([Start]) --> Process[Process]
            Process --> Decision{Valid?}
            Decision -->|Yes| Success[Success]
            Decision -->|No| Error[Error]
            Success --> End([End])
            Error --> End

            style Start fill:#e1f5e1
            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """);

        NodeContract Start = Contract.Nodes.Single(Node => Node.Id == "Start");
        NodeContract End = Contract.Nodes.Single(Node => Node.Id == "End");
        NodeContract Error = Contract.Nodes.Single(Node => Node.Id == "Error");
        NodeContract Success = Contract.Nodes.Single(Node => Node.Id == "Success");

        Assert.That(Start.InlineStyle.Fill, Is.EqualTo("#e1f5e1"));
        Assert.That(End.InlineStyle.Fill, Is.EqualTo("#e1f5e1"));
        Assert.That(Error.InlineStyle.Fill, Is.EqualTo("#f8d7da"));
        Assert.That(Success.InlineStyle.Fill, Is.EqualTo("#d1ecf1"));
    }

    [Test]
    public void StyleDirective_BeforeNodeDeclaration_StillAppliesFill()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            style Orphan fill:#e1f5e1
            Orphan[Later node]
            """);

        NodeContract Orphan = Contract.Nodes.Single(Node => Node.Id == "Orphan");
        Assert.That(Orphan.InlineStyle.Fill, Is.EqualTo("#e1f5e1"));
    }

    [Test]
    public void F53A_StyleDirective_PopulatesNodeInlineStyle()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            Success --> End([End])
            Error --> End

            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """);

        NodeContract End = Contract.Nodes.Single(Node => Node.Id == "End");
        NodeContract Error = Contract.Nodes.Single(Node => Node.Id == "Error");
        NodeContract Success = Contract.Nodes.Single(Node => Node.Id == "Success");

        Assert.That(End.InlineStyle.Fill, Is.EqualTo("#e1f5e1"));
        Assert.That(Error.InlineStyle.Fill, Is.EqualTo("#f8d7da"));
        Assert.That(Success.InlineStyle.Fill, Is.EqualTo("#d1ecf1"));
    }
}
