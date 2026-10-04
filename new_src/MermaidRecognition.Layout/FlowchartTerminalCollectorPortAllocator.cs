using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// TB/BT terminal collector routes for multi-branch joins into a spine-aligned sink (F58).
/// </summary>
public static class FlowchartTerminalCollectorPortAllocator
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double FAR_MARGIN_MIN_OFFSET_PX = 80.0;

    private const double SIDE_LEAD_PX = 28.0;

    private const double STADIUM_MERGE_APPROACH_X_BLEND = 0.2741;

    private const double STADIUM_MERGE_APPROACH_Y_FROM_TOP_RATIO = 0.1935;

    private const int MINIMUM_INCOMING_FOR_TERMINAL_COLLECTOR = 3;

    private enum FlowchartTerminalCollectorBranchRole
    {
        FarLeftMargin,

        FarRightAuxiliary,

        CentreLeft,

        CentreRight
    }

    /// <summary>
    /// Resolves margin or shoulder collector routes into a terminal sink with three or more incoming paths.
    /// </summary>
    public static bool TryResolveEdgeRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];

        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        if (IsDecisionShape(TargetShape))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (!ShouldApplyTerminalCollectorRouting(Contract, Edge.TargetId))
        {
            return false;
        }

        FlowchartTerminalCollectorBranchRole BranchRole = ResolveBranchRole(
            Contract,
            Edge,
            BoundsById,
            SourceBounds,
            TargetBounds,
            SourceShape,
            Direction,
            out int BranchIndex,
            out int BranchCount);

        bool BottomToTop = Direction == FlowchartDirection.BottomToTop;
        bool IsMarginBranch = BranchRole == FlowchartTerminalCollectorBranchRole.FarLeftMargin
            || BranchRole == FlowchartTerminalCollectorBranchRole.FarRightAuxiliary;
        if (!IsMarginBranch)
        {
            if (BottomToTop)
            {
                if (SourceBounds.CenterY >= TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
                {
                    return false;
                }
            }
            else if (SourceBounds.CenterY <= TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        (double SourceX, double SourceY) = ResolveSourceAttach(
            SourceBounds,
            TargetBounds,
            SourceShape,
            Direction,
            Edge.MarkerStart);
        if (IsMarginBranch
            && FlowchartShapeBoundaryAttachHelper.TryResolveOutboundAttach(
                SourceBounds,
                SourceShape,
                TargetBounds.CenterX,
                TargetBounds.CenterY,
                Edge.MarkerStart,
                out double MarginSourceX,
                out double MarginSourceY))
        {
            SourceX = MarginSourceX;
            SourceY = MarginSourceY;
        }

        ResolveTargetAttachAndApproach(
            BranchRole,
            SourceBounds,
            TargetBounds,
            TargetShape,
            Edge.MarkerEnd,
            BranchIndex,
            BranchCount,
            out double TargetX,
            out double TargetY,
            out double ApproachX,
            out double ApproachY);

        bool RouteBuilt = false;
        bool IsSpineStraightRoute = false;
        bool UseStadiumMergeRoute = IsStadiumLikeShape(TargetShape)
            && (BranchRole == FlowchartTerminalCollectorBranchRole.CentreLeft
                || BranchRole == FlowchartTerminalCollectorBranchRole.CentreRight);
        if (FlowchartShapeBoundaryAttachHelper.AreSpineColumnAligned(SourceBounds, TargetBounds)
            && FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds)
            && FlowchartShapeBoundaryAttachHelper.TryResolveOutboundAttach(
                SourceBounds,
                SourceShape,
                TargetBounds.CenterX,
                TargetBounds.CenterY,
                Edge.MarkerStart,
                out double SpineSourceX,
                out double SpineSourceY)
            && FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                TargetBounds,
                TargetShape,
                SourceBounds.CenterX,
                SourceBounds.CenterY,
                Edge.MarkerEnd,
                out double SpineTargetX,
                out double SpineTargetY))
        {
            LayoutPoints.Add((SpineSourceX, SpineSourceY));
            LayoutPoints.Add((SpineTargetX, SpineTargetY));
            RouteBuilt = true;
            IsSpineStraightRoute = true;
        }

        if (!RouteBuilt && BranchRole == FlowchartTerminalCollectorBranchRole.FarLeftMargin)
        {
            double TargetLeft = TargetBounds.CenterX - (TargetBounds.Width / 2.0);
            double CorridorX = Math.Min(SourceBounds.CenterX, TargetLeft) - FAR_MARGIN_MIN_OFFSET_PX;

            LayoutPoints.Add((SourceX, SourceY));
            LayoutPoints.Add((CorridorX, SourceY));
            LayoutPoints.Add((CorridorX, ApproachY));
            LayoutPoints.Add((TargetX, ApproachY));
            LayoutPoints.Add((TargetX, TargetY));
            RouteBuilt = true;
        }

        if (!RouteBuilt && BranchRole == FlowchartTerminalCollectorBranchRole.FarRightAuxiliary)
        {
            double TargetRight = TargetBounds.CenterX + (TargetBounds.Width / 2.0);
            double RouteHubCenterX = Math.Max(SourceBounds.CenterX, TargetRight) + FAR_MARGIN_MIN_OFFSET_PX;
            FlowchartPerpendicularRouteBuilder.AppendCurvedPerpendicularRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                RouteHubCenterX);
            RouteBuilt = true;
        }

        if (!RouteBuilt)
        {
            if (UseStadiumMergeRoute)
            {
                AppendStadiumMergeRoute(
                    LayoutPoints,
                    SourceX,
                    SourceY,
                    SourceBounds,
                    SourceShape,
                    TargetX,
                    TargetY,
                    TargetBounds,
                    TargetShape,
                    ApproachX,
                    ApproachY);
            }
            else
            {
                FlowchartPerpendicularRouteBuilder.AppendCurvedPerpendicularRoute(
                    LayoutPoints,
                    SourceX,
                    SourceY,
                    SourceBounds,
                    SourceShape,
                    TargetX,
                    TargetY,
                    TargetBounds,
                    TargetShape,
                    ApproachX);
            }
        }

        if (!IsSpineStraightRoute
            && BranchRole != FlowchartTerminalCollectorBranchRole.FarLeftMargin
            && !UseStadiumMergeRoute)
        {
            LayoutPoints = FlowchartEdgeTerminalStubExpander.Apply(
                LayoutPoints,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape);
        }

        return LayoutPoints.Count >= 2;
    }

    /// <summary>
    /// Returns true when the route is a spine-aligned two-point connector.
    /// </summary>
    public static bool IsSpineStraightRoute(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        if (LayoutPoints.Count != 2)
        {
            return false;
        }

        return FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
            LayoutPoints[0].X,
            LayoutPoints[0].Y,
            LayoutPoints[1].X,
            LayoutPoints[1].Y);
    }

    /// <summary>
    /// Returns whether an edge should use terminal collector routing instead of generic fan-in.
    /// </summary>
    public static bool ShouldApplyTerminalCollectorRouting(DiagramContract Contract, string TargetNodeId)
    {
        if (CountVisibleOutgoing(Contract, TargetNodeId) > 0)
        {
            return false;
        }

        if (!IsTerminalCollectorTarget(Contract, TargetNodeId))
        {
            return false;
        }

        NodeContract? TargetNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == TargetNodeId);
        if (TargetNode is not null
            && IsStadiumLikeShape(TargetNode.Shape)
            && CountVisibleIncoming(Contract, TargetNodeId) >= 2)
        {
            return true;
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!IsDecisionShape(Node.Shape))
            {
                continue;
            }

            if (CountVisibleOutgoing(Contract, Node.Id) >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendStadiumMergeRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double ApproachX,
        double ApproachY)
    {
        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double TargetStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TargetBounds);
        (double SourceStubX, double SourceStubY) = ComputeStadiumMergeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape,
            SourceStubLength);
        (double TargetStubX, double TargetStubY) = ComputeStadiumMergeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape,
            TargetStubLength);

        double SpanY = TargetY - SourceY;
        double RouteY = SourceY + Math.Clamp(SpanY * 0.45, -40.0, 40.0);
        double MidY = (RouteY + TargetY) / 2.0;
        double MergeApproachX = ApproachX;
        if (Math.Abs(MergeApproachX - SourceX) < COLLINEAR_EPSILON_PX)
        {
            MergeApproachX = SourceX + ((TargetX - SourceX) * 0.45);
        }

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));
        Points.Add((SourceX, RouteY));
        Points.Add((MergeApproachX, MidY));
        Points.Add((TargetStubX, TargetStubY));
        Points.Add((TargetX, TargetY));
    }

    private static (double X, double Y) ComputeStadiumMergeStubOuterPoint(
        double BoundaryX,
        double BoundaryY,
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double StubLength)
    {
        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            Bounds,
            BoundaryX,
            BoundaryY);

        switch (Face)
        {
            case FlowchartEdgeAttachFace.Top:
                return (BoundaryX, BoundaryY - StubLength);

            case FlowchartEdgeAttachFace.Bottom:
                return (BoundaryX, BoundaryY + StubLength);

            case FlowchartEdgeAttachFace.Left:
                return (BoundaryX - StubLength, BoundaryY);

            case FlowchartEdgeAttachFace.Right:
                return (BoundaryX + StubLength, BoundaryY);

            default:
                return (BoundaryX, BoundaryY + StubLength);
        }
    }

    private static (double X, double Y) ResolveSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        FlowchartDirection Direction,
        EdgeEndpointMarker SourceEndpointMarker)
    {
        double HalfHeight = SourceBounds.Height / 2.0;
        double CentreX = SourceBounds.CenterX;
        double CentreY = SourceBounds.CenterY;
        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);

        if (Direction == FlowchartDirection.BottomToTop)
        {
            if (SourceBounds.CenterY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
            {
                return (CentreX, CentreY + HalfHeight + GapPx);
            }

            return (CentreX, CentreY - HalfHeight - GapPx);
        }

        if (FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds))
        {
            return (CentreX, CentreY - HalfHeight - GapPx);
        }

        return (CentreX, CentreY + HalfHeight + GapPx);
    }

    private static void ResolveTargetAttachAndApproach(
        FlowchartTerminalCollectorBranchRole BranchRole,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        EdgeEndpointMarker TargetEndpointMarker,
        int BranchIndex,
        int BranchCount,
        out double TargetX,
        out double TargetY,
        out double ApproachX,
        out double ApproachY)
    {
        double HalfWidth = TargetBounds.Width / 2.0;
        double HalfHeight = TargetBounds.Height / 2.0;
        double CentreX = TargetBounds.CenterX;
        double CentreY = TargetBounds.CenterY;

        if (BranchRole == FlowchartTerminalCollectorBranchRole.FarLeftMargin)
        {
            double SyntheticSourceX = CentreX;
            double SyntheticSourceY = CentreY + HalfHeight + Math.Max(SIDE_LEAD_PX, 32.0);
            if (FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                    TargetBounds,
                    TargetShape,
                    SyntheticSourceX,
                    SyntheticSourceY,
                    TargetEndpointMarker,
                    out TargetX,
                    out TargetY))
            {
                ApproachX = SourceBounds.CenterX + ((TargetX - SourceBounds.CenterX) * 0.55);
                ApproachY = TargetY + SIDE_LEAD_PX;
                return;
            }

            TargetX = CentreX;
            TargetY = CentreY + HalfHeight + FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
            ApproachX = SourceBounds.CenterX + ((TargetX - SourceBounds.CenterX) * 0.55);
            ApproachY = TargetY + SIDE_LEAD_PX;
            return;
        }

        if (BranchRole == FlowchartTerminalCollectorBranchRole.FarRightAuxiliary)
        {
            TargetX = CentreX + HalfWidth + FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
            TargetY = CentreY;
            ApproachX = TargetX + SIDE_LEAD_PX;
            ApproachY = CentreY;
            return;
        }

        ApproachX = CentreX
            + ((SourceBounds.CenterX - CentreX) * STADIUM_MERGE_APPROACH_X_BLEND);
        double TargetTop = CentreY - HalfHeight;
        ApproachY = TargetTop + (TargetBounds.Height * STADIUM_MERGE_APPROACH_Y_FROM_TOP_RATIO);

        if (IsStadiumLikeShape(TargetShape))
        {
            (TargetX, TargetY) = ResolveStadiumShoulderAttach(
                CentreX,
                CentreY,
                TargetBounds.Width,
                TargetBounds.Height,
                SourceBounds.CenterX,
                ApproachX,
                ApproachY,
                TargetEndpointMarker);
        }
        else
        {
            bool UseTopLeftCorner = SourceBounds.CenterX < CentreX - COLLINEAR_EPSILON_PX;
            (TargetX, TargetY) = ResolveRectTopCornerAttach(
                CentreX,
                CentreY,
                TargetBounds.Width,
                TargetBounds.Height,
                UseTopLeftCorner,
                TargetEndpointMarker);
            ApplyCollectorTargetPortSpread(
                ref TargetX,
                BranchIndex,
                BranchCount,
                CentreX,
                HalfWidth);
        }
    }

    private static void ApplyCollectorTargetPortSpread(
        ref double TargetX,
        int BranchIndex,
        int BranchCount,
        double CentreX,
        double HalfWidth)
    {
        if (BranchCount <= 1)
        {
            return;
        }

        double Span = HalfWidth * 0.75;
        double Step = Span / Math.Max(BranchCount - 1, 1);
        TargetX = (CentreX - (Span / 2.0)) + (Step * BranchIndex);
    }

    private static FlowchartTerminalCollectorBranchRole ResolveBranchRole(
        DiagramContract Contract,
        EdgeContract Edge,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        FlowchartDirection Direction,
        out int BranchIndex,
        out int BranchCount)
    {
        BranchIndex = 0;
        BranchCount = 1;
        if (IsCylinderShape(SourceShape))
        {
            if (FlowchartShapeBoundaryAttachHelper.AreSpineColumnAligned(SourceBounds, TargetBounds))
            {
                return FlowchartTerminalCollectorBranchRole.CentreRight;
            }

            return FlowchartTerminalCollectorBranchRole.FarRightAuxiliary;
        }

        List<(string NodeId, double CenterX)> RectangleSourcesAbove = [];
        foreach (EdgeContract IncomingEdge in Contract.Edges)
        {
            if (string.Equals(IncomingEdge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(IncomingEdge.TargetId, Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds IncomingSourceBounds))
            {
                continue;
            }

            NodeContract? IncomingSourceNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == IncomingEdge.SourceId);
            if (IncomingSourceNode is not null && IsCylinderShape(IncomingSourceNode.Shape))
            {
                continue;
            }

            if (Direction == FlowchartDirection.BottomToTop)
            {
                if (IncomingSourceBounds.CenterY >= TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
                {
                    continue;
                }
            }
            else
            {
                bool IsFarLeftMarginColumn = IncomingSourceBounds.CenterX
                    < TargetBounds.CenterX - FAR_MARGIN_MIN_OFFSET_PX;
                bool IsFarRightMarginColumn = IncomingSourceBounds.CenterX
                    > TargetBounds.CenterX + FAR_MARGIN_MIN_OFFSET_PX;
                if (!IsFarLeftMarginColumn
                    && !IsFarRightMarginColumn
                    && IncomingSourceBounds.CenterY <= TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
                {
                    continue;
                }
            }

            RectangleSourcesAbove.Add((IncomingEdge.SourceId, IncomingSourceBounds.CenterX));
        }

        RectangleSourcesAbove.Sort((Left, Right) => Left.CenterX.CompareTo(Right.CenterX));
        BranchCount = RectangleSourcesAbove.Count;

        for (int SourceIndex = 0; SourceIndex < RectangleSourcesAbove.Count; SourceIndex++)
        {
            if (string.Equals(RectangleSourcesAbove[SourceIndex].NodeId, Edge.SourceId, StringComparison.Ordinal))
            {
                BranchIndex = SourceIndex;
                break;
            }
        }

        if (RectangleSourcesAbove.Count <= 1)
        {
            return SourceBounds.CenterX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX
                ? FlowchartTerminalCollectorBranchRole.CentreLeft
                : FlowchartTerminalCollectorBranchRole.CentreRight;
        }

        NodeContract? TargetNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.TargetId);
        if (TargetNode is not null
            && IsStadiumLikeShape(TargetNode.Shape)
            && RectangleSourcesAbove.Count == 2)
        {
            if (SourceBounds.CenterX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
            {
                return FlowchartTerminalCollectorBranchRole.CentreLeft;
            }

            return FlowchartTerminalCollectorBranchRole.CentreRight;
        }

        if (BranchIndex == 0
            && SourceBounds.CenterX < TargetBounds.CenterX - FAR_MARGIN_MIN_OFFSET_PX)
        {
            return FlowchartTerminalCollectorBranchRole.FarLeftMargin;
        }

        if (BranchIndex == RectangleSourcesAbove.Count - 1
            && SourceBounds.CenterX > TargetBounds.CenterX + FAR_MARGIN_MIN_OFFSET_PX)
        {
            return FlowchartTerminalCollectorBranchRole.CentreRight;
        }

        if (SourceBounds.CenterX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
        {
            return FlowchartTerminalCollectorBranchRole.CentreLeft;
        }

        return FlowchartTerminalCollectorBranchRole.CentreRight;
    }

    private static (double X, double Y) ResolveRectTopCornerAttach(
        double CentreX,
        double CentreY,
        double Width,
        double Height,
        bool UseTopLeftCorner,
        EdgeEndpointMarker TargetEndpointMarker)
    {
        double HalfWidth = Width / 2.0;
        double HalfHeight = Height / 2.0;
        double CornerX = UseTopLeftCorner ? CentreX - HalfWidth : CentreX + HalfWidth;
        double CornerY = CentreY - HalfHeight;
        double DiagonalUnit = 1.0 / Math.Sqrt(2.0);
        double DirX = UseTopLeftCorner ? DiagonalUnit : -DiagonalUnit;
        double DirY = DiagonalUnit;
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);

        double HitX = CornerX + (DirX * TargetGapPx);
        double HitY = CornerY + (DirY * TargetGapPx);
        return (HitX, HitY);
    }

    private static (double X, double Y) ResolveStadiumShoulderAttach(
        double TargetCentreX,
        double TargetCentreY,
        double TargetWidth,
        double TargetHeight,
        double SourceCentreX,
        double ApproachX,
        double ApproachY,
        EdgeEndpointMarker TargetEndpointMarker)
    {
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
        double Radius = TargetHeight / 2.0;
        double HalfWidth = TargetWidth / 2.0;
        double CapCentreX = SourceCentreX < TargetCentreX - COLLINEAR_EPSILON_PX
            ? TargetCentreX - HalfWidth + Radius
            : TargetCentreX + HalfWidth - Radius;
        double CapCentreY = TargetCentreY;
        double Angle = Math.Atan2(ApproachY - CapCentreY, ApproachX - CapCentreX);
        double HitX = CapCentreX + (Radius * Math.Cos(Angle));
        double HitY = CapCentreY + (Radius * Math.Sin(Angle));

        double GapX = HitX - TargetCentreX;
        double GapY = HitY - TargetCentreY;
        double GapLength = Math.Sqrt((GapX * GapX) + (GapY * GapY));
        if (GapLength > COLLINEAR_EPSILON_PX)
        {
            double Scale = (GapLength + TargetGapPx) / GapLength;
            HitX = TargetCentreX + (GapX * Scale);
            HitY = TargetCentreY + (GapY * Scale);
        }

        return (HitX, HitY);
    }

    private static bool IsTerminalCollectorTarget(DiagramContract Contract, string TargetNodeId)
    {
        int IncomingCount = CountVisibleIncoming(Contract, TargetNodeId);
        bool HasCylinderSource = false;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(Edge.TargetId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            NodeContract? SourceNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.SourceId);
            if (SourceNode is not null && IsCylinderShape(SourceNode.Shape))
            {
                HasCylinderSource = true;
                break;
            }
        }

        NodeContract? TargetNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == TargetNodeId);
        bool IsStadiumSink = TargetNode is not null && IsStadiumLikeShape(TargetNode.Shape);

        return IncomingCount >= MINIMUM_INCOMING_FOR_TERMINAL_COLLECTOR
            || (HasCylinderSource && IncomingCount >= 2)
            || (IsStadiumSink && IncomingCount >= 2);
    }

    private static int CountVisibleIncoming(DiagramContract Contract, string NodeId)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }

    private static int CountVisibleOutgoing(DiagramContract Contract, string NodeId)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }

    private static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCylinderShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "cylinder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "database", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStadiumLikeShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "stadium", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rounded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rounded_rectangle", StringComparison.OrdinalIgnoreCase);
    }
}
