using System.Collections.Immutable;
using System.Threading;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class DiagramPlacementSuggestionSearchTests
{
    [Test]
    public void RepeatedSearchesChooseTheSameVerifiedStateWithoutChangingTheBaseline()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Before = DiagramPlacementStateKey.From(Snapshot.Layout);
        var First = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab");
        Assert.That(First.Suggestion, Is.Not.Null, First.Reason);
        var Expected = DiagramPlacementStateKey.From(First.Suggestion!.Trial.CopyCandidate()!.Layout);
        for (int Run = 0; Run < 3; Run++)
        {
            var Next = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab");
            Assert.That(Next.Suggestion, Is.Not.Null);
            Assert.That(DiagramPlacementStateKey.From(Next.Suggestion!.Trial.CopyCandidate()!.Layout), Is.EqualTo(Expected));
            Assert.That(Next.Trials.Select(Item => Item.Reason), Is.EqualTo(First.Trials.Select(Item => Item.Reason)));
        }
        Assert.Multiple(() =>
        {
            Assert.That(First.Trials, Has.Length.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            Assert.That(First.DuplicateStates, Is.GreaterThan(0), "the routing-first no-op is the baseline state");
            Assert.That(First.Suggestion.After!.Canonical.Bends, Is.LessThan(First.Before.Canonical.Bends));
            Assert.That(DiagramPlacementStateKey.From(Snapshot.Layout), Is.EqualTo(Before));
            Assert.That(First.History.AcceptedStates, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void CandidateRankingChargesBothIncumbentAndChallengerSymmetrically()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Suggestion = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab").Suggestion!;
        var Expensive = Suggestion with { RepairCharge = Suggestion.RepairCharge + 100 };
        var Scorer = new DiagramPlacementScorer(Snapshot);
        Assert.That(Scorer.CompareImprovements(Suggestion, Expensive), Is.LessThan(0));
        Assert.That(Scorer.CompareImprovements(Expensive, Suggestion), Is.GreaterThan(0));
        Assert.That(Scorer.CompareImprovements(Suggestion, Suggestion), Is.Zero);
    }

    [Test]
    public void PreviouslyAcceptedStatesAreSkippedAndTheJournalRemainsImmutable()
    {
        var Snapshot = Fixture().CopySnapshot();
        var First = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab");
        var Accepted = First.HistoryAfterAcceptance();
        Assert.That(First.History.AcceptedStates, Has.Count.EqualTo(1));
        Assert.That(Accepted.AcceptedStates, Has.Count.EqualTo(2));
        var Again = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab", Previous: Accepted);
        Assert.That(Again.PreviouslyAcceptedStates, Is.GreaterThanOrEqualTo(1));
        Assert.That(Again.Trials.Any(Trial => Trial.Reason.Contains("previously accepted")), Is.True);
        if (Again.Suggestion != null) Assert.That(Accepted.AcceptedStates.Contains(DiagramPlacementStateKey.From(Again.Suggestion.Trial.CopyCandidate()!.Layout)), Is.False);
    }

    [TestCase("source")]
    [TestCase("options")]
    [TestCase("locks")]
    [TestCase("padding")]
    [TestCase("metadata")]
    public void ChangedSearchInputsStartANewVisitedStateScope(string Change)
    {
        var Snapshot = Fixture().CopySnapshot();
        var First = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab");
        var All = First.Trials.Select(Trial => Trial.Trial.CopyCandidate()).Where(Item => Item != null)
            .Select(Item => DiagramPlacementStateKey.From(Item!.Layout)).ToImmutableHashSet();
        var Blocked = First.History with { AcceptedStates = First.History.AcceptedStates.Union(All) };
        Assert.That(DiagramPlacementSuggestionSearch.Find(Snapshot, "ab", Previous: Blocked).Suggestion, Is.Null);
        var Model = DiagramSourceModel.Copy(Snapshot.RoutingModel);
        Snapshot = Snapshot with { RoutingModel = Model };
        string[] Locks = []; double? Padding = null;
        if (Change == "source") Snapshot = Snapshot with { SourceRevision = Snapshot.SourceRevision + 1 };
        if (Change == "options") Snapshot.Layout.RenderContext = new() { UseCellRegions = true,
            LayoutOptions = new() { EnableWidthConstrainedLayout = true, TargetWidthPx = 1200 } };
        if (Change == "locks") Locks = ["a"];
        if (Change == "padding") Padding = 24;
        if (Change == "metadata") Model.Nodes.Single(Node => Node.Id == "a").MetadataMutable.Add("semanticFact", new RawMetadata("semanticFact", "changed"));
        var Fresh = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab", LockedNodes: Locks, PaddingPx: Padding, Previous: Blocked);
        Assert.That(Fresh.PreviouslyAcceptedStates, Is.Zero);
        Assert.That(Fresh.History.Scope, Is.Not.EqualTo(First.History.Scope));
        Assert.That(Fresh.Suggestion, Is.Not.Null, Fresh.Reason);
    }

    [Test]
    public void SearchLimitReportsUnsearchedArrangementsWithoutClaimingAnOptimum()
    {
        var Result = DiagramPlacementSuggestionSearch.Find(Fixture().CopySnapshot(), "ab", CandidateLimit: 1);
        Assert.That(Result.Trials, Has.Length.EqualTo(1));
        Assert.That(Result.BudgetExhausted, Is.True);
        Assert.That(Result.Suggestion, Is.Null);
        Assert.That(Result.Reason, Does.Contain("further arrangements were not searched"));
    }

    [Test]
    public void LockedNodesProduceAnExplainedFiniteNoImprovementResult()
    {
        var Result = DiagramPlacementSuggestionSearch.Find(Fixture().CopySnapshot(), "ab", LockedNodes: new[] { "a", "b" });
        Assert.That(Result.Suggestion, Is.Null);
        Assert.That(Result.BudgetExhausted, Is.False);
        Assert.That(Result.Trials, Has.Length.EqualTo(1));
        Assert.That(Result.SkippedNodes.Keys, Is.EquivalentTo(new[] { "a", "b" }));
        Assert.That(Result.Reason, Does.Contain("No verified improvement"));
    }

    [Test]
    public void RepeatedAcceptedMovesStrictlyImproveWithoutReturningToAnEarlierScene()
    {
        var Session = Fixture();
        DiagramPlacementSearchHistory? History = null;
        HashSet<DiagramPlacementStateKey> Seen = [];
        bool Finished = false;
        for (int Step = 0; Step < 6; Step++)
        {
            var Snapshot = Session.CopySnapshot();
            Assert.That(Seen.Add(DiagramPlacementStateKey.From(Snapshot.Layout)), Is.True);
            var Result = DiagramPlacementSuggestionSearch.Find(Snapshot, "ab", Previous: History);
            if (Result.Suggestion == null) { Finished = true; break; }
            Assert.That(Result.Suggestion.IsImprovement, Is.True);
            var Trial = Result.Suggestion.Trial;
            var Candidate = Trial.Kind == DiagramPlacementTrialKind.NodeMove ? Session.PrepareMove(0, Trial.Targets)
                : Session.PreparePorts(0, "ab", Snapshot.RoutingModel.Edges.Single(Edge => Edge.Id == "ab").RoutingHint ?? new());
            Commit(Session, Candidate);
            Assert.That(DiagramPlacementStateKey.From(Session.CopyLayout()), Is.EqualTo(DiagramPlacementStateKey.From(Trial.CopyCandidate()!.Layout)));
            History = Result.HistoryAfterAcceptance();
        }
        Assert.That(Finished, Is.True, "the small fixture should reach a bounded no-improvement result");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CancellationBeforeOrDuringTrialsCannotMutateAcceptedGeometry(bool During)
    {
        var Snapshot = Fixture().CopySnapshot();
        var Before = DiagramPlacementStateKey.From(Snapshot.Layout);
        using var Cancellation = new CancellationTokenSource();
        if (!During) Cancellation.Cancel();
        else FlowchartRenderContext.OperationCountObserver = (Name, _) =>
        { if (Name.StartsWith("automatic-route-cache-", StringComparison.Ordinal)) Cancellation.Cancel(); };
        try { Assert.Throws<OperationCanceledException>(() => DiagramPlacementSuggestionSearch.Find(Snapshot, "ab", Token: Cancellation.Token)); }
        finally { FlowchartRenderContext.OperationCountObserver = null; }
        Assert.That(DiagramPlacementStateKey.From(Snapshot.Layout), Is.EqualTo(Before));
    }

    private static DiagramLayoutSession Fixture()
    {
        var Session = new DiagramLayoutSession("suggestions", 0);
        Commit(Session, Session.PrepareAddNode(0, new() { Id = "a", Label = "item", Shape = "rect" }, new(0, 0, 1, 1)));
        Commit(Session, Session.PrepareAddNode(0, new() { Id = "b", Label = "item", Shape = "rect" }, new(3, 2, 1, 1)));
        Commit(Session, Session.PrepareAddRelationship(0, "ab", new() { SourceId = "a", TargetId = "b" }));
        return Session;
    }
    private static void Commit(DiagramLayoutSession Session, DiagramLayoutSession.Update Candidate)
    { Assert.That(Candidate.FailureReason, Is.Null); Assert.That(Session.TryCommit(Candidate, out var Reason), Is.True, Reason); }
}
