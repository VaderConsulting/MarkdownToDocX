using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Packs complete standalone group membership through pure cell-grid candidates.</summary>
internal static partial class FlowchartGroupMemberPacking
{
    /// <summary>Scores every finite declared-order column count and publishes only the best complete legal improvement.</summary>
    /// <param name="Members">Complete standalone group in declared order.</param>
    /// <returns>Whether the accepted complete reservation plan improves the established block score.</returns>
    internal static bool ApplyCellMembers(List<DiagramGridLayout.GridNode> Members)
    {
        if (Members.Count < 2) return false;
        var Before = FlowchartCellRepair.Capture(Members);
        int Rows = Members.Max(Node => Before.Regions[Node.Id].RowSpan), Columns = Members.Max(Node => Before.Regions[Node.Id].ColumnSpan);
        long Left = Before.Regions.Values.Min(Region => Region.StartColumn), Right = Before.Regions.Values.Max(Region => Region.EndColumn);
        long Top = Before.Regions.Values.Min(Region => Region.StartRow), Bottom = Before.Regions.Values.Max(Region => Region.EndRow);
        var Current = FlowchartCellCandidateScore.Measure(Members, Before, IncludeEmptySpace: false);
        double BestScore = Current.Score, BestArea = Current.Area;
        FlowchartCellRepair.Assignment? Best = null;
        for (int Count = 1; Count <= Members.Count; Count++)
        {
            int RowCount = (Members.Count + Count - 1) / Count;
            int StartRow = checked((int)Math.Floor((Top + Bottom - (long)RowCount * Rows) / 2d));
            int StartColumn = checked((int)Math.Floor((Left + Right - (long)Count * Columns) / 2d));
            var Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
            for (int Index = 0; Index < Members.Count; Index++)
            {
                var Region = Before.Regions[Members[Index].Id];
                Targets.Add(Members[Index].Id, new(checked(StartRow + Index / Count * Rows),
                    checked(StartColumn + Index % Count * Columns), Region.RowSpan, Region.ColumnSpan));
            }
            var Plan = FlowchartCellRepair.PlanAssignment(Members, Targets);
            if (Plan == null) continue;
            var Candidate = FlowchartCellCandidateScore.Measure(Members, Plan.After, IncludeEmptySpace: false);
            if (Candidate.Score < BestScore - 0.0001 || Best != null && Candidate.Score == BestScore && Candidate.Area < BestArea)
            {
                Best = Plan; BestScore = Candidate.Score; BestArea = Candidate.Area;
            }
        }
        return Best?.TryApply() == true;
    }

}
