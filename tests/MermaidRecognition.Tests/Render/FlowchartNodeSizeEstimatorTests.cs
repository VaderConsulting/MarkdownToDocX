using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class FlowchartNodeSizeEstimatorTests
{
    [Test]
    public void Estimate_LongLabel_IsWiderThanShortLabel()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Short] --> B[This is a much longer process step name]
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract ShortNode = Contract.Nodes.First(Node => Node.Id == "A");
        NodeContract LongNode = Contract.Nodes.First(Node => Node.Id == "B");

        FlowchartNodeDimensions ShortSize = FlowchartNodeSizeEstimator.Estimate(ShortNode, Options);
        FlowchartNodeDimensions LongSize = FlowchartNodeSizeEstimator.Estimate(LongNode, Options);

        Assert.That(LongSize.Width, Is.GreaterThan(ShortSize.Width));
    }

    [Test]
    public void Estimate_NarrowWrappingWidth_IncreasesHeightForLongText()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            %%{init: {"flowchart": {"wrappingWidth": 60}}}%%
            flowchart TD
            A[This is a long label that should wrap onto multiple lines]
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract Node = Contract.Nodes[0];
        FlowchartNodeDimensions Dimensions = FlowchartNodeSizeEstimator.Estimate(Node, Options);

        Assert.That(Dimensions.DisplayLabel, Does.Contain("\n"));
        Assert.That(Dimensions.Height, Is.GreaterThan(60.0));
    }

    [Test]
    public void Estimate_HtmlTableLabel_UsesNormalisedCellTextNotRawMarkup()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A["<table><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></table>"] --> B[Out]
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract TableNode = Contract.Nodes.First(Node => Node.Id == "A");
        FlowchartNodeDimensions Dimensions = FlowchartNodeSizeEstimator.Estimate(TableNode, Options);

        Assert.That(Dimensions.DisplayLabel, Is.EqualTo("A  B\nC  D"));
        Assert.That(Dimensions.Width, Is.LessThan(140.0));
        Assert.That(Dimensions.Height, Is.GreaterThan(50.0));
        Assert.That(Dimensions.Width / Dimensions.Height, Is.InRange(0.55, 1.75));
    }

    [Test]
    public void Estimate_DiamondShape_UsesSquareDimensions()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Choose{Choose one option}
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract Node = Contract.Nodes[0];
        FlowchartNodeDimensions Dimensions = FlowchartNodeSizeEstimator.Estimate(Node, Options);

        Assert.That(Dimensions.Width, Is.EqualTo(Dimensions.Height).Within(0.01));
    }
}
