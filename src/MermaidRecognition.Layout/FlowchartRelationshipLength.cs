using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Opens extra space along a relationship when its Mermaid dash count asks for a longer rank span.
/// The cell grid otherwise packs each pair onto the next row and the extra dashes disappear.
/// </summary>
internal static class FlowchartRelationshipLength
{
    /// <summary>
    /// Grows the gap after the upstream node by (minimum length - 1) rank spacings.
    /// </summary>
    /// <param name="Contract">Edges whose <see cref="EdgeContract.MinLength"/> is already set.</param>
    /// <param name="Nodes">Placed nodes that share one cell grid.</param>
    /// <param name="Horizontal">True when ranks run left to right.</param>
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Contract.Edges.Count == 0 || Nodes.Count == 0 || Nodes.Exists(Node => Node.CellNode == null))
        {
            return;
        }

        double RankSpacing = FlowchartRenderOptions.FromContract(Contract).RankSpacingPx;
        Dictionary<string, DiagramGridLayout.GridNode> ById = new Dictionary<string, DiagramGridLayout.GridNode>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            ById[Node.Id] = Node;
        }

        Dictionary<int, double> ExtraByGap = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int MinLength = Math.Max(1, Edge.MinLength);
            if (MinLength <= 1)
            {
                continue;
            }

            if (!ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? SourceNode)
                || !ById.TryGetValue(Edge.TargetId, out DiagramGridLayout.GridNode? TargetNode))
            {
                continue;
            }

            FlowchartCellRegion SourceRegion = SourceNode.CellNode!.Region;
            FlowchartCellRegion TargetRegion = TargetNode.CellNode!.Region;
            int SourceStart = Horizontal ? SourceRegion.StartColumn : SourceRegion.StartRow;
            int TargetStart = Horizontal ? TargetRegion.StartColumn : TargetRegion.StartRow;
            if (SourceStart == TargetStart)
            {
                continue;
            }

            DiagramGridLayout.GridNode Upstream = SourceStart < TargetStart ? SourceNode : TargetNode;
            FlowchartCellRegion UpstreamRegion = Upstream.CellNode!.Region;
            int After = (Horizontal ? UpstreamRegion.EndColumn : UpstreamRegion.EndRow) - 1;
            double Extra = (MinLength - 1) * RankSpacing;
            if (!ExtraByGap.TryGetValue(After, out double Existing) || Extra > Existing)
            {
                ExtraByGap[After] = Extra;
            }
        }

        if (ExtraByGap.Count == 0)
        {
            return;
        }

        List<FlowchartCellGapGrowth> Growth = [];
        foreach (KeyValuePair<int, double> Pair in ExtraByGap)
        {
            Growth.Add(new FlowchartCellGapGrowth(Horizontal, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX));
        }

        FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(), Growth)?.TryApply();
    }
}
