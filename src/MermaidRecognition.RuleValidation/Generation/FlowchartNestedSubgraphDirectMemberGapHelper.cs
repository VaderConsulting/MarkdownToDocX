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
/// Validates minimum spacing between direct member nodes and nested child subgraph cluster boxes.
/// </summary>
public static class FlowchartNestedSubgraphDirectMemberGapHelper
{
    private const double GAP_TOLERANCE_PX = 0.5;

    /// <summary>
    /// Records direct-member versus nested-cluster gap facts on parent groups and diagram layout.
    /// </summary>
    public static void ApplyDirectMemberNestedGapFacts(
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
            Group.Facts["Layout.DirectMemberNestedGapValid"] = true;
            Group.Facts["HasDirectMemberWithNestedChildSubgraph"] = false;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSubgraphBoundsRequest BoundsRequest = FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        Dictionary<string, GroupContract> ContractGroupById = Contract.Groups
            .ToDictionary(Group => Group.Id, Group => Group, StringComparer.Ordinal);

        double MinimumGap = FlowchartSubgraphGapThresholds.ResolveDirectMemberNestedGapPx(Options);
        bool AnyViolation = false;

        foreach (GroupContract Group in Contract.Groups)
        {
            if (Group.ChildGroupIds.Count == 0)
            {
                continue;
            }

            List<string> DirectMemberNodeIds = CollectDirectMemberNodeIds(Group, ContractGroupById, Contract);
            if (DirectMemberNodeIds.Count == 0)
            {
                continue;
            }

            HashSet<string> NestedDescendantNodeIds = CollectNestedDescendantNodeIds(Group, ContractGroupById, Contract);
            if (NestedDescendantNodeIds.Count == 0)
            {
                continue;
            }

            if (!GroupById.TryGetValue(Group.Id, out GroupActualResult? ParentResult))
            {
                continue;
            }

            ParentResult.Facts["HasDirectMemberWithNestedChildSubgraph"] = true;

            List<string> FlowOrderedMemberNodeIds = OrderNodesByInternalFlow(
                Contract,
                [.. NestedDescendantNodeIds.Union(DirectMemberNodeIds)]);
            if (FlowOrderedMemberNodeIds.Count == 0)
            {
                FlowOrderedMemberNodeIds = [.. DirectMemberNodeIds
                    .Concat(NestedDescendantNodeIds)
                    .Distinct(StringComparer.Ordinal)];
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

            List<string> UpstreamDirectNodeIds = [];
            List<string> DownstreamDirectNodeIds = [];
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

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                if (!RectByGroupId.TryGetValue(ChildGroupId, out FlowchartSubgraphLayoutRect? ChildRect))
                {
                    continue;
                }

                if (UpstreamDirectNodeIds.Count > 0
                    && !DirectMembersRespectNestedClusterLeadingEdge(
                        UpstreamDirectNodeIds,
                        ChildRect,
                        BoundsById,
                        HorizontalFlow,
                        ReverseFlow,
                        MinimumGap,
                        UpstreamClusterSide: true))
                {
                    AnyViolation = true;
                    ParentResult.Facts["Layout.DirectMemberNestedGapValid"] = false;
                }

                if (DownstreamDirectNodeIds.Count > 0
                    && !DirectMembersRespectNestedClusterLeadingEdge(
                        DownstreamDirectNodeIds,
                        ChildRect,
                        BoundsById,
                        HorizontalFlow,
                        ReverseFlow,
                        MinimumGap,
                        UpstreamClusterSide: false))
                {
                    AnyViolation = true;
                    ParentResult.Facts["Layout.DirectMemberNestedGapValid"] = false;
                }
            }
        }

        Diagram.Layout["DirectMembersRespectNestedSubgraphSeparation"] = !AnyViolation;
    }

    private static bool DirectMembersRespectNestedClusterLeadingEdge(
        List<string> DirectMemberNodeIds,
        FlowchartSubgraphLayoutRect NestedClusterRect,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow,
        bool ReverseFlow,
        double MinimumGap,
        bool UpstreamClusterSide)
    {
        if (DirectMemberNodeIds.Count == 0)
        {
            return true;
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
                    if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
                    {
                        continue;
                    }

                    MaxDirectTrailingEdge = Math.Max(
                        MaxDirectTrailingEdge,
                        Bounds.CenterX + (Bounds.Width / 2.0));
                }

                return MaxDirectTrailingEdge <= RequiredTrailingEdge + GAP_TOLERANCE_PX;
            }

            double ClusterTrailingEdge = NestedClusterRect.Right;
            double RequiredLeadingEdge = ClusterTrailingEdge + MinimumGap;
            double MinDirectLeadingEdge = double.MaxValue;
            foreach (string NodeId in DirectMemberNodeIds)
            {
                if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
                {
                    continue;
                }

                MinDirectLeadingEdge = Math.Min(
                    MinDirectLeadingEdge,
                    Bounds.CenterX - (Bounds.Width / 2.0));
            }

            return MinDirectLeadingEdge >= RequiredLeadingEdge - GAP_TOLERANCE_PX;
        }

        if (UpstreamClusterSide ^ ReverseFlow)
        {
            double ClusterLeadingEdge = NestedClusterRect.Top;
            double RequiredTrailingEdge = ClusterLeadingEdge + MinimumGap;
            double MinDirectTrailingEdge = double.MaxValue;
            foreach (string NodeId in DirectMemberNodeIds)
            {
                if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
                {
                    continue;
                }

                MinDirectTrailingEdge = Math.Min(
                    MinDirectTrailingEdge,
                    Bounds.CenterY - (Bounds.Height / 2.0));
            }

            return MinDirectTrailingEdge >= RequiredTrailingEdge - GAP_TOLERANCE_PX;
        }

        double ClusterBottomEdge = NestedClusterRect.Bottom;
        double RequiredTopEdge = ClusterBottomEdge - MinimumGap;
        double MaxDirectTopEdge = double.MinValue;
        foreach (string NodeId in DirectMemberNodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
            {
                continue;
            }

            MaxDirectTopEdge = Math.Max(
                MaxDirectTopEdge,
                Bounds.CenterY + (Bounds.Height / 2.0));
        }

        return MaxDirectTopEdge <= RequiredTopEdge + GAP_TOLERANCE_PX;
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

            NestedDescendantNodeIds.UnionWith(CollectGroupMemberNodeIds(CandidateGroup, GroupById, Contract));
        }

        return NestedDescendantNodeIds;
    }

    private static List<string> CollectDirectMemberNodeIds(
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

        List<string> DirectMemberNodeIds = [];
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

    private static HashSet<string> CollectGroupMemberNodeIds(
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        DiagramContract Contract)
    {
        HashSet<string> MemberNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string ChildNodeId in Group.ChildNodeIds)
        {
            MemberNodeIds.Add(ChildNodeId);
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                MemberNodeIds.Add(Node.Id);
            }
        }

        foreach (string ChildGroupId in Group.ChildGroupIds)
        {
            if (GroupById.TryGetValue(ChildGroupId, out GroupContract? ChildGroup))
            {
                MemberNodeIds.UnionWith(CollectGroupMemberNodeIds(ChildGroup, GroupById, Contract));
            }
        }

        return MemberNodeIds;
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

        List<string> Ordered = [];
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
        for (int Index = 0; Index < Contract.Edges.Count; Index++)
        {
            EdgeContract Edge = Contract.Edges[Index];
            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                || string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                if (Index < Earliest)
                {
                    Earliest = Index;
                }
            }
        }

        return Earliest;
    }
}
