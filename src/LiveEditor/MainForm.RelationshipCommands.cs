using System;
using System.Linq;

namespace LiveEditor;

public partial class MainForm
{
    private (string Name, Action Execute)[] RelationshipAddChoices()
    {
        if (!CanEditRetainedPreview() || Preview.SelectedItems is not { Count: 2 } Selected
            || Selected.Any(Item => Item.Kind != PreviewObjectKind.Node)
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot) return [];
        string Source = CodeView.Editor.Text, From = Selected[0].Id, To = Selected[1].Id;
        string Document = Snapshot.DocumentId;
        var Kinds = PreviewRelationshipCommands.AllowedKinds(DocumentForCaret(Source), From, To);
        if (Kinds.Count == 0 || !PreviewRelationshipCommands.TryAdd(Source, From, To, Kinds[0], Snapshot, out _, out _)) return [];
        return [.. Kinds
            .Select(Kind => (Kind.Name, (Action)(() => AddPreviewRelationship(From, To, Kind, Source, Document))))];
    }

    internal bool AddPreviewRelationship(string From, string To, PreviewRelationshipCommands.Kind Kind, string Source, string Document)
    {
        if (!CanEditRetainedPreview() || CodeView.Editor.Text != Source
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot || Snapshot.DocumentId != Document) return false;
        if (!PreviewRelationshipCommands.TryAdd(Source, From, To, Kind, Snapshot, out var Edit, out var Reason))
        { SetStatus(Reason); return false; }
        return ApplyRelationshipCommand(Edit!, Source, Document, Edit!.Updated);
    }

    private bool CanDeletePreviewRelationship()
    {
        return CanEditRetainedPreview() && Preview.SelectedItems is { Count: 1 } Selected
            && PreviewRelationshipCommands.TryDelete(CodeView.Editor.Text, Selected[0], out _, out _);
    }

    internal bool DeletePreviewRelationship()
    {
        if (!CanEditRetainedPreview() || Preview.SelectedItems is not { Count: 1 } Selected
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot) return false;
        string Source = CodeView.Editor.Text;
        if (!PreviewRelationshipCommands.TryDelete(Source, Selected[0], out var Edit, out var Reason))
        { SetStatus(Reason); return false; }
        return ApplyRelationshipCommand(Edit!, Source, Snapshot.DocumentId, Source);
    }

    private bool ApplyRelationshipCommand(PreviewRelationshipCommands.Edit Edit, string Source, string Document, string SelectionSource)
    {
        if (!CanEditRetainedPreview() || CodeView.Editor.Text != Source) return false;
        HidePreviewHover();
        _RelationshipCommandSelection = (Document, SelectionSource, SelectionSource == Source ? Edit.Updated : Source, Edit.RelationshipLine);
        _SelectedRelationshipIdentity = null;
        _SyncingSelectionFromPreview = true;
        try { CodeView.ReplaceSourceSpan(Edit.Start, Edit.Length, Edit.Replacement); }
        finally { _SyncingSelectionFromPreview = false; }
        return true;
    }
}
