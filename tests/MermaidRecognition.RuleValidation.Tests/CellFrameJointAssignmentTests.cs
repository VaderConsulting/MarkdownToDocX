using System.Text.Json;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks complete hint preservation and bounded legal frame fitting against A164's original declarations.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellFrameJointAssignmentTests
{
    /// <summary>A164 keeps the original member and group constraints, then fits its frames within the unchanged budget.</summary>
    [Test]
    public void A164KeepsEveryDeclaredHintWithinTheFrameBudget()
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A164");
        var Messages = new List<string>();
        var OriginalHints = CellFrameHintEvidence.FromSource(Scenario);
        var Previous = DiagramGridLayout.GroupFrameFitPassObserver;
        DiagramGridLayout.GroupFrameFitPassObserver = Messages.Add;
        try
        {
            CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (Contract, Layout) =>
            {
                Assert.That(CellFrameHintEvidence.Capture(Contract), Is.EquivalentTo(OriginalHints));
                var Score = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
                Assert.That(Score.HintsHonoured, Is.EqualTo(Score.HintsTotal));
                Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
            },
                Nodes => Assert.That(Nodes.All(Node => Node.CellHintValidator == null
                    || Node.CellHintValidator(Node.CellNode!.Grid.Capture().Regions)), Is.True));
        }
        finally { DiagramGridLayout.GroupFrameFitPassObserver = Previous; }
        var Candidates = Messages.Where(Message => Message.StartsWith("CELL_FRAME_CANDIDATE_DETAIL|", StringComparison.Ordinal))
            .Select(Message => JsonDocument.Parse(Message.Split('|', 2)[1])).ToArray();
        try
        {
            Assert.That(Candidates, Is.Not.Empty);
            foreach (var Candidate in Candidates)
                TestContext.Out.WriteLine("CELL_FRAME_COMBINATION|" + Candidate.RootElement.GetRawText());
            Assert.That(Candidates.Max(Item => Item.RootElement.GetProperty("Candidate").GetInt32()),
                Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            Assert.That(Candidates.Any(Item => Item.RootElement.GetProperty("Faults").GetArrayLength() != 0), Is.True);
            var Winner = Candidates.First(Item => Item.RootElement.GetProperty("Faults").GetArrayLength() == 0).RootElement;
            var Moves = Winner.GetProperty("Moves").EnumerateArray().ToDictionary(Move => Move.GetProperty("Id").GetString()!,
                Move => (Rows: Move.GetProperty("NewRow").GetInt32() - Move.GetProperty("OldRow").GetInt32(),
                    Columns: Move.GetProperty("NewColumn").GetInt32() - Move.GetProperty("OldColumn").GetInt32()));
            Assert.That(Moves.Keys, Does.Contain("defMeta").And.Contain("sendNotif"));
            Assert.That(Moves["defMeta"], Is.EqualTo(Moves["sendNotif"]));
            Assert.That(Winner.GetProperty("Rows").EnumerateObject().Any(), Is.True);
        }
        finally { foreach (var Candidate in Candidates) Candidate.Dispose(); }
    }
}
