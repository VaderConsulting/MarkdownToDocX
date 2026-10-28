using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

internal sealed record DiagramEvaluationOrigin(string DocumentId, long SourceRevision, long LayoutRevision);
internal enum DiagramPlacementTrialKind { RouteOnly, NodeMove }

internal sealed class DiagramPlacementTrial
{
    private readonly DiagramLayoutSession.Update? Candidate;
    internal DiagramPlacementTrial(DiagramEvaluationOrigin Origin, DiagramPlacementTrialKind Kind,
        IReadOnlyDictionary<string, FlowchartCellRegion> Targets, DiagramLayoutSession.Update? Candidate, string? Failure)
    {
        this.Origin = Origin; this.Kind = Kind; this.Candidate = Candidate;
        this.Targets = Targets.ToImmutableDictionary(StringComparer.Ordinal);
        FailureReason = Failure ?? Candidate?.FailureReason;
        AffectedRouteIds = Candidate?.AffectedRouteIds.ToImmutableArray() ?? [];
        RebuiltRouteIds = Candidate?.RebuiltRouteIds.ToImmutableArray() ?? [];
    }
    internal DiagramEvaluationOrigin Origin { get; }
    internal DiagramPlacementTrialKind Kind { get; }
    internal ImmutableDictionary<string, FlowchartCellRegion> Targets { get; }
    internal string? FailureReason { get; }
    internal ImmutableArray<string> AffectedRouteIds { get; }
    internal ImmutableArray<string> RebuiltRouteIds { get; }
    internal DiagramLayoutSession.Snapshot? CopyCandidate()
    {
        if (Candidate?.State == null || FailureReason != null) return null;
        var Copy = Candidate.State.Fork();
        return new(Origin.DocumentId, Origin.SourceRevision, Origin.LayoutRevision, Copy.Contract, Copy.Layout);
    }
}

internal sealed record DiagramPlacementExploration(DiagramEvaluationOrigin Origin, ImmutableArray<DiagramPlacementTrial> Trials,
    ImmutableDictionary<string, string> SkippedNodes, bool BudgetExhausted, string Reason);

/// <summary>Owns isolated retained trials. It never publishes a move or enters initial placement.</summary>
internal sealed class DiagramPlacementEvaluator
{
    private readonly DiagramLayoutSession Session;
    private readonly DiagramLayoutSession.Snapshot Baseline;
    private readonly DiagramEvaluationOrigin Origin;
    private readonly ImmutableHashSet<string> Locked;

    internal DiagramPlacementEvaluator(DiagramLayoutSession.Snapshot Snapshot, IEnumerable<string>? LockedNodes = null)
    {
        Origin = new(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision);
        Session = new(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.RoutingModel, Snapshot.Layout);
        Baseline = Session.CopySnapshot();
        Locked = (LockedNodes ?? []).ToImmutableHashSet(StringComparer.Ordinal);
    }

    internal DiagramPlacementTrial EvaluateMove(IReadOnlyDictionary<string, FlowchartCellRegion> Targets, int? RouteLimit = null)
    {
        ArgumentNullException.ThrowIfNull(Targets);
        if (Targets.Any(Target => Locked.Contains(Target.Key) && Baseline.Layout.CellNodes!.Any(Node => Node.Id == Target.Key
            && Node.CellNode!.Region != Target.Value)))
            return new(Origin, DiagramPlacementTrialKind.NodeMove, Targets, null, "The move names an explicitly locked node.");
        var Candidate = Session.PrepareMove(Baseline.SourceRevision, Targets, RouteLimit, AllowContainedGroupMoves: true);
        return new(Origin, DiagramPlacementTrialKind.NodeMove, Targets, Candidate, null);
    }

    internal DiagramPlacementExploration Explore(string RelationshipId, int? CandidateLimit = null, int? RouteLimit = null, CancellationToken Token = default)
    {
        Token.ThrowIfCancellationRequested();
        int Bound = CandidateLimit ?? FlowchartLayoutRules.RepairCandidateMoves;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Bound);
        if (Bound > FlowchartLayoutRules.RepairCandidateMoves) throw new ArgumentOutOfRangeException(nameof(CandidateLimit));
        var Edge = Baseline.RoutingModel.Edges.SingleOrDefault(Edge => Edge.Id == RelationshipId)
            ?? throw new ArgumentException("The relationship identity is unknown.", nameof(RelationshipId));
        if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invisible layout constraints are not drawable placement subjects.", nameof(RelationshipId));
        var Route = Baseline.Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == RelationshipId);
        List<DiagramPlacementTrial> Trials = [];
        // Scoped allocation and route refinement offer existing path/terminal
        // candidates before any node changes. Mandatory hints are copied intact.
        Trials.Add(new(Origin, DiagramPlacementTrialKind.RouteOnly, ImmutableDictionary<string, FlowchartCellRegion>.Empty,
            Session.PreparePorts(Baseline.SourceRevision, RelationshipId, Edge.RoutingHint ?? new(), RouteLimit), null));
        HashSet<string> Nominated = new(StringComparer.Ordinal) { Edge.SourceId, Edge.TargetId };
        if (Route is { Points.Count: >= 2 })
        {
            var A = Route.Points[0]; var B = Route.Points[^1];
            foreach (var Box in Baseline.Layout.NodeBounds.Where(Box => Box.NodeId != Edge.SourceId && Box.NodeId != Edge.TargetId))
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Box)) Nominated.Add(Box.NodeId);
        }
        var Placed = Baseline.Layout.CellNodes!.ToList();
        var Skipped = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var Node in Placed.Where(Node => Nominated.Contains(Node.Id)))
            if (Locked.Contains(Node.Id)) Skipped.Add(Node.Id, "Explicitly locked node.");
            else if (Node.PlacementHintLocked) Skipped.Add(Node.Id, "Mandatory placement hint.");
        var Direction = FlowchartDirectionNormalizer.Normalize(Baseline.RoutingModel.Direction);
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        HashSet<string> Visited = new(StringComparer.Ordinal);
        try
        {
            foreach (var Move in DiagramGridLayout.CellRouteMoveCandidates(Baseline.RoutingModel, Placed, Horizontal,
                Nominated.Where(Id => !Skipped.ContainsKey(Id) && Placed.Any(Node => Node.Id == Id)), IncludeOutputChains: false))
            {
                Token.ThrowIfCancellationRequested();
                if (!Visited.Add(Move.Key)) continue;
                if (Trials.Count >= Bound) return new(Origin, Trials.ToImmutableArray(), Skipped.ToImmutable(), true, $"The candidate limit of {Bound} was reached.");
                var Node = Move.Nodes.Single(); var Old = Node.CellNode!.Region;
                var Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal)
                { [Node.Id] = new(checked(Old.StartRow + Move.Rows), checked(Old.StartColumn + Move.Columns), Old.RowSpan, Old.ColumnSpan) };
                Trials.Add(EvaluateMove(Targets, RouteLimit));
            }
        }
        catch (OverflowException)
        { return new(Origin, Trials.ToImmutableArray(), Skipped.ToImmutable(), false, "Generated moves exceed the supported cell coordinate range."); }
        return new(Origin, Trials.ToImmutableArray(), Skipped.ToImmutable(), false,
            Trials.Count == 1 ? "No eligible node move; existing paths and terminals were evaluated." : "Generated node candidates were evaluated.");
    }
}
