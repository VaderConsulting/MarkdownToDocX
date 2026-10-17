using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Removes empty strips without changing ranks, order or alignment.</summary>
internal static class FlowchartGridCompaction
{
    internal static void Apply(List<DiagramGridLayout.GridNode> Nodes, bool Horizontal, int MinimumGap)
    {
        if (Nodes.Any(Node => Node.CellNode != null))
        {
            ApplyCells(Nodes, Horizontal, MinimumGap);
            return;
        }
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        var Bands = Nodes.GroupBy(Cross).OrderBy(Band => Band.Key).ToList();
        double OccupiedEnd = double.NegativeInfinity;
        int Shift = 0;
        foreach (var Band in Bands)
        {
            double Extent = Band.Max(Half);
            double Start = Band.Key - Extent;
            if (!double.IsNegativeInfinity(OccupiedEnd))
                Shift += Math.Max(0, (int)Math.Floor((Start - OccupiedEnd - MinimumGap) / DiagramGridMetrics.GRID_UNIT_PX))
                    * DiagramGridMetrics.GRID_UNIT_PX;
            foreach (var Node in Band)
            {
                if (Horizontal) Node.Y -= Shift; else Node.X -= Shift;
            }
            OccupiedEnd = Math.Max(OccupiedEnd, Band.Key + Extent);
        }
    }

    /// <summary>Removes only wholly empty cross-axis strips and publishes all translated reservations and required gaps together.</summary>
    /// <param name="Nodes">Complete diagram ownership.</param>
    /// <param name="Horizontal">Whether the cross axis is logical rows.</param>
    /// <param name="MinimumGap">Existing measured cross-axis clearance requirement, in drawing units.</param>
    /// <returns>Whether the complete compaction plan was legal and accepted.</returns>
    internal static bool ApplyCells(List<DiagramGridLayout.GridNode> Nodes, bool Horizontal, int MinimumGap)
    {
        if (Nodes.Count == 0) return false;
        var Before = FlowchartCellRepair.Capture(Nodes);
        var Grid = Nodes[0].CellNode!.Grid;
        var Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
        var Growth = new List<FlowchartCellGapGrowth>();
        var Bands = Before.Regions.GroupBy(Pair => Horizontal ? Pair.Value.StartRow : Pair.Value.StartColumn).OrderBy(Band => Band.Key);
        int? End = null;
        int Removed = 0;
        foreach (var Band in Bands)
        {
            if (End.HasValue)
            {
                int Empty = Math.Max(0, Band.Key - End.Value);
                Removed = checked(Removed + Empty);
                if (Band.Key >= End.Value)
                {
                    int After = checked(Band.Key - Removed - 1);
                    var Gaps = Horizontal ? Before.RowGaps : Before.ColumnGaps;
                    double Existing = Gaps.GetValueOrDefault(After, Horizontal ? Grid.Configuration.RowGap : Grid.Configuration.ColumnGap);
                    if (Existing < MinimumGap) Growth.Add(new(!Horizontal, After, MinimumGap - Existing, DiagramGridMetrics.GRID_UNIT_PX));
                }
            }
            foreach (var Pair in Band)
            {
                var Region = Pair.Value;
                Targets.Add(Pair.Key, new(Horizontal ? checked(Region.StartRow - Removed) : Region.StartRow,
                    Horizontal ? Region.StartColumn : checked(Region.StartColumn - Removed), Region.RowSpan, Region.ColumnSpan));
            }
            int BandEnd = Band.Max(Pair => Horizontal ? Pair.Value.EndRow : Pair.Value.EndColumn);
            End = End.HasValue ? Math.Max(End.Value, BandEnd) : BandEnd;
        }
        // A lock or collision rejects regions and all gap growth together.
        return FlowchartCellRepair.PlanAssignment(Nodes, Targets, Growth)?.TryApply() == true;
    }
}
