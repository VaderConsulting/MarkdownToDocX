using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Computes layout-space validation facts for flowchart edges from resolved geometry.
/// </summary>
public static class FlowchartEdgeValidationFactsBuilder
{
    private const double BOUNDARY_TOLERANCE_PX = 1.5;

    private const double CENTRE_TERMINATION_MAX_PX = 4.0;

    private const double STRAIGHT_DRIFT_TOLERANCE_PX = 0.75;

    private const double LABEL_SPINE_TOLERANCE_PX = 2.5;

    private const double LABEL_MIDSPAN_TOLERANCE_PX = 3.0;

    private const double LABEL_BRANCH_SEGMENT_RATIO = 0.62;

    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Per-edge validation facts produced from layout geometry.
    /// </summary>
    public sealed class FlowchartEdgeValidationFacts
    {
        public bool TerminateAtShapeBoundary { get; set; }

        public bool TerminateAtVisibleBoundary { get; set; }

        public bool CrossEndpointStandoffValid { get; set; }

        public bool StraightWithinTolerance { get; set; }

        public bool LabelCentredOnConnectorSpine { get; set; }

        public bool LabelNotOnConnectorLine { get; set; }

        public bool LabelHalfPolylineMidspan { get; set; }

        public bool LabelNearBranchSegment { get; set; }
    }

    /// <summary>
    /// Diagram-level validation facts for edge sets and clusters.
    /// </summary>
    public sealed class FlowchartDiagramValidationFacts
    {
        public bool SiblingBranchesDoNotCross { get; set; } = true;

        public Dictionary<string, bool> ClusterIntersectionByEdgeId { get; } =
            new Dictionary<string, bool>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Builds per-edge validation facts for one laid-out diagram.
    /// </summary>
    public static Dictionary<string, FlowchartEdgeValidationFacts> BuildEdgeFacts(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction,
        IReadOnlyDictionary<string, string> RouteClassByEdgeId)
    {
        Dictionary<string, FlowchartEdgeValidationFacts> FactsByEdgeId =
            new Dictionary<string, FlowchartEdgeValidationFacts>(StringComparer.Ordinal);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string EdgeKey = BuildEdgeKey(Edge);
            FlowchartEdgeValidationFacts Facts = new FlowchartEdgeValidationFacts
            {
                TerminateAtShapeBoundary = false,
                TerminateAtVisibleBoundary = false,
                CrossEndpointStandoffValid = true,
                StraightWithinTolerance = false,
                LabelCentredOnConnectorSpine = true,
                LabelNotOnConnectorLine = true,
                LabelHalfPolylineMidspan = true,
                LabelNearBranchSegment = true
            };

            if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                FactsByEdgeId[EdgeKey] = Facts;
                continue;
            }

            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind RouteKind))
            {
                FactsByEdgeId[EdgeKey] = Facts;
                continue;
            }

            NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode);
            NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode);
            string? SourceShape = SourceNode?.Shape;
            string? TargetShape = TargetNode?.Shape;

            ApplyBoundaryFacts(
                Facts,
                Edge,
                LayoutPoints,
                SourceBounds,
                TargetBounds,
                SourceShape,
                TargetShape);

            Facts.StraightWithinTolerance = EvaluateStraightWithinTolerance(
                LayoutPoints,
                RouteKind,
                SourceBounds,
                TargetBounds,
                Direction,
                RouteClassByEdgeId.TryGetValue(EdgeKey, out string? RouteClass) ? RouteClass : "Unknown");

            if (!string.IsNullOrWhiteSpace(Edge.Label))
            {
                ApplyLabelFacts(
                    Facts,
                    Edge,
                    Contract,
                    BoundsById,
                    LayoutPoints,
                    SourceBounds,
                    TargetBounds,
                    Direction,
                    SourceShape,
                    RouteClassByEdgeId.TryGetValue(EdgeKey, out string? LabelRouteClass) ? LabelRouteClass : "Unknown");
            }

            FactsByEdgeId[EdgeKey] = Facts;
        }

        return FactsByEdgeId;
    }

    /// <summary>
    /// Builds diagram-level validation facts such as sibling-branch crossing.
    /// </summary>
    public static FlowchartDiagramValidationFacts BuildDiagramFacts(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction,
        IReadOnlyDictionary<string, string> RouteClassByEdgeId)
    {
        FlowchartDiagramValidationFacts DiagramFacts = new FlowchartDiagramValidationFacts();
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        Dictionary<string, List<(EdgeContract Edge, List<(double X, double Y)> Points)>> BranchesBySource =
            new Dictionary<string, List<(EdgeContract Edge, List<(double X, double Y)> Points)>>(StringComparer.Ordinal);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string EdgeKey = BuildEdgeKey(Edge);
            if (!RouteClassByEdgeId.TryGetValue(EdgeKey, out string? RouteClass)
                || !string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal))
            {
                continue;
            }

            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind _))
            {
                continue;
            }

            if (!BranchesBySource.ContainsKey(Edge.SourceId))
            {
                BranchesBySource[Edge.SourceId] = new List<(EdgeContract Edge, List<(double X, double Y)> Points)>();
            }

            BranchesBySource[Edge.SourceId].Add((Edge, LayoutPoints));
        }

        foreach (KeyValuePair<string, List<(EdgeContract Edge, List<(double X, double Y)> Points)>> Entry in BranchesBySource)
        {
            List<(EdgeContract Edge, List<(double X, double Y)> Points)> Branches = Entry.Value;
            if (Branches.Count < 2)
            {
                continue;
            }

            for (int LeftIndex = 0; LeftIndex < Branches.Count - 1; LeftIndex++)
            {
                for (int RightIndex = LeftIndex + 1; RightIndex < Branches.Count; RightIndex++)
                {
                    if (PolylinesCross(Branches[LeftIndex].Points, Branches[RightIndex].Points))
                    {
                        DiagramFacts.SiblingBranchesDoNotCross = false;
                        return DiagramFacts;
                    }
                }
            }
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string EdgeKey = BuildEdgeKey(Edge);
            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind _))
            {
                DiagramFacts.ClusterIntersectionByEdgeId[EdgeKey] = true;
                continue;
            }

            DiagramFacts.ClusterIntersectionByEdgeId[EdgeKey] =
                !PathIntersectsUnrelatedClusterBodies(Edge, LayoutPoints, Contract, BoundsById);
        }

        return DiagramFacts;
    }

    private static void ApplyBoundaryFacts(
        FlowchartEdgeValidationFacts Facts,
        EdgeContract Edge,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape)
    {
        if (LayoutPoints.Count < 2)
        {
            return;
        }

        (double SourcePortX, double SourcePortY) = LayoutPoints[0];
        (double TargetPortX, double TargetPortY) = LayoutPoints[LayoutPoints.Count - 1];

        bool SourceAtCentre = DistanceToCentre(SourcePortX, SourcePortY, SourceBounds) <= CENTRE_TERMINATION_MAX_PX;
        bool TargetAtCentre = DistanceToCentre(TargetPortX, TargetPortY, TargetBounds) <= CENTRE_TERMINATION_MAX_PX;
        Facts.TerminateAtShapeBoundary = !SourceAtCentre && !TargetAtCentre;

        bool SourceVisible = EndpointMatchesResolvedAttach(
            SourcePortX,
            SourcePortY,
            SourceBounds,
            SourceShape,
            TargetBounds,
            TargetPortX,
            TargetPortY,
            Edge.MarkerStart,
            IsOutbound: true);
        bool TargetVisible = EndpointMatchesResolvedAttach(
            TargetPortX,
            TargetPortY,
            TargetBounds,
            TargetShape,
            SourceBounds,
            SourcePortX,
            SourcePortY,
            Edge.MarkerEnd,
            IsOutbound: false);
        Facts.TerminateAtVisibleBoundary = SourceVisible && TargetVisible;

        Facts.CrossEndpointStandoffValid =
            CrossStandoffValid(SourcePortX, SourcePortY, SourceBounds, SourceShape, TargetPortX, TargetPortY, Edge.MarkerStart)
            && CrossStandoffValid(TargetPortX, TargetPortY, TargetBounds, TargetShape, SourcePortX, SourcePortY, Edge.MarkerEnd);
    }

    private static bool EndpointMatchesResolvedAttach(
        double PortX,
        double PortY,
        FlowchartLayoutNodeBounds NodeBounds,
        string? Shape,
        FlowchartLayoutNodeBounds PeerBounds,
        double OtherPortX,
        double OtherPortY,
        EdgeEndpointMarker EndpointMarker,
        bool IsOutbound)
    {
        if (EndpointMatchesCentreToPeerClip(
                PortX,
                PortY,
                NodeBounds,
                Shape,
                PeerBounds,
                EndpointMarker,
                IsOutbound))
        {
            return true;
        }

        double ExpectedX;
        double ExpectedY;
        bool HasExpectedAttach;
        if (IsOutbound)
        {
            HasExpectedAttach = FlowchartShapeBoundaryAttachHelper.TryResolveOutboundAttach(
                NodeBounds,
                Shape,
                OtherPortX,
                OtherPortY,
                EndpointMarker,
                out ExpectedX,
                out ExpectedY);
        }
        else
        {
            HasExpectedAttach = FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                NodeBounds,
                Shape,
                OtherPortX,
                OtherPortY,
                EndpointMarker,
                out ExpectedX,
                out ExpectedY);
        }

        if (HasExpectedAttach && EndpointWithinTolerance(PortX, PortY, ExpectedX, ExpectedY))
        {
            return true;
        }

        double DirX = PortX - NodeBounds.CenterX;
        double DirY = PortY - NodeBounds.CenterY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return false;
        }

        DirX /= Length;
        DirY /= Length;

        (double ExitX, double ExitY) = FlowchartShapeLayoutHelper.RayExitShape(
            NodeBounds.CenterX,
            NodeBounds.CenterY,
            NodeBounds.Width,
            NodeBounds.Height,
            Shape,
            DirX,
            DirY);

        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(EndpointMarker);
        ExpectedX = ExitX + (DirX * GapPx);
        ExpectedY = ExitY + (DirY * GapPx);
        return EndpointWithinTolerance(PortX, PortY, ExpectedX, ExpectedY);
    }

    private static bool EndpointMatchesCentreToPeerClip(
        double PortX,
        double PortY,
        FlowchartLayoutNodeBounds NodeBounds,
        string? Shape,
        FlowchartLayoutNodeBounds PeerBounds,
        EdgeEndpointMarker EndpointMarker,
        bool IsOutbound)
    {
        _ = IsOutbound;

        double TowardPeerX = PeerBounds.CenterX - NodeBounds.CenterX;
        double TowardPeerY = PeerBounds.CenterY - NodeBounds.CenterY;
        double Length = Math.Sqrt((TowardPeerX * TowardPeerX) + (TowardPeerY * TowardPeerY));
        if (Length < 1e-6)
        {
            return false;
        }

        TowardPeerX /= Length;
        TowardPeerY /= Length;

        (double ExitX, double ExitY) = FlowchartShapeLayoutHelper.RayExitShape(
            NodeBounds.CenterX,
            NodeBounds.CenterY,
            NodeBounds.Width,
            NodeBounds.Height,
            Shape,
            TowardPeerX,
            TowardPeerY);

        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(EndpointMarker);
        double ExpectedX = ExitX + (TowardPeerX * GapPx);
        double ExpectedY = ExitY + (TowardPeerY * GapPx);
        return EndpointWithinTolerance(PortX, PortY, ExpectedX, ExpectedY);
    }

    private static bool EndpointWithinTolerance(
        double PortX,
        double PortY,
        double ExpectedX,
        double ExpectedY)
    {
        double DeltaX = PortX - ExpectedX;
        double DeltaY = PortY - ExpectedY;
        double Distance = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        return Distance <= BOUNDARY_TOLERANCE_PX;
    }

    private static bool CrossStandoffValid(
        double PortX,
        double PortY,
        FlowchartLayoutNodeBounds NodeBounds,
        string? Shape,
        double OtherPortX,
        double OtherPortY,
        EdgeEndpointMarker EndpointMarker)
    {
        if (EndpointMarker != EdgeEndpointMarker.Cross)
        {
            return true;
        }

        double DirX = OtherPortX - NodeBounds.CenterX;
        double DirY = OtherPortY - NodeBounds.CenterY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return false;
        }

        DirX /= Length;
        DirY /= Length;

        (double ExitX, double ExitY) = FlowchartShapeLayoutHelper.RayExitShape(
            NodeBounds.CenterX,
            NodeBounds.CenterY,
            NodeBounds.Width,
            NodeBounds.Height,
            Shape,
            DirX,
            DirY);

        double StandoffDistance = Math.Sqrt(
            ((PortX - ExitX) * (PortX - ExitX))
            + ((PortY - ExitY) * (PortY - ExitY)));
        double ExpectedGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(EdgeEndpointMarker.Cross);
        return Math.Abs(StandoffDistance - ExpectedGap) <= BOUNDARY_TOLERANCE_PX;
    }

    private static bool EvaluateStraightWithinTolerance(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        string RouteClass)
    {
        if (LayoutPoints.Count < 2)
        {
            return false;
        }

        (double StartX, double StartY) = LayoutPoints[0];
        (double EndX, double EndY) = LayoutPoints[LayoutPoints.Count - 1];
        bool ExpectStraight = FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
            StartX,
            StartY,
            EndX,
            EndY);

        if (!ExpectStraight)
        {
            return true;
        }

        if (RouteKind == FlowchartEdgeRouteKind.CrossGroup
            || RouteKind == FlowchartEdgeRouteKind.Corridor
            || RouteKind == FlowchartEdgeRouteKind.RetryLoop)
        {
            return true;
        }

        if (string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal)
            && LayoutPoints.Count >= 4
            && FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(LayoutPoints))
        {
            return true;
        }

        if (RouteKind == FlowchartEdgeRouteKind.Straight && LayoutPoints.Count == 2)
        {
            return true;
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        double SpineCoordinate = VerticalFlow
            ? (StartX + EndX) / 2.0
            : (StartY + EndY) / 2.0;

        foreach ((double PointX, double PointY) in LayoutPoints)
        {
            double CrossAxisDelta = VerticalFlow
                ? Math.Abs(PointX - SpineCoordinate)
                : Math.Abs(PointY - SpineCoordinate);
            if (CrossAxisDelta > STRAIGHT_DRIFT_TOLERANCE_PX)
            {
                return false;
            }
        }

        if (string.Equals(RouteClass, "CentreSpine", StringComparison.Ordinal)
            || string.Equals(RouteClass, "ShortAdjacent", StringComparison.Ordinal))
        {
            return LayoutPoints.Count <= 2;
        }

        return true;
    }

    private static void ApplyLabelFacts(
        FlowchartEdgeValidationFacts Facts,
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        string? SourceShape,
        string RouteClass)
    {
        bool IsLrLabelledRectFanOut = FlowchartFanOutPortAllocator.IsLrLabelledRectFanOutEdge(
            Edge,
            Contract,
            BoundsById,
            SourceShape,
            Direction);

        (double LabelX, double LabelY) = FlowchartEdgeLabelPlacementHelper.ResolveEdgeLabelPosition(
            SourceBounds,
            TargetBounds,
            LayoutPoints,
            Direction,
            SourceShape,
            Edge.Label,
            Edge,
            Contract,
            BoundsById,
            RouteClass);

        bool StraightConnector = LayoutPoints.Count >= 2
            && FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                LayoutPoints[0].X,
                LayoutPoints[0].Y,
                LayoutPoints[LayoutPoints.Count - 1].X,
                LayoutPoints[LayoutPoints.Count - 1].Y);

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;

        if (IsLrLabelledRectFanOut)
        {
            (double ExpectedLabelX, double ExpectedLabelY) =
                FlowchartEdgeLabelPlacementHelper.ComputeLrLabelledRectFanOutEdgeLabelPosition(
                    LayoutPoints,
                    SourceBounds,
                    TargetBounds);
            Facts.LabelCentredOnConnectorSpine =
                Math.Abs(LabelX - ExpectedLabelX) <= LABEL_SPINE_TOLERANCE_PX
                && Math.Abs(LabelY - ExpectedLabelY) <= LABEL_SPINE_TOLERANCE_PX;
            Facts.LabelNotOnConnectorLine = true;
        }
        else if (string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal))
        {
            if (FlowchartEdgeLabelPlacementHelper.HasExteriorCorridorDetourForValidation(LayoutPoints, Direction))
            {
                Facts.LabelCentredOnConnectorSpine = true;
                Facts.LabelNotOnConnectorLine = true;
            }
            else
            {
                (double ExpectedLabelX, double ExpectedLabelY) =
                    FlowchartEdgeLabelPlacementHelper.ComputeLocalDecisionBranchLabelExpectedPosition(
                        LayoutPoints,
                        SourceBounds,
                        Edge.Label,
                        Direction);
                if (VerticalFlow)
                {
                    Facts.LabelCentredOnConnectorSpine =
                        Math.Abs(LabelX - ExpectedLabelX) <= LABEL_SPINE_TOLERANCE_PX;
                    Facts.LabelNotOnConnectorLine = true;
                }
                else
                {
                    Facts.LabelCentredOnConnectorSpine = true;
                    double DistanceToPolyline = ComputePointToPolylineDistance(LabelX, LabelY, LayoutPoints);
                    Facts.LabelNotOnConnectorLine = DistanceToPolyline >= COLLINEAR_EPSILON_PX;
                }
            }
        }
        else if (StraightConnector && VerticalFlow)
        {
            double SpineX = (LayoutPoints[0].X + LayoutPoints[LayoutPoints.Count - 1].X) / 2.0;
            Facts.LabelCentredOnConnectorSpine = Math.Abs(LabelX - SpineX) <= LABEL_SPINE_TOLERANCE_PX;
            Facts.LabelNotOnConnectorLine = true;
        }
        else if (StraightConnector)
        {
            double SpineY = (LayoutPoints[0].Y + LayoutPoints[LayoutPoints.Count - 1].Y) / 2.0;
            Facts.LabelCentredOnConnectorSpine = Math.Abs(LabelY - SpineY) <= LABEL_SPINE_TOLERANCE_PX;
            Facts.LabelNotOnConnectorLine = true;
        }
        else
        {
            Facts.LabelCentredOnConnectorSpine = true;
            double DistanceToPolyline = ComputePointToPolylineDistance(LabelX, LabelY, LayoutPoints);
            Facts.LabelNotOnConnectorLine = DistanceToPolyline >= COLLINEAR_EPSILON_PX;
        }

        if (string.Equals(RouteClass, "LongBypass", StringComparison.Ordinal))
        {
            Facts.LabelHalfPolylineMidspan = true;
        }
        else if (string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase)
            && LayoutPoints.Count >= 4)
        {
            Facts.LabelHalfPolylineMidspan = true;
        }
        else if (string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal)
            && FlowchartEdgeLabelPlacementHelper.HasExteriorCorridorDetourForValidation(LayoutPoints, Direction))
        {
            Facts.LabelHalfPolylineMidspan = true;
        }
        else if (string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal))
        {
            (double ExpectedMidX, double ExpectedMidY) =
                FlowchartEdgeLabelPlacementHelper.ComputeLocalDecisionBranchLabelExpectedPosition(
                    LayoutPoints,
                    SourceBounds,
                    Edge.Label,
                    Direction);
            double MidspanDeltaX = LabelX - ExpectedMidX;
            double MidspanDeltaY = LabelY - ExpectedMidY;
            double MidspanDistance = Math.Sqrt((MidspanDeltaX * MidspanDeltaX) + (MidspanDeltaY * MidspanDeltaY));
            Facts.LabelHalfPolylineMidspan = MidspanDistance <= LABEL_MIDSPAN_TOLERANCE_PX;
        }
        else if (IsLrLabelledRectFanOut)
        {
            (double ExpectedMidX, double ExpectedMidY) =
                FlowchartEdgeLabelPlacementHelper.ComputeLrLabelledRectFanOutEdgeLabelPosition(
                    SourceBounds,
                    TargetBounds);
            double MidspanDeltaX = LabelX - ExpectedMidX;
            double MidspanDeltaY = LabelY - ExpectedMidY;
            double MidspanDistance = Math.Sqrt((MidspanDeltaX * MidspanDeltaX) + (MidspanDeltaY * MidspanDeltaY));
            Facts.LabelHalfPolylineMidspan = MidspanDistance <= LABEL_MIDSPAN_TOLERANCE_PX;
        }
        else
        {
            (double ExpectedMidX, double ExpectedMidY) = StraightConnector
                ? FlowchartEdgeLabelPlacementHelper.ComputePolylineHalfDistancePosition(LayoutPoints)
                : FlowchartEdgeLabelPlacementHelper.ComputeOffsetLabelPosition(LayoutPoints);
            double MidspanDeltaX = LabelX - ExpectedMidX;
            double MidspanDeltaY = LabelY - ExpectedMidY;
            double MidspanDistance = Math.Sqrt((MidspanDeltaX * MidspanDeltaX) + (MidspanDeltaY * MidspanDeltaY));
            double MidspanTolerance = StraightConnector
                ? LABEL_MIDSPAN_TOLERANCE_PX
                : FlowchartEdgeLabelPlacementHelper.DEFAULT_OFFSET_PX + LABEL_MIDSPAN_TOLERANCE_PX;
            Facts.LabelHalfPolylineMidspan = MidspanDistance <= MidspanTolerance;
        }

        if (string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal)
            && FlowchartEdgeLabelPlacementHelper.HasExteriorCorridorDetourForValidation(LayoutPoints, Direction))
        {
            Facts.LabelNearBranchSegment = true;
        }
        else if (string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal)
            && LayoutPoints.Count >= 2)
        {
            (double ExpectedBranchX, double ExpectedBranchY) =
                FlowchartEdgeLabelPlacementHelper.ComputeLocalDecisionBranchLabelExpectedPosition(
                    LayoutPoints,
                    SourceBounds,
                    Edge.Label,
                    Direction);
            double BranchDeltaX = LabelX - ExpectedBranchX;
            double BranchDeltaY = LabelY - ExpectedBranchY;
            double BranchDistance = Math.Sqrt((BranchDeltaX * BranchDeltaX) + (BranchDeltaY * BranchDeltaY));
            Facts.LabelNearBranchSegment = BranchDistance <= LABEL_MIDSPAN_TOLERANCE_PX;
        }
    }

    private static bool PathIntersectsUnrelatedClusterBodies(
        EdgeContract Edge,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (Contract.Groups.Count == 0 || LayoutPoints.Count < 2)
        {
            return false;
        }

        Dictionary<string, GroupContract> GroupById =
            FlowchartSubgraphMembershipHelper.BuildGroupById(Contract);
        Dictionary<string, FlowchartSubgraphLayoutRect> RectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        foreach (GroupContract Group in Contract.Groups)
        {
            if (FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(
                    Edge.SourceId,
                    Group,
                    Contract,
                    GroupById)
                || FlowchartSubgraphMembershipHelper.IsNodeMemberOfGroup(
                    Edge.TargetId,
                    Group,
                    Contract,
                    GroupById))
            {
                continue;
            }

            if (!RectByGroupId.TryGetValue(Group.Id, out FlowchartSubgraphLayoutRect? Rect))
            {
                continue;
            }

            for (int SegmentIndex = 0; SegmentIndex < LayoutPoints.Count - 1; SegmentIndex++)
            {
                (double StartX, double StartY) = LayoutPoints[SegmentIndex];
                (double EndX, double EndY) = LayoutPoints[SegmentIndex + 1];
                if (FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
                        StartX,
                        StartY,
                        EndX,
                        EndY,
                        Rect.Left,
                        Rect.Top,
                        Rect.Right,
                        Rect.Bottom))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PolylinesCross(
        IReadOnlyList<(double X, double Y)> LeftPoints,
        IReadOnlyList<(double X, double Y)> RightPoints)
    {
        for (int LeftSegmentIndex = 1; LeftSegmentIndex < LeftPoints.Count; LeftSegmentIndex++)
        {
            (double LeftStartX, double LeftStartY) = LeftPoints[LeftSegmentIndex - 1];
            (double LeftEndX, double LeftEndY) = LeftPoints[LeftSegmentIndex];
            for (int RightSegmentIndex = 1; RightSegmentIndex < RightPoints.Count; RightSegmentIndex++)
            {
                (double RightStartX, double RightStartY) = RightPoints[RightSegmentIndex - 1];
                (double RightEndX, double RightEndY) = RightPoints[RightSegmentIndex];
                if (SegmentsProperlyIntersect(
                        LeftStartX,
                        LeftStartY,
                        LeftEndX,
                        LeftEndY,
                        RightStartX,
                        RightStartY,
                        RightEndX,
                        RightEndY))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsProperlyIntersect(
        double Ax1,
        double Ay1,
        double Ax2,
        double Ay2,
        double Bx1,
        double By1,
        double Bx2,
        double By2)
    {
        double OrientationA = Orient(Bx1, By1, Bx2, By2, Ax1, Ay1);
        double OrientationB = Orient(Bx1, By1, Bx2, By2, Ax2, Ay2);
        double OrientationC = Orient(Ax1, Ay1, Ax2, Ay2, Bx1, By1);
        double OrientationD = Orient(Ax1, Ay1, Ax2, Ay2, Bx2, By2);

        if (OrientationA > 0.0 != OrientationB > 0.0
            && OrientationC > 0.0 != OrientationD > 0.0)
        {
            if (PointLiesOnSegment(Ax1, Ay1, Ax2, Ay2, Bx1, By1)
                || PointLiesOnSegment(Ax1, Ay1, Ax2, Ay2, Bx2, By2)
                || PointLiesOnSegment(Bx1, By1, Bx2, By2, Ax1, Ay1)
                || PointLiesOnSegment(Bx1, By1, Bx2, By2, Ax2, Ay2))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool PointLiesOnSegment(
        double SegmentStartX,
        double SegmentStartY,
        double SegmentEndX,
        double SegmentEndY,
        double PointX,
        double PointY)
    {
        const double ENDPOINT_TOLERANCE_PX = 0.75;
        double SegmentMinX = Math.Min(SegmentStartX, SegmentEndX) - ENDPOINT_TOLERANCE_PX;
        double SegmentMaxX = Math.Max(SegmentStartX, SegmentEndX) + ENDPOINT_TOLERANCE_PX;
        double SegmentMinY = Math.Min(SegmentStartY, SegmentEndY) - ENDPOINT_TOLERANCE_PX;
        double SegmentMaxY = Math.Max(SegmentStartY, SegmentEndY) + ENDPOINT_TOLERANCE_PX;
        if (PointX < SegmentMinX || PointX > SegmentMaxX || PointY < SegmentMinY || PointY > SegmentMaxY)
        {
            return false;
        }

        double CrossProduct = ((PointY - SegmentStartY) * (SegmentEndX - SegmentStartX))
            - ((PointX - SegmentStartX) * (SegmentEndY - SegmentStartY));
        return Math.Abs(CrossProduct) <= ENDPOINT_TOLERANCE_PX;
    }

    private static double Orient(double Px, double Py, double Qx, double Qy, double Rx, double Ry)
    {
        return ((Qy - Py) * (Rx - Qx)) - ((Qx - Px) * (Ry - Qy));
    }

    private static double DistanceToCentre(double PointX, double PointY, FlowchartLayoutNodeBounds Bounds)
    {
        double DeltaX = PointX - Bounds.CenterX;
        double DeltaY = PointY - Bounds.CenterY;
        return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
    }

    private static double ComputePointToPolylineDistance(
        double PointX,
        double PointY,
        IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count == 0)
        {
            return double.MaxValue;
        }

        if (Points.Count == 1)
        {
            double DeltaX = PointX - Points[0].X;
            double DeltaY = PointY - Points[0].Y;
            return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        double MinimumDistance = double.MaxValue;
        for (int SegmentIndex = 1; SegmentIndex < Points.Count; SegmentIndex++)
        {
            (double StartX, double StartY) = Points[SegmentIndex - 1];
            (double EndX, double EndY) = Points[SegmentIndex];
            double SegmentDistance = DistancePointToSegment(PointX, PointY, StartX, StartY, EndX, EndY);
            if (SegmentDistance < MinimumDistance)
            {
                MinimumDistance = SegmentDistance;
            }
        }

        return MinimumDistance;
    }

    private static double DistancePointToSegment(
        double PointX,
        double PointY,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        double SegmentDeltaX = EndX - StartX;
        double SegmentDeltaY = EndY - StartY;
        double SegmentLengthSquared = (SegmentDeltaX * SegmentDeltaX) + (SegmentDeltaY * SegmentDeltaY);
        if (SegmentLengthSquared <= 1e-9)
        {
            double DeltaX = PointX - StartX;
            double DeltaY = PointY - StartY;
            return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        double Projection = (((PointX - StartX) * SegmentDeltaX) + ((PointY - StartY) * SegmentDeltaY)) / SegmentLengthSquared;
        double ClampedProjection = Math.Max(0.0, Math.Min(1.0, Projection));
        double ClosestX = StartX + (ClampedProjection * SegmentDeltaX);
        double ClosestY = StartY + (ClampedProjection * SegmentDeltaY);
        double ClosestDeltaX = PointX - ClosestX;
        double ClosestDeltaY = PointY - ClosestY;
        return Math.Sqrt((ClosestDeltaX * ClosestDeltaX) + (ClosestDeltaY * ClosestDeltaY));
    }

    private static string BuildEdgeKey(EdgeContract Edge)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Id))
        {
            return Edge.Id;
        }

        return Edge.SourceId + "->" + Edge.TargetId;
    }
}
