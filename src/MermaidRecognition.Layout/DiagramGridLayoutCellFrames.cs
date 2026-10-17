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
    /// <summary>Builds derived frames from an immutable complete cell candidate without publishing regions or copying pixels back.</summary>
    /// <param name="Contract">Nested membership, titles and frame hints.</param>
    /// <param name="Placed">Authoritative nodes and renderer geometry definitions.</param>
    /// <param name="State">Complete prepared ownership and gaps.</param>
    /// <returns>Fresh projected frames in the nodes' existing coordinate direction.</returns>
    internal static List<GridFrame> ProjectCellFrames(DiagramContract Contract, List<GridNode> Placed, FlowchartLogicalGrid.Snapshot State)
    {
        var Views = new List<GridNode>();
        foreach (var Node in Placed)
        {
            var Box = Node.CellNode!.Bounds(State.Regions[Node.Id], Node.GeometryProvider, State);
            // These are disposable frame-builder views derived forward from
            // cells. They never own reservations and are never copied back.
            Views.Add(new GridNode { Id = Node.Id, GroupId = Node.GroupId, IsDiamond = Node.IsDiamond,
                Shape = Node.Shape, GeometryProvider = Node.GeometryProvider, Rank = Node.Rank, Column = Node.Column,
                X = Box.CenterX, Y = Box.CenterY, Width = checked((int)Box.Width), Height = checked((int)Box.Height) });
        }
        var Frames = new List<GridFrame>();
        if (Placed[0].CellNode!.UsesRoutingCoordinates) BuildFramesYUp(Contract, Views, Frames);
        else BuildFrames(Contract, Views, Frames);
        return Frames;
    }

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
                {
                    GroupFrameFitPassObserver?.Invoke("CELL_FRAME_UNRESOLVED|" + System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Trigger,
                        Context.MoveBeforeDetourBudget.CandidateMoves,
                        Context.MoveBeforeDetourBudget.AcceptedMoves,
                        Conflicts = CellFrameConflicts(Contract, Placed, Frames, DiagramGridMetrics.GRID_UNIT_PX),
                        Frames = Frames.Select(Frame => new { Frame.Id, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom }),
                        Nodes = Placed.Select(Node => new { Node.Id, Node.GroupId, Node.PlacementHintLocked,
                            Region = new { Node.CellNode!.Region.StartRow, Node.CellNode.Region.StartColumn,
                                Node.CellNode.Region.RowSpan, Node.CellNode.Region.ColumnSpan },
                            Node.X, Node.Y, Node.Width, Node.Height }),
                        Rows = Placed[0].CellNode!.Grid.Capture().RowGaps,
                        Columns = Placed[0].CellNode!.Grid.Capture().ColumnGaps
                    }));
                    throw new InvalidOperationException($"Unresolved cell-region frame conflict after {Trigger}; no legal assignment within the named repair limits.");
                }
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
    /// <param name="ShortfallX">Measured horizontal separation still needed to clear the two visual bounds.</param>
    /// <param name="ShortfallY">Measured vertical separation still needed to clear the two visual bounds.</param>
    private sealed record CellFrameConflict(string Key, string First, string Second, double ShortfallX, double ShortfallY);

    /// <summary>Finds conflicts in derived geometry, using cells solely as ownership and movement inputs.</summary>
    /// <param name="Contract">Group ancestry and relationship clearances.</param>
    /// <param name="Placed">Cell-bound nodes in one coordinate view.</param>
    /// <param name="Frames">Frames already derived in that view.</param>
    /// <param name="RequiredGap">Measured caller clearance; it never determines cell coordinates.</param>
    /// <param name="PreparedBounds">Optional pure candidate geometry, avoiding any temporary ownership publication.</param>
    /// <returns>Deterministic distinct conflict identities and their two movable units.</returns>
    private static List<CellFrameConflict> CellFrameConflicts(DiagramContract Contract,
        List<GridNode> Placed, List<GridFrame> Frames, int RequiredGap,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>? PreparedBounds = null)
    {
        List<CellFrameConflict> Conflicts = [];
        foreach (var Frame in Frames)
        {
            foreach (var Other in Frames.Where(Other => string.CompareOrdinal(Frame.Id, Other.Id) < 0))
                if (!IsInsideGroup(Contract, Frame.Id, Other.Id) && !IsInsideGroup(Contract, Other.Id, Frame.Id)
                    && Frame.Left < Other.Right && Other.Left < Frame.Right
                    && Math.Min(Frame.Top, Frame.Bottom) < Math.Max(Other.Top, Other.Bottom)
                    && Math.Min(Other.Top, Other.Bottom) < Math.Max(Frame.Top, Frame.Bottom))
                    Conflicts.Add(new("frames|" + Frame.Id + "|" + Other.Id, Frame.Id, Other.Id,
                        Math.Min(Frame.Right - Other.Left, Other.Right - Frame.Left),
                        Math.Min(Math.Max(Frame.Top, Frame.Bottom) - Math.Min(Other.Top, Other.Bottom),
                            Math.Max(Other.Top, Other.Bottom) - Math.Min(Frame.Top, Frame.Bottom))));
            foreach (var Node in Placed.Where(Node => !IsInsideGroup(Contract, Node.Id, Frame.Id)))
            {
                var Box = PreparedBounds?.GetValueOrDefault(Node.Id);
                double X = Box?.CenterX ?? Node.X, Y = Box?.CenterY ?? Node.Y;
                double Width = Box?.Width ?? Node.Width, Height = Box?.Height ?? Node.Height;
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
                if (X + Width / 2.0 > Frame.Left - Gap && X - Width / 2.0 < Frame.Right + Gap
                    && Y + Height / 2.0 > Math.Min(Frame.Top, Frame.Bottom) - Gap
                    && Y - Height / 2.0 < Math.Max(Frame.Top, Frame.Bottom) + Gap)
                    Conflicts.Add(new("node|" + Frame.Id + "|" + Node.Id, Frame.Id, Node.Id,
                        Math.Min(X + Width / 2 - Frame.Left + Gap, Frame.Right + Gap - X + Width / 2),
                        Math.Min(Y + Height / 2 - Math.Min(Frame.Top, Frame.Bottom) + Gap,
                            Math.Max(Frame.Top, Frame.Bottom) + Gap - Y + Height / 2)));
            }
        }
        return Conflicts.OrderBy(Conflict => Conflict.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>Checks complete projected cell candidates against the same hard frame-conflict identities as live fitting.</summary>
    /// <param name="Contract">Membership and mandatory clearances.</param>
    /// <param name="Placed">Declared nodes supplying membership.</param>
    /// <param name="Frames">Frames projected from the same prepared state.</param>
    /// <param name="Bounds">Node bounds projected from that state.</param>
    /// <returns>Stable conflict keys, without publishing or mutating the candidate.</returns>
    internal static HashSet<string> ProjectedCellFrameConflictKeys(DiagramContract Contract, List<GridNode> Placed,
        List<GridFrame> Frames, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds) =>
        CellFrameConflicts(Contract, Placed, Frames, DiagramGridMetrics.GRID_UNIT_PX, Bounds)
            .Select(Conflict => Conflict.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>Delegates complete region and measured-gap candidates to the scored cell-authoritative legality search.</summary>
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
        return SearchScoredCellFrameAssignment(Contract, Placed, Frames, MovingOnly, RequiredGap,
            ConcernedFrame, ConcernedNode, BudgetOverride ?? FlowchartRenderContext.Current.MoveBeforeDetourBudget);
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
    internal static void ReplaceCellFrames(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames)
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
