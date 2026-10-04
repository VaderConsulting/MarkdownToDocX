using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates minimum spacing between external nodes and subgraph cluster boxes on cross-boundary links.
/// </summary>
public static class FlowchartSubgraphExternalGapHelper
{
    private const double GAP_TOLERANCE_PX = 0.5;

    /// <summary>
    /// Records external subgraph gap facts on groups and diagram layout.
    /// </summary>
    public static void ApplyExternalSubgraphGapFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool ReverseFlow = Direction == FlowchartDirection.BottomToTop
            || Direction == FlowchartDirection.RightToLeft;

        Dictionary<string, GroupActualResult> GroupById = Diagram.Groups
            .ToDictionary(Group => Group.GroupId, Group => Group, StringComparer.Ordinal);

        foreach (GroupActualResult Group in Diagram.Groups)
        {
            Group.Facts["Layout.ExternalSubgraphGapValid"] = true;
            Group.Facts["HasExternalBoundaryNode"] = false;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSubgraphBoundsRequest BoundsRequest = FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        Dictionary<string, string?> GroupIdByNodeId = BuildGroupIdByNodeId(Contract);
        bool AnyViolation = false;
        bool SkipFlowAxisGapChecks = !HorizontalFlow
            && Diagram.Layout.TryGetValue("HasParallelColumnLayout", out object? HasParallelValue)
            && HasParallelValue is bool HasParallelLayout
            && HasParallelLayout
            && Diagram.Layout.TryGetValue("ParallelColumnRowsAligned", out object? RowsAlignedValue)
            && RowsAlignedValue is bool RowsAligned
            && RowsAligned;

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GroupIdByNodeId.TryGetValue(Edge.SourceId, out string? SourceGroupId);
            GroupIdByNodeId.TryGetValue(Edge.TargetId, out string? TargetGroupId);

            if (!SkipFlowAxisGapChecks
                && SourceGroupId is null
                && TargetGroupId is not null)
            {
                string? BoundaryGroupId = ResolveTopLevelBoundaryGroupId(TargetGroupId, Contract);
                if (BoundaryGroupId is not null
                    && TryMeasureIngressGap(
                        Edge.SourceId,
                        BoundaryGroupId,
                        BoundsById,
                        RectByGroupId,
                        HorizontalFlow,
                        ReverseFlow,
                        out double IngressGap))
                {
                    MarkExternalBoundaryGroup(GroupById, BoundaryGroupId);
                    if (IngressGap + GAP_TOLERANCE_PX < FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX)
                    {
                        AnyViolation = true;
                        if (GroupById.TryGetValue(BoundaryGroupId, out GroupActualResult? GroupResult))
                        {
                            GroupResult.Facts["Layout.ExternalSubgraphGapValid"] = false;
                        }
                    }
                }
            }

            if (!SkipFlowAxisGapChecks
                && SourceGroupId is not null
                && TargetGroupId is null)
            {
                string? BoundaryGroupId = ResolveTopLevelBoundaryGroupId(SourceGroupId, Contract);
                if (BoundaryGroupId is not null
                    && TryMeasureEgressGap(
                        Edge.TargetId,
                        BoundaryGroupId,
                        BoundsById,
                        RectByGroupId,
                        HorizontalFlow,
                        ReverseFlow,
                        out double EgressGap))
                {
                    MarkExternalBoundaryGroup(GroupById, BoundaryGroupId);
                    if (EgressGap + GAP_TOLERANCE_PX < FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX)
                    {
                        AnyViolation = true;
                        if (GroupById.TryGetValue(BoundaryGroupId, out GroupActualResult? GroupResult))
                        {
                            GroupResult.Facts["Layout.ExternalSubgraphGapValid"] = false;
                        }
                    }
                }
            }
        }

        Diagram.Layout["ExternalNodesRespectSubgraphMinimumGap"] = !AnyViolation;
    }

    private static void MarkExternalBoundaryGroup(
        Dictionary<string, GroupActualResult> GroupById,
        string GroupId)
    {
        if (GroupById.TryGetValue(GroupId, out GroupActualResult? GroupResult))
        {
            GroupResult.Facts["HasExternalBoundaryNode"] = true;
        }
    }

    private static bool TryMeasureIngressGap(
        string ExternalNodeId,
        string GroupId,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId,
        bool HorizontalFlow,
        bool ReverseFlow,
        out double Gap)
    {
        Gap = double.NaN;
        if (!BoundsById.TryGetValue(ExternalNodeId, out FlowchartLayoutNodeBounds? ExternalBounds))
        {
            return false;
        }

        if (!RectByGroupId.TryGetValue(GroupId, out FlowchartSubgraphLayoutRect? Rect))
        {
            return false;
        }

        (double LeadingEdge, double TrailingEdge) = GetGroupFlowSpan(Rect, HorizontalFlow);
        if (HorizontalFlow)
        {
            if (!ReverseFlow)
            {
                double ExternalTrailing = ExternalBounds.CenterX + (ExternalBounds.Width / 2.0);
                Gap = LeadingEdge - ExternalTrailing;
            }
            else
            {
                double ExternalLeading = ExternalBounds.CenterX - (ExternalBounds.Width / 2.0);
                Gap = ExternalLeading - TrailingEdge;
            }
        }
        else
        {
            if (!ReverseFlow)
            {
                double ExternalBottom = ExternalBounds.CenterY - (ExternalBounds.Height / 2.0);
                Gap = ExternalBottom - TrailingEdge;
            }
            else
            {
                double ExternalTop = ExternalBounds.CenterY + (ExternalBounds.Height / 2.0);
                Gap = LeadingEdge - ExternalTop;
            }
        }

        return true;
    }

    private static bool TryMeasureEgressGap(
        string ExternalNodeId,
        string GroupId,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId,
        bool HorizontalFlow,
        bool ReverseFlow,
        out double Gap)
    {
        Gap = double.NaN;
        if (!BoundsById.TryGetValue(ExternalNodeId, out FlowchartLayoutNodeBounds? ExternalBounds))
        {
            return false;
        }

        if (!RectByGroupId.TryGetValue(GroupId, out FlowchartSubgraphLayoutRect? Rect))
        {
            return false;
        }

        (double LeadingEdge, double TrailingEdge) = GetGroupFlowSpan(Rect, HorizontalFlow);
        if (HorizontalFlow)
        {
            if (!ReverseFlow)
            {
                double ExternalLeading = ExternalBounds.CenterX - (ExternalBounds.Width / 2.0);
                Gap = ExternalLeading - TrailingEdge;
            }
            else
            {
                double ExternalTrailing = ExternalBounds.CenterX + (ExternalBounds.Width / 2.0);
                Gap = LeadingEdge - ExternalTrailing;
            }
        }
        else
        {
            if (!ReverseFlow)
            {
                double ExternalTop = ExternalBounds.CenterY + (ExternalBounds.Height / 2.0);
                Gap = LeadingEdge - ExternalTop;
            }
            else
            {
                double ExternalBottom = ExternalBounds.CenterY - (ExternalBounds.Height / 2.0);
                Gap = ExternalBottom - TrailingEdge;
            }
        }

        return true;
    }

    private static (double LeadingEdge, double TrailingEdge) GetGroupFlowSpan(
        FlowchartSubgraphLayoutRect Rect,
        bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return (Rect.Left, Rect.Right);
        }

        return (Rect.Bottom, Rect.Top);
    }

    private static string? ResolveTopLevelBoundaryGroupId(
        string MemberGroupId,
        DiagramContract Contract)
    {
        Dictionary<string, GroupContract> GroupById = Contract.Groups
            .ToDictionary(Group => Group.Id, Group => Group, StringComparer.Ordinal);
        string? CurrentGroupId = MemberGroupId;
        while (CurrentGroupId is not null && GroupById.TryGetValue(CurrentGroupId, out GroupContract? Group))
        {
            if (Group.ParentGroupId is null)
            {
                return CurrentGroupId;
            }

            CurrentGroupId = Group.ParentGroupId;
        }

        return MemberGroupId;
    }

    private static Dictionary<string, string?> BuildGroupIdByNodeId(DiagramContract Contract)
    {
        Dictionary<string, string?> GroupIdByNodeId =
            new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (NodeContract Node in Contract.Nodes)
        {
            GroupIdByNodeId[Node.Id] = Node.ParentGroupId;
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            foreach (string ChildNodeId in Group.ChildNodeIds)
            {
                GroupIdByNodeId[ChildNodeId] = Group.Id;
            }
        }

        return GroupIdByNodeId;
    }
}
