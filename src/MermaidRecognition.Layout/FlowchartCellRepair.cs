using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Applies complete geometry repair candidates to cell ownership, never to derived pixel bounds.</summary>
internal static class FlowchartCellRepair
{
    /// <summary>Captures cell state when present, while leaving the existing open-mode path separate.</summary>
    /// <param name="Nodes">Complete geometry participants.</param>
    /// <returns>Cell state, or null when every node still belongs to the legacy open or placement path.</returns>
    internal static FlowchartLogicalGrid.Snapshot? CaptureIfBound(IReadOnlyList<DiagramGridLayout.GridNode> Nodes)
    {
        if (Nodes.All(Node => Node.CellNode == null)) return null;
        return Capture(Nodes);
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
    {
        if (!TryGrid(Nodes, out var Grid) || Nodes.Any(Node => Node.PlacementHintLocked)) return false;
        return Grid.TryMove(Nodes.Select(Node => Node.Id).ToArray(), Rows, Columns);
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
        return Node.CellNode.Grid.TryResize(Node.Id, Rows, Columns);
    }

    /// <summary>Repairs a measured routing-space shortfall by changing a shared gap, not by translating pixel boxes.</summary>
    /// <param name="Nodes">All nodes whose derived positions or dimensions may change.</param>
    /// <param name="Horizontal">True for a column gap, false for a row gap.</param>
    /// <param name="After">The declared row or column preceding the gap.</param>
    /// <param name="Shortfall">Measured missing clearance in pixels.</param>
    /// <returns>False without mutation if the change would move a pinned node or has no measured shortfall.</returns>
    internal static bool TryGrowGap(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, bool Horizontal, int After, double Shortfall)
    {
        if (!TryGrid(Nodes, out var Grid) || Shortfall <= 0) return false;
        if (Nodes.Any(Node => Node.PlacementHintLocked && AffectedByGap(Node.CellNode!.Region, Horizontal, After))) return false;
        Grid.GrowGap(Horizontal, After, Shortfall, DiagramGridMetrics.GRID_UNIT_PX);
        return true;
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
    {
        ArgumentNullException.ThrowIfNull(Changes);
        if (!TryGrid(Nodes, out var Grid)) return false;
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        foreach (var Change in Changes)
        {
            if (!ById.TryGetValue(Change.Key, out var Node)) return false;
            var Before = Grid.RegionOf(Change.Key);
            if (Node.PlacementHintLocked && Change.Value != Before) return false;
        }
        return Grid.TryApply(Changes);
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
