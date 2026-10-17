using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Keeps independent-rank track allocation and outside-connector placement on the real cell pipeline.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellPlacementPolicyCatalogueRegressionTests
{
    /// <summary>Selects original scenarios that exposed assumptions about forced alignment.</summary>
    /// <returns>Unmodified catalogue sources for both partner track failures and the outside connector writer.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        HashSet<string> Reviewed = ["F136", "A110", "A111"];
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Reviewed.Contains(Scenario.Id)) yield return new TestCaseData(Scenario).SetName("CellPlacementPolicy_" + Scenario.Id);
        }
    }

    /// <summary>Requires real placement and routing to complete without an illegal pixel writer or a missing specialised track.</summary>
    /// <param name="Scenario">Original reviewed catalogue source.</param>
    [TestCaseSource(nameof(Cases))]
    [CancelAfter(60000)]
    public void IndependentPlacementCompletes(RouteRuleCatalogueAuditTests.Scenario Scenario) =>
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, RequireCompletion: true);
}
