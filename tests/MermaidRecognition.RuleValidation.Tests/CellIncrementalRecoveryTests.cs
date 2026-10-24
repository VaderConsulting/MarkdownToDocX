using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Preserves completed moves and exact port slots that earlier scoped scoring trials lost.</summary>
[TestFixture, NonParallelizable]
public sealed class CellIncrementalRecoveryTests
{
    /// <summary>Selects lost reference wins, joint hub improvements, preserved diamond routes and the published-slot regression.</summary>
    /// <returns>Original sources and their retained bend ceilings; a null ceiling selects slot-only coverage.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        Dictionary<string, int?> Ceilings = new() { ["F58"] = null, ["F59"] = 6, ["F61"] = 4,
            ["A18"] = 20, ["A107"] = 14, ["A141"] = 12, ["EIP215"] = 4, ["EIP246"] = 0,
            ["F30"] = 3, ["F120"] = 3, ["EIP342"] = 2, ["A103"] = 0, ["A136"] = 0, ["A161"] = 0 };
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Ceilings.TryGetValue(Scenario.Id, out var Ceiling))
                yield return new TestCaseData(Scenario, Ceiling).SetName("CellIncrementalRecovery_" + Scenario.Id);
        }
    }

    /// <summary>Requires preserved bend results, legal slots and the unchanged distinct-candidate limit after reference recovery.</summary>
    /// <param name="Scenario">Unmodified catalogue source.</param>
    /// <param name="BendCeiling">Previously verified bends, if this scenario pins a lost move.</param>
    [TestCaseSource(nameof(Cases))]
    public void CompletedRecoveryKeepsTheReferenceWins(RouteRuleCatalogueAuditTests.Scenario Scenario, int? BendCeiling)
    {
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
        {
            var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
            Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
            Assert.That(Layout.RenderContext.MoveBeforeDetourBudget.CandidateMoves,
                Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            if (BendCeiling.HasValue)
                Assert.That(Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Is.LessThanOrEqualTo(BendCeiling.Value));
        });
    }
}
