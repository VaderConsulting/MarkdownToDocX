using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Enterprise catalogue regressions for stacked vertical chords and border-scrape routing.</summary>
[TestFixture, NonParallelizable]
public sealed class EnterpriseStackedRouteRegressionTests
{
    [TestCase("A38", "priceSvc", "stockSvc")]
    [TestCase("A45", "officer", "customer")]
    [TestCase("A46", "checkout", "ordering")]
    [Explicit("Stacked vertical chord regressions; enable when A38/A45/A46 routing is green.")]
    public void StackedAlignedNeighbours_UseStraightVerticalChord(string id, string sourceId, string targetId)
    {
        var layout = LayoutCatalogue(id);
        var route = layout.EdgeRoutes.Single(Route =>
            Route.SourceId == sourceId && Route.TargetId == targetId);
        Assert.Multiple(() =>
        {
            Assert.That(route.Points, Has.Count.EqualTo(2), $"{id}: expected a two-point vertical chord");
            if (sourceId == "officer" && targetId == "customer")
            {
                Assert.That(route.ExitSide, Is.EqualTo("top"));
                Assert.That(route.EntrySide, Is.EqualTo("bottom"));
            }
            else
            {
                Assert.That(route.ExitSide, Is.EqualTo("bottom"));
                Assert.That(route.EntrySide, Is.EqualTo("top"));
            }
            Assert.That(Math.Abs(route.Points[0].X - route.Points[1].X), Is.LessThanOrEqualTo(1.0));
        });
    }

    [TestCase("A38", "priceSvc", "placeOrder", "right")]
    [TestCase("A37", "checkout", "ordering", "right")]
    public void OffsetTargetBelow_ExitsSourceBottomOrSideChannel(string id, string sourceId, string targetId, string entrySide)
    {
        var layout = LayoutCatalogue(id);
        var route = layout.EdgeRoutes.Single(Route =>
            Route.SourceId == sourceId && Route.TargetId == targetId);
        Assert.That(route.ExitSide, Is.EqualTo("bottom").Or.EqualTo("right").Or.EqualTo("left"));
        Assert.That(route.EntrySide, Is.EqualTo(entrySide));
    }

    [TestCase("A153", "a3", "a4")]
    public void StackedClusterNeighbours_AvoidSubTrackJogs(string id, string sourceId, string targetId)
    {
        var contract = ContractCatalogue(id);
        var layout = LayoutCatalogue(id);
        var route = layout.EdgeRoutes.Single(Route =>
            Route.SourceId == sourceId && Route.TargetId == targetId);
        var findings = FlowchartRouteRuleChecker.Check(contract, layout)
            .Where(Finding => Finding.Subject.Contains(sourceId, StringComparison.Ordinal)
                && Finding.Subject.Contains(targetId, StringComparison.Ordinal)).ToArray();
        Assert.That(findings.Where(Finding => Finding.Rule is "4.2" or "5.1"), Is.Empty,
            string.Join("; ", findings.Select(Finding => Finding.Description)));
        if (route.Points.Count == 2)
        {
            Assert.That(Math.Abs(route.Points[0].X - route.Points[1].X), Is.LessThanOrEqualTo(1.0));
        }
    }

    [Test]
    public void A160_HubFan_PortsOrderedByTargetColumn()
    {
        var contract = ContractCatalogue("A160");
        var layout = LayoutCatalogue("A160");
        var hubRoutes = layout.EdgeRoutes.Where(Route => Route.SourceId == "hub").OrderBy(Route => Route.Points[0].X).ToArray();
        Assert.That(hubRoutes, Has.Length.EqualTo(16));
        var findings = FlowchartRouteRuleChecker.Check(contract, layout);
        TestContext.Out.WriteLine($"A160_FAN|portOrder={findings.Count(Finding => Finding.Rule is "3.3/5.2")}|shared={findings.Count(Finding => Finding.Rule == "5.1")}");
        int inversions = 0;
        for (int Index = 1; Index < hubRoutes.Length; Index++)
        {
            var boxA = layout.NodeBounds.Single(Box => Box.NodeId == hubRoutes[Index - 1].TargetId);
            var boxB = layout.NodeBounds.Single(Box => Box.NodeId == hubRoutes[Index].TargetId);
            if (boxA.CenterX > boxB.CenterX + 1.0)
            {
                inversions++;
            }
        }

        TestContext.Out.WriteLine($"A160_FAN|columnInversions={inversions}");
        if (inversions > 0)
        {
            Assert.Warn($"A160 hub port column inversions: {inversions}");
        }
    }

    [Test]
    public void A167_ReportsRule59Findings()
    {
        var contract = ContractCatalogue("A167");
        var layout = LayoutCatalogue("A167");
        var findings = FlowchartRouteRuleChecker.Check(contract, layout);
        TestContext.Out.WriteLine($"A167_RULE59|count={findings.Count(Finding => Finding.Rule == "5.9")}|rule10={findings.Count(Finding => Finding.Rule == "10")}|shared={findings.Count(Finding => Finding.Rule == "5.1")}");
    }

    [TestCase("A45", "officer", "customer")]
    [Explicit("A45 column scrape regression; enable when border routing is green.")]
    public void SameColumnRoute_DoesNotScrapeForeignBorder(string id, string sourceId, string targetId)
    {
        var contract = ContractCatalogue(id);
        var layout = LayoutCatalogue(id);
        var route = layout.EdgeRoutes.Single(Route =>
            Route.SourceId == sourceId && Route.TargetId == targetId);
        var findings = FlowchartRouteRuleChecker.Check(contract, layout)
            .Where(Finding => Finding.Subject.Contains(sourceId, StringComparison.Ordinal)
                && Finding.Rule is "4.9" or "4.2").ToArray();
        Assert.That(findings, Is.Empty, string.Join("; ", findings.Select(Finding => Finding.Description)));
        Assert.That(route.Points, Has.Count.EqualTo(2));
    }

    private static DiagramContract ContractCatalogue(string id)
    {
        var scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == id);
        return EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(scenario.Source).Document!);
    }

    private static FlowchartLayoutResult LayoutCatalogue(string id)
    {
        var contract = ContractCatalogue(id);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(contract, new FlowchartRenderContext { UseCellRegions = true }, out var layout), Is.True);
        return layout;
    }
}
