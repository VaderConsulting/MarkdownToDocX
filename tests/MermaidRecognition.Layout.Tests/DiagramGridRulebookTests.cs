using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Rulebook section 1 spacing and section 10 must-never checks.
/// </summary>
[TestFixture]
public sealed class DiagramGridRulebookTests
{
    [Test]
    public void Metrics_BaseRankGap_IsTwoGridSteps()
    {
        Assert.That(DiagramGridMetrics.BASE_RANK_GAP_PX, Is.EqualTo(2 * DiagramGridMetrics.GRID_UNIT_PX));
        Assert.That(DiagramGridMetrics.PitchMainRank(DiagramGridMetrics.NODE_HEIGHT_PX), Is.EqualTo(
            DiagramGridMetrics.NODE_HEIGHT_PX + DiagramGridMetrics.BASE_RANK_GAP_PX));
    }

    [Test]
    public void Metrics_MinStraightRun_MatchesCornerRadiusPlusGridStep()
    {
        Assert.That(DiagramGridMetrics.MIN_STRAIGHT_RUN_PX, Is.EqualTo(50));
    }

    [Test]
    public void TryLayout_Chain_HasNoGridQualityViolations()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[In] --> B[Middle] --> C[Out]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        List<FlowchartLayoutQualityFailure> Violations = [];
        DiagramGridQuality.Collect(Contract, Layout, Violations);
        Assert.That(Violations, Is.Empty, string.Join("; ", Violations.ConvertAll(Failure => Failure.Description)));
    }

    [Test]
    public void TryLayout_GroupedTwoByTwo_CrossedEdgesAreOrthogonal()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A & B --> C & D
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        List<FlowchartLayoutQualityFailure> Violations = [];
        DiagramGridQuality.Collect(Contract, Layout, Violations);
        List<FlowchartLayoutQualityFailure> Diagonals = Violations.FindAll(
            Failure => string.Equals(Failure.Kind, "LineShape", StringComparison.Ordinal)
                && Failure.Description.Contains("diagonal", StringComparison.Ordinal));
        Assert.That(Diagonals, Is.Empty, string.Join("; ", Diagonals.ConvertAll(Failure => Failure.Description)));
    }

    [Test]
    public void TryLayout_PairedCrossLinks_PlaceChildrenUnderParents()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A & B --> C & D
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutNodeBounds A = Find(Layout, "A");
        FlowchartLayoutNodeBounds B = Find(Layout, "B");
        FlowchartLayoutNodeBounds C = Find(Layout, "C");
        FlowchartLayoutNodeBounds D = Find(Layout, "D");
        Assert.That(C.CenterX, Is.EqualTo(A.CenterX).Within(1.0));
        Assert.That(D.CenterX, Is.EqualTo(B.CenterX).Within(1.0));
    }

    private static FlowchartLayoutNodeBounds Find(FlowchartLayoutResult Layout, string NodeId)
    {
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            if (string.Equals(Bounds.NodeId, NodeId, StringComparison.Ordinal))
            {
                return Bounds;
            }
        }

        throw new InvalidOperationException("Node not found: " + NodeId);
    }
}
