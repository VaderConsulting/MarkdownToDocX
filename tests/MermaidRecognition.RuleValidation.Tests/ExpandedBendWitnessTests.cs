using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Proves each new check can see a real fault without changing the measured layout, hints, cells or caches.</summary>
[TestFixture, NonParallelizable]
public sealed class ExpandedBendWitnessTests
{
    /// <summary>Uses deliberately unpolished production-owned state to exercise each diagnostic before its repair is applied.</summary>
    /// <param name="Id">Unmodified catalogue source providing the reproducer.</param>
    /// <param name="Check">Case-sensitive new check identity.</param>
    [TestCase("A113", 'P'), TestCase("F45", 'S'), TestCase("F74", 'M'), TestCase("A113", 'H')]
    public void NewCheckReportsItsFaultWithoutMutatingTheOriginal(string Id, char Check)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        DiagramContract Contract = Scenario.Enterprise ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true, DisableExpandedBendRepairsForTests = Check == 'P',
            DisableFinishedBendRepairsForTests = true };
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out FlowchartLayoutResult? Layout), Is.True);
        string Before = CellProductionParityTests.Snapshot(Contract, Layout);
        FlowchartLogicalGrid.Snapshot Cells = Layout.CellNodes![0].CellNode!.Grid.Capture();
        IReadOnlyList<UnnecessaryBendViolation> Findings = UnnecessaryBendChecker.Check(Id, Contract, Layout);
        Assert.That(Findings.Any(Finding => Finding.Check == Check), Is.True, $"{Id} must expose the new {Check} fault.");
        Assert.That(CellProductionParityTests.Snapshot(Contract, Layout), Is.EqualTo(Before));
        Assert.That(Layout.CellNodes[0].CellNode!.Grid.Capture(), Is.SameAs(Cells));
        foreach (UnnecessaryBendViolation? Finding in Findings.Where(Finding => Finding.Check == Check))
        {
            TestContext.Out.WriteLine($"EXPANDED_WITNESS|{Id}|{Check}|{Finding.LineId}|{Finding.Detail}");
        }
    }
}
