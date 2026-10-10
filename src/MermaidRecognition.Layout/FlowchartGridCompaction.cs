using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Removes empty strips without changing ranks, order or alignment.</summary>
internal static class FlowchartGridCompaction
{
    internal static void Apply(List<DiagramGridLayout.GridNode> Nodes, bool Horizontal, int MinimumGap)
    {
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
}
