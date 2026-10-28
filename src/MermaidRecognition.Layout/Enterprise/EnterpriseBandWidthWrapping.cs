using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout.Enterprise;

internal static partial class EnterpriseBandPlacement
{
    private sealed partial class BandPlan
    {
        /// <summary>Folds peers in vertical linked ranks into additional cell rows before routing.</summary>
        private void WrapIndependentRankRows(Dictionary<string, FlowchartCellRegion> Targets, int Wrap)
        {
            // Group blocks, explicit hints and horizontal main-axis chains retain
            // their existing semantics in this first width experiment.
            if (Wrap == int.MaxValue || Contract.Groups.Count > 0 || Contract.Direction is "LR" or "RL"
                || Contract.Nodes.Any(Node => Node.PlacementHint?.IsActive == true)) return;
            int AddedRows = 0;
            foreach (var Rank in Nodes.GroupBy(Node => Targets[Node.Id].StartRow).OrderBy(Group => Group.Key).ToArray())
            {
                var Members = Rank.OrderBy(Node => Targets[Node.Id].StartColumn).ThenBy(Node => Node.Id, StringComparer.Ordinal).ToArray();
                int Top = checked(Rank.Key + AddedRows);
                int Row = Top, Column = 0, RowHeight = 0;
                int OriginalHeight = Members.Max(Node => Targets[Node.Id].RowSpan);
                foreach (var Node in Members)
                {
                    var Region = Targets[Node.Id];
                    if (Column > 0 && (long)Column + Region.ColumnSpan > Wrap)
                    {
                        Row = checked(Row + RowHeight);
                        Column = 0; RowHeight = 0;
                    }
                    Targets[Node.Id] = new(Row, Column, Region.RowSpan, Region.ColumnSpan);
                    Column = checked(Column + Region.ColumnSpan);
                    RowHeight = Math.Max(RowHeight, Region.RowSpan);
                }
                AddedRows = checked(AddedRows + Row + RowHeight - Top - OriginalHeight);
            }
        }
    }
}
