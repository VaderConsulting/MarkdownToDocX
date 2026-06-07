using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates member node insets and centreing inside subgraph cluster boxes.
/// </summary>
public static class FlowchartSubgraphMemberInsetHelper
{
    private const double INSET_TOLERANCE_PX = 1.0;

    private const double CENTRE_TOLERANCE_PX = 2.0;

    /// <summary>
    /// Records subgraph member inset and centreing facts on groups.
    /// </summary>
    public static void ApplySubgraphMemberInsetFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        Dictionary<string, GroupActualResult> GroupById = Diagram.Groups
            .ToDictionary(Group => Group.GroupId, Group => Group, StringComparer.Ordinal);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSubgraphBoundsRequest BoundsRequest =
            FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        foreach (GroupActualResult Group in Diagram.Groups)
        {
            Group.Facts["Layout.MemberClusterInsetValid"] = true;
            Group.Facts["Layout.MembersVerticallyCenteredInCluster"] = true;
            Group.Facts["Layout.MembersHorizontallyCenteredInCluster"] = true;
            Group.Facts["HasMemberNodes"] = false;
        }

        bool AnyInsetViolation = false;
        bool AnyVerticalCentreViolation = false;
        bool AnyHorizontalCentreViolation = false;

        foreach (GroupContract Group in Contract.Groups)
        {
            if (!RectByGroupId.TryGetValue(Group.Id, out FlowchartSubgraphLayoutRect? ClusterRect))
            {
                continue;
            }

            if (!GroupById.TryGetValue(Group.Id, out GroupActualResult? GroupResult))
            {
                continue;
            }

            List<string> MemberNodeIds = CollectAllDescendantMemberNodeIds(Group, Contract);
            if (MemberNodeIds.Count == 0)
            {
                continue;
            }

            GroupResult.Facts["HasMemberNodes"] = true;

            bool GroupInsetValid = true;
            bool GroupVerticalCentreValid = true;
            bool GroupHorizontalCentreValid = true;

            if (!FlowchartSubgraphBoundsBuilder.TryComputeMemberContentBounds(
                    Contract,
                    Group,
                    BoundsById,
                    out double ContentMinX,
                    out double ContentMinY,
                    out double ContentMaxX,
                    out double ContentMaxY))
            {
                continue;
            }

            foreach (string MemberNodeId in MemberNodeIds)
            {
                if (!BoundsById.TryGetValue(MemberNodeId, out FlowchartLayoutNodeBounds? Bounds))
                {
                    continue;
                }

                double NodeLeft = Bounds.CenterX - (Bounds.Width / 2.0);
                double NodeRight = Bounds.CenterX + (Bounds.Width / 2.0);
                double NodeBottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double NodeTop = Bounds.CenterY + (Bounds.Height / 2.0);

                double LeftInset = NodeLeft - ClusterRect.Left;
                double RightInset = ClusterRect.Right - NodeRight;
                double BottomInset = NodeBottom - ClusterRect.Bottom;
                double TopInset = ClusterRect.Top - NodeTop;

                if (LeftInset + INSET_TOLERANCE_PX < FlowchartSubgraphBoundsBuilder.PADDING_PX
                    || RightInset + INSET_TOLERANCE_PX < FlowchartSubgraphBoundsBuilder.PADDING_PX
                    || BottomInset + INSET_TOLERANCE_PX < FlowchartSubgraphBoundsBuilder.PADDING_PX
                    || TopInset + INSET_TOLERANCE_PX
                        < FlowchartSubgraphBoundsBuilder.PADDING_PX + FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX)
                {
                    GroupInsetValid = false;
                }
            }

            double ContentRegionLeft = ClusterRect.Left + FlowchartSubgraphBoundsBuilder.PADDING_PX;
            double ContentRegionRight = ClusterRect.Right - FlowchartSubgraphBoundsBuilder.PADDING_PX;
            double ContentRegionBottom = ClusterRect.Bottom
                + FlowchartSubgraphBoundsBuilder.PADDING_PX
                + FlowchartSubgraphBoundsBuilder.VERTICAL_CONTENT_SLACK_PX;
            double ContentRegionTop = ClusterRect.Top
                - FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX
                - FlowchartSubgraphBoundsBuilder.PADDING_PX;

            double RegionCenterX = (ContentRegionLeft + ContentRegionRight) / 2.0;
            double RegionCenterY = (ContentRegionBottom + ContentRegionTop) / 2.0;

            if (FlowchartSubgraphSpineCenterHelper.TryGetPrimaryColumnCenterX(
                    Contract,
                    Group,
                    BoundsById,
                    out double PrimaryColumnCenterX))
            {
                if (Math.Abs(PrimaryColumnCenterX - RegionCenterX) > CENTRE_TOLERANCE_PX)
                {
                    GroupHorizontalCentreValid = false;
                }
            }
            else
            {
                double ContentCenterX = (ContentMinX + ContentMaxX) / 2.0;
                if (Math.Abs(ContentCenterX - RegionCenterX) > CENTRE_TOLERANCE_PX)
                {
                    GroupHorizontalCentreValid = false;
                }
            }

            if (FlowchartSubgraphSpineCenterHelper.TryGetPrimaryRowCenterY(
                    Contract,
                    Group,
                    BoundsById,
                    out double PrimaryRowCenterY))
            {
                if (Math.Abs(PrimaryRowCenterY - RegionCenterY) > CENTRE_TOLERANCE_PX)
                {
                    GroupVerticalCentreValid = false;
                }
            }
            else
            {
                double ContentCenterY = (ContentMinY + ContentMaxY) / 2.0;
                if (Math.Abs(ContentCenterY - RegionCenterY) > CENTRE_TOLERANCE_PX)
                {
                    GroupVerticalCentreValid = false;
                }
            }

            if (!GroupInsetValid)
            {
                AnyInsetViolation = true;
                GroupResult.Facts["Layout.MemberClusterInsetValid"] = false;
            }

            if (!GroupVerticalCentreValid)
            {
                AnyVerticalCentreViolation = true;
                GroupResult.Facts["Layout.MembersVerticallyCenteredInCluster"] = false;
            }

            if (!GroupHorizontalCentreValid)
            {
                AnyHorizontalCentreViolation = true;
                GroupResult.Facts["Layout.MembersHorizontallyCenteredInCluster"] = false;
            }
        }

        Diagram.Layout["SubgraphMemberInsetsRespected"] = !AnyInsetViolation;
        Diagram.Layout["SubgraphMembersVerticallyCentered"] = !AnyVerticalCentreViolation;
        Diagram.Layout["SubgraphMembersHorizontallyCentered"] = !AnyHorizontalCentreViolation;
    }

    private static List<string> CollectAllDescendantMemberNodeIds(GroupContract Group, DiagramContract Contract)
    {
        Dictionary<string, GroupContract> GroupById =
            FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        HashSet<string> MemberNodeIds = new HashSet<string>(StringComparer.Ordinal);
        CollectDescendantMemberNodeIdsRecursive(Group, Contract, GroupById, MemberNodeIds);
        return MemberNodeIds.ToList();
    }

    private static void CollectDescendantMemberNodeIdsRecursive(
        GroupContract Group,
        DiagramContract Contract,
        Dictionary<string, GroupContract> GroupById,
        HashSet<string> MemberNodeIds)
    {
        foreach (string NodeId in Group.ChildNodeIds)
        {
            MemberNodeIds.Add(NodeId);
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                MemberNodeIds.Add(Node.Id);
            }
        }

        foreach (GroupContract CandidateGroup in GroupById.Values)
        {
            if (!string.Equals(CandidateGroup.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            CollectDescendantMemberNodeIdsRecursive(CandidateGroup, Contract, GroupById, MemberNodeIds);
        }
    }
}
