using System.Text;

using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

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
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract,
            new FlowchartRenderContext { UseCellRegions = true }, out FlowchartLayoutResult Layout), Is.True);

        FlowchartCellNode Cell = Layout.CellNodes![0].CellNode!;
        FlowchartLogicalGridConfiguration Configuration = Cell.Grid.Configuration;
        var Size = Cell.Grid.Measure(Cell.Region);
        Assert.That(Layout.CellNodes.All(Node => Node.CellNode!.Region.RowSpan == Cell.Region.RowSpan
            && Node.CellNode.Region.ColumnSpan == Cell.Region.ColumnSpan), Is.True);
        FlowchartWrapPlan.Plan Expected = FlowchartWrapPlan.Choose(
            NodeCount, (int)Size.Width, (int)Size.Height,
            (int)Configuration.ColumnGap, (int)Configuration.RowGap, Direction == "LR");
        double Width = Layout.NodeBounds.Max(Node => Node.CenterX + Node.Width / 2.0)
            - Layout.NodeBounds.Min(Node => Node.CenterX - Node.Width / 2.0);
        double Height = Layout.NodeBounds.Max(Node => Node.CenterY + Node.Height / 2.0)
            - Layout.NodeBounds.Min(Node => Node.CenterY - Node.Height / 2.0);

        Assert.Multiple(() =>
        {
            Assert.That(Width / Configuration.CellWidth,
                Is.EqualTo(Expected.Width / Configuration.CellWidth).Within(DiagramGridMetrics.GRID_UNIT_PX / Configuration.CellWidth));
            Assert.That(Height / Configuration.CellHeight,
                Is.EqualTo(Expected.Height / Configuration.CellHeight).Within(DiagramGridMetrics.GRID_UNIT_PX / Configuration.CellHeight));
            Assert.That(HasOverlap(Layout), Is.False);
        });
    }

    [TestCase("LR")]
    [TestCase("TD")]
    public void Grid_LabelledLongChainReservesMeasuredRoomInEitherWrapDirection(string Direction)
    {
        const int NodeCount = 40;
        var Source = new StringBuilder($"flowchart {Direction}\n");
        for (int Index = 1; Index < NodeCount; Index++)
            Source.AppendLine($"  n{Index}[Node {Index}] -->|measured label| n{Index + 1}[Node {Index + 1}]");
        var Contract = MermaidRecognizer.Parse(Source.ToString());
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartLabelDimensions Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            "measured label", Options, Options.EdgeLabelFontSizePx);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            double LongestRun = Route.Points.Zip(Route.Points.Skip(1))
                .Max(Pair => Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y));
            bool Horizontal = Route.Points.Zip(Route.Points.Skip(1))
                .OrderByDescending(Pair => Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y))
                .Select(Pair => Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.01).First();
            double Required = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                Size, Options.EdgeLabelFontSizePx, Horizontal) + DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            Assert.That(LongestRun, Is.GreaterThanOrEqualTo(Required - 0.01), Route.EdgeId);
        }
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
