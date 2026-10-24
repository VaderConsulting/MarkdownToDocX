using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Reuses whole-cell lanes across disjoint rank strips through one complete reservation plan.</summary>
internal static partial class FlowchartSparseRankCompaction
{
    /// <summary>Compacts sparse logical cross-axis bands while retaining rank-skipping corridors and shared gap clearance.</summary>
    /// <param name="Contract">Relationships whose skipped ranks retain side corridors.</param>
    /// <param name="Nodes">All diagram ownership.</param>
    /// <param name="Horizontal">Whether cross-axis bands use rows.</param>
    /// <returns>Whether a complete legal compaction plan was accepted.</returns>
    internal static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Nodes.Count == 0) return false;
        var Before = FlowchartCellRepair.Capture(Nodes); var Grid = Nodes[0].CellNode!.Grid;
        var Bands = Nodes.GroupBy(Node => Horizontal ? Before.Regions[Node.Id].StartRow : Before.Regions[Node.Id].StartColumn)
            .OrderBy(Band => Band.Key).Select(Band => Band.ToArray()).ToArray();
        if (Bands.Length <= 2 * Nodes.GroupBy(Node => Node.Rank).Max(Rank => Rank.Count())) return false;
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var Related = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && ById.TryGetValue(Edge.SourceId, out var Source) && ById.TryGetValue(Edge.TargetId, out var Target) && Math.Abs(Source.Rank - Target.Rank) > 1)
            .SelectMany(Edge => new[] { (Edge.SourceId, Edge.TargetId), (Edge.TargetId, Edge.SourceId) }).ToHashSet();
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var Earlier = new List<DiagramGridLayout.GridNode>(); var Gaps = new Dictionary<int, double>();
        foreach (var Band in Bands)
        {
            int Candidate = Bands[0].Min(Node => Horizontal ? Before.Regions[Node.Id].StartRow : Before.Regions[Node.Id].StartColumn);
            foreach (var Node in Band)
            foreach (var Other in Earlier)
            {
                var A = Targets[Node.Id]; var B = Targets[Other.Id];
                bool MainOverlap = Horizontal ? A.StartColumn < B.EndColumn && B.StartColumn < A.EndColumn
                    : A.StartRow < B.EndRow && B.StartRow < A.EndRow;
                if (!Related.Contains((Node.Id, Other.Id)) && !MainOverlap) continue;
                Candidate = Math.Max(Candidate, Horizontal ? B.EndRow : B.EndColumn);
            }
            if (Band.Any(Node => Node.PlacementHintLocked)) Candidate = Band.Min(Node => Horizontal ? Before.Regions[Node.Id].StartRow : Before.Regions[Node.Id].StartColumn);
            foreach (var Node in Band)
            {
                var Region = Before.Regions[Node.Id];
                Targets[Node.Id] = new(Horizontal ? Candidate : Region.StartRow, Horizontal ? Region.StartColumn : Candidate, Region.RowSpan, Region.ColumnSpan);
            }
            // Preserve the existing two-node-gap corridor requirement through
            // a shared external gap, not a 30-pixel node displacement.
            if (Earlier.Any(Other => Band.Any(Node => Related.Contains((Node.Id, Other.Id))
                || (Horizontal ? Targets[Node.Id].StartColumn < Targets[Other.Id].EndColumn && Targets[Other.Id].StartColumn < Targets[Node.Id].EndColumn
                    : Targets[Node.Id].StartRow < Targets[Other.Id].EndRow && Targets[Other.Id].StartRow < Targets[Node.Id].EndRow))))
            {
                int After = checked(Candidate - 1);
                double Existing = (Horizontal ? Before.RowGaps : Before.ColumnGaps).GetValueOrDefault(After,
                    Horizontal ? Grid.Configuration.RowGap : Grid.Configuration.ColumnGap);
                double Missing = 2 * DiagramGridMetrics.NODE_GAP_PX - Existing;
                if (Missing > 0) Gaps[After] = Math.Max(Gaps.GetValueOrDefault(After), Missing);
            }
            Earlier.AddRange(Band);
        }
        // Internal gaps are not new side corridors. Reject rather than enlarge
        // an unrelated spanning node during this compaction candidate.
        if (Gaps.Keys.Any(After => Targets.Values.Any(Region => Horizontal
            ? Region.StartRow <= After && Region.EndRow > After + 1 : Region.StartColumn <= After && Region.EndColumn > After + 1))) return false;
        return FlowchartCellRepair.PlanAssignment(Nodes, Targets,
            Gaps.Select(Pair => new FlowchartCellGapGrowth(!Horizontal, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray())?.TryApply() == true;
    }
}
