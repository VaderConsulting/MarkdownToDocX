using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Native flowchart SVG using adopted Sugiyama layout in <c>Sugiyama/</c> (pass 21 R1 pilot).
/// Consumes <see cref="FlowchartRenderOptions"/> from the recognition contract (spec §3.17, §4.1).
/// </summary>
public static class FlowchartSugiyamaSvgRenderer
{
    private const double FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR = 1.5;

    private const double ARROWHEAD_LENGTH_PX = 10.0;

    private const double ARROWHEAD_WIDTH_PX = 7.0;

    private const double ARROW_TIP_NODE_OVERLAP_PX = 1.0;

    private const double CIRCLE_MARKER_RADIUS_PX = 5.0;

    private const double CIRCLE_MARKER_INSET_PX = 10.0;

    private const double CROSS_MARKER_HALF_PX = 5.0;

    private const double CROSS_MARKER_INSET_PX = 10.2;

    [ThreadStatic]
    private static FlowchartSvgRenderSession? _ActiveRenderSession;

    /// <summary>
    /// Renders SVG using Sugiyama node placement; returns empty when layout fails.
    /// </summary>
    public static string RenderToSvg(DiagramContract Contract)
    {
        if (!IsFlowchartContract(Contract) || Contract.Nodes.Count == 0)
        {
            return string.Empty;
        }

        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout)
            || Layout.NodeBounds.Count == 0)
        {
            return string.Empty;
        }

        return RenderToSvg(Contract, Layout);
    }

    /// <summary>
    /// Renders SVG from an already computed layout. Used by the sample 03 oracle comparison.
    /// </summary>
    /// <param name="Contract">Flowchart contract.</param>
    /// <param name="Layout">Node bounds to paint.</param>
    /// <returns>SVG markup, or empty when the layout is unusable.</returns>
    public static string RenderToSvg(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        if (!IsFlowchartContract(Contract) || Layout == null || Layout.NodeBounds.Count == 0)
        {
            return string.Empty;
        }

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        double DiagramPaddingPx = RenderOptions.DiagramPaddingPx;

        Dictionary<string, NodeContract> NodeById =
            new Dictionary<string, NodeContract>(StringComparer.Ordinal);

        foreach (NodeContract NodeEntry in Contract.Nodes)
        {
            NodeById[NodeEntry.Id] = NodeEntry;
        }

        double MinLeft = double.MaxValue;
        double MinBottom = double.MaxValue;
        double MaxRight = double.MinValue;
        double MaxTop = double.MinValue;

        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            string? Shape = NodeById.TryGetValue(Bounds.NodeId, out NodeContract? BoundsNode) ? BoundsNode.Shape : null;
            double LeftExtension = FlowchartShapeLayoutHelper.GetLeftExtensionPx(Shape, Bounds.Height);
            double RightExtension = FlowchartShapeLayoutHelper.GetRightExtensionPx(Shape, Bounds.Height);
            double Left = Bounds.CenterX - (Bounds.Width / 2.0) - LeftExtension;
            double Right = Bounds.CenterX + (Bounds.Width / 2.0) + RightExtension;
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);

            if (Left < MinLeft)
            {
                MinLeft = Left;
            }

            if (Right > MaxRight)
            {
                MaxRight = Right;
            }

            if (Bottom < MinBottom)
            {
                MinBottom = Bottom;
            }

            if (Top > MaxTop)
            {
                MaxTop = Top;
            }
        }

        double GraphWidth = Math.Max(1.0, MaxRight - MinLeft);
        double GraphHeight = Math.Max(1.0, MaxTop - MinBottom);
        double SvgWidth = GraphWidth + (DiagramPaddingPx * 2.0);
        double SvgHeight = GraphHeight + (DiagramPaddingPx * 2.0);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);

        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, BoundsById);
        List<FlowchartResolvedGuide> ResolvedGuides = FlowchartEdgeRouteResolver.ResolveAllVisibleGuidePoints(
            Contract,
            Layout,
            BoundsById,
            NodeById,
            Direction);
        Dictionary<EdgeContract, FlowchartResolvedGuide> GuideByEdge =
            new Dictionary<EdgeContract, FlowchartResolvedGuide>();
        foreach (FlowchartResolvedGuide Guide in ResolvedGuides)
        {
            GuideByEdge[Guide.Edge] = Guide;
        }

        Dictionary<string, string> RouteClassByEdgeKey =
            FlowchartEdgeRoutingIntentAnalyzer.BuildRouteClassNamesByEdgeKey(Contract, Layout, Direction);

        ExpandLayoutBoundsForEdgeLabels(
            Contract,
            RenderOptions,
            BoundsById,
            RouteClassByEdgeKey,
            Direction,
            GuideByEdge,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);

        ExpandLayoutBoundsForRetryLoops(
            Contract,
            Direction,
            Layout.NodeBounds,
            NodeById,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);

        FlowchartSubgraphBoundsRequest BoundsRequest =
            FlowchartSubgraphBoundsRequest.FromContract(Contract, RenderOptions);
        List<FlowchartSubgraphLayoutRect> SubgraphRects = Layout.ClusterBounds.Count > 0
            ? BuildDagreClusterRects(Contract, Layout)
            : FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest);
        if (Layout.ClusterBounds.Count > 0)
        {
            foreach (FlowchartLayoutNodeBounds Cluster in Layout.ClusterBounds)
            {
                BoundsById[Cluster.NodeId] = Cluster;
            }
        }
        FlowchartSubgraphBoundsBuilder.ExpandLayoutExtents(
            SubgraphRects,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);
        ExpandLayoutBoundsForExteriorStrokes(
            ResolvedGuides,
            DiagramPaddingPx,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);
        ExpandLayoutBoundsForDagreRoutes(
            Layout,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);

        GraphWidth = Math.Max(1.0, MaxRight - MinLeft);
        GraphHeight = Math.Max(1.0, MaxTop - MinBottom);
        SvgWidth = GraphWidth + (DiagramPaddingPx * 2.0);
        SvgHeight = GraphHeight + (DiagramPaddingPx * 2.0);

        StringBuilder Builder = new StringBuilder();
        Builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"");
        Builder.Append(Math.Ceiling(SvgWidth));
        Builder.Append("\" height=\"");
        Builder.Append(Math.Ceiling(SvgHeight));
        Builder.Append("\" viewBox=\"0 0 ");
        Builder.Append(Math.Ceiling(SvgWidth).ToString(CultureInfo.InvariantCulture));
        Builder.Append(' ');
        Builder.Append(Math.Ceiling(SvgHeight).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\">");

        FlowchartSvgRenderSession RenderSession = new FlowchartSvgRenderSession();
        _ActiveRenderSession = RenderSession;

        try
        {
        AppendBackgroundRect(Builder, SvgWidth, SvgHeight, RenderOptions.Background);

        FlowchartSubgraphSvgRenderer.AppendSubgraphs(
            Builder,
            SubgraphRects,
            MinLeft,
            MaxTop,
            DiagramPaddingPx,
            RenderOptions);

        Dictionary<string, (double Left, double Top, double Width, double Height)> NodeLayoutById =
            new Dictionary<string, (double Left, double Top, double Width, double Height)>(StringComparer.Ordinal);

        List<NodeContract> NodesInPaintOrder = Contract.Nodes
            .OrderByDescending(Node => BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds Bounds) ? Bounds.CenterY : double.MinValue)
            .ThenBy(Node => Node.Id, StringComparer.Ordinal)
            .ToList();

        foreach (NodeContract Node in NodesInPaintOrder)
        {
            if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Node.Id))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double Left = DiagramPaddingPx + (Bounds.CenterX - (Bounds.Width / 2.0) - MinLeft);
            double Top = DiagramPaddingPx + (MaxTop - Bounds.CenterY - (Bounds.Height / 2.0));
            NodeLayoutById[Node.Id] = (Left, Top, Bounds.Width, Bounds.Height);
        }

        Dictionary<string, Queue<FlowchartLayoutEdgeRoute>> DagreRoutes = IndexDagreRoutes(Layout);
        int DagreEdgeIndex = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            string DagreEdgeId = string.IsNullOrEmpty(Edge.Id)
                ? "e" + DagreEdgeIndex.ToString(CultureInfo.InvariantCulture)
                : Edge.Id;
            DagreEdgeIndex++;
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? RouteClass = TryResolveRouteClassName(Edge, RouteClassByEdgeKey);
            FlowchartResolvedStyle EdgeStyle = RenderOptions.ResolveEdgeStyle(Edge);
            if (TryTakeDagreRoute(DagreRoutes, DagreEdgeId, out FlowchartLayoutEdgeRoute? DagreRoute)
                && DagreRoute.Points.Count >= 2
                && BoundsById.TryGetValue(DagreRoute.SourceId, out FlowchartLayoutNodeBounds DagreSource)
                && BoundsById.TryGetValue(DagreRoute.TargetId, out FlowchartLayoutNodeBounds DagreTarget))
            {
                string? DagreSourceShape = NodeById.TryGetValue(DagreRoute.SourceId, out NodeContract? DagreSourceNode)
                    ? DagreSourceNode.Shape
                    : "rect";
                if (Layout.UsesGridRoutes
                    && GuideByEdge.TryGetValue(Edge, out FlowchartResolvedGuide? GridGuide)
                    && GridGuide.Points.Count >= 2
                    && (GridGuide.RouteKind == FlowchartEdgeRouteKind.Grid
                        || GridGuide.RouteKind == FlowchartEdgeRouteKind.Straight))
                {
                    RenderResolvedEdgeRoute(
                        Builder,
                        Edge,
                        Contract,
                        BoundsById,
                        DagreSource,
                        DagreTarget,
                        DagreSourceShape,
                        GridGuide.Points,
                        GridGuide.RouteKind,
                        Direction,
                        RouteClass,
                        MinLeft,
                        MaxTop,
                        DiagramPaddingPx,
                        EdgeStyle,
                        RenderOptions);
                    continue;
                }

                RenderDagreBasisEdge(
                    Builder,
                    Edge,
                    Contract,
                    BoundsById,
                    DagreSource,
                    DagreTarget,
                    DagreSourceShape,
                    DagreRoute,
                    Direction,
                    RouteClass,
                    MinLeft,
                    MaxTop,
                    DiagramPaddingPx,
                    EdgeStyle,
                    RenderOptions);
                continue;
            }

            bool SourceIsGroup = FlowchartCompoundEndpointHelper.IsGroupId(Contract, Edge.SourceId);
            bool TargetIsGroup = FlowchartCompoundEndpointHelper.IsGroupId(Contract, Edge.TargetId);
            if (SourceIsGroup || TargetIsGroup)
            {
                if (GuideByEdge.TryGetValue(Edge, out FlowchartResolvedGuide? CompoundGuide)
                    && CompoundGuide.Points.Count >= 2)
                {
                    List<(double X, double Y)> CompoundPoints = CompoundGuide.Points;
                    FlowchartEdgeRouteKind CompoundKind = CompoundGuide.RouteKind;
                    Dictionary<string, FlowchartLayoutNodeBounds> CompoundBounds =
                        new Dictionary<string, FlowchartLayoutNodeBounds>(BoundsById, StringComparer.Ordinal);
                    FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, CompoundBounds);
                    if (CompoundBounds.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds CompoundSource)
                        && CompoundBounds.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds CompoundTarget))
                    {
                        string? CompoundSourceShape = SourceIsGroup
                            ? "rect"
                            : (NodeById.TryGetValue(Edge.SourceId, out NodeContract? CompoundSourceNode)
                                ? CompoundSourceNode.Shape
                                : null);
                        RenderResolvedEdgeRoute(
                            Builder,
                            Edge,
                            Contract,
                            CompoundBounds,
                            CompoundSource,
                            CompoundTarget,
                            CompoundSourceShape,
                            CompoundPoints,
                            CompoundKind,
                            Direction,
                            RouteClass,
                            MinLeft,
                            MaxTop,
                            DiagramPaddingPx,
                            EdgeStyle,
                            RenderOptions);
                    }

                    continue;
                }
            }

            if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
            bool IsRetryLoop = FlowchartEdgePathBuilder.IsLrRetryLoopEdge(
                Edge,
                Contract,
                SourceBounds,
                TargetBounds,
                SourceShape,
                TargetShape);

            if (IsRetryLoop
                && TryBuildRetryLoopLayoutPoints(Edge, Contract, BoundsById, NodeById, Layout.NodeBounds, Direction, out List<(double X, double Y)> LayoutPoints))
            {
                List<(double X, double Y)> SvgPoints = [];
                foreach ((double LayoutX, double LayoutY) in LayoutPoints)
                {
                    SvgPoints.Add(LayoutToSvgPoint(LayoutX, LayoutY, MinLeft, MaxTop, DiagramPaddingPx));
                }

                string PathData = FlowchartCurvePathBuilder.BuildRetryLoopPathData(SvgPoints);
                AppendStyledEdgePath(Builder, PathData, EdgeStyle, Edge.SourceId, Edge.TargetId);

                if (SvgPoints.Count >= 2)
                {
                    AppendCurvedEdgeMarkers(
                        Builder,
                        Edge,
                        PathData,
                        SvgPoints,
                        FlowchartRenderedPathKind.RetryLoop,
                        EdgeStyle);
                }

                continue;
            }

            if (FlowchartGroupedVertexEdgePathBuilder.TryBuildGroupedTwoByTwoPath(
                    Edge,
                    Contract,
                    SourceBounds,
                    TargetBounds,
                    Direction,
                    out List<(double X, double Y)> GroupedLayoutPoints)
                && GroupedLayoutPoints.Count >= 2)
            {
                (double GroupLayoutX1, double GroupLayoutY1) = GroupedLayoutPoints[0];
                (double GroupLayoutX2, double GroupLayoutY2) = GroupedLayoutPoints[GroupedLayoutPoints.Count - 1];
                (double GroupStartX, double GroupStartY) = LayoutToSvgPoint(GroupLayoutX1, GroupLayoutY1, MinLeft, MaxTop, DiagramPaddingPx);
                (double GroupTipX, double GroupTipY) = LayoutToSvgPoint(GroupLayoutX2, GroupLayoutY2, MinLeft, MaxTop, DiagramPaddingPx);

                RenderStraightEdgeSegment(
                    Builder,
                    Edge,
                    GroupStartX,
                    GroupStartY,
                    GroupTipX,
                    GroupTipY,
                    EdgeStyle,
                    RenderOptions);

                AppendResolvedEdgeLabel(
                    Builder,
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    GroupedLayoutPoints,
                    Direction,
                    RouteClass,
                    MinLeft,
                    MaxTop,
                    DiagramPaddingPx,
                    EdgeStyle,
                    RenderOptions);

                continue;
            }

            if (GuideByEdge.TryGetValue(Edge, out FlowchartResolvedGuide? RoutedGuide)
                && RoutedGuide.Points.Count >= 2)
            {
                List<(double X, double Y)> RoutedLayoutPoints = RoutedGuide.Points;
                FlowchartEdgeRouteKind RouteKind = RoutedGuide.RouteKind;
                RenderResolvedEdgeRoute(
                    Builder,
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    RoutedLayoutPoints,
                    RouteKind,
                    Direction,
                    RouteClass,
                    MinLeft,
                    MaxTop,
                    DiagramPaddingPx,
                    EdgeStyle,
                    RenderOptions);
                continue;
            }

            if (FlowchartFanOutPortAllocator.TryResolveEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceShape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> FanOutLayoutPoints)
                && FanOutLayoutPoints.Count >= 2)
            {
                List<(double X, double Y)> FanOutSvgPoints = [];
                foreach ((double LayoutX, double LayoutY) in FanOutLayoutPoints)
                {
                    FanOutSvgPoints.Add(LayoutToSvgPoint(LayoutX, LayoutY, MinLeft, MaxTop, DiagramPaddingPx));
                }

                if (FanOutSvgPoints.Count == 2
                    || FlowchartEdgeConnectionGeometry.IsCollinearStubStraightRoute(FanOutSvgPoints))
                {
                    (double FanOutStartX, double FanOutStartY) = FanOutSvgPoints[0];
                    (double FanOutTipX, double FanOutTipY) = FanOutSvgPoints[FanOutSvgPoints.Count - 1];
                    RenderStraightEdgeSegment(
                        Builder,
                        Edge,
                        FanOutStartX,
                        FanOutStartY,
                        FanOutTipX,
                        FanOutTipY,
                        EdgeStyle,
                        RenderOptions);
                }
                else
                {
                    string FanOutPathData = Direction == FlowchartDirection.LeftToRight
                        || Direction == FlowchartDirection.RightToLeft
                        ? FlowchartCurvePathBuilder.BuildHorizontalFanRoutePathData(FanOutSvgPoints)
                        : FlowchartCurvePathBuilder.BuildVerticalFanRoutePathData(FanOutSvgPoints);
                    AppendStyledEdgePath(Builder, FanOutPathData, EdgeStyle, Edge.SourceId, Edge.TargetId);

                    if (FanOutSvgPoints.Count > 2)
                    {
                        bool HorizontalFanFlow = Direction == FlowchartDirection.LeftToRight
                            || Direction == FlowchartDirection.RightToLeft;
                        AppendCurvedEdgeMarkers(
                            Builder,
                            Edge,
                            FanOutPathData,
                            FanOutSvgPoints,
                            ResolveFanRenderedPathKind(FanOutSvgPoints, HorizontalFanFlow),
                            EdgeStyle);
                    }
                }

                AppendResolvedEdgeLabel(
                    Builder,
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    FanOutLayoutPoints,
                    Direction,
                    RouteClass,
                    MinLeft,
                    MaxTop,
                    DiagramPaddingPx,
                    EdgeStyle,
                    RenderOptions);

                continue;
            }

            if (FlowchartFanInPortAllocator.TryResolveEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceShape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> FanInLayoutPoints)
                && FanInLayoutPoints.Count >= 2)
            {
                List<(double X, double Y)> FanInSvgPoints = [];
                foreach ((double LayoutX, double LayoutY) in FanInLayoutPoints)
                {
                    FanInSvgPoints.Add(LayoutToSvgPoint(LayoutX, LayoutY, MinLeft, MaxTop, DiagramPaddingPx));
                }

                if (FanInSvgPoints.Count == 2
                    || FlowchartEdgeConnectionGeometry.IsCollinearStubStraightRoute(FanInSvgPoints))
                {
                    (double FanInStartX, double FanInStartY) = FanInSvgPoints[0];
                    (double FanInTipX, double FanInTipY) = FanInSvgPoints[FanInSvgPoints.Count - 1];
                    RenderStraightEdgeSegment(
                        Builder,
                        Edge,
                        FanInStartX,
                        FanInStartY,
                        FanInTipX,
                        FanInTipY,
                        EdgeStyle,
                        RenderOptions);
                }
                else
                {
                    string FanInPathData = Direction == FlowchartDirection.LeftToRight
                        || Direction == FlowchartDirection.RightToLeft
                        ? FlowchartCurvePathBuilder.BuildHorizontalFanRoutePathData(FanInSvgPoints)
                        : FlowchartCurvePathBuilder.BuildVerticalFanRoutePathData(FanInSvgPoints);
                    AppendStyledEdgePath(Builder, FanInPathData, EdgeStyle, Edge.SourceId, Edge.TargetId);

                    if (FanInSvgPoints.Count > 2)
                    {
                        bool HorizontalFanFlow = Direction == FlowchartDirection.LeftToRight
                            || Direction == FlowchartDirection.RightToLeft;
                        AppendCurvedEdgeMarkers(
                            Builder,
                            Edge,
                            FanInPathData,
                            FanInSvgPoints,
                            ResolveFanRenderedPathKind(FanInSvgPoints, HorizontalFanFlow),
                            EdgeStyle);
                    }
                }

                AppendResolvedEdgeLabel(
                    Builder,
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    FanInLayoutPoints,
                    Direction,
                    RouteClass,
                    MinLeft,
                    MaxTop,
                    DiagramPaddingPx,
                    EdgeStyle,
                    RenderOptions);

                continue;
            }

            if (FlowchartDecisionPortAllocator.TryResolveEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceShape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> DecisionLayoutPoints)
                && DecisionLayoutPoints.Count >= 2)
            {
                List<(double X, double Y)> DecisionSvgPoints = [];
                foreach ((double LayoutX, double LayoutY) in DecisionLayoutPoints)
                {
                    DecisionSvgPoints.Add(LayoutToSvgPoint(LayoutX, LayoutY, MinLeft, MaxTop, DiagramPaddingPx));
                }

                if (DecisionLayoutPoints.Count == 2)
                {
                    (double LayoutBoundaryX, double LayoutBoundaryY) = DecisionLayoutPoints[0];
                    (double LayoutTipX, double LayoutTipY) = DecisionLayoutPoints[1];
                    (double LayoutStartX, double LayoutStartY) = FlowchartEdgeTerminalStubExpander.ResolveExteriorStubPoint(
                        SourceBounds,
                        SourceShape,
                        LayoutBoundaryX,
                        LayoutBoundaryY,
                        LayoutTipX,
                        LayoutTipY);
                    (double DecisionBoundaryX, double DecisionBoundaryY) = LayoutToSvgPoint(
                        LayoutBoundaryX,
                        LayoutBoundaryY,
                        MinLeft,
                        MaxTop,
                        DiagramPaddingPx);
                    (double DecisionStartX, double DecisionStartY) = LayoutToSvgPoint(
                        LayoutStartX,
                        LayoutStartY,
                        MinLeft,
                        MaxTop,
                        DiagramPaddingPx);
                    (double DecisionTipX, double DecisionTipY) = LayoutToSvgPoint(
                        LayoutTipX,
                        LayoutTipY,
                        MinLeft,
                        MaxTop,
                        DiagramPaddingPx);
                    AppendStyledLine(
                        Builder,
                        DecisionBoundaryX,
                        DecisionBoundaryY,
                        DecisionStartX,
                        DecisionStartY,
                        EdgeStyle);
                    RenderStraightEdgeSegment(
                        Builder,
                        Edge,
                        DecisionStartX,
                        DecisionStartY,
                        DecisionTipX,
                        DecisionTipY,
                        EdgeStyle,
                        RenderOptions);
                }
                else
                {
                    string DecisionPathData = FlowchartCurvePathBuilder.BuildDecisionBranchPathData(DecisionSvgPoints);
                    AppendStyledEdgePath(Builder, DecisionPathData, EdgeStyle, Edge.SourceId, Edge.TargetId);

                    if (DecisionSvgPoints.Count > 2)
                    {
                        AppendCurvedEdgeMarkers(
                            Builder,
                            Edge,
                            DecisionPathData,
                            DecisionSvgPoints,
                            FlowchartRenderedPathKind.DecisionBranch,
                            EdgeStyle);
                    }
                }

                AppendResolvedEdgeLabel(
                    Builder,
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    DecisionLayoutPoints,
                    Direction,
                    RouteClass,
                    MinLeft,
                    MaxTop,
                    DiagramPaddingPx,
                    EdgeStyle,
                    RenderOptions);

                continue;
            }

            if (FlowchartTerminalCollectorPortAllocator.TryResolveEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceShape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> TerminalCollectorLayoutPoints)
                && TerminalCollectorLayoutPoints.Count >= 2)
            {
                RenderResolvedEdgeRoute(
                    Builder,
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    TerminalCollectorLayoutPoints,
                    FlowchartTerminalCollectorPortAllocator.IsSpineStraightRoute(TerminalCollectorLayoutPoints)
                        ? FlowchartEdgeRouteKind.Straight
                        : FlowchartEdgeRouteKind.Corridor,
                    Direction,
                    RouteClass,
                    MinLeft,
                    MaxTop,
                    DiagramPaddingPx,
                    EdgeStyle,
                    RenderOptions);
            }
        }

        foreach (NodeContract Node in NodesInPaintOrder)
        {
            if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Node.Id))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            if (!NodeLayoutById.TryGetValue(Node.Id, out (double Left, double Top, double Width, double Height) NodeLayout))
            {
                continue;
            }

            FlowchartResolvedStyle NodeStyle = RenderOptions.ResolveNodeStyle(Node);
            AppendNodeShape(Builder, Node, NodeLayout.Left, NodeLayout.Top, NodeLayout.Width, NodeLayout.Height, NodeStyle);
        }

        RenderSession.FlushDeferredMarkers(Builder);

        foreach (NodeContract Node in NodesInPaintOrder)
        {
            if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Node.Id))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            if (!NodeLayoutById.TryGetValue(Node.Id, out (double Left, double Top, double Width, double Height) NodeLayout))
            {
                continue;
            }

            FlowchartResolvedStyle NodeStyle = RenderOptions.ResolveNodeStyle(Node);
            FlowchartNodeDimensions Dimensions = FlowchartNodeSizeEstimator.Estimate(Node, RenderOptions);
            string SourceLabel = string.IsNullOrEmpty(Node.Label) ? Node.Id : Node.Label;
            string RenderLabel = string.IsNullOrWhiteSpace(Dimensions.DisplayLabel)
                ? SourceLabel
                : Dimensions.DisplayLabel;

            double LabelCentreX = NodeLayout.Left + (NodeLayout.Width / 2.0);
            double LabelCentreY = NodeLayout.Top + (NodeLayout.Height / 2.0);
            if (string.Equals(Node.Shape, "cylinder", StringComparison.OrdinalIgnoreCase))
            {
                LabelCentreY += FlowchartShapePathGenerator.GetCylinderEllipseRadiusY(NodeLayout.Width) / 2.0;
            }

            AppendNodeLabel(
                Builder,
                RenderLabel,
                LabelCentreX,
                LabelCentreY,
                NodeStyle,
                RenderOptions,
                SourceLabel);
        }

        Builder.Append("</svg>");
        return FlowchartSvgLineJump.Apply(Builder.ToString());
        }
        finally
        {
            _ActiveRenderSession = null;
        }
    }

    private sealed class FlowchartSvgRenderSession
    {
        private readonly List<Action<StringBuilder>> _DeferredMarkerDraws = [];

        public void DeferMarkerDraw(Action<StringBuilder> DrawMarker) => _DeferredMarkerDraws.Add(DrawMarker);

        public void FlushDeferredMarkers(StringBuilder Builder)
        {
            FlowchartSvgRenderSession? PreviousSession = _ActiveRenderSession;
            _ActiveRenderSession = null;
            try
            {
                foreach (Action<StringBuilder> DrawMarker in _DeferredMarkerDraws)
                {
                    DrawMarker(Builder);
                }
            }
            finally
            {
                _ActiveRenderSession = PreviousSession;
            }
        }
    }

    private static void AppendNodeShape(
        StringBuilder Builder,
        NodeContract Node,
        double Left,
        double Top,
        double Width,
        double Height,
        FlowchartResolvedStyle NodeStyle)
    {
        bool IsCylinder = string.Equals(Node.Shape, "cylinder", StringComparison.OrdinalIgnoreCase);
        string FillPath = FlowchartShapePathGenerator.GetFillPath(Node.Shape, Left, Top, Width, Height);

        if (IsCylinder)
        {
            AppendStyledPath(Builder, FillPath, NodeStyle, FillOpacity: null, StrokeOverride: "none");
        }
        else
        {
            AppendStyledPath(Builder, FillPath, NodeStyle, FillOpacity: null, StrokeOverride: null);
        }

        string? StrokeOverlayPath = FlowchartShapePathGenerator.GetStrokeOverlayPath(Node.Shape, Left, Top, Width, Height);
        if (StrokeOverlayPath is not null)
        {
            AppendStyledPath(Builder, StrokeOverlayPath, NodeStyle, FillOpacity: "0", StrokeOverride: null);
        }
    }

    private static void AppendStyledPath(
        StringBuilder Builder,
        string PathData,
        FlowchartResolvedStyle Style,
        string? FillOpacity,
        string? StrokeOverride)
    {
        Builder.Append("<path d=\"");
        Builder.Append(PathData);
        Builder.Append("\" fill=\"");
        Builder.Append(EscapeXml(Style.Fill));
        Builder.Append('\"');

        if (FillOpacity is not null)
        {
            Builder.Append(" fill-opacity=\"");
            Builder.Append(FillOpacity);
            Builder.Append('\"');
        }

        Builder.Append(" stroke=\"");
        Builder.Append(EscapeXml(StrokeOverride ?? Style.Stroke));
        Builder.Append("\" stroke-width=\"");
        Builder.Append(Style.StrokeWidth.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" stroke-linejoin=\"round\" stroke-linecap=\"round\"");

        if (!string.IsNullOrEmpty(Style.StrokeDasharray))
        {
            Builder.Append(" stroke-dasharray=\"");
            Builder.Append(EscapeXml(Style.StrokeDasharray));
            Builder.Append('\"');
        }

        Builder.Append("/>");
    }

    private static double GetMarkerInset(EdgeEndpointMarker Marker)
    {
        if (Marker == EdgeEndpointMarker.Point)
        {
            return ARROWHEAD_LENGTH_PX;
        }

        if (Marker == EdgeEndpointMarker.Circle)
        {
            return CIRCLE_MARKER_INSET_PX;
        }

        if (Marker == EdgeEndpointMarker.Cross)
        {
            return CROSS_MARKER_INSET_PX;
        }

        return 0.0;
    }

    private static void AppendCircleMarker(StringBuilder Builder, double CentreX, double CentreY, string Stroke)
    {
        Builder.Append("<circle cx=\"");
        Builder.Append(CentreX.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" cy=\"");
        Builder.Append(CentreY.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" r=\"");
        Builder.Append(CIRCLE_MARKER_RADIUS_PX.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"");
        Builder.Append(EscapeXml(Stroke));
        Builder.Append("\" stroke=\"none\"/>");
    }

    private static void AppendCrossMarker(
        StringBuilder Builder,
        double CentreX,
        double CentreY,
        double TowardX,
        double TowardY,
        string Stroke,
        double StrokeWidth)
    {
        double DirX = CentreX - TowardX;
        double DirY = CentreY - TowardY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            DirX = 1.0;
            DirY = 0.0;
            Length = 1.0;
        }

        DirX /= Length;
        DirY /= Length;
        double PerpX = -DirY;
        double PerpY = DirX;
        double Half = CROSS_MARKER_HALF_PX;

        double Ax = CentreX + (PerpX * Half) + (DirX * Half);
        double Ay = CentreY + (PerpY * Half) + (DirY * Half);
        double Bx = CentreX - (PerpX * Half) - (DirX * Half);
        double By = CentreY - (PerpY * Half) - (DirY * Half);
        double Cx = CentreX + (PerpX * Half) - (DirX * Half);
        double Cy = CentreY + (PerpY * Half) - (DirY * Half);
        double Dx = CentreX - (PerpX * Half) + (DirX * Half);
        double Dy = CentreY - (PerpY * Half) + (DirY * Half);

        AppendCrossLine(Builder, Ax, Ay, Bx, By, Stroke, StrokeWidth);
        AppendCrossLine(Builder, Cx, Cy, Dx, Dy, Stroke, StrokeWidth);
    }

    private static void AppendCrossLine(
        StringBuilder Builder,
        double X1,
        double Y1,
        double X2,
        double Y2,
        string Stroke,
        double StrokeWidth)
    {
        Builder.Append("<line x1=\"");
        Builder.Append(X1.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" y1=\"");
        Builder.Append(Y1.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" x2=\"");
        Builder.Append(X2.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" y2=\"");
        Builder.Append(Y2.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" stroke=\"");
        Builder.Append(EscapeXml(Stroke));
        Builder.Append("\" stroke-width=\"");
        Builder.Append(StrokeWidth.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" stroke-linecap=\"round\"/>");
    }

    private static void AppendEdgeLabel(
        StringBuilder Builder,
        FlowchartLabelDimensions LabelDimensions,
        double CentreX,
        double CentreY,
        FlowchartResolvedStyle EdgeStyle,
        FlowchartRenderOptions Options,
        string? RawLabel = null)
    {
        string LabelForRender = string.IsNullOrWhiteSpace(RawLabel) ? LabelDimensions.DisplayText : RawLabel;
        if (Options.HtmlLabels && FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup(LabelForRender))
        {
            FlowchartRichLabelSvgWriter.AppendCentredLabel(
                Builder,
                LabelForRender,
                CentreX,
                CentreY,
                Options.EdgeLabelFontSizePx,
                EdgeStyle.Stroke,
                EdgeStyle.FontFamily,
                EdgeStyle.FontWeight,
                FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR);
            return;
        }

        string[] Lines = LabelDimensions.DisplayText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        double EdgeLabelFontSizePx = Options.EdgeLabelFontSizePx;
        double GapWidth = Math.Max(12.0, LabelDimensions.Width + 6.0);
        double GapHeight = Math.Max(EdgeLabelFontSizePx, LabelDimensions.Height) + 4.0;
        Builder.Append("<rect x=\"");
        Builder.Append((CentreX - (GapWidth / 2.0)).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" y=\"");
        Builder.Append((CentreY - (GapHeight / 2.0)).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" width=\"");
        Builder.Append(GapWidth.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" height=\"");
        Builder.Append(GapHeight.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"#ffffff\" stroke=\"none\"/>");
        double LineHeightPx = EdgeLabelFontSizePx * FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR;
        double FirstLineY = FlowchartRichLabelSvgWriter.ComputeFirstBaselineY(
            CentreY,
            Lines.Length,
            EdgeLabelFontSizePx,
            FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR);

        Builder.Append("<text x=\"");
        Builder.Append(CentreX.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" y=\"");
        Builder.Append(FirstLineY.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" text-anchor=\"middle\" fill=\"");
        Builder.Append(EscapeXml(EdgeStyle.Stroke));
        Builder.Append("\" font-family=\"");
        Builder.Append(EscapeXml(EdgeStyle.FontFamily));
        Builder.Append("\" font-size=\"");
        Builder.Append(EdgeLabelFontSizePx.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" font-weight=\"");
        Builder.Append(EscapeXml(EdgeStyle.FontWeight));
        Builder.Append("\">");

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            if (LineIndex == 0)
            {
                Builder.Append("<tspan x=\"");
                Builder.Append(CentreX.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\" dy=\"0\">");
            }
            else
            {
                Builder.Append("<tspan x=\"");
                Builder.Append(CentreX.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\" dy=\"");
                Builder.Append(LineHeightPx.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\">");
            }

            Builder.Append(EscapeXml(Lines[LineIndex]));
            Builder.Append("</tspan>");
        }

        Builder.Append("</text>");
    }

    private static (double X, double Y) GetUnitDirection(double FromX, double FromY, double ToX, double ToY)
    {
        double DirX = ToX - FromX;
        double DirY = ToY - FromY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return (1.0, 0.0);
        }

        return (DirX / Length, DirY / Length);
    }

    private static (double X, double Y) ShortenSegmentFromStart(
        double X1,
        double Y1,
        double X2,
        double Y2,
        double ShortenPx)
    {
        double DirX = X2 - X1;
        double DirY = Y2 - Y1;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length <= ShortenPx)
        {
            return (X2, Y2);
        }

        double Scale = ShortenPx / Length;
        return (X1 + (DirX * Scale), Y1 + (DirY * Scale));
    }

    internal static void ExpandLayoutBoundsForRetryLoops(
        DiagramContract Contract,
        FlowchartDirection Direction,
        IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        ref double MinLeft,
        ref double MinBottom,
        ref double MaxRight,
        ref double MaxTop)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;

            if (!FlowchartEdgePathBuilder.IsLrRetryLoopEdge(
                    Edge,
                    Contract,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    TargetShape))
            {
                continue;
            }

            if (!TryBuildRetryLoopLayoutPoints(Edge, Contract, BoundsById, NodeById, NodeBounds, Direction, out List<(double X, double Y)> LayoutPoints))
            {
                continue;
            }

            foreach ((double LayoutX, double LayoutY) in LayoutPoints)
            {
                if (LayoutX < MinLeft)
                {
                    MinLeft = LayoutX;
                }

                if (LayoutX > MaxRight)
                {
                    MaxRight = LayoutX;
                }

                if (LayoutY < MinBottom)
                {
                    MinBottom = LayoutY;
                }

                if (LayoutY > MaxTop)
                {
                    MaxTop = LayoutY;
                }
            }
        }
    }

    /// <summary>
    /// Builds the guide points for an LR/RL retry loop. Loops that share a node with another retry loop get nested corridors;
    /// a lone loop uses the single corridor above the nodes (F34).
    /// </summary>
    private static bool TryBuildRetryLoopLayoutPoints(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        if (FlowchartEdgePathBuilder.TryBuildNestedLrRetryLoopPath(Edge, Contract, BoundsById, NodeById, Direction, out LayoutPoints))
        {
            return true;
        }

        return FlowchartEdgePathBuilder.TryBuildLrRetryLoopPath(BoundsById[Edge.SourceId], BoundsById[Edge.TargetId], NodeBounds, Direction, out LayoutPoints, Edge.MarkerStart, Edge.MarkerEnd);
    }

    private static void RenderResolvedEdgeRoute(
        StringBuilder Builder,
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction,
        string? RouteClass,
        double MinLeft,
        double MaxTop,
        double DiagramPaddingPx,
        FlowchartResolvedStyle EdgeStyle,
        FlowchartRenderOptions RenderOptions)
    {
        List<(double X, double Y)> GuideSvgPoints = [];
        foreach ((double LayoutX, double LayoutY) in LayoutPoints)
        {
            GuideSvgPoints.Add(LayoutToSvgPoint(LayoutX, LayoutY, MinLeft, MaxTop, DiagramPaddingPx));
        }

        bool StraightIsAxisAligned = RouteKind == FlowchartEdgeRouteKind.Straight
            && GuideSvgPoints.Count >= 2
            && (Math.Abs(GuideSvgPoints[0].X - GuideSvgPoints[GuideSvgPoints.Count - 1].X) <= 0.6
                || Math.Abs(GuideSvgPoints[0].Y - GuideSvgPoints[GuideSvgPoints.Count - 1].Y) <= 0.6);
        if (StraightIsAxisAligned)
        {
            (double LayoutBoundaryX, double LayoutBoundaryY) = LayoutPoints[0];
            (double LayoutTipX, double LayoutTipY) = LayoutPoints[LayoutPoints.Count - 1];
            (double X1, double Y1) = GuideSvgPoints[0];
            (double TipX, double TipY) = GuideSvgPoints[GuideSvgPoints.Count - 1];

            if (FlowchartDiamondAttachHelper.IsDiamondShape(SourceShape))
            {
                (double LayoutStartX, double LayoutStartY) = FlowchartEdgeTerminalStubExpander.ResolveExteriorStubPoint(
                    SourceBounds,
                    SourceShape,
                    LayoutBoundaryX,
                    LayoutBoundaryY,
                    LayoutTipX,
                    LayoutTipY);
                (X1, Y1) = LayoutToSvgPoint(LayoutStartX, LayoutStartY, MinLeft, MaxTop, DiagramPaddingPx);
                (double BoundaryX, double BoundaryY) = GuideSvgPoints[0];
                AppendStyledLine(Builder, BoundaryX, BoundaryY, X1, Y1, EdgeStyle);
            }

            RenderStraightEdgeSegment(
                Builder,
                Edge,
                X1,
                Y1,
                TipX,
                TipY,
                EdgeStyle,
                RenderOptions);
        }
        else if (RouteKind == FlowchartEdgeRouteKind.Corridor
            || RouteKind == FlowchartEdgeRouteKind.Grid)
        {
            string PathData = FlowchartCurvePathBuilder.BuildCorridorRoutePathData(GuideSvgPoints, RouteKind);
            AppendStyledEdgePath(Builder, PathData, EdgeStyle, Edge.SourceId, Edge.TargetId);

            if (GuideSvgPoints.Count >= 2)
            {
                FlowchartRenderedPathKind PathKind = FlowchartCurvePathBuilder.ResolveRenderedPathKind(
                    LayoutPoints,
                    RouteKind,
                    Direction);
                AppendCurvedEdgeMarkers(
                    Builder,
                    Edge,
                    PathData,
                    GuideSvgPoints,
                    PathKind,
                    EdgeStyle);
            }
        }
        else if (RouteKind == FlowchartEdgeRouteKind.Decision && LayoutPoints.Count == 2)
        {
            (double LayoutBoundaryX, double LayoutBoundaryY) = LayoutPoints[0];
            (double LayoutTipX, double LayoutTipY) = LayoutPoints[1];
            (double LayoutStartX, double LayoutStartY) = FlowchartEdgeTerminalStubExpander.ResolveExteriorStubPoint(
                SourceBounds,
                SourceShape,
                LayoutBoundaryX,
                LayoutBoundaryY,
                LayoutTipX,
                LayoutTipY);
            (double BoundaryX, double BoundaryY) = LayoutToSvgPoint(
                LayoutBoundaryX,
                LayoutBoundaryY,
                MinLeft,
                MaxTop,
                DiagramPaddingPx);
            (double StartX, double StartY) = LayoutToSvgPoint(
                LayoutStartX,
                LayoutStartY,
                MinLeft,
                MaxTop,
                DiagramPaddingPx);
            (double TipX, double TipY) = GuideSvgPoints[1];
            AppendStyledLine(Builder, BoundaryX, BoundaryY, StartX, StartY, EdgeStyle);
            RenderStraightEdgeSegment(Builder, Edge, StartX, StartY, TipX, TipY, EdgeStyle, RenderOptions);
        }
        else
        {
            string PathData = FlowchartCurvePathBuilder.BuildRenderedEdgePathData(
                GuideSvgPoints,
                RouteKind,
                Direction);
            if (string.IsNullOrWhiteSpace(PathData))
            {
                PathData = FlowchartCurvePathBuilder.BuildRenderedEdgePathData(
                    FlowchartPaddedVisibilityRouter.EnsureOrthogonal(GuideSvgPoints),
                    FlowchartEdgeRouteKind.Corridor,
                    Direction);
            }

            AppendStyledEdgePath(Builder, PathData, EdgeStyle, Edge.SourceId, Edge.TargetId);

            if (GuideSvgPoints.Count >= 2)
            {
                FlowchartRenderedPathKind PathKind = FlowchartCurvePathBuilder.ResolveRenderedPathKind(
                    LayoutPoints,
                    RouteKind,
                    Direction);
                AppendCurvedEdgeMarkers(
                    Builder,
                    Edge,
                    PathData,
                    GuideSvgPoints,
                    PathKind,
                    EdgeStyle);
            }
        }

        AppendResolvedEdgeLabel(
            Builder,
            Edge,
            Contract,
            BoundsById,
            SourceBounds,
            TargetBounds,
            SourceShape,
            LayoutPoints,
            RouteKind,
            Direction,
            RouteClass,
            MinLeft,
            MaxTop,
            DiagramPaddingPx,
            EdgeStyle,
            RenderOptions);
    }

    private static void AppendResolvedEdgeLabel(
        StringBuilder Builder,
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartDirection Direction,
        string? RouteClass,
        double MinLeft,
        double MaxTop,
        double DiagramPaddingPx,
        FlowchartResolvedStyle EdgeStyle,
        FlowchartRenderOptions RenderOptions)
    {
        AppendResolvedEdgeLabel(
            Builder,
            Edge,
            Contract,
            BoundsById,
            SourceBounds,
            TargetBounds,
            SourceShape,
            LayoutPoints,
            FlowchartEdgeRouteKind.Curved,
            Direction,
            RouteClass,
            MinLeft,
            MaxTop,
            DiagramPaddingPx,
            EdgeStyle,
            RenderOptions);
    }

    private static void AppendResolvedEdgeLabel(
        StringBuilder Builder,
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction,
        string? RouteClass,
        double MinLeft,
        double MaxTop,
        double DiagramPaddingPx,
        FlowchartResolvedStyle EdgeStyle,
        FlowchartRenderOptions RenderOptions)
    {
        if (string.IsNullOrWhiteSpace(Edge.Label))
        {
            return;
        }

        FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            Edge.Label,
            RenderOptions,
            RenderOptions.EdgeLabelFontSizePx);
        (double LabelX, double LabelY) = ResolveDrawnEdgeLabelCentre(Edge, Contract, BoundsById, SourceBounds, TargetBounds, SourceShape, LayoutPoints, Direction, RouteClass, RenderOptions, RouteKind);
        (LabelX, LabelY) = LayoutToSvgPoint(LabelX, LabelY, MinLeft, MaxTop, DiagramPaddingPx);
        AppendEdgeLabel(Builder, LabelDimensions, LabelX, LabelY, EdgeStyle, RenderOptions, Edge.Label);
    }

    /// <summary>
    /// Returns the centre of an edge label where it is drawn, in layout coordinates (Y up).
    /// This is the placement position from <see cref="FlowchartEdgeLabelPlacementHelper"/>, moved so the label box does not cover its own connector, other connectors or nodes (spec §15.0C.8).
    /// Rule validation calls this so it checks labels against the same position the renderer uses.
    /// </summary>
    /// <param name="Edge">The labelled edge.</param>
    /// <param name="Contract">The diagram the edge belongs to.</param>
    /// <param name="BoundsById">Layout bounds for every node, keyed by node id.</param>
    /// <param name="SourceBounds">Layout bounds of the edge source.</param>
    /// <param name="TargetBounds">Layout bounds of the edge target.</param>
    /// <param name="SourceShape">Shape name of the edge source, or null.</param>
    /// <param name="LayoutPoints">The edge guide points in layout coordinates.</param>
    /// <param name="Direction">The diagram direction.</param>
    /// <param name="RouteClass">The semantic route class name, or null.</param>
    /// <param name="RenderOptions">Render options used to measure the label.</param>
    /// <param name="RouteKind">The resolved route kind used to flatten the drawn stroke.</param>
    /// <returns>The drawn label centre in layout coordinates.</returns>
    public static (double X, double Y) ResolveDrawnEdgeLabelCentre(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartDirection Direction,
        string? RouteClass,
        FlowchartRenderOptions RenderOptions,
        FlowchartEdgeRouteKind RouteKind = FlowchartEdgeRouteKind.Curved)
    {
        FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? string.Empty, RenderOptions, RenderOptions.EdgeLabelFontSizePx);
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
        double LabelHeight = FlowchartEdgeLabelPlacementHelper.ComputeRenderedEdgeLabelHeight(LabelDimensions, RenderOptions.EdgeLabelFontSizePx);
        if (LayoutPoints.Count > 2
            && !FlowchartCurvePathBuilder.TryCollapseAxisAlignedEndpoints(LayoutPoints, out _)
            && !IsAxisAlignedPolyline(LayoutPoints)
            && TryCurveMidpoint(LayoutPoints, out double CurveX, out double CurveY))
        {
            return (CurveX, CurveY);
        }

        (double ClearedX, double ClearedY) = MoveLayoutLabelClearOfConnector(Edge, Contract, LabelX, LabelY, LabelDimensions.Width, LabelHeight, LayoutPoints, BoundsById, SourceBounds, TargetBounds, Direction);
        FlowchartEdgeRouteKind FlattenKind = RouteKind;
        if (LayoutPoints.Count <= 2)
        {
            FlattenKind = FlowchartEdgeRouteKind.Straight;
        }
        else if (FlattenKind != FlowchartEdgeRouteKind.Corridor
            && FlowchartSharedRouteQualityHelper.LooksLikeOrthogonalCorridor(LayoutPoints))
        {
            FlattenKind = FlowchartEdgeRouteKind.Corridor;
        }

        List<(double X, double Y)> RenderedSamples = FlowchartSharedEdgeRouter.FlattenRenderedGuide(
            LayoutPoints,
            FlattenKind,
            Direction);
        if (RenderedSamples.Count >= 2
            && (FlowchartSharedRouteQualityHelper.LooksLikeOrthogonalCorridor(LayoutPoints)
                || IsAxisAlignedPolyline(LayoutPoints)))
        {
            return FlowchartSharedRouteQualityHelper.ResolveLabelOnOwnEdge(
                RenderedSamples,
                LabelDimensions.Width,
                LabelHeight,
                BoundsById,
                Edge.SourceId,
                Edge.TargetId,
                0.0);
        }

        if (RenderedSamples.Count >= 2
            && FlowchartSharedRouteQualityHelper.DistanceFromPointToPolyline(ClearedX, ClearedY, RenderedSamples)
            > 6.5)
        {
            return FlowchartSharedRouteQualityHelper.ResolveLabelOnOwnEdge(
                RenderedSamples,
                LabelDimensions.Width,
                LabelHeight,
                BoundsById,
                Edge.SourceId,
                Edge.TargetId,
                6.0);
        }

        return (ClearedX, ClearedY);
    }

    private static bool TryCurveMidpoint(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        out double X,
        out double Y)
    {
        X = 0.0;
        Y = 0.0;
        string PathData = FlowchartCurvePathBuilder.BuildPureD3CurveBasisPathData(LayoutPoints);
        if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples)
            || Samples.Count < 2)
        {
            return false;
        }

        double Total = 0.0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double Dx = Samples[Index].X - Samples[Index - 1].X;
            double Dy = Samples[Index].Y - Samples[Index - 1].Y;
            Total += Math.Sqrt((Dx * Dx) + (Dy * Dy));
        }

        double Remaining = Total / 2.0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double Dx = Samples[Index].X - Samples[Index - 1].X;
            double Dy = Samples[Index].Y - Samples[Index - 1].Y;
            double Segment = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (Segment >= Remaining && Segment > 1e-9)
            {
                double Ratio = Remaining / Segment;
                X = Samples[Index - 1].X + (Dx * Ratio);
                Y = Samples[Index - 1].Y + (Dy * Ratio);
                return true;
            }

            Remaining -= Segment;
        }

        X = Samples[Samples.Count - 1].X;
        Y = Samples[Samples.Count - 1].Y;
        return true;
    }

    private static bool IsAxisAlignedPolyline(IReadOnlyList<(double X, double Y)> LayoutPoints)
    {
        if (LayoutPoints.Count < 2)
        {
            return false;
        }

        for (int Index = 1; Index < LayoutPoints.Count; Index++)
        {
            double DeltaX = Math.Abs(LayoutPoints[Index].X - LayoutPoints[Index - 1].X);
            double DeltaY = Math.Abs(LayoutPoints[Index].Y - LayoutPoints[Index - 1].Y);
            if (DeltaX > 1.5 && DeltaY > 1.5)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Moves a label centre (layout coordinates, Y up) so the label box does not cover its connector.
    /// Canvas sizing and label drawing both call this, so the canvas always fits the moved label.
    /// </summary>
    private static (double X, double Y) MoveLayoutLabelClearOfConnector(
        EdgeContract Edge,
        DiagramContract Contract,
        double LabelX,
        double LabelY,
        double LabelWidth,
        double LabelHeight,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction)
    {
        List<(double X, double Y)> GuideRenderPoints = [];
        foreach ((double LayoutX, double LayoutY) in LayoutPoints)
        {
            GuideRenderPoints.Add((LayoutX, -LayoutY));
        }

        List<IReadOnlyList<(double X, double Y)>> OtherRenderPoints = CollectOtherEdgeRenderGuidePoints(Edge, Contract, BoundsById, Direction);

        HashSet<string> DiamondIds = new HashSet<string>(Contract.Nodes.Where(Node => IsDecisionShape(Node.Shape)).Select(Node => Node.Id), StringComparer.Ordinal);
        List<FlowchartSvgRect> NodeRects = [];
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            if (DiamondIds.Contains(Bounds.NodeId))
            {
                AppendDiamondRenderStrips(NodeRects, Bounds);
                continue;
            }

            NodeRects.Add(new FlowchartSvgRect(Bounds.CenterX - (Bounds.Width / 2.0), -(Bounds.CenterY + (Bounds.Height / 2.0)), Bounds.CenterX + (Bounds.Width / 2.0), -(Bounds.CenterY - (Bounds.Height / 2.0))));
        }

        double PreferredSign = ResolveLabelClearanceSign(SourceBounds, TargetBounds, Direction);
        (double RenderX, double RenderY) = FlowchartEdgeLabelClearanceHelper.MoveClearOfConnector(LabelX, -LabelY, LabelWidth, LabelHeight, GuideRenderPoints, Direction, NodeRects, PreferredSign, OtherRenderPoints);
        return (RenderX, -RenderY);
    }

    /// <summary>
    /// Adds a diamond to the label obstacle list as thin horizontal strips that follow its sloped sides
    /// (render orientation, X and -Y), so a label may sit in the empty corners of the diamond's bounding box.
    /// </summary>
    private static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase) || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendDiamondRenderStrips(List<FlowchartSvgRect> NodeRects, FlowchartLayoutNodeBounds Bounds)
    {
        const int DIAMOND_STRIP_COUNT = 16;
        double HalfWidth = Bounds.Width / 2.0;
        double HalfHeight = Bounds.Height / 2.0;
        double StripHeight = Bounds.Height / DIAMOND_STRIP_COUNT;
        for (int StripIndex = 0; StripIndex < DIAMOND_STRIP_COUNT; StripIndex++)
        {
            double StripTop = (Bounds.CenterY + HalfHeight) - (StripIndex * StripHeight);
            double StripBottom = StripTop - StripHeight;
            double NearestToCentre = Math.Min(Math.Abs(StripTop - Bounds.CenterY), Math.Abs(StripBottom - Bounds.CenterY));
            if (StripTop > Bounds.CenterY && StripBottom < Bounds.CenterY)
            {
                NearestToCentre = 0.0;
            }

            double StripHalfWidth = HalfWidth * (1.0 - (NearestToCentre / HalfHeight));
            NodeRects.Add(new FlowchartSvgRect(Bounds.CenterX - StripHalfWidth, -StripTop, Bounds.CenterX + StripHalfWidth, -StripBottom));
        }
    }

    /// <summary>
    /// Resolves the guide points of every visible edge except <paramref name="Edge"/>, flipped to render orientation (X, -Y).
    /// Used so a label is not placed on top of a neighbouring connector (F59 "No" beside the Wait loop-back).
    /// </summary>
    private static List<IReadOnlyList<(double X, double Y)>> CollectOtherEdgeRenderGuidePoints(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        List<IReadOnlyList<(double X, double Y)>> Result = [];
        Dictionary<string, NodeContract> NodeById = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            NodeById[Node.Id] = Node;
        }

        foreach (EdgeContract Other in Contract.Edges)
        {
            if (ReferenceEquals(Other, Edge) || string.Equals(Other.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Other,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> OtherPoints,
                    out FlowchartEdgeRouteKind _))
            {
                continue;
            }

            List<(double X, double Y)> RenderPoints = [];
            foreach ((double LayoutX, double LayoutY) in OtherPoints)
            {
                RenderPoints.Add((LayoutX, -LayoutY));
            }

            Result.Add(RenderPoints);
        }

        return Result;
    }

    /// <summary>
    /// Returns the preferred render-space direction for moving a label off its connector: away from the source across the flow.
    /// Straight connectors prefer the right side (top-to-bottom) or above the line (left-to-right).
    /// </summary>
    private static double ResolveLabelClearanceSign(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction)
    {
        const double ALIGNED_TOLERANCE_PX = 0.75;
        if (Direction == FlowchartDirection.TopToBottom || Direction == FlowchartDirection.BottomToTop)
        {
            double DeltaX = TargetBounds.CenterX - SourceBounds.CenterX;
            if (DeltaX < -ALIGNED_TOLERANCE_PX)
            {
                return -1.0;
            }

            return 1.0;
        }

        double LayoutDeltaY = TargetBounds.CenterY - SourceBounds.CenterY;
        if (LayoutDeltaY < -ALIGNED_TOLERANCE_PX)
        {
            return 1.0;
        }

        return -1.0;
    }

    private static void RenderStraightEdgeSegment(
        StringBuilder Builder,
        EdgeContract Edge,
        double X1,
        double Y1,
        double TipX,
        double TipY,
        FlowchartResolvedStyle EdgeStyle,
        FlowchartRenderOptions RenderOptions)
    {
        (double DirX, double DirY) = GetUnitDirection(X1, Y1, TipX, TipY);

        double LineStartX = X1;
        double LineStartY = Y1;
        double LineEndX = TipX;
        double LineEndY = TipY;

        double StartInset = GetMarkerInset(Edge.MarkerStart);
        double EndInset = GetMarkerInset(Edge.MarkerEnd);
        if (StartInset > 0.0)
        {
            (LineStartX, LineStartY) = ShortenSegmentFromStart(LineStartX, LineStartY, LineEndX, LineEndY, StartInset);
        }

        double RenderTipX = TipX;
        double RenderTipY = TipY;
        if (Edge.MarkerEnd == EdgeEndpointMarker.Point)
        {
            (RenderTipX, RenderTipY) = InsetArrowTipIntoNode(TipX, TipY, DirX, DirY);
        }

        if (EndInset > 0.0)
        {
            (LineEndX, LineEndY) = ShortenSegmentForArrowhead(LineStartX, LineStartY, RenderTipX, RenderTipY, EndInset);
        }

        AppendStyledLine(Builder, LineStartX, LineStartY, LineEndX, LineEndY, EdgeStyle, "butt", Edge.SourceId, Edge.TargetId);

        if (Edge.MarkerEnd == EdgeEndpointMarker.Point)
        {
            AppendArrowhead(Builder, RenderTipX, RenderTipY, LineEndX, LineEndY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerEnd == EdgeEndpointMarker.Circle)
        {
            double MarkerCentreX = TipX - (DirX * CIRCLE_MARKER_RADIUS_PX);
            double MarkerCentreY = TipY - (DirY * CIRCLE_MARKER_RADIUS_PX);
            AppendCircleMarker(Builder, MarkerCentreX, MarkerCentreY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerEnd == EdgeEndpointMarker.Cross)
        {
            double MarkerCentreX = TipX - (DirX * CROSS_MARKER_HALF_PX);
            double MarkerCentreY = TipY - (DirY * CROSS_MARKER_HALF_PX);
            AppendCrossMarker(Builder, MarkerCentreX, MarkerCentreY, LineEndX, LineEndY, EdgeStyle.Stroke, EdgeStyle.StrokeWidth);
        }

        if (Edge.MarkerStart == EdgeEndpointMarker.Point)
        {
            (double RenderStartTipX, double RenderStartTipY) = InsetArrowTipIntoNode(X1, Y1, -DirX, -DirY);
            AppendArrowhead(Builder, RenderStartTipX, RenderStartTipY, LineStartX, LineStartY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerStart == EdgeEndpointMarker.Circle)
        {
            double MarkerCentreX = X1 + (DirX * CIRCLE_MARKER_RADIUS_PX);
            double MarkerCentreY = Y1 + (DirY * CIRCLE_MARKER_RADIUS_PX);
            AppendCircleMarker(Builder, MarkerCentreX, MarkerCentreY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerStart == EdgeEndpointMarker.Cross)
        {
            double MarkerCentreX = X1 + (DirX * CROSS_MARKER_HALF_PX);
            double MarkerCentreY = Y1 + (DirY * CROSS_MARKER_HALF_PX);
            AppendCrossMarker(Builder, MarkerCentreX, MarkerCentreY, X1 + DirX, Y1 + DirY, EdgeStyle.Stroke, EdgeStyle.StrokeWidth);
        }

    }

    private static (double X, double Y) ComputeGuidePolylineMidpoint(IReadOnlyList<(double X, double Y)> GuidePoints)
    {
        if (GuidePoints.Count == 0)
        {
            return (0.0, 0.0);
        }

        if (GuidePoints.Count == 1)
        {
            return GuidePoints[0];
        }

        double TotalDistance = 0.0;
        for (int PointIndex = 1; PointIndex < GuidePoints.Count; PointIndex++)
        {
            (double PreviousX, double PreviousY) = GuidePoints[PointIndex - 1];
            (double PointX, double PointY) = GuidePoints[PointIndex];
            double DeltaX = PointX - PreviousX;
            double DeltaY = PointY - PreviousY;
            TotalDistance += Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        double RemainingDistance = TotalDistance / 2.0;
        for (int PointIndex = 1; PointIndex < GuidePoints.Count; PointIndex++)
        {
            (double PreviousX, double PreviousY) = GuidePoints[PointIndex - 1];
            (double PointX, double PointY) = GuidePoints[PointIndex];
            double DeltaX = PointX - PreviousX;
            double DeltaY = PointY - PreviousY;
            double SegmentDistance = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
            if (SegmentDistance <= 1e-7)
            {
                continue;
            }

            if (SegmentDistance < RemainingDistance)
            {
                RemainingDistance -= SegmentDistance;
                continue;
            }

            double DistanceRatio = RemainingDistance / SegmentDistance;
            return (
                PreviousX + (DeltaX * DistanceRatio),
                PreviousY + (DeltaY * DistanceRatio));
        }

        return GuidePoints[GuidePoints.Count - 1];
    }

    private static void AppendStyledEdgePath(
        StringBuilder Builder,
        string PathData,
        FlowchartResolvedStyle Style)
    {
        AppendStyledEdgePath(Builder, PathData, Style, null, null);
    }

    private static void AppendStyledEdgePath(
        StringBuilder Builder,
        string PathData,
        FlowchartResolvedStyle Style,
        string? SourceId,
        string? TargetId)
    {
        if (string.IsNullOrEmpty(PathData))
        {
            return;
        }

        Builder.Append("<path d=\"");
        Builder.Append(PathData);
        Builder.Append("\" fill=\"none\" stroke=\"");
        Builder.Append(EscapeXml(Style.Stroke));
        Builder.Append("\" stroke-width=\"");
        Builder.Append(Style.StrokeWidth.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" stroke-linejoin=\"round\" stroke-linecap=\"round\"");
        AppendEdgeIdentityAttributes(Builder, SourceId, TargetId);

        if (!string.IsNullOrEmpty(Style.StrokeDasharray))
        {
            Builder.Append(" stroke-dasharray=\"");
            Builder.Append(EscapeXml(Style.StrokeDasharray));
            Builder.Append('\"');
        }

        Builder.Append("/>");
    }

    private static void AppendEdgeIdentityAttributes(
        StringBuilder Builder,
        string? SourceId,
        string? TargetId)
    {
        if (string.IsNullOrEmpty(SourceId) || string.IsNullOrEmpty(TargetId))
        {
            return;
        }

        Builder.Append(" data-edge-source=\"");
        Builder.Append(EscapeXml(SourceId));
        Builder.Append("\" data-edge-target=\"");
        Builder.Append(EscapeXml(TargetId));
        Builder.Append('\"');
    }

    private static void AppendStyledLine(
        StringBuilder Builder,
        double X1,
        double Y1,
        double X2,
        double Y2,
        FlowchartResolvedStyle Style,
        string LineCap = "round")
    {
        AppendStyledLine(Builder, X1, Y1, X2, Y2, Style, LineCap, null, null);
    }

    private static void AppendStyledLine(
        StringBuilder Builder,
        double X1,
        double Y1,
        double X2,
        double Y2,
        FlowchartResolvedStyle Style,
        string LineCap,
        string? SourceId,
        string? TargetId)
    {
        Builder.Append("<line x1=\"");
        Builder.Append(X1.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" y1=\"");
        Builder.Append(Y1.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" x2=\"");
        Builder.Append(X2.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" y2=\"");
        Builder.Append(Y2.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" stroke=\"");
        Builder.Append(EscapeXml(Style.Stroke));
        Builder.Append("\" stroke-width=\"");
        Builder.Append(Style.StrokeWidth.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" stroke-linecap=\"");
        Builder.Append(EscapeXml(LineCap));
        Builder.Append('\"');
        AppendEdgeIdentityAttributes(Builder, SourceId, TargetId);

        if (!string.IsNullOrEmpty(Style.StrokeDasharray))
        {
            Builder.Append(" stroke-dasharray=\"");
            Builder.Append(EscapeXml(Style.StrokeDasharray));
            Builder.Append('\"');
        }

        Builder.Append("/>");
    }

    private static FlowchartRenderedPathKind ResolveFanRenderedPathKind(
        IReadOnlyList<(double X, double Y)> SvgPoints,
        bool HorizontalFanFlow)
    {
        if (SvgPoints.Count >= 5)
        {
            if (HorizontalFanFlow)
            {
                return FlowchartRenderedPathKind.HorizontalFanRoute;
            }

            return FlowchartRenderedPathKind.VerticalFanRoute;
        }

        return FlowchartRenderedPathKind.CurveBasisFromGuidePoints;
    }

    private static void AppendCurvedEdgeMarkers(
        StringBuilder Builder,
        EdgeContract Edge,
        string PathData,
        IReadOnlyList<(double X, double Y)> SvgPoints,
        FlowchartRenderedPathKind PathKind,
        FlowchartResolvedStyle EdgeStyle)
    {
        if (SvgPoints.Count < 2)
        {
            return;
        }

        (double StartX, double StartY) = SvgPoints[0];
        (double EndX, double EndY) = SvgPoints[SvgPoints.Count - 1];
        if (!FlowchartRenderedPathMarkerHelper.TryResolveEndpointApproachPoint(
                PathData,
                AtTargetEnd: true,
                out double TargetApproachX,
                out double TargetApproachY))
        {
            (TargetApproachX, TargetApproachY) = FlowchartCurvePathBuilder.ResolveMarkerApproachPoint(
                SvgPoints,
                PathKind,
                AtTargetEnd: true);
        }

        if (!FlowchartRenderedPathMarkerHelper.TryResolveEndpointApproachPoint(
                PathData,
                AtTargetEnd: false,
                out double SourceApproachX,
                out double SourceApproachY))
        {
            (SourceApproachX, SourceApproachY) = FlowchartCurvePathBuilder.ResolveMarkerApproachPoint(
                SvgPoints,
                PathKind,
                AtTargetEnd: false);
        }

        if (Edge.MarkerEnd == EdgeEndpointMarker.Point)
        {
            (double DirX, double DirY) = GetUnitDirection(TargetApproachX, TargetApproachY, EndX, EndY);
            (double RenderTipX, double RenderTipY) = InsetArrowTipIntoNode(EndX, EndY, DirX, DirY);
            AppendArrowhead(Builder, RenderTipX, RenderTipY, TargetApproachX, TargetApproachY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerEnd == EdgeEndpointMarker.Circle)
        {
            (double DirX, double DirY) = GetUnitDirection(TargetApproachX, TargetApproachY, EndX, EndY);
            double MarkerCentreX = EndX - (DirX * CIRCLE_MARKER_RADIUS_PX);
            double MarkerCentreY = EndY - (DirY * CIRCLE_MARKER_RADIUS_PX);
            AppendCircleMarker(Builder, MarkerCentreX, MarkerCentreY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerEnd == EdgeEndpointMarker.Cross)
        {
            (double DirX, double DirY) = GetUnitDirection(TargetApproachX, TargetApproachY, EndX, EndY);
            double MarkerCentreX = EndX - (DirX * CROSS_MARKER_HALF_PX);
            double MarkerCentreY = EndY - (DirY * CROSS_MARKER_HALF_PX);
            AppendCrossMarker(Builder, MarkerCentreX, MarkerCentreY, EndX, EndY, EdgeStyle.Stroke, EdgeStyle.StrokeWidth);
        }

        if (Edge.MarkerStart == EdgeEndpointMarker.Point)
        {
            (double DirStartX, double DirStartY) = GetUnitDirection(SourceApproachX, SourceApproachY, StartX, StartY);
            (double RenderStartTipX, double RenderStartTipY) = InsetArrowTipIntoNode(StartX, StartY, DirStartX, DirStartY);
            AppendArrowhead(Builder, RenderStartTipX, RenderStartTipY, SourceApproachX, SourceApproachY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerStart == EdgeEndpointMarker.Circle)
        {
            (double DirStartX, double DirStartY) = GetUnitDirection(SourceApproachX, SourceApproachY, StartX, StartY);
            double MarkerCentreX = StartX + (DirStartX * CIRCLE_MARKER_RADIUS_PX);
            double MarkerCentreY = StartY + (DirStartY * CIRCLE_MARKER_RADIUS_PX);
            AppendCircleMarker(Builder, MarkerCentreX, MarkerCentreY, EdgeStyle.Stroke);
        }
        else if (Edge.MarkerStart == EdgeEndpointMarker.Cross)
        {
            (double DirStartX, double DirStartY) = GetUnitDirection(SourceApproachX, SourceApproachY, StartX, StartY);
            double MarkerCentreX = StartX + (DirStartX * CROSS_MARKER_HALF_PX);
            double MarkerCentreY = StartY + (DirStartY * CROSS_MARKER_HALF_PX);
            AppendCrossMarker(Builder, MarkerCentreX, MarkerCentreY, StartX, StartY, EdgeStyle.Stroke, EdgeStyle.StrokeWidth);
        }
    }

    private static (double X, double Y) InsetArrowTipIntoNode(
        double BoundaryX,
        double BoundaryY,
        double InteriorDirX,
        double InteriorDirY) => (
            BoundaryX + (InteriorDirX * ARROW_TIP_NODE_OVERLAP_PX),
            BoundaryY + (InteriorDirY * ARROW_TIP_NODE_OVERLAP_PX));

    private static void AppendArrowhead(
        StringBuilder Builder,
        double TipX,
        double TipY,
        double FromX,
        double FromY,
        string Fill)
    {
        if (_ActiveRenderSession is not null)
        {
            double CapturedTipX = TipX;
            double CapturedTipY = TipY;
            double CapturedFromX = FromX;
            double CapturedFromY = FromY;
            string CapturedFill = Fill;
            _ActiveRenderSession.DeferMarkerDraw(
                DeferredBuilder => AppendArrowheadCore(
                    DeferredBuilder,
                    CapturedTipX,
                    CapturedTipY,
                    CapturedFromX,
                    CapturedFromY,
                    CapturedFill));
            return;
        }

        AppendArrowheadCore(Builder, TipX, TipY, FromX, FromY, Fill);
    }

    private static void AppendArrowheadCore(
        StringBuilder Builder,
        double TipX,
        double TipY,
        double FromX,
        double FromY,
        string Fill)
    {
        double DirX = TipX - FromX;
        double DirY = TipY - FromY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return;
        }

        DirX /= Length;
        DirY /= Length;
        double PerpX = -DirY;
        double PerpY = DirX;
        double BaseX = TipX - (DirX * ARROWHEAD_LENGTH_PX);
        double BaseY = TipY - (DirY * ARROWHEAD_LENGTH_PX);
        double HalfWidth = ARROWHEAD_WIDTH_PX / 2.0;

        Builder.Append("<polygon points=\"");
        Builder.Append(TipX.ToString(CultureInfo.InvariantCulture));
        Builder.Append(',');
        Builder.Append(TipY.ToString(CultureInfo.InvariantCulture));
        Builder.Append(' ');
        Builder.Append((BaseX + (PerpX * HalfWidth)).ToString(CultureInfo.InvariantCulture));
        Builder.Append(',');
        Builder.Append((BaseY + (PerpY * HalfWidth)).ToString(CultureInfo.InvariantCulture));
        Builder.Append(' ');
        Builder.Append((BaseX - (PerpX * HalfWidth)).ToString(CultureInfo.InvariantCulture));
        Builder.Append(',');
        Builder.Append((BaseY - (PerpY * HalfWidth)).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"");
        Builder.Append(EscapeXml(Fill));
        Builder.Append("\" stroke=\"none\"/>");
    }

    private static (double X, double Y) ShortenSegmentForArrowhead(
        double X1,
        double Y1,
        double X2,
        double Y2,
        double ShortenPx)
    {
        double DirX = X2 - X1;
        double DirY = Y2 - Y1;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length <= ShortenPx)
        {
            return (X2, Y2);
        }

        double Scale = (Length - ShortenPx) / Length;
        return (X1 + (DirX * Scale), Y1 + (DirY * Scale));
    }

    private static (double X1, double Y1, double X2, double Y2) ComputeClippedEdgeSegment(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape)
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

        return (SourceX, SourceY, TargetX, TargetY);
    }

    internal static void ExpandLayoutBoundsForEdgeLabels(
        DiagramContract Contract,
        FlowchartRenderOptions RenderOptions,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, string> RouteClassByEdgeKey,
        FlowchartDirection Direction,
        IReadOnlyDictionary<EdgeContract, FlowchartResolvedGuide> GuideByEdge,
        ref double MinLeft,
        ref double MinBottom,
        ref double MaxRight,
        ref double MaxTop)
    {
        const double EDGE_LABEL_MARGIN_PX = 4.0;
        const double MOVED_EDGE_LABEL_HORIZONTAL_MARGIN_PX = 10.0;

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.IsNullOrWhiteSpace(Edge.Label)
                || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                Edge.Label,
                RenderOptions,
                RenderOptions.EdgeLabelFontSizePx);
            string[] Lines = LabelDimensions.DisplayText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            double LineHeightPx = RenderOptions.EdgeLabelFontSizePx * FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR;
            double TotalTextHeightPx = Lines.Length == 1
                ? RenderOptions.EdgeLabelFontSizePx
                : Lines.Length * LineHeightPx;

            Dictionary<string, NodeContract> NodeById = Contract.Nodes
                .ToDictionary(Node => Node.Id, Node => Node);
            List<(double X, double Y)> LayoutGuidePoints =
            [
                (SourceBounds.CenterX, SourceBounds.CenterY),
                (TargetBounds.CenterX, TargetBounds.CenterY)
            ];
            if (GuideByEdge.TryGetValue(Edge, out FlowchartResolvedGuide? ResolvedGuide)
                && ResolvedGuide.Points.Count >= 2)
            {
                LayoutGuidePoints = ResolvedGuide.Points;
            }
            else if (FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> ResolvedGuidePoints,
                    out FlowchartEdgeRouteKind _))
            {
                LayoutGuidePoints = ResolvedGuidePoints;
            }

            NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode);
            string? SourceShape = SourceNode?.Shape;
            string? RouteClass = TryResolveRouteClassName(Edge, RouteClassByEdgeKey);
            (double LabelCentreX, double LabelCentreY) = ResolveDrawnEdgeLabelCentre(
                Edge,
                Contract,
                BoundsById,
                SourceBounds,
                TargetBounds,
                SourceShape,
                LayoutGuidePoints,
                Direction,
                RouteClass,
                RenderOptions);
            double HorizontalMarginPx = MOVED_EDGE_LABEL_HORIZONTAL_MARGIN_PX;
            double HalfLabelWidth = (LabelDimensions.Width / 2.0) + HorizontalMarginPx;
            double HalfLabelHeight = (TotalTextHeightPx / 2.0) + EDGE_LABEL_MARGIN_PX;

            double LabelLeft = LabelCentreX - HalfLabelWidth;
            double LabelRight = LabelCentreX + HalfLabelWidth;
            double LabelBottom = LabelCentreY - HalfLabelHeight;
            double LabelTop = LabelCentreY + HalfLabelHeight;

            if (LabelLeft < MinLeft)
            {
                MinLeft = LabelLeft;
            }

            if (LabelRight > MaxRight)
            {
                MaxRight = LabelRight;
            }

            if (LabelBottom < MinBottom)
            {
                MinBottom = LabelBottom;
            }

            if (LabelTop > MaxTop)
            {
                MaxTop = LabelTop;
            }
        }
    }

    /// <summary>
    /// Extends the canvas so a stroke that runs outside the node union stays at least 10 px from the viewBox.
    /// </summary>
    internal static void ExpandLayoutBoundsForExteriorStrokes(
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        double DiagramPaddingPx,
        ref double MinLeft,
        ref double MinBottom,
        ref double MaxRight,
        ref double MaxTop)
    {
        const double CANVAS_MARGIN_PX = 10.0;
        double NeedLeft = 0.0;
        double NeedRight = 0.0;
        double NeedTop = 0.0;
        double NeedBottom = 0.0;
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            foreach ((double LayoutX, double LayoutY) in Guide.Points)
            {
                bool Outside = LayoutX < MinLeft - 0.5
                    || LayoutX > MaxRight + 0.5
                    || LayoutY < MinBottom - 0.5
                    || LayoutY > MaxTop + 0.5;
                if (!Outside)
                {
                    continue;
                }

                double SvgX = DiagramPaddingPx + (LayoutX - MinLeft);
                if (SvgX < CANVAS_MARGIN_PX)
                {
                    NeedLeft = Math.Max(NeedLeft, CANVAS_MARGIN_PX - SvgX);
                }

                double RightGap = MaxRight + DiagramPaddingPx - LayoutX;
                if (RightGap < CANVAS_MARGIN_PX)
                {
                    NeedRight = Math.Max(NeedRight, CANVAS_MARGIN_PX - RightGap);
                }

                double SvgY = DiagramPaddingPx + (MaxTop - LayoutY);
                if (SvgY < CANVAS_MARGIN_PX)
                {
                    NeedTop = Math.Max(NeedTop, CANVAS_MARGIN_PX - SvgY);
                }

                double BottomGap = LayoutY - MinBottom + DiagramPaddingPx;
                if (BottomGap < CANVAS_MARGIN_PX)
                {
                    NeedBottom = Math.Max(NeedBottom, CANVAS_MARGIN_PX - BottomGap);
                }
            }
        }

        MinLeft -= NeedLeft;
        MaxRight += NeedRight;
        MaxTop += NeedTop;
        MinBottom -= NeedBottom;
    }

    private static void RenderDagreBasisEdge(
        StringBuilder Builder,
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        FlowchartLayoutEdgeRoute Route,
        FlowchartDirection Direction,
        string? RouteClass,
        double MinLeft,
        double MaxTop,
        double DiagramPaddingPx,
        FlowchartResolvedStyle EdgeStyle,
        FlowchartRenderOptions RenderOptions)
    {
        List<(double X, double Y)> LayoutPoints = new List<(double X, double Y)>();
        foreach (FlowchartLayoutPoint Point in Route.Points)
        {
            LayoutPoints.Add((Point.X, Point.Y));
        }

        if (LayoutPoints.Count == 2
            && FlowchartCurvePathBuilder.TryCollapseAxisAlignedEndpoints(LayoutPoints, out List<(double X, double Y)> StraightSegment))
        {
            (double StraightStartX, double StraightStartY) = LayoutToSvgPoint(
                StraightSegment[0].X,
                StraightSegment[0].Y,
                MinLeft,
                MaxTop,
                DiagramPaddingPx);
            (double StraightEndX, double StraightEndY) = LayoutToSvgPoint(
                StraightSegment[1].X,
                StraightSegment[1].Y,
                MinLeft,
                MaxTop,
                DiagramPaddingPx);
            RenderStraightEdgeSegment(
                Builder,
                Edge,
                StraightStartX,
                StraightStartY,
                StraightEndX,
                StraightEndY,
                EdgeStyle,
                RenderOptions);
            AppendResolvedEdgeLabel(
                Builder,
                Edge,
                Contract,
                BoundsById,
                SourceBounds,
                TargetBounds,
                SourceShape,
                StraightSegment,
                Direction,
                RouteClass,
                MinLeft,
                MaxTop,
                DiagramPaddingPx,
                EdgeStyle,
                RenderOptions);
            return;
        }

        List<(double X, double Y)> SvgPoints = new List<(double X, double Y)>();
        foreach ((double LayoutX, double LayoutY) in LayoutPoints)
        {
            SvgPoints.Add(LayoutToSvgPoint(LayoutX, LayoutY, MinLeft, MaxTop, DiagramPaddingPx));
        }

        string PathData = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(
            SvgPoints,
            DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        AppendStyledEdgePath(Builder, PathData, EdgeStyle, Edge.SourceId, Edge.TargetId);
        if (SvgPoints.Count >= 2)
        {
            AppendCurvedEdgeMarkers(
                Builder,
                Edge,
                PathData,
                SvgPoints,
                FlowchartRenderedPathKind.Corridor,
                EdgeStyle);
        }

        AppendResolvedEdgeLabel(
            Builder,
            Edge,
            Contract,
            BoundsById,
            SourceBounds,
            TargetBounds,
            SourceShape,
            LayoutPoints,
            Direction,
            RouteClass,
            MinLeft,
            MaxTop,
            DiagramPaddingPx,
            EdgeStyle,
            RenderOptions);
    }

    private static Dictionary<string, Queue<FlowchartLayoutEdgeRoute>> IndexDagreRoutes(FlowchartLayoutResult Layout)
    {
        Dictionary<string, Queue<FlowchartLayoutEdgeRoute>> Routes =
            new Dictionary<string, Queue<FlowchartLayoutEdgeRoute>>(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (string.IsNullOrEmpty(Route.EdgeId))
            {
                continue;
            }

            if (!Routes.TryGetValue(Route.EdgeId, out Queue<FlowchartLayoutEdgeRoute>? Queue))
            {
                Queue = new Queue<FlowchartLayoutEdgeRoute>();
                Routes[Route.EdgeId] = Queue;
            }

            Queue.Enqueue(Route);
        }

        return Routes;
    }

    private static bool TryTakeDagreRoute(
        Dictionary<string, Queue<FlowchartLayoutEdgeRoute>> Routes,
        string EdgeId,
        out FlowchartLayoutEdgeRoute? Route)
    {
        Route = null;
        if (!Routes.TryGetValue(EdgeId, out Queue<FlowchartLayoutEdgeRoute>? Queue) || Queue.Count == 0)
        {
            return false;
        }

        Route = Queue.Dequeue();
        return true;
    }

    internal static List<FlowchartSubgraphLayoutRect> BuildDagreClusterRects(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        Dictionary<string, string?> Titles = new Dictionary<string, string?>(StringComparer.Ordinal);
        Dictionary<string, int> Depth = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            Titles[Group.Id] = Group.Title;
            int Level = 0;
            string? Parent = Group.ParentGroupId;
            while (!string.IsNullOrEmpty(Parent))
            {
                Level++;
                string? Next = null;
                foreach (GroupContract Candidate in Contract.Groups)
                {
                    if (Candidate.Id == Parent)
                    {
                        Next = Candidate.ParentGroupId;
                        break;
                    }
                }

                Parent = Next;
            }

            Depth[Group.Id] = Level;
        }

        List<FlowchartSubgraphLayoutRect> Rects = new List<FlowchartSubgraphLayoutRect>();
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.ClusterBounds)
        {
            Rects.Add(new FlowchartSubgraphLayoutRect
            {
                GroupId = Bounds.NodeId,
                Left = Bounds.CenterX - (Bounds.Width / 2.0),
                Right = Bounds.CenterX + (Bounds.Width / 2.0),
                Bottom = Bounds.CenterY - (Bounds.Height / 2.0),
                Top = Bounds.CenterY + (Bounds.Height / 2.0),
                Title = Titles.TryGetValue(Bounds.NodeId, out string? Title) ? Title : Bounds.NodeId
            });
        }

        Rects.Sort((Left, Right) =>
        {
            int LeftDepth = Depth.TryGetValue(Left.GroupId, out int LeftLevel) ? LeftLevel : 0;
            int RightDepth = Depth.TryGetValue(Right.GroupId, out int RightLevel) ? RightLevel : 0;
            return RightDepth.CompareTo(LeftDepth);
        });
        return Rects;
    }

    internal static void ExpandLayoutBoundsForDagreRoutes(
        FlowchartLayoutResult Layout,
        ref double MinLeft,
        ref double MinBottom,
        ref double MaxRight,
        ref double MaxTop)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                if (Point.X < MinLeft)
                {
                    MinLeft = Point.X;
                }

                if (Point.X > MaxRight)
                {
                    MaxRight = Point.X;
                }

                if (Point.Y < MinBottom)
                {
                    MinBottom = Point.Y;
                }

                if (Point.Y > MaxTop)
                {
                    MaxTop = Point.Y;
                }
            }
        }
    }

    private static (double X, double Y) LayoutToSvgPoint(
        double LayoutX,
        double LayoutY,
        double MinLeft,
        double MaxTop,
        double DiagramPaddingPx)
    {
        double X = DiagramPaddingPx + (LayoutX - MinLeft);
        double Y = DiagramPaddingPx + (MaxTop - LayoutY);
        return (X, Y);
    }

    private static void AppendNodeLabel(
        StringBuilder Builder,
        string Label,
        double CentreX,
        double CentreY,
        FlowchartResolvedStyle NodeStyle,
        FlowchartRenderOptions Options,
        string? RichMarkupSourceLabel = null)
    {
        string RichProbe = RichMarkupSourceLabel ?? Label;
        if (Options.HtmlLabels && FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup(RichProbe))
        {
            FlowchartRichLabelSvgWriter.AppendCentredLabel(
                Builder,
                RichProbe,
                CentreX,
                CentreY,
                NodeStyle.FontSizePx,
                NodeStyle.TextColor,
                NodeStyle.FontFamily,
                NodeStyle.FontWeight,
                FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR);
            return;
        }

        string[] Lines = Label.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (Lines.Length == 1)
        {
            double BaselineY = FlowchartRichLabelSvgWriter.ComputeFirstBaselineY(CentreY, 1, NodeStyle.FontSizePx, FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR);
            Builder.Append("<text x=\"");
            Builder.Append(CentreX.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" y=\"");
            Builder.Append(BaselineY.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" text-anchor=\"middle\" fill=\"");
            Builder.Append(EscapeXml(NodeStyle.TextColor));
            Builder.Append("\" font-family=\"");
            Builder.Append(EscapeXml(NodeStyle.FontFamily));
            Builder.Append("\" font-size=\"");
            Builder.Append(NodeStyle.FontSizePx.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" font-weight=\"");
            Builder.Append(EscapeXml(NodeStyle.FontWeight));
            Builder.Append("\">");
            Builder.Append(EscapeXml(Lines[0]));
            Builder.Append("</text>");
            return;
        }

        double LineHeightPx = NodeStyle.FontSizePx * FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR;
        double FirstLineY = FlowchartRichLabelSvgWriter.ComputeFirstBaselineY(CentreY, Lines.Length, NodeStyle.FontSizePx, FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR);

        Builder.Append("<text y=\"");
        Builder.Append(FirstLineY.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" text-anchor=\"middle\" fill=\"");
        Builder.Append(EscapeXml(NodeStyle.TextColor));
        Builder.Append("\" font-family=\"");
        Builder.Append(EscapeXml(NodeStyle.FontFamily));
        Builder.Append("\" font-size=\"");
        Builder.Append(NodeStyle.FontSizePx.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" font-weight=\"");
        Builder.Append(EscapeXml(NodeStyle.FontWeight));
        Builder.Append("\">");

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            if (LineIndex == 0)
            {
                Builder.Append("<tspan x=\"");
                Builder.Append(CentreX.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\" dy=\"0\">");
            }
            else
            {
                Builder.Append("<tspan x=\"");
                Builder.Append(CentreX.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\" dy=\"");
                Builder.Append(LineHeightPx.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\">");
            }

            Builder.Append(EscapeXml(Lines[LineIndex]));
            Builder.Append("</tspan>");
        }

        Builder.Append("</text>");
    }

    private static void AppendBackgroundRect(StringBuilder Builder, double Width, double Height, string Fill)
    {
        Builder.Append("<rect x=\"0\" y=\"0\" width=\"");
        Builder.Append(Width.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" height=\"");
        Builder.Append(Height.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"");
        Builder.Append(EscapeXml(Fill));
        Builder.Append("\"/>");
    }

    private static bool IsFlowchartContract(DiagramContract Contract)
    {
        if (Contract == null)
        {
            return false;
        }

        if (Contract.RemarkStatus == MermaidDiagramRemarkStatus.CatalogueRemarked)
        {
            return false;
        }

        string Kind = Contract.Kind ?? string.Empty;
        return Kind.Equals("flowchart", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("graph", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryResolveRouteClassName(
        EdgeContract Edge,
        IReadOnlyDictionary<string, string> RouteClassByEdgeKey)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Id)
            && RouteClassByEdgeKey.TryGetValue(Edge.Id, out string? RouteClassById))
        {
            return RouteClassById;
        }

        string SourceTargetKey = Edge.SourceId + "->" + Edge.TargetId;
        if (RouteClassByEdgeKey.TryGetValue(SourceTargetKey, out string? RouteClassByEndpoints))
        {
            return RouteClassByEndpoints;
        }

        return null;
    }

    private static string EscapeXml(string Value) => Value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
