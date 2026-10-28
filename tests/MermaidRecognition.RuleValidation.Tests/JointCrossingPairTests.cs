using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Replays each newly exposed crossing with the bounded joint search and checks exact rejected-state restoration.</summary>
[TestFixture, NonParallelizable, Explicit("Measures the eleven approved crossing exceptions individually.")]
public sealed class JointCrossingPairTests
{
    /// <summary>Exercises an actual production source with one pair selected for a diagnostic replay.</summary>
    /// <param name="Id">Catalogue source.</param>
    /// <param name="Pair">Exact declared pair identity.</param>
    /// <param name="MaximumBends">Approved scenario bend ceiling.</param>
    /// <param name="MaximumCrossings">Approved geometric crossing ceiling.</param>
    [TestCase("A110", "containerHosting->orderManager:49 x messageQueue->webShop:54", 180, 461)]
    [TestCase("A110", "containerHosting->paymentHub:50 x messageQueue->orderManager:55", 180, 461)]
    [TestCase("A110", "orderManager->stockLookupSvc:38 x orderManager->shippingQuoteSvc:44", 180, 461)]
    [TestCase("A110", "priceLookupSvc->shipOrder:15 x shippingQuoteSvc->cancelOrder:26", 180, 461)]
    [TestCase("A110", "stockLookupSvc->cancelOrder:8 x priceLookupSvc->shipOrder:15", 180, 461)]
    [TestCase("A113", "customers->convert:0 x placeOrder->orderSvc:11", 117, 74)]
    [TestCase("A115", "drSvc->shop:23 x perthData->backup:30", 100, 139)]
    [TestCase("A115", "perthLan->perthApp:2 x shopImg1->shop:24", 100, 139)]
    [TestCase("A117", "g2->t2:6 x dDecom->t2:37", 138, 87)]
    [TestCase("A142", "cadMf->cadT1:24 x hrApiMf->hrApiCon:169", 322, 158)]
    [TestCase("A142", "empowerExt->dmeEtms:36 x cadMf->dmeEtms:165", 322, 158)]
    public void PairTrial_PreservesQualityAndRollsBackExactly(string Id, string Pair, int MaximumBends, int MaximumCrossings)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        string? Saved = null;
        int Rejections = 0;
        DiagramGridLayout.CrossingPairDiagnosticFilter = Key => Key == Pair;
        DiagramGridLayout.CrossingPairCandidateObserver = (Key, Kind, Reason) =>
            TestContext.Out.WriteLine($"PAIR_OPTION|{Id}|{Key}|{Kind}|{Reason}");
        DiagramGridLayout.CrossingPairStateObserver = (Stage, Layout, Nodes) =>
        {
            FlowchartLogicalGrid.Snapshot Cells = FlowchartCellRepair.Capture(Nodes);
            string Signature = JsonSerializer.Serialize(new {
                Model = CellProductionParityTests.Snapshot(Contract, Layout),
                Cells.Regions, Cells.ColumnGaps, Cells.RowGaps
            });
            if (Stage == "before")
            {
                Saved = Signature;
            }

            if (Stage == "rejected") { Assert.That(Signature, Is.EqualTo(Saved)); Rejections++; }
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract,
                new FlowchartRenderContext { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            int Bends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
            int Crossings = 0;
            for (int A = 0; A < Layout.EdgeRoutes.Count; A++)
            {
                for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
                {
                    Crossings += FlowchartBranchRouteOrder.Intersections(
                    Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
                }
            }

            IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
            Assert.That(Bends, Is.LessThanOrEqualTo(MaximumBends));
            Assert.That(Crossings, Is.LessThanOrEqualTo(MaximumCrossings));
            Assert.That(Saved, Is.Not.Null, "The selected production pair must reach the bounded search.");
            Assert.That(Rejections, Is.GreaterThan(0), "Exercise an actual rejected-candidate rollback.");
            Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
            TestContext.Out.WriteLine($"PAIR_RESULT|{Id}|{Pair}|removed={!Findings.Any(Finding => Finding.Rule == "5.9" && Finding.Subject == Pair)}|bends={Bends}|crossings={Crossings}|rejections={Rejections}");
        }
        finally
        {
            DiagramGridLayout.CrossingPairDiagnosticFilter = null;
            DiagramGridLayout.CrossingPairCandidateObserver = null;
            DiagramGridLayout.CrossingPairStateObserver = null;
        }
    }
}
