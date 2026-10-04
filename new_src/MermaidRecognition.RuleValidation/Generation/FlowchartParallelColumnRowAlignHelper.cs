using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates parallel diagram columns share aligned flow-axis rows (F73).
/// </summary>
public static class FlowchartParallelColumnRowAlignHelper
{
    private const double COLUMN_CLUSTER_TOLERANCE_PX = 40.0;

    private const double ROW_ALIGN_TOLERANCE_PX = 2.0;

    private const double VERTICAL_STACK_MIN_SEPARATION_PX = 20.0;

    /// <summary>
    /// Records parallel column row alignment facts on the diagram layout.
    /// </summary>
    public static void ApplyParallelColumnRowAlignFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        Diagram.Layout["ParallelColumnRowsAligned"] = true;
        Diagram.Layout["HasParallelColumnLayout"] = false;

        if (BoundsById.Count < 4)
        {
            return;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        if (HorizontalFlow)
        {
            return;
        }

        List<List<string>> ColumnChains = FlowchartParallelColumnChainBuilder.BuildColumnChains(
            Contract,
            BoundsById,
            COLUMN_CLUSTER_TOLERANCE_PX,
            VERTICAL_STACK_MIN_SEPARATION_PX);
        if (ColumnChains.Count < 2)
        {
            return;
        }

        Diagram.Layout["HasParallelColumnLayout"] = true;

        int MaxRowCount = ColumnChains.Max(Chain => Chain.Count);
        if (MaxRowCount < 2)
        {
            return;
        }

        bool AnyRowViolation = false;
        for (int RowIndex = 0; RowIndex < MaxRowCount; RowIndex++)
        {
            List<double> RowCenters = [];
            foreach (List<string> Chain in ColumnChains)
            {
                if (RowIndex >= Chain.Count)
                {
                    continue;
                }

                if (BoundsById.TryGetValue(Chain[RowIndex], out FlowchartLayoutNodeBounds? Bounds))
                {
                    RowCenters.Add(Bounds.CenterY);
                }
            }

            if (RowCenters.Count < 2)
            {
                continue;
            }

            double MinCenter = RowCenters.Min();
            double MaxCenter = RowCenters.Max();
            if (MaxCenter - MinCenter > ROW_ALIGN_TOLERANCE_PX)
            {
                AnyRowViolation = true;
            }
        }

        Diagram.Layout["ParallelColumnRowsAligned"] = !AnyRowViolation;
    }

}
