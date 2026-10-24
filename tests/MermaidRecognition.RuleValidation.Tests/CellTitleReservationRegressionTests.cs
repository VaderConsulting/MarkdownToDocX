using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks that measured title space survives complete routing as derived cell geometry, including the old late-growth failures.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellTitleReservationRegressionTests
{
    /// <summary>A long title is fitted before routing; both one-member frames retain exactly their fitted extents through final repair.</summary>
    [Test]
    public void LongTitleStaysLegalWithoutAnyLateResize()
    {
        var Scenario = new RouteRuleCatalogueAuditTests.Scenario("long-frame-title", """
            flowchart LR
              subgraph G["Operations and governance with a deliberately long measured frame title"]
                A[Input]
              end
              subgraph H[Neighbour]
                B[Output]
              end
              A --> B
            """, false);
        Dictionary<string, (double Width, double Height)>? Fitted = null;
        DiagramContract? CompletedContract = null;
        FlowchartLayoutResult? Completed = null;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
        {
            CompletedContract = Contract; Completed = Layout;
            Assert.That(Fitted, Is.Not.Null);
            foreach (var Frame in Layout.ClusterBounds)
                Assert.That((Frame.Width, Frame.Height), Is.EqualTo(Fitted![Frame.NodeId]));
        }, Nodes => CheckFinalModel(CompletedContract!, Completed!, Nodes), PrepareBeforeRouting: Nodes =>
        {
            var Frames = DiagramGridLayout.ProjectCellFrames(MermaidRecognition.Core.MermaidRecognizer.Parse(Scenario.Source),
                Nodes, Nodes[0].CellNode!.Grid.Capture());
            Fitted = Frames.ToDictionary(Frame => Frame.Id, Frame => ((double)(Frame.Right - Frame.Left), (double)Math.Abs(Frame.Bottom - Frame.Top)));
            Assert.That(Fitted["G"].Width, Is.GreaterThan(Nodes.Single(Node => Node.Id == "A").Width));
        });
    }

    /// <summary>The original nested and empty-group sources cannot retain stale pixel growth after title-route recovery.</summary>
    /// <param name="Id">Unchanged previously affected catalogue scenario.</param>
    [TestCase("F45")]
    [TestCase("A58")]
    [TestCase("A138")]
    [TestCase("A167")]
    [TestCase("A168")]
    [TestCase("A169")]
    [TestCase("EIP84")]
    public void PreviousLateGrowthKeepsTheDerivedFrame(string Id)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        DiagramContract? CompletedContract = null;
        FlowchartLayoutResult? Completed = null;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true,
            (Contract, Layout) => { CompletedContract = Contract; Completed = Layout; },
            Nodes => CheckFinalModel(CompletedContract!, Completed!, Nodes));
    }

    /// <summary>Uses independent final output gates and the forward projection from actual regions, so a resized output frame cannot pass.</summary>
    /// <param name="Contract">Completed source with original memberships.</param>
    /// <param name="Layout">Final routing and frame output.</param>
    /// <param name="Nodes">Actual cell ownership after every repair.</param>
    private static void CheckFinalModel(DiagramContract Contract, FlowchartLayoutResult Layout,
        IReadOnlyList<DiagramGridLayout.GridNode> Nodes)
    {
        Assert.Multiple(() =>
        {
            Assert.That(CellGridAudit.Faults(Contract, Layout, Nodes), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule == "10"), Is.Empty);
            Assert.That(Layout.RenderContext.LayoutStageViolations.Where(Finding => Finding.Kind is "NoLegalRoute" or "TitleBandRoute"), Is.Empty);
        });
    }
}
