using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Supplies same-rank terminal label room through complete cell and shared-gap candidates.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Measures labelled terminal shortfalls and plans legal external-gap or whole-cell alternatives before one publication.</summary>
    /// <param name="Contract">Declared labels and terminal relationships.</param>
    /// <param name="Placed">Complete shared diagram ownership.</param>
    /// <param name="HorizontalFlow">Whether the cross axis is logical rows.</param>
    /// <returns>Whether accepted ownership or gap geometry changed.</returns>
    internal static bool GrowCellTerminalSameRankLabelGaps(DiagramContract Contract, List<GridNode> Placed, bool HorizontalFlow)
    {
        var Before = FlowchartCellRepair.Capture(Placed); var Grid = Placed[0].CellNode!.Grid;
        var Regions = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var Growth = new Dictionary<int, double>(); var ById = IndexPlaced(Placed); var State = Before;
        var Options = FlowchartRenderOptions.FromContract(Contract); bool Changed = false;
        foreach (var Edge in Contract.Edges.OrderBy(Edge => Edge.CreationIndex))
        {
            if (string.IsNullOrWhiteSpace(Edge.Label) || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                || !ById.TryGetValue(Edge.SourceId, out var Source) || !ById.TryGetValue(Edge.TargetId, out var Target)
                || Contract.Edges.Any(Other => Other.SourceId == Target.Id && !string.Equals(Other.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))) continue;
            var Bounds = Placed.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Bounds(Regions[Node.Id], Node.GeometryProvider, State), StringComparer.Ordinal);
            var A = Bounds[Source.Id]; var B = Bounds[Target.Id];
            if (Math.Abs(HorizontalFlow ? A.CenterX - B.CenterX : A.CenterY - B.CenterY) > 0.5) continue;
            double Distance = HorizontalFlow ? B.CenterY - A.CenterY : B.CenterX - A.CenterX;
            if (Math.Abs(Distance) < 0.5) continue;
            double Clear = Math.Abs(Distance) - (HorizontalFlow ? A.Height + B.Height : A.Width + B.Width) / 2;
            var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label, Options, Options.EdgeLabelFontSizePx);
            double Required = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(Size, Options.EdgeLabelFontSizePx, Horizontal: !HorizontalFlow)
                + DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            if (Clear + 0.5 >= Required) continue;
            var ExistingFrames = ProjectCellFrames(Contract, Placed, State);
            var ExistingConflicts = ProjectedCellFrameConflictKeys(Contract, Placed, ExistingFrames, Bounds);
            FlowchartCellRepair.Assignment? Best = null; double BestScore = double.PositiveInfinity;
            int Sign = Math.Sign(Distance) * (HorizontalFlow && Target.CellNode!.UsesRoutingCoordinates ? -1 : 1);
            int Attempts = 0;
            for (int Move = 0; Attempts < FlowchartLayoutRules.RepairCandidateMoves; Move++)
            {
                Attempts++;
                var Trial = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal);
                var TrialGrowth = new Dictionary<int, double>(Growth);
                var Original = Regions[Target.Id];
                if (Move == 0)
                {
                    var S = Regions[Source.Id];
                    var Earlier = Sign > 0 ? S : Original; var Later = Sign > 0 ? Original : S;
                    int First = HorizontalFlow ? Earlier.EndRow - 1 : Earlier.EndColumn - 1;
                    int Last = HorizontalFlow ? Later.StartRow - 1 : Later.StartColumn - 1;
                    int? Gap = null;
                    for (int After = First; After <= Last; After++)
                    {
                        if (Trial.Values.Any(Region => HorizontalFlow ? Region.StartRow <= After && Region.EndRow > After + 1
                            : Region.StartColumn <= After && Region.EndColumn > After + 1)) continue;
                        Gap = After; break;
                    }
                    if (!Gap.HasValue) continue;
                    TrialGrowth[Gap.Value] = TrialGrowth.GetValueOrDefault(Gap.Value) + Required - Clear;
                }
                else
                {
                    if (Target.PlacementHintLocked) break;
                    Trial[Target.Id] = new(HorizontalFlow ? checked(Original.StartRow + Sign * Move) : Original.StartRow,
                        HorizontalFlow ? Original.StartColumn : checked(Original.StartColumn + Sign * Move), Original.RowSpan, Original.ColumnSpan);
                }
                var Plan = FlowchartCellRepair.PlanAssignment(Placed, Trial,
                    TrialGrowth.Select(Pair => new FlowchartCellGapGrowth(!HorizontalFlow, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray());
                if (Plan == null) continue;
                var CandidateBounds = Placed.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Bounds(Plan.After.Regions[Node.Id], Node.GeometryProvider, Plan.After), StringComparer.Ordinal);
                var P = CandidateBounds[Source.Id]; var Q = CandidateBounds[Target.Id];
                double GapSize = Math.Abs(HorizontalFlow ? Q.CenterY - P.CenterY : Q.CenterX - P.CenterX)
                    - (HorizontalFlow ? P.Height + Q.Height : P.Width + Q.Width) / 2;
                if (GapSize + 0.5 < Required) continue;
                var CandidateFrames = ProjectCellFrames(Contract, Placed, Plan.After);
                if (ProjectedCellFrameConflictKeys(Contract, Placed, CandidateFrames, CandidateBounds).Any(Key => !ExistingConflicts.Contains(Key))) continue;
                double Score = FlowchartCellCandidateScore.Measure(Placed, Plan.After).Score;
                if (Score < BestScore) { Best = Plan; BestScore = Score; }
            }
            if (Best == null) continue;
            Regions = new(Best.After.Regions, StringComparer.Ordinal);
            Growth = FlowchartCellRepair.GrownGapsSince(Before, Best.After, Columns: !HorizontalFlow);
            State = Best.After; Changed = true;
        }
        return Changed && FlowchartCellRepair.PlanAssignment(Placed, Regions,
            Growth.Select(Pair => new FlowchartCellGapGrowth(!HorizontalFlow, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray())?.TryApply() == true;
    }
}
