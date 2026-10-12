using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves layout-space guide polylines for visible edges (mirrors <see cref="FlowchartSugiyamaSvgRenderer"/> routing).
/// </summary>
public static class FlowchartEdgeRouteResolver
{
    /// <summary>
    /// Resolves guide points in MSAGL layout coordinates for an edge.
    /// Uses the same shared search as the rest of the diagram so a single-edge
    /// lookup cannot pick a different path.
    /// </summary>
    public static bool TryResolveLayoutGuidePoints(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints,
        out FlowchartEdgeRouteKind RouteKind,
        FlowchartLayoutResult? Layout = null)
    {
        List<FlowchartResolvedGuide> Guides = GetOrResolveAllVisibleGuidePoints(
            Contract,
            Layout,
            BoundsById,
            NodeById,
            Direction);
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (!IsSameEdge(Guide.Edge, Edge))
            {
                continue;
            }

            LayoutPoints = new List<(double X, double Y)>(Guide.Points);
            RouteKind = Guide.RouteKind;
            return LayoutPoints.Count >= 2;
        }

        LayoutPoints = [];
        RouteKind = FlowchartEdgeRouteKind.Unknown;
        return false;
    }

    private static List<FlowchartResolvedGuide> GetOrResolveAllVisibleGuidePoints(
        DiagramContract Contract,
        FlowchartLayoutResult? Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction)
    {
        if (Layout == null
            && FlowchartRenderContext.Current.CachedRouteGuides != null
            && ReferenceEquals(FlowchartRenderContext.Current.CachedRouteContract, Contract))
        {
            return FlowchartRenderContext.Current.CachedRouteGuides;
        }

        List<FlowchartResolvedGuide> Guides = ResolveAllVisibleGuidePoints(
            Contract,
            Layout,
            BoundsById,
            NodeById,
            Direction);
        if (Layout == null)
        {
            FlowchartRenderContext.Current.CachedRouteContract = Contract;
            FlowchartRenderContext.Current.CachedRouteGuides = Guides;
        }

        return Guides;
    }

    private static bool IsSameEdge(EdgeContract Left, EdgeContract Right)
    {
        if (!string.IsNullOrEmpty(Left.Id)
            && !string.IsNullOrEmpty(Right.Id))
        {
            return string.Equals(Left.Id, Right.Id, StringComparison.Ordinal);
        }

        return string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            && string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves every visible edge, then applies diagram-wide parallel separation.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="BoundsById">Node bounds, including group rectangles when present.</param>
    /// <param name="NodeById">Nodes by id.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <returns>Resolved guides in contract edge order.</returns>
    public static List<FlowchartResolvedGuide> ResolveAllVisibleGuidePoints(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction)
    {
        return ResolveAllVisibleGuidePoints(Contract, null, BoundsById, NodeById, Direction);
    }

    /// <summary>
    /// Resolves every visible edge, preferring precomputed grid routes when present.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="Layout">Optional layout with grid edge routes.</param>
    /// <param name="BoundsById">Node bounds, including group rectangles when present.</param>
    /// <param name="NodeById">Nodes by id.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <returns>Resolved guides in contract edge order.</returns>
    public static List<FlowchartResolvedGuide> ResolveAllVisibleGuidePoints(
        DiagramContract Contract,
        FlowchartLayoutResult? Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> MutableBounds =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
        {
            MutableBounds[Pair.Key] = Pair.Value;
        }

        FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, MutableBounds);

        List<FlowchartResolvedGuide> Guides = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Layout != null
                && TryResolveGridLayoutPoints(
                    Layout,
                    Contract,
                    NodeById,
                    Edge,
                    out List<(double X, double Y)> GridPoints,
                    out FlowchartEdgeRouteKind GridKind))
            {
                Guides.Add(new FlowchartResolvedGuide(Edge, GridPoints, GridKind));
                continue;
            }

            if (!TryResolveRawLayoutGuidePoints(
                    Edge,
                    Contract,
                    MutableBounds,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> Points,
                    out FlowchartEdgeRouteKind RouteKind))
            {
                continue;
            }

            Guides.Add(new FlowchartResolvedGuide(Edge, Points, RouteKind));
        }

        if (!UsedGridLayoutRoutes(Layout, Guides))
        {
            FlowchartSharedEdgeRouter.RouteAll(Contract, MutableBounds, NodeById, Direction, Guides);
        }

        FlowchartRenderContext.Current.CachedRouteContract = Contract;
        FlowchartRenderContext.Current.CachedRouteGuides = Guides;
        return Guides;
    }

    private static bool UsedGridLayoutRoutes(FlowchartLayoutResult? Layout, List<FlowchartResolvedGuide> Guides)
    {
        return Layout != null && Layout.EdgeRoutes.Count > 0;
    }

    /// <summary>
    /// Returns guide points from a completed grid layout when the edge was routed there.
    /// </summary>
    internal static bool TryResolveGridLayoutPoints(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        EdgeContract Edge,
        out List<(double X, double Y)> LayoutPoints,
        out FlowchartEdgeRouteKind RouteKind)
    {
        LayoutPoints = [];
        RouteKind = FlowchartEdgeRouteKind.Unknown;
        if (!Layout.TryCopyDrawnRoutePoints(Edge, out List<(double X, double Y)> Copied)
            || Copied.Count < 2)
        {
            return false;
        }

        LayoutPoints = Copied;
        if (FlowchartBackEdgeHelper.FindBackEdges(Contract).Contains(Edge))
        {
            RouteKind = FlowchartEdgeRouteKind.RetryLoop;
            return true;
        }

        if (LayoutPoints.Count == 2)
        {
            RouteKind = FlowchartEdgeRouteKind.Straight;
            return true;
        }

        if (NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode))
        {
            if (FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape)
                && CountVisibleOutgoing(Contract, Edge.SourceId) >= 2)
            {
                RouteKind = FlowchartEdgeRouteKind.Grid;
                return true;
            }

            Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
                new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
            foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
            {
                BoundsById[Bounds.NodeId] = Bounds;
            }

            if (FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract,
                    Edge.SourceId,
                    SourceNode.Shape,
                    FlowchartDirectionNormalizer.Normalize(Contract.Direction),
                    BoundsById))
            {
                RouteKind = FlowchartEdgeRouteKind.Grid;
                return true;
            }
        }

        RouteKind = FlowchartEdgeRouteKind.Grid;
        return true;
    }

    private static int CountVisibleOutgoing(DiagramContract Contract, string SourceId)
    {
        int Count = 0;
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.SourceId, SourceId, StringComparison.Ordinal)
                && !string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                Count++;
            }
        }

        return Count;
    }

    private static bool TryResolveRawLayoutGuidePoints(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints,
        out FlowchartEdgeRouteKind RouteKind)
    {
        LayoutPoints = [];
        RouteKind = FlowchartEdgeRouteKind.Unknown;

        if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> EffectiveBounds = BoundsById;
        if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Edge.SourceId)
            || FlowchartCompoundEndpointHelper.IsGroupId(Contract, Edge.TargetId))
        {
            Dictionary<string, FlowchartLayoutNodeBounds> WithGroups =
                new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
            {
                WithGroups[Pair.Key] = Pair.Value;
            }

            FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, WithGroups);
            if (FlowchartCompoundEndpointHelper.TryBuildFacingBoundaryPath(
                    Edge,
                    Contract,
                    WithGroups,
                    NodeById,
                    out LayoutPoints))
            {
                RouteKind = FlowchartEdgeRouteKind.Straight;
                return true;
            }

            EffectiveBounds = WithGroups;
        }

        if (!EffectiveBounds.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !EffectiveBounds.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
        string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;

        if (FlowchartLoopBackRouteHelper.TryResolveDecisionLoopBackRoute(Edge, Contract, EffectiveBounds, NodeById, Direction, out List<(double X, double Y)> DecisionLoopPoints))
        {
            LayoutPoints = DecisionLoopPoints;
            RouteKind = FlowchartEdgeRouteKind.RetryLoop;
            return true;
        }

        if (FlowchartLoopBackRouteHelper.TryResolveSoleForwardDecisionBranch(Edge, Contract, EffectiveBounds, NodeById, Direction, out List<(double X, double Y)> SoleBranchPoints))
        {
            LayoutPoints = SoleBranchPoints;
            RouteKind = FlowchartEdgeRouteKind.Straight;
            return true;
        }

        if (FlowchartDecisionPortAllocator.TryResolveEdgeRoute(
                Edge,
                Contract,
                EffectiveBounds,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> EarlyDecisionPoints))
        {
            LayoutPoints = EarlyDecisionPoints;
            RouteKind = FlowchartEdgeRouteKind.Grid;
            return true;
        }

        if (FlowchartEdgePathBuilder.IsLrRetryLoopEdge(
                Edge,
                Contract,
                SourceBounds,
                TargetBounds,
                SourceShape,
                TargetShape)
            && FlowchartEdgePathBuilder.TryBuildLrRetryLoopPath(
                SourceBounds,
                TargetBounds,
                BoundsById.Values.ToList(),
                Direction,
                out List<(double X, double Y)> RetryPoints,
                Edge.MarkerStart,
                Edge.MarkerEnd))
        {
            LayoutPoints = RetryPoints;
            RouteKind = FlowchartEdgeRouteKind.RetryLoop;
            return true;
        }

        if (FlowchartGroupedVertexEdgePathBuilder.TryBuildGroupedTwoByTwoPath(
                Edge,
                Contract,
                SourceBounds,
                TargetBounds,
                Direction,
                out List<(double X, double Y)> GroupedPoints))
        {
            LayoutPoints = GroupedPoints;
            RouteKind = FlowchartEdgeRouteKind.GroupedTwoByTwo;
            return true;
        }

        if (FlowchartLoopBackRouteHelper.TryResolveStackedReciprocalLane(Edge, Contract, BoundsById, NodeById, Direction, out List<(double X, double Y)> ReciprocalLanePoints))
        {
            LayoutPoints = ReciprocalLanePoints;
            RouteKind = FlowchartEdgeRouteKind.Straight;
            return true;
        }

        string? CrossSourceGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.SourceId, Contract);
        string? CrossTargetGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.TargetId, Contract);
        if (!string.Equals(CrossSourceGroupId, CrossTargetGroupId, StringComparison.Ordinal)
            && string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase))
        {
            (double CrossStartX, double CrossStartY, double CrossEndX, double CrossEndY) =
                ResolveClippedEdgeSegment(
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    TargetShape,
                    Edge.MarkerStart,
                    Edge.MarkerEnd);
            bool UseOrthogonalDottedCorridor = string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase);
            if (FlowchartEdgeGuidePointBuilder.TryBuildCrossGroupLayoutGuidePoints(
                    Contract,
                    Edge.SourceId,
                    Edge.TargetId,
                    SourceBounds,
                    TargetBounds,
                    CrossStartX,
                    CrossStartY,
                    CrossEndX,
                    CrossEndY,
                    Direction,
                    BoundsById,
                    UseOrthogonalDottedCorridor,
                    out List<(double X, double Y)> EarlyCrossGroupPoints))
            {
                LayoutPoints = EarlyCrossGroupPoints;
                RouteKind = IsUnderCorridorRoute(EarlyCrossGroupPoints, Direction) ? FlowchartEdgeRouteKind.RetryLoop : FlowchartEdgeRouteKind.CrossGroup;
                return true;
            }
        }

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            RenderOptions,
            out FlowchartSpineLayoutContext? SpineContext);

        if (FlowchartTerminalCollectorPortAllocator.TryResolveEdgeRoute(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> EarlyTerminalCollectorPoints))
        {
            LayoutPoints = EarlyTerminalCollectorPoints;
            RouteKind = EarlyTerminalCollectorPoints.Count == 2
                || FlowchartTerminalCollectorPortAllocator.IsSpineStraightRoute(EarlyTerminalCollectorPoints)
                ? FlowchartEdgeRouteKind.Straight
                : FlowchartEdgeRouteKind.Curved;
            return true;
        }

        FlowchartEdgeSemanticRouteClass SemanticRouteClass = FlowchartEdgeRouteClassifier.Classify(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            SpineContext);

        List<(double X, double Y)> CentreSpinePoints = [];
        if (SemanticRouteClass == FlowchartEdgeSemanticRouteClass.CentreSpine
            && FlowchartCentreSpineRouteBuilder.TryBuildRoute(
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                Edge.MarkerStart,
                Edge.MarkerEnd,
                Direction,
                CentreSpinePoints))
        {
            LayoutPoints = CentreSpinePoints;
            RouteKind = FlowchartEdgeRouteKind.Straight;
            return true;
        }

        if (SemanticRouteClass != FlowchartEdgeSemanticRouteClass.Unknown
            && FlowchartEdgeRouteStrategyRegistry.TryResolveRoute(
                SemanticRouteClass,
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                SpineContext,
                out List<(double X, double Y)> SemanticPoints,
                out FlowchartEdgeRouteKind SemanticRouteKind))
        {
            if (SemanticRouteKind == FlowchartEdgeRouteKind.Corridor)
            {
                LayoutPoints = SemanticPoints;
            }
            else
            {
                LayoutPoints = FlowchartEdgeTerminalStubExpander.Apply(
                    SemanticPoints,
                    SourceBounds,
                    SourceShape,
                    TargetBounds,
                    TargetShape);
            }

            RouteKind = SemanticRouteKind;
            return true;
        }

        if (FlowchartParallelBranchHubPortHelper.TryResolveEdgeRoute(Edge, Contract, BoundsById, NodeById, Direction, out List<(double X, double Y)> ParallelBranchPoints, out bool _))
        {
            LayoutPoints = ParallelBranchPoints;
            RouteKind = FlowchartEdgeRouteKind.Curved;
            return true;
        }

        if (FlowchartFanInPortAllocator.TryResolveLoopBackRoute(Edge, Contract, BoundsById, TargetShape, Direction, out List<(double X, double Y)> LoopBackPoints))
        {
            LayoutPoints = LoopBackPoints;
            RouteKind = FlowchartEdgeRouteKind.RetryLoop;
            return true;
        }

        if (FlowchartFanInPortAllocator.TryResolveEdgeRoute(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> FanInPoints))
        {
            LayoutPoints = FanInPoints;
            RouteKind = FlowchartEdgeConnectionGeometry.IsCollinearStubStraightRoute(FanInPoints)
                || FanInPoints.Count == 2
                ? FlowchartEdgeRouteKind.Straight
                : FlowchartEdgeRouteKind.Curved;
            return true;
        }

        if (FlowchartOrdinaryFanOutRouteBuilder.TryBuildEdgeRoute(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> OrdinaryFanOutPoints))
        {
            LayoutPoints = OrdinaryFanOutPoints;
            RouteKind = FlowchartEdgeConnectionGeometry.IsCollinearStubStraightRoute(OrdinaryFanOutPoints)
                || OrdinaryFanOutPoints.Count == 2
                ? FlowchartEdgeRouteKind.Straight
                : FlowchartEdgeRouteKind.Grid;
            return true;
        }

        if (FlowchartFanOutPortAllocator.TryResolveEdgeRoute(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out List<(double X, double Y)> FanOutPoints))
        {
            LayoutPoints = FanOutPoints;
            RouteKind = FlowchartEdgeConnectionGeometry.IsCollinearStubStraightRoute(FanOutPoints)
                || FanOutPoints.Count == 2
                ? FlowchartEdgeRouteKind.Straight
                : FlowchartEdgeRouteKind.Curved;
            return true;
        }

        (double LayoutX1, double LayoutY1, double LayoutX2, double LayoutY2) =
            ResolveClippedEdgeSegment(
                SourceBounds,
                TargetBounds,
                SourceShape,
                TargetShape,
                Edge.MarkerStart,
                Edge.MarkerEnd);

        bool UseStraightSegment = FlowchartEdgeGuidePointBuilder.ShouldUseStraightSegment(
            SourceBounds,
            TargetBounds,
            Contract,
            Edge.SourceId,
            Edge.TargetId);

        if (UseStraightSegment)
        {
            FlowchartReciprocalEdgeHelper.ApplyReciprocalLaneOffset(
                Edge,
                Contract,
                ref LayoutX1,
                ref LayoutY1,
                ref LayoutX2,
                ref LayoutY2);
            if (FlowchartPerpendicularRouteBuilder.TryAppendStraightOrObstacleAvoidingRoute(
                    LayoutPoints,
                    LayoutX1,
                    LayoutY1,
                    SourceBounds,
                    SourceShape,
                    LayoutX2,
                    LayoutY2,
                    TargetBounds,
                    TargetShape,
                    Direction,
                    BoundsById,
                    Edge.SourceId,
                    Edge.TargetId))
            {
                RouteKind = ResolveObstacleAwareRouteKind(Edge, Contract, LayoutPoints);
                return true;
            }
        }

        bool PreferOrthogonalCorridor = string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase);
        if (FlowchartEdgeGuidePointBuilder.TryBuildCrossGroupLayoutGuidePoints(
                Contract,
                Edge.SourceId,
                Edge.TargetId,
                SourceBounds,
                TargetBounds,
                LayoutX1,
                LayoutY1,
                LayoutX2,
                LayoutY2,
                Direction,
                BoundsById,
                PreferOrthogonalCorridor,
                out List<(double X, double Y)> CrossGroupPoints))
        {
            if (IsUnderCorridorRoute(CrossGroupPoints, Direction))
            {
                LayoutPoints = CrossGroupPoints;
                RouteKind = FlowchartEdgeRouteKind.RetryLoop;
                return true;
            }

            LayoutPoints = FlowchartEdgeTerminalStubExpander.Apply(
                CrossGroupPoints,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape);
            RouteKind = FlowchartEdgeRouteKind.CrossGroup;
            return true;
        }

        List<(double X, double Y)> CurvedPoints = FlowchartEdgeGuidePointBuilder.BuildLayoutCurvedGuidePoints(
            LayoutX1,
            LayoutY1,
            LayoutX2,
            LayoutY2,
            SourceShape,
            TargetShape,
            Edge,
            Contract,
            Direction,
            SourceBounds.CenterX,
            SourceBounds.CenterY,
            TargetBounds.CenterX,
            TargetBounds.CenterY);

        if (CurvedPoints.Count == 2)
        {
            FlowchartReciprocalEdgeHelper.ApplyReciprocalLaneOffset(Edge, Contract, CurvedPoints);
            List<(double X, double Y)> ObstacleAwarePoints = [];
            if (FlowchartPerpendicularRouteBuilder.TryAppendStraightOrObstacleAvoidingRoute(
                    ObstacleAwarePoints,
                    CurvedPoints[0].X,
                    CurvedPoints[0].Y,
                    SourceBounds,
                    SourceShape,
                    CurvedPoints[1].X,
                    CurvedPoints[1].Y,
                    TargetBounds,
                    TargetShape,
                    Direction,
                    BoundsById,
                    Edge.SourceId,
                    Edge.TargetId))
            {
                LayoutPoints = ObstacleAwarePoints;
                RouteKind = ResolveObstacleAwareRouteKind(Edge, Contract, LayoutPoints);
                return true;
            }
        }

        LayoutPoints = FlowchartEdgeTerminalStubExpander.Apply(
            CurvedPoints,
            SourceBounds,
            SourceShape,
            TargetBounds,
            TargetShape);
        RouteKind = FlowchartEdgeRouteKind.Curved;
        return LayoutPoints.Count >= 2;
    }

    private static (double X1, double Y1, double X2, double Y2) ResolveClippedEdgeSegment(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape,
        EdgeEndpointMarker MarkerStart,
        EdgeEndpointMarker MarkerEnd)
    {
        double DirX = TargetBounds.CenterX - SourceBounds.CenterX;
        double DirY = TargetBounds.CenterY - SourceBounds.CenterY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return (SourceBounds.CenterX, SourceBounds.CenterY, TargetBounds.CenterX, TargetBounds.CenterY);
        }

        DirX /= Length;
        DirY /= Length;

        (double SourceX, double SourceY) = FlowchartShapeLayoutHelper.RayExitShape(
            SourceBounds.CenterX,
            SourceBounds.CenterY,
            SourceBounds.Width,
            SourceBounds.Height,
            SourceShape,
            DirX,
            DirY);

        (double TargetX, double TargetY) = FlowchartShapeLayoutHelper.RayExitShape(
            TargetBounds.CenterX,
            TargetBounds.CenterY,
            TargetBounds.Width,
            TargetBounds.Height,
            TargetShape,
            -DirX,
            -DirY);

        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(MarkerStart);
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(MarkerEnd);
        SourceX += DirX * SourceGapPx;
        SourceY += DirY * SourceGapPx;
        TargetX -= DirX * TargetGapPx;
        TargetY -= DirY * TargetGapPx;

        return (SourceX, SourceY, TargetX, TargetY);
    }

    /// <summary>
    /// Returns true for the four-point left-to-right under-corridor route: down out of the source, across, and up into the target (F60).
    /// That route is drawn with rounded corners.
    /// </summary>
    private static bool IsUnderCorridorRoute(IReadOnlyList<(double X, double Y)> Points, FlowchartDirection Direction)
    {
        if (Direction != FlowchartDirection.LeftToRight && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        if (Points.Count != 4)
        {
            return false;
        }

        bool DownOut = Math.Abs(Points[0].X - Points[1].X) < 0.01 && Points[1].Y < Points[0].Y;
        bool Across = Math.Abs(Points[1].Y - Points[2].Y) < 0.01;
        bool UpIn = Math.Abs(Points[2].X - Points[3].X) < 0.01 && Points[3].Y > Points[2].Y;
        return DownOut && Across && UpIn;
    }

    private static FlowchartEdgeRouteKind ResolveObstacleAwareRouteKind(
        EdgeContract Edge,
        DiagramContract Contract,
        List<(double X, double Y)> LayoutPoints)
    {
        if (LayoutPoints.Count == 2)
        {
            return FlowchartEdgeRouteKind.Straight;
        }

        string? SourceGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.SourceId, Contract);
        string? TargetGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.TargetId, Contract);
        bool CrossesSubgraphBoundary = !string.Equals(SourceGroupId, TargetGroupId, StringComparison.Ordinal);

        if (!CrossesSubgraphBoundary
            && FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(LayoutPoints))
        {
            return FlowchartEdgeRouteKind.Corridor;
        }

        return FlowchartEdgeRouteKind.CrossGroup;
    }
}

/// <summary>
/// One resolved visible edge after the shared quality pass.
/// </summary>
public sealed class FlowchartResolvedGuide
{
    /// <summary>
    /// Creates a resolved guide.
    /// </summary>
    /// <param name="Edge">The edge.</param>
    /// <param name="Points">Layout guide points.</param>
    /// <param name="RouteKind">Resolved kind.</param>
    public FlowchartResolvedGuide(
        EdgeContract Edge,
        List<(double X, double Y)> Points,
        FlowchartEdgeRouteKind RouteKind)
    {
        this.Edge = Edge;
        this.Points = Points;
        this.RouteKind = RouteKind;
    }

    /// <summary>
    /// The edge.
    /// </summary>
    public EdgeContract Edge { get; }

    /// <summary>
    /// Layout guide points.
    /// </summary>
    public List<(double X, double Y)> Points { get; }

    /// <summary>
    /// Resolved kind.
    /// </summary>
    public FlowchartEdgeRouteKind RouteKind { get; set; }
}

/// <summary>
/// Classification of how an edge route was resolved.
/// </summary>
public enum FlowchartEdgeRouteKind
{
    Unknown,

    Straight,

    Curved,

    Decision,

    RetryLoop,

    GroupedTwoByTwo,

    CrossGroup,

    /// <summary>
    /// Orthogonal exterior corridor with explicit waypoint fidelity (bypass / exception lanes).
    /// </summary>
    Corridor,

    /// <summary>
    /// Orthogonal grid route from <see cref="DiagramGridLayout"/> that must not be rewritten.
    /// </summary>
    Grid
}
