using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridRankGapTests
{
    private const string LoadBalanceSource = """
        flowchart TD
            Q[Queue] --> L{Load balance}
            L --> C1[Instance 1]
            L --> C2[Instance 2]
            L --> C3[Instance 3]
            C1 --> DL[Shared DLQ policy]
            C2 --> DL
            C3 --> DL
        """;

    private const string PurgeRequestSource = """
        flowchart TD
            P[Purge request] --> V{Authorised + scoped?}
            V -->|no| X[Reject / audit alert]
            V -->|yes| E[Export optional archive]
            E --> R[Remove messages]
            R --> L[Log counts + ticket ref]
        """;

    [Test]
    public void FanAndMerge_GrowOnlyTheGapsThatNeedOffsetRoutes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(LoadBalanceSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        List<double> ClearGaps = MeasureClearGaps(Layout);
        Assert.That(ClearGaps, Has.Count.EqualTo(3));
        Assert.That(ClearGaps[0], Is.EqualTo(60).Within(1.0));
        Assert.That(ClearGaps[1], Is.GreaterThanOrEqualTo(ClearGaps[0]));
        Assert.That(ClearGaps[1], Is.EqualTo(ClearGaps[2]).Within(1.0));
        Assert.That(ClearGaps[2], Is.GreaterThan(DiagramGridMetrics.NODE_GAP_PX));
    }

    [Test]
    public void MixedShapes_MeasureClearGapFromTheTallestNodePerRank()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(LoadBalanceSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Diamond = Find(Layout, "L");
        FlowchartLayoutNodeBounds Queue = Find(Layout, "Q");
        double DiamondBottom = Diamond.CenterY - (Diamond.Height / 2.0);
        double QueueBottom = Queue.CenterY - (Queue.Height / 2.0);
        Assert.That(DiamondBottom, Is.LessThan(QueueBottom));

        List<double> ClearGaps = MeasureClearGaps(Layout);
        double QueueToDiamondGap = QueueBottom - (Diamond.CenterY + (Diamond.Height / 2.0));
        Assert.That(QueueToDiamondGap, Is.EqualTo(ClearGaps[0]).Within(1.0));
    }

    [Test]
    public void FanAndMerge_RenderEveryDeclaredConnection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(LoadBalanceSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg, Does.Contain("data-edge-source=\"L\" data-edge-target=\"C1\""));
        Assert.That(Svg, Does.Contain("data-edge-source=\"C1\" data-edge-target=\"DL\""));
    }

    [Test]
    public void BinaryDecisionWithSideChain_PreservesBaseGapsOutsideTheBranch()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(PurgeRequestSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        List<double> ClearGaps = MeasureClearGaps(Layout);
        Assert.That(ClearGaps, Has.Count.EqualTo(4));
        foreach (double Gap in ClearGaps)
        {
            Assert.That(Gap, Is.GreaterThanOrEqualTo(60));
            Assert.That(Gap % DiagramGridMetrics.GRID_UNIT_PX, Is.EqualTo(0).Within(1.0));
        }

        Assert.That(ClearGaps[0], Is.GreaterThan(DiagramGridMetrics.NODE_GAP_PX));
    }

    [Test]
    public void BinaryDecisionWithSideChain_ReservesExtraSpaceOnlyAtTheBranch()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(PurgeRequestSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Export = Find(Layout, "E");
        FlowchartLayoutNodeBounds Remove = Find(Layout, "R");
        FlowchartLayoutNodeBounds Log = Find(Layout, "L");
        FlowchartLayoutNodeBounds Decision = Find(Layout, "V");
        FlowchartLayoutNodeBounds Reject = Find(Layout, "X");

        double ExportToRemoveGap = BottomOf(Export) - TopOf(Remove);
        double RemoveToLogGap = BottomOf(Remove) - TopOf(Log);
        double DecisionToBranchGap = BottomOf(Decision) - TopOf(Reject);

        Assert.That(ExportToRemoveGap, Is.EqualTo(60).Within(1.0));
        Assert.That(DecisionToBranchGap, Is.GreaterThanOrEqualTo(ExportToRemoveGap));
        Assert.That(RemoveToLogGap, Is.EqualTo(60).Within(1.0));
    }

    private static double BottomOf(FlowchartLayoutNodeBounds Bounds) =>
        Bounds.CenterY - (Bounds.Height / 2.0);

    private static double TopOf(FlowchartLayoutNodeBounds Bounds) =>
        Bounds.CenterY + (Bounds.Height / 2.0);

    private static List<double> MeasureClearGaps(FlowchartLayoutResult Layout)
    {
        List<FlowchartLayoutNodeBounds> RankGroups = Layout.NodeBounds
            .GroupBy(Bounds => Bounds.CenterY)
            .OrderByDescending(Group => Group.Key)
            .Select(Group => Group.OrderByDescending(Bounds => Bounds.Height).First())
            .ToList();

        List<double> ClearGaps = [];
        for (int Index = 0; Index < RankGroups.Count - 1; Index++)
        {
            double BottomOfUpper = RankGroups[Index].CenterY - (RankGroups[Index].Height / 2.0);
            double TopOfLower = RankGroups[Index + 1].CenterY + (RankGroups[Index + 1].Height / 2.0);
            ClearGaps.Add(BottomOfUpper - TopOfLower);
        }

        return ClearGaps;
    }

    private static FlowchartLayoutNodeBounds Find(FlowchartLayoutResult Layout, string Id)
    {
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            if (string.Equals(Bounds.NodeId, Id, StringComparison.Ordinal))
            {
                return Bounds;
            }
        }

        Assert.Fail("Missing node " + Id);
        return new FlowchartLayoutNodeBounds();
    }
}
