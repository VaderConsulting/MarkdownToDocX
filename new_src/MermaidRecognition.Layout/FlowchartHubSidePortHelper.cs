using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Side-face ports for small top-to-bottom and bottom-to-top hubs (F31 fan-out, F32 fan-in).
/// A branch whose other node lies wholly beyond the hub's left or right edge joins the hub at the middle of that side.
/// The route is a horizontal stub at the hub, a vertical stub at the other node, and one smooth quarter curve between them.
/// All coordinates are layout space (Y up).
/// </summary>
public static class FlowchartHubSidePortHelper
{
    private const double SIDE_CLEARANCE_PX = 0.75;
    private const double OBSTACLE_MARGIN_PX = 4.0;

    /// <summary>
    /// Resolves the hub side-face attach point, but only when the quarter route to the other node passes no other node.
    /// The route runs horizontally from the hub side to the other node's column, then along that column to the other node's facing side.
    /// Dense layouts, such as a full bipartite join between two rows (F111), fail this check and keep their flow-face ports.
    /// </summary>
    /// <param name="HubBounds">The hub node (fan-out source or fan-in target).</param>
    /// <param name="OtherBounds">The node at the other end of the edge.</param>
    /// <param name="Direction">Diagram direction. Only top-to-bottom and bottom-to-top qualify.</param>
    /// <param name="HubMarker">The endpoint marker at the hub end, used for the cross-marker exterior gap.</param>
    /// <param name="AllBounds">Bounds for every node in the diagram, used as obstacles.</param>
    /// <param name="AttachX">Hub attach X.</param>
    /// <param name="AttachY">Hub attach Y.</param>
    /// <param name="OutwardX">Unit X of the outward direction at the hub attach (-1 or +1).</param>
    /// <returns>True when a side-face port applies and its route is clear.</returns>
    public static bool TryResolveClearHubSideAttach(
        FlowchartLayoutNodeBounds HubBounds,
        FlowchartLayoutNodeBounds OtherBounds,
        FlowchartDirection Direction,
        EdgeEndpointMarker HubMarker,
        IEnumerable<FlowchartLayoutNodeBounds> AllBounds,
        out double AttachX,
        out double AttachY,
        out double OutwardX)
    {
        if (!TryResolveHubSideAttach(HubBounds, OtherBounds, Direction, HubMarker, out AttachX, out AttachY, out OutwardX))
        {
            return false;
        }

        if (IsQuarterRouteBlocked(HubBounds, OtherBounds, AttachX, AttachY, AllBounds))
        {
            AttachX = 0.0;
            AttachY = 0.0;
            OutwardX = 0.0;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true when any node other than the hub and the other node overlaps the horizontal run along the hub's middle row
    /// or the vertical run along the other node's column.
    /// </summary>
    private static bool IsQuarterRouteBlocked(
        FlowchartLayoutNodeBounds HubBounds,
        FlowchartLayoutNodeBounds OtherBounds,
        double AttachX,
        double AttachY,
        IEnumerable<FlowchartLayoutNodeBounds> AllBounds)
    {
        double ColumnX = OtherBounds.CenterX;
        double RowMinX = Math.Min(AttachX, ColumnX);
        double RowMaxX = Math.Max(AttachX, ColumnX);
        double OtherHalfHeight = OtherBounds.Height / 2.0;
        double OtherFaceY = OtherBounds.CenterY < AttachY ? OtherBounds.CenterY + OtherHalfHeight : OtherBounds.CenterY - OtherHalfHeight;
        double ColumnMinY = Math.Min(OtherFaceY, AttachY);
        double ColumnMaxY = Math.Max(OtherFaceY, AttachY);

        foreach (FlowchartLayoutNodeBounds Obstacle in AllBounds)
        {
            if (ReferenceEquals(Obstacle, HubBounds) || ReferenceEquals(Obstacle, OtherBounds))
            {
                continue;
            }

            if (string.Equals(Obstacle.NodeId, HubBounds.NodeId, StringComparison.Ordinal) || string.Equals(Obstacle.NodeId, OtherBounds.NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double Left = Obstacle.CenterX - (Obstacle.Width / 2.0) - OBSTACLE_MARGIN_PX;
            double Right = Obstacle.CenterX + (Obstacle.Width / 2.0) + OBSTACLE_MARGIN_PX;
            double Bottom = Obstacle.CenterY - (Obstacle.Height / 2.0) - OBSTACLE_MARGIN_PX;
            double Top = Obstacle.CenterY + (Obstacle.Height / 2.0) + OBSTACLE_MARGIN_PX;

            bool BlocksRow = AttachY > Bottom && AttachY < Top && RowMaxX > Left && RowMinX < Right;
            bool BlocksColumn = ColumnX > Left && ColumnX < Right && ColumnMaxY > Bottom && ColumnMinY < Top;
            if (BlocksRow || BlocksColumn)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the hub side-face attach point when the other node sits beyond the hub's left or right edge.
    /// </summary>
    /// <param name="HubBounds">The hub node (fan-out source or fan-in target).</param>
    /// <param name="OtherBounds">The node at the other end of the edge.</param>
    /// <param name="Direction">Diagram direction. Only top-to-bottom and bottom-to-top qualify.</param>
    /// <param name="HubMarker">The endpoint marker at the hub end, used for the cross-marker exterior gap.</param>
    /// <param name="AttachX">Hub attach X.</param>
    /// <param name="AttachY">Hub attach Y.</param>
    /// <param name="OutwardX">Unit X of the outward direction at the hub attach (-1 or +1).</param>
    /// <returns>True when a side-face port applies.</returns>
    public static bool TryResolveHubSideAttach(
        FlowchartLayoutNodeBounds HubBounds,
        FlowchartLayoutNodeBounds OtherBounds,
        FlowchartDirection Direction,
        EdgeEndpointMarker HubMarker,
        out double AttachX,
        out double AttachY,
        out double OutwardX)
    {
        AttachX = 0.0;
        AttachY = 0.0;
        OutwardX = 0.0;

        if (Direction != FlowchartDirection.TopToBottom && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        double HubHalfWidth = HubBounds.Width / 2.0;
        double HubLeft = HubBounds.CenterX - HubHalfWidth;
        double HubRight = HubBounds.CenterX + HubHalfWidth;
        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(HubMarker);

        if (OtherBounds.CenterX < HubLeft - SIDE_CLEARANCE_PX)
        {
            AttachX = HubLeft - GapPx;
            AttachY = HubBounds.CenterY;
            OutwardX = -1.0;
            return true;
        }

        if (OtherBounds.CenterX > HubRight + SIDE_CLEARANCE_PX)
        {
            AttachX = HubRight + GapPx;
            AttachY = HubBounds.CenterY;
            OutwardX = 1.0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the centre attach on the face of <paramref name="OtherBounds"/> that faces the hub along the flow axis.
    /// </summary>
    public static (double X, double Y, double OutwardY) ResolveOtherNodeFlowFaceAttach(
        FlowchartLayoutNodeBounds OtherBounds,
        FlowchartLayoutNodeBounds HubBounds,
        EdgeEndpointMarker OtherMarker)
    {
        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(OtherMarker);
        double HalfHeight = OtherBounds.Height / 2.0;
        if (OtherBounds.CenterY < HubBounds.CenterY)
        {
            return (OtherBounds.CenterX, OtherBounds.CenterY + HalfHeight + GapPx, 1.0);
        }

        return (OtherBounds.CenterX, OtherBounds.CenterY - HalfHeight - GapPx, -1.0);
    }

    /// <summary>
    /// Appends boundary, stub, stub, boundary for a hub side-face route.
    /// </summary>
    /// <param name="Points">Receives the four guide points from edge source to edge target.</param>
    /// <param name="HubIsSource">True for fan-out (hub to other node), false for fan-in (other node to hub).</param>
    /// <param name="HubX">Hub side attach X.</param>
    /// <param name="HubY">Hub side attach Y.</param>
    /// <param name="HubOutwardX">Unit X pointing away from the hub side (-1 or +1).</param>
    /// <param name="HubBounds">The hub node, used for the hub stub length.</param>
    /// <param name="OtherX">Other node flow-face attach X.</param>
    /// <param name="OtherY">Other node flow-face attach Y.</param>
    /// <param name="OtherOutwardY">Unit Y pointing away from the other node's flow face (-1 or +1).</param>
    /// <param name="OtherBounds">The other node, used for its stub length.</param>
    public static void AppendQuarterRoute(
        List<(double X, double Y)> Points,
        bool HubIsSource,
        double HubX,
        double HubY,
        double HubOutwardX,
        FlowchartLayoutNodeBounds HubBounds,
        double OtherX,
        double OtherY,
        double OtherOutwardY,
        FlowchartLayoutNodeBounds OtherBounds)
    {
        double HubStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(HubBounds);
        double OtherStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(OtherBounds);
        (double X, double Y) HubBoundary = (HubX, HubY);
        (double X, double Y) HubStub = (HubX + (HubOutwardX * HubStubLength), HubY);
        (double X, double Y) OtherBoundary = (OtherX, OtherY);
        (double X, double Y) OtherStub = (OtherX, OtherY + (OtherOutwardY * OtherStubLength));

        if (HubIsSource)
        {
            Points.Add(HubBoundary);
            Points.Add(HubStub);
            Points.Add(OtherStub);
            Points.Add(OtherBoundary);
            return;
        }

        Points.Add(OtherBoundary);
        Points.Add(OtherStub);
        Points.Add(HubStub);
        Points.Add(HubBoundary);
    }
}
