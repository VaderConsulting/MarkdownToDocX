using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Corner-to-corner straight routes for grouped-vertex 2×2 complete bipartite expansion (F61).
/// </summary>
public static class FlowchartGroupedVertexEdgePathBuilder
{
    private const double COLUMN_ALIGNMENT_TOLERANCE_PX = 1.5;

    private const double EDGE_NODE_GAP_PX = 2.0;

    /// <summary>
    /// Builds a two-point straight layout route when the edge belongs to a TB grouped 2×2 expansion.
    /// </summary>
    public static bool TryBuildGroupedTwoByTwoPath(
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];

        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        if (!TryGetGroupedTwoByTwoNodeIds(Contract, out HashSet<string> NodeIds))
        {
            return false;
        }

        if (!NodeIds.Contains(Edge.SourceId) || !NodeIds.Contains(Edge.TargetId))
        {
            return false;
        }

        double SourceHalfWidth = SourceBounds.Width / 2.0;
        double SourceHalfHeight = SourceBounds.Height / 2.0;
        double TargetHalfWidth = TargetBounds.Width / 2.0;
        double TargetHalfHeight = TargetBounds.Height / 2.0;

        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart);
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd);

        bool SameColumn = Math.Abs(SourceBounds.CenterX - TargetBounds.CenterX) <= COLUMN_ALIGNMENT_TOLERANCE_PX;
        if (SameColumn)
        {
            double SourceAttachX = SourceBounds.CenterX;
            double SourceAttachY = SourceBounds.CenterY - SourceHalfHeight - SourceGapPx;
            double TargetAttachX = TargetBounds.CenterX;
            double TargetAttachY = TargetBounds.CenterY + TargetHalfHeight + TargetGapPx;
            LayoutPoints.Add((SourceAttachX, SourceAttachY));
            LayoutPoints.Add((TargetAttachX, TargetAttachY));
            return true;
        }

        bool SourceLeftOfTarget = SourceBounds.CenterX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX;
        bool SourceRightOfTarget = SourceBounds.CenterX > TargetBounds.CenterX + COLLINEAR_EPSILON_PX;
        if (!SourceLeftOfTarget && !SourceRightOfTarget)
        {
            return false;
        }

        if (SourceLeftOfTarget)
        {
            double SourceAttachX = SourceBounds.CenterX + SourceHalfWidth + SourceGapPx;
            double SourceAttachY = SourceBounds.CenterY - SourceHalfHeight - SourceGapPx;
            double TargetAttachX = TargetBounds.CenterX - TargetHalfWidth - TargetGapPx;
            double TargetAttachY = TargetBounds.CenterY + TargetHalfHeight + TargetGapPx;
            LayoutPoints.Add((SourceAttachX, SourceAttachY));
            LayoutPoints.Add((TargetAttachX, TargetAttachY));
            return true;
        }

        double SourceSouthWestX = SourceBounds.CenterX - SourceHalfWidth - SourceGapPx;
        double SourceSouthWestY = SourceBounds.CenterY - SourceHalfHeight - SourceGapPx;
        double TargetNorthEastX = TargetBounds.CenterX + TargetHalfWidth + TargetGapPx;
        double TargetNorthEastY = TargetBounds.CenterY + TargetHalfHeight + TargetGapPx;
        LayoutPoints.Add((SourceSouthWestX, SourceSouthWestY));
        LayoutPoints.Add((TargetNorthEastX, TargetNorthEastY));
        return true;
    }

    private const double COLLINEAR_EPSILON_PX = 0.75;

    private static bool TryGetGroupedTwoByTwoNodeIds(DiagramContract Contract, out HashSet<string> NodeIds)
    {
        NodeIds = new HashSet<string>(StringComparer.Ordinal);

        if (Contract.Edges.Count != 4)
        {
            return false;
        }

        HashSet<string> Sources = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> Targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Sources.Add(Edge.SourceId);
            Targets.Add(Edge.TargetId);
        }

        if (Sources.Count != 2 || Targets.Count != 2)
        {
            return false;
        }

        List<string> SourceList = [.. Sources.OrderBy(Id => Id, StringComparer.Ordinal)];
        List<string> TargetList = [.. Targets.OrderBy(Id => Id, StringComparer.Ordinal)];

        string SourceA = SourceList[0];
        string SourceB = SourceList[1];
        string TargetC = TargetList[0];
        string TargetD = TargetList[1];

        bool HasCompleteBipartite =
            HasEdge(Contract, SourceA, TargetC)
            && HasEdge(Contract, SourceA, TargetD)
            && HasEdge(Contract, SourceB, TargetC)
            && HasEdge(Contract, SourceB, TargetD);

        if (!HasCompleteBipartite)
        {
            return false;
        }

        NodeIds.Add(SourceA);
        NodeIds.Add(SourceB);
        NodeIds.Add(TargetC);
        NodeIds.Add(TargetD);
        return true;
    }

    private static bool HasEdge(DiagramContract Contract, string SourceId, string TargetId)
    {
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
