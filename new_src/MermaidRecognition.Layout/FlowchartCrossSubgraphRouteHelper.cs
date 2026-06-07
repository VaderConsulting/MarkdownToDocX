using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves cross-subgraph guide axes that avoid unrelated cluster bodies.
/// </summary>
public static class FlowchartCrossSubgraphRouteHelper
{
    private const double CROSS_BOUNDARY_STUB_PX = 14.0;

    private const double CLUSTER_AVOIDANCE_MARGIN_PX = 16.0;

    /// <summary>
    /// Resolves an LR-flow cross-group guide X (Naiad outward-stub parity with cluster avoidance).
    /// </summary>
    public static double ResolveCrossGroupGuideCenterX(
        DiagramContract Contract,
        string SourceId,
        string TargetId,
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartDirection Direction,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        double FlowMinX = Math.Min(StartX, EndX);
        double GuideX = RightToLeft ? FlowMinX + CROSS_BOUNDARY_STUB_PX : FlowMinX - CROSS_BOUNDARY_STUB_PX;

        return AdjustGuideXToAvoidUnrelatedClusters(
            Contract,
            SourceId,
            TargetId,
            StartX,
            EndX,
            StartY,
            EndY,
            GuideX,
            BoundsById);
    }

    private static double AdjustGuideXToAvoidUnrelatedClusters(
        DiagramContract Contract,
        string SourceId,
        string TargetId,
        double StartX,
        double EndX,
        double StartY,
        double EndY,
        double GuideX,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        Dictionary<string, GroupContract> GroupById =
            FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        double MinY = double.PositiveInfinity;
        double MaxY = double.NegativeInfinity;
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            double NodeBottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double NodeTop = Bounds.CenterY + (Bounds.Height / 2.0);
            if (NodeBottom < MinY)
            {
                MinY = NodeBottom;
            }

            if (NodeTop > MaxY)
            {
                MaxY = NodeTop;
            }
        }

        if (double.IsPositiveInfinity(MinY))
        {
            MinY = Math.Min(StartY, EndY);
            MaxY = Math.Max(StartY, EndY);
        }

        double FlowMinX = Math.Min(StartX, EndX);
        double FlowMaxX = Math.Max(StartX, EndX);
        double AdjustedGuideX = GuideX;
        bool Adjusted = false;

        foreach (GroupContract Group in Contract.Groups)
        {
            if (FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(SourceId, Group, Contract, GroupById)
                || FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(TargetId, Group, Contract, GroupById))
            {
                continue;
            }

            if (!RectByGroupId.TryGetValue(Group.Id, out FlowchartSubgraphLayoutRect? Rect))
            {
                continue;
            }

            if (Rect.Right <= FlowMinX || Rect.Left >= FlowMaxX)
            {
                continue;
            }

            if (!VerticalSegmentIntersectsRect(GuideX, MinY, MaxY, Rect))
            {
                continue;
            }

            double LeftCorridorX = Rect.Left - CLUSTER_AVOIDANCE_MARGIN_PX;
            double RightCorridorX = Rect.Right + CLUSTER_AVOIDANCE_MARGIN_PX;
            if (StartX <= EndX)
            {
                if (LeftCorridorX < AdjustedGuideX || !Adjusted)
                {
                    AdjustedGuideX = LeftCorridorX;
                    Adjusted = true;
                }
            }
            else if (RightCorridorX > AdjustedGuideX || !Adjusted)
            {
                AdjustedGuideX = RightCorridorX;
                Adjusted = true;
            }
        }

        return AdjustedGuideX;
    }

    private static bool VerticalSegmentIntersectsRect(
        double SegmentX,
        double MinY,
        double MaxY,
        FlowchartSubgraphLayoutRect Rect)
    {
        if (SegmentX <= Rect.Left || SegmentX >= Rect.Right)
        {
            return false;
        }

        return MaxY >= Rect.Bottom && MinY <= Rect.Top;
    }
}
