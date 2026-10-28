using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

/// <summary>Detached endpoint inventory and a single source replacement; pointer motion does no analysis or routing.</summary>
internal sealed class PreviewRelationshipMove
{
    internal sealed record Target(string NodeId, LayoutHintDirection Direction, int Step, PointF Point);
    internal sealed record Edit(int Start, int Length, string Replacement);
    private readonly EnterpriseDocument Document;
    private readonly int EdgeIndex;
    private readonly Target[] OriginalEnds;
    private readonly IReadOnlyList<Target>[] Eligible;
    internal string SourceText { get; }
    internal string DocumentId { get; }
    internal string Identity { get; }
    internal int LineNumber { get; }
    internal Target Original(bool Source) => OriginalEnds[Source ? 0 : 1];
    internal IReadOnlyList<Target> Targets(bool Source) => Eligible[Source ? 0 : 1];

    private PreviewRelationshipMove(string Source, EnterpriseDocument Document, DiagramSourceUpdateService.Snapshot Snapshot,
        int EdgeIndex, int Line, Target[] Ends, IReadOnlyList<Target>[] Eligible)
    {
        SourceText = Source; this.Document = Document; DocumentId = Snapshot.DocumentId;
        this.EdgeIndex = EdgeIndex; LineNumber = Line; OriginalEnds = Ends; this.Eligible = Eligible;
        Identity = Snapshot.Identities.Bindings.Single(Binding => Binding.ParsedEdgeIndex == EdgeIndex).Id;
    }

    internal static int IndexAtLine(EnterpriseDocument Document, int Line)
    {
        int[] Lines = [.. Document.Relationships.Where(Edge => !Edge.HiddenByNesting).Select(Edge => Edge.LineNumber),
            .. Document.PlainConnections.Select(Edge => Edge.LineNumber)];
        return Array.IndexOf(Lines, Line);
    }

    internal static PreviewRelationshipMove? Create(string Source, PreviewObject Item, IReadOnlyList<PreviewObject> Items,
        DiagramSourceUpdateService.Snapshot Snapshot)
    {
        if (Item.Kind != PreviewObjectKind.Relationship || Snapshot.IsStale || Snapshot.SourceText != Source) return null;
        var Analysis = EnterpriseValidator.Analyze(Source);
        if (Analysis.HasErrors) return null;
        var Document = Analysis.Document;
        int Index = IndexAtLine(Document, Item.LineNumber);
        if (Index < 0 || Index >= Snapshot.Model.Edges.Count) return null;
        var Edge = Snapshot.Model.Edges[Index];
        if (Edge.SourceId != Item.SourceId || Edge.TargetId != Item.TargetId || Edge.SourceId == Edge.TargetId) return null;
        var Route = Snapshot.Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == Edge.Id);
        if (Route == null || Route.Points.Count < 2) return null;
        var Boxes = Snapshot.Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        var ShownNodes = Items.Where(Item => Item.Kind == PreviewObjectKind.Node).ToDictionary(Item => Item.Id, StringComparer.Ordinal);
        Dictionary<string, Target[]> Inventory = new(StringComparer.Ordinal);
        foreach (var Node in Snapshot.Model.Nodes)
        {
            if (!ShownNodes.TryGetValue(Node.Id, out var Shown) || !Boxes.TryGetValue(Node.Id, out var Box)) continue;
            bool Ungrouped = Document.Elements.TryGetValue(Node.Id, out var Element) ? string.IsNullOrEmpty(Element.ParentId)
                : Document.Notes.TryGetValue(Node.Id, out var Note) && string.IsNullOrEmpty(Note.ParentId);
            if (!Ungrouped) continue;
            Inventory[Node.Id] = [.. FlowchartConnectionPoints.ForNode(Node, Box).Select(Point => new Target(Node.Id, Point.Direction, Point.Step,
                new((float)((Shown.Left + Shown.Right) / 2 + Point.X - Box.CenterX),
                    (float)((Shown.Top + Shown.Bottom) / 2 - Point.Y + Box.CenterY))))];
        }
        Target? End(bool SourceEnd)
        {
            string Id = SourceEnd ? Edge.SourceId : Edge.TargetId;
            if (!Inventory.TryGetValue(Id, out var Points) || !Boxes.TryGetValue(Id, out var Box)) return null;
            var Shown = ShownNodes[Id];
            var P = SourceEnd ? Route.Points[0] : Route.Points[^1];
            double X = (Shown.Left + Shown.Right) / 2 + P.X - Box.CenterX;
            double Y = (Shown.Top + Shown.Bottom) / 2 - P.Y + Box.CenterY;
            return Points.FirstOrDefault(Point => Math.Abs(Point.Point.X - X) <= 0.51 && Math.Abs(Point.Point.Y - Y) <= 0.51);
        }
        var Start = End(true); var Finish = End(false);
        if (Start == null || Finish == null) return null;
        var Visible = Document.Relationships.Where(Edge => !Edge.HiddenByNesting).ToArray();
        var Relationship = Index < Visible.Length ? Visible[Index] : null;
        bool Allowed(string Id, bool SourceEnd)
        {
            string SourceId = SourceEnd ? Id : Edge.SourceId, TargetId = SourceEnd ? Edge.TargetId : Id;
            if (SourceId == TargetId) return false;
            if (Relationship == null)
                return (Document.Notes.ContainsKey(SourceId) || Document.Notes.ContainsKey(TargetId))
                    && !Document.PlainConnections.Any(Line => Line.LineNumber != Item.LineNumber && Line.SourceId == SourceId && Line.TargetId == TargetId);
            if (!Document.Elements.ContainsKey(SourceId) || !Document.Elements.ContainsKey(TargetId)) return false;
            string SourceType = EnterpriseElementCatalog.GetKebabName(Document.Elements[SourceId].ElementType);
            string TargetType = EnterpriseElementCatalog.GetKebabName(Document.Elements[TargetId].ElementType);
            return EnterpriseRulesCatalog.Instance.GetAllowedLetters(Document.Version, SourceType, TargetType)
                .Contains(EnterpriseElementCatalog.GetLetter(Relationship.Kind))
                && !Document.Relationships.Any(Other => Other.LineNumber != Item.LineNumber && Other.SourceId == SourceId && Other.TargetId == TargetId
                    && Other.Kind == Relationship.Kind && Other.AccessKind == Relationship.AccessKind
                    && Other.InfluenceStrength == Relationship.InfluenceStrength && Other.DirectedAssociation == Relationship.DirectedAssociation);
        }
        IReadOnlyList<Target> Candidates(bool SourceEnd)
        {
            // Existing explicit reservations are ineligible; ordinary siblings can be redistributed by the worker.
            var Reserved = Snapshot.Model.Edges.Where(Other => Other.Id != Edge.Id && Other.RoutingHint?.IsActive == true)
                .SelectMany(Other => new[] { (Id: Other.SourceId, Side: Other.RoutingHint!.Leave, Step: Other.RoutingHint.LeavePoint),
                    (Id: Other.TargetId, Side: Other.RoutingHint!.Enter, Step: Other.RoutingHint.EnterPoint) }).ToHashSet();
            return [.. Inventory.Where(Pair => Allowed(Pair.Key, SourceEnd)).SelectMany(Pair => Pair.Value)
                .Where(Point => !Reserved.Contains((Point.NodeId, (LayoutHintDirection?)Point.Direction, (int?)Point.Step)))];
        }
        return new(Source, Document, Snapshot, Index, Item.LineNumber, [Start, Finish], [Candidates(true), Candidates(false)]);
    }

    internal bool TryCreateEdit(bool SourceEnd, Target Target, out Edit? Change, out string Reason)
    {
        Change = null; Reason = "Choose an eligible connection point.";
        if (!Targets(SourceEnd).Contains(Target) || Target == Original(SourceEnd)) return false;
        if (!PreviewSelection.TryGetLineSelection(SourceText, LineNumber, out int Start, out int Length)) return false;
        string Line = SourceText.Substring(Start, Length);
        const string Id = @"[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*";
        var Match = Regex.Match(Line, @"^(?<indent>\s*)(?<source>" + Id + @")(?<link>\s+(?:-[A-Za-z]+(?:\([^)]*\))?-+>|-{3,})\s+)(?<target>" + Id + @")(?<suffix>.*)$");
        if (!Match.Success) { Reason = "Endpoint editing requires one relationship declaration per line."; return false; }
        var Visible = Document.Relationships.Where(Edge => !Edge.HiddenByNesting).ToArray();
        var Hint = EdgeIndex < Visible.Length ? Visible[EdgeIndex].RoutingHint : Document.PlainConnections[EdgeIndex - Visible.Length].RoutingHint;
        var Leave = SourceEnd ? Target : Original(true); var Enter = SourceEnd ? Original(false) : Target;
        static string Letter(LayoutHintDirection Direction) => Direction switch { LayoutHintDirection.North => "N", LayoutHintDirection.South => "S",
            LayoutHintDirection.East => "E", _ => "W" };
        static string EndHint(Target Point) => Letter(Point.Direction) + "@" + Point.Step.ToString(CultureInfo.InvariantCulture);
        string Suffix = Regex.Replace(Match.Groups["suffix"].Value, @"\s+\[[^\]]*\]\s*$", string.Empty).TrimEnd();
        string Replacement = Match.Groups["indent"].Value + Leave.NodeId + Match.Groups["link"].Value + Enter.NodeId + Suffix
            + " [leave " + EndHint(Leave) + ", enter " + EndHint(Enter)
            + (Hint?.IsActive == true && Hint.Travel.HasValue ? ", travel " + Letter(Hint.Travel.Value) : string.Empty) + "]";
        string Updated = SourceText[..Start] + Replacement + SourceText[(Start + Length)..];
        var Analysis = EnterpriseValidator.Analyze(Updated);
        if (Analysis.HasErrors || Analysis.Document.Relationships.Count != Document.Relationships.Count
            || Analysis.Document.PlainConnections.Count != Document.PlainConnections.Count)
        { Reason = "The relationship cannot reconnect to that element."; return false; }
        Change = new(Start, Length, Replacement); Reason = string.Empty; return true;
    }
}
