using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace MermaidRecognition.Layout;

internal readonly record struct DiagramPlacementStateKey(ulong Geometry, ulong Cells)
{
    internal static DiagramPlacementStateKey From(FlowchartLayoutResult Layout) => new(
        FlowchartLayoutGeometryHash.HashLayoutGeometry(Layout), Layout.CellNodes is { Count: > 0 }
            ? FlowchartLayoutGeometryHash.HashCellRepairSignature(Layout.CellNodes[0].CellNode!.Grid.Capture()) : 0);
}

internal sealed record DiagramPlacementSearchScope(string DocumentId, long SourceRevision, DiagramLayoutOptions Options, string Locks, double PaddingPx, string RequestSignature);
internal sealed record DiagramPlacementSearchHistory(DiagramPlacementSearchScope Scope, ImmutableHashSet<DiagramPlacementStateKey> AcceptedStates);

internal sealed record DiagramPlacementSearchResult(DiagramEvaluationOrigin Origin, DiagramPlacementMetrics Before,
    ImmutableArray<DiagramPlacementComparison> Trials, DiagramPlacementComparison? Suggestion,
    int UniqueStates, int DuplicateStates, int PreviouslyAcceptedStates, bool BudgetExhausted, string Reason,
    DiagramPlacementSearchHistory History, ImmutableDictionary<string, string> SkippedNodes)
{
    // Keep this successor only after ordinary source/geometry publication succeeds.
    internal DiagramPlacementSearchHistory HistoryAfterAcceptance() => Suggestion?.Trial.CopyCandidate() is { } Candidate
        ? History with { AcceptedStates = History.AcceptedStates.Add(DiagramPlacementStateKey.From(Candidate.Layout)) } : History;
}

/// <summary>One bounded deterministic search; cancellation and failed publication leave accepted history unchanged.</summary>
internal static class DiagramPlacementSuggestionSearch
{
    internal static DiagramPlacementSearchResult Find(DiagramLayoutSession.Snapshot Snapshot, string RelationshipId,
        IEnumerable<string>? LockedNodes = null, int? CandidateLimit = null, int? RouteLimit = null, double? PaddingPx = null,
        DiagramPlacementSearchHistory? Previous = null, CancellationToken Token = default)
    {
        Token.ThrowIfCancellationRequested();
        var Locked = (LockedNodes ?? []).Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToImmutableArray();
        var Scorer = new DiagramPlacementScorer(Snapshot, PaddingPx);
        var Scope = new DiagramPlacementSearchScope(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Layout.RenderContext.LayoutOptions,
            JsonSerializer.Serialize(Locked), Scorer.PaddingPx, Scorer.RequestSignature);
        var Accepted = Previous?.Scope == Scope ? Previous.AcceptedStates : ImmutableHashSet<DiagramPlacementStateKey>.Empty;
        var Baseline = new DiagramLayoutSession(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.RoutingModel, Snapshot.Layout).CopySnapshot();
        var Origin = new DiagramEvaluationOrigin(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision);
        var BeforeState = DiagramPlacementStateKey.From(Baseline.Layout);
        var History = new DiagramPlacementSearchHistory(Scope, Accepted.Add(BeforeState));
        HashSet<DiagramPlacementStateKey> Seen = [BeforeState];
        var Exploration = new DiagramPlacementEvaluator(Snapshot, Locked).Explore(RelationshipId, CandidateLimit, RouteLimit, Token);
        List<DiagramPlacementComparison> Trials = [];
        int Duplicates = 0, Revisited = 0, Unique = 0;
        DiagramPlacementComparison? Best = null;
        foreach (var Trial in Exploration.Trials)
        {
            Token.ThrowIfCancellationRequested();
            if (Trial.CopyCandidate() is { } Candidate)
            {
                var State = DiagramPlacementStateKey.From(Candidate.Layout);
                if (!Seen.Add(State)) { Duplicates++; Skip("The complete candidate state was already examined."); continue; }
                if (Accepted.Contains(State)) { Revisited++; Skip("The candidate returns to a previously accepted state."); continue; }
                Unique++;
            }
            var Comparison = Scorer.Compare(Trial);
            Trials.Add(Comparison);
            if (Comparison.IsImprovement && (Best == null || Scorer.CompareImprovements(Comparison, Best) < 0)) Best = Comparison;
            void Skip(string Reason) => Trials.Add(new(Trial, Scorer.Before, null, false, Reason, 0, 0));
        }
        string Result = Best != null ? "A verified improvement was found within the bounded search."
            : Exploration.BudgetExhausted ? "No verified improvement was found within the candidate budget; further arrangements were not searched."
            : "No verified improvement was found among the generated candidates. " + Exploration.Reason;
        return new(Origin, Scorer.Before, Trials.ToImmutableArray(), Best, Unique, Duplicates, Revisited, Exploration.BudgetExhausted, Result, History, Exploration.SkippedNodes);
    }
}
