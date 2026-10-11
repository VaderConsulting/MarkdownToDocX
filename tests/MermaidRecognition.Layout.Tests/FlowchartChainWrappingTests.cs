using System.Text;

using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartChainWrappingTests
{
    [TestCase("LR")]
    [TestCase("TD")]
    public void Grid_LongChainWrapsNearFourByThreeWithoutOverlap(string Direction)
    {
        const int NodeCount = 160;
        var Source = new StringBuilder($"flowchart {Direction}\n");
        for (int Index = 1; Index < NodeCount; Index++)
            Source.AppendLine($"  n{Index}[Node {Index}] --> n{Index + 1}[Node {Index + 1}]");
        var Contract = MermaidRecognizer.Parse(Source.ToString());
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        (int NodeWidth, int NodeHeight) = DiagramGridLayout.ResolveStandardBox(Contract);
        int ColumnGap = Direction == "LR"
            ? Math.Max(DiagramGridMetrics.NODE_GAP_PX, DiagramGridMetrics.MIN_LR_RANK_GAP_PX)
            : DiagramGridMetrics.NODE_GAP_PX;
        FlowchartWrapPlan.Plan Expected = FlowchartWrapPlan.Choose(
            NodeCount, NodeWidth, NodeHeight, ColumnGap, DiagramGridMetrics.NODE_GAP_PX, Direction == "LR");
        double Width = Layout.NodeBounds.Max(Node => Node.CenterX + Node.Width / 2.0)
            - Layout.NodeBounds.Min(Node => Node.CenterX - Node.Width / 2.0);
        double Height = Layout.NodeBounds.Max(Node => Node.CenterY + Node.Height / 2.0)
            - Layout.NodeBounds.Min(Node => Node.CenterY - Node.Height / 2.0);

        Assert.Multiple(() =>
        {
            Assert.That(Width, Is.EqualTo(Expected.Width).Within(DiagramGridMetrics.GRID_UNIT_PX));
            Assert.That(Height, Is.EqualTo(Expected.Height).Within(DiagramGridMetrics.GRID_UNIT_PX));
            Assert.That(HasOverlap(Layout), Is.False);
        });
    }

    private static bool HasOverlap(FlowchartLayoutResult Layout)
    {
        for (int Left = 0; Left < Layout.NodeBounds.Count; Left++)
        for (int Right = Left + 1; Right < Layout.NodeBounds.Count; Right++)
        {
            FlowchartLayoutNodeBounds A = Layout.NodeBounds[Left];
            FlowchartLayoutNodeBounds B = Layout.NodeBounds[Right];
            if (Math.Abs(A.CenterX - B.CenterX) < (A.Width + B.Width) / 2.0
                && Math.Abs(A.CenterY - B.CenterY) < (A.Height + B.Height) / 2.0)
                return true;
        }

        return false;
    }
}
