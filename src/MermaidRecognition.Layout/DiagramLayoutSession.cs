using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Owns one document's accepted geometry. Source revisions advance even for invalid text;
/// only a validated candidate based on the current geometry and source may be published.
/// This engine service does not enable editor gestures or interpret source changes.
/// </summary>
internal sealed class DiagramLayoutSession
{
    private readonly object Gate = new();
    private DiagramGridLayout.RetainedRoutingState Committed;
    private long LatestSourceRevision;
    private long CommittedSourceRevision;
    private long LayoutRevision;

    /// <summary>Starts a canvas that can be constructed one explicitly placed item at a time.</summary>
    internal DiagramLayoutSession(string DocumentId, long SourceRevision, string Direction = "TB", DiagramLayoutOptions? Options = null)
        : this(DocumentId, SourceRevision, new DiagramContract { Kind = "flowchart", Direction = Direction },
            new FlowchartLayoutResult { UsesGridRoutes = true, CellNodes = [],
                RenderContext = new() { LayoutOptions = Options ?? DiagramLayoutOptions.Default } }) { }

    internal DiagramLayoutSession(string DocumentId, long SourceRevision, DiagramContract Contract,
        FlowchartLayoutResult Layout)
    {
        ArgumentException.ThrowIfNullOrEmpty(DocumentId);
        ArgumentOutOfRangeException.ThrowIfNegative(SourceRevision);
        this.DocumentId = DocumentId;
        LatestSourceRevision = SourceRevision;
        CommittedSourceRevision = SourceRevision;
        Committed = DiagramGridLayout.RetainedRoutingState.Capture(Contract, Layout);
    }

    internal string DocumentId { get; }
    internal DiagramLayoutOptions Options => Committed.Options;
    internal long Revision { get { lock (Gate) { return LayoutRevision; } } }
    internal bool IsStale { get { lock (Gate) { return LatestSourceRevision != CommittedSourceRevision; } } }

    /// <summary>Call when text changes, including parse failures, before dispatching background work.</summary>
    internal void ObserveSourceRevision(long SourceRevision)
    {
        lock (Gate)
        {
            if (SourceRevision < LatestSourceRevision)
            {
                throw new ArgumentOutOfRangeException(nameof(SourceRevision), "Source revisions must be monotonic.");
            }
            LatestSourceRevision = SourceRevision;
        }
    }

    /// <summary>Returns detached render geometry; consumers cannot mutate the committed state.</summary>
    internal FlowchartLayoutResult CopyLayout()
    {
        return CopySnapshot().Layout;
    }

    internal Snapshot CopySnapshot()
    {
        lock (Gate)
        {
            var Copy = Committed.Fork();
            return new(DocumentId, CommittedSourceRevision, LayoutRevision, Copy.Contract, Copy.Layout);
        }
    }

    internal sealed record Snapshot(string DocumentId, long SourceRevision, long Revision,
        DiagramContract RoutingModel, FlowchartLayoutResult Layout);

    /// <summary>
    /// Plans explicit cell translations against retained geometry. Resizing and replacement
    /// source hints use separate operations; group refitting remains unsupported.
    /// No initial placement or whole-diagram routing entry point is reachable here.
    /// </summary>
    internal Update PrepareMove(long SourceRevision, IReadOnlyDictionary<string, FlowchartCellRegion> Targets,
        int? RouteLimit = null, bool AllowContainedGroupMoves = false)
    {
        ArgumentNullException.ThrowIfNull(Targets);
        return Prepare(SourceRevision, Candidate =>
        {
            string? Failure = Candidate.TryMove(Targets, Limit(RouteLimit), out var Affected, out var Rebuilt, AllowContainedGroupMoves);
            return (Failure, Affected, Rebuilt);
        });
    }

    internal Update PrepareAddNode(long SourceRevision, NodeContract Node, FlowchartCellRegion Region, int? RouteLimit = null)
        => Prepare(SourceRevision, Candidate =>
        {
            string? Failure = Candidate.TryAddNode(Node, Region, Limit(RouteLimit), out var Affected, out var Rebuilt);
            return (Failure, Affected, Rebuilt);
        });

    /// <summary>The editor identity is independent of parsed endpoints and creation order.</summary>
    internal Update PrepareAddRelationship(long SourceRevision, string RelationshipId, EdgeContract Relationship, int? RouteLimit = null)
        => Prepare(SourceRevision, Candidate =>
        {
            string? Failure = Candidate.TryAddRelationship(RelationshipId, Relationship, Limit(RouteLimit), out var Affected, out var Rebuilt);
            return (Failure, Affected, Rebuilt);
        });

    internal Update PrepareReconnect(long SourceRevision, string RelationshipId, string SourceId, string TargetId, int? RouteLimit = null)
        => Prepare(SourceRevision, Candidate =>
        {
            string? Failure = Candidate.TryReconnect(RelationshipId, SourceId, TargetId, Limit(RouteLimit), out var Affected, out var Rebuilt);
            return (Failure, Affected, Rebuilt);
        });

    internal Update PreparePorts(long SourceRevision, string RelationshipId, RoutingHintContract Ports, int? RouteLimit = null)
        => Prepare(SourceRevision, Candidate =>
        {
            string? Failure = Candidate.TryChangePorts(RelationshipId, Ports, Limit(RouteLimit), out var Affected, out var Rebuilt);
            return (Failure, Affected, Rebuilt);
        });

    internal Update PrepareRemoveRelationship(long SourceRevision, string RelationshipId, int? RouteLimit = null)
        => Prepare(SourceRevision, Candidate =>
        {
            string? Failure = Candidate.TryRemoveRelationship(RelationshipId, Limit(RouteLimit), out var Affected, out var Rebuilt);
            return (Failure, Affected, Rebuilt);
        });

    private static int Limit(int? Requested)
    {
        int Value = Requested ?? FlowchartLayoutRules.RepairChainLines;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Value);
        return Value;
    }

    internal Update PrepareResizeNode(long SourceRevision, string NodeId, double Width, double Height, string? Label = null, int? RouteLimit = null)
        => Prepare(SourceRevision, Candidate =>
        {
            var Model = DiagramGridLayout.CopyRoutingInputs(Candidate.Contract);
            var Node = Model.NodesMutable.Find(Node => Node.Id == NodeId);
            if (Node == null) return ("The node identity is unknown.", Array.Empty<string>(), Array.Empty<string>());
            Node.LayoutWidth = Width; Node.LayoutHeight = Height;
            if (Label != null) Node.Label = Label;
            string? Failure = Candidate.TrySourceUpdate(Model, [NodeId], [], Limit(RouteLimit), out var Affected, out var Rebuilt);
            return (Failure, Affected, Rebuilt);
        });

    internal Update PrepareRemoveNode(long SourceRevision, string NodeId, int? RouteLimit = null)
        => Prepare(SourceRevision, Candidate =>
        {
            var Model = DiagramGridLayout.CopyRoutingInputs(Candidate.Contract);
            var Node = Model.NodesMutable.Find(Node => Node.Id == NodeId);
            if (Node == null) return ("The node identity is unknown.", Array.Empty<string>(), Array.Empty<string>());
            if (Model.Edges.Where(Edge => Edge.SourceId == NodeId || Edge.TargetId == NodeId).Any(Candidate.IsTimelineConstraint))
                return ("Plateau triggering topology changes require a new semantic constraint session.", Array.Empty<string>(), Array.Empty<string>());
            var Removed = Model.Edges.Where(Edge => Edge.SourceId == NodeId || Edge.TargetId == NodeId).Select(Edge => Edge.Id!).ToArray();
            Model.NodesMutable.Remove(Node);
            Model.EdgesMutable.RemoveAll(Edge => Edge.SourceId == NodeId || Edge.TargetId == NodeId);
            string? Failure = Candidate.TrySourceUpdate(Model, [], Removed, Limit(RouteLimit), out var Affected, out var Rebuilt);
            return (Failure, Affected, Rebuilt);
        });

    private Update Prepare(long SourceRevision,
        Func<DiagramGridLayout.RetainedRoutingState, (string? Failure, string[] Affected, string[] Rebuilt)> Operation)
    {
        DiagramGridLayout.RetainedRoutingState Candidate;
        long BaseRevision;
        lock (Gate)
        {
            BaseRevision = LayoutRevision;
            if (SourceRevision != LatestSourceRevision)
            {
                return new(this, SourceRevision, BaseRevision, null, [], [], "The source revision is stale.");
            }
            Candidate = Committed.Fork();
        }

        try
        {
            (string? Failure, string[] Affected, string[] Rebuilt) = Operation(Candidate);
            return new(this, SourceRevision, BaseRevision, Failure == null ? Candidate : null, Affected, Rebuilt, Failure);
        }
        catch (InvalidOperationException Failure)
        {
            return new(this, SourceRevision, BaseRevision, null, [], [], "Local routing failed: " + Failure.Message);
        }
        catch (OverflowException)
        {
            return new(this, SourceRevision, BaseRevision, null, [], [], "The edit exceeds the supported cell coordinate range.");
        }
    }

    /// <summary>Publishes geometry, ownership, guides and caches together, or changes nothing.</summary>
    internal bool TryCommit(Update Candidate, out string? Reason)
    {
        ArgumentNullException.ThrowIfNull(Candidate);
        lock (Gate)
        {
            Reason = Candidate.FailureReason;
            if (!ReferenceEquals(Candidate.Owner, this)) { Reason = "The candidate belongs to another document session."; }
            else if (Candidate.SourceRevision != LatestSourceRevision || Candidate.BaseRevision != LayoutRevision)
            {
                Reason = "The candidate was superseded by a source or layout revision.";
            }
            if (Reason != null || Candidate.State == null) { return false; }
            Committed = Candidate.State;
            CommittedSourceRevision = Candidate.SourceRevision;
            LayoutRevision++;
            return true;
        }
    }

    internal sealed class Update
    {
        internal Update(DiagramLayoutSession Owner, long SourceRevision, long BaseRevision,
            DiagramGridLayout.RetainedRoutingState? State, string[] Affected, string[] Rebuilt, string? Failure)
        {
            this.Owner = Owner; this.SourceRevision = SourceRevision; this.BaseRevision = BaseRevision;
            this.State = State; FailureReason = Failure;
            AffectedRouteIds = Array.AsReadOnly(Affected); RebuiltRouteIds = Array.AsReadOnly(Rebuilt);
        }

        internal DiagramLayoutSession Owner { get; }
        internal DiagramGridLayout.RetainedRoutingState? State { get; }
        internal long SourceRevision { get; }
        internal long BaseRevision { get; }
        internal string? FailureReason { get; }
        internal IReadOnlyList<string> AffectedRouteIds { get; }
        internal IReadOnlyList<string> RebuiltRouteIds { get; }
    }
}
