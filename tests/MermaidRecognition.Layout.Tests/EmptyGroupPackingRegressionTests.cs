using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Exercises the real parser and layout path that previously drifted forever while packing an empty group beside hinted members.</summary>
[TestFixture]
[NonParallelizable]
public sealed class EmptyGroupPackingRegressionTests
{
    /// <summary>Retains a hinted empty frame's exact gap instead of replacing it with the automatic main-axis clearance.</summary>
    [Test]
    [CancelAfter(10000)]
    public void HintedEmptyFrameRetainsItsGap()
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze("""
            enterprise LR
            group box "Box"
            application-component peer "Peer" in box
            group empty "Empty" right-of box
            """);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Analysis.Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds Peer = Layout.ClusterBounds.Single(Box => Box.NodeId == "box");
        FlowchartLayoutNodeBounds Empty = Layout.ClusterBounds.Single(Box => Box.NodeId == "empty");
        Assert.That(Empty.CenterX - (Empty.Width / 2) - Peer.CenterX - (Peer.Width / 2),
            Is.EqualTo(DiagramGridMetrics.NODE_GAP_PX));
        Assert.That(Contract.Groups.Single(Group => Group.Id == "empty").PlacementHint!.IsActive, Is.True);
    }

    /// <summary>Settles empty and populated groups using the same spacing rule without breaking the child hint or route legality.</summary>
    /// <param name="Direction">One of the four public flow directions.</param>
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    [CancelAfter(10000)]
    public void EmptyGroupBesideNestedHintedMembersSettles(string Direction)
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze($"""
            enterprise
            direction {Direction}
            group outer "Outer title"
            group inner "Inner title" in outer
            group empty "Empty title"
            application-component a "A" in inner
            application-component b "B" in inner right-of a
            application-component c "C" in outer
            application-component d "D"
            a -flow-> c
            b -flow-> d
            """);
        Assert.That(Analysis.Document, Is.Not.Null);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Analysis.Document!);
        Action<string, double>? Previous = DiagramGridLayout.LayoutStageTimingObserver;
        // Observe cancellation at the actual iterative frame-fit boundary, so
        // a recurrence fails this test rather than leaving a runaway worker.
        DiagramGridLayout.LayoutStageTimingObserver = (_, _) =>
            TestContext.CurrentContext.CancellationToken.ThrowIfCancellationRequested();
        FlowchartLayoutResult Layout;
        try { Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out Layout!), Is.True); }
        finally { DiagramGridLayout.LayoutStageTimingObserver = Previous; }
        FlowchartLayoutNodeBounds A = Layout.NodeBounds.Single(Box => Box.NodeId == "a");
        FlowchartLayoutNodeBounds B = Layout.NodeBounds.Single(Box => Box.NodeId == "b");
        Assert.That(B.CenterY, Is.EqualTo(A.CenterY));
        Assert.That(B.CenterX - A.CenterX, Is.EqualTo(((A.Width + B.Width) / 2) + DiagramGridMetrics.NODE_GAP_PX));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "b").PlacementHint!.IsActive, Is.True);
        Assert.That(Layout.ClusterBounds, Has.Count.EqualTo(3));
        Assert.That(Layout.ClusterBounds.All(Box => double.IsFinite(Box.Width) && Box.Width > 0
            && double.IsFinite(Box.Height) && Box.Height > 0), Is.True);
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
    }
}
