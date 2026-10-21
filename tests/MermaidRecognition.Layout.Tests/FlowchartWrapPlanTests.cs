using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartWrapPlanTests
{
    [TestCase(160, true)]
    [TestCase(160, false)]
    public void Choose_WrapsWhenSpanExceedsDiagramUsableWidth(int NodeCount, bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TD\n  measured[\"A label long enough to determine the shared box\"]");
        (int NodeWidth, int NodeHeight) = DiagramGridLayout.ResolveStandardBox(Contract);
        int ColumnGap = DiagramGridMetrics.NODE_GAP_PX;
        int RankGap = Math.Max(DiagramGridMetrics.NODE_GAP_PX, DiagramGridMetrics.MIN_LR_RANK_GAP_PX);

        FlowchartWrapPlan.Plan Actual = FlowchartWrapPlan.Choose(
            NodeCount, NodeWidth, NodeHeight, ColumnGap, RankGap, Horizontal);

        double Usable = FlowchartLayoutRules.DiagramUsableWidthPx;
        Assert.Multiple(() =>
        {
            Assert.That(Actual.Columns, Is.GreaterThan(0));
            Assert.That(Actual.Rows, Is.GreaterThan(0));
            Assert.That(Actual.Columns * Actual.Rows, Is.GreaterThanOrEqualTo(NodeCount));
            if (Horizontal)
            {
                Assert.That(Actual.Width, Is.LessThanOrEqualTo(Usable + 1e-9));
            }
            else
            {
                Assert.That(Actual.Height, Is.LessThanOrEqualTo(Usable + 1e-9));
            }
        });
    }

    [Test]
    public void Choose_SingleRowWhenFitsWithinUsableWidth()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\n  a --> b --> c");
        (int NodeWidth, int NodeHeight) = DiagramGridLayout.ResolveStandardBox(Contract);
        int ColumnGap = DiagramGridMetrics.NODE_GAP_PX;
        int RankGap = DiagramGridMetrics.NODE_GAP_PX;
        FlowchartWrapPlan.Plan Actual = FlowchartWrapPlan.Choose(3, NodeWidth, NodeHeight, ColumnGap, RankGap, true);
        Assert.That(Actual.Rows, Is.EqualTo(1));
        Assert.That(Actual.Columns, Is.EqualTo(3));
    }
}
