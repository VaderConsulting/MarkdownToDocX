using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks that removing repair caps cannot replace a completed legal fan with a faulty cell-move candidate.</summary>
[TestFixture, NonParallelizable]
public sealed class CapRemovalFanRegressionTests
{
    /// <summary>Scores cell moves against completed nesting, preserving legal ports and separated fan tracks.</summary>
    /// <param name="Id">Production fan measured before and after the approved cap removal.</param>
    [TestCase("A104"), TestCase("A105")]
    public void CompletedFanIsTheReferenceForCellMoves(string Id)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        var Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
        TestContext.Out.WriteLine($"CAP_REMOVAL_FAN|{Id}|bends={Score.Bends}|crossings={Score.Crossings}|score={Score.OptimizationScore:R}");
        Assert.Multiple(() =>
        {
            Assert.That(Findings, Is.Empty, "A complete nested star must remain free of route and port faults.");
            Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
            Assert.That(Layout.RenderContext.RejectedCellPixelWrites, Is.Empty);
            Assert.That(Layout.CellNodes, Is.Not.Null);
            Assert.That(CellGridAudit.Faults(Contract, Layout, Layout.CellNodes!), Is.Empty);
        });
    }
}