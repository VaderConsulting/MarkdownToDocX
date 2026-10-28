using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

internal sealed record PlacementSuggestionPreview(DiagramEvaluationOrigin Origin, string OriginalSource,
    PreviewNodePlacement.Edit Edit, string Svg, byte[] Png, DiagramPlacementComparison Comparison, int ExaminedCandidates);

internal sealed partial class RetainedEnterprisePreview
{
    internal PlacementSuggestionPreview? PreparePlacementSuggestion(string Source, string RelationshipId,
        DiagramLayoutOptions Options, CancellationToken Token, out string Reason)
    {
        Token.ThrowIfCancellationRequested();
        RetainedEnterprisePreview Detached;
        DiagramSourceUpdateService.Snapshot Snapshot;
        lock (Gate)
        {
            Detached = ForkForEvaluation();
            Snapshot = Detached.Service!.CopySnapshot();
        }
        if (Snapshot.SourceText != Source || Snapshot.IsStale || Options != Snapshot.Layout.RenderContext.LayoutOptions)
            throw new InvalidOperationException("The accepted source changed before the suggestion started.");
        var Baseline = new DiagramLayoutSession.Snapshot(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision, Snapshot.Model, Snapshot.Layout);
        var Search = DiagramPlacementSuggestionSearch.Find(Baseline, RelationshipId, PaddingPx: Detached.Frame!.DiagramPaddingPx, Token: Token);
        var Scorer = new DiagramPlacementScorer(Baseline, Detached.Frame.DiagramPaddingPx);
        var Choices = Search.Trials.Where(Item => Item.IsImprovement && Item.Trial.Kind == DiagramPlacementTrialKind.NodeMove
            && Item.Trial.Targets.Count == 1 && Item.After!.MovedNodes > 0).ToList();
        Choices.Sort(Scorer.CompareImprovements);
        Reason = Search.Reason;
        var Regions = Baseline.Layout.CellNodes!.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region, StringComparer.Ordinal);
        foreach (var Choice in Choices)
        {
            Token.ThrowIfCancellationRequested();
            var Target = Choice.Trial.Targets.Single();
            if (!PreviewNodePlacement.TryCreateAt(Source, Target.Key, Target.Value, out var Edit, out Reason)) continue;
            var Evaluation = Detached.ForkForEvaluation();
            long NextRevision = checked(Snapshot.SourceRevision + 1);
            Evaluation.Observe(NextRevision);
            Prepared Draft;
            try { Draft = Evaluation.Prepare(Edit!.Apply(Source), NextRevision, EnterpriseValidator.Analyze(Edit.Apply(Source)), Options, AllowInitialLayout: false); }
            catch (InvalidOperationException Failure) { Reason = Failure.Message; continue; }
            if (Draft.Rebuild || Draft.Update == null) { Reason = "The proposed placement requires initial layout rather than a retained update."; continue; }
            var Actual = Draft.CandidateService.CopyCandidateSnapshot(Draft.Update);
            // Source semantics may carry relative dependents with the nominated
            // node. Verify and display the actual source transaction, including
            // its measured-label dependencies, rather than the cheaper trial.
            var Targets = Actual.Layout.CellNodes!.Where(Node => Regions[Node.Id] != Node.CellNode!.Region)
                .ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region, StringComparer.Ordinal);
            if (Targets.Count == 0) continue;
            // The new absolute pin records the chosen position. It must not earn
            // an extra hint reward when comparing geometry with the old scene.
            var Measured = new DiagramLayoutSession.Snapshot(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision, Snapshot.Model, Actual.Layout);
            var Trial = DiagramPlacementTrial.FromValidatedGeometry(Search.Origin, Targets, Measured, Draft.AffectedRouteIds, Draft.RebuiltRouteIds);
            var Comparison = Scorer.Compare(Trial);
            if (!Comparison.IsImprovement) { Reason = Comparison.Reason; continue; }
            Token.ThrowIfCancellationRequested();
            byte[]? Png = MDConverter.MermaidPreviewRender.RasterizeAcceptedSvg(Draft.Svg, out _);
            if (Png == null || Png.Length == 0) { Reason = "The suggested diagram could not be rasterised."; continue; }
            Reason = "A verified source placement is ready to preview.";
            return new(Search.Origin, Source, Edit!, Draft.Svg, Png, Comparison, Search.Trials.Length);
        }
        Reason = Choices.Count == 0 ? Search.Suggestion?.Trial.Kind == DiagramPlacementTrialKind.RouteOnly
                ? "No verified node placement improvement was found; a route-only improvement was measured."
                : "No verified node placement improvement was found within the search budget. " + Search.Reason
            : "No generated placement could be verified through the source and label update. " + Reason;
        return null;
    }
}
