using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates that nested child subgraph cluster boxes sit inside their parent cluster with minimum exterior gap.
/// </summary>
public static class FlowchartSubgraphNestedContainmentHelper
{
    private const double GAP_TOLERANCE_PX = 0.5;

    /// <summary>
    /// Records nested subgraph containment facts on parent groups and diagram layout.
    /// </summary>
    public static void ApplyNestedSubgraphContainmentFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        Dictionary<string, GroupActualResult> GroupById = Diagram.Groups
            .ToDictionary(Group => Group.GroupId, Group => Group, StringComparer.Ordinal);

        foreach (GroupActualResult Group in Diagram.Groups)
        {
            Group.Facts["Layout.NestedSubgraphContainmentValid"] = true;
            Group.Facts["HasNestedChildSubgraph"] = false;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSubgraphBoundsRequest BoundsRequest = FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        Dictionary<string, GroupContract> ContractGroupById = Contract.Groups
            .ToDictionary(Group => Group.Id, Group => Group, StringComparer.Ordinal);

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        double MinimumCrossAxisGap = FlowchartSubgraphGapThresholds.ResolveNestedCrossAxisGapPx();
        double MinimumFlowAxisGap = FlowchartSubgraphGapThresholds.ResolveNestedFlowAxisGapPx(Options);
        bool AnyViolation = false;

        foreach (GroupContract ParentGroup in Contract.Groups)
        {
            if (ParentGroup.ChildGroupIds.Count == 0)
            {
                continue;
            }

            if (!RectByGroupId.TryGetValue(ParentGroup.Id, out FlowchartSubgraphLayoutRect? ParentRect))
            {
                continue;
            }

            if (!GroupById.TryGetValue(ParentGroup.Id, out GroupActualResult? ParentResult))
            {
                continue;
            }

            ParentResult.Facts["HasNestedChildSubgraph"] = true;

            foreach (string ChildGroupId in ParentGroup.ChildGroupIds)
            {
                if (!RectByGroupId.TryGetValue(ChildGroupId, out FlowchartSubgraphLayoutRect? ChildRect))
                {
                    continue;
                }

                double LeftGap = ChildRect.Left - ParentRect.Left;
                double RightGap = ParentRect.Right - ChildRect.Right;
                double BottomGap = ChildRect.Bottom - ParentRect.Bottom;
                double TopGap = ParentRect.Top - ChildRect.Top;

                if (HorizontalFlow)
                {
                    if (LeftGap + GAP_TOLERANCE_PX < MinimumFlowAxisGap
                        || RightGap + GAP_TOLERANCE_PX < MinimumFlowAxisGap
                        || BottomGap + GAP_TOLERANCE_PX < MinimumCrossAxisGap
                        || TopGap + GAP_TOLERANCE_PX < MinimumCrossAxisGap)
                    {
                        AnyViolation = true;
                        ParentResult.Facts["Layout.NestedSubgraphContainmentValid"] = false;
                    }
                }
                else
                {
                    if (LeftGap + GAP_TOLERANCE_PX < MinimumCrossAxisGap
                        || RightGap + GAP_TOLERANCE_PX < MinimumCrossAxisGap
                        || BottomGap + GAP_TOLERANCE_PX < MinimumFlowAxisGap
                        || TopGap + GAP_TOLERANCE_PX < MinimumFlowAxisGap)
                    {
                        AnyViolation = true;
                        ParentResult.Facts["Layout.NestedSubgraphContainmentValid"] = false;
                    }
                }
            }
        }

        Diagram.Layout["NestedSubgraphsContainedInParentClusters"] = !AnyViolation;
    }
}
