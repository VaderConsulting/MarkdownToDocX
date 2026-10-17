using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Assigns spine and margin lanes directly to whole-cell reservations.</summary>
public static partial class FlowchartSpineColumnPostProcessor
{
    /// <summary>Builds one complete spine/collector plan and rejects it unchanged if any occupancy or hint would be violated.</summary>
    /// <param name="Contract">Graph and branch labels used by the existing spine selection.</param>
    /// <param name="Nodes">All nodes sharing the diagram grid.</param>
    /// <param name="Direction">Physical diagram direction; horizontal layouts retain their established exemption.</param>
    /// <returns>Whether a complete legal plan was accepted.</returns>
    internal static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartDirection Direction)
    {
        if (Nodes.Count <= 1 || Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft) return false;
        var Before = FlowchartCellRepair.Capture(Nodes);
        if (!ShouldApplySpineColumnLayout(Contract, Before.Regions)) return false;
        var Roots = FindRootNodeIds(Contract, Before.Regions.Keys);
        if (Roots.Count == 0) return false;
        string Root = Roots.OrderBy(Id => GetEarliestCreationIndex(Contract, Id)).First();
        var Spine = BuildPrimarySpineNodeIds(Contract, Root);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        int SpineColumn = Targets[Root].StartColumn;
        int Pitch = Targets.Values.Max(Region => Region.ColumnSpan);
        var Locked = Nodes.Where(Node => Node.PlacementHintLocked).Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);

        foreach (string Id in Spine) Set(Id, null, SpineColumn);
        var Sides = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && Targets.ContainsKey(Edge.SourceId) && Targets.ContainsKey(Edge.TargetId) && !Spine.Contains(Edge.TargetId)
            && IsDecisionShape(GetNodeShape(Contract, Edge.SourceId))).Select(Edge => Edge.TargetId).ToHashSet(StringComparer.Ordinal);
        foreach (string Id in Sides)
        {
            // This helper uses only labels and graph topology. Its numeric
            // inputs here are logical lane indices, never drawing positions.
            int Column = checked((int)ResolveSideBranchCrossAxis(Contract, Id, Sides, SpineColumn, Pitch, FindDecisionEdgeToTarget(Contract, Id)));
            if (Contract.Edges.Any(Edge => Edge.SourceId == Id && Spine.Contains(Edge.TargetId)
                && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                && IsNoLabeledEdge(FindDecisionEdgeToTarget(Contract, Id))) Column = checked(SpineColumn - 2 * Pitch);
            Set(Id, null, Column);
        }
        foreach (var Node in Contract.Nodes.Where(Node => IsTerminalCollectorTarget(Contract, Node.Id, Targets)))
        {
            var Sources = Contract.Edges.Where(Edge => Edge.TargetId == Node.Id && Targets.ContainsKey(Edge.SourceId)
                && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).Select(Edge => Edge.SourceId).ToArray();
            int Row = Sources.Where(Id => Targets[Id].StartColumn >= SpineColumn - Pitch
                    && !IsDecisionShape(GetNodeShape(Contract, Id)) && Targets[Id].StartRow > Targets[Node.Id].StartRow)
                .Select(Id => Targets[Id].StartRow).DefaultIfEmpty(Targets[Node.Id].EndRow).Min();
            foreach (string Id in Sources)
            {
                if (Targets[Id].StartColumn < SpineColumn - Pitch) Set(Id, Row, checked(SpineColumn - 2 * Pitch));
                else if (IsSpineColumnCollectorSource(Contract, Id)) Set(Id, null, SpineColumn);
            }
        }
        return FlowchartCellRepair.TryAssign(Nodes, Targets);

        // Each proposal changes only the private complete map. No earlier
        // member move leaks if a later reservation collides or fails a lock.
        void Set(string Id, int? Row, int Column)
        {
            if (Locked.Contains(Id) || !Targets.TryGetValue(Id, out var Region)) return;
            Targets[Id] = new(Row ?? Region.StartRow, Column, Region.RowSpan, Region.ColumnSpan);
        }
    }
}
