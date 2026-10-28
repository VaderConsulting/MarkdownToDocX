using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartSelfLoopLabelRouteRepairTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AlignedLoopExemptionKeepsOrdinaryStraighteningAndOwnBodyChecks(bool Loop)
    {
        var Contract = new DiagramContract();
        Contract.NodesMutable.Add(new() { Id = "a", Shape = "rect" });
        Contract.NodesMutable.Add(new() { Id = "b", Shape = "rect" });
        Contract.EdgesMutable.Add(new() { Id = "line", SourceId = "a", TargetId = Loop ? "a" : "b" });
        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new() { NodeId = "a", CenterX = 0, CenterY = 0, Width = 100, Height = 60 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "b", CenterX = 300, CenterY = 0, Width = 100, Height = 60 });
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = "line", SourceId = "a", TargetId = Loop ? "a" : "b", ExitSide = "right", EntrySide = "left" };
        var Points = Loop ? new[] { (50d, 0d), (0d, 0d), (-50d, 0d) }
            : new[] { (50d, 0d), (70d, 0d), (70d, 80d), (230d, 80d), (230d, 0d), (250d, 0d) };
        foreach (var (X, Y) in Points) Route.Points.Add(new() { X = X, Y = Y });
        Layout.EdgeRoutesMutable.Add(Route);
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.That(Findings.Any(Item => Item.Rule == "4.2"), Is.EqualTo(!Loop));
        if (Loop) Assert.That(Findings.Any(Item => Item.Rule == "10" && Item.Description.Contains("node 'a'")), Is.True);
    }
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
