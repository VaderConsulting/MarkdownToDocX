using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

/// <summary>Complete, validated relationship source commands; source labels do not determine capability.</summary>
internal static class PreviewRelationshipCommands
{
    internal sealed record Edit(int Start, int Length, string Replacement, int RelationshipLine, string Updated);
    internal sealed record Kind(EnterpriseRelationshipKind? Relationship, string Name);

    internal static IReadOnlyList<Kind> AllowedKinds(EnterpriseDocument Document, string Source, string Target)
    {
        if (Source == Target || !Ungrouped(Document, Source) || !Ungrouped(Document, Target)) return [];
        if (Document.Notes.ContainsKey(Source) || Document.Notes.ContainsKey(Target))
            return Document.PlainConnections.Any(Line => Line.SourceId == Source && Line.TargetId == Target) ? [] : [new(null, "Note connection")];
        string Letters = EnterpriseRulesCatalog.Instance.GetAllowedLetters(Document.Version,
            EnterpriseElementCatalog.GetKebabName(Document.Elements[Source].ElementType), EnterpriseElementCatalog.GetKebabName(Document.Elements[Target].ElementType));
        return [.. EnterpriseElementCatalog.Relationships.Values.Where(Kind => Letters.Contains(Kind.Letter)
            && !(Kind.Kind == EnterpriseRelationshipKind.Triggering && Document.Elements[Source].ElementType == EnterpriseElementType.Plateau
                && Document.Elements[Target].ElementType == EnterpriseElementType.Plateau)
            && !Document.Relationships.Any(Edge => Edge.SourceId == Source && Edge.TargetId == Target && Edge.Kind == Kind.Kind
                && (Kind.Kind != EnterpriseRelationshipKind.Access || Edge.AccessKind == EnterpriseAccessKind.Write)
                && (Kind.Kind != EnterpriseRelationshipKind.Influence || string.IsNullOrEmpty(Edge.InfluenceStrength))
                && (Kind.Kind != EnterpriseRelationshipKind.Association || !Edge.DirectedAssociation)))
            .OrderBy(Kind => Kind.Name, StringComparer.Ordinal).Select(Kind => new Kind(Kind.Kind, Kind.Name))];
    }

    private static bool Ungrouped(EnterpriseDocument Document, string Id) => Document.Elements.TryGetValue(Id, out var Element)
        ? string.IsNullOrEmpty(Element.ParentId) && !Document.Elements.Values.Any(Child => Child.ParentId == Id)
        : Document.Notes.TryGetValue(Id, out var Note) && string.IsNullOrEmpty(Note.ParentId);

    internal static bool TryAdd(string Source, string From, string To, Kind Kind, DiagramSourceUpdateService.Snapshot Snapshot,
        out Edit? Change, out string Reason)
    {
        Change = null; Reason = "Choose two eligible nodes, source first, and an allowed relationship type.";
        if (Snapshot.IsStale || Snapshot.SourceText != Source) return false;
        var Analysis = EnterpriseValidator.Analyze(Source);
        if (Analysis.HasErrors || !AllowedKinds(Analysis.Document, From, To).Any(Allowed => Allowed.Relationship == Kind.Relationship)) return false;
        var Boxes = Snapshot.Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        var Nodes = Snapshot.Model.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        if (!Boxes.TryGetValue(From, out var A) || !Boxes.TryGetValue(To, out var B)) return false;
        FlowchartConnectionPoint? FreePoint(string Id, FlowchartLayoutNodeBounds Box, FlowchartLayoutNodeBounds Other)
        {
            var Occupied = Snapshot.Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2)
                .SelectMany(Route => new[] { (Id: Route.SourceId, Point: Route.Points[0]), (Id: Route.TargetId, Point: Route.Points[^1]) })
                .Where(End => End.Id == Id).Select(End => End.Point).ToArray();
            double Dx = Other.CenterX - Box.CenterX, Dy = Other.CenterY - Box.CenterY;
            var Facing = Math.Abs(Dx) >= Math.Abs(Dy) ? Dx >= 0 ? LayoutHintDirection.East : LayoutHintDirection.West
                : Dy >= 0 ? LayoutHintDirection.North : LayoutHintDirection.South;
            return FlowchartConnectionPoints.ForNode(Nodes[Id], Box)
                .Where(Point => !Occupied.Any(End => Math.Abs(End.X - Point.X) < 0.51 && Math.Abs(End.Y - Point.Y) < 0.51))
                .OrderBy(Point => Point.Direction != Facing).ThenBy(Point => Math.Abs(Point.Step)).ThenBy(Point => Point.Step)
                .Select(Point => (FlowchartConnectionPoint?)Point).FirstOrDefault();
        }
        var Leave = FreePoint(From, A, B); var Enter = FreePoint(To, B, A);
        if (Leave == null || Enter == null) { Reason = "The selected nodes have no free eligible connection points."; return false; }
        static string Endpoint(FlowchartConnectionPoint Point) => (Point.Direction switch { LayoutHintDirection.North => "N", LayoutHintDirection.South => "S",
            LayoutHintDirection.East => "E", _ => "W" }) + "@" + Point.Step.ToString(CultureInfo.InvariantCulture);
        string Line = From + (Kind.Relationship.HasValue ? " -" + EnterpriseElementCatalog.GetRelationshipName(Kind.Relationship.Value) + "-> " : " --- ")
            + To + " [leave " + Endpoint(Leave.Value) + ", enter " + Endpoint(Enter.Value) + "]";
        string Newline = Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string Prefix = Source.EndsWith('\n') ? string.Empty : Newline;
        string Updated = Source + Prefix + Line + Newline;
        var Parsed = EnterpriseValidator.Analyze(Updated);
        if (Parsed.HasErrors) { Reason = "That relationship is not valid for the selected nodes."; return false; }
        int LineNumber = Source.Count(Character => Character == '\n') + 1 + (Prefix.Length == 0 ? 0 : 1);
        Change = new(Source.Length, 0, Prefix + Line + Newline, LineNumber, Updated); Reason = string.Empty; return true;
    }

    internal static bool TryDelete(string Source, PreviewObject Item, out Edit? Change, out string Reason)
    {
        Change = null; Reason = "Select a current relationship declaration.";
        if (Item.Kind != PreviewObjectKind.Relationship) return false;
        var Analysis = EnterpriseValidator.Analyze(Source);
        if (Analysis.HasErrors || PreviewRelationshipMove.IndexAtLine(Analysis.Document, Item.LineNumber) < 0
            || !PreviewSelection.TryGetLineSelection(Source, Item.LineNumber, out int Start, out int Length)) return false;
        int RemovedIndex = Analysis.Document.Relationships.FindIndex(Edge => Edge.LineNumber == Item.LineNumber);
        if (RemovedIndex >= 0)
        {
            var Edge = Analysis.Document.Relationships[RemovedIndex];
            if (Edge.SourceId != Item.SourceId || Edge.TargetId != Item.TargetId) return false;
            if (!string.Equals(Item.RelationshipKind, EnterpriseElementCatalog.GetRelationshipName(Edge.Kind), StringComparison.OrdinalIgnoreCase)) return false;
            if (Edge.Kind == EnterpriseRelationshipKind.Triggering
                && Analysis.Document.Elements.TryGetValue(Edge.SourceId, out var SourceElement) && SourceElement.ElementType == EnterpriseElementType.Plateau
                && Analysis.Document.Elements.TryGetValue(Edge.TargetId, out var TargetElement) && TargetElement.ElementType == EnterpriseElementType.Plateau)
            { Reason = "Changing this timeline requires Auto layout."; return false; }
        }
        else if (!Analysis.Document.PlainConnections.Any(Edge => Edge.LineNumber == Item.LineNumber && Edge.SourceId == Item.SourceId && Edge.TargetId == Item.TargetId)) return false;
        int End = Start + Length;
        if (End < Source.Length && Source[End] == '\r') End++;
        if (End < Source.Length && Source[End] == '\n') End++;
        string Updated = Source.Remove(Start, End - Start);
        if (RemovedIndex >= 0)
        {
            Updated = Regex.Replace(Updated, @"(?m)^(?<prefix>[ \t]*linkStyle[ \t]+)(?<indexes>\d+(?:,\d+)*)(?<tail>[ \t]+[^\r\n]*)(?<cr>\r?)$", Match =>
            {
                var Indexes = Match.Groups["indexes"].Value.Split(',').Select(Value => int.Parse(Value, CultureInfo.InvariantCulture))
                    .Where(Index => Index != RemovedIndex).Select(Index => Index > RemovedIndex ? Index - 1 : Index).ToArray();
                return Indexes.Length == 0 ? Match.Groups["cr"].Value : Match.Groups["prefix"].Value + string.Join(",", Indexes) + Match.Groups["tail"].Value + Match.Groups["cr"].Value;
            }, RegexOptions.IgnoreCase);
        }
        var Parsed = EnterpriseValidator.Analyze(Updated);
        if (Parsed.HasErrors || Parsed.Document.Relationships.Count + Parsed.Document.PlainConnections.Count
            != Analysis.Document.Relationships.Count + Analysis.Document.PlainConnections.Count - 1)
        { Reason = "The relationship could not be removed without changing another declaration."; return false; }
        int First = 0, Suffix = 0;
        while (First < Source.Length && First < Updated.Length && Source[First] == Updated[First]) First++;
        while (Suffix < Source.Length - First && Suffix < Updated.Length - First && Source[^(Suffix + 1)] == Updated[^(Suffix + 1)]) Suffix++;
        Change = new(First, Source.Length - First - Suffix, Updated.Substring(First, Updated.Length - First - Suffix), Item.LineNumber, Updated);
        Reason = string.Empty; return true;
    }
}
