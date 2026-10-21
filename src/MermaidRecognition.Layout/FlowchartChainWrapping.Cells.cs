using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Wraps a complete simple path through one cell-region and gap transaction.</summary>
internal static partial class FlowchartChainWrapping
{
    /// <summary>Plans whole-span row/column wrapping, grows measured label gaps and accepts only an improved aspect ratio.</summary>
    /// <param name="Path">Complete connected path in declared traversal order.</param>
    /// <param name="Horizontal">Whether traversal wraps across columns first.</param>
    /// <param name="ColumnGap">Measured horizontal label/routing clearance.</param>
    /// <param name="RowGap">Measured vertical label/routing clearance.</param>
    /// <returns>Whether the complete legal wrap improved the aspect ratio.</returns>
    internal static bool ApplyCellPath(List<DiagramGridLayout.GridNode> Path, bool Horizontal, int ColumnGap, int RowGap)
    {
        if (Path.Count < FlowchartWrapPlan.MinimumRun + 1) return false;
        var Before = FlowchartCellRepair.Capture(Path); var Grid = Path[0].CellNode!.Grid;
        int RowPitch = Path.Max(Node => Before.Regions[Node.Id].RowSpan), ColumnPitch = Path.Max(Node => Before.Regions[Node.Id].ColumnSpan);
        int Top = Path.Min(Node => Before.Regions[Node.Id].StartRow), Left = Path.Min(Node => Before.Regions[Node.Id].StartColumn);
        var Size = Grid.Measure(new(Top, Left, RowPitch, ColumnPitch));
        var Wrap = FlowchartWrapPlan.Choose(Path.Count, checked((int)Math.Ceiling(Size.Width)), checked((int)Math.Ceiling(Size.Height)),
            Math.Max(ColumnGap, checked((int)Math.Ceiling(Grid.Configuration.ColumnGap))),
            Math.Max(RowGap, checked((int)Math.Ceiling(Grid.Configuration.RowGap))), Horizontal);
        if (Wrap.Columns >= Path.Count || Wrap.Rows >= Path.Count) return false;
        double OldWidth = Path.Max(Node => Node.X + Node.Width / 2d) - Path.Min(Node => Node.X - Node.Width / 2d);
        double OldHeight = Path.Max(Node => Node.Y + Node.Height / 2d) - Path.Min(Node => Node.Y - Node.Height / 2d);
        var Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
        for (int Index = 0; Index < Path.Count; Index++)
        {
            int Column = Horizontal ? Index % Wrap.Columns : Index / Wrap.Rows;
            int Row = Horizontal ? Index / Wrap.Columns : Index % Wrap.Rows;
            var Region = Before.Regions[Path[Index].Id];
            Targets.Add(Path[Index].Id, new(checked(Top + Row * RowPitch), checked(Left + Column * ColumnPitch), Region.RowSpan, Region.ColumnSpan));
        }
        var Growth = new List<FlowchartCellGapGrowth>();
        for (int Column = 1; Column < Wrap.Columns; Column++)
        {
            int After = checked(Left + Column * ColumnPitch - 1);
            double Existing = Before.ColumnGaps.GetValueOrDefault(After, Grid.Configuration.ColumnGap);
            if (ColumnGap > Existing) Growth.Add(new(true, After, ColumnGap - Existing, DiagramGridMetrics.GRID_UNIT_PX));
        }
        for (int Row = 1; Row < Wrap.Rows; Row++)
        {
            int After = checked(Top + Row * RowPitch - 1);
            double Existing = Before.RowGaps.GetValueOrDefault(After, Grid.Configuration.RowGap);
            if (RowGap > Existing) Growth.Add(new(false, After, RowGap - Existing, DiagramGridMetrics.GRID_UNIT_PX));
        }
        var Plan = FlowchartCellRepair.PlanAssignment(Path, Targets, Growth);
        if (Plan?.TryApply() != true) return false;
        double UsableSpanPx = FlowchartLayoutRules.DiagramUsableWidthPx;
        if (Horizontal && OldWidth <= UsableSpanPx + 1e-9)
        {
            Plan.Rollback();
            return false;
        }

        if (!Horizontal && OldHeight <= UsableSpanPx + 1e-9)
        {
            Plan.Rollback();
            return false;
        }

        return true;
    }
}
