using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Applies fan-and-tail component wrapping through complete cell-region candidates.</summary>
internal static partial class FlowchartComponentWrapping
{
    private const int MaximumCompactionPasses = 16;

    internal static string? LastCellCompactionDiagnostic { get; private set; }

    internal static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Horizontal) return false;

        double Usable = FlowchartLayoutRules.DiagramUsableWidthPx;
        bool Changed = false;
        for (int Pass = 0; Pass < MaximumCompactionPasses; Pass++)
        {
            if (!TryAnalyze(Contract, Nodes, out ComponentContext Context))
            {
                LastCellCompactionDiagnostic = $"analyze-false-pass-{Pass}";
                break;
            }
            if (!TryApplyCellsOnce(Contract, Nodes, Context, Usable, out bool Accepted)) break;
            if (!Accepted) break;
            Changed = true;
            double Width = MeasureWidth(Nodes, FlowchartCellRepair.Capture(Nodes));
            if (Width <= Usable + 1e-9) break;
        }

        return Changed;
    }

    private static bool TryApplyCellsOnce(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        ComponentContext Context, double Usable, out bool Accepted)
    {
        Accepted = false;
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes);
        double OldWidth = MeasureWidth(Nodes, Before);
        if (OldWidth <= Usable + 1e-9)
        {
            LastCellCompactionDiagnostic = "already-within-usable";
            return false;
        }

        int NodeWidth = Nodes.Max(Node => Node.Width);
        int NodeHeight = Nodes.Max(Node => Node.Height);
        int ColumnPitch = NodeWidth + DiagramGridMetrics.NODE_GAP_PX;
        int RowPitch = NodeHeight + DiagramGridMetrics.NODE_GAP_PX;
        int RowStride = Before.Regions.Values.Max(Region => Region.RowSpan);
        int ColumnStride = Before.Regions.Values.Max(Region => Region.ColumnSpan);
        if (RowStride == 0 || ColumnStride == 0)
        {
            LastCellCompactionDiagnostic = "zero-stride";
            return false;
        }

        int MaximumRun = Context.Tail.Count;
        for (int Run = MaximumRun; Run >= FlowchartWrapPlan.MinimumRun; Run--)
        {
            Plan Best = Choose(Contract, Context.Components, Context.Main, Context.Tail, Context.FanGrid.Count,
                Context.RelationalLeaves.Count, NodeWidth, NodeHeight, ColumnPitch, RowPitch, NarrowWidthCompaction: true,
                MinimumRun: Run, MaximumRun: Run);
            if (!TryAssignCompactionPlan(Nodes, Context, Before, Best, RowStride, ColumnStride, Usable, OldWidth,
                    out Accepted, out double NewWidth, out FlowchartCellRepair.Assignment? Assignment))
                continue;
            try
            {
                if (!Assignment!.TryApply())
                {
                    LastCellCompactionDiagnostic = "try-apply-false";
                    continue;
                }
                NewWidth = MeasureWidth(Nodes, Assignment.After);
                if (NewWidth >= OldWidth - 1e-9)
                {
                    LastCellCompactionDiagnostic = $"no-shrink-{NewWidth:R}-vs-{OldWidth:R}";
                    continue;
                }
                Accepted = true;
                if (NewWidth <= Usable + 1e-9) LastCellCompactionDiagnostic = "within-usable";
                return true;
            }
            finally
            {
                if (!Accepted) Assignment?.Rollback();
            }
        }

        LastCellCompactionDiagnostic = "all-runs-failed";
        return false;
    }

    private static bool TryAssignCompactionPlan(List<DiagramGridLayout.GridNode> Nodes, ComponentContext Context,
        FlowchartLogicalGrid.Snapshot Before, Plan Best, int RowStride, int ColumnStride, double Usable, double OldWidth,
        out bool Accepted, out double NewWidth, out FlowchartCellRepair.Assignment? Assignment)
    {
        Accepted = false;
        NewWidth = OldWidth;
        Assignment = null;
        int OriginRow = 0;
        int OriginColumn = 0;
        int FanRows = DivideRoundUp(Context.FanGrid.Count, Best.FanColumns);
        int HubRow = checked(OriginRow + FanRows * RowStride);
        var Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);

        FlowchartCellRegion RootRegion = Before.Regions[Context.Root.Id];
        int RootColumn = checked(OriginColumn + ((Best.FanColumns - 1) / 2) * ColumnStride);
        Targets[Context.Root.Id] = new(HubRow, RootColumn, RootRegion.RowSpan, RootRegion.ColumnSpan);

        for (int Index = 0; Index < Context.FanGrid.Count; Index++)
        {
            FlowchartCellRegion Region = Before.Regions[Context.FanGrid[Index].Id];
            int Column = checked(OriginColumn + (Index % Best.FanColumns) * ColumnStride);
            int Row = checked(OriginRow + Index / Best.FanColumns * RowStride);
            Targets[Context.FanGrid[Index].Id] = new(Row, Column, Region.RowSpan, Region.ColumnSpan);
        }

        int TailColumn = checked(OriginColumn + (Best.FanColumns + 1) * ColumnStride);
        FlowchartCellRegion ContinuationRegion = Before.Regions[Context.Continuation.Id];
        Targets[Context.Continuation.Id] = new(HubRow, TailColumn, ContinuationRegion.RowSpan, ContinuationRegion.ColumnSpan);

        int TailStartRow = checked(HubRow + RowStride);
        PlaceCellColumnMajor(Context.Tail, Before, Targets, TailStartRow, TailColumn, Best.Run, ColumnStride, RowStride, Best.Offset);

        int BelowRow = checked(TailStartRow + (Best.Run + 1) * RowStride);
        foreach (List<DiagramGridLayout.GridNode> Component in Context.Components.Where(Component => Component != Context.Main))
        {
            PlaceCellColumnMajor(Component, Before, Targets, BelowRow, OriginColumn, Best.Run, ColumnStride, RowStride);
            BelowRow = checked(BelowRow + (Best.Run + 1) * RowStride);
        }

        int RelationalRow = checked(HubRow + RowStride);
        foreach (DiagramGridLayout.GridNode Child in Context.RelationalLeaves)
        {
            FlowchartCellRegion ChildRegion = Before.Regions[Child.Id];
            Targets[Child.Id] = new(RelationalRow, TailColumn, ChildRegion.RowSpan, ChildRegion.ColumnSpan);
            RelationalRow = checked(RelationalRow + RowStride);
        }

        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            if (!Targets.ContainsKey(Node.Id))
            {
                LastCellCompactionDiagnostic = $"missing-target-{Node.Id}";
                return false;
            }
        }

        if (TryFindOverlappingPair(Targets, out string First, out string Second))
        {
            LastCellCompactionDiagnostic = $"overlap-{First}-{Second}";
            return false;
        }

        Assignment = FlowchartCellRepair.PlanAssignment(Nodes, Targets, AllowHintedRegionRepair: true);
        if (Assignment == null)
        {
            LastCellCompactionDiagnostic = Nodes.Any(Node => Node.PlacementHintLocked)
                ? "plan-assignment-null-hints" : "plan-assignment-null";
            return false;
        }

        NewWidth = MeasureWidth(Nodes, Assignment.After);
        return NewWidth < OldWidth - 1e-9 || NewWidth <= Usable + 1e-9;
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

    private static bool TryFindOverlappingPair(IReadOnlyDictionary<string, FlowchartCellRegion> Targets,
        out string FirstId, out string SecondId)
    {
        var Entries = Targets.ToArray();
        for (int First = 0; First < Entries.Length; First++)
        for (int Second = 0; Second < First; Second++)
        {
            if (Entries[First].Value.Overlaps(Entries[Second].Value))
            {
                FirstId = Entries[First].Key;
                SecondId = Entries[Second].Key;
                return true;
            }
        }
        FirstId = SecondId = string.Empty;
        return false;
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
