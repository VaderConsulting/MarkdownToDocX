using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F112NestedSubgraphGapTests
{
    private const string F112_SOURCE = """
        flowchart TB
        subgraph L0
          subgraph L1
            subgraph L2
              subgraph L3
                subgraph L4
                  A[A] --> B[B]
                end
              end
            end
          end
        end
        """;

    [Test]
    public void TryLayout_F112_EachNestedSubgraphPairRespectsMinimumExteriorGap()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F112_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        FlowchartSubgraphBoundsRequest BoundsRequest =
            FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        List<FlowchartSubgraphLayoutRect> SubgraphRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectById =
            SubgraphRects.ToDictionary(Rect => Rect.GroupId, StringComparer.Ordinal);

        double MinimumCrossAxisGap = FlowchartSubgraphGapThresholds.ResolveNestedCrossAxisGapPx();
        double MinimumFlowAxisGap = FlowchartSubgraphGapThresholds.ResolveNestedFlowAxisGapPx(Options);
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        (string ParentId, string ChildId)[] NestedPairs =
        [
            ("L0", "L1"),
            ("L1", "L2"),
            ("L2", "L3"),
            ("L3", "L4")
        ];

        Assert.That(
            MinimumCrossAxisGap,
            Is.EqualTo(FlowchartSubgraphBoundsBuilder.NESTED_CLUSTER_GAP_PX),
            "Cross-flow nested exterior gap is fixed at 24px");
        Assert.That(
            MinimumFlowAxisGap,
            Is.EqualTo(Math.Max(FlowchartSubgraphBoundsBuilder.NESTED_CLUSTER_GAP_PX, Options.RankSpacingPx)),
            "Flow-axis nested exterior gap uses max(24px, rankSpacing)");

        foreach ((string ParentId, string ChildId) in NestedPairs)
        {
            FlowchartSubgraphLayoutRect ParentRect = RectById[ParentId];
            FlowchartSubgraphLayoutRect ChildRect = RectById[ChildId];

            double LeftGap = ChildRect.Left - ParentRect.Left;
            double RightGap = ParentRect.Right - ChildRect.Right;
            double BottomGap = ChildRect.Bottom - ParentRect.Bottom;
            double TopGap = ParentRect.Top - ChildRect.Top;

            if (HorizontalFlow)
            {
                Assert.That(LeftGap, Is.GreaterThanOrEqualTo(MinimumFlowAxisGap - 0.5), $"{ChildId} left gap inside {ParentId}");
                Assert.That(RightGap, Is.GreaterThanOrEqualTo(MinimumFlowAxisGap - 0.5), $"{ChildId} right gap inside {ParentId}");
                Assert.That(BottomGap, Is.GreaterThanOrEqualTo(MinimumCrossAxisGap - 0.5), $"{ChildId} bottom gap inside {ParentId}");
                Assert.That(TopGap, Is.GreaterThanOrEqualTo(MinimumCrossAxisGap - 0.5), $"{ChildId} top gap inside {ParentId}");
            }
            else
            {
                Assert.That(LeftGap, Is.GreaterThanOrEqualTo(MinimumCrossAxisGap - 0.5), $"{ChildId} left gap inside {ParentId}");
                Assert.That(RightGap, Is.GreaterThanOrEqualTo(MinimumCrossAxisGap - 0.5), $"{ChildId} right gap inside {ParentId}");
                Assert.That(BottomGap, Is.GreaterThanOrEqualTo(MinimumFlowAxisGap - 0.5), $"{ChildId} bottom gap inside {ParentId}");
                Assert.That(TopGap, Is.GreaterThanOrEqualTo(MinimumFlowAxisGap - 0.5), $"{ChildId} top gap inside {ParentId}");
            }
        }
    }

    [Test]
    public void RenderToSvg_F112_RendersAllNestedClusterBoxes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F112_SOURCE);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Contain("L0"));
        Assert.That(Svg, Does.Contain("L4"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(Svg, "fill=\"#ffffde\"").Count, Is.GreaterThanOrEqualTo(5));
    }
}
