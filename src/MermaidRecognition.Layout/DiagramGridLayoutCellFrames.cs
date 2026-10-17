using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Cell-authoritative frame fitting and foreign-node eviction, with derived drawing caches only.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Settles derived group frames by trying whole-cell member assignments, without pixel trial nodes.</summary>
    /// <param name="Contract">Membership and fixed placement hints.</param>
    /// <param name="Placed">Nodes sharing one authoritative cell grid.</param>
    /// <param name="Frames">Receives frames derived from the accepted regions.</param>
    /// <param name="Trigger">Caller description retained in frame-fit diagnostics.</param>
    /// <param name="Context">Explicit owning diagram state; candidate calls cannot borrow another render's budget.</param>
    /// <exception cref="InvalidOperationException">The existing named budgets cannot resolve a remaining frame conflict.</exception>
    private static void FitCellFrames(DiagramContract Contract, List<GridNode> Placed,
        List<GridFrame> Frames, string Trigger, FlowchartRenderContext Context)
    {
        var Original = FlowchartCellRepair.Capture(Placed);
        var OriginalFrames = Frames.ToArray();
        bool Settled = false;
        try
        {
            ReplaceCellFrames(Contract, Placed, Frames);
            int Accepted = 0;
            while (CellFrameConflicts(Contract, Placed, Frames, DiagramGridMetrics.GRID_UNIT_PX).Count != 0)
            {
                if (!SearchCellFrameAssignment(Contract, Placed, Frames, BudgetOverride: Context.MoveBeforeDetourBudget))
                    throw new InvalidOperationException($"Unresolved cell-region frame conflict after {Trigger}; no legal assignment within the named repair limits.");
                Accepted++;
            }
            GroupFrameRefitObserver?.Invoke($"cell refit for {Trigger}; {Accepted} whole-cell assignments; stable frame fit");
            Settled = true;
        }
        finally
        {
            if (!Settled)
            {
                FlowchartCellRepair.Restore(Placed, Original);
                Frames.Clear(); Frames.AddRange(OriginalFrames);
            }
        }
    }

    /// <summary>A conflict between unrelated frames, or between a frame and one foreign node.</summary>
    /// <param name="Key">Stable conflict identity for rejecting newly introduced conflicts.</param>
    /// <param name="First">First member group.</param>
    /// <param name="Second">Other group or foreign node.</param>
    private sealed record CellFrameConflict(string Key, string First, string Second);

    /// <summary>Finds conflicts in derived geometry, using cells solely as ownership and movement inputs.</summary>
    /// <param name="Contract">Group ancestry and relationship clearances.</param>
    /// <param name="Placed">Cell-bound nodes in one coordinate view.</param>
    /// <param name="Frames">Frames already derived in that view.</param>
    /// <param name="RequiredGap">Measured caller clearance; it never determines cell coordinates.</param>
    /// <returns>Deterministic distinct conflict identities and their two movable units.</returns>
    private static List<CellFrameConflict> CellFrameConflicts(DiagramContract Contract,
        List<GridNode> Placed, List<GridFrame> Frames, int RequiredGap)
    {
        List<CellFrameConflict> Conflicts = [];
        foreach (var Frame in Frames)
        {
            foreach (var Other in Frames.Where(Other => string.CompareOrdinal(Frame.Id, Other.Id) < 0))
                if (!IsInsideGroup(Contract, Frame.Id, Other.Id) && !IsInsideGroup(Contract, Other.Id, Frame.Id)
                    && Frame.Left < Other.Right && Other.Left < Frame.Right
                    && Math.Min(Frame.Top, Frame.Bottom) < Math.Max(Other.Top, Other.Bottom)
                    && Math.Min(Other.Top, Other.Bottom) < Math.Max(Frame.Top, Frame.Bottom))
                    Conflicts.Add(new("frames|" + Frame.Id + "|" + Other.Id, Frame.Id, Other.Id));
            foreach (var Node in Placed.Where(Node => !IsInsideGroup(Contract, Node.Id, Frame.Id)))
            {
                int Gap = RequiredGap;
                if (Contract.Edges.Any(Edge => Edge.SourceId == Node.Id
                        && (Edge.TargetId == Frame.Id || IsInsideGroup(Contract, Edge.TargetId, Frame.Id))
                    || Edge.TargetId == Node.Id
                        && (Edge.SourceId == Frame.Id || IsInsideGroup(Contract, Edge.SourceId, Frame.Id))))
                    Gap = Math.Max(Gap, 2 * DiagramGridMetrics.GRID_UNIT_PX);
                string? Parent = ParentGroup(Contract, Frame.Id);
                if (Parent != null && IsInsideGroup(Contract, Node.Id, Parent))
                    Gap = Math.Max(Gap, DiagramGridMetrics.CeilToGrid(
                        FlowchartSubgraphGapThresholds.ResolveDirectMemberNestedGapPx(FlowchartRenderOptions.FromContract(Contract))));
                if (Node.X + Node.Width / 2.0 > Frame.Left - Gap && Node.X - Node.Width / 2.0 < Frame.Right + Gap
                    && Node.Y + Node.Height / 2.0 > Math.Min(Frame.Top, Frame.Bottom) - Gap
                    && Node.Y - Node.Height / 2.0 < Math.Max(Frame.Top, Frame.Bottom) + Gap)
                    Conflicts.Add(new("node|" + Frame.Id + "|" + Node.Id, Frame.Id, Node.Id));
            }
        }
        return Conflicts.OrderBy(Conflict => Conflict.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>Scores complete region translations and retains only a strict conflict reduction with no new conflicts.</summary>
    /// <param name="Contract">Group ancestry and placement hints.</param>
    /// <param name="Placed">All ownership participants, including fixed obstacles.</param>
    /// <param name="Frames">Derived frame cache; rejected candidates restore its exact objects.</param>
    /// <param name="MovingOnly">Optional frame or node whose unit may move for a direct repair call.</param>
    /// <param name="RequiredGap">Measured clearance, independent of integer candidate distances.</param>
    /// <param name="ConcernedFrame">Optional frame whose conflict must be reduced by a direct repair.</param>
    /// <param name="ConcernedNode">Optional foreign node that must be cleared from that frame.</param>
    /// <param name="BudgetOverride">Explicit owning budget when called from the staged engine.</param>
    /// <returns>Whether one complete assignment was accepted within the existing named budgets.</returns>
    private static bool SearchCellFrameAssignment(DiagramContract Contract, List<GridNode> Placed,
        List<GridFrame> Frames, string? MovingOnly = null, int RequiredGap = DiagramGridMetrics.GRID_UNIT_PX,
        string? ConcernedFrame = null, string? ConcernedNode = null,
        FlowchartRenderContext.RepairSearchBudget? BudgetOverride = null)
    {
        var Original = FlowchartCellRepair.Capture(Placed);
        var OriginalFrames = Frames.ToArray();
        var Before = CellFrameConflicts(Contract, Placed, Frames, RequiredGap);
        if (Before.Count == 0) return false;
        var Existing = Before.Select(Conflict => Conflict.Key).ToHashSet(StringComparer.Ordinal);
        var Required = Before.Where(Conflict => (ConcernedFrame == null || Conflict.First == ConcernedFrame
                || Conflict.Second == ConcernedFrame) && (ConcernedNode == null
                || Conflict.Key == "node|" + ConcernedFrame + "|" + ConcernedNode))
            .Select(Conflict => Conflict.Key).ToHashSet(StringComparer.Ordinal);
        if (Required.Count == 0) return false;
        var Budget = BudgetOverride ?? FlowchartRenderContext.Current.MoveBeforeDetourBudget;
        if (Budget.AcceptedMoves >= FlowchartLayoutRules.RepairChainDepth) return false;
        var Positions = Placed.ToDictionary(Node => Node.Id, Node => (Node.X, Node.Y), StringComparer.Ordinal);
        FlowchartLogicalGrid.Snapshot? Best = null;
        GridFrame[]? BestFrames = null;
        int BestRemaining = Before.Count;
        double BestLength = double.PositiveInfinity;
        var Units = Before.SelectMany(Conflict => new[] { Conflict.First, Conflict.Second })
            .Distinct(StringComparer.Ordinal).Where(Id => MovingOnly == null || Id == MovingOnly).ToArray();
        foreach (string Unit in Units)
        {
            var Moving = Placed.Where(Node => Node.Id == Unit || IsInsideGroup(Contract, Node.Id, Unit)).ToArray();
            if (Moving.Length == 0 || Moving.Any(Node => Node.PlacementHintLocked
                || RouteRepairStage.HasHintedContainingGroup(Contract, Node.Id))) continue;
            var MovingIds = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
            if (Contract.Edges.Count(Edge => MovingIds.Contains(Edge.SourceId) || MovingIds.Contains(Edge.TargetId))
                > FlowchartLayoutRules.RepairChainLines) continue;
            // Relative dependents cannot be silently left behind when their peer moves.
            if (HasExternalCellHintDependent(Contract, Unit, Moving)) continue;
            foreach (int Distance in Enumerable.Range(1, FlowchartLayoutRules.RepairChainDepth))
            foreach (var Delta in new[] { (Rows: -Distance, Columns: 0), (Rows: Distance, Columns: 0),
                (Rows: 0, Columns: -Distance), (Rows: 0, Columns: Distance) })
            {
                if (Budget.CandidateMoves >= FlowchartLayoutRules.RepairCandidateMoves) break;
                Budget.CandidateMoves++;
                var Plan = FlowchartCellRepair.PlanMove(Moving, Delta.Rows, Delta.Columns)
                    ?? FlowchartCellRepair.PlanDisplacement(Placed, Moving, Delta.Rows, Delta.Columns);
                if (Plan == null) continue;
                var Participants = Plan.Nodes.ToArray();
                if (Participants.Any(Node => RouteRepairStage.HasHintedContainingGroup(Contract, Node.Id))
                    || HasExternalCellHintDependent(Contract, Unit, Participants)) continue;
                var ParticipantIds = Participants.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                if (Contract.Edges.Count(Edge => ParticipantIds.Contains(Edge.SourceId) || ParticipantIds.Contains(Edge.TargetId))
                    > FlowchartLayoutRules.RepairChainLines) continue;
                try
                {
                    if (!Plan.TryApply()) continue;
                    ReplaceCellFrames(Contract, Placed, Frames);
                    GroupFrameFitPassObserver?.Invoke($"cell frame candidate {Unit}: ({Delta.Rows},{Delta.Columns})");
                    var After = CellFrameConflicts(Contract, Placed, Frames, RequiredGap);
                    if (After.Any(Conflict => !Existing.Contains(Conflict.Key)) || After.Count >= Before.Count) continue;
                    if (After.Count(Conflict => Required.Contains(Conflict.Key)) >= Required.Count) continue;
                    double Length = Participants.Sum(Node => Math.Abs(Node.X - Positions[Node.Id].X) + Math.Abs(Node.Y - Positions[Node.Id].Y));
                    if (After.Count > BestRemaining || After.Count == BestRemaining && Length >= BestLength) continue;
                    BestRemaining = After.Count;
                    BestLength = Length;
                    Best = FlowchartCellRepair.Capture(Placed);
                    BestFrames = Frames.ToArray();
                }
                finally
                {
                    FlowchartCellRepair.Restore(Placed, Original);
                    Frames.Clear(); Frames.AddRange(OriginalFrames);
                }
            }
        }
        if (Best == null) return false;
        FlowchartCellRepair.Restore(Placed, Best);
        Frames.Clear(); Frames.AddRange(BestFrames!);
        Budget.AcceptedMoves++;
        return true;
    }

    /// <summary>Protects hints attached to nodes or groups outside a proposed moving unit.</summary>
    /// <param name="Contract">All declared hint dependencies.</param>
    /// <param name="Unit">Frame or node proposed for movement.</param>
    /// <param name="Moving">Its complete member set.</param>
    /// <returns>Whether a fixed dependent would lose its placement relative to its moved peer.</returns>
    private static bool HasExternalCellHintDependent(DiagramContract Contract, string Unit, GridNode[] Moving)
    {
        var Ids = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        Ids.UnionWith(Contract.Groups.Where(Group => Group.Id == Unit || IsInsideGroup(Contract, Group.Id, Unit)).Select(Group => Group.Id));
        return Contract.Nodes.Any(Node => !Ids.Contains(Node.Id) && Node.PlacementHint?.IsActive == true
                && Ids.Contains(Node.PlacementHint.PeerId ?? string.Empty))
            || Contract.Groups.Any(Group => !Ids.Contains(Group.Id) && Group.PlacementHint?.IsActive == true
                && Ids.Contains(Group.PlacementHint.PeerId ?? string.Empty));
    }

    /// <summary>Builds a fresh derived frame list in the same existing coordinate view as its cell-bound members.</summary>
    /// <param name="Contract">Frame titles and membership.</param>
    /// <param name="Placed">Members whose regions are already authoritative.</param>
    /// <returns>New frame objects; no node position or size is changed.</returns>
    private static List<GridFrame> FramesForCells(DiagramContract Contract, List<GridNode> Placed)
    {
        List<GridFrame> Frames = [];
        ReplaceCellFrames(Contract, Placed, Frames);
        return Frames;
    }

    /// <summary>Replaces only the drawing cache from declared member regions, rejecting mixed coordinate views.</summary>
    /// <param name="Contract">Group definitions and title measurements.</param>
    /// <param name="Placed">Nodes sharing one cell grid and coordinate direction.</param>
    /// <param name="Frames">Destination derived cache.</param>
    private static void ReplaceCellFrames(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames)
    {
        _ = FlowchartCellRepair.Capture(Placed);
        bool Routing = Placed[0].CellNode!.UsesRoutingCoordinates;
        if (Placed.Any(Node => Node.CellNode!.UsesRoutingCoordinates != Routing))
            throw new InvalidOperationException("A frame cannot mix placement and routing coordinate views.");
        Frames.Clear();
        if (Routing) BuildFramesYUp(Contract, Placed, Frames);
        else BuildFrames(Contract, Placed, Frames);
    }
}
