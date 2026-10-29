using System;
using System.Linq;

namespace LiveEditor;

public partial class MainForm
{
    private (string Document, string Identity)? _SelectedRelationshipIdentity;
    private (string Document, string Source, string OtherSource, int Line)? _RelationshipCommandSelection;
    private bool HasRelationshipCommandSelection => _RelationshipCommandSelection is { } Command
        && (CodeView.Editor.Text == Command.Source || CodeView.Editor.Text == Command.OtherSource);

    private bool CanEditRetainedPreview(bool AllowFailedLocalUpdate = false)
    {
        var Options = LiveEditorFeatureGate.CaptureLayoutOptions();
        if (!(Options.EnableIncrementalRouting && Options == _SessionLayoutOptions
            && !(LiveEditorFeatureGate.S_LegacyPreview && _Settings.LegacyPreview)
            && LastRetainedUpdateKind != null)) return false;
        if (PreviewIndexIsCurrent && _DiagramMetadata.Generation == _RenderGeneration && !_DiagramMetadata.RenderFailed
            && _DiagramMetadata.PercentRendered == 100) return true;
        return AllowFailedLocalUpdate && HasFailedLocalUpdateOnCurrentText;
    }

    /// <summary>
    /// True while the editor still holds exactly the text whose local update was rejected: the accepted picture and
    /// retained session are intact, so a corrective edit can be made.
    /// </summary>
    private bool HasFailedLocalUpdateOnCurrentText => Preview.Index != null && _FailedLocalUpdateTextVersion == _EditorTextVersion
        && _DiagramMetadata.Generation == _RenderGeneration && _DiagramMetadata.RenderFailed;

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
        _RelationshipCommandSelection = null;
        _SelectedRelationshipIdentity = null;
        if (Selected?.Kind != PreviewObjectKind.Relationship || !CanEditRetainedPreview()
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot) return;
        int Index = PreviewRelationshipMove.IndexAtLine(DocumentForCaret(CodeView.Editor.Text), Selected.LineNumber);
        var Binding = Snapshot.Identities.Bindings.SingleOrDefault(Binding => Binding.ParsedEdgeIndex == Index);
        if (Binding != null) _SelectedRelationshipIdentity = (Snapshot.DocumentId, Binding.Id);
    }

    private PreviewObject? RestoredRelationshipSelection()
    {
        var Snapshot = _RetainedPreview.CopyInteractionSnapshot();
        if (Snapshot != null && _RelationshipCommandSelection is { } Command && Snapshot.DocumentId == Command.Document
            && Snapshot.SourceText == Command.Source)
        {
            int Index = PreviewRelationshipMove.IndexAtLine(DocumentForCaret(Command.Source), Command.Line);
            var Restored = Snapshot.Identities.Bindings.SingleOrDefault(Binding => Binding.ParsedEdgeIndex == Index);
            if (Restored != null) _SelectedRelationshipIdentity = (Snapshot.DocumentId, Restored.Id);
        }
        if (_SelectedRelationshipIdentity is not { } Selected || Snapshot == null
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
