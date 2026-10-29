using System;
using System.Drawing;

namespace LiveEditor;

public partial class MainForm
{
    private Func<PointF, PointF?>? CreateNodeMoveSnap(PreviewObject Item)
    {
        if (!CanMovePreviewNode(Item) || _RetainedPreview.CopyPlacementLayout() is not { } Layout) return null;
        var Grid = new PreviewNodePlacement.GridSnap(CodeView.Editor.Text, Preview.Items, Layout);
        if (Grid.Snap(Item.Id, new((float)((Item.Left + Item.Right) / 2), (float)((Item.Top + Item.Bottom) / 2))) == null) return null;
        return Drop => Grid.Snap(Item.Id, Drop)?.Center;
    }

    private bool CanMovePreviewNode(PreviewObject Item)
    {
        if (!CanEditRetainedPreview(AllowFailedLocalUpdate: true) || Item.Kind != PreviewObjectKind.Node) return false;
        var Document = DocumentForCaret(CodeView.Editor.Text);
        return Document.Elements.TryGetValue(Item.Id, out var Element)
            && !Document.Groups.ContainsKey(Item.Id) && string.IsNullOrEmpty(Element.ParentId)
            && (PreviewIndexIsCurrent || AcceptedDeclarationMatches(Element));
    }

    // After a rejected edit the accepted picture still describes the last accepted source. The node may only be
    // moved again when its declaration is where that source had it, so the rewrite targets the right line.
    private bool AcceptedDeclarationMatches(MermaidRecognition.Kinds.Enterprise.EnterpriseElement Current)
        => _RetainedPreview.CopyInteractionSnapshot() is { } Snapshot
            && MermaidRecognition.Kinds.Enterprise.EnterpriseValidator.Analyze(Snapshot.SourceText).Document.Elements
                .TryGetValue(Current.Id, out var Accepted)
            && Accepted.LineNumber == Current.LineNumber && Accepted.ParentId == Current.ParentId;

    internal bool MovePreviewNode(string NodeId, PointF Drop)
    {
        var Item = Preview.Index?.FindObject("node\u001f" + NodeId);
        if (Item == null || !CanMovePreviewNode(Item)) return false;
        bool Corrective = !PreviewIndexIsCurrent;
        string Source = CodeView.Editor.Text;
        var Layout = _RetainedPreview.CopyPlacementLayout();
        if (Layout == null) return false;
        var Grid = new PreviewNodePlacement.GridSnap(Source, Preview.Items, Layout);
        var Snap = Grid.Snap(NodeId, Drop);
        // Returning a node to its accepted cell is not a no-op while a rejected hint is still in the source.
        if (Snap == null || (!Corrective && Math.Abs(Snap.Center.X - (Item.Left + Item.Right) / 2) < 0.01
            && Math.Abs(Snap.Center.Y - (Item.Top + Item.Bottom) / 2) < 0.01)) return false;
        if (!PreviewNodePlacement.TryCreate(Source, NodeId, Preview.Items, Drop, out var Edit, out string Reason, Grid))
        {
            SetStatus(Reason);
            return false;
        }
        string Updated = Edit!.Apply(Source);
        if (Updated == Source) return false;
        HidePreviewHover();
        _SyncingSelectionFromPreview = true;
        try
        {
            // One RichEdit replacement is one undoable command. TextChanged is
            // the only render submission, exactly as for a typed hint.
            ApplyGestureEdit(Edit.Start, Edit.Length, Edit.Replacement);
        }
        finally { _SyncingSelectionFromPreview = false; }
        return true;
    }
}
