using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Pins the fan and convergence side regressions caused by provisional allocation and late channel repair.</summary>
[TestFixture]
[NonParallelizable]
public sealed class FacingSideRegressionTests
{
    /// <summary>Final built assignments must retain facing sides and every hard legality constraint.</summary>
    /// <param name="Id">The unchanged catalogue source whose side findings increased.</param>
    [TestCase("F111")]
    [TestCase("F136")]
    [TestCase("EIP188")]
    [TestCase("A18")]
    [TestCase("A14")]
    [TestCase("A152")]
    [TestCase("A148")]
    [TestCase("EIP24")]
    [TestCase("A159")]
    public void FinalAssignmentsPreserveFanAndConvergenceSides(string Id)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue()
            .Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "4.11" or "5.8" or "4.2" or "4.9" or "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
    }
}
