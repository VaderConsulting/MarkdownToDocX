using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Applies complete geometry repair candidates to cell ownership, never to derived pixel bounds.</summary>
internal static class FlowchartCellRepair
{
    /// <summary>Returns exact rounded gap growth accumulated between two complete states of the same grid.</summary>
    /// <param name="Before">Original ownership and gap state.</param>
    /// <param name="After">Prepared or accepted later state.</param>
    /// <param name="Columns">Whether to compare column rather than row gaps.</param>
    /// <returns>Actual positive increases relative to the original gaps, suitable for a private cumulative plan.</returns>
    internal static Dictionary<int, double> GrownGapsSince(FlowchartLogicalGrid.Snapshot Before, FlowchartLogicalGrid.Snapshot After, bool Columns)
    {
        if (!ReferenceEquals(Before.Owner, After.Owner)) throw new ArgumentException("Gap comparisons must use the same diagram grid.", nameof(After));
        var Original = Columns ? Before.ColumnGaps : Before.RowGaps; var Candidate = Columns ? After.ColumnGaps : After.RowGaps;
        double BaseGap = Columns ? Before.Owner.Configuration.ColumnGap : Before.Owner.Configuration.RowGap;
        return Candidate.Where(Pair => Pair.Value > Original.GetValueOrDefault(Pair.Key, BaseGap))
            .ToDictionary(Pair => Pair.Key, Pair => Pair.Value - Original.GetValueOrDefault(Pair.Key, BaseGap));
    }

    /// <summary>Captures authoritative cell ownership and gaps for a complete repair candidate.</summary>
    /// <param name="Nodes">Nodes participating in the owning diagram.</param>
    /// <returns>Exact state for rollback, or throws when the nodes do not share cell authority.</returns>
    internal static FlowchartLogicalGrid.Snapshot Capture(IReadOnlyList<DiagramGridLayout.GridNode> Nodes)
    {
        if (!TryGrid(Nodes, out var Grid)) throw new ArgumentException("Repair nodes must share one cell grid.", nameof(Nodes));
        return Grid.Capture();
    }

    /// <summary>Restores a rejected candidate from its cells, never from cached drawing bounds.</summary>
    /// <param name="Nodes">Nodes in the owning diagram.</param>
    /// <param name="State">Exact state captured before building the candidate.</param>
    internal static void Restore(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid.Snapshot State)
    {
        if (!TryGrid(Nodes, out var Grid)) throw new ArgumentException("Repair nodes must share one cell grid.", nameof(Nodes));
        Grid.Restore(State);
    }

    /// <summary>Translates an unhinted endpoint or linked group as one whole-cell assignment.</summary>
    /// <param name="Nodes">Complete set of nodes participating in the repair.</param>
    /// <param name="Rows">Signed logical row displacement; north decreases this value.</param>
    /// <param name="Columns">Signed logical column displacement.</param>
    /// <returns>False without mutation for hints, mixed grids or shared final occupancy.</returns>
    internal static bool TryMove(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, int Rows, int Columns)
        => PlanMove(Nodes, Rows, Columns)?.TryApply() == true;

    /// <summary>Builds every translated target before offering a group or endpoint move to a repair search.</summary>
    /// <param name="Nodes">Complete moving unit.</param>
    /// <param name="Rows">Signed integer row displacement.</param>
    /// <param name="Columns">Signed integer column displacement.</param>
    /// <returns>A validated complete assignment, or null without mutation when it cannot be applied legally.</returns>
    internal static Assignment? PlanMove(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, int Rows, int Columns)
    {
        if (!TryGrid(Nodes, out _)) return null;
        var Targets = Nodes.ToDictionary(Node => Node.Id, Node => Translate(Node.CellNode!.Region, Rows, Columns), StringComparer.Ordinal);
        return PlanAssignment(Nodes, Targets);
    }

    /// <summary>Offers an explicit whole-cell span change for a measured geometry shortfall.</summary>
    /// <param name="Node">The affected cell-bound node.</param>
    /// <param name="Rows">Positive proposed row span.</param>
    /// <param name="Columns">Positive proposed column span.</param>
    /// <returns>False without mutation when the proposed cells are unavailable or a pinned centre would change.</returns>
    internal static bool TryResize(DiagramGridLayout.GridNode Node, int Rows, int Columns)
    {
        ArgumentNullException.ThrowIfNull(Node);
        if (Node.CellNode == null) return false;
        var Before = Node.CellNode.Region;
        if (Node.PlacementHintLocked && (Rows != Before.RowSpan || Columns != Before.ColumnSpan)) return false;
        return TryAssign([Node], new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal)
            { [Node.Id] = new(Before.StartRow, Before.StartColumn, Rows, Columns) });
    }

    /// <summary>Repairs a measured routing-space shortfall by changing a shared gap, not by translating pixel boxes.</summary>
    /// <param name="Nodes">All nodes whose derived positions or dimensions may change.</param>
    /// <param name="Horizontal">True for a column gap, false for a row gap.</param>
    /// <param name="After">The declared row or column preceding the gap.</param>
    /// <param name="Shortfall">Measured missing clearance in pixels.</param>
    /// <returns>False without mutation if the change would move a pinned node or has no measured shortfall.</returns>
    internal static bool TryGrowGap(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, bool Horizontal, int After, double Shortfall)
    {
        if (Shortfall <= 0) return false;
        return PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(),
            [new(Horizontal, After, Shortfall, DiagramGridMetrics.GRID_UNIT_PX)])?.TryApply() == true;
    }

    /// <summary>Finds nodes whose region includes a gap or whose origin lies beyond it relative to the shared grid origin.</summary>
    /// <param name="Region">Explicit whole-cell reservation.</param>
    /// <param name="Horizontal">True for columns, false for rows.</param>
    /// <param name="After">Index preceding the gap.</param>
    /// <returns>Whether growing that gap changes this node's derived geometry.</returns>
    internal static bool AffectedByGap(FlowchartCellRegion Region, bool Horizontal, int After)
    {
        int Start = Horizontal ? Region.StartColumn : Region.StartRow;
        int End = Horizontal ? Region.EndColumn : Region.EndRow;
        return Start <= After && After < End - 1 || Start > 0 && After >= 0 && After < Start
            || Start < 0 && After >= Start && After < 0;
    }

    /// <summary>Offers a complete side, slot or track-order geometry assignment before building its routes.</summary>
    /// <param name="Nodes">Nodes whose regions may be replaced together.</param>
    /// <param name="Changes">Whole rectangular reservations for precisely those nodes.</param>
    /// <returns>False without mutation for a pinned move, missing node or shared cell.</returns>
    internal static bool TryAssign(IReadOnlyList<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, FlowchartCellRegion> Changes)
        => PlanAssignment(Nodes, Changes)?.TryApply() == true;

    /// <summary>Prepares one immutable target state including all regions and all gaps changed by the repair.</summary>
    /// <param name="Nodes">Every moved node and every node whose geometry is affected by gap growth.</param>
    /// <param name="Changes">Complete final rectangles; disconnected or fractional occupancy cannot enter this integer rectangle type.</param>
    /// <param name="GapGrowth">Optional complete set of measured gap increases.</param>
    /// <returns>A validated assignment that has not changed the grid, or null for shared occupancy, incomplete participants or hints.</returns>
    internal static Assignment? PlanAssignment(IReadOnlyList<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, FlowchartCellRegion> Changes, IReadOnlyList<FlowchartCellGapGrowth>? GapGrowth = null)
    {
        ArgumentNullException.ThrowIfNull(Changes);
        if (!TryGrid(Nodes, out var Grid)) return null;
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        foreach (var Change in Changes)
        {
            if (!ById.TryGetValue(Change.Key, out var Node)) return null;
            var Before = Grid.RegionOf(Change.Key);
            if (Node.PlacementHintLocked && Change.Value != Before) return null;
        }
        var Original = Grid.Capture();
        foreach (var Growth in GapGrowth ?? [])
        foreach (var Pair in Original.Regions)
        {
            if (Growth.Shortfall == 0) continue;
            var Target = Changes.GetValueOrDefault(Pair.Key, Pair.Value);
            if (!AffectedByGap(Pair.Value, Growth.Horizontal, Growth.After)
                && !AffectedByGap(Target, Growth.Horizontal, Growth.After)) continue;
            if (!ById.TryGetValue(Pair.Key, out var Node) || Node.PlacementHintLocked) return null;
        }
        var Affected = Nodes.Where(Node => Changes.TryGetValue(Node.Id, out var Target) && Target != Node.CellNode!.Region
            || (GapGrowth ?? []).Any(Growth => Growth.Shortfall != 0
                && (AffectedByGap(Node.CellNode!.Region, Growth.Horizontal, Growth.After)
                    || AffectedByGap(Changes.GetValueOrDefault(Node.Id, Node.CellNode!.Region), Growth.Horizontal, Growth.After)))).ToArray();
        return Grid.TryPrepare(Changes, GapGrowth ?? [], out var Candidate)
            ? new(Grid, Affected, Original, Candidate!) : null;
    }

    /// <summary>Builds a complete displacement chain before any moving node or neighbour changes cells.</summary>
    /// <param name="AllNodes">All nodes of the owning diagram, including fixed obstacles.</param>
    /// <param name="Moving">The original endpoint or group whose move may displace neighbours.</param>
    /// <param name="Rows">Signed whole-row translation shared by the displacement chain.</param>
    /// <param name="Columns">Signed whole-column translation shared by the displacement chain.</param>
    /// <returns>A complete validated assignment, or null when pinned neighbours or the existing chain-depth limit prevent it.</returns>
    internal static Assignment? PlanDisplacement(IReadOnlyList<DiagramGridLayout.GridNode> AllNodes,
        IReadOnlyList<DiagramGridLayout.GridNode> Moving, int Rows, int Columns)
    {
        if (!TryGrid(AllNodes, out var Grid) || Moving.Count == 0 || Rows == 0 && Columns == 0) return null;
        var ById = AllNodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        if (Moving.Any(Node => !ById.TryGetValue(Node.Id, out var Found) || !ReferenceEquals(Node, Found))) return null;
        var Ids = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        for (int Depth = 0; Depth < FlowchartLayoutRules.RepairChainDepth; Depth++)
        {
            var Targets = Ids.ToDictionary(Id => Id, Id => Translate(Grid.RegionOf(Id), Rows, Columns), StringComparer.Ordinal);
            var Displaced = AllNodes.Where(Node => !Ids.Contains(Node.Id)
                && Targets.Values.Any(Target => Target.Overlaps(Node.CellNode!.Region))).ToArray();
            if (Displaced.Length == 0) return PlanAssignment(Ids.Select(Id => ById[Id]).ToArray(), Targets);
            if (Displaced.Any(Node => Node.PlacementHintLocked)) return null;
            Ids.UnionWith(Displaced.Select(Node => Node.Id));
        }
        return null;
    }

    /// <summary>Translates a complete rectangle using checked integer coordinates, never a pixel displacement.</summary>
    /// <param name="Region">Original occupied rectangle.</param>
    /// <param name="Rows">Whole-row delta.</param>
    /// <param name="Columns">Whole-column delta.</param>
    /// <returns>The complete contiguous target reservation.</returns>
    private static FlowchartCellRegion Translate(FlowchartCellRegion Region, int Rows, int Columns) =>
        new(checked(Region.StartRow + Rows), checked(Region.StartColumn + Columns), Region.RowSpan, Region.ColumnSpan);

    /// <summary>A validated complete repair; preparing it has no side effects and applying it publishes one immutable state.</summary>
    internal sealed class Assignment
    {
        private readonly FlowchartLogicalGrid Grid;
        /// <summary>Captures participants and both complete states without retaining mutable target dictionaries.</summary>
        /// <param name="Grid">Owning diagram grid.</param>
        /// <param name="Nodes">Complete moving or gap-affected participant set.</param>
        /// <param name="Before">Exact original ownership and gaps.</param>
        /// <param name="After">Complete validated target ownership and gaps.</param>
        internal Assignment(FlowchartLogicalGrid Grid, IReadOnlyList<DiagramGridLayout.GridNode> Nodes,
            FlowchartLogicalGrid.Snapshot Before, FlowchartLogicalGrid.Snapshot After)
        {
            this.Grid = Grid; this.Nodes = Array.AsReadOnly(Nodes.ToArray()); this.Before = Before; this.After = After;
        }
        /// <summary>All participants, including displaced neighbours, for the caller's hint and route-chain checks.</summary>
        internal IReadOnlyList<DiagramGridLayout.GridNode> Nodes { get; }
        /// <summary>Exact source regions and gaps for complete rollback.</summary>
        internal FlowchartLogicalGrid.Snapshot Before { get; }
        /// <summary>Complete target regions and gaps, fixed before any live mutation.</summary>
        internal FlowchartLogicalGrid.Snapshot After { get; }
        /// <summary>Applies every target together only if its original state is still current.</summary>
        /// <returns>False without mutation for a stale candidate or a participant pinned since planning.</returns>
        internal bool TryApply() => !Nodes.Any(Node => Node.PlacementHintLocked) && Grid.TryCommit(Before, After);
        /// <summary>Restores source ownership and every grown gap together without pixel copyback.</summary>
        internal void Rollback() => Grid.Restore(Before);
    }

    /// <summary>Requires one shared authoritative grid for a complete repair option.</summary>
    /// <param name="Nodes">Nodes participating in the option.</param>
    /// <param name="Grid">Receives their common owning grid.</param>
    /// <returns>Whether every node is cell-bound to the same grid.</returns>
    private static bool TryGrid(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, out FlowchartLogicalGrid Grid)
    {
        ArgumentNullException.ThrowIfNull(Nodes);
        Grid = Nodes.Count > 0 ? Nodes[0].CellNode?.Grid! : null!;
        return Grid != null && Nodes.All(Node => ReferenceEquals(Node.CellNode?.Grid, Nodes[0].CellNode!.Grid));
    }
}
