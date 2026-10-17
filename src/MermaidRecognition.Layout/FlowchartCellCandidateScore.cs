using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Projects complete cell candidates into the existing placement score without live geometry mutation.</summary>
internal static class FlowchartCellCandidateScore
{
    /// <summary>Measures diagram body bounds and optionally empty space using unchanged score weights.</summary>
    /// <param name="Nodes">Complete scored membership.</param>
    /// <param name="State">Immutable ownership and gaps belonging to these nodes.</param>
    /// <param name="IncludeEmptySpace">Whether the caller's established policy includes its node-area empty-space term.</param>
    /// <returns>The existing placement score and area tie-break.</returns>
    internal static (double Score, double Area) Measure(IReadOnlyList<DiagramGridLayout.GridNode> Nodes,
        FlowchartLogicalGrid.Snapshot State, bool IncludeEmptySpace = true)
    {
        var Bounds = Nodes.Select(Node => Node.CellNode!.Bounds(State.Regions[Node.Id], Node.GeometryProvider, State)).ToArray();
        double Width = Bounds.Max(Box => Box.CenterX + Box.Width / 2) - Bounds.Min(Box => Box.CenterX - Box.Width / 2);
        double Height = Bounds.Max(Box => Box.CenterY + Box.Height / 2) - Bounds.Min(Box => Box.CenterY - Box.Height / 2);
        double Used = IncludeEmptySpace ? Bounds.Sum(Box => (Box.Width + DiagramGridMetrics.NODE_GAP_PX) * (Box.Height + DiagramGridMetrics.NODE_GAP_PX)) : 0;
        double Empty = IncludeEmptySpace ? Math.Max(0, 1 - Used / Math.Max(1, Width * Height)) : 0;
        return (FlowchartLayoutScore.Calculate(Width, Height, 0, Empty), Width * Height);
    }
}
