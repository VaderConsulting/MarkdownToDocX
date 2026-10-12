using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class SameRankRelationshipLabelGapTests
{
    [Test]
    public void TerminalSameRankTarget_MovesByMeasuredLabelRoom()
    {
        const string Source = """
            enterprise
              group hr "Human Resources"
              business-process payRun "Run Payroll" in hr
              business-object payslip "Payslip" in hr
              business-object rules "Payroll rules"
              payRun -access(write)-> payslip "writes<br/>one per employee"
              payRun -access(read)-> rules "reads<br/>at the start"
            """;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(
            EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        EdgeContract Edge = Contract.Edges.Single(Edge => Edge.TargetId == "payslip");
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Route => Route.EdgeId == Edge.Id);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            Edge.Label!, Options, Options.EdgeLabelFontSizePx);

        Assert.That(FlowchartEdgeLabelGapHelper.TryResolveLabelSegmentBreak(
            Route.Points.Select(Point => (Point.X, Point.Y)).ToList(),
            Dimensions,
            Options.EdgeLabelFontSizePx,
            out _), Is.True);
    }
}
