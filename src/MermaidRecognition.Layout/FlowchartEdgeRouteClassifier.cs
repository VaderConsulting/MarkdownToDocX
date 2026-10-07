using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Assigns semantic route classes from spine topology and layout geometry before path generation.
/// </summary>
public static class FlowchartEdgeRouteClassifier
{
    private const int TERMINAL_COLLECTOR_INCOMING_THRESHOLD = 2;

    /// <summary>
    /// Classifies one edge using spine membership, column offset, and corridor intersection.
    /// </summary>
    public static FlowchartEdgeSemanticRouteClass Classify(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext? SpineContext)
    {
        if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            return FlowchartEdgeSemanticRouteClass.Unknown;
        }

        if (SpineContext is null)
        {
            return FlowchartEdgeSemanticRouteClass.Unknown;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return FlowchartEdgeSemanticRouteClass.Unknown;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (HorizontalFlow)
        {
            return FlowchartEdgeSemanticRouteClass.Unknown;
        }

        NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode);
        string? SourceShape = SourceNode?.Shape;

        bool SourceOnSpine = FlowchartSpineGraphHelper.IsOnSpineColumn(SourceBounds, SpineContext);
        bool TargetOnSpine = FlowchartSpineGraphHelper.IsOnSpineColumn(TargetBounds, SpineContext);
        bool SourceAboveTarget = FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds);
        bool IsDecisionFanOut = FlowchartSpineGraphHelper.IsDecisionFanOutSource(Contract, Edge.SourceId, SourceShape);
        int TargetIncomingCount = CountVisibleIncoming(Contract, Edge.TargetId);
        bool SkipsInterveningSpineColumnNode = HasInterveningSpineColumnNode(
            BoundsById,
            SpineContext,
            SourceBounds,
            TargetBounds);

        if (SourceOnSpine
            && TargetOnSpine
            && IsDecisionFanOut
            && SourceAboveTarget
            && SkipsInterveningSpineColumnNode)
        {
            return FlowchartEdgeSemanticRouteClass.SkipBypass;
        }

        if (IsDecisionFanOut
            && SourceOnSpine
            && SourceAboveTarget)
        {
            if (TargetOnSpine
                && FlowchartMultiIncomingTargetPortAllocator.TargetHasBranchAndMergeIncoming(
                    Contract,
                    BoundsById,
                    SpineContext,
                    Edge.TargetId))
            {
                return FlowchartEdgeSemanticRouteClass.LocalDecisionBranch;
            }

            if (TargetOnSpine)
            {
                return FlowchartEdgeSemanticRouteClass.CentreSpine;
            }

            if (FlowchartSpineGraphHelper.IsFarMarginColumn(TargetBounds, SpineContext))
            {
                return FlowchartEdgeSemanticRouteClass.LongExceptionRoute;
            }

            if (FlowchartSpineGraphHelper.IsInnerMarginColumn(TargetBounds, SpineContext))
            {
                return FlowchartEdgeSemanticRouteClass.LocalDecisionBranch;
            }

            if (FlowchartSpineCorridorHelper.TryBuildCorridorExcluding(
                    BoundsById,
                    SpineContext,
                    Edge.SourceId,
                    out FlowchartSpineCorridorBounds Corridor)
                && FlowchartRouteObstacleHelper.CentreChordIntersectsSpineCorridor(
                    SourceBounds,
                    TargetBounds,
                    Corridor))
            {
                return FlowchartEdgeSemanticRouteClass.LongExceptionRoute;
            }

            return FlowchartEdgeSemanticRouteClass.LocalDecisionBranch;
        }

        if (SourceOnSpine
            && TargetOnSpine
            && SourceAboveTarget)
        {
            return FlowchartEdgeSemanticRouteClass.CentreSpine;
        }

        if (IsDecisionFanOut)
        {
            return FlowchartEdgeSemanticRouteClass.Unknown;
        }

        if (!SourceOnSpine
            && TargetOnSpine
            && TargetIncomingCount >= TERMINAL_COLLECTOR_INCOMING_THRESHOLD
            && string.Equals(Edge.TargetId, SpineContext.PrimarySinkId, StringComparison.Ordinal))
        {
            return FlowchartEdgeSemanticRouteClass.MergeBack;
        }

        if (!SourceOnSpine
            && TargetOnSpine
            && SourceAboveTarget
            && Math.Abs(SourceBounds.CenterX - SpineContext.SpineCrossAxis) >= SpineContext.ColumnStep * 0.4)
        {
            return FlowchartEdgeSemanticRouteClass.MergeBack;
        }

        return FlowchartEdgeSemanticRouteClass.Unknown;
    }

    /// <summary>
    /// Builds spine context and classifies one edge in a single call for diagnostics and tests.
    /// </summary>
    public static FlowchartEdgeSemanticRouteClass ClassifyWithContext(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options,
        out FlowchartSpineLayoutContext? SpineContext)
    {
        FlowchartSpineGraphHelper.TryBuildContext(Contract, BoundsById, Direction, Options, out SpineContext);
        return Classify(Edge, Contract, BoundsById, NodeById, Direction, SpineContext);
    }

    private static int CountVisibleIncoming(DiagramContract Contract, string NodeId)
    {
        int Count = 0;
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Candidate.TargetId, NodeId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
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
