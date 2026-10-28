using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MermaidRecognition.Layout;

namespace LiveEditor;

public partial class MainForm
{
    private readonly ToolStripMenuItem _SuggestPlacementMenuItem = new("Suggest placement improvement…") { Name = "SuggestPlacementMenuItem" };
    private int _SuggestionSerial;
    internal PlacementSuggestionPreview? LastPlacementSuggestion { get; private set; }
    internal string? LastPlacementSuggestionReason { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<PlacementSuggestionPreview, bool>? PlacementSuggestionReview { get; set; }

    private void WirePlacementSuggestions()
    {
        ViewMenu.DropDownItems.Insert(ViewMenu.DropDownItems.IndexOf(AutoLayoutMenuItem) + 1, _SuggestPlacementMenuItem);
        _SuggestPlacementMenuItem.Click += (_, _) =>
        {
            if (!SuggestPlacementImprovement()) SetStatus("Select a relationship or a connected element in the current grid preview.");
        };
        ViewMenu.DropDownOpening += (_, _) => _SuggestPlacementMenuItem.Enabled = CanSuggestPlacement();
    }

    private void ClearPlacementSuggestion()
    {
        _SuggestionSerial++; LastPlacementSuggestion = null; LastPlacementSuggestionReason = null;
    }

    private bool CanSuggestPlacement()
    {
        var Options = LiveEditorFeatureGate.CaptureLayoutOptions();
        return Options.EnableRouteAwareConstruction && Options == _SessionLayoutOptions && LastRouteReport is { Routes.Length: > 0 }
            && !_SessionLegacyPreview.GetValueOrDefault() && PreviewIndexIsCurrent && LastRetainedUpdateKind != null
            && _DiagramMetadata.Generation == _RenderGeneration && !_DiagramMetadata.RenderFailed && _DiagramMetadata.PercentRendered == 100;
    }

    internal bool SuggestPlacementImprovement(string? RelationshipId = null)
    {
        if (!CanSuggestPlacement() || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot || Snapshot.IsStale) return false;
        if (RelationshipId == null)
        {
            var Selected = CurrentPreviewItem();
            var Candidates = LastRouteReport!.Routes.AsEnumerable();
            if (Selected?.Kind == PreviewObjectKind.Relationship)
            {
                int Index = PreviewRelationshipMove.IndexAtLine(DocumentForCaret(CodeView.Editor.Text), Selected.LineNumber);
                string? Id = Snapshot.Identities.Bindings.SingleOrDefault(Binding => Binding.ParsedEdgeIndex == Index)?.Id;
                Candidates = Candidates.Where(Route => Route.Identity == Id);
            }
            else if (Selected?.Kind == PreviewObjectKind.Node) Candidates = Candidates.Where(Route => Route.SourceId == Selected.Id || Route.TargetId == Selected.Id);
            RelationshipId = Candidates.OrderByDescending(Route => Route.Assessment).ThenByDescending(Route => Route.Bends)
                .ThenByDescending(Route => Route.CrossingPartners.Length).ThenBy(Route => Route.Identity, StringComparer.Ordinal).FirstOrDefault()?.Identity;
        }
        if (RelationshipId == null || Snapshot.Model.Edges.All(Edge => Edge.Id != RelationshipId)) return false;
        ClearPlacementSuggestion();
        _RenderCancellation?.Cancel(); _RenderCancellation?.Dispose();
        _RenderCancellation = new CancellationTokenSource();
        var Request = new RenderRequest(CodeView.Editor.Text, _RenderGeneration, _EditorTextVersion, false,
            _SessionLayoutOptions!, _RetainedPreview, _RenderCancellation.Token)
            { PlacementSuggestion = _SuggestionSerial, RelationshipIdentity = RelationshipId };
        SubmitWorkerRequest(Request, "Looking for a placement improvement…");
        return true;
    }

    private bool SuggestionRequestIsCurrent(RenderRequest Request) => !IsDisposed && !Request.Token.IsCancellationRequested
        && Request.Generation == _RenderGeneration && Request.TextVersion == _EditorTextVersion && Request.Retained == _RetainedPreview
        && Request.PlacementSuggestion == _SuggestionSerial && Request.Options == LiveEditorFeatureGate.CaptureLayoutOptions();

    private void PlacementSuggestionWorkerPass(RenderRequest Request)
    {
        try
        {
            var Proposal = Request.Retained.PreparePlacementSuggestion(Request.Source, Request.RelationshipIdentity!, Request.Options, Request.Token, out var Reason);
            Request.Token.ThrowIfCancellationRequested();
            if (IsDisposed || !IsHandleCreated) return;
            Invoke(() =>
            {
                if (!SuggestionRequestIsCurrent(Request)) return;
                LastPlacementSuggestion = Proposal; LastPlacementSuggestionReason = Reason;
                if (Proposal == null) { SetStatus(Reason); return; }
                bool Apply;
                if (PlacementSuggestionReview != null) Apply = PlacementSuggestionReview(Proposal);
                else
                {
                    using var Dialog = new PlacementSuggestionDialog(Proposal, Preview.RenderedSvg!, Preview.LastPreviewPngBytes!);
                    Apply = Dialog.ShowDialog(this) == DialogResult.OK;
                }
                if (!Apply) { SetStatus("Placement preview cancelled; the diagram is unchanged."); return; }
                if (!ApplyPlacementSuggestion(Proposal)) SetStatus("The source or layout changed; request a new placement suggestion.");
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception Failure)
        {
            if (!IsDisposed && IsHandleCreated) BeginInvoke(() =>
            {
                if (!SuggestionRequestIsCurrent(Request)) return;
                LastPlacementSuggestionReason = Failure.Message;
                SetStatus("No placement suggestion: " + Failure.Message);
            });
        }
    }

    internal bool ApplyPlacementSuggestion(PlacementSuggestionPreview Proposal)
    {
        if (!CanSuggestPlacement() || CodeView.Editor.Text != Proposal.OriginalSource
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot || Snapshot.IsStale
            || Proposal.Origin != new DiagramEvaluationOrigin(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision)) return false;
        HidePreviewHover();
        _SyncingSelectionFromPreview = true;
        try { CodeView.ReplaceSourceSpan(Proposal.Edit.Start, Proposal.Edit.Length, Proposal.Edit.Replacement); }
        finally { _SyncingSelectionFromPreview = false; }
        return true;
    }
}
