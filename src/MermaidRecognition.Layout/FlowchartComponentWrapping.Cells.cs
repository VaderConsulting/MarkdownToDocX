using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Applies fan-and-tail component wrapping through complete cell-region candidates.</summary>
internal static partial class FlowchartComponentWrapping
{
    internal static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Horizontal || !TryAnalyze(Contract, Nodes, out ComponentContext Context)) return false;

        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes);
        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        double OldWidth = MeasureWidth(Nodes, Before);
        double Usable = FlowchartLayoutRules.DiagramUsableWidthPx;
        if (OldWidth <= Usable + 1e-9) return false;

        int NodeWidth = Nodes.Max(Node => Node.Width);
        int NodeHeight = Nodes.Max(Node => Node.Height);
        int ColumnPitch = NodeWidth + DiagramGridMetrics.NODE_GAP_PX;
        int RowPitch = NodeHeight + DiagramGridMetrics.NODE_GAP_PX;
        Plan Best = Choose(Contract, Context.Components, Context.Main, Context.Tail, Context.FanGrid.Count,
            Context.RelationalLeaves.Count, NodeWidth, NodeHeight, ColumnPitch, RowPitch);
        if (Best.Width >= OldWidth - 1e-9) return false;

        int MaxColumnSpan = Context.FanGrid.Concat([Context.Root]).Max(Node => Before.Regions[Node.Id].ColumnSpan);
        int MaxRowSpan = Context.FanGrid.Concat([Context.Root]).Max(Node => Before.Regions[Node.Id].RowSpan);
        int ColumnStride = CellStride(MaxColumnSpan,
            Grid.Configuration.CellWidth, Grid.Configuration.ColumnGap, ColumnPitch - NodeWidth);
        int RowStride = CellStride(MaxRowSpan,
            Grid.Configuration.CellHeight, Grid.Configuration.RowGap, RowPitch - NodeHeight);
        if (ColumnStride == 0 || RowStride == 0) return false;

        FlowchartCellRegion RootRegion = Before.Regions[Context.Root.Id];
        int FanRows = DivideRoundUp(Context.FanGrid.Count, Best.FanColumns);
        int RowShift = Math.Max(0, FanRows * RowStride - RootRegion.StartRow);
        int HubRow = checked(RootRegion.StartRow + RowShift);
        int HubColumn = RootRegion.StartColumn;
        int SideColumn = Nodes.Min(Node => Before.Regions[Node.Id].StartColumn);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var Growth = new List<FlowchartCellGapGrowth>();

        int NextColumn = SideColumn;
        foreach (List<DiagramGridLayout.GridNode> Component in Context.Components.Where(Component => Component != Context.Main))
        {
            PlaceCellColumnMajor(Component, Before, Targets, HubRow, NextColumn, Best.Run, ColumnStride, RowStride);
            int Columns = DivideRoundUp(Component.Count, Best.Run);
            NextColumn = checked(NextColumn + (Columns + 1) * ColumnStride);
        }

        int FanLeftColumn = Math.Max(NextColumn, SideColumn);
        int RootColumn = checked(FanLeftColumn + ((Best.FanColumns - 1) / 2) * ColumnStride);
        Targets[Context.Root.Id] = new(HubRow, RootColumn, RootRegion.RowSpan, RootRegion.ColumnSpan);
        for (int Index = 0; Index < Context.FanGrid.Count; Index++)
        {
            FlowchartCellRegion Region = Before.Regions[Context.FanGrid[Index].Id];
            int Column = checked(FanLeftColumn + (Index % Best.FanColumns) * ColumnStride);
            int Row = checked(HubRow - (1 + Index / Best.FanColumns) * RowStride);
            Targets[Context.FanGrid[Index].Id] = new(Row, Column, Region.RowSpan, Region.ColumnSpan);
        }

        int TailColumn = checked(FanLeftColumn + (Best.FanColumns + 1) * ColumnStride);
        FlowchartCellRegion ContinuationRegion = Before.Regions[Context.Continuation.Id];
        Targets[Context.Continuation.Id] = new(HubRow, TailColumn, ContinuationRegion.RowSpan, ContinuationRegion.ColumnSpan);
        int TailStartRow = checked(HubRow + RowStride);
        PlaceCellColumnMajor(Context.Tail, Before, Targets, TailStartRow, TailColumn, Best.Run, ColumnStride, RowStride, Best.Offset);

        int ExteriorColumn = checked(TailColumn + (DivideRoundUp(Context.Tail.Count + Best.Offset, Best.Run) + 1) * ColumnStride);
        foreach (DiagramGridLayout.GridNode Child in Context.RelationalLeaves)
        {
            DiagramGridLayout.GridNode Parent = Context.Incoming[Child.Id]
                .Where(Id => Context.Tail.Contains(Context.ById[Id]))
                .Select(Id => Context.ById[Id]).First();
            FlowchartCellRegion ChildRegion = Before.Regions[Child.Id];
            FlowchartCellRegion ParentRegion = Targets[Parent.Id];
            Targets[Child.Id] = new(ParentRegion.StartRow, ExteriorColumn, ChildRegion.RowSpan, ChildRegion.ColumnSpan);
            ExteriorColumn = checked(ExteriorColumn + ColumnStride);
        }

        AppendColumnGrowth(Before, Grid, Growth, SideColumn, NextColumn, ColumnStride, ColumnPitch - NodeWidth);
        AppendColumnGrowth(Before, Grid, Growth, FanLeftColumn, ExteriorColumn, ColumnStride, ColumnPitch - NodeWidth);

        FlowchartCellRepair.Assignment? Assignment = FlowchartCellRepair.PlanAssignment(Nodes, Targets, Growth);
        if (Assignment == null) return false;
        bool Accepted = false;
        try
        {
            if (!Assignment.TryApply()) return false;
            double NewWidth = MeasureWidth(Nodes, Assignment.After);
            Accepted = NewWidth <= Usable + 1e-9 && NewWidth < OldWidth - 1e-9;
            return Accepted;
        }
        finally
        {
            if (!Accepted) Assignment.Rollback();
        }
    }

    private static void PlaceCellColumnMajor(IReadOnlyList<DiagramGridLayout.GridNode> Nodes,
        FlowchartLogicalGrid.Snapshot Before, Dictionary<string, FlowchartCellRegion> Targets,
        int StartRow, int StartColumn, int Rows, int ColumnStride, int RowStride, int Offset = 0)
    {
        for (int Index = 0; Index < Nodes.Count; Index++)
        {
            (int Column, int Row) = Cell(Index + Offset, Rows);
            FlowchartCellRegion Region = Before.Regions[Nodes[Index].Id];
            Targets[Nodes[Index].Id] = new(checked(StartRow + Row * RowStride), checked(StartColumn + Column * ColumnStride),
                Region.RowSpan, Region.ColumnSpan);
        }
    }

    private static void AppendColumnGrowth(FlowchartLogicalGrid.Snapshot Before, FlowchartLogicalGrid Grid,
        List<FlowchartCellGapGrowth> Growth, int FromColumn, int ToColumn, int Stride, double NeededGapPx)
    {
        for (int Column = FromColumn; Column < ToColumn; Column = checked(Column + Stride))
        {
            int After = checked(Column + Stride - 1);
            double Existing = Before.ColumnGaps.GetValueOrDefault(After, Grid.Configuration.ColumnGap);
            if (NeededGapPx > Existing) Growth.Add(new(true, After, NeededGapPx - Existing, DiagramGridMetrics.GRID_UNIT_PX));
        }
    }

    private static int CellStride(int Span, double Cell, double Gap, double NeededGapPx)
    {
        for (int Extra = 0; Extra < FlowchartLayoutRules.RepairCandidateMoves; Extra++)
        {
            double Clearance = Extra * Cell + (Extra + 1d) * Gap;
            if (Clearance >= NeededGapPx) return checked(Span + Extra);
        }

        return 0;
    }

    private static double MeasureWidth(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid.Snapshot State) =>
        FlowchartCellCandidateScore.MeasureWidth(Nodes, State);

    private sealed record ComponentContext(
        Dictionary<string, DiagramGridLayout.GridNode> ById,
        Dictionary<string, List<string>> Incoming,
        List<List<DiagramGridLayout.GridNode>> Components,
        List<DiagramGridLayout.GridNode> Main,
        DiagramGridLayout.GridNode Root,
        List<DiagramGridLayout.GridNode> FanGrid,
        List<DiagramGridLayout.GridNode> Tail,
        List<DiagramGridLayout.GridNode> RelationalLeaves,
        DiagramGridLayout.GridNode Continuation);
}
