using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Packs large fans through complete cell-region candidates and measured external channel gaps.</summary>
internal static partial class FlowchartFanPacking
{
    /// <summary>Scores every finite column-count candidate without live mutation and publishes the cheapest legal complete fan plan.</summary>
    /// <param name="Nodes">All diagram nodes, including unrelated obstacles.</param>
    /// <param name="Hub">The single fan hub.</param>
    /// <param name="Leaves">Declared-order simple fan members.</param>
    /// <param name="FanOut">Whether links leave the hub.</param>
    /// <param name="Horizontal">Whether primary flow uses columns.</param>
    /// <param name="Reverse">Whether primary flow runs towards smaller coordinates.</param>
    /// <param name="TrackGap">Existing measured channel clearance.</param>
    /// <param name="LabelTrackPitch">Existing measured cross-track label clearance.</param>
    /// <returns>Whether the complete legal fan candidate was accepted.</returns>
    internal static bool ApplyCellFan(List<DiagramGridLayout.GridNode> Nodes, DiagramGridLayout.GridNode Hub,
        List<DiagramGridLayout.GridNode> Leaves, bool FanOut, bool Horizontal, bool Reverse, int TrackGap, double LabelTrackPitch)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes); FlowchartLogicalGrid Grid = Hub.CellNode!.Grid;
        string Side = Horizontal ? (Reverse == FanOut ? "left" : "right") : (Reverse == FanOut ? "top" : "bottom");
        double PortPitch = FlowchartLayoutRules.LineSpacing;
        if (!DiagramGridLayout.TryPlanCellPortCapacity(Hub, Before.Regions[Hub.Id], Side, Leaves.Count, PortPitch, out FlowchartCellRegion? HubSpan))
        {
            return false;
        }

        bool MandatoryGrowth = HubSpan != Before.Regions[Hub.Id];
        int LeafRows = Leaves.Max(Node => Before.Regions[Node.Id].RowSpan), LeafColumns = Leaves.Max(Node => Before.Regions[Node.Id].ColumnSpan);
        int OriginRow = Leaves.Append(Hub).Min(Node => Before.Regions[Node.Id].StartRow);
        int OriginColumn = Leaves.Append(Hub).Min(Node => Before.Regions[Node.Id].StartColumn);
        double Usable = FlowchartLayoutRules.DiagramUsableWidthPx;
        (double Width, double Height) Overflow(FlowchartLogicalGrid.Snapshot State)
        {
            double Width = FlowchartCellCandidateScore.MeasureWidth(Leaves, State);
            double Area = FlowchartCellCandidateScore.Measure(Leaves, State, IncludeEmptySpace: false).Area;
            return (Math.Max(0, Width - Usable), Math.Max(0, Area / Width - Usable));
        }
        // Rule 2.18 wraps the fan block using its own measured footprint.
        // Unrelated components and mandatory hub/channel growth cannot make a
        // valid local pack ineligible merely because the whole diagram widens.
        var BestOverflow = Overflow(Before);
        var Initial = FlowchartCellCandidateScore.Measure(Nodes, Before);
        double BestScore = Initial.Score, BestArea = Initial.Area;
        FlowchartCellRepair.Assignment? Best = null;
        // Initial packing has one finite option per declared column count,
        // matching the existing fan planner. No route search occurs here.
        for (int Columns = 1; Columns <= Leaves.Count; Columns++)
        {
            int Rows = (Leaves.Count + Columns - 1) / Columns;
            double RequiredColumns = Horizontal ? DiagramGridMetrics.NODE_GAP_PX : Math.Max(DiagramGridMetrics.NODE_GAP_PX, (Rows - 1) * LabelTrackPitch);
            double RequiredRows = Horizontal ? Math.Max(DiagramGridMetrics.NODE_GAP_PX, (Columns - 1) * LabelTrackPitch) : DiagramGridMetrics.NODE_GAP_PX;
            int ColumnStride = CellFanStride(LeafColumns, Grid.Configuration.CellWidth, Grid.Configuration.ColumnGap, RequiredColumns);
            int RowStride = CellFanStride(LeafRows, Grid.Configuration.CellHeight, Grid.Configuration.RowGap, RequiredRows);
            if (ColumnStride == 0 || RowStride == 0)
            {
                continue;
            }

            int BlockColumns = checked(((Columns - 1) * ColumnStride) + LeafColumns), BlockRows = checked(((Rows - 1) * RowStride) + LeafRows);
            int HubRow, HubColumn, BlockRow, BlockColumn;
            if (Horizontal)
            {
                BlockRow = OriginRow;
                HubRow = checked(OriginRow + (int)Math.Floor((BlockRows - HubSpan.RowSpan) / 2d));
                BlockColumn = Reverse == FanOut ? OriginColumn : checked(OriginColumn + HubSpan.ColumnSpan);
                HubColumn = Reverse == FanOut ? checked(OriginColumn + BlockColumns) : OriginColumn;
            }
            else
            {
                BlockColumn = OriginColumn;
                HubColumn = checked(OriginColumn + (int)Math.Floor((BlockColumns - HubSpan.ColumnSpan) / 2d));
                BlockRow = Reverse == FanOut ? OriginRow : checked(OriginRow + HubSpan.RowSpan);
                HubRow = Reverse == FanOut ? checked(OriginRow + BlockRows) : OriginRow;
            }
            Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal)
                { [Hub.Id] = new(HubRow, HubColumn, HubSpan.RowSpan, HubSpan.ColumnSpan) };
            for (int Index = 0; Index < Leaves.Count; Index++)
            {
                FlowchartCellRegion Region = Before.Regions[Leaves[Index].Id];
                Targets[Leaves[Index].Id] = new(checked(BlockRow + (Index / Columns * RowStride)),
                    checked(BlockColumn + (Index % Columns * ColumnStride)), Region.RowSpan, Region.ColumnSpan);
            }
            int After = Horizontal ? (Reverse == FanOut ? HubColumn - 1 : checked(HubColumn + HubSpan.ColumnSpan - 1))
                : (Reverse == FanOut ? HubRow - 1 : checked(HubRow + HubSpan.RowSpan - 1));
            double Existing = (Horizontal ? Before.ColumnGaps : Before.RowGaps).GetValueOrDefault(After,
                Horizontal ? Grid.Configuration.ColumnGap : Grid.Configuration.RowGap);
            List<FlowchartCellGapGrowth> Growth = [];
            if (TrackGap > Existing)
            {
                if (Targets.Values.Any(Region => Horizontal ? Region.StartColumn <= After && Region.EndColumn > After + 1
                    : Region.StartRow <= After && Region.EndRow > After + 1))
                {
                    continue;
                }

                Growth.Add(new(Horizontal, After, TrackGap - Existing, DiagramGridMetrics.GRID_UNIT_PX));
            }
            FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Nodes, Targets, Growth);
            if (Plan == null)
            {
                continue;
            }

            var CandidateOverflow = Overflow(Plan.After);
            var Candidate = FlowchartCellCandidateScore.Measure(Nodes, Plan.After);
            int WrapComparison = CandidateOverflow.CompareTo(BestOverflow);
            bool Better = (Best == null && MandatoryGrowth) || WrapComparison < 0
                || (WrapComparison == 0 && (Candidate.Score < BestScore - 0.0001
                    || (Math.Abs(Candidate.Score - BestScore) < 0.0001 && Candidate.Area < BestArea - 0.0001)));
            if (Better) { Best = Plan; BestScore = Candidate.Score; BestArea = Candidate.Area; BestOverflow = CandidateOverflow; }
        }
        return Best?.TryApply() == true;
    }

    /// <summary>Finds a whole-cell slot stride whose guaranteed external clearance meets a measured need.</summary>
    /// <param name="Span">Maximum reserved leaf span.</param>
    /// <param name="Cell">Fixed configured base extent.</param>
    /// <param name="Gap">Fixed minimum gap; grown gaps can only improve this lower bound.</param>
    /// <param name="Needed">Measured label or track clearance.</param>
    /// <returns>A whole-cell stride, or zero when the existing named candidate limit cannot supply it.</returns>
    private static int CellFanStride(int Span, double Cell, double Gap, double Needed)
    {
        for (int Extra = 0; Extra < FlowchartLayoutRules.RepairCandidateMoves; Extra++)
        {
            if ((Extra * Cell) + ((Extra + 1d) * Gap) >= Needed)
            {
                return checked(Span + Extra);
            }
        }

        return 0;
    }

}
