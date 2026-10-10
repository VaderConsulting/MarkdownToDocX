using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Routes edges whose endpoint is a subgraph or compound to the facing cluster boundary.
/// </summary>
public static class FlowchartCompoundEndpointHelper
{
    internal static bool Contains(DiagramContract Contract, string GroupId, string ChildId)
    {
        string? Parent = null;
        foreach (var Node in Contract.Nodes)
            if (Node.Id == ChildId) { Parent = Node.ParentGroupId; break; }
        foreach (var Group in Contract.Groups)
            if (Group.Id == ChildId) { Parent = Group.ParentGroupId; break; }
        var Seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(Parent) && Seen.Add(Parent))
        {
            if (Parent == GroupId) return true;
            string? Next = null;
            foreach (var Group in Contract.Groups)
                if (Group.Id == Parent) { Next = Group.ParentGroupId; break; }
            Parent = Next;
        }
        return false;
    }

    internal static int InternalBoundaryPadding(DiagramContract Contract, string GroupId)
    {
        int Padding = 0;
        foreach (var Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)) continue;
            if ((Edge.SourceId == GroupId && Contains(Contract, GroupId, Edge.TargetId))
                || (Edge.TargetId == GroupId && Contains(Contract, GroupId, Edge.SourceId)))
            {
                var Label = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? "",
                    FlowchartRenderOptions.FromContract(Contract));
                Padding = Math.Max(Padding, DiagramGridMetrics.CeilToGrid(Label.Height + 70));
            }
        }
        return Padding;
    }

    /// <summary>
    /// True when <paramref name="Id"/> is a declared subgraph id.
    /// </summary>
    /// <param name="Contract">Flowchart contract.</param>
    /// <param name="Id">Node or group id.</param>
    /// <returns>True when the id is a group.</returns>
    public static bool IsGroupId(DiagramContract Contract, string Id)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds each subgraph rectangle to <paramref name="BoundsById"/> so edges can attach to the cluster.
    /// Existing member node bounds are left unchanged. Group-id dummy nodes are replaced by the cluster box.
    /// </summary>
    /// <param name="Contract">Flowchart contract.</param>
    /// <param name="BoundsById">Mutable layout bounds.</param>
    public static void AddGroupBounds(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (Contract.Groups.Count == 0)
        {
            return;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        List<FlowchartSubgraphLayoutRect> Rects = FlowchartSubgraphBoundsBuilder.BuildOrderedRects(
            Contract,
            BoundsById,
            FlowchartSubgraphBoundsRequest.FromContract(Contract, Options));
        foreach (FlowchartSubgraphLayoutRect Rect in Rects)
        {
            BoundsById[Rect.GroupId] = ToNodeBounds(Rect);
        }
    }

    /// <summary>
    /// Builds a facing-side path when either endpoint is a subgraph. The path is clipped to each
    /// rectangle and does not loop around the diagram.
    /// </summary>
    /// <param name="Edge">Edge.</param>
    /// <param name="Contract">Contract.</param>
    /// <param name="BoundsById">Layout bounds, including group rectangles.</param>
    /// <param name="NodeById">Nodes by id.</param>
    /// <param name="LayoutPoints">Resolved points.</param>
    /// <returns>True when this edge uses compound-boundary routing.</returns>
    public static bool TryBuildFacingBoundaryPath(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = new List<(double X, double Y)>();
        bool SourceIsGroup = IsGroupId(Contract, Edge.SourceId);
        bool TargetIsGroup = IsGroupId(Contract, Edge.TargetId);
        if (!SourceIsGroup && !TargetIsGroup)
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        string? SourceShape = SourceIsGroup
            ? "rect"
            : (NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : "rect");
        string? TargetShape = TargetIsGroup
            ? "rect"
            : (NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : "rect");

        (double StartX, double StartY) = FacingPort(SourceBounds, TargetBounds);
        (double EndX, double EndY) = FacingPort(TargetBounds, SourceBounds);

        if (SourceIsGroup)
        {
            (StartX, StartY) = FlowchartShapeLayoutHelper.RayExitShape(
                SourceBounds.CenterX,
                SourceBounds.CenterY,
                SourceBounds.Width,
                SourceBounds.Height,
                "rect",
                EndX - SourceBounds.CenterX,
                EndY - SourceBounds.CenterY);
        }
        else
        {
            (StartX, StartY) = FlowchartShapeLayoutHelper.RayExitShape(
                SourceBounds.CenterX,
                SourceBounds.CenterY,
                SourceBounds.Width,
                SourceBounds.Height,
                SourceShape,
                EndX - SourceBounds.CenterX,
                EndY - SourceBounds.CenterY);
        }

        if (TargetIsGroup)
        {
            (EndX, EndY) = FlowchartShapeLayoutHelper.RayExitShape(
                TargetBounds.CenterX,
                TargetBounds.CenterY,
                TargetBounds.Width,
                TargetBounds.Height,
                "rect",
                StartX - TargetBounds.CenterX,
                StartY - TargetBounds.CenterY);
        }
        else
        {
            (EndX, EndY) = FlowchartShapeLayoutHelper.RayExitShape(
                TargetBounds.CenterX,
                TargetBounds.CenterY,
                TargetBounds.Width,
                TargetBounds.Height,
                TargetShape,
                StartX - TargetBounds.CenterX,
                StartY - TargetBounds.CenterY);
        }

        LayoutPoints.Add((StartX, StartY));
        LayoutPoints.Add((EndX, EndY));
        return LayoutPoints.Count >= 2;
    }

    internal static FlowchartLayoutNodeBounds ToNodeBounds(FlowchartSubgraphLayoutRect Rect)
    {
        return new FlowchartLayoutNodeBounds
        {
            NodeId = Rect.GroupId,
            CenterX = (Rect.Left + Rect.Right) / 2.0,
            CenterY = (Rect.Bottom + Rect.Top) / 2.0,
            Width = Math.Max(1.0, Rect.Right - Rect.Left),
            Height = Math.Max(1.0, Rect.Top - Rect.Bottom)
        };
    }

    private static (double X, double Y) FacingPort(
        FlowchartLayoutNodeBounds Self,
        FlowchartLayoutNodeBounds Other)
    {
        double Dx = Other.CenterX - Self.CenterX;
        double Dy = Other.CenterY - Self.CenterY;
        double HalfWidth = Self.Width / 2.0;
        double HalfHeight = Self.Height / 2.0;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            double Sign = Dx >= 0.0 ? 1.0 : -1.0;
            return (Self.CenterX + (Sign * HalfWidth), Self.CenterY);
        }

        double VerticalSign = Dy >= 0.0 ? 1.0 : -1.0;
        return (Self.CenterX, Self.CenterY + (VerticalSign * HalfHeight));
    }
}
