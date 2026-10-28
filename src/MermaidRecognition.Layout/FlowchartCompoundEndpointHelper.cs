using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Routes edges whose endpoint is a subgraph or compound to the facing cluster boundary.
/// </summary>
public static class FlowchartCompoundEndpointHelper
{
    internal static void ReattachVisiblePorts(EdgeContract Edge, DiagramContract Contract,
        FlowchartLayoutResult Layout, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes, FlowchartDirection Direction,
        List<(double X, double Y)> Points, Dictionary<string, List<(double X, double Y)>> UsedPorts)
    {
        if (Points.Count < 2 || !Bounds.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || !Bounds.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds? Target))
        {
            return;
        }

        FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.FirstOrDefault(Route => Route.EdgeId == Edge.Id);
        if (Route == null)
        {
            return;
        }

        string? Exit = Route.ExitSide, Entry = Route.EntrySide;
        (double X, double Y) Start = Points[0]; (double X, double Y) End = Points[^1];
        bool Repair(string Id, FlowchartLayoutNodeBounds Box, (double X, double Y) Other,
            ref (double X, double Y) Port, ref string? Side)
        {
            if (!IsGroupId(Contract, Id))
            {
                return false;
            }

            if (!UsedPorts.TryGetValue(Id, out List<(double X, double Y)>? Ports))
            {
                UsedPorts[Id] = Ports = [];
            }

            double Left = Box.CenterX - (Box.Width / 2), Right = Box.CenterX + (Box.Width / 2);
            double Bottom = Box.CenterY - (Box.Height / 2), Top = Box.CenterY + (Box.Height / 2);
            if (Port.X >= Left - 0.5 && Port.X <= Right + 0.5 && Port.Y >= Bottom - 0.5 && Port.Y <= Top + 0.5
                && (Math.Abs(Port.X - Left) < 0.5 || Math.Abs(Port.X - Right) < 0.5
                    || Math.Abs(Port.Y - Top) < 0.5 || Math.Abs(Port.Y - Bottom) < 0.5))
            {
                Ports.Add(Port);
                return false;
            }
            // Recomputed visible frames can be smaller than the routing frames.
            // Choose a facing border, reserving the title band on vertical sides.
            if (Other.X < Left || Other.X > Right)
            {
                Side = Other.X < Left ? "left" : "right";
                Port = (Other.X < Left ? Left : Right,
                    Math.Clamp(
                        Port.Y,
                        Bottom + Math.Min(FlowchartGeometryPolicy.CompoundPortInsetPx, Box.Height / 4),
                        Top - Math.Min(FlowchartGeometryPolicy.CompoundTitleInsetPx, Box.Height / 2)));
            }
            else
            {
                Side = Other.Y > Top ? "top" : "bottom";
                Port = (Math.Clamp(
                        Port.X,
                        Left + Math.Min(FlowchartGeometryPolicy.CompoundPortInsetPx, Box.Width / 4),
                        Right - Math.Min(FlowchartGeometryPolicy.CompoundPortInsetPx, Box.Width / 4)),
                    Other.Y > Top ? Top : Bottom);
            }
            (double X, double Y) Preferred = Port;
            bool Vertical = Side is "left" or "right";
            double Low = Vertical
                ? Bottom + Math.Min(FlowchartGeometryPolicy.CompoundPortInsetPx, Box.Height / 4)
                : Left + Math.Min(FlowchartGeometryPolicy.CompoundPortInsetPx, Box.Width / 4);
            double High = Vertical
                ? Top - Math.Min(FlowchartGeometryPolicy.CompoundTitleInsetPx, Box.Height / 2)
                : Right - Math.Min(FlowchartGeometryPolicy.CompoundPortInsetPx, Box.Width / 4);
            for (int Step = 0; Step <= ((int)((High - Low) / FlowchartGeometryPolicy.CompoundPortSpacingPx) * 2) + 2; Step++)
            {
                double Offset = Step == 0
                    ? 0
                    : ((Step + 1) / 2) * FlowchartGeometryPolicy.CompoundPortSpacingPx * (Step % 2 == 1 ? -1 : 1);
                (double X, double Y) Candidate = Vertical ? (X: Preferred.X, Y: Preferred.Y + Offset) : (X: Preferred.X + Offset, Y: Preferred.Y);
                double Along = Vertical ? Candidate.Y : Candidate.X;
                if (Along < Low
                    || Along > High
                    || Ports.Any(Used => Math.Abs(Used.X - Candidate.X) + Math.Abs(Used.Y - Candidate.Y)
                        < FlowchartGeometryPolicy.CompoundPortSpacingPx))
                {
                    continue;
                }

                Port = Candidate;
                break;
            }
            Ports.Add(Port);
            return true;
        }
        bool Changed = Repair(Edge.SourceId, Source, End, ref Start, ref Exit);
        Changed |= Repair(Edge.TargetId, Target, Start, ref End, ref Entry);
        if (!Changed)
        {
            return;
        }

        Nodes.TryGetValue(Edge.SourceId, out NodeContract? SourceNode);
        Nodes.TryGetValue(Edge.TargetId, out NodeContract? TargetNode);
        List<(double X, double Y)> Rebuilt = DiagramGridRouteBuilder.BuildRoute(Start.X, Start.Y, Source, SourceNode?.Shape ?? "rect", Exit,
            End.X, End.Y, Target, TargetNode?.Shape ?? "rect", Entry, Bounds, Edge.SourceId, Edge.TargetId,
            Contract, Direction, Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft);
        Points.Clear();
        Points.AddRange(Rebuilt);
    }

    internal static bool Contains(DiagramContract Contract, string GroupId, string ChildId)
    {
        string? Parent = null;
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (Node.Id == ChildId) { Parent = Node.ParentGroupId; break; }
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (Group.Id == ChildId) { Parent = Group.ParentGroupId; break; }
        }

        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(Parent) && Seen.Add(Parent))
        {
            if (Parent == GroupId)
            {
                return true;
            }

            string? Next = null;
            foreach (GroupContract Group in Contract.Groups)
            {
                if (Group.Id == Parent) { Next = Group.ParentGroupId; break; }
            }

            Parent = Next;
        }
        return false;
    }

    internal static int InternalBoundaryPadding(DiagramContract Contract, string GroupId)
    {
        int Padding = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if ((Edge.SourceId == GroupId && Contains(Contract, GroupId, Edge.TargetId))
                || (Edge.TargetId == GroupId && Contains(Contract, GroupId, Edge.SourceId)))
            {
                FlowchartLabelDimensions Label = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? "",
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
        LayoutPoints = [];
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
