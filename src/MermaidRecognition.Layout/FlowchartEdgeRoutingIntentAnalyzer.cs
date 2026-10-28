using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds renderer-independent routing-intent JSON from a laid-out flowchart diagram.
/// Classifies edges from graph topology and layout geometry without using edge label text.
/// </summary>
public static class FlowchartEdgeRoutingIntentAnalyzer
{
    private const double BOUNDARY_REGION_TOLERANCE_RATIO = 0.22;

    private const double ADJACENT_RANK_MAX_FLOW_AXIS_DELTA_RATIO = 1.35;

    private const double CROSS_LANE_MIN_CROSS_AXIS_DELTA_PX = 48.0;

    private const int MULTI_INPUT_MERGE_THRESHOLD = 2;

    private const double SPINE_ALIGN_TOLERANCE_PX = 30.0;

    /// <summary>
    /// Analyses all visible edges and returns routing intent for automated validation.
    /// </summary>
    public static FlowchartRoutingIntentDiagramResult Analyze(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction,
        string DiagramId)
    {
        FlowchartRoutingIntentDiagramResult Result = new FlowchartRoutingIntentDiagramResult
        {
            DiagramId = DiagramId,
            Direction = FormatDirection(Direction)
        };

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            RenderOptions,
            out FlowchartSpineLayoutContext? SpineContext);

        int EdgeSequence = 0;
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

            EdgeSequence++;
            FlowchartEdgeRoute StructuralRoute = FlowchartRouter.ClassifyEdge(Edge, Contract);
            FlowchartEdgeSemanticRouteClass SemanticRouteClass = FlowchartEdgeRouteClassifier.Classify(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                SpineContext);

            bool RouteResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out FlowchartEdgeRouteKind ResolvedRouteKind);

            double? SourcePortX = null;
            double? SourcePortY = null;
            double? TargetPortX = null;
            double? TargetPortY = null;
            if (RouteResolved && LayoutPoints.Count >= 2)
            {
                SourcePortX = LayoutPoints[0].X;
                SourcePortY = LayoutPoints[0].Y;
                TargetPortX = LayoutPoints[LayoutPoints.Count - 1].X;
                TargetPortY = LayoutPoints[LayoutPoints.Count - 1].Y;
            }

            FlowchartRoutingIntentRouteClass RouteClass = InferRouteClass(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                SpineContext,
                StructuralRoute,
                SemanticRouteClass,
                SourceBounds,
                TargetBounds);

            FlowchartBoundaryRegionIntent SourceBoundaryIntent = InferBoundaryRegion(
                SourceBounds,
                SourcePortX,
                SourcePortY,
                Direction,
                IsOutbound: true);
            FlowchartBoundaryRegionIntent TargetBoundaryIntent = InferBoundaryRegion(
                TargetBounds,
                TargetPortX,
                TargetPortY,
                Direction,
                IsOutbound: false);

            FlowchartPathShapeIntent PathShapeIntent = InferPathShapeIntent(
                RouteClass,
                ResolvedRouteKind,
                Direction);
            FlowchartLabelPlacementIntent LabelPlacementIntent = InferLabelPlacementIntent(
                Edge,
                RouteClass,
                ResolvedRouteKind);
            FlowchartObstacleAvoidanceIntent ObstacleAvoidanceIntent = InferObstacleAvoidanceIntent(
                RouteClass,
                StructuralRoute,
                SemanticRouteClass);

            List<string> ExpectedBehaviour = BuildExpectedBehaviour(
                RouteClass,
                StructuralRoute,
                SemanticRouteClass,
                Edge,
                Contract,
                SourceBounds,
                TargetBounds,
                SpineContext);
            List<string> InvalidBehaviours = BuildInvalidBehaviours(
                RouteClass,
                StructuralRoute,
                Edge);

            FlowchartEdgeRoutingIntentRecord Record = new FlowchartEdgeRoutingIntentRecord
            {
                EdgeId = "E" + EdgeSequence.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                SourceNodeId = Edge.SourceId,
                TargetNodeId = Edge.TargetId,
                RouteClass = RouteClass,
                SemanticRouteClass = SemanticRouteClass,
                StructuralRouteKind = StructuralRoute.Kind,
                ResolvedRouteKind = ResolvedRouteKind,
                SourceBoundaryIntent = SourceBoundaryIntent,
                TargetBoundaryIntent = TargetBoundaryIntent,
                PathShapeIntent = PathShapeIntent,
                LabelPlacementIntent = LabelPlacementIntent,
                ObstacleAvoidanceIntent = ObstacleAvoidanceIntent,
                ExpectedBehaviour = ExpectedBehaviour,
                InvalidBehaviours = InvalidBehaviours,
                SourcePortX = SourcePortX,
                SourcePortY = SourcePortY,
                TargetPortX = TargetPortX,
                TargetPortY = TargetPortY
            };

            Result.Edges.Add(Record);
        }

        return Result;
    }

    private static FlowchartRoutingIntentRouteClass InferRouteClass(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext? SpineContext,
        FlowchartEdgeRoute StructuralRoute,
        FlowchartEdgeSemanticRouteClass SemanticRouteClass,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        if (StructuralRoute.Kind == FlowchartRouteKind.BackEdgeLoop)
        {
            return FlowchartRoutingIntentRouteClass.ReturnBackward;
        }

        if (StructuralRoute.Kind == FlowchartRouteKind.CrossGroupBoundary)
        {
            return FlowchartRoutingIntentRouteClass.CrossLane;
        }

        switch (SemanticRouteClass)
        {
            case FlowchartEdgeSemanticRouteClass.CentreSpine:
                return FlowchartRoutingIntentRouteClass.CentreSpine;
            case FlowchartEdgeSemanticRouteClass.LocalDecisionBranch:
                return FlowchartRoutingIntentRouteClass.LocalDecisionBranch;
            case FlowchartEdgeSemanticRouteClass.MergeBack:
            {
                int TargetIncomingCount = CountVisibleIncoming(Contract, Edge.TargetId);
                if (TargetIncomingCount >= MULTI_INPUT_MERGE_THRESHOLD
                    || StructuralRoute.Kind == FlowchartRouteKind.DecisionFanIn)
                {
                    return FlowchartRoutingIntentRouteClass.MultiInputMerge;
                }

                return FlowchartRoutingIntentRouteClass.MergeBack;
            }
            case FlowchartEdgeSemanticRouteClass.SkipBypass:
                return FlowchartRoutingIntentRouteClass.SkipBypass;
            case FlowchartEdgeSemanticRouteClass.LongExceptionRoute:
                return FlowchartRoutingIntentRouteClass.LongExceptionBypass;
            default:
                break;
        }

        if (StructuralRoute.Kind == FlowchartRouteKind.DecisionFanOut)
        {
            return FlowchartRoutingIntentRouteClass.LocalDecisionBranch;
        }

        int IncomingCount = CountVisibleIncoming(Contract, Edge.TargetId);
        if (IncomingCount >= MULTI_INPUT_MERGE_THRESHOLD
            && StructuralRoute.Kind == FlowchartRouteKind.DecisionFanIn)
        {
            return FlowchartRoutingIntentRouteClass.MultiInputMerge;
        }

        if (IsShortAdjacentEdge(SourceBounds, TargetBounds, Direction)
            && StructuralRoute.Kind == FlowchartRouteKind.SimpleLink)
        {
            return FlowchartRoutingIntentRouteClass.ShortAdjacent;
        }

        if (SpineContext is not null
            && !FlowchartSpineGraphHelper.IsOnSpineColumn(SourceBounds, SpineContext)
            && !FlowchartSpineGraphHelper.IsOnSpineColumn(TargetBounds, SpineContext)
            && Math.Abs(SourceBounds.CenterX - TargetBounds.CenterX) >= CROSS_LANE_MIN_CROSS_AXIS_DELTA_PX)
        {
            return FlowchartRoutingIntentRouteClass.CrossLane;
        }

        if (ResolvedRouteNeedsObstacleAvoidance(SemanticRouteClass, StructuralRoute))
        {
            return FlowchartRoutingIntentRouteClass.ObstacleAvoidance;
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        if (StructuralRoute.Kind == FlowchartRouteKind.SimpleLink)
        {
            if (VerticalFlow
                && FlowchartShapeBoundaryAttachHelper.AreSpineColumnAligned(SourceBounds, TargetBounds)
                && FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds))
            {
                return FlowchartRoutingIntentRouteClass.CentreSpine;
            }

            if (HorizontalFlow
                && Math.Abs(SourceBounds.CenterY - TargetBounds.CenterY) <= SPINE_ALIGN_TOLERANCE_PX)
            {
                bool LeftToRight = Direction == FlowchartDirection.LeftToRight;
                bool SourceBeforeTarget = LeftToRight
                    ? SourceBounds.CenterX < TargetBounds.CenterX
                    : SourceBounds.CenterX > TargetBounds.CenterX;
                if (SourceBeforeTarget)
                {
                    return FlowchartRoutingIntentRouteClass.CentreSpine;
                }
            }
        }

        if (StructuralRoute.Kind == FlowchartRouteKind.DecisionFanOut)
        {
            return FlowchartRoutingIntentRouteClass.LocalDecisionBranch;
        }

        int VisibleIncoming = CountVisibleIncoming(Contract, Edge.TargetId);
        if (VisibleIncoming >= MULTI_INPUT_MERGE_THRESHOLD
            && StructuralRoute.Kind == FlowchartRouteKind.DecisionFanIn)
        {
            return FlowchartRoutingIntentRouteClass.MultiInputMerge;
        }

        return FlowchartRoutingIntentRouteClass.Unknown;
    }

    private static bool ResolvedRouteNeedsObstacleAvoidance(
        FlowchartEdgeSemanticRouteClass SemanticRouteClass,
        FlowchartEdgeRoute StructuralRoute)
    {
        if (SemanticRouteClass == FlowchartEdgeSemanticRouteClass.LongExceptionRoute
            || SemanticRouteClass == FlowchartEdgeSemanticRouteClass.SkipBypass)
        {
            return true;
        }

        if (StructuralRoute.Kind == FlowchartRouteKind.DecisionFanOut)
        {
            return true;
        }

        return false;
    }

    private static FlowchartBoundaryRegionIntent InferBoundaryRegion(
        FlowchartLayoutNodeBounds Bounds,
        double? PortX,
        double? PortY,
        FlowchartDirection Direction,
        bool IsOutbound)
    {
        if (!PortX.HasValue || !PortY.HasValue)
        {
            return InferDefaultBoundaryRegion(Direction, IsOutbound);
        }

        double HalfWidth = Bounds.Width / 2.0;
        double HalfHeight = Bounds.Height / 2.0;
        double RelativeX = PortX.Value - Bounds.CenterX;
        double RelativeY = PortY.Value - Bounds.CenterY;
        double WidthTolerance = Math.Max(4.0, HalfWidth * BOUNDARY_REGION_TOLERANCE_RATIO);
        double HeightTolerance = Math.Max(4.0, HalfHeight * BOUNDARY_REGION_TOLERANCE_RATIO);

        bool NearTop = RelativeY >= (HalfHeight - HeightTolerance);
        bool NearBottom = RelativeY <= -(HalfHeight - HeightTolerance);
        bool NearLeft = RelativeX <= -(HalfWidth - WidthTolerance);
        bool NearRight = RelativeX >= (HalfWidth - WidthTolerance);
        bool NearCentreX = Math.Abs(RelativeX) <= WidthTolerance;
        bool NearCentreY = Math.Abs(RelativeY) <= HeightTolerance;

        if (NearTop && NearCentreX)
        {
            return FlowchartBoundaryRegionIntent.TopCentre;
        }

        if (NearBottom && NearCentreX)
        {
            return FlowchartBoundaryRegionIntent.BottomCentre;
        }

        if (NearLeft && NearCentreY)
        {
            return FlowchartBoundaryRegionIntent.LeftCentre;
        }

        if (NearRight && NearCentreY)
        {
            return FlowchartBoundaryRegionIntent.RightCentre;
        }

        if (NearTop && NearLeft)
        {
            return FlowchartBoundaryRegionIntent.TopLeft;
        }

        if (NearTop && NearRight)
        {
            return FlowchartBoundaryRegionIntent.TopRight;
        }

        if (NearBottom && NearLeft)
        {
            return FlowchartBoundaryRegionIntent.BottomLeft;
        }

        if (NearBottom && NearRight)
        {
            return FlowchartBoundaryRegionIntent.BottomRight;
        }

        if (NearRight || NearBottom)
        {
            return FlowchartBoundaryRegionIntent.RightOrBottomRight;
        }

        if (NearTop || NearLeft)
        {
            return FlowchartBoundaryRegionIntent.TopOrLeft;
        }

        return InferDefaultBoundaryRegion(Direction, IsOutbound);
    }

    private static FlowchartBoundaryRegionIntent InferDefaultBoundaryRegion(
        FlowchartDirection Direction,
        bool IsOutbound)
    {
        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        if (VerticalFlow)
        {
            if (IsOutbound)
            {
                return FlowchartBoundaryRegionIntent.BottomCentre;
            }

            return FlowchartBoundaryRegionIntent.TopCentre;
        }

        if (IsOutbound)
        {
            return FlowchartBoundaryRegionIntent.RightCentre;
        }

        return FlowchartBoundaryRegionIntent.LeftCentre;
    }

    private static FlowchartPathShapeIntent InferPathShapeIntent(
        FlowchartRoutingIntentRouteClass RouteClass,
        FlowchartEdgeRouteKind ResolvedRouteKind,
        FlowchartDirection Direction)
    {
        if (RouteClass == FlowchartRoutingIntentRouteClass.ReturnBackward)
        {
            return FlowchartPathShapeIntent.ReturnLoopCurve;
        }

        if (ResolvedRouteKind == FlowchartEdgeRouteKind.Corridor)
        {
            if (RouteClass == FlowchartRoutingIntentRouteClass.LongExceptionBypass
                || RouteClass == FlowchartRoutingIntentRouteClass.SkipBypass)
            {
                return FlowchartPathShapeIntent.LongBypassCorridor;
            }

            return FlowchartPathShapeIntent.OrthogonalCorridor;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.CentreSpine
            || RouteClass == FlowchartRoutingIntentRouteClass.ShortAdjacent)
        {
            bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
                || Direction == FlowchartDirection.BottomToTop;
            if (VerticalFlow)
            {
                return FlowchartPathShapeIntent.StraightOrSoftVertical;
            }

            return FlowchartPathShapeIntent.StraightOrSoftHorizontal;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.LocalDecisionBranch)
        {
            return FlowchartPathShapeIntent.ShortSmoothBranch;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.MergeBack
            || RouteClass == FlowchartRoutingIntentRouteClass.MultiInputMerge)
        {
            return FlowchartPathShapeIntent.SmoothMergeBack;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.LongExceptionBypass
            || RouteClass == FlowchartRoutingIntentRouteClass.SkipBypass
            || RouteClass == FlowchartRoutingIntentRouteClass.ObstacleAvoidance)
        {
            return FlowchartPathShapeIntent.LongBypassCorridor;
        }

        if (ResolvedRouteKind == FlowchartEdgeRouteKind.Straight)
        {
            bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
                || Direction == FlowchartDirection.BottomToTop;
            if (VerticalFlow)
            {
                return FlowchartPathShapeIntent.StraightOrSoftVertical;
            }

            return FlowchartPathShapeIntent.StraightOrSoftHorizontal;
        }

        return FlowchartPathShapeIntent.ShortSmoothBranch;
    }

    private static FlowchartLabelPlacementIntent InferLabelPlacementIntent(
        EdgeContract Edge,
        FlowchartRoutingIntentRouteClass RouteClass,
        FlowchartEdgeRouteKind ResolvedRouteKind)
    {
        if (string.IsNullOrWhiteSpace(Edge.Label))
        {
            return FlowchartLabelPlacementIntent.None;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.LongExceptionBypass
            || RouteClass == FlowchartRoutingIntentRouteClass.SkipBypass
            || ResolvedRouteKind == FlowchartEdgeRouteKind.Corridor)
        {
            return FlowchartLabelPlacementIntent.NearCorridorSegment;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.LocalDecisionBranch
            || ResolvedRouteKind == FlowchartEdgeRouteKind.Decision)
        {
            return FlowchartLabelPlacementIntent.NearSourceBranchSegment;
        }

        return FlowchartLabelPlacementIntent.OffsetAlongEdgeMidpoint;
    }

    private static FlowchartObstacleAvoidanceIntent InferObstacleAvoidanceIntent(
        FlowchartRoutingIntentRouteClass RouteClass,
        FlowchartEdgeRoute StructuralRoute,
        FlowchartEdgeSemanticRouteClass SemanticRouteClass)
    {
        if (RouteClass == FlowchartRoutingIntentRouteClass.CentreSpine
            || RouteClass == FlowchartRoutingIntentRouteClass.ShortAdjacent)
        {
            return FlowchartObstacleAvoidanceIntent.NotRequired;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.LongExceptionBypass
            || SemanticRouteClass == FlowchartEdgeSemanticRouteClass.LongExceptionRoute)
        {
            return FlowchartObstacleAvoidanceIntent.AvoidSpineCorridor;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.SkipBypass)
        {
            return FlowchartObstacleAvoidanceIntent.AvoidNodeBodiesAndDecisionDiamonds;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.LocalDecisionBranch
            || StructuralRoute.Kind == FlowchartRouteKind.DecisionFanOut)
        {
            return FlowchartObstacleAvoidanceIntent.AvoidDecisionNodeAndSiblingBranch;
        }

        if (RouteClass == FlowchartRoutingIntentRouteClass.MergeBack
            || RouteClass == FlowchartRoutingIntentRouteClass.MultiInputMerge
            || RouteClass == FlowchartRoutingIntentRouteClass.ObstacleAvoidance)
        {
            return FlowchartObstacleAvoidanceIntent.AvoidNodeBodiesAndDecisionDiamonds;
        }

        return FlowchartObstacleAvoidanceIntent.AvoidUnrelatedNodes;
    }

    private static List<string> BuildExpectedBehaviour(
        FlowchartRoutingIntentRouteClass RouteClass,
        FlowchartEdgeRoute StructuralRoute,
        FlowchartEdgeSemanticRouteClass SemanticRouteClass,
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartSpineLayoutContext? SpineContext)
    {
        List<string> ExpectedBehaviour = [];

        switch (RouteClass)
        {
            case FlowchartRoutingIntentRouteClass.CentreSpine:
                ExpectedBehaviour.Add("RemainOnMainSpine");
                ExpectedBehaviour.Add("UseDirectVerticalConnection");
                ExpectedBehaviour.Add("DoNotRouteAroundUnrelatedNodes");
                break;
            case FlowchartRoutingIntentRouteClass.LocalDecisionBranch:
                ExpectedBehaviour.Add("LeaveDecisionClearly");
                ExpectedBehaviour.Add("KeepLabelAttachedToBranch");
                ExpectedBehaviour.Add("DoNotCrossSiblingBranch");
                ExpectedBehaviour.Add("DoNotPassThroughNodeBody");
                break;
            case FlowchartRoutingIntentRouteClass.MergeBack:
                ExpectedBehaviour.Add("RejoinMainPathWithoutHook");
                ExpectedBehaviour.Add("AvoidCrossingSpineNodes");
                ExpectedBehaviour.Add("UseDistinctMergePort");
                break;
            case FlowchartRoutingIntentRouteClass.MultiInputMerge:
                ExpectedBehaviour.Add("UseDistinctMergePort");
                ExpectedBehaviour.Add("AvoidAmbiguousMergeCollapse");
                ExpectedBehaviour.Add("RejoinMainPathWithoutHook");
                break;
            case FlowchartRoutingIntentRouteClass.SkipBypass:
                ExpectedBehaviour.Add("BypassInterveningSpineNodesLocally");
                ExpectedBehaviour.Add("DoNotTreatAsArbitraryLongDetour");
                ExpectedBehaviour.Add("AvoidSpineCorridorInterior");
                break;
            case FlowchartRoutingIntentRouteClass.LongExceptionBypass:
                ExpectedBehaviour.Add("RouteInExteriorCorridorLane");
                ExpectedBehaviour.Add("AvoidSpineCorridorInterior");
                ExpectedBehaviour.Add("DoNotUseDiagonalChordThroughSpine");
                break;
            case FlowchartRoutingIntentRouteClass.ShortAdjacent:
                ExpectedBehaviour.Add("UseDirectLocalConnection");
                ExpectedBehaviour.Add("AvoidUnnecessaryDetour");
                break;
            case FlowchartRoutingIntentRouteClass.CrossLane:
                ExpectedBehaviour.Add("CrossLaneWithoutNodePassThrough");
                ExpectedBehaviour.Add("UseSmoothOrOrthogonalLaneTransition");
                break;
            case FlowchartRoutingIntentRouteClass.ReturnBackward:
                ExpectedBehaviour.Add("RouteReturnLoopWithoutNodePassThrough");
                ExpectedBehaviour.Add("AvoidHookAtSource");
                break;
            case FlowchartRoutingIntentRouteClass.ObstacleAvoidance:
                ExpectedBehaviour.Add("RouteAroundObstacles");
                ExpectedBehaviour.Add("DoNotPassThroughNodeBody");
                break;
            default:
                ExpectedBehaviour.Add("AttachAtShapeBoundary");
                break;
        }

        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            ExpectedBehaviour.Add("KeepLabelAttachedToEdge");
        }

        if (StructuralRoute.Kind == FlowchartRouteKind.DecisionFanOut)
        {
            ExpectedBehaviour.Add("TreatAsSeparateLocalBranch");
        }

        if (SpineContext is not null
            && FlowchartSpineGraphHelper.IsOnSpineColumn(SourceBounds, SpineContext)
            && FlowchartSpineGraphHelper.IsOnSpineColumn(TargetBounds, SpineContext))
        {
            ExpectedBehaviour.Add("RespectSpineColumnAlignment");
        }

        if (SemanticRouteClass == FlowchartEdgeSemanticRouteClass.MergeBack)
        {
            ExpectedBehaviour.Add("MergeBackIntoMainPath");
        }

        return [.. ExpectedBehaviour.Distinct(StringComparer.Ordinal)];
    }

    private static List<string> BuildInvalidBehaviours(
        FlowchartRoutingIntentRouteClass RouteClass,
        FlowchartEdgeRoute StructuralRoute,
        EdgeContract Edge)
    {
        List<string> InvalidBehaviours =
        [
            "NodeBodyIntersection",
            "CrossesUnrelatedNode",
            "LabelDetachedFromEdge",
            "LabelCloserToWrongEdge"
        ];

        switch (RouteClass)
        {
            case FlowchartRoutingIntentRouteClass.CentreSpine:
            case FlowchartRoutingIntentRouteClass.ShortAdjacent:
                InvalidBehaviours.Add("HardElbow");
                InvalidBehaviours.Add("HookAtSource");
                InvalidBehaviours.Add("HookAtTarget");
                InvalidBehaviours.Add("UnnecessaryDetour");
                InvalidBehaviours.Add("LongBypassMisclassification");
                break;
            case FlowchartRoutingIntentRouteClass.LocalDecisionBranch:
                InvalidBehaviours.Add("HardElbow");
                InvalidBehaviours.Add("HookShape");
                InvalidBehaviours.Add("AmbiguousMerge");
                InvalidBehaviours.Add("LongBypassMisclassification");
                InvalidBehaviours.Add("CrossesSiblingBranch");
                break;
            case FlowchartRoutingIntentRouteClass.MergeBack:
            case FlowchartRoutingIntentRouteClass.MultiInputMerge:
                InvalidBehaviours.Add("HookAtTarget");
                InvalidBehaviours.Add("AmbiguousMerge");
                InvalidBehaviours.Add("HardElbow");
                InvalidBehaviours.Add("SpineCorridorCutThrough");
                break;
            case FlowchartRoutingIntentRouteClass.SkipBypass:
                InvalidBehaviours.Add("RouteThroughCentreSpine");
                InvalidBehaviours.Add("ArbitraryLongDetour");
                InvalidBehaviours.Add("HardElbow");
                InvalidBehaviours.Add("LongBypassMisclassification");
                break;
            case FlowchartRoutingIntentRouteClass.LongExceptionBypass:
                InvalidBehaviours.Add("DiagonalSpineCrossing");
                InvalidBehaviours.Add("RouteThroughCentreSpine");
                InvalidBehaviours.Add("HardElbow");
                InvalidBehaviours.Add("LocalBranchMisclassification");
                break;
            case FlowchartRoutingIntentRouteClass.ReturnBackward:
                InvalidBehaviours.Add("HookAtSource");
                InvalidBehaviours.Add("NodeBodyIntersection");
                break;
            case FlowchartRoutingIntentRouteClass.CrossLane:
            case FlowchartRoutingIntentRouteClass.ObstacleAvoidance:
                InvalidBehaviours.Add("HardElbow");
                InvalidBehaviours.Add("NodeBodyIntersection");
                InvalidBehaviours.Add("CrossesDecisionDiamond");
                break;
            default:
                InvalidBehaviours.Add("HardElbow");
                InvalidBehaviours.Add("HookShape");
                break;
        }

        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            InvalidBehaviours.Add("LabelDetachedFromEdge");
            InvalidBehaviours.Add("LabelCloserToWrongEdge");
        }

        if (StructuralRoute.Kind == FlowchartRouteKind.DecisionFanOut)
        {
            InvalidBehaviours.Add("CollapseWithUnrelatedEdge");
        }

        return [.. InvalidBehaviours.Distinct(StringComparer.Ordinal)];
    }

    private static bool IsShortAdjacentEdge(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction)
    {
        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        if (VerticalFlow)
        {
            double FlowAxisDelta = Math.Abs(SourceBounds.CenterY - TargetBounds.CenterY);
            double TypicalSpan = ((SourceBounds.Height + TargetBounds.Height) / 2.0) + 40.0;
            return FlowAxisDelta <= (TypicalSpan * ADJACENT_RANK_MAX_FLOW_AXIS_DELTA_RATIO);
        }

        double HorizontalDelta = Math.Abs(SourceBounds.CenterX - TargetBounds.CenterX);
        double TypicalHorizontalSpan = ((SourceBounds.Width + TargetBounds.Width) / 2.0) + 40.0;
        return HorizontalDelta <= (TypicalHorizontalSpan * ADJACENT_RANK_MAX_FLOW_AXIS_DELTA_RATIO);
    }

    private static int CountVisibleIncoming(DiagramContract Contract, string NodeId)
    {
        int Count = 0;
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Candidate.TargetId, NodeId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }

    private static string FormatDirection(FlowchartDirection Direction)
    {
        switch (Direction)
        {
            case FlowchartDirection.TopToBottom:
                return "TD";
            case FlowchartDirection.BottomToTop:
                return "BT";
            case FlowchartDirection.LeftToRight:
                return "LR";
            case FlowchartDirection.RightToLeft:
                return "RL";
            default:
                return Direction.ToString();
        }
    }

    /// <summary>
    /// Builds route-class names keyed by edge id or <c>source-&gt;target</c> for label placement.
    /// </summary>
    public static Dictionary<string, string> BuildRouteClassNamesByEdgeKey(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction)
    {
        Dictionary<string, string> RouteClassByEdgeKey =
            new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            RenderOptions,
            out FlowchartSpineLayoutContext? SpineContext);

        int EdgeSequence = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                || !BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            EdgeSequence++;
            FlowchartEdgeRoute StructuralRoute = FlowchartRouter.ClassifyEdge(Edge, Contract);
            FlowchartEdgeSemanticRouteClass SemanticRouteClass = FlowchartEdgeRouteClassifier.Classify(
                Edge, Contract, BoundsById, NodeById, Direction, SpineContext);
            FlowchartRoutingIntentRouteClass RouteClass = InferRouteClass(
                Edge, Contract, BoundsById, NodeById, Direction, SpineContext,
                StructuralRoute, SemanticRouteClass, SourceBounds, TargetBounds);
            string RouteClassName = MapRouteClassName(RouteClass);
            RouteClassByEdgeKey["E" + EdgeSequence.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)] = RouteClassName;
            RouteClassByEdgeKey[Edge.SourceId + "->" + Edge.TargetId] = RouteClassName;
        }

        return RouteClassByEdgeKey;
    }

    private static string MapRouteClassName(FlowchartRoutingIntentRouteClass RouteClass)
    {
        switch (RouteClass)
        {
            case FlowchartRoutingIntentRouteClass.CentreSpine:
                return "CentreSpine";
            case FlowchartRoutingIntentRouteClass.ShortAdjacent:
                return "ShortAdjacent";
            case FlowchartRoutingIntentRouteClass.LocalDecisionBranch:
                return "LocalDecisionBranch";
            case FlowchartRoutingIntentRouteClass.MultiInputMerge:
                return "FanIn";
            case FlowchartRoutingIntentRouteClass.MergeBack:
                return "MergeBack";
            case FlowchartRoutingIntentRouteClass.LongExceptionBypass:
            case FlowchartRoutingIntentRouteClass.SkipBypass:
                return "LongBypass";
            case FlowchartRoutingIntentRouteClass.ReturnBackward:
                return "CycleOrReturn";
            case FlowchartRoutingIntentRouteClass.CrossLane:
            case FlowchartRoutingIntentRouteClass.ObstacleAvoidance:
                return "CrossLane";
            default:
                return "Unknown";
        }
    }
}
