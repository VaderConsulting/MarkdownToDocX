using System;
using System.Linq;

namespace LiveEditor;

public partial class MainForm
{
    private (string Name, Action Execute)[] RelationshipAddChoices()
    {
        if (!CanEditRetainedPreview(AllowFailedLocalUpdate: true) || Preview.SelectedItems is not { Count: >= 1 and <= 2 } Selected
            || Selected.Any(Item => Item.Kind != PreviewObjectKind.Node)
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot) return [];
        string Source = CodeView.Editor.Text, From = Selected[0].Id, To = Selected[^1].Id;
        string Document = Snapshot.DocumentId;
        var Kinds = PreviewRelationshipCommands.AllowedKinds(DocumentForCaret(Source), From, To);
        return [.. Kinds
            .Where(Kind => PreviewRelationshipCommands.TryAdd(Source, From, To, Kind, Snapshot, out _, out _, AllowFailedUpdate: true))
            .Select(Kind => (Kind.Name + (From == To ? " to itself" : ""), (Action)(() => AddPreviewRelationship(From, To, Kind, Source, Document))))];
    }

    internal bool AddPreviewRelationship(string From, string To, PreviewRelationshipCommands.Kind Kind, string Source, string Document)
    {
        if (!CanEditRetainedPreview(AllowFailedLocalUpdate: true) || CodeView.Editor.Text != Source
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot || Snapshot.DocumentId != Document) return false;
        if (!PreviewRelationshipCommands.TryAdd(Source, From, To, Kind, Snapshot, out var Edit, out var Reason, AllowFailedUpdate: true))
        { SetStatus(Reason); return false; }
        return ApplyRelationshipCommand(Edit!, Source, Document, Edit!.Updated);
    }

    // Preview objects carry lines of the last accepted source; map them onto the text being edited.
    private int? CurrentSourceLine(PreviewObject Item)
        => _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot ? null
            : Snapshot.SourceText == CodeView.Editor.Text ? Item.LineNumber
            : SourceLineMap.Map(Snapshot.SourceText, CodeView.Editor.Text, Item.LineNumber);

    private bool CanDeletePreviewRelationship()
    {
        return CanEditRetainedPreview(AllowFailedLocalUpdate: true) && Preview.SelectedItems is { Count: 1 } Selected
            && CurrentSourceLine(Selected[0]) is { } Line
            && PreviewRelationshipCommands.TryDelete(CodeView.Editor.Text, Selected[0], out _, out _, Line);
    }

    internal bool DeletePreviewRelationship()
    {
        if (!CanEditRetainedPreview(AllowFailedLocalUpdate: true) || Preview.SelectedItems is not { Count: 1 } Selected
            || _RetainedPreview.CopyInteractionSnapshot() is not { } Snapshot) return false;
        string Source = CodeView.Editor.Text;
        if (CurrentSourceLine(Selected[0]) is not { } Line) return false;
        if (!PreviewRelationshipCommands.TryDelete(Source, Selected[0], out var Edit, out var Reason, Line))
        { SetStatus(Reason); return false; }
        return ApplyRelationshipCommand(Edit!, Source, Snapshot.DocumentId, Source);
    }

    private bool ApplyRelationshipCommand(PreviewRelationshipCommands.Edit Edit, string Source, string Document, string SelectionSource)
    {
        if (!CanEditRetainedPreview(AllowFailedLocalUpdate: true) || CodeView.Editor.Text != Source) return false;
        HidePreviewHover();
        _RelationshipCommandSelection = (Document, SelectionSource, SelectionSource == Source ? Edit.Updated : Source, Edit.RelationshipLine);
        _SelectedRelationshipIdentity = null;
        _SyncingSelectionFromPreview = true;
        try { CodeView.ReplaceSourceSpan(Edit.Start, Edit.Length, Edit.Replacement); }
        finally { _SyncingSelectionFromPreview = false; }
        return true;
    }
}
