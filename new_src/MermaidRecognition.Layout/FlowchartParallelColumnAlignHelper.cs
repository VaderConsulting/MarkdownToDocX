using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Aligns nodes in parallel diagram columns to shared flow-axis rows (F73 parity).
/// </summary>
public static class FlowchartParallelColumnAlignHelper
{
    private const double COLUMN_CLUSTER_TOLERANCE_PX = 40.0;

    private const double ROW_ALIGN_TOLERANCE_PX = 1.0;

    private const double VERTICAL_STACK_MIN_SEPARATION_PX = 20.0;

    /// <summary>
    /// Aligns parallel column chains so corresponding rows share the same cross-axis coordinate.
    /// </summary>
    public static void Apply(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        _ = Options;

        if (MutableById.Count < 4)
        {
            return;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        List<List<string>> ColumnChains = FlowchartParallelColumnChainBuilder.BuildColumnChains(
            Contract,
            MutableById,
            COLUMN_CLUSTER_TOLERANCE_PX,
            VERTICAL_STACK_MIN_SEPARATION_PX);
        if (ColumnChains.Count < 2)
        {
            return;
        }

        int MaxRowCount = ColumnChains.Max(Chain => Chain.Count);
        if (MaxRowCount < 2)
        {
            return;
        }

        List<double> TargetRowCenterByIndex = new List<double>(MaxRowCount);
        for (int RowIndex = 0; RowIndex < MaxRowCount; RowIndex++)
        {
            double RowTarget = double.NegativeInfinity;
            bool HasRow = false;

            foreach (List<string> Chain in ColumnChains)
            {
                if (RowIndex >= Chain.Count)
                {
                    continue;
                }

                if (!MutableById.TryGetValue(Chain[RowIndex], out FlowchartLayoutNodeBounds Bounds))
                {
                    continue;
                }

                double RowCenter = Bounds.CenterY;
                if (!HasRow || RowCenter > RowTarget)
                {
                    HasRow = true;
                    RowTarget = RowCenter;
                }
            }

            if (!HasRow)
            {
                RowTarget = 0.0;
            }

            TargetRowCenterByIndex.Add(RowTarget);
        }

        foreach (List<string> Chain in ColumnChains)
        {
            for (int RowIndex = 0; RowIndex < Chain.Count; RowIndex++)
            {
                string NodeId = Chain[RowIndex];
                if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                {
                    continue;
                }

                double TargetRowCenter = TargetRowCenterByIndex[RowIndex];
                if (Math.Abs(Bounds.CenterY - TargetRowCenter) <= ROW_ALIGN_TOLERANCE_PX)
                {
                    continue;
                }

                MutableById[NodeId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = Bounds.CenterX,
                    CenterY = TargetRowCenter,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                };
            }
        }
    }

}
