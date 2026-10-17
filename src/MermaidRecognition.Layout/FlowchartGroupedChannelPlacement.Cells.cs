using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Places complete grouped many-to-many channels through parent-grid region and gap plans.</summary>
internal static partial class FlowchartGroupedChannelPlacement
{
    /// <summary>Identifies a complete unhinted pair whose later channel placement supersedes old parallel-rank preparation.</summary>
    /// <param name="Contract">Membership and honoured placement hints.</param>
    /// <param name="Nodes">Every present node.</param>
    /// <param name="Sources">Complete source membership, already checked for bipartite topology.</param>
    /// <param name="Targets">Complete target membership, already checked for bipartite topology.</param>
    /// <returns>Whether the cell channel planner owns this block.</returns>
    internal static bool OwnsCompleteCellBlock(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        DiagramGridLayout.GridNode[] Sources, DiagramGridLayout.GridNode[] Targets)
    {
        string? A = Sources[0].GroupId, B = Targets[0].GroupId;
        if (A == null || B == null || A == B || Sources.Any(Node => Node.GroupId != A) || Targets.Any(Node => Node.GroupId != B)
            || Nodes.Count(Node => Node.GroupId == A) != Sources.Length || Nodes.Count(Node => Node.GroupId == B) != Targets.Length) return false;
        var Members = Sources.Concat(Targets).ToArray(); var Ids = Members.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        return !Members.Any(Node => Node.PlacementHintLocked)
            && !Contract.Nodes.Any(Node => Ids.Contains(Node.Id) && Node.PlacementHint?.IsActive == true)
            && !Contract.Groups.Any(Group => Group.PlacementHint?.IsActive == true
                && Members.Any(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Group.Id)));
    }

    /// <summary>Plans individually measured side capacity, both complete groups and their shared routing channel before publication.</summary>
    /// <param name="Nodes">All diagram ownership and gap participants.</param>
    /// <param name="Sources">Declared-order source group.</param>
    /// <param name="Targets">Declared-order target group.</param>
    /// <returns>Whether the cheapest complete legal channel candidate was accepted.</returns>
    internal static bool ApplyCellBlock(List<DiagramGridLayout.GridNode> Nodes, DiagramGridLayout.GridNode[] Sources,
        DiagramGridLayout.GridNode[] Targets)
    {
        var Before = FlowchartCellRepair.Capture(Nodes); var Grid = Nodes[0].CellNode!.Grid;
        var Spans = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        foreach (var Node in Sources.Concat(Targets))
        {
            bool Source = Sources.Contains(Node);
            if (!DiagramGridLayout.TryPlanCellPortCapacity(Node, Spans[Node.Id], Source ? "right" : "left",
                Source ? Targets.Length : Sources.Length, DiagramGridMetrics.GRID_UNIT_PX, out var Region)) return false;
            Spans[Node.Id] = Region;
        }
        int SourceRows = Sources.Max(Node => Spans[Node.Id].RowSpan), TargetRows = Targets.Max(Node => Spans[Node.Id].RowSpan);
        int SourceColumns = Sources.Max(Node => Spans[Node.Id].ColumnSpan);
        int Left = Sources.Concat(Targets).Min(Node => Spans[Node.Id].StartColumn), Top = Sources.Concat(Targets).Min(Node => Spans[Node.Id].StartRow);
        int Tracks = checked(Sources.Length * Targets.Length - Math.Min(Sources.Length, Targets.Length));
        int Needed = checked(DiagramGridMetrics.CeilToGrid(4 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
            + Math.Max(0, Tracks - 1) * DiagramGridMetrics.GRID_UNIT_PX) + 2 * DiagramGridMetrics.GRID_UNIT_PX);
        FlowchartCellRepair.Assignment? Best = null; double BestScore = double.PositiveInfinity;
        for (int ExtraColumns = 0; ExtraColumns < FlowchartLayoutRules.RepairCandidateMoves; ExtraColumns++)
        {
            var Regions = new Dictionary<string, FlowchartCellRegion>(Spans, StringComparer.Ordinal);
            for (int Index = 0; Index < Sources.Length; Index++)
            {
                var Region = Spans[Sources[Index].Id]; Regions[Sources[Index].Id] = new(checked(Top + Index * SourceRows), Left, Region.RowSpan, Region.ColumnSpan);
            }
            for (int Index = 0; Index < Targets.Length; Index++)
            {
                var Region = Spans[Targets[Index].Id]; Regions[Targets[Index].Id] = new(checked(Top + Index * TargetRows),
                    checked(Left + SourceColumns + ExtraColumns), Region.RowSpan, Region.ColumnSpan);
            }
            int After = checked(Left + SourceColumns - 1);
            double End = Sources.Max(Node => { var Box = Node.CellNode!.Bounds(Regions[Node.Id], Node.GeometryProvider); return Box.CenterX + Box.Width / 2; });
            double Start = Targets.Min(Node => { var Box = Node.CellNode!.Bounds(Regions[Node.Id], Node.GeometryProvider); return Box.CenterX - Box.Width / 2; });
            double Missing = Needed - (Start - End); var Growth = new List<FlowchartCellGapGrowth>();
            if (Missing > 0)
            {
                if (Regions.Values.Any(Region => Region.StartColumn <= After && Region.EndColumn > After + 1)) continue;
                Growth.Add(new(true, After, Missing, DiagramGridMetrics.GRID_UNIT_PX));
            }
            var Plan = FlowchartCellRepair.PlanAssignment(Nodes, Regions, Growth);
            if (Plan == null) continue;
            double Score = FlowchartCellCandidateScore.Measure(Nodes, Plan.After).Score;
            if (Score < BestScore) { Best = Plan; BestScore = Score; }
        }
        return Best?.TryApply() == true;
    }
}
