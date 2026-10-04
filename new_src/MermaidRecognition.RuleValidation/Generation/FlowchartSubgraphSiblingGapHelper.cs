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
/// Validates minimum spacing between sibling subgraph cluster boxes.
/// </summary>
public static class FlowchartSubgraphSiblingGapHelper
{
    private const double GAP_TOLERANCE_PX = 0.5;

    /// <summary>
    /// Records sibling subgraph gap facts on groups and diagram layout.
    /// </summary>
    public static void ApplySiblingSubgraphGapFacts(
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
            Group.Facts["Layout.SiblingSubgraphGapValid"] = true;
            Group.Facts["HasSiblingSubgraph"] = false;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSubgraphBoundsRequest BoundsRequest = FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        Dictionary<string, List<GroupContract>> SiblingsByParentId =
            new Dictionary<string, List<GroupContract>>(StringComparer.Ordinal);

        foreach (GroupContract Group in Contract.Groups)
        {
            string ParentKey = Group.ParentGroupId ?? string.Empty;
            if (!SiblingsByParentId.TryGetValue(ParentKey, out List<GroupContract>? Siblings))
            {
                Siblings = [];
                SiblingsByParentId[ParentKey] = Siblings;
            }

            Siblings.Add(Group);
        }

        bool AnyViolation = false;
        foreach (KeyValuePair<string, List<GroupContract>> Entry in SiblingsByParentId)
        {
            if (Entry.Value.Count < 2)
            {
                continue;
            }

            foreach (GroupContract Group in Entry.Value)
            {
                if (GroupById.TryGetValue(Group.Id, out GroupActualResult? GroupResult))
                {
                    GroupResult.Facts["HasSiblingSubgraph"] = true;
                }
            }

            List<GroupContract> OrderedGroups = Entry.Value
                .Where(Group => RectByGroupId.ContainsKey(Group.Id))
                .OrderBy(Group => GetGroupFlowLeadingEdge(RectByGroupId[Group.Id], HorizontalFlow))
                .ToList();

            if (!HorizontalFlow
                && Diagram.Layout.TryGetValue("HasParallelColumnLayout", out object? HasParallelValue)
                && HasParallelValue is bool HasParallelLayout
                && HasParallelLayout
                && Diagram.Layout.TryGetValue("ParallelColumnRowsAligned", out object? RowsAlignedValue)
                && RowsAlignedValue is bool RowsAligned
                && RowsAligned)
            {
                continue;
            }

            double PreviousTrailingEdge = double.NaN;
            foreach (GroupContract Group in OrderedGroups)
            {
                FlowchartSubgraphLayoutRect Rect = RectByGroupId[Group.Id];
                (double LeadingEdge, double TrailingEdge) = GetGroupFlowSpan(Rect, HorizontalFlow);

                if (!double.IsNaN(PreviousTrailingEdge))
                {
                    double FlowGap = ReverseFlow
                        ? PreviousTrailingEdge - LeadingEdge
                        : LeadingEdge - PreviousTrailingEdge;

                    if (FlowGap + GAP_TOLERANCE_PX < FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX)
                    {
                        AnyViolation = true;
                        if (GroupById.TryGetValue(Group.Id, out GroupActualResult? GroupResult))
                        {
                            GroupResult.Facts["Layout.SiblingSubgraphGapValid"] = false;
                        }
                    }
                }

                PreviousTrailingEdge = TrailingEdge;
            }
        }

        Diagram.Layout["SiblingSubgraphsRespectMinimumGap"] = !AnyViolation;
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

    private static double GetGroupFlowLeadingEdge(
        FlowchartSubgraphLayoutRect Rect,
        bool HorizontalFlow)
    {
        (double LeadingEdge, _) = GetGroupFlowSpan(Rect, HorizontalFlow);
        return LeadingEdge;
    }

}
