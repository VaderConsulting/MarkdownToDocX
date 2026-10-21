using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Shared geometry-first port side selection for flowcharts and enterprise diagrams.</summary>
internal static class FlowchartFacingPortSelector
{
    /// <summary>When two boxes share a column with no other node between them, use centre bottom to centre top (or the reverse).</summary>
    internal static bool TryStackedVerticalSides(
        FlowchartLayoutNodeBounds from,
        FlowchartLayoutNodeBounds to,
        out string fromSide,
        out string toSide,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>? columnObstacles = null,
        string? fromNodeId = null)
    {
        fromSide = string.Empty;
        toSide = string.Empty;
        if (Math.Abs(from.CenterX - to.CenterX) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            return false;
        }

        if (from.CenterY > to.CenterY + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (!IsImmediateVerticalNeighbour(from, to, columnObstacles, fromNodeId, downwardOnPage: true))
            {
                return false;
            }

            fromSide = "bottom";
            toSide = "top";
            return true;
        }

        if (from.CenterY < to.CenterY - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (!IsImmediateVerticalNeighbour(from, to, columnObstacles, fromNodeId, downwardOnPage: false))
            {
                return false;
            }

            fromSide = "top";
            toSide = "bottom";
            return true;
        }

        return false;
    }

    private static bool IsImmediateVerticalNeighbour(
        FlowchartLayoutNodeBounds from,
        FlowchartLayoutNodeBounds to,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>? columnObstacles,
        string? fromNodeId,
        bool downwardOnPage)
    {
        if (columnObstacles == null || fromNodeId == null)
        {
            return true;
        }

        IEnumerable<FlowchartLayoutNodeBounds> candidates = columnObstacles.Values.Where(box =>
            box.NodeId != fromNodeId
            && Math.Abs(box.CenterX - from.CenterX) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX);
        if (downwardOnPage)
        {
            double limit = to.CenterY;
            return !candidates.Any(box => box.CenterY < from.CenterY - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                && box.CenterY > limit + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX);
        }

        double upperLimit = to.CenterY;
        return !candidates.Any(box => box.CenterY > from.CenterY + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
            && box.CenterY < upperLimit - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX);
    }

    /// <summary>Page-facing side from one centre toward another, with stacked-column and diagonal-below overrides.</summary>
    internal static string ResolveFacingSide(
        FlowchartLayoutNodeBounds from,
        FlowchartLayoutNodeBounds to,
        DiagramContract? contract = null,
        string? fromNodeId = null,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>? boundsById = null)
    {
        if (TryStackedVerticalSides(from, to, out string stackedFrom, out _, boundsById, fromNodeId))
        {
            return stackedFrom;
        }

        double dx = to.CenterX - from.CenterX;
        double dy = to.CenterY - from.CenterY;
        if (dy < -DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
            && Math.Abs(dx) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (contract != null && fromNodeId != null && boundsById != null
                && SourceBottomBusyWithAlignedChild(contract, fromNodeId, from, boundsById))
            {
                return dx > 0 ? "right" : "left";
            }

            return "bottom";
        }

        if (dy > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
            && Math.Abs(dx) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            return "top";
        }

        return Math.Abs(dx) >= Math.Abs(dy) ? dx >= 0 ? "right" : "left" : dy >= 0 ? "top" : "bottom";
    }

    /// <summary>Entry side on the target when the source is already chosen.</summary>
    internal static string ResolveEntrySide(
        FlowchartLayoutNodeBounds target,
        FlowchartLayoutNodeBounds source,
        string sourceExitSide)
    {
        if (TryStackedVerticalSides(source, target, out _, out string stackedEntry, null, null))
        {
            return stackedEntry;
        }

        if (sourceExitSide is "bottom" or "top")
        {
            double dx = source.CenterX - target.CenterX;
            double dy = source.CenterY - target.CenterY;
            if (Math.Abs(dx) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                && Math.Abs(dy) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return ResolveFacingSide(target, source);
            }

            if (Math.Abs(dx) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return dx > 0 ? "right" : "left";
            }
        }

        return ResolveFacingSide(target, source);
    }

    private static bool SourceBottomBusyWithAlignedChild(
        DiagramContract contract,
        string sourceId,
        FlowchartLayoutNodeBounds source,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> boundsById)
    {
        double bottomBand = source.Width / 2.0 + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
        foreach (EdgeContract edge in contract.Edges)
        {
            if (!string.Equals(edge.SourceId, sourceId, StringComparison.Ordinal)
                || string.Equals(edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!boundsById.TryGetValue(edge.TargetId, out FlowchartLayoutNodeBounds? target))
            {
                continue;
            }

            if (target.CenterY >= source.CenterY - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                continue;
            }

            if (Math.Abs(target.CenterX - source.CenterX) <= bottomBand)
            {
                return true;
            }
        }

        return false;
    }
}
