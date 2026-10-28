using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

/// <summary>Converts a dropped element centre into a same-group, same-layer relative placement hint.</summary>
internal static class PreviewNodePlacement
{
    internal sealed record Edit(int Start, int Length, string Replacement, string Hint)
    {
        internal string Apply(string Source) => Source.Substring(0, Start) + Replacement + Source.Substring(Start + Length);
    }

    internal static bool TryCreate(string Source, string NodeId, IReadOnlyList<PreviewObject> Items,
        PointF Drop, out Edit? Change, out string Reason)
    {
        Change = null;
        Reason = "Drop near another element in the same group and layer.";
        if (!float.IsFinite(Drop.X) || !float.IsFinite(Drop.Y)) return false;
        var Analysis = EnterpriseValidator.Analyze(Source);
        var Document = Analysis.Document;
        if (Analysis.HasErrors || !Document.Elements.TryGetValue(NodeId, out var Element)
            || Document.Groups.ContainsKey(NodeId))
        {
            Reason = "Move a regular element in a valid Enterprise diagram.";
            return false;
        }
        var Moving = Items.FirstOrDefault(Item => Item.Kind == PreviewObjectKind.Node && Item.Id == NodeId);
        if (Moving == null || Moving.Right <= Moving.Left || Moving.Bottom <= Moving.Top) return false;
        var Layer = EnterpriseElementCatalog.Elements[Element.ElementType].Layer;
        var Peer = Items.Where(Item => Item.Kind == PreviewObjectKind.Node && Item.Id != NodeId
            && Item.Right > Item.Left && Item.Bottom > Item.Top
            && Document.Elements.TryGetValue(Item.Id, out var Other) && !Document.Groups.ContainsKey(Item.Id)
            && Other.ParentId == Element.ParentId && EnterpriseElementCatalog.Elements[Other.ElementType].Layer == Layer
            && !DependsOn(Document, Other.Id, NodeId))
            .OrderBy(Item => DistanceSquared(Drop, Item)).ThenBy(Item => Item.Id, StringComparer.Ordinal).FirstOrDefault();
        if (Peer == null) return false;
        double Dx = Drop.X - (Peer.Left + Peer.Right) / 2;
        double Dy = Drop.Y - (Peer.Top + Peer.Bottom) / 2;
        double HalfWidth = (Moving.Right - Moving.Left + Peer.Right - Peer.Left) / 2;
        double HalfHeight = (Moving.Bottom - Moving.Top + Peer.Bottom - Peer.Top) / 2;
        bool Horizontal = Math.Abs(Dx) / HalfWidth >= Math.Abs(Dy) / HalfHeight;
        string Direction = Horizontal ? (Dx < 0 ? "left-of" : "right-of") : (Dy < 0 ? "above" : "below");
        double Extra = (Horizontal ? Math.Abs(Dx) - HalfWidth : Math.Abs(Dy) - HalfHeight) - DiagramGridMetrics.NODE_GAP_PX;
        int Offset = (int)Math.Clamp(Math.Round(Extra / DiagramGridMetrics.GRID_UNIT_PX), 0, 10000);
        string Hint = Direction + " " + Peer.Id + (Offset == 0 ? string.Empty : " +" + Offset.ToString(CultureInfo.InvariantCulture));
        if (!TryRewrite(Source, Element.LineNumber, Hint, out Change))
        {
            Reason = "This declaration spans multiple lines; edit its placement hint in the source.";
            return false;
        }
        var Updated = EnterpriseValidator.Analyze(Change!.Apply(Source));
        if (Updated.HasErrors || !Updated.Document.Elements.TryGetValue(NodeId, out var After)
            || After.PlacementHint?.PeerId != Peer.Id || After.Label != Element.Label || After.ParentId != Element.ParentId)
        {
            Change = null;
            Reason = "The placement hint could not be applied without changing the declaration.";
            return false;
        }
        Reason = string.Empty;
        return true;
    }

    private static bool DependsOn(EnterpriseDocument Document, string Peer, string Moving)
    {
        var Seen = new HashSet<string>(StringComparer.Ordinal);
        while (Seen.Add(Peer))
        {
            if (Peer == Moving) return true;
            if (!Document.Elements.TryGetValue(Peer, out var Element) || string.IsNullOrEmpty(Element.PlacementHint?.PeerId)) return false;
            Peer = Element.PlacementHint.PeerId;
        }
        return true;
    }

    private static double DistanceSquared(PointF Point, PreviewObject Item)
    {
        double X = Point.X - (Item.Left + Item.Right) / 2, Y = Point.Y - (Item.Top + Item.Bottom) / 2;
        return X * X + Y * Y;
    }

    private static bool TryRewrite(string Source, int LineNumber, string Hint, out Edit? Change)
    {
        Change = null;
        int Start = 0;
        for (int Line = 1; Line < LineNumber; Line++)
        {
            int Next = Source.IndexOf('\n', Start);
            if (Next < 0) return false;
            Start = Next + 1;
        }
        int End = Start;
        while (End < Source.Length && Source[End] != '\r' && Source[End] != '\n') End++;
        string Text = Source.Substring(Start, End - Start);
        char Quote = '\0';
        bool Escaped = false;
        int Suffix = Text.Length, OldHintStart = -1;
        string PreviousWord = string.Empty;
        int Words = 0;
        for (int I = 0; I < Text.Length; I++)
        {
            char C = Text[I];
            if (Quote != '\0')
            {
                if (Escaped) Escaped = false;
                else if (C == '\\') Escaped = true;
                else if (C == Quote) Quote = '\0';
                continue;
            }
            if (C is '\'' or '"') { Quote = C; continue; }
            if (Text.AsSpan(I).StartsWith(":::".AsSpan(), StringComparison.Ordinal)
                || Text.AsSpan(I).StartsWith("%%".AsSpan(), StringComparison.Ordinal)) { Suffix = I; break; }
            if (char.IsWhiteSpace(C)) continue;
            int WordStart = I;
            while (I < Text.Length && !char.IsWhiteSpace(Text[I])) I++;
            string Word = Text.Substring(WordStart, I - WordStart);
            if (Words >= 2 && PreviousWord != "in" && Word is "above" or "below" or "left-of" or "right-of" or "push")
            {
                OldHintStart = WordStart;
                break;
            }
            PreviousWord = Word; Words++; I--;
        }
        if (Quote != '\0') return false;
        // Scan the tail separately so an existing hint is replaced before any
        // inline class or comment, while quoted label words remain untouched.
        if (OldHintStart >= 0)
        {
            int Class = Text.IndexOf(":::", OldHintStart, StringComparison.Ordinal);
            int Comment = Text.IndexOf("%%", OldHintStart, StringComparison.Ordinal);
            Suffix = Math.Min(Class < 0 ? Text.Length : Class, Comment < 0 ? Text.Length : Comment);
        }
        int PrefixEnd = OldHintStart >= 0 ? OldHintStart : Suffix;
        string Replacement = Text.Substring(0, PrefixEnd).TrimEnd() + " " + Hint;
        if (Suffix < Text.Length) Replacement += " " + Text.Substring(Suffix);
        Change = new(Start, End - Start, Replacement, Hint);
        return true;
    }
}
