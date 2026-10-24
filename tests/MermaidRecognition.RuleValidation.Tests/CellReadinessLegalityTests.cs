using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks the real final legality and slot gates for the late cell-model failures, not merely pipeline completion.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellReadinessLegalityTests
{
    /// <summary>Selects original sources that exposed curved-normal fallbacks and post-fit title-band frame mutation.</summary>
    /// <returns>Four unchanged catalogue regressions.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        HashSet<string> Reviewed = ["A112", "A167", "A168", "A169"];
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Reviewed.Contains(Scenario.Id)) yield return new TestCaseData(Scenario).SetName("CellLegality_" + Scenario.Id);
        }
    }

    /// <summary>Runs every real cell-bound stage and checks final paths, frame overlaps and allocated slots with the existing auditors.</summary>
    /// <param name="Scenario">Unchanged source with its original hints.</param>
    [TestCaseSource(nameof(Cases))]
    public void FinalModelKeepsLegalityAndSlots(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
        {
            var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
            Assert.Multiple(() =>
            {
                Assert.That(Findings.Where(Finding => Finding.Rule == "10"), Is.Empty);
                Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
                Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
                Assert.That(Layout.RenderContext.LayoutStageViolations.Where(Finding => Finding.Kind is "NoLegalRoute" or "TitleBandRoute"), Is.Empty);
            });
        });
    }
}
