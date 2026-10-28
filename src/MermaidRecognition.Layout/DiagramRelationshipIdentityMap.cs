using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Immutable, document-local relationship identities for a parsed source revision.
/// Retain a reconciled map only when the corresponding model and geometry are accepted.
/// Parser IDs, line numbers and creation indices are metadata, never identity evidence.
/// </summary>
internal sealed class DiagramRelationshipIdentityMap
{
    private readonly Entry[] Entries;
    private readonly long NextIdentity;

    private DiagramRelationshipIdentityMap(string DocumentId, long SourceRevision, Entry[] Entries, long NextIdentity)
    {
        this.DocumentId = DocumentId;
        this.SourceRevision = SourceRevision;
        this.Entries = Entries;
        this.NextIdentity = NextIdentity;
        Bindings = Array.AsReadOnly(Entries.Select(Item => Item.Binding).ToArray());
    }

    internal string DocumentId { get; }
    internal long SourceRevision { get; }
    internal IReadOnlyList<Binding> Bindings { get; }

    internal static DiagramRelationshipIdentityMap Capture(string DocumentId, long SourceRevision, DiagramContract Model)
    {
        ArgumentException.ThrowIfNullOrEmpty(DocumentId);
        ArgumentOutOfRangeException.ThrowIfNegative(SourceRevision);
        ArgumentNullException.ThrowIfNull(Model);
        Entry[] Entries = Describe(Model);
        string? Failure = ValidateExplicitIds(Entries);
        if (Failure != null) { throw new ArgumentException(Failure, nameof(Model)); }
        long Next = 1;
        foreach (Entry Item in Entries) { Item.Binding = Item.Binding with { Id = Allocate(ref Next) }; }
        return new(DocumentId, SourceRevision, Entries, Next);
    }

    /// <summary>
    /// Matches explicit IDs, exact content, unique endpoint pairs, then uniquely evidenced
    /// one-endpoint reconnects. Ambiguity produces no successor; it never silently transfers
    /// an arbitrary relationship's selection, route or diagnostics to another declaration.
    /// The host must additionally check its latest observed source/layout revision at publication.
    /// </summary>
    internal Reconciliation Reconcile(long SourceRevision, DiagramContract Model)
    {
        ArgumentNullException.ThrowIfNull(Model);
        if (SourceRevision <= this.SourceRevision) { return Rejected("The source revision must advance beyond the accepted identity map."); }
        Entry[] Incoming = Describe(Model);
        string? Failure = ValidateExplicitIds(Incoming);
        if (Failure != null) { return Rejected(Failure); }

        HashSet<int> Old = Enumerable.Range(0, Entries.Length).ToHashSet();
        HashSet<int> New = Enumerable.Range(0, Incoming.Length).ToHashSet();
        List<string> Notices = [];

        void Match(int Previous, int Current)
        {
            Incoming[Current].Binding = Incoming[Current].Binding with { Id = Entries[Previous].Binding.Id };
            Old.Remove(Previous);
            New.Remove(Current);
        }

        var Explicit = Entries.Select((Item, Index) => (Item, Index))
            .Where(Pair => Pair.Item.ExplicitId != null).ToDictionary(Pair => Pair.Item.ExplicitId!, Pair => Pair.Index, StringComparer.Ordinal);
        foreach (int Index in New.ToArray())
        {
            if (Incoming[Index].ExplicitId is { } Id && Explicit.TryGetValue(Id, out int Previous)) { Match(Previous, Index); }
        }

        // Explicit IDs are authoritative: adding/removing/changing one is a new declaration.
        int[] UnkeyedOld() => Old.Where(Index => Entries[Index].ExplicitId == null).Order().ToArray();
        int[] UnkeyedNew() => New.Where(Index => Incoming[Index].ExplicitId == null).Order().ToArray();

        var ExactOld = UnkeyedOld().ToLookup(Index => (Entries[Index].Endpoints, Entries[Index].Content));
        foreach (var Group in UnkeyedNew().GroupBy(Index => (Incoming[Index].Endpoints, Incoming[Index].Content)))
        {
            int[] Previous = ExactOld[Group.Key].ToArray();
            int[] Current = Group.ToArray();
            if (Previous.Length == 0) { continue; }
            if (Previous.Length != Current.Length)
            {
                return Rejected("Ambiguous parallel relationships at " + Incoming[Current[0]].Endpoints
                    + ": indistinguishable declarations changed in number. Supply explicit identities or rebuild the session.");
            }
            if (Previous.Length > 1)
            {
                Notices.Add("Indistinguishable parallel relationships at " + Incoming[Current[0]].Endpoints
                    + " retain identity by declaration occurrence order.");
            }
            for (int Index = 0; Index < Previous.Length; Index++) { Match(Previous[Index], Current[Index]); }
        }

        // This also preserves identity for label, relationship-kind and port-hint edits.
        var EndpointsOld = UnkeyedOld().ToLookup(Index => Entries[Index].Endpoints);
        foreach (var Group in UnkeyedNew().GroupBy(Index => Incoming[Index].Endpoints))
        {
            int[] Previous = EndpointsOld[Group.Key].ToArray();
            int[] Current = Group.ToArray();
            if (Previous.Length == 0) { continue; }
            if (Previous.Length != 1 || Current.Length != 1)
            {
                return Rejected("Ambiguous relationship edits at " + Group.Key
                    + ": multiple unmatched declarations share endpoints. Supply explicit identities or rebuild the session.");
            }
            Match(Previous[0], Current[0]);
        }

        // Index both endpoint inventories rather than comparing every old/new edge pair.
        var Sources = UnkeyedOld().ToLookup(Index => (Entries[Index].Content, Entries[Index].Endpoints.Source));
        var Targets = UnkeyedOld().ToLookup(Index => (Entries[Index].Content, Entries[Index].Endpoints.Target));
        Dictionary<int, int[]> Candidates = [];
        Dictionary<int, int> Uses = [];
        foreach (int Index in UnkeyedNew())
        {
            Entry Item = Incoming[Index];
            int[] Possible = Sources[(Item.Content, Item.Endpoints.Source)]
                .Concat(Targets[(Item.Content, Item.Endpoints.Target)]).Distinct().Take(2).ToArray();
            if (Possible.Length > 1)
            {
                return Rejected("Ambiguous reconnect at " + Item.Endpoints
                    + ": more than one declaration could own the edit. Supply explicit identities or rebuild the session.");
            }
            Candidates.Add(Index, Possible);
            foreach (int Previous in Possible) { Uses[Previous] = Uses.GetValueOrDefault(Previous) + 1; }
        }
        foreach (var Pair in Candidates)
        {
            if (Pair.Value.Length == 1 && Uses[Pair.Value[0]] == 1) { Match(Pair.Value[0], Pair.Key); }
            else if (Pair.Value.Length != 0)
            {
                return Rejected("Ambiguous reconnect at " + Incoming[Pair.Key].Endpoints
                    + ": more than one declaration could own the edit. Supply explicit identities or rebuild the session.");
            }
        }

        long Next = NextIdentity;
        string[] Removed = Old.Order().Select(Index => Entries[Index].Binding.Id).ToArray();
        List<string> Added = [];
        foreach (int Index in New.Order())
        {
            string Id = Allocate(ref Next);
            Incoming[Index].Binding = Incoming[Index].Binding with { Id = Id };
            Added.Add(Id);
        }
        return new(new(DocumentId, SourceRevision, Incoming, Next), Array.AsReadOnly(Added.ToArray()),
            Array.AsReadOnly(Removed), Array.AsReadOnly(Notices.ToArray()), null);
    }

    private static string Allocate(ref long Next)
    {
        string Id = "relationship:" + Next.ToString(CultureInfo.InvariantCulture);
        Next = checked(Next + 1);
        return Id;
    }

    private static Reconciliation Rejected(string Reason) => new(null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Reason);

    private static string? ValidateExplicitIds(Entry[] Items)
    {
        HashSet<string> Seen = new(StringComparer.Ordinal);
        foreach (Entry Item in Items)
        {
            if (Item.ExplicitId is not { } Id) { continue; }
            if (string.IsNullOrWhiteSpace(Id) || !Seen.Add(Id)) { return "Explicit relationship identities must be nonempty and unique."; }
        }
        return null;
    }

    private static Entry[] Describe(DiagramContract Model) => Model.Edges.Select((Edge, Index) => new Entry(
        new("", Index, Edge.Id, Edge.CreationIndex, Edge.SourceId, Edge.TargetId),
        Edge.IsUserDefinedId ? Edge.Id ?? "" : null,
        new(Edge.SourceId, Edge.TargetId),
        new(Edge.Label ?? "", Edge.LabelType, Edge.MarkerStart, Edge.MarkerEnd, Edge.Stroke, Edge.MinLength, Edge.Curve,
            Raw(Edge, "enterpriseRelationshipKind"), Raw(Edge, "enterprisePlainConnection"),
            Edge.RoutingHint?.Leave, Edge.RoutingHint?.Enter, Edge.RoutingHint?.Travel))).ToArray();

    private static string? Raw(EdgeContract Edge, string Key) =>
        Edge.Metadata.TryGetValue(Key, out var Value) && Value is RawMetadata Raw ? Raw.RawValue : null;

    internal sealed record Binding(string Id, int ParsedEdgeIndex, string? ParserId, int CreationIndex, string SourceId, string TargetId);
    internal sealed record Reconciliation(DiagramRelationshipIdentityMap? Next, IReadOnlyList<string> AddedIds,
        IReadOnlyList<string> RemovedIds, IReadOnlyList<string> Notices, string? FailureReason);

    private sealed class Entry(Binding Binding, string? ExplicitId, Endpoints Endpoints, Content Content)
    {
        internal Binding Binding = Binding;
        internal readonly string? ExplicitId = ExplicitId;
        internal readonly Endpoints Endpoints = Endpoints;
        internal readonly Content Content = Content;
    }

    private sealed record Endpoints(string Source, string Target)
    {
        public override string ToString() => Source + " -> " + Target;
    }

    private sealed record Content(string Label, MermaidLabelType LabelType, EdgeEndpointMarker Start, EdgeEndpointMarker End,
        string? Stroke, int MinLength, string? Curve, string? EnterpriseKind, string? PlainConnection,
        LayoutHintDirection? Leave, LayoutHintDirection? Enter, LayoutHintDirection? Travel);
}
