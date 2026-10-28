using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Replays the nested-frame assignment that previously mistook enclosing frames for solid obstacles.</summary>
[TestFixture, NonParallelizable]
public sealed class EnclosingFrameFallbackTests
{
    /// <summary>Checks that complete routing preserves every declared line through the captured nested-frame arrangement.</summary>
    [Test]
    public void NestedFrameCandidateKeepsEveryAllocatedRouteLegal()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A115");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        bool Observed = false;
        DiagramGridLayout.FinalRepairRegionForTests = Nodes => new Dictionary<string, FlowchartCellRegion>
        {
                ["perthLb1"] = new(6, 3, 1, 2),
                ["perthLb2"] = new(7, 3, 1, 2),
                ["httpsIn"] = new(8, 3, 1, 2),
                ["shopImg1"] = new(8, 9, 1, 2),
                ["shopImg2"] = new(8, 15, 1, 2),
                ["perthOrders"] = new(7, 22, 1, 2),
                ["perthMq"] = new(6, 25, 1, 2),
                ["shopImg3"] = new(7, 34, 1, 2),
                ["sydneyOrders"] = new(7, 40, 1, 2),
                ["perthLan"] = new(3, 50, 1, 2),
                ["sydneyLan"] = new(4, 50, 1, 2),
                ["internet"] = new(7, 50, 1, 2),
                ["wan"] = new(6, 50, 1, 2),
                ["replication"] = new(5, 50, 1, 2),
                ["lbSvc"] = new(10, 48, 1, 2),
                ["containerSvc"] = new(6, 48, 1, 2),
                ["dbSvc"] = new(8, 48, 1, 2),
                ["mqSvc"] = new(9, 48, 1, 2),
                ["drSvc"] = new(4, 48, 1, 2),
                ["failover"] = new(3, 48, 1, 2),
                ["backup"] = new(5, 48, 1, 2),
                ["backupDue"] = new(7, 48, 1, 2),
                ["backupSet"] = new(3, 46, 1, 2),
                ["adminLaptop"] = new(8, 50, 1, 2),
                ["shop"] = new(0, 46, 1, 2),
                ["orderMgr"] = new(1, 46, 1, 2),
        };
        DiagramGridLayout.FinalRepairMeasurementObserver = (Key, Layout) =>
        {
            if (Key != "diagnostic-region")
            {
                return;
            }

            Observed = true;
            Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(Contract.Edges.Count));
            Assert.That(Layout.EdgeRoutes.Where(Route => Route.Points.Count < 2).Select(Route => Route.EdgeId), Is.Empty);
            Assert.That(Layout.RenderContext.LayoutStageViolations.Where(Item => Item.Kind == "NoLegalRoute"), Is.Empty);
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
            foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
            {
                Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route,
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()), Is.True, Route.EdgeId);
            }
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out _), Is.True);
            Assert.That(Observed, Is.True, "The captured assignment must reach full routing confirmation.");
        }
        finally
        {
            DiagramGridLayout.FinalRepairRegionForTests = null;
            DiagramGridLayout.FinalRepairMeasurementObserver = null;
        }
    }
}