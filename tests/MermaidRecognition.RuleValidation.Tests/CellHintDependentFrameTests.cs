using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Preserves the complete original hint chain when A165's frame repair translates its dependent group members.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellHintDependentFrameTests
{
    /// <summary>The real source completes with legal unrelated frames and every active relative cell constraint still satisfied.</summary>
    [Test]
    public void A165MovesItsHintDependentClosureLegally()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A165");
        DiagramContract? CompletedContract = null;
        Dictionary<string, string> OriginalHints = CellFrameHintEvidence.FromSource(Scenario);
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
        {
            CompletedContract = Contract;
            Assert.That(CellFrameHintEvidence.Capture(Contract), Is.EquivalentTo(OriginalHints));
            FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
            Assert.That(Score.HintsHonoured, Is.EqualTo(Score.HintsTotal));
            Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
        }, Nodes =>
        {
            Assert.That(CompletedContract, Is.Not.Null);
            Assert.That(FlowchartPlacementHints.CellHintsMatch(CompletedContract!, [.. Nodes],
                Nodes[0].CellNode!.Grid.Capture().Regions), Is.True);
            Assert.That(CompletedContract!.Groups.Any(Group => Group.PlacementHint?.IsActive == true), Is.True);
        });
    }
}
