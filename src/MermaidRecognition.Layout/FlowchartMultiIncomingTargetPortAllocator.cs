using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Spreads inbound attach points along the top edge of merge targets with multiple incoming edges.
/// </summary>
public static class FlowchartMultiIncomingTargetPortAllocator
{
    private const double EDGE_NODE_GAP_PX = 2.0;

    private const double TOP_EDGE_SPAN_RATIO = 0.72;

    private enum FlowchartIncomingTargetPortPriority
    {
        FarMarginMergeBack = 0,

        CentreSpine = 1,

        SkipBypass = 2,

        LocalBranch = 3
    }

    /// <summary>
    /// Resolves a spread top inbound attach for one edge among several incoming paths.
    /// </summary>
    public static bool TryResolveSpreadTopInboundAttach(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext? SpineContext,
        double ApproachSourceX,
        double ApproachSourceY,
        out double AttachX,
        out double AttachY)
    {
        AttachX = 0.0;
        AttachY = 0.0;

        List<(string SourceId, double SortKey)> IncomingSources = CollectIncomingSources(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            SpineContext,
            Edge.TargetId);
        if (IncomingSources.Count <= 1)
        {
            return FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                TargetBounds,
                TargetShape,
                ApproachSourceX,
                ApproachSourceY,
                Edge.MarkerEnd,
                out AttachX,
                out AttachY);
        }

        int BranchIndex = 0;
        for (int SourceIndex = 0; SourceIndex < IncomingSources.Count; SourceIndex++)
        {
            if (string.Equals(IncomingSources[SourceIndex].SourceId, Edge.SourceId, StringComparison.Ordinal))
            {
                BranchIndex = SourceIndex;
                break;
            }
        }

        double HalfWidth = TargetBounds.Width / 2.0;
        double HalfHeight = TargetBounds.Height / 2.0;
        double Span = HalfWidth * TOP_EDGE_SPAN_RATIO;
        double Step = Span / Math.Max(IncomingSources.Count - 1, 1);
        AttachX = (TargetBounds.CenterX - (Span / 2.0)) + (Step * BranchIndex);
        AttachY = TargetBounds.CenterY + HalfHeight + FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd);
        return true;
    }

    /// <summary>
    /// Resolves a spread top inbound attach when spine context is unavailable.
    /// </summary>
    public static bool TryResolveSpreadTopInboundAttach(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double ApproachSourceX,
        double ApproachSourceY,
        out double AttachX,
        out double AttachY)
    {
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartSpineGraphHelper.TryBuildContext(Contract, BoundsById, Direction, Options, out FlowchartSpineLayoutContext? SpineContext);
        return TryResolveSpreadTopInboundAttach(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            TargetBounds,
            TargetShape,
            Direction,
            SpineContext,
            ApproachSourceX,
            ApproachSourceY,
            out AttachX,
            out AttachY);
    }

    /// <summary>
    /// Returns true when the target receives a side merge-back edge in addition to other incoming paths.
    /// </summary>
    public static bool TargetHasBranchAndMergeIncoming(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSpineLayoutContext SpineContext,
        string TargetNodeId)
    {
        bool HasSideMergeBack = false;
        bool HasOtherIncoming = false;

        foreach (EdgeContract IncomingEdge in Contract.Edges)
        {
            if (string.Equals(IncomingEdge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(IncomingEdge.TargetId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(IncomingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            bool SourceOnSpine = FlowchartSpineGraphHelper.IsOnSpineColumn(SourceBounds, SpineContext);
            bool TargetOnSpine = FlowchartSpineGraphHelper.IsOnSpineColumn(TargetBounds, SpineContext);
            if (!SourceOnSpine
                && TargetOnSpine
                && FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds))
            {
                HasSideMergeBack = true;
                continue;
            }

            HasOtherIncoming = true;
        }

        return HasSideMergeBack && HasOtherIncoming;
    }

    private static List<(string SourceId, double SortKey)> CollectIncomingSources(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext? SpineContext,
        string TargetNodeId)
    {
        List<(string SourceId, double SortKey)> IncomingSources = [];
        if (!BoundsById.TryGetValue(TargetNodeId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return IncomingSources;
        }

        foreach (EdgeContract IncomingEdge in Contract.Edges)
        {
            if (string.Equals(IncomingEdge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(IncomingEdge.TargetId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
            {
                continue;
            }

            FlowchartIncomingTargetPortPriority PortPriority = ResolvePortPriority(
                IncomingEdge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                SpineContext,
                SourceBounds,
                TargetBounds);
            double SortKey = SourceBounds.CenterX + ((int)PortPriority * 0.01);
            IncomingSources.Add((IncomingEdge.SourceId, SortKey));
        }

        IncomingSources.Sort((Left, Right) => Left.SortKey.CompareTo(Right.SortKey));
        return IncomingSources;
    }

    private static FlowchartIncomingTargetPortPriority ResolvePortPriority(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext? SpineContext,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        if (SpineContext is null)
        {
            return FlowchartIncomingTargetPortPriority.LocalBranch;
        }

        NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode);
        string? SourceShape = SourceNode?.Shape;
        bool SourceOnSpine = FlowchartSpineGraphHelper.IsOnSpineColumn(SourceBounds, SpineContext);
        bool TargetOnSpine = FlowchartSpineGraphHelper.IsOnSpineColumn(TargetBounds, SpineContext);
        bool SourceAboveTarget = FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds);

        if (!SourceOnSpine
            && TargetOnSpine
            && SourceAboveTarget)
        {
            return FlowchartIncomingTargetPortPriority.FarMarginMergeBack;
        }

        if (SourceOnSpine
            && TargetOnSpine
            && FlowchartSpineGraphHelper.IsDecisionFanOutSource(Contract, Edge.SourceId, SourceShape)
            && SourceAboveTarget
            && HasInterveningSpineColumnNode(BoundsById, SpineContext, SourceBounds, TargetBounds))
        {
            return FlowchartIncomingTargetPortPriority.SkipBypass;
        }

        if (SourceOnSpine && TargetOnSpine && SourceAboveTarget)
        {
            return FlowchartIncomingTargetPortPriority.CentreSpine;
        }

        return FlowchartIncomingTargetPortPriority.LocalBranch;
    }

    private static bool HasInterveningSpineColumnNode(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSpineLayoutContext SpineContext,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceBounds.NodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetBounds.NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!FlowchartSpineGraphHelper.IsOnSpineColumn(CandidateBounds, SpineContext))
            {
                continue;
            }

            if (CandidateBounds.CenterY >= SourceBounds.CenterY - FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                continue;
            }

            if (CandidateBounds.CenterY <= TargetBounds.CenterY + FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
