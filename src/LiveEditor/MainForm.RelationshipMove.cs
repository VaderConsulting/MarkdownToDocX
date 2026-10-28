using System;
using System.Linq;

namespace LiveEditor;

public partial class MainForm
{
    private (string Document, string Identity)? _SelectedRelationshipIdentity;

    private bool CanEditRetainedPreview()
    {
        var Options = LiveEditorFeatureGate.CaptureLayoutOptions();
        return Options.EnableIncrementalRouting && Options == _SessionLayoutOptions
            && !(LiveEditorFeatureGate.S_LegacyPreview && _Settings.LegacyPreview)
            && LastRetainedUpdateKind != null && PreviewIndexIsCurrent
            && _DiagramMetadata.Generation == _RenderGeneration && !_DiagramMetadata.RenderFailed
            && _DiagramMetadata.PercentRendered == 100;
    }

    private PreviewRelationshipMove? CreateRelationshipMove(PreviewObject Item)
    {
        if (!CanEditRetainedPreview() || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot) return null;
        return PreviewRelationshipMove.Create(CodeView.Editor.Text, Item, Preview.Items, Snapshot);
    }

    internal bool MovePreviewRelationship(PreviewRelationshipMove Move, bool SourceEnd, PreviewRelationshipMove.Target Target)
    {
        if (!CanEditRetainedPreview() || CodeView.Editor.Text != Move.SourceText
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot || Snapshot.DocumentId != Move.DocumentId
            || !Snapshot.Identities.Bindings.Any(Binding => Binding.Id == Move.Identity)) return false;
        if (!Move.TryCreateEdit(SourceEnd, Target, out var Edit, out var Reason))
        { if (!string.IsNullOrEmpty(Reason)) SetStatus(Reason); return false; }
        HidePreviewHover();
        _SelectedRelationshipIdentity = (Move.DocumentId, Move.Identity);
        _SyncingSelectionFromPreview = true;
        try { CodeView.ReplaceSourceSpan(Edit!.Start, Edit.Length, Edit.Replacement); }
        finally { _SyncingSelectionFromPreview = false; }
        return true;
    }

    private void RememberRelationshipSelection(PreviewObject? Selected)
    {
        if (_IgnorePreviewSelection || !PreviewIndexIsCurrent) return;
        _SelectedRelationshipIdentity = null;
        if (Selected?.Kind != PreviewObjectKind.Relationship || !CanEditRetainedPreview()
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot) return;
        int Index = PreviewRelationshipMove.IndexAtLine(DocumentForCaret(CodeView.Editor.Text), Selected.LineNumber);
        var Binding = Snapshot.Identities.Bindings.SingleOrDefault(Binding => Binding.ParsedEdgeIndex == Index);
        if (Binding != null) _SelectedRelationshipIdentity = (Snapshot.DocumentId, Binding.Id);
    }

    private PreviewObject? RestoredRelationshipSelection()
    {
        if (_SelectedRelationshipIdentity is not { } Selected || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot
            || Snapshot.DocumentId != Selected.Document || Snapshot.SourceText != CodeView.Editor.Text) return null;
        var Binding = Snapshot.Identities.Bindings.SingleOrDefault(Binding => Binding.Id == Selected.Identity);
        if (Binding == null) return null;
        var Document = DocumentForCaret(CodeView.Editor.Text);
        int[] Lines = [.. Document.Relationships.Where(Edge => !Edge.HiddenByNesting).Select(Edge => Edge.LineNumber),
            .. Document.PlainConnections.Select(Edge => Edge.LineNumber)];
        return Binding.ParsedEdgeIndex < Lines.Length ? Preview.Items.FirstOrDefault(Item => Item.Kind == PreviewObjectKind.Relationship
            && Item.LineNumber == Lines[Binding.ParsedEdgeIndex]) : null;
    }
}
