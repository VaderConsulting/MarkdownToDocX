using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

/// <summary>Converts a dropped element centre into a same-group, same-layer relative placement hint.</summary>
internal static partial class PreviewNodePlacement
{
    internal sealed record Edit(int Start, int Length, string Replacement, string Hint)
    {
        internal string Apply(string Source) => Source.Substring(0, Start) + Replacement + Source.Substring(Start + Length);
    }

    internal static bool TryCreate(string Source, string NodeId, IReadOnlyList<PreviewObject> Items,
        PointF Drop, out Edit? Change, out string Reason, GridSnap? Grid = null)
    {
        if (!TryCreateCore(Source, NodeId, Items, Drop, out Change, out Reason, Grid)) return false;
        // Dragging moves one node. Whatever was placed relative to it keeps its current cell instead of following.
        return Grid == null || TryPinDependents(Source, NodeId, Grid, ref Change, ref Reason);
    }

    private static bool TryPinDependents(string Source, string NodeId, GridSnap Grid, ref Edit? Change, ref string Reason)
    {
        var Document = EnterpriseValidator.Analyze(Source).Document;
        var Dependents = Document.Elements.Values.Select(Element => (Element.Id, Element.LineNumber, Element.PlacementHint))
            .Concat(Document.Notes.Values.Select(Note => (Note.Id, Note.LineNumber, Note.PlacementHint)))
            .Where(Item => Item.PlacementHint is { IsActive: true, Kind: not PlacementHintKind.Push and not PlacementHintKind.At } Hint
                && Hint.PeerId == NodeId)
            .OrderBy(Item => Item.LineNumber).ToArray();
        if (Dependents.Length == 0) return true;
        string Updated = Change!.Apply(Source);
        var Pins = new List<(string Id, int Row, int Column)>();
        foreach (var Dependent in Dependents)
        {
            if (Grid.RegionOf(Dependent.Id) is not { } Region)
            {
                Change = null; Reason = "Another element is placed relative to this one and its position is not known."; return false;
            }
            string Pin = "at " + Region.StartRow.ToString(CultureInfo.InvariantCulture) + " " + Region.StartColumn.ToString(CultureInfo.InvariantCulture);
            if (!TryRewrite(Updated, Dependent.LineNumber, Pin, out var PinEdit))
            {
                Change = null; Reason = "An element placed relative to this one must have its own declaration line to stay in place."; return false;
            }
            Updated = PinEdit!.Apply(Updated);
            Pins.Add((Dependent.Id, Region.StartRow, Region.StartColumn));
        }
        var Parsed = EnterpriseValidator.Analyze(Updated);
        if (Parsed.HasErrors || Pins.Any(Pin => !Parsed.Document.Elements.TryGetValue(Pin.Id, out var Element)
            ? !(Parsed.Document.Notes.TryGetValue(Pin.Id, out var Note) && Note.PlacementHint is { Kind: PlacementHintKind.At } NoteHint
                && NoteHint.CellRow == Pin.Row && NoteHint.CellColumn == Pin.Column)
            : Element.PlacementHint is not { Kind: PlacementHintKind.At } Placed || Placed.CellRow != Pin.Row || Placed.CellColumn != Pin.Column))
        {
            Change = null; Reason = "Elements placed relative to this one could not be kept in place without changing a declaration."; return false;
        }
        int First = 0, Suffix = 0;
        while (First < Source.Length && First < Updated.Length && Source[First] == Updated[First]) First++;
        while (Suffix < Source.Length - First && Suffix < Updated.Length - First && Source[^(Suffix + 1)] == Updated[^(Suffix + 1)]) Suffix++;
        Change = new(First, Source.Length - First - Suffix, Updated.Substring(First, Updated.Length - First - Suffix), Change.Hint);
        return true;
    }

    private static bool TryCreateCore(string Source, string NodeId, IReadOnlyList<PreviewObject> Items,
        PointF Drop, out Edit? Change, out string Reason, GridSnap? Grid)
    {
        Change = null;
        Reason = "Drop near another element in the same group and layer.";
        if (!float.IsFinite(Drop.X) || !float.IsFinite(Drop.Y))
        {
            return false;
        }

        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        EnterpriseDocument Document = Analysis.Document;
        if (Analysis.HasErrors || !Document.Elements.TryGetValue(NodeId, out EnterpriseElement? Element)
            || Document.Groups.ContainsKey(NodeId))
        {
            Reason = "Move a regular element in a valid Enterprise diagram.";
            return false;
        }
        PreviewObject? Moving = Items.FirstOrDefault(Item => Item.Kind == PreviewObjectKind.Node && Item.Id == NodeId);
        if (Moving == null || Moving.Right <= Moving.Left || Moving.Bottom <= Moving.Top)
        {
            return false;
        }

        var Snapped = Grid?.Snap(NodeId, Drop);
        if (Grid != null && Snapped == null) return false;
        if (Snapped is { Direction: "at", CellRow: { } Row, CellColumn: { } Column })
        {
            string Pin = "at " + Row.ToString(CultureInfo.InvariantCulture) + " " + Column.ToString(CultureInfo.InvariantCulture);
            if (!TryRewrite(Source, Element.LineNumber, Pin, out Change))
            {
                Reason = "Drag supports one declaration per line; edit this placement hint in the source.";
                return false;
            }
            var Parsed = EnterpriseValidator.Analyze(Change!.Apply(Source));
            if (Parsed.HasErrors || !Parsed.Document.Elements.TryGetValue(NodeId, out var Pinned)
                || Pinned.PlacementHint is not { Kind: PlacementHintKind.At } PinHint
                || PinHint.CellRow != Row || PinHint.CellColumn != Column || Pinned.Label != Element.Label || Pinned.ParentId != Element.ParentId
                || !Document.Elements.Keys.OrderBy(Id => Id, StringComparer.Ordinal).SequenceEqual(
                    Parsed.Document.Elements.Keys.OrderBy(Id => Id, StringComparer.Ordinal)))
            {
                Change = null; Reason = "The cell position could not be applied without changing the declaration."; return false;
            }
            Reason = string.Empty; return true;
        }

        EnterpriseLayer Layer = EnterpriseElementCatalog.Elements[Element.ElementType].Layer;
        PreviewObject? Peer = Items.Where(Item => Item.Kind == PreviewObjectKind.Node && Item.Id != NodeId
            && Item.Right > Item.Left && Item.Bottom > Item.Top
            && Document.Elements.TryGetValue(Item.Id, out EnterpriseElement? Other) && !Document.Groups.ContainsKey(Item.Id)
            && Other.ParentId == Element.ParentId && EnterpriseElementCatalog.Elements[Other.ElementType].Layer == Layer
            && !DependsOn(Document, Other.Id, NodeId))
            .OrderBy(Item => DistanceSquared(Drop, Item)).ThenBy(Item => Item.Id, StringComparer.Ordinal).FirstOrDefault();
        if (Peer == null)
        {
            return false;
        }

        double Dx = Drop.X - ((Peer.Left + Peer.Right) / 2);
        double Dy = Drop.Y - ((Peer.Top + Peer.Bottom) / 2);
        double HalfWidth = (Moving.Right - Moving.Left + Peer.Right - Peer.Left) / 2;
        double HalfHeight = (Moving.Bottom - Moving.Top + Peer.Bottom - Peer.Top) / 2;
        bool Horizontal = Math.Abs(Dx) / HalfWidth >= Math.Abs(Dy) / HalfHeight;
        string Direction = Horizontal ? (Dx < 0 ? "left-of" : "right-of") : (Dy < 0 ? "above" : "below");
        double Extra = (Horizontal ? Math.Abs(Dx) - HalfWidth : Math.Abs(Dy) - HalfHeight) - DiagramGridMetrics.NODE_GAP_PX;
        int Offset = (int)Math.Clamp(Math.Round(Extra / DiagramGridMetrics.GRID_UNIT_PX), 0, 10000);
        int? ColumnOffset = null;
        string Hint = Direction + " " + Peer.Id + (Offset == 0 ? string.Empty : " +" + Offset.ToString(CultureInfo.InvariantCulture));
        if (Grid != null)
        {
            if (Snapped == null) return false;
            Peer = Items.First(Item => Item.Id == Snapped.PeerId && Item.Kind == PreviewObjectKind.Node);
            Direction = Snapped.Direction; Offset = Snapped.Offset;
            ColumnOffset = Snapped.ColumnOffset;
            Hint = Direction + " " + Peer.Id + (Offset == 0 ? string.Empty : " +" + Offset.ToString(CultureInfo.InvariantCulture));
            if (ColumnOffset.HasValue)
                Hint = Direction + " " + Peer.Id + " +" + Offset.ToString(CultureInfo.InvariantCulture)
                    + " +" + ColumnOffset.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (!TryRewrite(Source, Element.LineNumber, Hint, out Change))
        {
            Reason = "Drag supports one declaration per line; edit this placement hint in the source.";
            return false;
        }
        EnterpriseAnalysisResult Updated = EnterpriseValidator.Analyze(Change!.Apply(Source));
        PlacementHintKind ExpectedKind = Direction switch
        {
            "left-of" => PlacementHintKind.LeftOf, "right-of" => PlacementHintKind.RightOf,
            "NW" => PlacementHintKind.NorthWest, "NE" => PlacementHintKind.NorthEast,
            "SE" => PlacementHintKind.SouthEast, "SW" => PlacementHintKind.SouthWest,
            "above" => PlacementHintKind.Above, _ => PlacementHintKind.Below
        };
        if (Updated.HasErrors || !Updated.Document.Elements.TryGetValue(NodeId, out EnterpriseElement? After)
            || After.PlacementHint?.PeerId != Peer.Id || After.PlacementHint.Kind != ExpectedKind || After.PlacementHint.Offset != Offset
            || After.PlacementHint.ColumnOffset != ColumnOffset
            || After.Label != Element.Label || After.ParentId != Element.ParentId
            || !Document.Elements.Keys.OrderBy(Id => Id, StringComparer.Ordinal).SequenceEqual(
                Updated.Document.Elements.Keys.OrderBy(Id => Id, StringComparer.Ordinal)))
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
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        while (Seen.Add(Peer))
        {
            if (Peer == Moving)
            {
                return true;
            }

            if (!Document.Elements.TryGetValue(Peer, out EnterpriseElement? Element) || string.IsNullOrEmpty(Element.PlacementHint?.PeerId))
            {
                return false;
            }

            Peer = Element.PlacementHint.PeerId;
        }
        return true;
    }

    private static double DistanceSquared(PointF Point, PreviewObject Item)
    {
        double X = Point.X - ((Item.Left + Item.Right) / 2), Y = Point.Y - ((Item.Top + Item.Bottom) / 2);
        return (X * X) + (Y * Y);
    }

    private static bool TryRewrite(string Source, int LineNumber, string Hint, out Edit? Change)
    {
        Change = null;
        int Start = 0;
        for (int Line = 1; Line < LineNumber; Line++)
        {
            int Next = Source.IndexOf('\n', Start);
            if (Next < 0)
            {
                return false;
            }

            Start = Next + 1;
        }
        int End = Start;
        while (End < Source.Length && Source[End] != '\r' && Source[End] != '\n')
        {
            End++;
        }

        string Text = Source.Substring(Start, End - Start);
        if (!IsSingleStatement(Text))
        {
            return false;
        }

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
                if (Escaped)
                {
                    Escaped = false;
                }
                else if (C == '\\')
                {
                    Escaped = true;
                }
                else if (C == Quote)
                {
                    Quote = '\0';
                }

                continue;
            }
            if (C is '\'' or '"') { Quote = C; continue; }
            if (Text.AsSpan(I).StartsWith(":::".AsSpan(), StringComparison.Ordinal)
                || Text.AsSpan(I).StartsWith("%%".AsSpan(), StringComparison.Ordinal)) { Suffix = I; break; }
            if (char.IsWhiteSpace(C))
            {
                continue;
            }

            int WordStart = I;
            while (I < Text.Length && !char.IsWhiteSpace(Text[I]))
            {
                I++;
            }

            string Word = Text.Substring(WordStart, I - WordStart);
            if (Words >= 2 && PreviousWord != "in" && (Word is "above" or "below" or "left-of" or "right-of" or "push" or "at"
                || Word.ToUpperInvariant() is "NW" or "NE" or "SE" or "SW"))
            {
                OldHintStart = WordStart;
                break;
            }
            PreviousWord = Word; Words++; I--;
        }
        if (Quote != '\0')
        {
            return false;
        }
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
        if (Suffix < Text.Length)
        {
            Replacement += " " + Text.Substring(Suffix);
        }

        Change = new(Start, End - Start, Replacement, Hint);
        return true;
    }

    private static bool IsSingleStatement(string Text)
    {
        char Quote = '\0';
        for (int I = 0; I < Text.Length; I++)
        {
            char C = Text[I];
            if (Quote != '\0')
            {
                if (C == '\\')
                {
                    I++;
                }
                else if (C == Quote)
                {
                    Quote = '\0';
                }
            }
            else if (C is '\'' or '"')
            {
                Quote = C;
            }
            else if (C == ';')
            {
                return false;
            }
            else if (C == '%' && I + 1 < Text.Length && Text[I + 1] == '%')
            {
                break;
            }
        }
        return Quote == '\0';
    }
}
