using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class F93LayoutMeasurementTests
{
    [Test]
    public void Estimate_F93MarkdownList_ProducesSquareishNode()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["- Item 1<br/>- Item 2<br/>- Item 3"] --> B[Next]
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract ListNode = Contract.Nodes[0];
        FlowchartNodeDimensions Dimensions = FlowchartNodeSizeEstimator.Estimate(ListNode, Options);

        TestContext.WriteLine($"Size: {Dimensions.Width} x {Dimensions.Height}");
        TestContext.WriteLine($"Display: [{Dimensions.DisplayLabel.Replace("\n", "\\n")}]");

        Assert.That(Dimensions.Width, Is.EqualTo(Dimensions.Height).Within(0.5));
    }
}
