using System.Linq;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Priority legality catalogue regressions (node clip, frame endpoints, straddle, shared tracks).</summary>
[TestFixture]
[NonParallelizable]
public sealed class PriorityLegalityRegressionTests
{
    /// <summary>Runs production layout and asserts core route legality gates for a catalogue scenario.</summary>
    /// <param name="Id">Catalogue identity.</param>
    [TestCase("A112")]
    [TestCase("A33")]
    [TestCase("F45")]
    [TestCase("F62")]
    public void ProductionLayout_HasCoreRouteLegality(string Id)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue()
            .Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!)
            .Single(Case => Case.Id == Id);
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
        {
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
            Assert.Multiple(() =>
            {
                Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "5.1"), Is.Empty,
                    string.Join("; ", Findings.Where(Finding => Finding.Rule is "10" or "5.1")
                        .Select(Finding => Finding.Rule + " " + Finding.Subject)));
                Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
                Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
                Assert.That(Layout.RenderContext.LayoutStageViolations
                    .Where(Finding => Finding.Kind is "NoLegalRoute" or "TitleBandRoute"), Is.Empty);
            });
        });
    }
}
