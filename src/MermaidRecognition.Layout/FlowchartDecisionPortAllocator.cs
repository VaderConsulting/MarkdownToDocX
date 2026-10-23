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
        out List<(double X, double Y)> LayoutPoints,
        IReadOnlyList<(double X, double Y)>? IncomingPorts = null)
    {
        LayoutPoints = [];

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (FlowchartBackEdgeHelper.FindBackEdges(Contract).Contains(Edge))
        {
            return false;
        }

        if (FlowchartSharedPortAllocator.IsDecisionShape(SourceShape)
            && FlowchartSharedPortAllocator.CountVisibleEdges(Contract, Edge.SourceId, true) >= 2)
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
                LayoutPoints,
                IncomingPorts);
        }

        if (FlowchartSharedPortAllocator.IsDecisionShape(TargetShape)
            && FlowchartSharedPortAllocator.CountVisibleEdges(Contract, Edge.TargetId, false) >= 2
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
        List<(double X, double Y)> LayoutPoints,
        IReadOnlyList<(double X, double Y)>? IncomingPorts = null)
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

        if (SemanticRouteClass == FlowchartEdgeSemanticRouteClass.CentreSpine)
        {
            bool CompassFanOut = TryPrepareDecisionFanOutBranch(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    Direction,
                    out List<EdgeContract> CompassOutgoing,
                    out _,
                    out _,
                    out bool CompassUseBottomFace,
                    out _,
                    out double CompassCentreX,
                    out double CompassCentreY,
                    out double CompassHalfWidth,
                    out double CompassHalfHeight)
                && TryAssignCompassFanOutPorts(
                    CompassOutgoing,
                    BoundsById,
                    CompassCentreX,
                    CompassCentreY,
                    CompassHalfWidth,
                    CompassHalfHeight,
                    CompassUseBottomFace,
                    out _);
            if (!CompassFanOut
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
            LayoutPoints,
            IncomingPorts);
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
        Outgoing = FlowchartSharedPortAllocator.GetVisibleEdgesFromSource(Contract, Edge.SourceId);
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
            SortOutgoingByTargetCrossAxis(Outgoing, BoundsById, UseDescendingCrossAxis: true, UseCrossAxisY: true);
        }
        else
        {
            SortOutgoingByTargetCrossAxis(Outgoing, BoundsById, UseDescendingCrossAxis: false, UseCrossAxisY: false);
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
        List<(double X, double Y)> LayoutPoints,
        IReadOnlyList<(double X, double Y)>? IncomingPorts = null)
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
            TargetBounds,
            IncomingPorts);

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
        bool IsBypassRejoinFanOut = TryResolveBypassRejoinFanOutVertex(
            Edge,
            Outgoing,
            BoundsById,
            CentreX,
            HorizontalFlow,
            out _);
        bool UseSpreadTopPort = !IsBypassRejoinFanOut
            && SpineContext is not null
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

        bool CompassFanOutPorts = TryAssignCompassFanOutPorts(
            Outgoing,
            BoundsById,
            CentreX,
            CentreY,
            HalfWidth,
            HalfHeight,
            UseBottomFace,
            out _);

        if (!UseSpreadTopPort
            && !CompassFanOutPorts
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
        List<EdgeContract> Incoming = FlowchartSharedPortAllocator.GetVisibleEdgesToTarget(Contract, Edge.TargetId)
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

    [Flags]
    private enum DiamondVertexOccupancy
    {
        None = 0,
        Top = 1,
        Bottom = 2,
        Left = 4,
        Right = 8
    }

    private enum DiamondVertexKind
    {
        Top,
        Bottom,
        Left,
        Right
    }

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
        FlowchartLayoutNodeBounds TargetBounds,
        IReadOnlyList<(double X, double Y)>? IncomingPorts = null)
    {
        List<EdgeContract> Outgoing = FlowchartSharedPortAllocator.GetVisibleEdgesFromSource(Contract, Edge.SourceId);
        if (UseOppositeVerticalPorts)
        {
            Outgoing.Sort((Left, Right) => Left.CreationIndex.CompareTo(Right.CreationIndex));
        }
        else if (HorizontalFlow)
        {
            SortOutgoingByTargetCrossAxis(Outgoing, BoundsById, UseDescendingCrossAxis: true, UseCrossAxisY: true);
        }
        else
        {
            SortOutgoingByTargetCrossAxis(Outgoing, BoundsById, UseDescendingCrossAxis: false, UseCrossAxisY: false);
        }

        Dictionary<string, (double X, double Y, FlowchartDecisionPortRole Role)> PortsByTarget =
            BuildVertexFirstFanOutPorts(
                SourceBounds,
                BoundsById,
                Contract,
                Direction,
                CentreX,
                CentreY,
                HalfWidth,
                HalfHeight,
                UseBottomFace,
                HorizontalFlow,
                Outgoing,
                IncomingPorts);

        if (PortsByTarget.TryGetValue(Edge.TargetId, out (double X, double Y, FlowchartDecisionPortRole Role) Port))
        {
            return Port;
        }

        return ResolveVertexAttach(
            DiamondVertexKind.Bottom,
            CentreX,
            CentreY,
            HalfWidth,
            HalfHeight,
            UseBottomFace);
    }

    /// <summary>
    /// B12b: diamond vertices are first ports; edge centres only when suitable vertices are taken.
    /// </summary>
    private static Dictionary<string, (double X, double Y, FlowchartDecisionPortRole Role)> BuildVertexFirstFanOutPorts(
        FlowchartLayoutNodeBounds SourceBounds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        DiagramContract Contract,
        FlowchartDirection Direction,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool UseBottomFace,
        bool HorizontalFlow,
        List<EdgeContract> Outgoing,
        IReadOnlyList<(double X, double Y)>? IncomingPorts = null)
    {
        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        DiamondVertexOccupancy Taken = DiamondVertexOccupancy.None;
        if (IncomingPorts is null)
        {
            Taken = ResolveIncomingVertexOccupancy(Contract, BoundsById, SourceBounds,
                CentreX, CentreY, HalfWidth, HalfHeight, HorizontalFlow);
        }
        else
        {
            // Grid routes have already allocated their inbound ports. Use those
            // endpoints rather than inferring a vertex from the parent's centre.
            foreach (var Port in IncomingPorts)
            {
                if (Math.Abs(Port.X - (CentreX - HalfWidth)) < 1 && Math.Abs(Port.Y - CentreY) < 1)
                    Taken |= DiamondVertexOccupancy.Left;
                if (Math.Abs(Port.X - (CentreX + HalfWidth)) < 1 && Math.Abs(Port.Y - CentreY) < 1)
                    Taken |= DiamondVertexOccupancy.Right;
                if (Math.Abs(Port.Y - (CentreY - HalfHeight)) < 1 && Math.Abs(Port.X - CentreX) < 1)
                    Taken |= DiamondVertexOccupancy.Bottom;
                if (Math.Abs(Port.Y - (CentreY + HalfHeight)) < 1 && Math.Abs(Port.X - CentreX) < 1)
                    Taken |= DiamondVertexOccupancy.Top;
            }
        }

        Dictionary<string, (double X, double Y, FlowchartDecisionPortRole Role)> PortsByTarget =
            new Dictionary<string, (double X, double Y, FlowchartDecisionPortRole Role)>(StringComparer.Ordinal);

        if (TryAssignCompassFanOutPorts(Outgoing, BoundsById, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace,
                out Dictionary<string, (double X, double Y, FlowchartDecisionPortRole Role)> CompassPorts))
        {
            foreach (KeyValuePair<string, (double X, double Y, FlowchartDecisionPortRole Role)> Entry in CompassPorts)
            {
                PortsByTarget[Entry.Key] = Entry.Value;
            }

            return PortsByTarget;
        }

        foreach (EdgeContract OutgoingEdge in Outgoing)
        {
            if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            DiamondVertexKind Preferred = ResolvePreferredVertexForTarget(
                TargetBounds,
                CentreX,
                CentreY,
                UseBottomFace,
                HorizontalFlow,
                RightToLeft);
            if (TryResolveBypassRejoinFanOutVertex(
                    OutgoingEdge,
                    Outgoing,
                    BoundsById,
                    CentreX,
                    HorizontalFlow,
                    out DiamondVertexKind BypassVertex))
            {
                Preferred = BypassVertex;
            }
            if (ShouldUseSlantedEdgeCentrePort(
                    Preferred,
                    TargetBounds,
                    CentreX,
                    CentreY,
                    HalfWidth,
                    HorizontalFlow))
            {
                PortsByTarget[OutgoingEdge.TargetId] = ResolveEdgeCentreFallbackAttach(
                    Preferred,
                    CentreX,
                    CentreY,
                    HalfWidth,
                    HalfHeight,
                    UseBottomFace);
                continue;
            }

            PortsByTarget[OutgoingEdge.TargetId] = AllocatePreferredVertexAttach(
                Preferred,
                ref Taken,
                CentreX,
                CentreY,
                HalfWidth,
                HalfHeight,
                UseBottomFace);
        }

        return PortsByTarget;
    }

    private static bool TryHasCoRankedDecisionSibling(
        List<EdgeContract> Outgoing,
        EdgeContract Edge,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (!BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        foreach (EdgeContract OtherEdge in Outgoing)
        {
            if (string.Equals(OtherEdge.TargetId, Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(OtherEdge.TargetId, out FlowchartLayoutNodeBounds CandidateBounds))
            {
                continue;
            }

            if (Math.Abs(CandidateBounds.CenterY - TargetBounds.CenterY) <= COLLINEAR_EPSILON_PX)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A9: when a bypass target shares a rank with a sibling branch child, pick the side port
    /// facing that target instead of treating it as a collinear spine drop.
    /// </summary>
    private static bool TryResolveBypassRejoinFanOutVertex(
        EdgeContract Edge,
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double CentreX,
        bool HorizontalFlow,
        out DiamondVertexKind Vertex)
    {
        Vertex = DiamondVertexKind.Bottom;
        if (HorizontalFlow || Outgoing.Count != 2)
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        FlowchartLayoutNodeBounds? SiblingBounds = null;
        foreach (EdgeContract OtherEdge in Outgoing)
        {
            if (string.Equals(OtherEdge.TargetId, Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(OtherEdge.TargetId, out FlowchartLayoutNodeBounds CandidateBounds))
            {
                continue;
            }

            if (Math.Abs(CandidateBounds.CenterY - TargetBounds.CenterY) <= COLLINEAR_EPSILON_PX)
            {
                SiblingBounds = CandidateBounds;
                break;
            }
        }

        if (SiblingBounds is null)
        {
            return false;
        }

        double TargetDx = TargetBounds.CenterX - CentreX;
        if (TargetDx < -SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            Vertex = DiamondVertexKind.Left;
            return true;
        }

        if (TargetDx > SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            Vertex = DiamondVertexKind.Right;
            return true;
        }

        double SiblingDx = SiblingBounds.CenterX - CentreX;
        if (SiblingDx < -SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            Vertex = DiamondVertexKind.Right;
            return true;
        }

        if (SiblingDx > SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            Vertex = DiamondVertexKind.Left;
            return true;
        }

        return false;
    }

    /// <summary>
    /// B16: when a side vertex would turn downward immediately, use the lower slanted edge centre
    /// so the first corner sits at least one corner radius plus one grid step from the vertex.
    /// </summary>
    private static bool ShouldUseSlantedEdgeCentrePort(
        DiamondVertexKind Preferred,
        FlowchartLayoutNodeBounds TargetBounds,
        double CentreX,
        double CentreY,
        double HalfWidth,
        bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return false;
        }

        if (Preferred != DiamondVertexKind.Left && Preferred != DiamondVertexKind.Right)
        {
            return false;
        }

        double MinimumStubPx = DiagramGridRouteBuilder.CORNER_RADIUS_PX + DiagramGridMetrics.GRID_UNIT_PX;
        if (TargetBounds.CenterY >= CentreY - COLLINEAR_EPSILON_PX)
        {
            return false;
        }

        double VertexX = Preferred == DiamondVertexKind.Left ? CentreX - HalfWidth : CentreX + HalfWidth;
        double HorizontalSpan = Math.Abs(TargetBounds.CenterX - VertexX);
        return HorizontalSpan < MinimumStubPx;
    }

    private static DiamondVertexOccupancy ResolveIncomingVertexOccupancy(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool HorizontalFlow)
    {
        DiamondVertexOccupancy Taken = DiamondVertexOccupancy.None;
        foreach (EdgeContract IncomingEdge in Contract.Edges)
        {
            if (!string.Equals(IncomingEdge.TargetId, SourceBounds.NodeId, StringComparison.Ordinal)
                || string.Equals(IncomingEdge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds InboundSource))
            {
                continue;
            }

            Taken |= InferIncomingVertex(
                InboundSource,
                CentreX,
                CentreY,
                HalfWidth,
                HalfHeight,
                HorizontalFlow);
        }

        return Taken;
    }

    private static DiamondVertexOccupancy InferIncomingVertex(
        FlowchartLayoutNodeBounds InboundSource,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool HorizontalFlow)
    {
        double DeltaX = InboundSource.CenterX - CentreX;
        double DeltaY = InboundSource.CenterY - CentreY;
        if (HorizontalFlow)
        {
            if (DeltaX < -HalfWidth * 0.35)
            {
                return DiamondVertexOccupancy.Left;
            }

            if (DeltaX > HalfWidth * 0.35)
            {
                return DiamondVertexOccupancy.Right;
            }

            if (Math.Abs(DeltaY) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return DiamondVertexOccupancy.Left;
            }

            return DeltaY > 0.0 ? DiamondVertexOccupancy.Top : DiamondVertexOccupancy.Bottom;
        }

        if (DeltaY > HalfHeight * 0.35)
        {
            return DiamondVertexOccupancy.Top;
        }

        if (DeltaY < -HalfHeight * 0.35)
        {
            return DiamondVertexOccupancy.Bottom;
        }

        if (Math.Abs(DeltaX) <= SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            return DiamondVertexOccupancy.Top;
        }

        return DeltaX < 0.0 ? DiamondVertexOccupancy.Left : DiamondVertexOccupancy.Right;
    }

    private static DiamondVertexKind ResolvePreferredVertexForTarget(
        FlowchartLayoutNodeBounds TargetBounds,
        double CentreX,
        double CentreY,
        bool UseBottomFace,
        bool HorizontalFlow,
        bool RightToLeft)
    {
        if (HorizontalFlow)
        {
            double TargetDy = TargetBounds.CenterY - CentreY;
            if (Math.Abs(TargetDy) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return RightToLeft ? DiamondVertexKind.Left : DiamondVertexKind.Right;
            }

            return TargetDy > 0.0 ? DiamondVertexKind.Top : DiamondVertexKind.Bottom;
        }

        double TargetDx = TargetBounds.CenterX - CentreX;
        if (Math.Abs(TargetDx) <= SIDE_BRANCH_CROSS_AXIS_EPSILON_PX)
        {
            return UseBottomFace ? DiamondVertexKind.Bottom : DiamondVertexKind.Top;
        }

        return TargetDx < 0.0 ? DiamondVertexKind.Left : DiamondVertexKind.Right;
    }

    private static (double X, double Y, FlowchartDecisionPortRole Role) AllocatePreferredVertexAttach(
        DiamondVertexKind Preferred,
        ref DiamondVertexOccupancy Taken,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool UseBottomFace)
    {
        if (!IsVertexTaken(Taken, Preferred))
        {
            Taken |= VertexToOccupancy(Preferred);
            return ResolveVertexAttach(Preferred, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
        }

        return ResolveEdgeCentreFallbackAttach(Preferred, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
    }

    private static bool IsVertexTaken(DiamondVertexOccupancy Taken, DiamondVertexKind Vertex) =>
        (Taken & VertexToOccupancy(Vertex)) != 0;

    private static DiamondVertexOccupancy VertexToOccupancy(DiamondVertexKind Vertex) => Vertex switch
    {
        DiamondVertexKind.Top => DiamondVertexOccupancy.Top,
        DiamondVertexKind.Bottom => DiamondVertexOccupancy.Bottom,
        DiamondVertexKind.Left => DiamondVertexOccupancy.Left,
        DiamondVertexKind.Right => DiamondVertexOccupancy.Right,
        _ => DiamondVertexOccupancy.None
    };

    private static (double X, double Y, FlowchartDecisionPortRole Role) ResolveVertexAttach(
        DiamondVertexKind Vertex,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool UseBottomFace)
    {
        return Vertex switch
        {
            DiamondVertexKind.Top => (CentreX, CentreY + HalfHeight, FlowchartDecisionPortRole.TopVertex),
            DiamondVertexKind.Bottom => (CentreX, CentreY - HalfHeight, FlowchartDecisionPortRole.BottomVertex),
            DiamondVertexKind.Left => (CentreX - HalfWidth, CentreY, FlowchartDecisionPortRole.SideLeft),
            DiamondVertexKind.Right => (CentreX + HalfWidth, CentreY, FlowchartDecisionPortRole.SideRight),
            _ => (CentreX, UseBottomFace ? CentreY - HalfHeight : CentreY + HalfHeight, FlowchartDecisionPortRole.BottomVertex)
        };
    }

    private static (double X, double Y, FlowchartDecisionPortRole Role) ResolveEdgeCentreFallbackAttach(
        DiamondVertexKind Side,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool UseBottomFace)
    {
        return Side switch
        {
            DiamondVertexKind.Top => (
                CentreX,
                CentreY + (HalfHeight / 2.0),
                FlowchartDecisionPortRole.TopVertex),
            DiamondVertexKind.Bottom => (
                CentreX,
                CentreY - (HalfHeight / 2.0),
                FlowchartDecisionPortRole.BottomVertex),
            DiamondVertexKind.Left => (
                CentreX - (HalfWidth / 2.0),
                UseBottomFace ? CentreY - (HalfHeight / 2.0) : CentreY + (HalfHeight / 2.0),
                FlowchartDecisionPortRole.SideLeft),
            DiamondVertexKind.Right => (
                CentreX + (HalfWidth / 2.0),
                UseBottomFace ? CentreY - (HalfHeight / 2.0) : CentreY + (HalfHeight / 2.0),
                FlowchartDecisionPortRole.SideRight),
            _ => (CentreX, CentreY - HalfHeight, FlowchartDecisionPortRole.BottomVertex)
        };
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
            return (CentreX - HalfWidth, CentreY);
        }

        if (Role == FlowchartDecisionPortRole.SideRight)
        {
            return (CentreX + HalfWidth, CentreY);
        }

        return (CentreX + (HalfWidth / 2.0), CentreY + (HalfHeight / 2.0));
    }

    private static bool TryAssignCompassFanOutPorts(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        bool UseBottomFace,
        out Dictionary<string, (double X, double Y, FlowchartDecisionPortRole Role)> PortsByTarget)
    {
        PortsByTarget = new Dictionary<string, (double X, double Y, FlowchartDecisionPortRole Role)>(StringComparer.Ordinal);
        if (Outgoing.Count == 0)
        {
            return false;
        }

        EdgeContract? YesEdge = null;
        EdgeContract? NoEdge = null;
        foreach (EdgeContract Edge in Outgoing)
        {
            if (FlowchartDecisionBranchLabelHelper.IsYesBranchLabel(Edge.Label)) YesEdge = Edge;
            if (FlowchartDecisionBranchLabelHelper.IsNoBranchLabel(Edge.Label)) NoEdge = Edge;
        }

        if (Outgoing.Count == 2 && YesEdge != null && NoEdge != null)
        {
            PortsByTarget[YesEdge.TargetId] = ResolveVertexAttach(
                DiamondVertexKind.Bottom, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
            PortsByTarget[NoEdge.TargetId] = ResolveVertexAttach(
                DiamondVertexKind.Right, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
            return true;
        }

        if (Outgoing.Count == 3)
        {
            List<EdgeContract> Ordered = Outgoing
                .OrderBy(Edge => BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds? Box) ? Box.CenterX : 0.0)
                .ThenBy(Edge => Edge.CreationIndex)
                .ToList();
            PortsByTarget[Ordered[0].TargetId] = ResolveVertexAttach(
                DiamondVertexKind.Left, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
            PortsByTarget[Ordered[1].TargetId] = ResolveVertexAttach(
                DiamondVertexKind.Bottom, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
            PortsByTarget[Ordered[2].TargetId] = ResolveVertexAttach(
                DiamondVertexKind.Right, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
            return true;
        }

        if (Outgoing.Count == 2)
        {
            List<EdgeContract> Ordered = Outgoing.OrderBy(Edge => Edge.CreationIndex).ToList();
            PortsByTarget[Ordered[0].TargetId] = ResolveVertexAttach(
                DiamondVertexKind.Bottom, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
            PortsByTarget[Ordered[1].TargetId] = ResolveVertexAttach(
                DiamondVertexKind.Right, CentreX, CentreY, HalfWidth, HalfHeight, UseBottomFace);
            return true;
        }

        return false;
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
        bool UseDescendingCrossAxis,
        bool UseCrossAxisY) => Outgoing.Sort((Left, Right) =>
                                             {
                                                 double LeftValue = UseCrossAxisY ? GetCrossAxisY(Left.TargetId, BoundsById) : GetCrossAxis(Left.TargetId, BoundsById);
                                                 double RightValue = UseCrossAxisY ? GetCrossAxisY(Right.TargetId, BoundsById) : GetCrossAxis(Right.TargetId, BoundsById);
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

    private static double GetCrossAxisY(
        string NodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
        {
            return Bounds.CenterY;
        }

        return 0.0;
    }

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
