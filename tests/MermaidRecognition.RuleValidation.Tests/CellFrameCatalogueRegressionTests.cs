using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Keeps the ten reviewed frame-fit failures on the experimental cell pipeline without enabling production cells.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellFrameCatalogueRegressionTests
{
    /// <summary>Selects the unchanged reviewed catalogue sources, without adding engine exceptions for their identities.</summary>
    /// <returns>Ten named regressions with original hints and memberships.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        HashSet<string> Reviewed = ["F42", "F141", "A162", "A164", "A165", "A167", "A169", "EIP78", "EIP266", "EIP271"];
        foreach (TestCaseData Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            RouteRuleCatalogueAuditTests.Scenario Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Reviewed.Contains(Scenario.Id))
            {
                yield return new TestCaseData(Scenario).SetName("CellFrameRegression_" + Scenario.Id);
            }
        }
    }

    /// <summary>Requires real placement, frame fitting, routing and final validation to finish without any blocked pixel writer.</summary>
    /// <param name="Scenario">One reviewed original source.</param>
    [TestCaseSource(nameof(Cases))]
    [CancelAfter(60000)]
    public void ReviewedFramesCompleteCellPipeline(RouteRuleCatalogueAuditTests.Scenario Scenario) =>
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, RequireCompletion: true);
}
