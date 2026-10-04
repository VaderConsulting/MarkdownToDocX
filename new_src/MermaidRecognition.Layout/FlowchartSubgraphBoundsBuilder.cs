using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Computes subgraph cluster bounds from laid-out member nodes (Mermaid parity).
/// </summary>
public static class FlowchartSubgraphBoundsBuilder
{
    /// <summary>
    /// Minimum inset between member node bodies and the cluster border on the left, right, and bottom.
    /// </summary>
    public const double PADDING_PX = 18.0;

    /// <summary>
    /// Reserved band at the top of each cluster for the subgraph title label.
    /// </summary>
    public const double TITLE_BAND_PX = 26.0;

    /// <summary>
    /// Extra vertical space below member content (balances the title band; Naiad/Mermaid parity).
    /// </summary>
    public const double VERTICAL_CONTENT_SLACK_PX = 26.0;

    /// <summary>
    /// Minimum exterior gap between a nested child subgraph cluster border and its parent cluster border.
    /// </summary>
    public const double NESTED_CLUSTER_GAP_PX = 24.0;

    /// <summary>
    /// Builds layout-space cluster rectangles for all groups, innermost first.
    /// </summary>
    public static List<FlowchartSubgraphLayoutRect> BuildOrderedRects(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById) => BuildOrderedRects(Contract, BoundsById, FlowchartSubgraphBoundsRequest.LegacyDefault);

    /// <summary>
    /// Builds layout-space cluster rectangles for all groups, innermost first.
    /// </summary>
    public static List<FlowchartSubgraphLayoutRect> BuildOrderedRects(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSubgraphBoundsRequest Request)
    {
        Dictionary<string, GroupContract> GroupById =
            new Dictionary<string, GroupContract>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupById[Group.Id] = Group;
        }

        List<FlowchartSubgraphLayoutRect> Ordered = [];
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            if (Group.ParentGroupId is not null)
            {
                continue;
            }

            CollectGroupRectsRecursive(Contract, Group, GroupById, BoundsById, Ordered, Visited, Request);
        }

        return Ordered;
    }

    /// <summary>
    /// Expands layout extents to include cluster padding and title bands.
    /// </summary>
    public static void ExpandLayoutExtents(
        IEnumerable<FlowchartSubgraphLayoutRect> SubgraphRects,
        ref double MinLeft,
        ref double MinBottom,
        ref double MaxRight,
        ref double MaxTop)
    {
        foreach (FlowchartSubgraphLayoutRect Rect in SubgraphRects)
        {
            if (Rect.Left < MinLeft)
            {
                MinLeft = Rect.Left;
            }

            if (Rect.Bottom < MinBottom)
            {
                MinBottom = Rect.Bottom;
            }

            if (Rect.Right > MaxRight)
            {
                MaxRight = Rect.Right;
            }

            if (Rect.Top > MaxTop)
            {
                MaxTop = Rect.Top;
            }
        }
    }

    private static void CollectGroupRectsRecursive(
        DiagramContract Contract,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<FlowchartSubgraphLayoutRect> Ordered,
        HashSet<string> Visited,
        FlowchartSubgraphBoundsRequest Request)
    {
        if (!Visited.Add(Group.Id))
        {
            return;
        }

        foreach (string ChildGroupId in Group.ChildGroupIds)
        {
            if (GroupById.TryGetValue(ChildGroupId, out GroupContract? ChildGroup))
            {
                CollectGroupRectsRecursive(Contract, ChildGroup, GroupById, BoundsById, Ordered, Visited, Request);
            }
        }

        FlowchartSubgraphLayoutRect? Rect = TryComputeGroupRect(Contract, Group, GroupById, BoundsById, Ordered, Request);
        if (Rect is not null)
        {
            Ordered.Add(Rect);
        }
    }

    /// <summary>
    /// Computes the tight content bounds for a group, including nested child cluster boxes.
    /// </summary>
    public static bool TryComputeMemberContentBounds(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        out double MinX,
        out double MinY,
        out double MaxX,
        out double MaxY)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        return TryComputeMemberContentBounds(Contract, Group, BoundsById, Options, out MinX, out MinY, out MaxX, out MaxY);
    }

    /// <summary>
    /// Computes the tight content bounds for a group using configured nested cluster gap thresholds.
    /// </summary>
    public static bool TryComputeMemberContentBounds(
        DiagramContract Contract,
        GroupContract Group,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartRenderOptions Options,
        out double MinX,
        out double MinY,
        out double MaxX,
        out double MaxY)
    {
        MinX = 0.0;
        MinY = 0.0;
        MaxX = 0.0;
        MaxY = 0.0;

        Dictionary<string, GroupContract> GroupById =
            new Dictionary<string, GroupContract>(StringComparer.Ordinal);
        foreach (GroupContract CandidateGroup in Contract.Groups)
        {
            GroupById[CandidateGroup.Id] = CandidateGroup;
        }

        FlowchartSubgraphBoundsRequest Request = FlowchartSubgraphBoundsRequest.FromContract(Contract, Options);
        List<FlowchartSubgraphLayoutRect> OrderedRects = BuildOrderedRects(Contract, BoundsById, Request);
        return TryComputeMemberContentBounds(
            Contract,
            Group,
            GroupById,
            BoundsById,
            OrderedRects,
            Request,
            out MinX,
            out MinY,
            out MaxX,
            out MaxY);
    }

    private static bool TryComputeMemberContentBounds(
        DiagramContract Contract,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<FlowchartSubgraphLayoutRect> ComputedRects,
        FlowchartSubgraphBoundsRequest Request,
        out double MinX,
        out double MinY,
        out double MaxX,
        out double MaxY)
    {
        MinX = 0.0;
        MinY = 0.0;
        MaxX = 0.0;
        MaxY = 0.0;
        bool HasAny = false;

        HashSet<string> NestedGroupNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract CandidateGroup in GroupById.Values)
        {
            if (!string.Equals(CandidateGroup.ParentGroupId, Group.Id, StringComparison.Ordinal))
            {
                continue;
            }

            FlowchartSubgraphLayoutRect? NestedRect = ComputedRects.Find(
                Candidate => string.Equals(Candidate.GroupId, CandidateGroup.Id, StringComparison.Ordinal));
            if (NestedRect is null)
            {
                continue;
            }

            IncludeNestedChildClusterBounds(
                NestedRect,
                Request,
                ref HasAny,
                ref MinX,
                ref MinY,
                ref MaxX,
                ref MaxY);

            foreach (string NodeId in CandidateGroup.ChildNodeIds)
            {
                NestedGroupNodeIds.Add(NodeId);
            }

            foreach (NodeContract Node in Contract.Nodes)
            {
                if (string.Equals(Node.ParentGroupId, CandidateGroup.Id, StringComparison.Ordinal))
                {
                    NestedGroupNodeIds.Add(Node.Id);
                }
            }
        }

        foreach (string NodeId in Group.ChildNodeIds)
        {
            if (NestedGroupNodeIds.Contains(NodeId))
            {
                continue;
            }

            if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds NodeBounds))
            {
                IncludeNodeBounds(NodeBounds, ref HasAny, ref MinX, ref MinY, ref MaxX, ref MaxY);
            }
        }

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

            if (BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds NodeBounds))
            {
                IncludeNodeBounds(NodeBounds, ref HasAny, ref MinX, ref MinY, ref MaxX, ref MaxY);
            }
        }

        if (HasAny)
        {
            FlowchartSubgraphSpineCenterHelper.ExpandHorizontalBoundsAroundPrimaryColumn(
                Contract,
                Group,
                BoundsById,
                ref MinX,
                ref MaxX);
            FlowchartSubgraphSpineCenterHelper.ExpandVerticalBoundsAroundPrimaryRow(
                Contract,
                Group,
                BoundsById,
                ref MinY,
                ref MaxY);
        }

        return HasAny;
    }

    private static FlowchartSubgraphLayoutRect? TryComputeGroupRect(
        DiagramContract Contract,
        GroupContract Group,
        Dictionary<string, GroupContract> GroupById,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<FlowchartSubgraphLayoutRect> ComputedRects,
        FlowchartSubgraphBoundsRequest Request)
    {
        if (!TryComputeMemberContentBounds(
                Contract,
                Group,
                GroupById,
                BoundsById,
                ComputedRects,
                Request,
                out double MinLeft,
                out double MinBottom,
                out double MaxRight,
                out double MaxTop))
        {
            return null;
        }

        FlowchartSubgraphLayoutRect Result = new FlowchartSubgraphLayoutRect
        {
            GroupId = Group.Id,
            Left = MinLeft - PADDING_PX,
            Bottom = MinBottom - PADDING_PX - VERTICAL_CONTENT_SLACK_PX,
            Right = MaxRight + PADDING_PX,
            Top = MaxTop + PADDING_PX + TITLE_BAND_PX,
            Title = string.IsNullOrWhiteSpace(Group.Title) ? Group.Id : Group.Title
        };
        return Result;
    }

    private static void IncludeNestedChildClusterBounds(
        FlowchartSubgraphLayoutRect NestedRect,
        FlowchartSubgraphBoundsRequest Request,
        ref bool HasAny,
        ref double MinX,
        ref double MinY,
        ref double MaxX,
        ref double MaxY)
    {
        double CrossAxisGap = Request.NestedCrossAxisGapPx;
        double FlowAxisGap = Request.NestedFlowAxisGapPx;
        if (Request.HorizontalFlow)
        {
            IncludeBounds(
                NestedRect.Left - FlowAxisGap + PADDING_PX,
                NestedRect.Bottom - CrossAxisGap + PADDING_PX + VERTICAL_CONTENT_SLACK_PX,
                NestedRect.Right + FlowAxisGap - PADDING_PX,
                NestedRect.Top + CrossAxisGap - TITLE_BAND_PX - PADDING_PX,
                ref HasAny,
                ref MinX,
                ref MinY,
                ref MaxX,
                ref MaxY);
            return;
        }

        IncludeBounds(
            NestedRect.Left - CrossAxisGap + PADDING_PX,
            NestedRect.Bottom - FlowAxisGap + PADDING_PX + VERTICAL_CONTENT_SLACK_PX,
            NestedRect.Right + CrossAxisGap - PADDING_PX,
            NestedRect.Top + FlowAxisGap - TITLE_BAND_PX - PADDING_PX,
            ref HasAny,
            ref MinX,
            ref MinY,
            ref MaxX,
            ref MaxY);
    }

    private static void IncludeNodeBounds(
        FlowchartLayoutNodeBounds NodeBounds,
        ref bool HasAny,
        ref double MinX,
        ref double MinY,
        ref double MaxX,
        ref double MaxY) => IncludeBounds(
            NodeBounds.CenterX - (NodeBounds.Width / 2.0),
            NodeBounds.CenterY - (NodeBounds.Height / 2.0),
            NodeBounds.CenterX + (NodeBounds.Width / 2.0),
            NodeBounds.CenterY + (NodeBounds.Height / 2.0),
            ref HasAny,
            ref MinX,
            ref MinY,
            ref MaxX,
            ref MaxY);

    private static void IncludeBounds(
        double Left,
        double Bottom,
        double Right,
        double Top,
        ref bool HasAny,
        ref double MinX,
        ref double MinY,
        ref double MaxX,
        ref double MaxY)
    {
        if (!HasAny)
        {
            HasAny = true;
            MinX = Left;
            MinY = Bottom;
            MaxX = Right;
            MaxY = Top;
            return;
        }

        if (Left < MinX)
        {
            MinX = Left;
        }

        if (Bottom < MinY)
        {
            MinY = Bottom;
        }

        if (Right > MaxX)
        {
            MaxX = Right;
        }

        if (Top > MaxY)
        {
            MaxY = Top;
        }
    }
}
