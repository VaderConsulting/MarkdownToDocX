using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartSelfLoopLabelRouteRepairTests
{
    [Test]
    public void LabelledSelfLoop_ChoosesABoxSideWithMeasuredLabelRoom()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\nqueue[Retry Queue]\nqueue -->|retry| queue");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single();
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartLabelDimensions Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            "retry", Options, Options.EdgeLabelFontSizePx);
        double HorizontalRequired = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
            Size, Options.EdgeLabelFontSizePx, true) + (2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        double VerticalRequired = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
            Size, Options.EdgeLabelFontSizePx, false) + (2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);

        Assert.That(Route.Points.Zip(Route.Points.Skip(1)).Any(Pair =>
            Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.01
                ? Math.Abs(Pair.First.X - Pair.Second.X) >= HorizontalRequired - 0.01
                : Math.Abs(Pair.First.Y - Pair.Second.Y) >= VerticalRequired - 0.01), Is.True);
    }
}
