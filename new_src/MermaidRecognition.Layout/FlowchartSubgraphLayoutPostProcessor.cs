using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Layout adjustments for subgraph membership, sibling spacing, and boundary-node alignment.
/// </summary>
public static class FlowchartSubgraphLayoutPostProcessor
{
    private const double SUBGRAPH_PADDING_PX = 18.0;

    private const double SUBGRAPH_TITLE_BAND_PX = 26.0;

    /// <summary>
    /// Minimum exterior gap between sibling subgraph cluster boxes on the primary flow axis.
    /// </summary>
    public const double SUBGRAPH_EXTERNAL_GAP_PX = 24.0;

    /// <summary>
    /// Applies subgraph alignment and minimum spacing when the contract declares groups.
    /// </summary>
    public static List<FlowchartLayoutNodeBounds> Apply(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Contract.Groups.Count == 0 || Source.Count == 0)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool ReverseFlow = Direction == FlowchartDirection.RightToLeft
            || Direction == FlowchartDirection.BottomToTop;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        Dictionary<string, GroupContract> GroupById =
            new Dictionary<string, GroupContract>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupById[Group.Id] = Group;
        }

        ApplyExplicitSubgraphDirectionLayout(Contract, MutableById, GroupById, Direction, Options);
        AlignSiblingSubgraphCrossAxisSpine(Contract, MutableById, GroupById, HorizontalFlow);
        AlignExternalBoundaryNodes(Contract, MutableById, GroupById, HorizontalFlow);
        EnforceNestedSubgraphClusterSeparation(Contract, MutableById, GroupById, HorizontalFlow, ReverseFlow, Options);
        CenterSubgraphMembersInClusterBoxes(Contract, MutableById, GroupById, Options);
        EnforceSiblingSubgraphGaps(Contract, MutableById, GroupById, HorizontalFlow, ReverseFlow, Options);
        EnforceExternalSubgraphGaps(Contract, MutableById, GroupById, HorizontalFlow, ReverseFlow, Options);
        FlowchartParallelColumnAlignHelper.Apply(Contract, MutableById, Direction, Options);

        return MutableById.Values.ToList();
    }

    private static void ApplyExplicitSubgraphDirectionLayout(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        FlowchartDirection DiagramDirection,
        FlowchartRenderOptions Options)
    {
        bool DiagramHorizontal = DiagramDirection == FlowchartDirection.LeftToRight
            || DiagramDirection == FlowchartDirection.RightToLeft;

        List<GroupContract> OrderedGroups = Contract.Groups
            .OrderByDescending(Group => GetGroupDepth(Group, GroupById))
            .ToList();

        foreach (GroupContract Group in OrderedGroups)
        {
            if (string.IsNullOrWhiteSpace(Group.Direction))
            {
                continue;
            }

            if (SubgraphHasMemberExternalEdge(Contract, Group, GroupById))
            {
                if (DiagramHorizontal)
                {
                    LayoutHorizontalSubgraphMembers(Contract, Group, GroupById, MutableById, Options);
                }
                else
                {
                    LayoutVerticalSubgraphMembers(Contract, Group, GroupById, MutableById, Options);
                }

                continue;
            }

            bool GroupHorizontal = string.Equals(Group.Direction, "LeftToRight", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Group.Direction, "RightToLeft", StringComparison.OrdinalIgnoreCase);
            bool GroupVertical = string.Equals(Group.Direction, "TopToBottom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Group.Direction, "BottomToTop", StringComparison.OrdinalIgnoreCase);

            if (GroupHorizontal && !DiagramHorizontal)
            {
                LayoutHorizontalSubgraphMembers(Contract, Group, GroupById, MutableById, Options);
            }
            else if (GroupVertical && DiagramHorizontal)
            {
                LayoutVerticalSubgraphMembers(Contract, Group, GroupById, MutableById, Options);
            }
            else if (GroupVertical && !DiagramHorizontal && Group.ParentGroupId is not null)
            {
                LayoutVerticalSubgraphMembers(Contract, Group, GroupById, MutableById, Options);
            }
        }
    }

    /// <summary>
    /// Mermaid ignores declared subgraph direction when a member node links across the subgraph boundary.
    /// </summary>
    private static bool SubgraphHasMemberExternalEdge(
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

            if (string.Equals(Edge.TargetId, Group.Id, StringComparison.Ordinal)
                && !MemberNodeIds.Contains(Edge.SourceId))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, Group.Id, StringComparison.Ordinal)
                && !MemberNodeIds.Contains(Edge.TargetId))
            {
                continue;
            }

            if (SourceIsMember != TargetIsMember)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Keeps direct members outside nested cluster boxes on the flow axis (F45/F84 parity with Naiad compound layout).
    /// </summary>
    private static void EnforceNestedSubgraphClusterSeparation(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        bool HorizontalFlow,
        bool ReverseFlow,
        FlowchartRenderOptions Options)
    {
        double MinimumGap = Math.Max(SUBGRAPH_EXTERNAL_GAP_PX, Options.RankSpacingPx);
        List<GroupContract> OrderedGroups = Contract.Groups
            .OrderBy(Group => GetGroupDepth(Group, GroupById))
            .ToList();

        foreach (GroupContract Group in OrderedGroups)
        {
            if (Group.ChildGroupIds.Count == 0)
            {
                continue;
            }

            HashSet<string> NestedDescendantNodeIds = CollectNestedDescendantNodeIds(Group, GroupById, Contract);
            if (NestedDescendantNodeIds.Count == 0)
            {
                continue;
            }

            List<string> DirectMemberNodeIds = CollectDirectGroupMemberNodeIds(Group, GroupById, Contract);
            if (DirectMemberNodeIds.Count == 0)
            {
                continue;
            }

            List<string> FlowOrderedMemberNodeIds = OrderNodesByInternalFlow(
                Contract,
                NestedDescendantNodeIds.Union(DirectMemberNodeIds).ToList());
            if (FlowOrderedMemberNodeIds.Count == 0)
            {
                FlowOrderedMemberNodeIds = DirectMemberNodeIds
                    .Concat(NestedDescendantNodeIds)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            int FirstNestedFlowIndex = int.MaxValue;
            int LastNestedFlowIndex = int.MinValue;
            for (int Index = 0; Index < FlowOrderedMemberNodeIds.Count; Index++)
            {
                if (!NestedDescendantNodeIds.Contains(FlowOrderedMemberNodeIds[Index]))
                {
                    continue;
                }

                if (Index < FirstNestedFlowIndex)
                {
                    FirstNestedFlowIndex = Index;
                }

                if (Index > LastNestedFlowIndex)
                {
                    LastNestedFlowIndex = Index;
                }
            }

            if (FirstNestedFlowIndex == int.MaxValue)
            {
                continue;
            }

            List<string> UpstreamDirectNodeIds = new List<string>();
            List<string> DownstreamDirectNodeIds = new List<string>();
            for (int Index = 0; Index < FlowOrderedMemberNodeIds.Count; Index++)
            {
                string NodeId = FlowOrderedMemberNodeIds[Index];
                if (!DirectMemberNodeIds.Contains(NodeId))
                {
                    continue;
                }

                if (Index < FirstNestedFlowIndex)
                {
                    UpstreamDirectNodeIds.Add(NodeId);
                }
                else if (Index > LastNestedFlowIndex)
                {
                    DownstreamDirectNodeIds.Add(NodeId);
                }
            }

            Dictionary<string, FlowchartLayoutNodeBounds> ReadOnlyBounds = MutableById;
            FlowchartSubgraphBoundsRequest BoundsRequest =
                FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
            List<FlowchartSubgraphLayoutRect> SubgraphRects =
                FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, ReadOnlyBounds, BoundsRequest);

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                FlowchartSubgraphLayoutRect? ChildRect = SubgraphRects.Find(
                    Candidate => string.Equals(Candidate.GroupId, ChildGroupId, StringComparison.Ordinal));
                if (ChildRect is null)
                {
                    continue;
                }

                if (UpstreamDirectNodeIds.Count > 0)
                {
                    EnforceDirectMembersOutsideNestedClusterLeadingEdge(
                        UpstreamDirectNodeIds,
                        ChildRect,
                        MutableById,
                        HorizontalFlow,
                        ReverseFlow,
                        MinimumGap,
                        UpstreamClusterSide: true);
                }

                if (DownstreamDirectNodeIds.Count > 0)
                {
                    EnforceDirectMembersOutsideNestedClusterLeadingEdge(
                        DownstreamDirectNodeIds,
                        ChildRect,
                        MutableById,
                        HorizontalFlow,
                        ReverseFlow,
                        MinimumGap,
                        UpstreamClusterSide: false);
                }
            }
        }
    }

    private static void EnforceDirectMembersOutsideNestedClusterLeadingEdge(
        List<string> DirectMemberNodeIds,
        FlowchartSubgraphLayoutRect NestedClusterRect,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        bool HorizontalFlow,
        bool ReverseFlow,
        double MinimumGap,
        bool UpstreamClusterSide)
    {
        if (DirectMemberNodeIds.Count == 0)
        {
            return;
        }

        if (HorizontalFlow)
        {
            if (UpstreamClusterSide ^ ReverseFlow)
            {
                double ClusterLeadingEdge = NestedClusterRect.Left;
                double RequiredTrailingEdge = ClusterLeadingEdge - MinimumGap;
                double MaxDirectTrailingEdge = double.MinValue;
                foreach (string NodeId in DirectMemberNodeIds)
                {
                    if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                    {
                        continue;
                    }

                    MaxDirectTrailingEdge = Math.Max(
                        MaxDirectTrailingEdge,
                        Bounds.CenterX + (Bounds.Width / 2.0));
                }

                if (MaxDirectTrailingEdge <= RequiredTrailingEdge + 0.5)
                {
                    return;
                }

                double Shift = RequiredTrailingEdge - MaxDirectTrailingEdge;
                foreach (string NodeId in DirectMemberNodeIds)
                {
                    if (MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                    {
                        MutableById[NodeId] = ShiftBoundsOnFlowAxis(Bounds, Shift, HorizontalFlow: true);
                    }
                }

                return;
            }

            double ClusterTrailingEdge = NestedClusterRect.Right;
            double RequiredLeadingEdge = ClusterTrailingEdge + MinimumGap;
            double MinDirectLeadingEdge = double.MaxValue;
            foreach (string NodeId in DirectMemberNodeIds)
            {
                if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                {
                    continue;
                }

                MinDirectLeadingEdge = Math.Min(
                    MinDirectLeadingEdge,
                    Bounds.CenterX - (Bounds.Width / 2.0));
            }

            if (MinDirectLeadingEdge >= RequiredLeadingEdge - 0.5)
            {
                return;
            }

            double DownstreamShift = RequiredLeadingEdge - MinDirectLeadingEdge;
            foreach (string NodeId in DirectMemberNodeIds)
            {
                if (MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                {
                    MutableById[NodeId] = ShiftBoundsOnFlowAxis(Bounds, DownstreamShift, HorizontalFlow: true);
                }
            }

            return;
        }

        if (UpstreamClusterSide ^ ReverseFlow)
        {
            double ClusterLeadingEdge = NestedClusterRect.Top;
            double RequiredTrailingEdge = ClusterLeadingEdge + MinimumGap;
            double MinDirectTrailingEdge = double.MaxValue;
            foreach (string NodeId in DirectMemberNodeIds)
            {
                if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                {
                    continue;
                }

                MinDirectTrailingEdge = Math.Min(
                    MinDirectTrailingEdge,
                    Bounds.CenterY - (Bounds.Height / 2.0));
            }

            if (MinDirectTrailingEdge >= RequiredTrailingEdge - 0.5)
            {
                return;
            }

            double Shift = RequiredTrailingEdge - MinDirectTrailingEdge;
            foreach (string NodeId in DirectMemberNodeIds)
            {
                if (MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
                {
                    MutableById[NodeId] = ShiftBoundsOnFlowAxis(Bounds, Shift, HorizontalFlow: false);
                }
            }

            return;
        }

        double ClusterBottomEdge = NestedClusterRect.Bottom;
        double RequiredTopEdge = ClusterBottomEdge - MinimumGap;
        double MaxDirectTopEdge = double.MinValue;
        foreach (string NodeId in DirectMemberNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            MaxDirectTopEdge = Math.Max(
                MaxDirectTopEdge,
                Bounds.CenterY + (Bounds.Height / 2.0));
        }

        if (MaxDirectTopEdge <= RequiredTopEdge + 0.5)
        {
            return;
        }

        double DownstreamVerticalShift = RequiredTopEdge - MaxDirectTopEdge;
        foreach (string NodeId in DirectMemberNodeIds)
        {
            if (MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                MutableById[NodeId] = ShiftBoundsOnFlowAxis(Bounds, DownstreamVerticalShift, HorizontalFlow: false);
            }
        }
    }

    private static HashSet<string> CollectNestedDescendantNodeIds(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract)
    {
        HashSet<string> NestedDescendantNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract CandidateGroup in GroupById.Values)
        {
            if (!string.Equals(CandidateGroup.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            NestedDescendantNodeIds.UnionWith(
                CollectGroupMemberNodeIds(CandidateGroup, GroupById, Contract));
        }

        return NestedDescendantNodeIds;
    }

    private static int GetGroupDepth(GroupContract Group, Dictionary<string, GroupContract> GroupById)
    {
        int Depth = 0;
        string? ParentId = Group.ParentGroupId;
        while (ParentId is not null && GroupById.ContainsKey(ParentId))
        {
            Depth++;
            ParentId = GroupById[ParentId].ParentGroupId;
        }

        return Depth;
    }

    private static void LayoutHorizontalSubgraphMembers(
        DiagramContract Contract,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        FlowchartRenderOptions Options)
    {
        List<string> MemberNodeIds = CollectDirectGroupMemberNodeIds(Group, GroupById, Contract);
        if (MemberNodeIds.Count < 2)
        {
            return;
        }

        List<string> OrderedNodeIds = OrderNodesByInternalFlow(Contract, MemberNodeIds);
        if (OrderedNodeIds.Count < 2)
        {
            OrderedNodeIds = MemberNodeIds
                .OrderBy(NodeId => GetEarliestEdgeCreationIndex(Contract, NodeId))
                .ToList();
        }

        double SharedFlowY = 0.0;
        double PackCenterX = 0.0;
        int CrossCount = 0;
        foreach (string NodeId in OrderedNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            SharedFlowY += Bounds.CenterY;
            PackCenterX += Bounds.CenterX;
            CrossCount++;
        }

        if (CrossCount == 0)
        {
            return;
        }

        SharedFlowY /= CrossCount;
        PackCenterX /= CrossCount;
        double MinimumGap = Math.Max(Options.NodeSpacingPx, 24.0);
        double TotalWidth = 0.0;
        foreach (string NodeId in OrderedNodeIds)
        {
            if (MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                TotalWidth += Bounds.Width;
            }
        }

        TotalWidth += MinimumGap * (OrderedNodeIds.Count - 1);
        double CursorX = PackCenterX - (TotalWidth / 2.0);
        foreach (string NodeId in OrderedNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double CenterX = CursorX + (Bounds.Width / 2.0);
            CursorX += Bounds.Width + MinimumGap;
            MutableById[NodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = CenterX,
                CenterY = SharedFlowY,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }
    }

    private static void LayoutVerticalSubgraphMembers(
        DiagramContract Contract,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        FlowchartRenderOptions Options)
    {
        List<string> MemberNodeIds = CollectDirectGroupMemberNodeIds(Group, GroupById, Contract);
        if (MemberNodeIds.Count < 2)
        {
            return;
        }

        List<string> OrderedNodeIds = OrderNodesByInternalFlow(Contract, MemberNodeIds);
        if (OrderedNodeIds.Count < 2)
        {
            OrderedNodeIds = MemberNodeIds
                .OrderBy(NodeId => GetEarliestEdgeCreationIndex(Contract, NodeId))
                .ToList();
        }

        double SharedCenterX = 0.0;
        double PackCenterY = 0.0;
        int CrossCount = 0;
        foreach (string NodeId in OrderedNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            SharedCenterX += Bounds.CenterX;
            PackCenterY += Bounds.CenterY;
            CrossCount++;
        }

        if (CrossCount == 0)
        {
            return;
        }

        SharedCenterX /= CrossCount;
        PackCenterY /= CrossCount;
        double MinimumGap = Math.Max(Options.RankSpacingPx, Options.NodeSpacingPx);
        double TotalHeight = 0.0;
        foreach (string NodeId in OrderedNodeIds)
        {
            if (MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                TotalHeight += Bounds.Height;
            }
        }

        TotalHeight += MinimumGap * (OrderedNodeIds.Count - 1);
        double CursorY = PackCenterY + (TotalHeight / 2.0);
        foreach (string NodeId in OrderedNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double CenterY = CursorY - (Bounds.Height / 2.0);
            CursorY -= Bounds.Height + MinimumGap;
            MutableById[NodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = SharedCenterX,
                CenterY = CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }
    }

    private static List<string> CollectDirectGroupMemberNodeIds(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract)
    {
        HashSet<string> NestedGroupNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract CandidateGroup in GroupById.Values)
        {
            if (!string.Equals(CandidateGroup.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            NestedGroupNodeIds.UnionWith(CollectGroupMemberNodeIds(CandidateGroup, GroupById, Contract));
        }

        List<string> DirectMemberNodeIds = new List<string>();
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (NestedGroupNodeIds.Contains(Node.Id))
            {
                continue;
            }

            DirectMemberNodeIds.Add(Node.Id);
        }

        foreach (string NodeId in Group.ChildNodeIds)
        {
            if (!NestedGroupNodeIds.Contains(NodeId) && !DirectMemberNodeIds.Contains(NodeId, StringComparer.Ordinal))
            {
                DirectMemberNodeIds.Add(NodeId);
            }
        }

        return DirectMemberNodeIds;
    }

    private static List<string> OrderNodesByInternalFlow(DiagramContract Contract, List<string> MemberNodeIds)
    {
        HashSet<string> MemberSet = new HashSet<string>(MemberNodeIds, StringComparer.Ordinal);
        Dictionary<string, int> InDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string NodeId in MemberNodeIds)
        {
            InDegree[NodeId] = 0;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (MemberSet.Contains(Edge.SourceId) && MemberSet.Contains(Edge.TargetId))
            {
                InDegree[Edge.TargetId]++;
            }
        }

        List<string> Ordered = new List<string>();
        Queue<string> Ready = new Queue<string>();
        foreach (KeyValuePair<string, int> Entry in InDegree.OrderBy(Entry => GetEarliestEdgeCreationIndex(Contract, Entry.Key)))
        {
            if (Entry.Value == 0)
            {
                Ready.Enqueue(Entry.Key);
            }
        }

        while (Ready.Count > 0)
        {
            string NodeId = Ready.Dequeue();
            Ordered.Add(NodeId);
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!MemberSet.Contains(Edge.TargetId))
                {
                    continue;
                }

                InDegree[Edge.TargetId]--;
                if (InDegree[Edge.TargetId] == 0)
                {
                    Ready.Enqueue(Edge.TargetId);
                }
            }
        }

        return Ordered;
    }

    private static int GetEarliestEdgeCreationIndex(DiagramContract Contract, string NodeId)
    {
        int Earliest = int.MaxValue;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                || string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                Earliest = Math.Min(Earliest, Edge.CreationIndex);
            }
        }

        return Earliest == int.MaxValue ? 0 : Earliest;
    }

    /// <summary>
    /// Aligns sibling subgraph cross-boundary member rows to a shared spine (F60/F40 parity).
    /// </summary>
    private static void AlignSiblingSubgraphCrossAxisSpine(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        bool HorizontalFlow)
    {
        List<GroupContract> CrossBoundaryGroups = Contract.Groups
            .Where(Group => SubgraphHasMemberExternalEdge(Contract, Group, GroupById))
            .ToList();

        if (CrossBoundaryGroups.Count < 2)
        {
            return;
        }

        IEnumerable<IGrouping<string, GroupContract>> SiblingBuckets = CrossBoundaryGroups
            .GroupBy(
                Group => Group.ParentGroupId ?? string.Empty,
                StringComparer.Ordinal);

        foreach (IGrouping<string, GroupContract> Bucket in SiblingBuckets)
        {
            List<GroupContract> Siblings = Bucket.ToList();
            if (Siblings.Count < 2)
            {
                continue;
            }

            if (AreSiblingGroupsSideBySideOnCrossAxis(
                    Contract,
                    GroupById,
                    MutableById,
                    Siblings,
                    HorizontalFlow))
            {
                continue;
            }

            GroupContract ReferenceGroup = Siblings
                .OrderBy(Group => GetGroupMemberFlowLeadingEdge(Group, GroupById, Contract, MutableById, HorizontalFlow))
                .First();

            if (!TryGetGroupCrossCenter(
                    ReferenceGroup.Id,
                    GroupById,
                    Contract,
                    MutableById,
                    HorizontalFlow,
                    out double TargetCrossCenter))
            {
                continue;
            }

            foreach (GroupContract Group in Siblings)
            {
                if (string.Equals(Group.Id, ReferenceGroup.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryGetGroupCrossCenter(
                        Group.Id,
                        GroupById,
                        Contract,
                        MutableById,
                        HorizontalFlow,
                        out double GroupCrossCenter))
                {
                    continue;
                }

                double CrossDelta = TargetCrossCenter - GroupCrossCenter;
                if (Math.Abs(CrossDelta) < 0.5)
                {
                    continue;
                }

                if (HorizontalFlow)
                {
                    ShiftGroupMembersByOffset(Group, GroupById, Contract, MutableById, 0.0, CrossDelta);
                }
                else
                {
                    ShiftGroupMembersByOffset(Group, GroupById, Contract, MutableById, CrossDelta, 0.0);
                }
            }
        }
    }

    private static bool AreSiblingGroupsSideBySideOnCrossAxis(
        DiagramContract Contract,
        Dictionary<string, GroupContract> GroupById,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        List<GroupContract> Siblings,
        bool HorizontalFlow)
    {
        List<FlowchartSubgraphLayoutRect> Rects = FlowchartSubgraphBoundsBuilder
            .BuildOrderedRects(Contract, MutableById)
            .Where(Rect => Siblings.Any(Group => string.Equals(Group.Id, Rect.GroupId, StringComparison.Ordinal)))
            .ToList();

        if (Rects.Count < 2)
        {
            return false;
        }

        for (int LeftIndex = 0; LeftIndex < Rects.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Rects.Count; RightIndex++)
            {
                FlowchartSubgraphLayoutRect LeftRect = Rects[LeftIndex];
                FlowchartSubgraphLayoutRect RightRect = Rects[RightIndex];

                if (HorizontalFlow)
                {
                    bool SeparatedOnFlowAxis = LeftRect.Right <= RightRect.Left || RightRect.Right <= LeftRect.Left;
                    bool OverlapsOnCrossAxis = LeftRect.Bottom < RightRect.Top && RightRect.Bottom < LeftRect.Top;
                    if (SeparatedOnFlowAxis && OverlapsOnCrossAxis)
                    {
                        return false;
                    }

                    bool SeparatedOnCrossAxis = LeftRect.Top <= RightRect.Bottom || RightRect.Top <= LeftRect.Bottom;
                    bool OverlapsOnFlowAxis = LeftRect.Left < RightRect.Right && RightRect.Left < LeftRect.Right;
                    if (SeparatedOnCrossAxis && OverlapsOnFlowAxis)
                    {
                        return true;
                    }
                }
                else
                {
                    bool SeparatedOnCrossAxis = LeftRect.Right <= RightRect.Left || RightRect.Right <= LeftRect.Left;
                    bool OverlapsOnFlowAxis = LeftRect.Bottom < RightRect.Top && RightRect.Bottom < LeftRect.Top;
                    if (SeparatedOnCrossAxis && OverlapsOnFlowAxis)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static double GetGroupMemberFlowLeadingEdge(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        bool HorizontalFlow)
    {
        HashSet<string> MemberNodeIds = CollectGroupMemberNodeIds(Group, GroupById, Contract);
        double LeadingEdge = double.PositiveInfinity;
        bool HasAny = false;

        foreach (string NodeId in MemberNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double Edge = HorizontalFlow
                ? Bounds.CenterX - (Bounds.Width / 2.0)
                : Bounds.CenterY + (Bounds.Height / 2.0);

            if (!HasAny || Edge < LeadingEdge)
            {
                HasAny = true;
                LeadingEdge = Edge;
            }
        }

        return HasAny ? LeadingEdge : 0.0;
    }

    private static void AlignExternalBoundaryNodes(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        bool HorizontalFlow)
    {
        Dictionary<string, string?> GroupIdByNodeId = BuildGroupIdByNodeId(Contract, GroupById);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GroupIdByNodeId.TryGetValue(Edge.SourceId, out string? SourceGroupId);
            GroupIdByNodeId.TryGetValue(Edge.TargetId, out string? TargetGroupId);

            if (SourceGroupId is null && TargetGroupId is not null)
            {
                AlignNodeToGroupCrossCenter(
                    Edge.SourceId,
                    TargetGroupId,
                    MutableById,
                    GroupById,
                    Contract,
                    HorizontalFlow);
            }

            if (SourceGroupId is not null && TargetGroupId is null)
            {
                AlignNodeToGroupCrossCenter(
                    Edge.TargetId,
                    SourceGroupId,
                    MutableById,
                    GroupById,
                    Contract,
                    HorizontalFlow);
            }
        }
    }

    private static void AlignNodeToGroupCrossCenter(
        string ExternalNodeId,
        string GroupId,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        bool HorizontalFlow)
    {
        if (!MutableById.TryGetValue(ExternalNodeId, out FlowchartLayoutNodeBounds ExternalBounds))
        {
            return;
        }

        if (!TryGetGroupCrossCenter(GroupId, GroupById, Contract, MutableById, HorizontalFlow, out double CrossCenter))
        {
            return;
        }

        if (HorizontalFlow)
        {
            MutableById[ExternalNodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = ExternalBounds.NodeId,
                CenterX = ExternalBounds.CenterX,
                CenterY = CrossCenter,
                Width = ExternalBounds.Width,
                Height = ExternalBounds.Height
            };
        }
        else
        {
            MutableById[ExternalNodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = ExternalBounds.NodeId,
                CenterX = CrossCenter,
                CenterY = ExternalBounds.CenterY,
                Width = ExternalBounds.Width,
                Height = ExternalBounds.Height
            };
        }
    }

    /// <summary>
    /// Centres member content inside each cluster box with minimum padding and title-band clearance (Naiad parity).
    /// </summary>
    private static void CenterSubgraphMembersInClusterBoxes(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        FlowchartRenderOptions Options)
    {
        List<GroupContract> OrderedGroups = Contract.Groups
            .OrderByDescending(Group => GetGroupDepth(Group, GroupById))
            .ToList();

        foreach (GroupContract Group in OrderedGroups)
        {
            if (!TryGetGroupMemberContentBounds(
                    Group,
                    GroupById,
                    Contract,
                    MutableById,
                    Options,
                    out double MinX,
                    out double MinY,
                    out double MaxX,
                    out double MaxY))
            {
                continue;
            }

            bool UsesPrimaryColumnCentering = FlowchartSubgraphSpineCenterHelper.TryGetPrimaryColumnCenterX(
                Contract,
                Group,
                MutableById,
                out double PrimaryColumnCenterX);

            bool UsesPrimaryRowCentering = FlowchartSubgraphSpineCenterHelper.TryGetPrimaryRowCenterY(
                Contract,
                Group,
                MutableById,
                out double PrimaryRowCenterY);

            if (UsesPrimaryColumnCentering)
            {
                FlowchartSubgraphSpineCenterHelper.ExpandHorizontalBoundsAroundPrimaryColumn(
                    Contract,
                    Group,
                    MutableById,
                    ref MinX,
                    ref MaxX);
            }

            if (UsesPrimaryRowCentering)
            {
                FlowchartSubgraphSpineCenterHelper.ExpandVerticalBoundsAroundPrimaryRow(
                    Contract,
                    Group,
                    MutableById,
                    ref MinY,
                    ref MaxY);
            }

            double ContentWidth = MaxX - MinX;
            double ContentHeight = MaxY - MinY;
            double ClusterWidth = ContentWidth + (FlowchartSubgraphBoundsBuilder.PADDING_PX * 2.0);
            double ClusterHeight = ContentHeight
                + (FlowchartSubgraphBoundsBuilder.PADDING_PX * 2.0)
                + FlowchartSubgraphBoundsBuilder.TITLE_BAND_PX
                + FlowchartSubgraphBoundsBuilder.VERTICAL_CONTENT_SLACK_PX;

            double ContentCenterX = UsesPrimaryColumnCentering
                ? PrimaryColumnCenterX
                : (MinX + MaxX) / 2.0;
            double ContentCenterY = UsesPrimaryRowCentering
                ? PrimaryRowCenterY
                : (MinY + MaxY) / 2.0;
            double ClusterLeft = ContentCenterX - (ClusterWidth / 2.0);
            double ClusterBottom = ContentCenterY - (ClusterHeight / 2.0);

            double InteriorWidth = ClusterWidth - (FlowchartSubgraphBoundsBuilder.PADDING_PX * 2.0);
            double InteriorHeight = ClusterHeight - (FlowchartSubgraphBoundsBuilder.PADDING_PX * 2.0);
            double ExtraX = UsesPrimaryColumnCentering
                ? 0.0
                : (InteriorWidth - ContentWidth) / 2.0;
            double ExtraY = UsesPrimaryRowCentering
                ? 0.0
                : (InteriorHeight - ContentHeight) / 2.0;

            double TargetMinX = ClusterLeft + FlowchartSubgraphBoundsBuilder.PADDING_PX + ExtraX;
            double TargetMinY = ClusterBottom + FlowchartSubgraphBoundsBuilder.PADDING_PX + ExtraY;

            double ShiftX = UsesPrimaryColumnCentering ? 0.0 : TargetMinX - MinX;
            double ShiftY = UsesPrimaryRowCentering ? 0.0 : TargetMinY - MinY;

            if (Math.Abs(ShiftX) < 0.5 && Math.Abs(ShiftY) < 0.5)
            {
                continue;
            }

            ShiftGroupMembersByOffset(Group, GroupById, Contract, MutableById, ShiftX, ShiftY);
        }
    }

    private static bool TryGetGroupMemberContentBounds(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        FlowchartRenderOptions Options,
        out double MinX,
        out double MinY,
        out double MaxX,
        out double MaxY)
    {
        _ = GroupById;
        return FlowchartSubgraphBoundsBuilder.TryComputeMemberContentBounds(
            Contract,
            Group,
            MutableById,
            Options,
            out MinX,
            out MinY,
            out MaxX,
            out MaxY);
    }

    private static void ShiftGroupMembersByOffset(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        double ShiftX,
        double ShiftY)
    {
        HashSet<string> MemberNodeIds = CollectGroupMemberNodeIds(Group, GroupById, Contract);
        foreach (string NodeId in MemberNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
            {
                continue;
            }

            MutableById[NodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX + ShiftX,
                CenterY = Bounds.CenterY + ShiftY,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }
    }

    private static void EnforceSiblingSubgraphGaps(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        bool HorizontalFlow,
        bool ReverseFlow,
        FlowchartRenderOptions Options)
    {
        Dictionary<string, List<GroupContract>> SiblingsByParentId =
            new Dictionary<string, List<GroupContract>>(StringComparer.Ordinal);

        foreach (GroupContract Group in Contract.Groups)
        {
            string ParentKey = Group.ParentGroupId ?? string.Empty;
            if (!SiblingsByParentId.TryGetValue(ParentKey, out List<GroupContract>? Siblings))
            {
                Siblings = new List<GroupContract>();
                SiblingsByParentId[ParentKey] = Siblings;
            }

            Siblings.Add(Group);
        }

        double MinimumGap = Math.Max(SUBGRAPH_EXTERNAL_GAP_PX, Options.NodeSpacingPx);

        foreach (KeyValuePair<string, List<GroupContract>> Entry in SiblingsByParentId)
        {
            if (Entry.Value.Count < 2)
            {
                continue;
            }

            List<GroupContract> OrderedGroups = Entry.Value
                .OrderBy(Group => GetGroupFlowLeadingEdge(Group, GroupById, Contract, MutableById, HorizontalFlow))
                .ToList();

            double PreviousTrailingEdge = double.NaN;
            foreach (GroupContract Group in OrderedGroups)
            {
                (double LeadingEdge, double TrailingEdge) = GetGroupFlowSpan(
                    Group,
                    GroupById,
                    Contract,
                    MutableById,
                    HorizontalFlow);

                if (!double.IsNaN(PreviousTrailingEdge))
                {
                    double RequiredLeadingEdge = ReverseFlow
                        ? PreviousTrailingEdge - MinimumGap
                        : PreviousTrailingEdge + MinimumGap;
                    double Shift = ReverseFlow
                        ? RequiredLeadingEdge - TrailingEdge
                        : RequiredLeadingEdge - LeadingEdge;

                    if (ReverseFlow)
                    {
                        if (Shift < -0.5)
                        {
                            ShiftGroupMembers(Group, GroupById, Contract, MutableById, Shift, HorizontalFlow);
                            TrailingEdge += Shift;
                            LeadingEdge += Shift;
                        }
                    }
                    else if (Shift > 0.5)
                    {
                        ShiftGroupMembers(Group, GroupById, Contract, MutableById, Shift, HorizontalFlow);
                        TrailingEdge += Shift;
                        LeadingEdge += Shift;
                    }
                }

                PreviousTrailingEdge = TrailingEdge;
            }
        }
    }

    private static void EnforceExternalSubgraphGaps(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        Dictionary<string, GroupContract> GroupById,
        bool HorizontalFlow,
        bool ReverseFlow,
        FlowchartRenderOptions Options)
    {
        Dictionary<string, string?> GroupIdByNodeId = BuildGroupIdByNodeId(Contract, GroupById);
        double MinimumGap = Math.Max(SUBGRAPH_EXTERNAL_GAP_PX, Options.NodeSpacingPx);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GroupIdByNodeId.TryGetValue(Edge.SourceId, out string? SourceGroupId);
            GroupIdByNodeId.TryGetValue(Edge.TargetId, out string? TargetGroupId);

            if (SourceGroupId is null && TargetGroupId is not null)
            {
                string? BoundaryGroupId = ResolveTopLevelBoundaryGroupId(TargetGroupId, GroupById);
                if (BoundaryGroupId is not null
                    && GroupById.TryGetValue(BoundaryGroupId, out GroupContract? BoundaryGroup))
                {
                    ShiftExternalNodeForIngressGap(
                        Edge.SourceId,
                        BoundaryGroup,
                        GroupById,
                        Contract,
                        MutableById,
                        HorizontalFlow,
                        ReverseFlow,
                        MinimumGap);
                }
            }

            if (SourceGroupId is not null && TargetGroupId is null)
            {
                string? BoundaryGroupId = ResolveTopLevelBoundaryGroupId(SourceGroupId, GroupById);
                if (BoundaryGroupId is not null
                    && GroupById.TryGetValue(BoundaryGroupId, out GroupContract? BoundaryGroup))
                {
                    ShiftExternalNodeForEgressGap(
                        Edge.TargetId,
                        BoundaryGroup,
                        GroupById,
                        Contract,
                        MutableById,
                        HorizontalFlow,
                        ReverseFlow,
                        MinimumGap);
                }
            }
        }
    }

    private static string? ResolveTopLevelBoundaryGroupId(
        string MemberGroupId,
        Dictionary<string, GroupContract> GroupById)
    {
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

    private static void ShiftExternalNodeForIngressGap(
        string ExternalNodeId,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        bool HorizontalFlow,
        bool ReverseFlow,
        double MinimumGap)
    {
        if (!MutableById.TryGetValue(ExternalNodeId, out FlowchartLayoutNodeBounds ExternalBounds))
        {
            return;
        }

        (double LeadingEdge, double TrailingEdge) = GetGroupFlowSpan(
            Group,
            GroupById,
            Contract,
            MutableById,
            HorizontalFlow);
        double Shift = 0.0;

        if (HorizontalFlow)
        {
            if (!ReverseFlow)
            {
                double ExternalTrailing = ExternalBounds.CenterX + (ExternalBounds.Width / 2.0);
                double RequiredTrailing = LeadingEdge - MinimumGap;
                Shift = RequiredTrailing - ExternalTrailing;
                if (Shift > 0.0)
                {
                    return;
                }
            }
            else
            {
                double ExternalLeading = ExternalBounds.CenterX - (ExternalBounds.Width / 2.0);
                double RequiredLeading = TrailingEdge + MinimumGap;
                Shift = RequiredLeading - ExternalLeading;
                if (Shift < 0.0)
                {
                    return;
                }
            }
        }
        else
        {
            if (!ReverseFlow)
            {
                double ExternalBottom = ExternalBounds.CenterY - (ExternalBounds.Height / 2.0);
                double RequiredBottom = TrailingEdge + MinimumGap;
                Shift = RequiredBottom - ExternalBottom;
                if (Shift < 0.0)
                {
                    return;
                }
            }
            else
            {
                double ExternalTop = ExternalBounds.CenterY + (ExternalBounds.Height / 2.0);
                double RequiredTop = LeadingEdge - MinimumGap;
                Shift = RequiredTop - ExternalTop;
                if (Shift > 0.0)
                {
                    return;
                }
            }
        }

        if (Math.Abs(Shift) <= 0.5)
        {
            return;
        }

        MutableById[ExternalNodeId] = ShiftBoundsOnFlowAxis(ExternalBounds, Shift, HorizontalFlow);
    }

    private static void ShiftExternalNodeForEgressGap(
        string ExternalNodeId,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        bool HorizontalFlow,
        bool ReverseFlow,
        double MinimumGap)
    {
        if (!MutableById.TryGetValue(ExternalNodeId, out FlowchartLayoutNodeBounds ExternalBounds))
        {
            return;
        }

        (double LeadingEdge, double TrailingEdge) = GetGroupFlowSpan(
            Group,
            GroupById,
            Contract,
            MutableById,
            HorizontalFlow);
        double Shift = 0.0;

        if (HorizontalFlow)
        {
            if (!ReverseFlow)
            {
                double ExternalLeading = ExternalBounds.CenterX - (ExternalBounds.Width / 2.0);
                double RequiredLeading = TrailingEdge + MinimumGap;
                Shift = RequiredLeading - ExternalLeading;
                if (Shift < 0.0)
                {
                    return;
                }
            }
            else
            {
                double ExternalTrailing = ExternalBounds.CenterX + (ExternalBounds.Width / 2.0);
                double RequiredTrailing = LeadingEdge - MinimumGap;
                Shift = RequiredTrailing - ExternalTrailing;
                if (Shift > 0.0)
                {
                    return;
                }
            }
        }
        else
        {
            if (!ReverseFlow)
            {
                double ExternalTop = ExternalBounds.CenterY + (ExternalBounds.Height / 2.0);
                double RequiredTop = LeadingEdge - MinimumGap;
                Shift = RequiredTop - ExternalTop;
                if (Shift > 0.0)
                {
                    return;
                }
            }
            else
            {
                double ExternalBottom = ExternalBounds.CenterY - (ExternalBounds.Height / 2.0);
                double RequiredBottom = TrailingEdge + MinimumGap;
                Shift = RequiredBottom - ExternalBottom;
                if (Shift < 0.0)
                {
                    return;
                }
            }
        }

        if (Math.Abs(Shift) <= 0.5)
        {
            return;
        }

        MutableById[ExternalNodeId] = ShiftBoundsOnFlowAxis(ExternalBounds, Shift, HorizontalFlow);
    }

    private static void ShiftGroupMembers(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        double FlowShift,
        bool HorizontalFlow)
    {
        HashSet<string> MemberNodeIds = CollectGroupMemberNodeIds(Group, GroupById, Contract);
        foreach (string NodeId in MemberNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            MutableById[NodeId] = ShiftBoundsOnFlowAxis(Bounds, FlowShift, HorizontalFlow);
        }

        foreach (GroupContract CandidateGroup in GroupById.Values)
        {
            if (!string.Equals(CandidateGroup.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            ShiftGroupMembers(CandidateGroup, GroupById, Contract, MutableById, FlowShift, HorizontalFlow);
        }
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

    private static bool TryGetGroupCrossCenter(
        string GroupId,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        bool HorizontalFlow,
        out double CrossCenter)
    {
        CrossCenter = 0.0;
        if (!GroupById.TryGetValue(GroupId, out GroupContract? Group))
        {
            return false;
        }

        HashSet<string> MemberNodeIds = CollectGroupMemberNodeIds(Group, GroupById, Contract);
        bool HasAny = false;
        double MinCross = 0.0;
        double MaxCross = 0.0;

        foreach (string NodeId in MemberNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double Cross = HorizontalFlow ? Bounds.CenterY : Bounds.CenterX;
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

    private static (double LeadingEdge, double TrailingEdge) GetGroupFlowSpan(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        bool HorizontalFlow)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> ReadOnlyBounds = MutableById;
        List<FlowchartSubgraphLayoutRect> Rects = FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, ReadOnlyBounds);
        FlowchartSubgraphLayoutRect? Rect = Rects.Find(
            Candidate => string.Equals(Candidate.GroupId, Group.Id, StringComparison.Ordinal));
        if (Rect is null)
        {
            return (0.0, 0.0);
        }

        if (HorizontalFlow)
        {
            return (Rect.Left, Rect.Right);
        }

        return (Rect.Bottom, Rect.Top);
    }

    private static double GetGroupFlowLeadingEdge(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        bool HorizontalFlow)
    {
        (double LeadingEdge, _) = GetGroupFlowSpan(Group, GroupById, Contract, MutableById, HorizontalFlow);
        return LeadingEdge;
    }

    private static FlowchartLayoutNodeBounds ShiftBoundsOnFlowAxis(
        FlowchartLayoutNodeBounds Bounds,
        double Shift,
        bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX + Shift,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }

        return new FlowchartLayoutNodeBounds
        {
            NodeId = Bounds.NodeId,
            CenterX = Bounds.CenterX,
            CenterY = Bounds.CenterY + Shift,
            Width = Bounds.Width,
            Height = Bounds.Height
        };
    }

    private static Dictionary<string, string?> BuildGroupIdByNodeId(
        DiagramContract Contract,
        Dictionary<string, GroupContract> GroupById)
    {
        Dictionary<string, string?> GroupIdByNodeId =
            new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (NodeContract Node in Contract.Nodes)
        {
            GroupIdByNodeId[Node.Id] = Node.ParentGroupId;
        }

        foreach (GroupContract Group in GroupById.Values)
        {
            foreach (string NodeId in Group.ChildNodeIds)
            {
                GroupIdByNodeId[NodeId] = Group.Id;
            }
        }

        return GroupIdByNodeId;
    }
}
