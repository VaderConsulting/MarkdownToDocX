using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Diagnostics;

namespace MermaidRecognition.Layout;

internal enum DiagramSourceUpdateKind { MetadataOnly, Redraw, LocalRouting, RebuildRequired, Rejected }

/// <summary>
/// One publication boundary for parsed source, stable identities and retained geometry.
/// Hosts observe every text revision (including invalid text), prepare after analysis,
/// and commit before drawing. This service never starts initial layout or a rebuild.
/// </summary>
internal sealed class DiagramSourceUpdateService
{
    private readonly object Gate = new();
    private Accepted Committed;
    private long LatestSourceRevision;
    private long Revision;
    private string? InvalidationReason;

    internal DiagramSourceUpdateService(string DocumentId, long SourceRevision, string SourceText,
        DiagramContract Model, FlowchartLayoutResult Layout)
    {
        ArgumentNullException.ThrowIfNull(SourceText);
        var Identities = DiagramRelationshipIdentityMap.Capture(DocumentId, SourceRevision, Model);
        DiagramContract Bound = DiagramSourceModel.Bind(Model, Identities);
        // Detach before rebinding parser IDs; neither the caller's layout nor model is edited.
        var Copy = DiagramGridLayout.RetainedRoutingState.Capture(Model, Layout).Layout;
        var Routes = Copy.EdgeRoutes.ToDictionary(Route => Route.EdgeId, StringComparer.Ordinal);
        foreach (var Binding in Identities.Bindings)
        {
            if (Binding.ParserId == null || !Routes.TryGetValue(Binding.ParserId, out var Route))
            {
                throw new ArgumentException("Source capture requires a route for each unique parser relationship ID.", nameof(Layout));
            }
            Route.EdgeId = Binding.Id;
        }
        if (Copy.EdgeRoutes.Count != Identities.Bindings.Count)
        {
            throw new ArgumentException("Source capture requires matching relationship and route inventories.", nameof(Layout));
        }
        Copy.ResolvedEdgeGuides = null;
        Committed = new(SourceRevision, SourceText, Bound, Identities,
            DiagramGridLayout.RetainedRoutingState.Capture(Bound, Copy));
        this.DocumentId = DocumentId;
        LatestSourceRevision = SourceRevision;
    }

    internal string DocumentId { get; }
    internal DiagramLayoutOptions Options => Committed.Geometry.Options;

    /// <summary>Document/options switches and explicit Auto layout retire this instance permanently.</summary>
    internal void Invalidate(string Reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(Reason);
        lock (Gate) { InvalidationReason ??= Reason; }
    }

    internal void ObserveSourceRevision(long SourceRevision)
    {
        lock (Gate)
        {
            if (SourceRevision < LatestSourceRevision) { throw new ArgumentOutOfRangeException(nameof(SourceRevision)); }
            LatestSourceRevision = SourceRevision;
        }
    }

    internal Snapshot CopySnapshot()
    {
        lock (Gate)
        {
            return CopySnapshotCore(Committed, Revision);
        }
    }

    // Draw/rasterise an isolated candidate before publication. A drawing failure cannot
    // accept source/geometry that the host has never successfully displayed.
    internal Snapshot CopyCandidateSnapshot(Update Candidate)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(Candidate.Owner, this) || Candidate.Value == null)
                throw new ArgumentException("No drawable candidate belongs to this session.", nameof(Candidate));
            return CopySnapshotCore(Candidate.Value, Candidate.BaseRevision + 1);
        }
    }

    private Snapshot CopySnapshotCore(Accepted Value, long AcceptedRevision)
    {
        DiagramContract Model = DiagramSourceModel.Copy(Value.Model);
        FlowchartLayoutResult Layout = Value.Geometry.Fork().Layout;
        // A renderer must see full presentation/source facts on its accepted guides too.
        var Edges = Model.Edges.ToDictionary(Edge => Edge.Id!, StringComparer.Ordinal);
        Layout.ResolvedEdgeGuides = Layout.ResolvedEdgeGuides?.Select(Guide =>
            new FlowchartResolvedGuide(Edges[Guide.Edge.Id!], Guide.Points.ToList(), Guide.RouteKind)).ToList();
        Layout.RenderContext.CachedRouteContract = Model;
        Layout.RenderContext.CachedRouteGuides = Layout.ResolvedEdgeGuides;
        return new(DocumentId, Value.SourceRevision, LatestSourceRevision, AcceptedRevision,
            Value.SourceText, Model, Value.Identities, Layout, InvalidationReason);
    }

    internal sealed record Snapshot(string DocumentId, long SourceRevision, long ObservedSourceRevision,
        long Revision, string SourceText, DiagramContract Model, DiagramRelationshipIdentityMap Identities,
        FlowchartLayoutResult Layout, string? InvalidationReason)
    {
        internal bool IsStale => SourceRevision != ObservedSourceRevision || InvalidationReason != null;
    }

    internal Update Prepare(string DocumentId, long SourceRevision, string SourceText, DiagramContract Model,
        DiagramLayoutOptions Options, int? RouteLimit = null, bool CapturePlacementOrigins = false)
    {
        ArgumentNullException.ThrowIfNull(SourceText);
        ArgumentNullException.ThrowIfNull(Model);
        int Limit = RouteLimit ?? FlowchartLayoutRules.RepairChainLines;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Limit);
        Accepted Previous;
        long BaseRevision;
        lock (Gate)
        {
            Previous = Committed; BaseRevision = Revision;
            if (InvalidationReason != null) { return Failure(DiagramSourceUpdateKind.Rejected, "The source session was invalidated: " + InvalidationReason); }
            if (DocumentId != this.DocumentId) { return Failure(DiagramSourceUpdateKind.Rejected, "The request belongs to another document."); }
            if (SourceRevision != LatestSourceRevision || SourceRevision <= Previous.SourceRevision)
            {
                return Failure(DiagramSourceUpdateKind.Rejected, "The source revision is stale or already accepted.");
            }
            if (Options != Previous.Geometry.Options)
            {
                return Failure(DiagramSourceUpdateKind.RebuildRequired, "Layout options changed; start a new source session.");
            }
        }
        if (Model.Diagnostics.Any(Item => Item.Severity == MermaidDiagnosticSeverity.Error))
        {
            return Failure(DiagramSourceUpdateKind.Rejected, "The parsed model contains errors; retain the last accepted preview.");
        }
        DiagramContract Parsed = DiagramSourceModel.Copy(Model);
        var Reconciliation = Previous.Identities.Reconcile(SourceRevision, Parsed);
        if (Reconciliation.Next is not { } Identities)
        {
            return Failure(DiagramSourceUpdateKind.Rejected, Reconciliation.FailureReason!);
        }
        DiagramContract Bound = DiagramSourceModel.Bind(Parsed, Identities);
        var Classification = Classify(Previous.Model, Bound);
        if (Classification.Reason != null) { return Failure(DiagramSourceUpdateKind.RebuildRequired, Classification.Reason); }
        var Geometry = Previous.Geometry.Fork();
        string[] Affected = [], Rebuilt = [];
        try
        {
            if (Classification.Kind == DiagramSourceUpdateKind.LocalRouting)
            {
                string? Reason = Geometry.TrySourceUpdate(Bound, Classification.Hints, Classification.Edges,
                    Limit, out Affected, out Rebuilt, CapturePlacementOrigins);
                if (Reason != null) { return Failure(DiagramSourceUpdateKind.Rejected, Reason, Affected, Rebuilt); }
            }
            else { Geometry.ReplaceSourceMetadata(Bound); }
        }
        catch (InvalidOperationException Error)
        {
            return Failure(DiagramSourceUpdateKind.Rejected, "Local source update failed: " + Error.Message, Affected, Rebuilt);
        }
        catch (OverflowException)
        {
            return Failure(DiagramSourceUpdateKind.Rejected, "The source update exceeds the supported cell coordinate range.", Affected, Rebuilt);
        }
        return new(this, SourceRevision, BaseRevision, Classification.Kind,
            new(SourceRevision, SourceText, Bound, Identities, Geometry), Affected, Rebuilt,
            Reconciliation.Notices.ToArray(), null);

        Update Failure(DiagramSourceUpdateKind Kind, string Reason, string[]? Affected = null, string[]? Rebuilt = null)
            => new(this, SourceRevision, BaseRevision, Kind, null, Affected ?? [], Rebuilt ?? [], [], Reason);
    }

    internal bool TryCommit(Update Candidate, out string? Reason)
    {
        ArgumentNullException.ThrowIfNull(Candidate);
        lock (Gate)
        {
            Reason = Candidate.FailureReason;
            if (!ReferenceEquals(Candidate.Owner, this)) { Reason = "The update belongs to another source session."; }
            else if (InvalidationReason != null) { Reason = "The source session was invalidated: " + InvalidationReason; }
            else if (Candidate.SourceRevision != LatestSourceRevision || Candidate.BaseRevision != Revision)
            {
                Reason = "The update was superseded by a source or accepted-layout revision.";
            }
            if (Reason != null || Candidate.Value == null) { return false; }
            Committed = Candidate.Value;
            Revision++;
            return true;
        }
    }

    internal sealed record Accepted(long SourceRevision, string SourceText, DiagramContract Model,
        DiagramRelationshipIdentityMap Identities, DiagramGridLayout.RetainedRoutingState Geometry);

    internal sealed class Update
    {
        internal Update(DiagramSourceUpdateService Owner, long SourceRevision, long BaseRevision,
            DiagramSourceUpdateKind Kind, Accepted? Value, string[] Affected, string[] Rebuilt, string[] Notices, string? Reason)
        {
            this.Owner = Owner; this.SourceRevision = SourceRevision; this.BaseRevision = BaseRevision;
            this.Kind = Kind; this.Value = Value; FailureReason = Reason;
            AffectedRouteIds = Array.AsReadOnly(Affected); RebuiltRouteIds = Array.AsReadOnly(Rebuilt);
            this.Notices = Array.AsReadOnly(Notices);
        }
        internal DiagramSourceUpdateService Owner { get; }
        internal long SourceRevision { get; }
        internal long BaseRevision { get; }
        internal Accepted? Value { get; }
        internal DiagramSourceUpdateKind Kind { get; }
        internal string? FailureReason { get; }
        internal IReadOnlyList<string> AffectedRouteIds { get; }
        internal IReadOnlyList<string> RebuiltRouteIds { get; }
        internal IReadOnlyList<string> Notices { get; }
    }

    private sealed record Classification(DiagramSourceUpdateKind Kind, string[] Hints, string[] Edges, string? Reason = null);

    private static Classification Classify(DiagramContract Old, DiagramContract New)
    {
        Classification Rebuild(string Reason) => new(DiagramSourceUpdateKind.RebuildRequired, [], [], Reason);
        if (Old.Kind != New.Kind || Old.Direction != New.Direction || Old.LayoutMode != New.LayoutMode
            || Old.RemarkStatus != New.RemarkStatus || Json(Config(Old)) != Json(Config(New)))
        {
            return Rebuild("Diagram kind, direction, layout mode or configuration changed.");
        }
        if (Json(Groups(Old)) != Json(Groups(New))) { return Rebuild("Group structure, title or placement changed."); }
        var OldNodes = Old.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        if (New.Nodes.Count != OldNodes.Count || New.Nodes.Any(Node => !OldNodes.ContainsKey(Node.Id)))
        {
            return Rebuild("Node insertion or removal requires an explicit construction operation.");
        }
        List<string> Hints = [];
        foreach (NodeContract Node in New.Nodes)
        {
            NodeContract Previous = OldNodes[Node.Id];
            if (Json(NodeGeometry(Previous)) != Json(NodeGeometry(Node))
                || (!ReferenceEquals(Previous.GeometryProvider, Node.GeometryProvider)
                    && !(Previous.GeometryProvider is Enterprise.EnterpriseDecoratedNodeGeometry
                        && Node.GeometryProvider is Enterprise.EnterpriseDecoratedNodeGeometry)))
            {
                return Rebuild("Node size, label, shape, membership or font changed: " + Node.Id);
            }
            if (Json(Hint(Previous.PlacementHint)) != Json(Hint(Node.PlacementHint))) { Hints.Add(Node.Id); }
        }
        var OldEdges = Old.Edges.ToDictionary(Edge => Edge.Id!, StringComparer.Ordinal);
        if (OldEdges.Keys.Except(New.Edges.Select(Edge => Edge.Id!), StringComparer.Ordinal).Any())
        {
            return Rebuild("Relationship removal requires an explicit removal operation.");
        }
        List<string> Edges = [];
        foreach (EdgeContract Edge in New.Edges)
        {
            if (OldEdges.TryGetValue(Edge.Id!, out var Previous))
            {
                if (Json(EdgeGeometry(Previous)) != Json(EdgeGeometry(Edge)))
                {
                    return Rebuild("Relationship label, kind, marker or layout constraint changed: " + Edge.Id);
                }
                if (Previous.SourceId == Edge.SourceId && Previous.TargetId == Edge.TargetId
                    && Json(Ports(Previous.RoutingHint)) == Json(Ports(Edge.RoutingHint))) { continue; }
            }
            if ((Previous == null || Previous.SourceId != Edge.SourceId || Previous.TargetId != Edge.TargetId)
                && (IsTimelineConstraint(New, Edge) || (Previous != null && IsTimelineConstraint(Old, Previous))))
            {
                return Rebuild("Plateau triggering topology changes require a new semantic constraint session.");
            }
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                return Rebuild("Invisible relationships require a separate construction operation.");
            }
            Edges.Add(Edge.Id!);
        }
        if (Json(StyleGeometry(Old)) != Json(StyleGeometry(New))) { return Rebuild("Presentation fonts, line widths or curves changed."); }
        return new(Hints.Count != 0 || Edges.Count != 0 ? DiagramSourceUpdateKind.LocalRouting
            : Json(Paint(Old)) != Json(Paint(New)) ? DiagramSourceUpdateKind.Redraw : DiagramSourceUpdateKind.MetadataOnly,
            Hints.ToArray(), Edges.ToArray());
    }

    private static string Json(object? Value) => JsonSerializer.Serialize(Value);
    private static bool IsTimelineConstraint(DiagramContract Model, EdgeContract Edge)
        => Edge.Metadata.TryGetValue("enterpriseRelationshipKind", out var Kind) && Kind is RawMetadata { RawValue: "Triggering" }
            && Model.Nodes.Any(Node => Node.Id == Edge.SourceId && Node.Metadata.TryGetValue("enterpriseElementType", out var Type)
                && Type is RawMetadata { RawValue: "Plateau" })
            && Model.Nodes.Any(Node => Node.Id == Edge.TargetId && Node.Metadata.TryGetValue("enterpriseElementType", out var Type)
                && Type is RawMetadata { RawValue: "Plateau" });
    private static object Config(DiagramContract Model) => Model.Configuration.EffectiveValues.OrderBy(Pair => Pair.Key, StringComparer.Ordinal);
    private static object Groups(DiagramContract Model) => Model.Groups.OrderBy(Group => Group.Id, StringComparer.Ordinal)
        .Select(Group => new { Group.Id, Group.Title, Group.LabelType, Group.ParentGroupId, Group.Direction,
            Nodes = Group.ChildNodeIds.Order(StringComparer.Ordinal), Groups = Group.ChildGroupIds.Order(StringComparer.Ordinal),
            Hint = Hint(Group.PlacementHint) });
    private static object NodeGeometry(NodeContract Node) => new { Node.Label, Node.LabelType, Node.Shape, Node.ShapeSource,
        Node.LayoutWidth, Node.LayoutHeight, Node.ParentGroupId, Metadata = Metadata(Node.Metadata), Classes = Node.Classes.Order(StringComparer.Ordinal),
        Font = Font(Node.InlineStyle) };
    private static object EdgeGeometry(EdgeContract Edge) => new { Edge.Label, Edge.LabelType, Edge.MinLength,
        Edge.MarkerStart, Edge.MarkerEnd, Edge.Stroke, Edge.Curve, Metadata = Metadata(Edge.Metadata), Classes = Edge.Classes.Order(StringComparer.Ordinal) };
    private static object Metadata(IReadOnlyDictionary<string, MermaidMetadataValue> Values) => Values.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
        .Where(Pair => Pair.Value is not AnimationMetadata).Select(Pair => new { Pair.Key, Type = Pair.Value.GetType().FullName,
            Value = JsonSerializer.Serialize(Pair.Value, Pair.Value.GetType()) });
    private static object? Hint(PlacementHintContract? Value) => Value?.IsActive != true ? null
        : new { Value.Kind, Value.PeerId, Value.Offset, Value.ColumnOffset, Value.Direction, Value.CellRow, Value.CellColumn };
    private static object? Ports(RoutingHintContract? Value) => Value?.IsActive != true ? null
        : new { Value.Leave, Value.Enter, Value.Travel };
    private static object Font(RecognitionStyle Style) => new { Style.FontFamily, Style.FontSize, Style.FontWeight, Style.StrokeWidth };
    private static object StyleGeometry(DiagramContract Model) => new
    {
        Classes = Model.ClassDefs.OrderBy(Class => Class.ClassId, StringComparer.Ordinal).Select(Class => new { Class.ClassId, Font = Font(Class.Style) }),
        Links = Model.LinkStyles.Select(Link => new { Link.IsDefault, Link.EdgeIndexes, Link.Curve, Font = Font(Link.Style) })
    };
    private static object Paint(DiagramContract Model) => new
    {
        Nodes = Model.Nodes.OrderBy(Node => Node.Id, StringComparer.Ordinal).Select(Node => new { Node.Id, Node.InlineStyle }),
        Classes = Model.ClassDefs.OrderBy(Class => Class.ClassId, StringComparer.Ordinal).Select(Class => new { Class.ClassId, Class.Style }),
        Links = Model.LinkStyles.Select(Link => new { Link.IsDefault, Link.EdgeIndexes, Link.Style }),
        Animations = Model.Edges.OrderBy(Edge => Edge.Id, StringComparer.Ordinal).Select(Edge => new { Edge.Id,
            Values = Edge.Metadata.OrderBy(Pair => Pair.Key, StringComparer.Ordinal).Where(Pair => Pair.Value is AnimationMetadata)
                .Select(Pair => new { Pair.Key, Value = (AnimationMetadata)Pair.Value }) })
    };
}
