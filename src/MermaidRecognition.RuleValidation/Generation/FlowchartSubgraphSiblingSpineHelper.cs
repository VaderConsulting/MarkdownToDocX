using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates sibling subgraph cross-boundary member rows share a common spine coordinate.
/// </summary>
public static class FlowchartSubgraphSiblingSpineHelper
{
    private const double SPINE_TOLERANCE_PX = 2.0;

    /// <summary>
    /// Records sibling subgraph spine alignment facts on groups and diagram layout.
    /// </summary>
    public static void ApplySiblingSubgraphSpineFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        Dictionary<string, GroupActualResult> GroupById = Diagram.Groups
            .ToDictionary(Group => Group.GroupId, Group => Group, StringComparer.Ordinal);

        foreach (GroupActualResult Group in Diagram.Groups)
        {
            Group.Facts["Layout.SiblingSubgraphSpineAligned"] = true;
            Group.Facts["HasCrossBoundarySiblingSubgraph"] = false;
        }

        Diagram.Layout["SiblingSubgraphCrossBoundarySpineAligned"] = true;

        Dictionary<string, GroupContract> ContractGroupById =
            FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);

        List<GroupContract> CrossBoundaryGroups = Contract.Groups
            .Where(Group => HasMemberExternalEdge(Contract, Group, ContractGroupById))
            .ToList();

        if (CrossBoundaryGroups.Count < 2)
        {
            return;
        }

        IEnumerable<IGrouping<string, GroupContract>> SiblingBuckets = CrossBoundaryGroups
            .GroupBy(
                Group => Group.ParentGroupId ?? string.Empty,
                StringComparer.Ordinal);

        bool AnySpineViolation = false;

        foreach (IGrouping<string, GroupContract> Bucket in SiblingBuckets)
        {
            List<GroupContract> Siblings = Bucket.ToList();
            if (Siblings.Count < 2)
            {
                continue;
            }

            if (!HorizontalFlow
                && Diagram.Layout.TryGetValue("HasParallelColumnLayout", out object? HasParallelValue)
                && HasParallelValue is bool HasParallelLayout
                && HasParallelLayout
                && Diagram.Layout.TryGetValue("ParallelColumnRowsAligned", out object? RowsAlignedValue)
                && RowsAlignedValue is bool RowsAligned
                && RowsAligned)
            {
                foreach (GroupContract Group in Siblings)
                {
                    if (GroupById.TryGetValue(Group.Id, out GroupActualResult? GroupResult))
                    {
                        GroupResult.Facts["HasCrossBoundarySiblingSubgraph"] = true;
                        GroupResult.Facts["Layout.SiblingSubgraphSpineAligned"] = true;
                    }
                }

                continue;
            }

            bool UseRowSpine = !HorizontalFlow
                && AreSiblingGroupsSideBySideOnCrossAxis(Siblings, BoundsById);

            List<double> CrossCenters = [];
            foreach (GroupContract Group in Siblings)
            {
                if (TryGetGroupCrossCenter(
                        Group,
                        Contract,
                        ContractGroupById,
                        BoundsById,
                        HorizontalFlow,
                        UseRowSpine,
                        out double CrossCenter))
                {
                    CrossCenters.Add(CrossCenter);
                }
            }

            if (CrossCenters.Count < 2)
            {
                continue;
            }

            double TargetCrossCenter = CrossCenters.Min();
            bool BucketAligned = true;

            foreach (GroupContract Group in Siblings)
            {
                if (!GroupById.TryGetValue(Group.Id, out GroupActualResult? GroupResult))
                {
                    continue;
                }

                GroupResult.Facts["HasCrossBoundarySiblingSubgraph"] = true;

                if (!TryGetGroupCrossCenter(
                        Group,
                        Contract,
                        ContractGroupById,
                        BoundsById,
                        HorizontalFlow,
                        UseRowSpine,
                        out double GroupCrossCenter))
                {
                    continue;
                }

                if (Math.Abs(GroupCrossCenter - TargetCrossCenter) > SPINE_TOLERANCE_PX)
                {
                    BucketAligned = false;
                }
            }

            if (!BucketAligned)
            {
                AnySpineViolation = true;
                foreach (GroupContract Group in Siblings)
                {
                    if (GroupById.TryGetValue(Group.Id, out GroupActualResult? GroupResult))
                    {
                        GroupResult.Facts["Layout.SiblingSubgraphSpineAligned"] = false;
                    }
                }
            }
        }

        Diagram.Layout["SiblingSubgraphCrossBoundarySpineAligned"] = !AnySpineViolation;
    }

    private static bool HasMemberExternalEdge(
        DiagramContract Contract,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById)
    {
        HashSet<string> MemberNodeIds = CollectGroupMemberNodeIds(Group, GroupById, Contract);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool SourceIsMember = MemberNodeIds.Contains(Edge.SourceId);
            bool TargetIsMember = MemberNodeIds.Contains(Edge.TargetId);

            if (SourceIsMember != TargetIsMember)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMemberExternalEdge(
        DiagramContract Contract,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        string NodeId)
    {
        HashSet<string> MemberNodeIds = CollectGroupMemberNodeIds(Group, GroupById, Contract);
        if (!MemberNodeIds.Contains(NodeId))
        {
            return false;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool SourceIsMember = MemberNodeIds.Contains(Edge.SourceId);
            bool TargetIsMember = MemberNodeIds.Contains(Edge.TargetId);
            if (SourceIsMember == TargetIsMember)
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                || string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreSiblingGroupsSideBySideOnCrossAxis(
        List<GroupContract> Siblings,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (Siblings.Count < 2)
        {
            return false;
        }

        List<(double Left, double Right, double Top, double Bottom)> Rects = [];
        foreach (GroupContract Group in Siblings)
        {
            bool HasAny = false;
            double Left = double.PositiveInfinity;
            double Right = double.NegativeInfinity;
            double Top = double.NegativeInfinity;
            double Bottom = double.PositiveInfinity;

            foreach (string NodeId in Group.ChildNodeIds)
            {
                if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
                {
                    continue;
                }

                HasAny = true;
                Left = Math.Min(Left, Bounds.CenterX - (Bounds.Width / 2.0));
                Right = Math.Max(Right, Bounds.CenterX + (Bounds.Width / 2.0));
                Top = Math.Max(Top, Bounds.CenterY + (Bounds.Height / 2.0));
                Bottom = Math.Min(Bottom, Bounds.CenterY - (Bounds.Height / 2.0));
            }

            if (HasAny)
            {
                Rects.Add((Left, Right, Top, Bottom));
            }
        }

        if (Rects.Count < 2)
        {
            return false;
        }

        for (int LeftIndex = 0; LeftIndex < Rects.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Rects.Count; RightIndex++)
            {
                (double Left, double Right, double Top, double Bottom) LeftRect = Rects[LeftIndex];
                (double Left, double Right, double Top, double Bottom) RightRect = Rects[RightIndex];
                bool SeparatedOnCrossAxis = LeftRect.Right <= RightRect.Left || RightRect.Right <= LeftRect.Left;
                bool OverlapsOnFlowAxis = LeftRect.Bottom < RightRect.Top && RightRect.Bottom < LeftRect.Top;
                if (SeparatedOnCrossAxis && OverlapsOnFlowAxis)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetGroupCrossCenter(
        GroupContract Group,
        DiagramContract Contract,
        Dictionary<string, GroupContract> GroupById,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow,
        bool UseRowSpine,
        out double CrossCenter)
    {
        CrossCenter = 0.0;
        HashSet<string> MemberNodeIds = CollectGroupMemberNodeIds(Group, GroupById, Contract);
        bool HasAny = false;
        double MinCross = 0.0;
        double MaxCross = 0.0;

        foreach (string NodeId in MemberNodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
            {
                continue;
            }

            if (UseRowSpine && !HasMemberExternalEdge(Contract, Group, GroupById, NodeId))
            {
                continue;
            }

            double Cross = UseRowSpine || HorizontalFlow ? Bounds.CenterY : Bounds.CenterX;
            if (!HasAny)
            {
                HasAny = true;
                MinCross = Cross;
                MaxCross = Cross;
            }
            else
            {
                if (Cross < MinCross)
                {
                    MinCross = Cross;
                }

                if (Cross > MaxCross)
                {
                    MaxCross = Cross;
                }
            }
        }

        if (!HasAny)
        {
            return false;
        }

        CrossCenter = (MinCross + MaxCross) / 2.0;
        return true;
    }

    private static HashSet<string> CollectGroupMemberNodeIds(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract)
    {
        HashSet<string> MemberNodeIds = new HashSet<string>(StringComparer.Ordinal);

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

            MemberNodeIds.UnionWith(CollectGroupMemberNodeIds(CandidateGroup, GroupById, Contract));
        }

        return MemberNodeIds;
    }
}
