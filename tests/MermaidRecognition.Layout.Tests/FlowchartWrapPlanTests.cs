using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartWrapPlanTests
{
    [TestCase(160, true)]
    [TestCase(160, false)]
    [TestCase(503, true)]
    [TestCase(503, false)]
    public void Choose_UsesMeasuredSizesAndProducesClosestFourByThreePlan(int NodeCount, bool Horizontal)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TD\n  measured[\"A label long enough to determine the shared box\"]");
        (int NodeWidth, int NodeHeight) = DiagramGridLayout.ResolveStandardBox(Contract);
        int ColumnGap = DiagramGridMetrics.NODE_GAP_PX;
        int RankGap = Math.Max(DiagramGridMetrics.NODE_GAP_PX, DiagramGridMetrics.MIN_LR_RANK_GAP_PX);

        FlowchartWrapPlan.Plan Actual = FlowchartWrapPlan.Choose(
            NodeCount, NodeWidth, NodeHeight, ColumnGap, RankGap, Horizontal);

        Assert.Multiple(() =>
        {
            Assert.That(Actual.Columns, Is.GreaterThan(0));
            Assert.That(Actual.Rows, Is.GreaterThan(0));
            Assert.That(Actual.Columns * Actual.Rows, Is.GreaterThanOrEqualTo(NodeCount));
            Assert.That(Horizontal ? Actual.Columns : Actual.Rows,
                Is.GreaterThanOrEqualTo(Math.Min(NodeCount, FlowchartWrapPlan.MinimumRun)));
        });

        double ActualDistance = Distance(Actual.Width, Actual.Height);
        int FirstRun = Math.Min(NodeCount, FlowchartWrapPlan.MinimumRun);
        for (int Run = FirstRun; Run <= NodeCount; Run++)
        {
            int Columns = Horizontal ? Run : (NodeCount + Run - 1) / Run;
            int Rows = (NodeCount + Columns - 1) / Columns;
            int Width = Columns * NodeWidth + Math.Max(0, Columns - 1) * ColumnGap;
            int Height = Rows * NodeHeight + Math.Max(0, Rows - 1) * RankGap;
            Assert.That(ActualDistance, Is.LessThanOrEqualTo(Distance(Width, Height) + 1e-9));
        }
    }

    private static double Distance(int Width, int Height) => Math.Abs(Math.Log((Width / (double)Height) / (4.0 / 3.0)));
}
