using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Packs complete standalone group membership through pure cell-grid candidates.</summary>
internal static partial class FlowchartGroupMemberPacking
{
    /// <summary>Scores every finite declared-order column count and publishes only the best complete legal improvement.</summary>
    /// <param name="Members">Complete standalone group in declared order.</param>
    /// <returns>Whether a legal complete plan reduces wrap overflow, then block score and area.</returns>
    internal static bool ApplyCellMembers(List<DiagramGridLayout.GridNode> Members)
    {
        if (Members.Count < 2)
        {
            return false;
        }

        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Members);
        int Rows = Members.Max(Node => Before.Regions[Node.Id].RowSpan), Columns = Members.Max(Node => Before.Regions[Node.Id].ColumnSpan);
        long Left = Before.Regions.Values.Min(Region => Region.StartColumn), Right = Before.Regions.Values.Max(Region => Region.EndColumn);
        long Top = Before.Regions.Values.Min(Region => Region.StartRow), Bottom = Before.Regions.Values.Max(Region => Region.EndRow);
        (double Score, double Area) Current = FlowchartCellCandidateScore.Measure(Members, Before, IncludeEmptySpace: false);
        double BestScore = Current.Score, BestArea = Current.Area;
        // Rule 2.16 wraps the isolated piece's own rows/columns. The general
        // score has no height penalty, so a tall zero-score column must first
        // compete on its measured wrap overflow, rather than remain a tie.
        double WrapLimit = FlowchartLayoutRules.DiagramUsableWidthPx;
        (double Width, double Height) Overflow(FlowchartLogicalGrid.Snapshot State, double Area)
        {
            double Width = FlowchartCellCandidateScore.MeasureWidth(Members, State);
            return (Math.Max(0, Width - WrapLimit), Math.Max(0, Area / Width - WrapLimit));
        }
        var BestOverflow = Overflow(Before, Current.Area);
        FlowchartCellRepair.Assignment? Best = null;
        for (int Count = 1; Count <= Members.Count; Count++)
        {
            int RowCount = (Members.Count + Count - 1) / Count;
            int StartRow = checked((int)Math.Floor((Top + Bottom - ((long)RowCount * Rows)) / 2d));
            int StartColumn = checked((int)Math.Floor((Left + Right - ((long)Count * Columns)) / 2d));
            Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
            for (int Index = 0; Index < Members.Count; Index++)
            {
                FlowchartCellRegion Region = Before.Regions[Members[Index].Id];
                Targets.Add(Members[Index].Id, new(checked(StartRow + (Index / Count * Rows)),
                    checked(StartColumn + (Index % Count * Columns)), Region.RowSpan, Region.ColumnSpan));
            }
            FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Members, Targets);
            if (Plan == null)
            {
                continue;
            }

            (double Score, double Area) Candidate = FlowchartCellCandidateScore.Measure(Members, Plan.After, IncludeEmptySpace: false);
            var CandidateOverflow = Overflow(Plan.After, Candidate.Area);
            int WrapComparison = CandidateOverflow.CompareTo(BestOverflow);
            if (WrapComparison < 0 || (WrapComparison == 0 && (Candidate.Score < BestScore - 0.0001
                || (Candidate.Score == BestScore && Candidate.Area < BestArea))))
            {
                Best = Plan; BestScore = Candidate.Score; BestArea = Candidate.Area; BestOverflow = CandidateOverflow;
            }
        }
        return Best?.TryApply() == true;
    }

}
