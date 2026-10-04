using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Structural diamond port allocation — branch count, creation order, and direction only (spec §11.7).
/// </summary>
public static class FlowchartDecisionPortAllocator
{
    private const double EDGE_NODE_GAP_PX = 2.0;

    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double DECISION_GUIDE_BULGE_RATIO = 0.35;

    /// <summary>
    /// Resolves layout attach points and guided waypoints for decision fan-out/fan-in edges.
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

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (IsDecisionShape(SourceShape)
            && CountVisibleEdges(Contract, Edge.SourceId, true) >= 2)
        {
            return TryResolveDecisionFanOutRoute(
                Edge,
                Contract,
                BoundsById,
                SourceBounds,
                TargetBounds,
                SourceShape,
                TargetShape,
                Direction,
                LayoutPoints);
        }

        if (IsDecisionShape(TargetShape)
            && CountVisibleEdges(Contract, Edge.TargetId, false) >= 2
            && string.IsNullOrWhiteSpace(Edge.Label))
        {
            return TryResolveDecisionFanInRoute(
                Edge,
                Contract,
                BoundsById,
                SourceBounds,
                SourceShape,
                TargetBounds,
                Direction,
                LayoutPoints);
        }

        return false;
    }

    /// <summary>
    /// Builds a curved decision fan-out route without applying centre-spine straight routing.
    /// </summary>
    public static bool TryBuildDecisionFanOutCurvedRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        List<(double X, double Y)> LayoutPoints)
    {
        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        return TryAppendDecisionFanOutCurvedRoute(
            Edge,
            Contract,
            BoundsById,
            SourceBounds,
            TargetBounds,
            SourceShape,
            TargetShape,
            Direction,
            LayoutPoints);
    }

    /// <summary>
    /// Resolves decision fan-out boundary attach points for TB/BT branches.
    /// </summary>
    public static bool TryResolveDecisionFanOutAttachPoints(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        out double SourceX,
        out double SourceY,
        out double TargetX,
        out double TargetY,
        out FlowchartDecisionPortRole Role,
        out double HubCentreX)
    {
        SourceX = 0.0;
        SourceY = 0.0;
        TargetX = 0.0;
        TargetY = 0.0;
        Role = FlowchartDecisionPortRole.BottomVertex;
        HubCentreX = 0.0;

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (!TryPrepareDecisionFanOutBranch(
                Edge,
                Contract,
                BoundsById,
                SourceBounds,
                TargetBounds,
                Direction,
                out List<EdgeContract> Outgoing,
                out int BranchIndex,
                out bool HorizontalFlow,
                out bool UseBottomFace,
                out bool UseOppositeVerticalPorts,
                out double CentreX,
                out double CentreY,
                out double HalfWidth,
                out double HalfHeight))
        {
            return false;
        }

        HubCentreX = CentreX;
        (SourceX, SourceY, Role) = ResolveDecisionFanOutAttach(
            SourceBounds,
            BoundsById,
            Direction,
            CentreX,
            CentreY,
            HalfWidth,
            HalfHeight,
            UseBottomFace,
            BranchIndex,
            Outgoing.Count,
            HorizontalFlow,
            UseOppositeVerticalPorts,
            Edge,
            Contract,
            TargetBounds);
        (TargetX, TargetY) = ResolveFanOutTargetAttach(
            TargetBounds,
            TargetShape,
            Direction,
            SourceX,
            SourceY,
            Edge.MarkerEnd);
        return true;
    }

    private static bool TryResolveDecisionFanOutRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        List<(double X, double Y)> LayoutPoints)
    {
        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            RenderOptions,
            out FlowchartSpineLayoutContext? SpineContext);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartEdgeSemanticRouteClass SemanticRouteClass = FlowchartEdgeRouteClassifier.Classify(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            SpineContext);

        if (SemanticRouteClass == FlowchartEdgeSemanticRouteClass.CentreSpine
            && FlowchartCentreSpineRouteBuilder.TryBuildRoute(
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                Edge.MarkerStart,
                Edge.MarkerEnd,
                Direction,
                LayoutPoints))
        {
            return true;
        }

        return TryAppendDecisionFanOutCurvedRoute(
            Edge,
            Contract,
            BoundsById,
            SourceBounds,
            TargetBounds,
            SourceShape,
            TargetShape,
            Direction,
            LayoutPoints);
    }

    private static bool TryPrepareDecisionFanOutBranch(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        out List<EdgeContract> Outgoing,
        out int BranchIndex,
        out bool HorizontalFlow,
        out bool UseBottomFace,
        out bool UseOppositeVerticalPorts,
        out double CentreX,
        out double CentreY,
        out double HalfWidth,
        out double HalfHeight)
    {
        Outgoing = GetVisibleEdgesFromSource(Contract, Edge.SourceId);
        BranchIndex = -1;
        HorizontalFlow = false;
        UseBottomFace = false;
        UseOppositeVerticalPorts = false;
        CentreX = SourceBounds.CenterX;
        CentreY = SourceBounds.CenterY;
        HalfWidth = SourceBounds.Width / 2.0;
        HalfHeight = SourceBounds.Height / 2.0;

        if (Outgoing.Count < 2)
        {
            return false;
        }

        HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        UseBottomFace = !HorizontalFlow && AllTargetsBelowSource(Outgoing, BoundsById, CentreY);
        UseOppositeVerticalPorts = HorizontalFlow
            && AllTargetsShareHubCrossAxis(Outgoing, BoundsById, CentreY, UseCrossAxisY: true);

        if (UseOppositeVerticalPorts)
        {
            Outgoing.Sort((Left, Right) => Left.CreationIndex.CompareTo(Right.CreationIndex));
        }
        else if (HorizontalFlow)
        {
            SortOutgoingByTargetCrossAxis(Outgoing, BoundsById, UseDescendingCrossAxis: true);
        }
        else
        {
            SortOutgoingByTargetCrossAxis(Outgoing, BoundsById, UseDescendingCrossAxis: false);
        }

        BranchIndex = Outgoing.FindIndex(
            Candidate => string.Equals(Candidate.TargetId, Edge.TargetId, StringComparison.Ordinal));

        return BranchIndex >= 0;
    }

    private static bool TryAppendDecisionFanOutCurvedRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        List<(double X, double Y)> LayoutPoints)
    {
        if (!TryPrepareDecisionFanOutBranch(
                Edge,
                Contract,
                BoundsById,
                SourceBounds,
                TargetBounds,
                Direction,
                out List<EdgeContract> Outgoing,
                out int BranchIndex,
                out bool HorizontalFlow,
                out bool UseBottomFace,
                out bool UseOppositeVerticalPorts,
                out double CentreX,
                out double CentreY,
                out double HalfWidth,
                out double HalfHeight))
        {
            return false;
        }

        (double SourceX, double SourceY, FlowchartDecisionPortRole Role) = ResolveDecisionFanOutAttach(
            SourceBounds,
            BoundsById,
            Direction,
            CentreX,
            CentreY,
            HalfWidth,
            HalfHeight,
            UseBottomFace,
            BranchIndex,
            Outgoing.Count,
            HorizontalFlow,
            UseOppositeVerticalPorts,
            Edge,
            Contract,
            TargetBounds);

        (double TargetX, double TargetY) = ResolveFanOutTargetAttach(
            TargetBounds,
            TargetShape,
            Direction,
            SourceX,
            SourceY,
            Edge.MarkerEnd);

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            RenderOptions,
            out FlowchartSpineLayoutContext? SpineContext);
        bool UseSpreadTopPort = SpineContext is not null
            && FlowchartMultiIncomingTargetPortAllocator.TargetHasBranchAndMergeIncoming(
                Contract,
                BoundsById,
                SpineContext,
                Edge.TargetId);
        if (UseSpreadTopPort
            && FlowchartMultiIncomingTargetPortAllocator.TryResolveSpreadTopInboundAttach(
                Edge,
                Contract,
                BoundsById,
                TargetBounds,
                TargetShape,
                SourceX,
                SourceY,
                out double SpreadTargetX,
                out double SpreadTargetY))
        {
            TargetX = SpreadTargetX;
            TargetY = SpreadTargetY;
        }

        if (!UseSpreadTopPort
            && FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                SourceX,
                SourceY,
                TargetX,
                TargetY))
        {
            if (!FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    SourceX,
                    SourceY,
                    TargetX,
                    TargetY,
                    BoundsById,
                    Edge.SourceId,
                    Edge.TargetId))
            {
                LayoutPoints.Add((SourceX, SourceY));
                LayoutPoints.Add((TargetX, TargetY));
                return true;
            }

            if (!HorizontalFlow)
            {
                AppendVerticalDecisionBranchRouteWithObstacleAvoidance(
                    Edge,
                    BoundsById,
                    LayoutPoints,
                    SourceX,
                    SourceY,
                    SourceBounds,
                    TargetX,
                    TargetY,
                    TargetBounds,
                    TargetShape,
                    CentreX,
                    Role);
                return LayoutPoints.Count >= 2;
            }
        }

        if (HorizontalFlow)
        {
            if (UseOppositeVerticalPorts && Outgoing.Count == 2 && BranchIndex == 1)
            {
                double CorridorY = TargetBounds.CenterY - (TargetBounds.Height / 2.0) - Math.Max(EDGE_NODE_GAP_PX * 4.0, 16.0);
                foreach (EdgeContract OutgoingEdge in Outgoing)
                {
                    if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds BranchTargetBounds))
                    {
                        continue;
                    }

                    double BranchBottom = BranchTargetBounds.CenterY - (BranchTargetBounds.Height / 2.0);
                    if (BranchBottom - Math.Max(EDGE_NODE_GAP_PX * 4.0, 16.0) < CorridorY)
                    {
                        CorridorY = BranchBottom - Math.Max(EDGE_NODE_GAP_PX * 4.0, 16.0);
                    }
                }

                FlowchartPerpendicularRouteBuilder.AppendPerpendicularRouteWithCorridorY(
                    LayoutPoints,
                    SourceX,
                    SourceY,
                    SourceBounds,
                    "diamond",
                    TargetX,
                    TargetY,
                    TargetBounds,
                    TargetShape,
                    CorridorY);
                return true;
            }

            FlowchartHorizontalRouteGuideBuilder.AppendHorizontalDecisionBranchRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                "diamond",
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                CentreY,
                Role);
        }
        else
        {
            AppendVerticalDecisionBranchRouteWithObstacleAvoidance(
                Edge,
                BoundsById,
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                CentreX,
                Role);
        }

        return true;
    }

    private static bool TryResolveDecisionFanInRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        List<(double X, double Y)> LayoutPoints)
    {
        List<EdgeContract> Incoming = GetVisibleEdgesToTarget(Contract, Edge.TargetId)
            .Where(Candidate => string.IsNullOrWhiteSpace(Candidate.Label))
            .ToList();

        if (Incoming.Count < 2)
        {
            return false;
        }

        if (Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft)
        {
            return false;
        }

        SortIncomingBySourceCrossAxis(Incoming, BoundsById);

        int BranchIndex = Incoming.FindIndex(
            Candidate => string.Equals(Candidate.SourceId, Edge.SourceId, StringComparison.Ordinal));

        if (BranchIndex < 0)
        {
            return false;
        }

        bool UseTbMultiIncomingDiamondFanIn = TryIsTbMultiIncomingDiamondFanIn(
            Incoming,
            BoundsById,
            TargetBounds,
            Direction);

        double SourceX;
        double SourceY;
        if (UseTbMultiIncomingDiamondFanIn)
        {
            (SourceX, SourceY) = ResolveRectangleBottomCentreAttach(SourceBounds, Direction, Edge.MarkerStart);
        }
        else
        {
            (SourceX, SourceY) = FlowchartFanInPortAllocator.ResolveFanInSourceAttach(
                SourceBounds,
                TargetBounds,
                Direction,
                Edge.MarkerStart);
        }

        FlowchartDecisionPortRole Role = ResolveDecisionFanInEntryRole(BranchIndex, Incoming.Count);
        (double TargetX, double TargetY) = ResolveDecisionFanInTargetAttach(
            TargetBounds,
            SourceX,
            SourceY,
            Role);

        if (FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                SourceX,
                SourceY,
                TargetX,
                TargetY))
        {
            LayoutPoints.Add((SourceX, SourceY));
            LayoutPoints.Add((TargetX, TargetY));
            return true;
        }

        FlowchartVerticalRouteGuideBuilder.AppendVerticalDecisionBranchRoute(
            LayoutPoints,
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape,
            TargetX,
            TargetY,
            TargetBounds,
            "diamond",
            TargetBounds.CenterX,
            Role,
            IsInbound: true);
        return true;
    }

    private static bool TryIsTbMultiIncomingDiamondFanIn(
        List<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction)
    {
        if (Incoming.Count < 3)
        {
            return false;
        }

        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        return AllSourcesOnIncomingSide(Incoming, BoundsById, TargetBounds, Direction);
    }

    private static bool AllSourcesOnIncomingSide(
        List<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction)
    {
        foreach (EdgeContract IncomingEdge in Incoming)
        {
            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
            {
                return false;
            }

            if (Direction == FlowchartDirection.BottomToTop)
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

        return true;
    }

    private const double SIDE_BRANCH_CROSS_AXIS_EPSILON_PX = 40.0;

    private const double SAME_COLUMN_CROSS_AXIS_EPSILON_PX = 30.0;

    private static (double X, double Y, FlowchartDecisionPortRole Role) ResolveDecisionFanOutAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool UseBottomFace,
        int BranchIndex,
        int BranchCount,
        bool HorizontalFlow,
        bool UseOppositeVerticalPorts,
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        if (HorizontalFlow)
        {
            double TopRightSlopeX = CentreX + (HalfWidth / 2.0);
            double TopRightSlopeY = CentreY + (HalfHeight / 2.0);
            double RightVertexX = CentreX + HalfWidth;
            double RightVertexY = CentreY;
            double LowerRightSlopeX = CentreX + (HalfWidth / 2.0);
            double LowerRightSlopeY = CentreY - (HalfHeight / 2.0);

            if (BranchCount == 2)
            {
                if (BranchIndex == 0)
                {
                    return (TopRightSlopeX, TopRightSlopeY, FlowchartDecisionPortRole.SideLeft);
                }

                return (LowerRightSlopeX, LowerRightSlopeY, FlowchartDecisionPortRole.SideRight);
            }

            if (BranchIndex == 0)
            {
                return (TopRightSlopeX, TopRightSlopeY, FlowchartDecisionPortRole.SideLeft);
            }

            if (BranchIndex == 1)
            {
                return (RightVertexX, RightVertexY, FlowchartDecisionPortRole.BottomVertex);
            }

            return (LowerRightSlopeX, LowerRightSlopeY, FlowchartDecisionPortRole.SideRight);
        }

        double LeftMidX = CentreX - (HalfWidth / 2.0);
        double RightMidX = CentreX + (HalfWidth / 2.0);
        double MidVertexX = CentreX;
        double LeftMidY;
        double RightMidY;
        double MidVertexY;

        if (UseBottomFace)
        {
            LeftMidY = CentreY - (HalfHeight / 2.0);
            RightMidY = CentreY - (HalfHeight / 2.0);
            MidVertexY = CentreY - HalfHeight;
        }
        else
        {
            LeftMidY = CentreY + (HalfHeight / 2.0);
            RightMidY = CentreY + (HalfHeight / 2.0);
            MidVertexY = CentreY + HalfHeight;
        }

        if (BranchCount == 2 && UseBottomFace)
        {
            return ResolveVerticalTwoWayDecisionPort(
                SourceBounds,
                BoundsById,
                Direction,
                CentreX,
                CentreY,
                HalfWidth,
                HalfHeight,
                UseBottomFace,
                Edge,
                Contract,
                TargetBounds);
        }

        if (BranchCount == 2)
        {
            if (BranchIndex == 0)
            {
                return (LeftMidX, LeftMidY, FlowchartDecisionPortRole.SideLeft);
            }

            return (RightMidX, RightMidY, FlowchartDecisionPortRole.SideRight);
        }

        if (BranchIndex == 0)
        {
            return (LeftMidX, LeftMidY, FlowchartDecisionPortRole.SideLeft);
        }

        if (BranchIndex == 1)
        {
            return (MidVertexX, MidVertexY, FlowchartDecisionPortRole.BottomVertex);
        }

        return (RightMidX, RightMidY, FlowchartDecisionPortRole.SideRight);
    }

    private static (double X, double Y, FlowchartDecisionPortRole Role) ResolveVerticalTwoWayDecisionPort(
        FlowchartLayoutNodeBounds SourceBounds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool UseBottomFace,
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        double LeftMidX = CentreX - (HalfWidth / 2.0);
        double RightMidX = CentreX + (HalfWidth / 2.0);
        double MidVertexX = CentreX;
        double LeftMidY = UseBottomFace
            ? CentreY - (HalfHeight / 2.0)
            : CentreY + (HalfHeight / 2.0);
        double RightMidY = LeftMidY;
        double MidVertexY = UseBottomFace
            ? CentreY - HalfHeight
            : CentreY + HalfHeight;

        double CrossDelta = TargetBounds.CenterX - CentreX;
        if (CrossDelta < -SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            return (LeftMidX, LeftMidY, FlowchartDecisionPortRole.SideLeft);
        }

        if (CrossDelta > SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            return (RightMidX, RightMidY, FlowchartDecisionPortRole.SideRight);
        }

        if (IsNoLabeledEdge(Edge))
        {
            FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
            if (FlowchartSpineGraphHelper.TryBuildContext(
                    Contract,
                    BoundsById,
                    Direction,
                    RenderOptions,
                    out FlowchartSpineLayoutContext? SpineContext)
                && SpineContext is not null
                && HasInterveningSpineColumnNode(BoundsById, SpineContext, SourceBounds, TargetBounds))
            {
                return (RightMidX, RightMidY, FlowchartDecisionPortRole.SideRight);
            }

            return (LeftMidX, LeftMidY, FlowchartDecisionPortRole.SideLeft);
        }

        if (IsYesLabeledEdge(Edge))
        {
            return (MidVertexX, MidVertexY, FlowchartDecisionPortRole.BottomVertex);
        }

        return (MidVertexX, MidVertexY, FlowchartDecisionPortRole.BottomVertex);
    }

    private static bool HasInterveningSpineColumnNode(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSpineLayoutContext SpineContext,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceBounds.NodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetBounds.NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!FlowchartSpineGraphHelper.IsOnSpineColumn(CandidateBounds, SpineContext))
            {
                continue;
            }

            if (CandidateBounds.CenterY >= SourceBounds.CenterY - FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                continue;
            }

            if (CandidateBounds.CenterY <= TargetBounds.CenterY + FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsNoLabeledEdge(EdgeContract? Edge)
    {
        if (Edge is null || string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        return string.Equals(Edge.Label.Trim(), "No", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.Label.Trim(), "no", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsYesLabeledEdge(EdgeContract? Edge)
    {
        if (Edge is null || string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        return string.Equals(Edge.Label.Trim(), "Yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.Label.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static FlowchartDecisionPortRole ResolveDecisionFanInEntryRole(int BranchIndex, int BranchCount)
    {
        if (BranchCount >= 3 && BranchIndex == 1)
        {
            return FlowchartDecisionPortRole.TopVertex;
        }

        if (BranchIndex == 0)
        {
            return FlowchartDecisionPortRole.SideLeft;
        }

        return FlowchartDecisionPortRole.SideRight;
    }

    private static (double X, double Y) ResolveDecisionFanInTargetAttach(
        FlowchartLayoutNodeBounds TargetBounds,
        double SourceX,
        double SourceY,
        FlowchartDecisionPortRole Role)
    {
        double HalfWidth = TargetBounds.Width / 2.0;
        double HalfHeight = TargetBounds.Height / 2.0;
        double CentreX = TargetBounds.CenterX;
        double CentreY = TargetBounds.CenterY;

        if (Role == FlowchartDecisionPortRole.TopVertex)
        {
            return (CentreX, CentreY + HalfHeight);
        }

        if (Role == FlowchartDecisionPortRole.SideLeft)
        {
            return (CentreX - (HalfWidth / 2.0), CentreY + (HalfHeight / 2.0));
        }

        return (CentreX + (HalfWidth / 2.0), CentreY + (HalfHeight / 2.0));
    }

    private static (double X, double Y) ResolveFanOutTargetAttach(
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        FlowchartDirection Direction,
        double SourceX,
        double SourceY,
        EdgeEndpointMarker TargetEndpointMarker)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);

        if (HorizontalFlow)
        {
            double HalfWidth = TargetBounds.Width / 2.0;
            double HalfHeight = TargetBounds.Height / 2.0;
            if (SourceX > TargetBounds.CenterX + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX + HalfWidth + TargetGapPx, TargetBounds.CenterY);
            }

            if (SourceX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX - HalfWidth - TargetGapPx, TargetBounds.CenterY);
            }

            if (SourceY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeight + TargetGapPx);
            }

            if (SourceY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeight - TargetGapPx);
            }

            double TargetFaceX = RightToLeft
                ? TargetBounds.CenterX + HalfWidth + TargetGapPx
                : TargetBounds.CenterX - HalfWidth - TargetGapPx;
            return (TargetFaceX, TargetBounds.CenterY);
        }

        double HalfHeightTb = TargetBounds.Height / 2.0;
        if (Math.Abs(SourceX - TargetBounds.CenterX) > COLLINEAR_EPSILON_PX)
        {
            if (SourceY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeightTb + TargetGapPx);
            }

            if (SourceY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeightTb - TargetGapPx);
            }
        }

        if (FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                TargetBounds,
                TargetShape,
                SourceX,
                SourceY,
                TargetEndpointMarker,
                out double ShapeAttachX,
                out double ShapeAttachY))
        {
            return (ShapeAttachX, ShapeAttachY);
        }

        if (SourceY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
        {
            return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeightTb + TargetGapPx);
        }

        if (SourceY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
        {
            return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeightTb - TargetGapPx);
        }

        return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeightTb + TargetGapPx);
    }

    private static (double X, double Y) ResolveRectangleBottomCentreAttach(
        FlowchartLayoutNodeBounds Bounds,
        FlowchartDirection Direction,
        EdgeEndpointMarker EndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfHeight = Bounds.Height / 2.0;
        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(EndpointMarker);

        if (Direction == FlowchartDirection.BottomToTop)
        {
            return (Bounds.CenterX, Bounds.CenterY + HalfHeight + GapPx);
        }

        return (Bounds.CenterX, Bounds.CenterY - HalfHeight - GapPx);
    }

    private static bool AllTargetsBelowSource(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double SourceCentreY)
    {
        foreach (EdgeContract OutgoingEdge in Outgoing)
        {
            if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            if (TargetBounds.CenterY >= SourceCentreY - COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllTargetsShareHubCrossAxis(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double HubCrossAxisValue,
        bool UseCrossAxisY)
    {
        foreach (EdgeContract OutgoingEdge in Outgoing)
        {
            if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            double TargetCrossAxis = UseCrossAxisY ? TargetBounds.CenterY : TargetBounds.CenterX;
            if (Math.Abs(TargetCrossAxis - HubCrossAxisValue) > COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

    private static void SortOutgoingByTargetCrossAxis(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool UseDescendingCrossAxis) => Outgoing.Sort((Left, Right) =>
                                             {
                                                 double LeftValue = GetCrossAxis(Left.TargetId, BoundsById);
                                                 double RightValue = GetCrossAxis(Right.TargetId, BoundsById);
                                                 int CrossCompare = UseDescendingCrossAxis
                                                     ? RightValue.CompareTo(LeftValue)
                                                     : LeftValue.CompareTo(RightValue);
                                                 if (CrossCompare != 0)
                                                 {
                                                     return CrossCompare;
                                                 }

                                                 return Left.CreationIndex.CompareTo(Right.CreationIndex);
                                             });

    private static void SortIncomingBySourceCrossAxis(
        List<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById) => Incoming.Sort((Left, Right) =>
                                                                                   {
                                                                                       double LeftCross = GetCrossAxis(Left.SourceId, BoundsById);
                                                                                       double RightCross = GetCrossAxis(Right.SourceId, BoundsById);
                                                                                       int CrossCompare = LeftCross.CompareTo(RightCross);
                                                                                       if (CrossCompare != 0)
                                                                                       {
                                                                                           return CrossCompare;
                                                                                       }

                                                                                       return Left.CreationIndex.CompareTo(Right.CreationIndex);
                                                                                   });

    private static double GetCrossAxis(
        string NodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
        {
            return Bounds.CenterX;
        }

        return 0.0;
    }

    private static List<EdgeContract> GetVisibleEdgesFromSource(DiagramContract Contract, string SourceId)
    {
        List<EdgeContract> Edges = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal))
            {
                Edges.Add(Edge);
            }
        }

        return Edges;
    }

    private static List<EdgeContract> GetVisibleEdgesToTarget(DiagramContract Contract, string TargetId)
    {
        List<EdgeContract> Edges = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal))
            {
                Edges.Add(Edge);
            }
        }

        return Edges;
    }

    private static int CountVisibleEdges(DiagramContract Contract, string NodeId, bool Outgoing)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Outgoing)
            {
                if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal))
                {
                    Count++;
                }
            }
            else if (string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
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

    private static void AppendVerticalDecisionBranchRouteWithObstacleAvoidance(
        EdgeContract Edge,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<(double X, double Y)> LayoutPoints,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double HubCentreX,
        FlowchartDecisionPortRole Role,
        bool IsInbound = false)
    {
        if (FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                SourceX,
                SourceY,
                TargetX,
                TargetY,
                BoundsById,
                Edge.SourceId,
                Edge.TargetId))
        {
            double ExteriorCorridorX = FlowchartRouteObstacleHelper.ResolveCorridorXBesideVerticalObstacles(
                SourceX,
                SourceY,
                TargetY,
                Edge.SourceId,
                Edge.TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            FlowchartVerticalRouteGuideBuilder.AppendVerticalExteriorCorridorRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                "diamond",
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                ExteriorCorridorX);
            return;
        }

        List<(double X, double Y)> CandidatePoints = [];
        FlowchartVerticalRouteGuideBuilder.AppendVerticalDecisionBranchRoute(
            CandidatePoints,
            SourceX,
            SourceY,
            SourceBounds,
            "diamond",
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape,
            HubCentreX,
            Role,
            IsInbound);

        if (!RouteGeometryIntersectsUnrelatedNodeBodies(
                CandidatePoints,
                BoundsById,
                Edge.SourceId,
                Edge.TargetId))
        {
            LayoutPoints.AddRange(CandidatePoints);
            return;
        }

        double CorridorX = FlowchartRouteObstacleHelper.ResolveCorridorXBesideVerticalObstacles(
            SourceX,
            SourceY,
            TargetY,
            Edge.SourceId,
            Edge.TargetId,
            BoundsById,
            FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
        FlowchartVerticalRouteGuideBuilder.AppendVerticalExteriorCorridorRoute(
            LayoutPoints,
            SourceX,
            SourceY,
            SourceBounds,
            "diamond",
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape,
            CorridorX);
    }

    private static bool RouteGeometryIntersectsUnrelatedNodeBodies(
        List<(double X, double Y)> Points,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        foreach ((double PointX, double PointY) in Points)
        {
            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
            {
                FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
                if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                    || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                        PointX,
                        PointY,
                        PointX,
                        PointY,
                        CandidateBounds))
                {
                    return true;
                }
            }
        }

        for (int Index = 1; Index < Points.Count; Index++)
        {
            (double StartX, double StartY) = Points[Index - 1];
            (double EndX, double EndY) = Points[Index];
            if (FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    StartX,
                    StartY,
                    EndX,
                    EndY,
                    BoundsById,
                    SourceNodeId,
                    TargetNodeId))
            {
                return true;
            }
        }

        return false;
    }
}
