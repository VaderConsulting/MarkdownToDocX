using System;

namespace MermaidRecognition.Layout;

/// <summary>Chooses a grid wrap from measured node sizes, rule gaps and page usable width.</summary>
internal static class FlowchartWrapPlan
{
    internal const int MinimumRun = 4;

    internal readonly record struct Plan(int Columns, int Rows, int Width, int Height)
    {
        internal double Ratio => Height == 0 ? 0.0 : Width / (double)Height;
    }

    internal static Plan Choose(int NodeCount, int NodeWidth, int NodeHeight,
        int ColumnGap, int RankGap, bool Horizontal)
    {
        if (NodeCount < 1)
        {
            return default;
        }

        double UsableSpanPx = FlowchartLayoutRules.DiagramUsableWidthPx;
        if (Horizontal)
        {
            Plan SingleRow = Measure(NodeCount, NodeCount, NodeWidth, NodeHeight, ColumnGap, RankGap);
            if (SingleRow.Width <= UsableSpanPx + 1e-9)
            {
                return SingleRow;
            }

            int ColumnsPerRow = MaxUnitsPerSpan(NodeCount, NodeWidth, ColumnGap, UsableSpanPx);
            return Measure(NodeCount, ColumnsPerRow, NodeWidth, NodeHeight, ColumnGap, RankGap);
        }

        Plan SingleColumn = Measure(NodeCount, 1, NodeWidth, NodeHeight, ColumnGap, RankGap);
        if (SingleColumn.Height <= UsableSpanPx + 1e-9)
        {
            return SingleColumn;
        }

        int RowsPerColumn = MaxUnitsPerSpan(NodeCount, NodeHeight, RankGap, UsableSpanPx);
        int Columns = DivideRoundUp(NodeCount, RowsPerColumn);
        return Measure(NodeCount, Columns, NodeWidth, NodeHeight, ColumnGap, RankGap);
    }

    private static int MaxUnitsPerSpan(int NodeCount, int UnitSize, int Gap, double UsableSpanPx)
    {
        int Best = 1;
        for (int Count = 1; Count <= NodeCount; Count++)
        {
            int Span = Count * UnitSize + Math.Max(0, Count - 1) * Gap;
            if (Span <= UsableSpanPx + 1e-9)
            {
                Best = Count;
            }
            else
            {
                break;
            }
        }

        return Best;
    }

    private static Plan Measure(int NodeCount, int Columns, int NodeWidth, int NodeHeight,
        int ColumnGap, int RankGap)
    {
        Columns = Math.Max(1, Math.Min(NodeCount, Columns));
        int Rows = DivideRoundUp(NodeCount, Columns);
        int Width = Columns * NodeWidth + Math.Max(0, Columns - 1) * ColumnGap;
        int Height = Rows * NodeHeight + Math.Max(0, Rows - 1) * RankGap;
        return new Plan(Columns, Rows, Width, Height);
    }

    private static int DivideRoundUp(int Dividend, int Divisor) => (Dividend + Divisor - 1) / Divisor;
}
