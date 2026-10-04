using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class F91MeasurementTests
{
    const string MIXED_LABEL = "**Bold** and <i>italic</i> and `code`";

    [Test]
    public void StripRichMarkupToPlainText_MixedMarkdownHtml_ProducesVisibleText()
    {
        Assert.That(
            FlowchartRichLabelSvgWriter.StripRichMarkupToPlainText(MIXED_LABEL),
            Is.EqualTo("Bold and italic and code"));
    }

    [Test]
    public void MeasureWrappedLabel_MixedMarkdownHtml_FitsStyledSingleLineWidth()
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"""
            flowchart LR
            A["{MIXED_LABEL}"] --> B[Next]
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        NodeContract NodeA = Contract.Nodes[0];
        FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(NodeA.Label!, Options);
        FlowchartNodeDimensions NodeDimensions = FlowchartNodeSizeEstimator.Estimate(NodeA, Options);
        double StyledWidth = FlowchartRichLabelSvgWriter.EstimateStyledLabelMaxWidthPx(MIXED_LABEL, Options.FontSizePx);

        Assert.That(LabelDimensions.DisplayText, Is.EqualTo("Bold and italic and code"));
        Assert.That(LabelDimensions.Width, Is.GreaterThanOrEqualTo(StyledWidth - 1.0));
        Assert.That(NodeDimensions.Width, Is.GreaterThanOrEqualTo(StyledWidth));
    }
}
