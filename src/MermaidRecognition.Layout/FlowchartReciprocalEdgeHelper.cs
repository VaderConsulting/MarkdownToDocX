using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Layout and routing helpers for two-node reciprocal edge pairs (F104 feedback loops).
/// </summary>
public static class FlowchartReciprocalEdgeHelper
{
    public const double RECIPROCAL_EDGE_LANE_OFFSET_PX = 12.0;

    /// <summary>
    /// Returns true when the contract is a visible two-node reciprocal pair in a vertical flow direction.
    /// </summary>
    public static bool TryGetTbTwoNodeReciprocalPair(
        DiagramContract Contract,
        out EdgeContract FirstEdge,
        out EdgeContract SecondEdge)
    {
        FirstEdge = null!;
        SecondEdge = null!;

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        List<EdgeContract> VisibleEdges = [.. Contract.Edges
            .Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Edge => Edge.CreationIndex)];

        if (VisibleEdges.Count != 2 || Contract.Nodes.Count != 2)
        {
            return false;
        }

        EdgeContract Edge0 = VisibleEdges[0];
        EdgeContract Edge1 = VisibleEdges[1];
        if (!string.Equals(Edge0.SourceId, Edge1.TargetId, StringComparison.Ordinal)
            || !string.Equals(Edge0.TargetId, Edge1.SourceId, StringComparison.Ordinal))
        {
            return false;
        }

        FirstEdge = Edge0;
        SecondEdge = Edge1;
        return true;
    }

    /// <summary>
    /// Ensures the first declared edge runs upward on screen (target above source) for TB feedback loops.
    /// </summary>
    public static List<FlowchartLayoutNodeBounds> NormalizeTbFeedbackLoopNodeOrder(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction)
    {
        if (!TryGetTbTwoNodeReciprocalPair(Contract, out EdgeContract FirstEdge, out EdgeContract _))
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        if (!MutableById.TryGetValue(FirstEdge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !MutableById.TryGetValue(FirstEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return [.. MutableById.Values];
        }

        bool TargetShouldBeAboveSource = Direction == FlowchartDirection.TopToBottom;
        bool TargetIsAboveSource = TargetBounds.CenterY > SourceBounds.CenterY;
        if (TargetShouldBeAboveSource == TargetIsAboveSource)
        {
            return [.. MutableById.Values];
        }

        double SourceCenterY = SourceBounds.CenterY;
        MutableById[SourceBounds.NodeId] = new FlowchartLayoutNodeBounds
        {
            NodeId = SourceBounds.NodeId,
            CenterX = SourceBounds.CenterX,
            CenterY = TargetBounds.CenterY,
            Width = SourceBounds.Width,
            Height = SourceBounds.Height
        };
        MutableById[TargetBounds.NodeId] = new FlowchartLayoutNodeBounds
        {
            NodeId = TargetBounds.NodeId,
            CenterX = TargetBounds.CenterX,
            CenterY = SourceCenterY,
            Width = TargetBounds.Width,
            Height = TargetBounds.Height
        };

        return [.. MutableById.Values];
    }

    /// <summary>
    /// Applies a horizontal lane offset to a reciprocal edge guide segment.
    /// </summary>
    public static void ApplyReciprocalLaneOffset(
        EdgeContract Edge,
        DiagramContract Contract,
        ref double StartX,
        ref double StartY,
        ref double EndX,
        ref double EndY)
    {
        if (!TryGetTbTwoNodeReciprocalPair(Contract, out EdgeContract FirstEdge, out EdgeContract _))
        {
            return;
        }

        double LaneOffsetPx = Edge.CreationIndex == FirstEdge.CreationIndex
            ? RECIPROCAL_EDGE_LANE_OFFSET_PX
            : -RECIPROCAL_EDGE_LANE_OFFSET_PX;
        StartX += LaneOffsetPx;
        EndX += LaneOffsetPx;
    }

    /// <summary>
    /// Applies a horizontal lane offset to every point in a reciprocal edge guide polyline.
    /// </summary>
    public static void ApplyReciprocalLaneOffset(
        EdgeContract Edge,
        DiagramContract Contract,
        IList<(double X, double Y)> Points)
    {
        if (!TryGetTbTwoNodeReciprocalPair(Contract, out EdgeContract FirstEdge, out EdgeContract _))
        {
            return;
        }

        double LaneOffsetPx = Edge.CreationIndex == FirstEdge.CreationIndex
            ? RECIPROCAL_EDGE_LANE_OFFSET_PX
            : -RECIPROCAL_EDGE_LANE_OFFSET_PX;
        for (int PointIndex = 0; PointIndex < Points.Count; PointIndex++)
        {
            (double PointX, double PointY) = Points[PointIndex];
            Points[PointIndex] = (PointX + LaneOffsetPx, PointY);
        }
    }
}
