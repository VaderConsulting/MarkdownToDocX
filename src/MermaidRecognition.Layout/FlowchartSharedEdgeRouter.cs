using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// The one shared flowchart router. It searches ports, channels and detours for a failure-free
/// layout, then records a justification only when no such option exists.
/// </summary>
public static class FlowchartSharedEdgeRouter
{
    /// <summary>
    /// Minimum gap between two arrowheads or attach points on the same face.
    /// </summary>
    public const double ARROWHEAD_SEPARATION_PX = 10.0;

    /// <summary>
    /// Minimum gap between parallel segments of different edges.
    /// </summary>
    public const double PARALLEL_SEPARATION_PX = 8.0;

    /// <summary>
    /// Maximum distance from a label centre to its own rendered edge.
    /// </summary>
    public const double LABEL_OWN_EDGE_PX = 8.0;

    /// <summary>
    /// Minimum gap from a group border when a route runs parallel to that border.
    /// </summary>
    public const double GROUP_BORDER_CLEARANCE_PX = 12.0;

    /// <summary>
    /// Extra clearance so a curveBasis bow cannot enter a node body.
    /// </summary>
    public const double CURVE_BOW_CLEARANCE_PX = 28.0;

    /// <summary>
    /// Justifications recorded by the last <see cref="RouteAll"/> call.
    /// </summary>
    public static IReadOnlyList<FlowchartLayoutQualityFailure> LastJustifications
    {
        get
        {
            return _LastJustifications;
        }
    }

    private static List<FlowchartLayoutQualityFailure> _LastJustifications = [];

    /// <summary>
    /// Searches alternative ports, channels and detours for every non-designed edge, then unifies style.
    /// </summary>
    /// <param name="Contract">The diagram.</param>
    /// <param name="BoundsById">Node and group bounds. Same-rank swaps are written back here.</param>
    /// <param name="NodeById">Nodes by id.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <param name="Guides">Resolved guides; non-designed routes are replaced.</param>
    public static void RouteAll(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        List<FlowchartResolvedGuide> Guides)
    {
        _LastJustifications = [];
        ApplyPortSpread(Contract, BoundsById, NodeById, Direction, Guides);

        List<FlowchartResolvedGuide> Searchable = [];
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (IsDesignedRoute(Guide.RouteKind))
            {
                continue;
            }

            Searchable.Add(Guide);
        }

        Searchable.Sort(CompareGuideLength);

        foreach (FlowchartResolvedGuide Guide in Searchable)
        {
            RouteOneEdge(Contract, BoundsById, NodeById, Direction, Guides, Guide, PenaliseCrossings: true);
        }

        ApplyPortSpread(Contract, BoundsById, NodeById, Direction, Guides);
        SeparateParallelRenderedSegments(Direction, Guides);
        UnifyFlowchartStyle(Guides);
        RecordRemainingJustifications(Contract, BoundsById, Direction, Guides);
    }

    /// <summary>
    /// True when the route kind is a designed builder that the shared router must not rewrite.
    /// </summary>
    /// <param name="RouteKind">Resolved kind.</param>
    /// <returns>True for decision, retry, grouped two-by-two and designed corridor routes.</returns>
    public static bool IsDesignedRoute(FlowchartEdgeRouteKind RouteKind)
    {
        return RouteKind == FlowchartEdgeRouteKind.Decision
            || RouteKind == FlowchartEdgeRouteKind.RetryLoop
            || RouteKind == FlowchartEdgeRouteKind.GroupedTwoByTwo
            || RouteKind == FlowchartEdgeRouteKind.Corridor;
    }

    /// <summary>
    /// Flattens the same SVG path the renderer will emit, in the coordinate system of the guides.
    /// </summary>
    /// <param name="Points">Guide points.</param>
    /// <param name="RouteKind">Resolved kind.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <returns>Sampled polyline, or an empty list when the path cannot be flattened.</returns>
    public static List<(double X, double Y)> FlattenRenderedGuide(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction)
    {
        if (Points.Count < 2)
        {
            return [];
        }

        if (RouteKind == FlowchartEdgeRouteKind.Straight && Points.Count >= 2)
        {
            return
            [
                Points[0],
                Points[Points.Count - 1]
            ];
        }

        string PathData = FlowchartCurvePathBuilder.BuildRenderedEdgePathData(Points, RouteKind, Direction);
        if (FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples)
            && Samples.Count >= 2)
        {
            return Samples;
        }

        List<(double X, double Y)> Copy = [];
        foreach ((double X, double Y) Point in Points)
        {
            Copy.Add(Point);
        }

        return Copy;
    }

    private static void RouteOneEdge(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        IReadOnlyList<FlowchartResolvedGuide> AllGuides,
        FlowchartResolvedGuide Guide,
        bool PenaliseCrossings)
    {
        if (Guide.Points.Count < 2)
        {
            return;
        }

        List<CandidateRoute> Candidates = BuildCandidates(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            Guide);

        CandidateRoute? Best = null;
        double BestScore = double.PositiveInfinity;
        foreach (CandidateRoute Candidate in Candidates)
        {
            double Score = ScoreCandidate(
                Contract,
                BoundsById,
                Direction,
                AllGuides,
                Guide,
                Candidate,
                PenaliseCrossings);
            if (Score < BestScore)
            {
                BestScore = Score;
                Best = Candidate;
            }
        }

        if (Best is null)
        {
            return;
        }

        List<(double X, double Y)> BestSamples = FlattenRenderedGuide(Best.Points, Best.RouteKind, Direction);
        if (RenderedPathHitsNonEndpoint(Contract, BoundsById, Guide.Edge, BestSamples, out string? HitNodeId)
            && !string.IsNullOrEmpty(HitNodeId)
            && BoundsById.TryGetValue(HitNodeId, out FlowchartLayoutNodeBounds HitBounds)
            && Best.Points.Count >= 2)
        {
            (double StartX, double StartY) = Best.Points[0];
            (double EndX, double EndY) = Best.Points[Best.Points.Count - 1];
            double Left = HitBounds.CenterX - (HitBounds.Width / 2.0) - 80.0;
            double Right = HitBounds.CenterX + (HitBounds.Width / 2.0) + 80.0;
            double Top = HitBounds.CenterY + (HitBounds.Height / 2.0) + 80.0;
            double Bottom = HitBounds.CenterY - (HitBounds.Height / 2.0) - 80.0;
            List<CandidateRoute> Rescue =
            [
                new CandidateRoute([(StartX, StartY), (Left, StartY), (Left, EndY), (EndX, EndY)], FlowchartEdgeRouteKind.Curved),
                new CandidateRoute([(StartX, StartY), (Right, StartY), (Right, EndY), (EndX, EndY)], FlowchartEdgeRouteKind.Curved),
                new CandidateRoute([(StartX, StartY), (StartX, Top), (EndX, Top), (EndX, EndY)], FlowchartEdgeRouteKind.Curved),
                new CandidateRoute([(StartX, StartY), (StartX, Bottom), (EndX, Bottom), (EndX, EndY)], FlowchartEdgeRouteKind.Curved)
            ];
            foreach (CandidateRoute Candidate in Rescue)
            {
                double Score = ScoreCandidate(
                    Contract,
                    BoundsById,
                    Direction,
                    AllGuides,
                    Guide,
                    Candidate,
                    PenaliseCrossings);
                if (Score < BestScore)
                {
                    BestScore = Score;
                    Best = Candidate;
                }
            }
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Best.Points);
        Guide.RouteKind = Best.RouteKind;
    }

    private static List<CandidateRoute> BuildCandidates(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartResolvedGuide Guide)
    {
        List<CandidateRoute> Candidates = [];
        if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            Candidates.Add(new CandidateRoute(CopyPoints(Guide.Points), Guide.RouteKind));
            return Candidates;
        }

        string? SourceShape = ResolveShape(Contract, NodeById, Guide.Edge.SourceId);
        string? TargetShape = ResolveShape(Contract, NodeById, Guide.Edge.TargetId);
        List<(double StartX, double StartY, double EndX, double EndY)> PortPairs = BuildPortPairs(
            SourceBounds,
            TargetBounds,
            SourceShape,
            TargetShape,
            Guide.Edge.MarkerStart,
            Guide.Edge.MarkerEnd,
            Guide.Points);

        foreach ((double StartX, double StartY, double EndX, double EndY) in PortPairs)
        {
            AddDetourCandidates(Contract, BoundsById, Guide.Edge, StartX, StartY, EndX, EndY, Candidates);
        }

        Candidates.Add(new CandidateRoute(CopyPoints(Guide.Points), Guide.RouteKind));
        return Candidates;
    }

    private static void AddDetourCandidates(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge,
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        List<CandidateRoute> Candidates)
    {
        Candidates.Add(new CandidateRoute([(StartX, StartY), (EndX, EndY)], FlowchartEdgeRouteKind.Straight));

        double LaneLeft = double.PositiveInfinity;
        double LaneRight = double.NegativeInfinity;
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            LaneLeft = Math.Min(LaneLeft, Bounds.CenterX - (Bounds.Width / 2.0));
            LaneRight = Math.Max(LaneRight, Bounds.CenterX + (Bounds.Width / 2.0));
        }

        if (!double.IsInfinity(LaneLeft))
        {
            LaneLeft -= 20.0;
            LaneRight += 20.0;
            Candidates.Add(new CandidateRoute(
                [(StartX, StartY), (LaneLeft, StartY), (LaneLeft, EndY), (EndX, EndY)],
                FlowchartEdgeRouteKind.Curved));
            Candidates.Add(new CandidateRoute(
                [(StartX, StartY), (LaneRight, StartY), (LaneRight, EndY), (EndX, EndY)],
                FlowchartEdgeRouteKind.Curved));
        }

        double DeltaX = EndX - StartX;
        double DeltaY = EndY - StartY;
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        double PerpX = Length > 1e-6 ? -DeltaY / Length : -1.0;
        double PerpY = Length > 1e-6 ? DeltaX / Length : 0.0;
        double[] Offsets = [56.0, 120.0, 200.0];
        foreach (double Offset in Offsets)
        {
            foreach (double Sign in new[] { 1.0, -1.0 })
            {
                double MidX = ((StartX + EndX) / 2.0) + (PerpX * Offset * Sign);
                double MidY = ((StartY + EndY) / 2.0) + (PerpY * Offset * Sign);
                Candidates.Add(new CandidateRoute(
                    [(StartX, StartY), (MidX, MidY), (EndX, EndY)],
                    FlowchartEdgeRouteKind.Curved));
            }
        }

        foreach (FlowchartLayoutNodeBounds Obstacle in BoundsById.Values)
        {
            if (IsEndpointOrAncestorGroup(Contract, Edge, Obstacle.NodeId)
                || FlowchartCompoundEndpointHelper.IsGroupId(Contract, Obstacle.NodeId))
            {
                continue;
            }

            if (!ObstacleBlocksRoute(StartX, StartY, EndX, EndY, Obstacle))
            {
                continue;
            }

            double Left = Obstacle.CenterX - (Obstacle.Width / 2.0) - 56.0;
            double Right = Obstacle.CenterX + (Obstacle.Width / 2.0) + 56.0;
            double Top = Obstacle.CenterY + (Obstacle.Height / 2.0) + 56.0;
            double Bottom = Obstacle.CenterY - (Obstacle.Height / 2.0) - 56.0;
            (double X, double Y)[] Waypoints =
            [
                (Left, Obstacle.CenterY),
                (Right, Obstacle.CenterY),
                (Obstacle.CenterX, Top),
                (Obstacle.CenterX, Bottom)
            ];
            foreach ((double WayX, double WayY) in Waypoints)
            {
                Candidates.Add(new CandidateRoute(
                    [(StartX, StartY), (WayX, WayY), (EndX, EndY)],
                    FlowchartEdgeRouteKind.Curved));
                Candidates.Add(new CandidateRoute(
                    [(StartX, StartY), (WayX, StartY), (WayX, EndY), (EndX, EndY)],
                    FlowchartEdgeRouteKind.Curved));
                Candidates.Add(new CandidateRoute(
                    [(StartX, StartY), (StartX, WayY), (EndX, WayY), (EndX, EndY)],
                    FlowchartEdgeRouteKind.Curved));
            }
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds GroupBounds))
            {
                continue;
            }

            bool SourceInside = IsMemberOfGroup(Contract, Edge.SourceId, Group.Id);
            bool TargetInside = IsMemberOfGroup(Contract, Edge.TargetId, Group.Id);
            double GroupLeft = GroupBounds.CenterX - (GroupBounds.Width / 2.0);
            double GroupRight = GroupBounds.CenterX + (GroupBounds.Width / 2.0);
            double GroupTop = GroupBounds.CenterY + (GroupBounds.Height / 2.0);
            double GroupBottom = GroupBounds.CenterY - (GroupBounds.Height / 2.0);
            if (!SourceInside && !TargetInside)
            {
                double MidMinX = Math.Min(StartX, EndX) + 40.0;
                double MidMaxX = Math.Max(StartX, EndX) - 40.0;
                double MidMinY = Math.Min(StartY, EndY) + 40.0;
                double MidMaxY = Math.Max(StartY, EndY) - 40.0;
                bool GroupSitsBetween = GroupBounds.CenterX > MidMinX
                    && GroupBounds.CenterX < MidMaxX
                    && GroupBounds.CenterY > MidMinY
                    && GroupBounds.CenterY < MidMaxY;
                if (!GroupSitsBetween)
                {
                    continue;
                }

                foreach (double ExteriorGap in new[] { 40.0, 88.0 })
                {
                    Candidates.Add(new CandidateRoute(
                        [(StartX, StartY), (GroupLeft - ExteriorGap, StartY), (GroupLeft - ExteriorGap, EndY), (EndX, EndY)],
                        FlowchartEdgeRouteKind.Curved));
                    Candidates.Add(new CandidateRoute(
                        [(StartX, StartY), (GroupRight + ExteriorGap, StartY), (GroupRight + ExteriorGap, EndY), (EndX, EndY)],
                        FlowchartEdgeRouteKind.Curved));
                    Candidates.Add(new CandidateRoute(
                        [(StartX, StartY), (StartX, GroupTop + ExteriorGap), (EndX, GroupTop + ExteriorGap), (EndX, EndY)],
                        FlowchartEdgeRouteKind.Curved));
                    Candidates.Add(new CandidateRoute(
                        [(StartX, StartY), (StartX, GroupBottom - ExteriorGap), (EndX, GroupBottom - ExteriorGap), (EndX, EndY)],
                        FlowchartEdgeRouteKind.Curved));
                }

                continue;
            }

            if (SourceInside == TargetInside)
            {
                continue;
            }

            double InnerX = SourceInside ? StartX : EndX;
            double InnerY = SourceInside ? StartY : EndY;
            double FacingX = SourceInside ? EndX : StartX;
            double FacingY = SourceInside ? EndY : StartY;
            AddPerpendicularGroupExit(
                Candidates,
                StartX,
                StartY,
                EndX,
                EndY,
                GroupLeft,
                GroupRight,
                GroupTop,
                GroupBottom,
                GroupBounds.CenterX,
                GroupBounds.CenterY,
                FacingX,
                FacingY);
            AddPerpendicularGroupExit(
                Candidates,
                StartX,
                StartY,
                EndX,
                EndY,
                GroupLeft,
                GroupRight,
                GroupTop,
                GroupBottom,
                GroupBounds.CenterX,
                GroupBounds.CenterY,
                InnerX,
                InnerY);
        }
    }

    private static List<(double StartX, double StartY, double EndX, double EndY)> BuildPortPairs(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape,
        EdgeEndpointMarker MarkerStart,
        EdgeEndpointMarker MarkerEnd,
        IReadOnlyList<(double X, double Y)> Existing)
    {
        List<(double StartX, double StartY, double EndX, double EndY)> Pairs = [];
        if (Existing.Count >= 2)
        {
            Pairs.Add((Existing[0].X, Existing[0].Y, Existing[Existing.Count - 1].X, Existing[Existing.Count - 1].Y));
        }

        PortFace PreferredSource = PreferFaceToward(SourceBounds, TargetBounds);
        PortFace PreferredTarget = PreferFaceToward(TargetBounds, SourceBounds);
        (double StartX, double StartY) = FacePort(SourceBounds, SourceShape, PreferredSource, MarkerStart);
        (double EndX, double EndY) = FacePort(TargetBounds, TargetShape, PreferredTarget, MarkerEnd);
        Pairs.Add((StartX, StartY, EndX, EndY));
        return Pairs;
    }

    private static double ScoreCandidate(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        IReadOnlyList<FlowchartResolvedGuide> AllGuides,
        FlowchartResolvedGuide Guide,
        CandidateRoute Candidate,
        bool PenaliseCrossings)
    {
        List<(double X, double Y)> Samples = FlattenRenderedGuide(Candidate.Points, Candidate.RouteKind, Direction);
        if (Samples.Count < 2)
        {
            return double.PositiveInfinity;
        }

        double Score = PathLength(Samples);
        string? HitNodeId;
        if (RenderedPathHitsNonEndpoint(Contract, BoundsById, Guide.Edge, Samples, out HitNodeId))
        {
            Score += 500000.0;
        }

        if (HugsGroupBorder(Contract, BoundsById, Guide.Edge, Samples))
        {
            Score += 20000.0;
        }

        if (CutsThroughForeignGroup(Contract, BoundsById, Guide.Edge, Samples))
        {
            Score += 200000.0;
        }

        if (HasLongOwnGroupInterior(Contract, BoundsById, Guide.Edge, Samples))
        {
            Score += 150000.0;
        }

        if (HasHook(Samples, Direction))
        {
            Score += 8000.0;
        }

        if (LeavesContentBox(BoundsById, Samples))
        {
            Score += 250000.0;
        }

        if (PenaliseCrossings)
        {
            Score += 50000.0 * CountCrossingsWithOthers(AllGuides, Guide, Samples, Direction);
            Score += 4000.0 * CountNearParallel(AllGuides, Guide, Samples, Direction);
        }
        return Score;
    }

    private static void ApplyPortSpread(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        List<FlowchartResolvedGuide> Guides)
    {
        Dictionary<string, List<PortUse>> UsesByNode =
            new Dictionary<string, List<PortUse>>(StringComparer.Ordinal);
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (Guide.Points.Count < 2)
            {
                continue;
            }

            AddPortUse(UsesByNode, Guide.Edge.SourceId, Guide, AtSource: true);
            AddPortUse(UsesByNode, Guide.Edge.TargetId, Guide, AtSource: false);
        }

        foreach (KeyValuePair<string, List<PortUse>> Pair in UsesByNode)
        {
            if (Pair.Value.Count < 2 || !BoundsById.TryGetValue(Pair.Key, out FlowchartLayoutNodeBounds NodeBounds))
            {
                continue;
            }

            Dictionary<PortFace, List<PortUse>> ByFace = new Dictionary<PortFace, List<PortUse>>();
            foreach (PortUse Use in Pair.Value)
            {
                PortFace Face = ClassifyFace(NodeBounds, Use.X, Use.Y);
                if (!ByFace.TryGetValue(Face, out List<PortUse>? FaceUses))
                {
                    FaceUses = [];
                    ByFace[Face] = FaceUses;
                }

                FaceUses.Add(Use);
            }

            foreach (KeyValuePair<PortFace, List<PortUse>> FacePair in ByFace)
            {
                if (FacePair.Value.Count < 2)
                {
                    continue;
                }

                SpreadFace(NodeBounds, ResolveShape(Contract, NodeById, Pair.Key), FacePair.Key, FacePair.Value);
            }
        }

        _ = Direction;
    }

    private static void SpreadFace(
        FlowchartLayoutNodeBounds NodeBounds,
        string? Shape,
        PortFace Face,
        List<PortUse> Uses)
    {
        Uses.Sort((Left, Right) =>
        {
            if (FaceIsHorizontal(Face))
            {
                return Left.X.CompareTo(Right.X);
            }

            return Left.Y.CompareTo(Right.Y);
        });

        double Span = FaceIsHorizontal(Face) ? NodeBounds.Width : NodeBounds.Height;
        double Usable = Math.Max(ARROWHEAD_SEPARATION_PX, Span * 0.72);
        double Step = Uses.Count == 1 ? 0.0 : Usable / (Uses.Count - 1);
        double Origin = FaceIsHorizontal(Face)
            ? NodeBounds.CenterX - (Usable / 2.0)
            : NodeBounds.CenterY - (Usable / 2.0);
        for (int Index = 0; Index < Uses.Count; Index++)
        {
            PortUse Use = Uses[Index];
            double Slot = Origin + (Step * Index);
            (double PortX, double PortY) = FacePortAtSlot(NodeBounds, Shape, Face, Slot, Use.Marker);
            ReplaceEndpoint(Use.Guide, Use.AtSource, PortX, PortY);
        }
    }

    private static void SeparateParallelRenderedSegments(
        FlowchartDirection Direction,
        List<FlowchartResolvedGuide> Guides)
    {
        for (int Pass = 0; Pass < 3; Pass++)
        {
            for (int LeftIndex = 0; LeftIndex < Guides.Count; LeftIndex++)
            {
                FlowchartResolvedGuide Left = Guides[LeftIndex];
                List<(double X, double Y)> LeftSamples = FlattenRenderedGuide(Left.Points, Left.RouteKind, Direction);
                for (int RightIndex = LeftIndex + 1; RightIndex < Guides.Count; RightIndex++)
                {
                    FlowchartResolvedGuide Right = Guides[RightIndex];
                    if (SharesEndpoint(Left.Edge, Right.Edge))
                    {
                        continue;
                    }

                    List<(double X, double Y)> RightSamples = FlattenRenderedGuide(Right.Points, Right.RouteKind, Direction);
                    if (!TryGetClosestParallelGap(LeftSamples, RightSamples, out double Gap, out double ShiftX, out double ShiftY))
                    {
                        continue;
                    }

                    if (Gap >= PARALLEL_SEPARATION_PX + 2.0)
                    {
                        continue;
                    }

                    double Need = PARALLEL_SEPARATION_PX + 2.0 - Gap + 1.0;
                    ShiftInteriorPoints(Right, ShiftX * Need, ShiftY * Need);
                }
            }
        }
    }

    private static void UnifyFlowchartStyle(List<FlowchartResolvedGuide> Guides)
    {
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (IsDesignedRoute(Guide.RouteKind))
            {
                continue;
            }

            if (Guide.Points.Count <= 2)
            {
                Guide.RouteKind = FlowchartEdgeRouteKind.Straight;
                continue;
            }

            if (AreAxisAligned(Guide.Points[0], Guide.Points[Guide.Points.Count - 1])
                && Guide.Points.Count == 2)
            {
                Guide.RouteKind = FlowchartEdgeRouteKind.Straight;
                continue;
            }

            Guide.RouteKind = FlowchartEdgeRouteKind.Curved;
        }
    }

    private static void RecordRemainingJustifications(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        List<FlowchartResolvedGuide> Guides)
    {
        for (int LeftIndex = 0; LeftIndex < Guides.Count; LeftIndex++)
        {
            FlowchartResolvedGuide Left = Guides[LeftIndex];
            List<(double X, double Y)> LeftSamples = FlattenRenderedGuide(Left.Points, Left.RouteKind, Direction);
            if (RenderedPathHitsNonEndpoint(Contract, BoundsById, Left.Edge, LeftSamples, out string? HitNodeId)
                && !string.IsNullOrEmpty(HitNodeId)
                && IsNodeFullyEnclosingCorridor(BoundsById, Left.Edge, HitNodeId))
            {
                _LastJustifications.Add(new FlowchartLayoutQualityFailure(
                    "NodeIntersection",
                    Left.Edge.SourceId + "->" + Left.Edge.TargetId + " intersects " + HitNodeId,
                    [Left.Edge.SourceId, Left.Edge.TargetId, HitNodeId],
                    "node " + HitNodeId + " fully enclosed"));
            }

            for (int RightIndex = LeftIndex + 1; RightIndex < Guides.Count; RightIndex++)
            {
                FlowchartResolvedGuide Right = Guides[RightIndex];
                if (SharesEndpoint(Left.Edge, Right.Edge))
                {
                    continue;
                }

                List<(double X, double Y)> RightSamples = FlattenRenderedGuide(Right.Points, Right.RouteKind, Direction);
                if (!PolylinesCross(LeftSamples, RightSamples))
                {
                    continue;
                }

                string Reason;
                if (FlowchartLayoutCrossingSearch.TryJustifyK33Crossing(Contract, Left.Edge, Right.Edge, out string K33Reason))
                {
                    Reason = K33Reason;
                }
                else if (FlowchartLayoutCrossingSearch.TryJustifyUnavoidableLayeredCrossing(
                    Contract,
                    BoundsById,
                    Left.Edge,
                    Right.Edge,
                    Direction,
                    out string LayeredReason))
                {
                    Reason = LayeredReason;
                }
                else
                {
                    continue;
                }

                if (!FlowchartLayoutQualityJustification.IsSpecificReason(Reason))
                {
                    continue;
                }

                _LastJustifications.Add(new FlowchartLayoutQualityFailure(
                    "EdgeCrossing",
                    Left.Edge.SourceId + "->" + Left.Edge.TargetId
                    + " crosses "
                    + Right.Edge.SourceId + "->" + Right.Edge.TargetId,
                    [Left.Edge.SourceId, Left.Edge.TargetId, Right.Edge.SourceId, Right.Edge.TargetId],
                    Reason));
            }
        }
    }

    private static bool RenderedPathHitsNonEndpoint(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge,
        IReadOnlyList<(double X, double Y)> Samples,
        out string? HitNodeId)
    {
        HitNodeId = null;
        if (Samples.Count < 2)
        {
            return false;
        }

        double Skip = 4.0;
        double Travelled = 0.0;
        double Total = PathLength(Samples);
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            (double StartX, double StartY) = Samples[Index - 1];
            (double EndX, double EndY) = Samples[Index];
            double Segment = Distance(StartX, StartY, EndX, EndY);
            double MidTravel = Travelled + (Segment / 2.0);
            Travelled += Segment;
            if (MidTravel < Skip || MidTravel > Total - Skip)
            {
                continue;
            }

            foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
            {
                if (IsEndpointOrAncestorGroup(Contract, Edge, Bounds.NodeId))
                {
                    continue;
                }

                if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Bounds.NodeId))
                {
                    continue;
                }

                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(StartX, StartY, EndX, EndY, Bounds, -6.0))
                {
                    HitNodeId = Bounds.NodeId;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HugsGroupBorder(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge,
        IReadOnlyList<(double X, double Y)> Samples)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds GroupBounds))
            {
                continue;
            }

            if (IsEndpointOrAncestorGroup(Contract, Edge, Group.Id))
            {
                continue;
            }

            double Left = GroupBounds.CenterX - (GroupBounds.Width / 2.0);
            double Right = GroupBounds.CenterX + (GroupBounds.Width / 2.0);
            double Top = GroupBounds.CenterY + (GroupBounds.Height / 2.0);
            double Bottom = GroupBounds.CenterY - (GroupBounds.Height / 2.0);
            for (int Index = 1; Index < Samples.Count; Index++)
            {
                (double StartX, double StartY) = Samples[Index - 1];
                (double EndX, double EndY) = Samples[Index];
                double MidX = (StartX + EndX) / 2.0;
                double MidY = (StartY + EndY) / 2.0;
                bool Vertical = Math.Abs(StartX - EndX) <= 1.5 && Math.Abs(StartY - EndY) > 8.0;
                bool Horizontal = Math.Abs(StartY - EndY) <= 1.5 && Math.Abs(StartX - EndX) > 8.0;
                if (Vertical
                    && MidY < Top
                    && MidY > Bottom
                    && (Math.Abs(MidX - Left) < GROUP_BORDER_CLEARANCE_PX
                        || Math.Abs(MidX - Right) < GROUP_BORDER_CLEARANCE_PX))
                {
                    return true;
                }

                if (Horizontal
                    && MidX > Left
                    && MidX < Right
                    && (Math.Abs(MidY - Top) < GROUP_BORDER_CLEARANCE_PX
                        || Math.Abs(MidY - Bottom) < GROUP_BORDER_CLEARANCE_PX))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CutsThroughForeignGroup(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge,
        IReadOnlyList<(double X, double Y)> Samples)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds GroupBounds))
            {
                continue;
            }

            bool SourceInside = IsMemberOfGroup(Contract, Edge.SourceId, Group.Id);
            bool TargetInside = IsMemberOfGroup(Contract, Edge.TargetId, Group.Id);
            if (SourceInside || TargetInside)
            {
                continue;
            }

            double Left = GroupBounds.CenterX - (GroupBounds.Width / 2.0) + 6.0;
            double Right = GroupBounds.CenterX + (GroupBounds.Width / 2.0) - 6.0;
            double Top = GroupBounds.CenterY + (GroupBounds.Height / 2.0) - 6.0;
            double Bottom = GroupBounds.CenterY - (GroupBounds.Height / 2.0) + 6.0;
            int InteriorHits = 0;
            foreach ((double X, double Y) in Samples)
            {
                if (X > Left && X < Right && Y < Top && Y > Bottom)
                {
                    InteriorHits++;
                }
            }

            if (InteriorHits >= 3)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddPerpendicularGroupExit(
        List<CandidateRoute> Candidates,
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double GroupLeft,
        double GroupRight,
        double GroupTop,
        double GroupBottom,
        double GroupCenterX,
        double GroupCenterY,
        double AimX,
        double AimY)
    {
        double CrossX = Math.Max(GroupLeft + 16.0, Math.Min(GroupRight - 16.0, AimX));
        double CrossY = Math.Max(GroupBottom + 16.0, Math.Min(GroupTop - 16.0, AimY));
        bool CloserToVertical = DistanceToVerticalBorder(AimX, GroupLeft, GroupRight)
            <= DistanceToHorizontalBorder(AimY, GroupTop, GroupBottom);
        if (CloserToVertical)
        {
            double BorderX = Math.Abs(AimX - GroupLeft) < Math.Abs(AimX - GroupRight)
                ? GroupLeft
                : GroupRight;
            double InteriorX = BorderX + (Math.Sign(GroupCenterX - BorderX) * 18.0);
            double ExteriorX = BorderX - (Math.Sign(GroupCenterX - BorderX) * 18.0);
            Candidates.Add(new CandidateRoute(
                [(StartX, StartY), (InteriorX, CrossY), (ExteriorX, CrossY), (EndX, EndY)],
                FlowchartEdgeRouteKind.Curved));
            return;
        }

        double BorderY = Math.Abs(AimY - GroupTop) < Math.Abs(AimY - GroupBottom)
            ? GroupTop
            : GroupBottom;
        double InteriorY = BorderY + (Math.Sign(GroupCenterY - BorderY) * 18.0);
        double ExteriorY = BorderY - (Math.Sign(GroupCenterY - BorderY) * 18.0);
        Candidates.Add(new CandidateRoute(
            [(StartX, StartY), (CrossX, InteriorY), (CrossX, ExteriorY), (EndX, EndY)],
            FlowchartEdgeRouteKind.Curved));
    }

    private static bool HasLongOwnGroupInterior(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge,
        IReadOnlyList<(double X, double Y)> Samples)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds GroupBounds))
            {
                continue;
            }

            bool SourceInside = IsMemberOfGroup(Contract, Edge.SourceId, Group.Id);
            bool TargetInside = IsMemberOfGroup(Contract, Edge.TargetId, Group.Id);
            if (SourceInside == TargetInside)
            {
                continue;
            }

            double Left = GroupBounds.CenterX - (GroupBounds.Width / 2.0) + 4.0;
            double Right = GroupBounds.CenterX + (GroupBounds.Width / 2.0) - 4.0;
            double Top = GroupBounds.CenterY + (GroupBounds.Height / 2.0) - 4.0;
            double Bottom = GroupBounds.CenterY - (GroupBounds.Height / 2.0) + 4.0;
            double Interior = 0.0;
            for (int Index = 1; Index < Samples.Count; Index++)
            {
                double MidX = (Samples[Index - 1].X + Samples[Index].X) / 2.0;
                double MidY = (Samples[Index - 1].Y + Samples[Index].Y) / 2.0;
                if (MidX > Left && MidX < Right && MidY < Top && MidY > Bottom)
                {
                    Interior += Distance(
                        Samples[Index - 1].X,
                        Samples[Index - 1].Y,
                        Samples[Index].X,
                        Samples[Index].Y);
                }
            }

            string InnerId = SourceInside ? Edge.SourceId : Edge.TargetId;
            if (!BoundsById.TryGetValue(InnerId, out FlowchartLayoutNodeBounds Inner))
            {
                continue;
            }

            double Nearest = Math.Min(
                Math.Min(
                    Math.Abs(Inner.CenterX - (GroupBounds.CenterX - (GroupBounds.Width / 2.0))),
                    Math.Abs((GroupBounds.CenterX + (GroupBounds.Width / 2.0)) - Inner.CenterX)),
                Math.Min(
                    Math.Abs((GroupBounds.CenterY + (GroupBounds.Height / 2.0)) - Inner.CenterY),
                    Math.Abs(Inner.CenterY - (GroupBounds.CenterY - (GroupBounds.Height / 2.0)))));
            if (Interior > (Nearest * 2.0) + 40.0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHook(IReadOnlyList<(double X, double Y)> Samples, FlowchartDirection Direction)
    {
        if (Samples.Count < 3)
        {
            return false;
        }

        (double StartX, double StartY) = Samples[0];
        (double EndX, double EndY) = Samples[Samples.Count - 1];
        bool Vertical = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        double Progress = Vertical ? EndY - StartY : EndX - StartX;
        if (Math.Abs(Progress) < 8.0)
        {
            return false;
        }

        double Sign = Math.Sign(Progress);
        double Best = Vertical ? StartY : StartX;
        foreach ((double X, double Y) in Samples)
        {
            double Along = Vertical ? Y : X;
            if ((Along - Best) * Sign < -24.0)
            {
                return true;
            }

            if ((Along - Best) * Sign > 0.0)
            {
                Best = Along;
            }
        }

        return false;
    }

    private static int CountCrossingsWithOthers(
        IReadOnlyList<FlowchartResolvedGuide> AllGuides,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Samples,
        FlowchartDirection Direction)
    {
        int Count = 0;
        foreach (FlowchartResolvedGuide Other in AllGuides)
        {
            if (ReferenceEquals(Other, Guide) || SharesEndpoint(Guide.Edge, Other.Edge))
            {
                continue;
            }

            List<(double X, double Y)> OtherSamples = FlattenRenderedGuide(Other.Points, Other.RouteKind, Direction);
            if (PolylinesCross(Samples, OtherSamples))
            {
                Count++;
            }
        }

        return Count;
    }

    private static int CountNearParallel(
        IReadOnlyList<FlowchartResolvedGuide> AllGuides,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Samples,
        FlowchartDirection Direction)
    {
        int Count = 0;
        foreach (FlowchartResolvedGuide Other in AllGuides)
        {
            if (ReferenceEquals(Other, Guide) || SharesEndpoint(Guide.Edge, Other.Edge))
            {
                continue;
            }

            List<(double X, double Y)> OtherSamples = FlattenRenderedGuide(Other.Points, Other.RouteKind, Direction);
            if (TryGetClosestParallelGap(Samples, OtherSamples, out double Gap, out _, out _)
                && Gap < PARALLEL_SEPARATION_PX)
            {
                Count++;
            }
        }

        return Count;
    }

    private static bool TryGetClosestParallelGap(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right,
        out double Gap,
        out double ShiftX,
        out double ShiftY)
    {
        Gap = double.PositiveInfinity;
        ShiftX = 0.0;
        ShiftY = 0.0;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            (double Lx1, double Ly1) = Left[LeftIndex - 1];
            (double Lx2, double Ly2) = Left[LeftIndex];
            double Ldx = Lx2 - Lx1;
            double Ldy = Ly2 - Ly1;
            double Llen = Math.Sqrt((Ldx * Ldx) + (Ldy * Ldy));
            if (Llen < 12.0)
            {
                continue;
            }

            Ldx /= Llen;
            Ldy /= Llen;
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                (double Rx1, double Ry1) = Right[RightIndex - 1];
                (double Rx2, double Ry2) = Right[RightIndex];
                double Rdx = Rx2 - Rx1;
                double Rdy = Ry2 - Ry1;
                double Rlen = Math.Sqrt((Rdx * Rdx) + (Rdy * Rdy));
                if (Rlen < 12.0)
                {
                    continue;
                }

                Rdx /= Rlen;
                Rdy /= Rlen;
                double Dot = Math.Abs((Ldx * Rdx) + (Ldy * Rdy));
                if (Dot < 0.92)
                {
                    continue;
                }

                double Overlap = OverlapLength(Lx1, Ly1, Lx2, Ly2, Rx1, Ry1, Rx2, Ry2, Ldx, Ldy);
                if (Overlap < 10.0)
                {
                    continue;
                }

                double MidX = (Rx1 + Rx2) / 2.0;
                double MidY = (Ry1 + Ry2) / 2.0;
                double Distance = PointToSegmentDistance(MidX, MidY, Lx1, Ly1, Lx2, Ly2);
                if (Distance < Gap)
                {
                    Gap = Distance;
                    ShiftX = -Ldy;
                    ShiftY = Ldx;
                }
            }
        }

        return !double.IsPositiveInfinity(Gap);
    }

    private static bool PolylinesCross(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                if (SegmentsCross(
                    Left[LeftIndex - 1].X,
                    Left[LeftIndex - 1].Y,
                    Left[LeftIndex].X,
                    Left[LeftIndex].Y,
                    Right[RightIndex - 1].X,
                    Right[RightIndex - 1].Y,
                    Right[RightIndex].X,
                    Right[RightIndex].Y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsCross(
        double Ax,
        double Ay,
        double Bx,
        double By,
        double Cx,
        double Cy,
        double Dx,
        double Dy)
    {
        double Denominator = ((Bx - Ax) * (Dy - Cy)) - ((By - Ay) * (Dx - Cx));
        if (Math.Abs(Denominator) < 1e-9)
        {
            return false;
        }

        double Ua = (((Cx - Ax) * (Dy - Cy)) - ((Cy - Ay) * (Dx - Cx))) / Denominator;
        double Ub = (((Cx - Ax) * (By - Ay)) - ((Cy - Ay) * (Bx - Ax))) / Denominator;
        return Ua > 0.02 && Ua < 0.98 && Ub > 0.02 && Ub < 0.98;
    }

    private static void AddPortUse(
        Dictionary<string, List<PortUse>> UsesByNode,
        string NodeId,
        FlowchartResolvedGuide Guide,
        bool AtSource)
    {
        (double X, double Y) Point = AtSource ? Guide.Points[0] : Guide.Points[Guide.Points.Count - 1];
        if (!UsesByNode.TryGetValue(NodeId, out List<PortUse>? Uses))
        {
            Uses = [];
            UsesByNode[NodeId] = Uses;
        }

        Uses.Add(new PortUse
        {
            Guide = Guide,
            AtSource = AtSource,
            X = Point.X,
            Y = Point.Y,
            Face = PortFace.Bottom,
            Marker = AtSource ? Guide.Edge.MarkerStart : Guide.Edge.MarkerEnd
        });
    }

    private static void ReplaceEndpoint(FlowchartResolvedGuide Guide, bool AtSource, double X, double Y)
    {
        if (Guide.Points.Count < 2)
        {
            return;
        }

        if (AtSource)
        {
            Guide.Points[0] = (X, Y);
            return;
        }

        Guide.Points[Guide.Points.Count - 1] = (X, Y);
    }

    private static void ShiftInteriorPoints(FlowchartResolvedGuide Guide, double ShiftX, double ShiftY)
    {
        if (Guide.Points.Count < 3)
        {
            return;
        }

        for (int Index = 1; Index < Guide.Points.Count - 1; Index++)
        {
            (double X, double Y) = Guide.Points[Index];
            Guide.Points[Index] = (X + ShiftX, Y + ShiftY);
        }
    }

    private static PortFace ClassifyFace(FlowchartLayoutNodeBounds Bounds, double X, double Y)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double DistLeft = Math.Abs(X - Left);
        double DistRight = Math.Abs(X - Right);
        double DistTop = Math.Abs(Y - Top);
        double DistBottom = Math.Abs(Y - Bottom);
        double Best = DistBottom;
        PortFace Face = PortFace.Bottom;
        if (DistTop < Best)
        {
            Best = DistTop;
            Face = PortFace.Top;
        }

        if (DistLeft < Best)
        {
            Best = DistLeft;
            Face = PortFace.Left;
        }

        if (DistRight < Best)
        {
            Face = PortFace.Right;
        }

        return Face;
    }

    private static PortFace PreferFaceToward(FlowchartLayoutNodeBounds From, FlowchartLayoutNodeBounds Toward)
    {
        double Dx = Toward.CenterX - From.CenterX;
        double Dy = Toward.CenterY - From.CenterY;
        if (Math.Abs(Dx) > Math.Abs(Dy))
        {
            return Dx >= 0.0 ? PortFace.Right : PortFace.Left;
        }

        return Dy >= 0.0 ? PortFace.Top : PortFace.Bottom;
    }

    private static PortFace AdjacentFace(PortFace Face)
    {
        return Face switch
        {
            PortFace.Top => PortFace.Bottom,
            PortFace.Bottom => PortFace.Top,
            PortFace.Left => PortFace.Right,
            _ => PortFace.Left
        };
    }

    private static bool FaceIsHorizontal(PortFace Face)
    {
        return Face == PortFace.Top || Face == PortFace.Bottom;
    }

    private static (double X, double Y) FacePort(
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        PortFace Face,
        EdgeEndpointMarker Marker)
    {
        double Slot = FaceIsHorizontal(Face) ? Bounds.CenterX : Bounds.CenterY;
        return FacePortAtSlot(Bounds, Shape, Face, Slot, Marker);
    }

    private static (double X, double Y) FacePortAtSlot(
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        PortFace Face,
        double Slot,
        EdgeEndpointMarker Marker)
    {
        double DirX;
        double DirY;
        switch (Face)
        {
            case PortFace.Top:
                DirX = Slot - Bounds.CenterX;
                DirY = Bounds.Height / 2.0;
                break;
            case PortFace.Bottom:
                DirX = Slot - Bounds.CenterX;
                DirY = -Bounds.Height / 2.0;
                break;
            case PortFace.Left:
                DirX = -Bounds.Width / 2.0;
                DirY = Slot - Bounds.CenterY;
                break;
            default:
                DirX = Bounds.Width / 2.0;
                DirY = Slot - Bounds.CenterY;
                break;
        }

        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            DirX = 0.0;
            DirY = -1.0;
            Length = 1.0;
        }

        DirX /= Length;
        DirY /= Length;
        (double X, double Y) = FlowchartShapeLayoutHelper.RayExitShape(
            Bounds.CenterX,
            Bounds.CenterY,
            Bounds.Width,
            Bounds.Height,
            Shape,
            DirX,
            DirY);
        double Gap = FlowchartEdgeAttachConstants.GetExteriorGapPx(Marker);
        return (X + (DirX * Gap), Y + (DirY * Gap));
    }

    private static string? ResolveShape(
        DiagramContract Contract,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        string NodeId)
    {
        if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, NodeId))
        {
            return "rect";
        }

        return NodeById.TryGetValue(NodeId, out NodeContract? Node) ? Node.Shape : "rect";
    }

    private static bool IsEndpointOrAncestorGroup(DiagramContract Contract, EdgeContract Edge, string NodeId)
    {
        if (string.Equals(NodeId, Edge.SourceId, StringComparison.Ordinal)
            || string.Equals(NodeId, Edge.TargetId, StringComparison.Ordinal))
        {
            return true;
        }

        string? SourceGroup = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.SourceId, Contract);
        string? TargetGroup = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.TargetId, Contract);
        return IsSameOrAncestor(Contract, NodeId, SourceGroup) || IsSameOrAncestor(Contract, NodeId, TargetGroup);
    }

    private static bool IsSameOrAncestor(DiagramContract Contract, string CandidateGroupId, string? MemberGroupId)
    {
        string? Cursor = MemberGroupId;
        while (!string.IsNullOrEmpty(Cursor))
        {
            if (string.Equals(Cursor, CandidateGroupId, StringComparison.Ordinal))
            {
                return true;
            }

            string? Parent = null;
            foreach (GroupContract Group in Contract.Groups)
            {
                if (string.Equals(Group.Id, Cursor, StringComparison.Ordinal))
                {
                    Parent = Group.ParentGroupId;
                    break;
                }
            }

            Cursor = Parent;
        }

        return false;
    }

    private static bool IsMemberOfGroup(DiagramContract Contract, string NodeId, string GroupId)
    {
        if (string.Equals(NodeId, GroupId, StringComparison.Ordinal))
        {
            return true;
        }

        return IsSameOrAncestor(
            Contract,
            GroupId,
            FlowchartSubgraphMembershipHelper.FindInnermostGroupId(NodeId, Contract));
    }

    private static bool SharesEndpoint(EdgeContract Left, EdgeContract Right)
    {
        return string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.SourceId, Right.TargetId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal);
    }

    private static bool LeavesContentBox(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<(double X, double Y)> Samples)
    {
        double MinX = double.PositiveInfinity;
        double MaxX = double.NegativeInfinity;
        double MinY = double.PositiveInfinity;
        double MaxY = double.NegativeInfinity;
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            MinX = Math.Min(MinX, Bounds.CenterX - (Bounds.Width / 2.0));
            MaxX = Math.Max(MaxX, Bounds.CenterX + (Bounds.Width / 2.0));
            MinY = Math.Min(MinY, Bounds.CenterY - (Bounds.Height / 2.0));
            MaxY = Math.Max(MaxY, Bounds.CenterY + (Bounds.Height / 2.0));
        }

        MinX -= 24.0;
        MaxX += 24.0;
        MinY -= 24.0;
        MaxY += 24.0;
        foreach ((double X, double Y) in Samples)
        {
            if (X < MinX || X > MaxX || Y < MinY || Y > MaxY)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNodeFullyEnclosingCorridor(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge,
        string NodeId)
    {
        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds Source)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds Target)
            || !BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Obstacle))
        {
            return false;
        }

        double ObstacleLeft = Obstacle.CenterX - (Obstacle.Width / 2.0);
        double ObstacleRight = Obstacle.CenterX + (Obstacle.Width / 2.0);
        double ObstacleTop = Obstacle.CenterY + (Obstacle.Height / 2.0);
        double ObstacleBottom = Obstacle.CenterY - (Obstacle.Height / 2.0);
        double CorridorLeft = Math.Min(Source.CenterX, Target.CenterX);
        double CorridorRight = Math.Max(Source.CenterX, Target.CenterX);
        double CorridorBottom = Math.Min(Source.CenterY, Target.CenterY);
        double CorridorTop = Math.Max(Source.CenterY, Target.CenterY);
        bool BlocksVertical = ObstacleLeft <= CorridorLeft
            && ObstacleRight >= CorridorRight
            && ObstacleTop < CorridorTop
            && ObstacleBottom > CorridorBottom;
        bool BlocksHorizontal = ObstacleBottom <= CorridorBottom
            && ObstacleTop >= CorridorTop
            && ObstacleLeft > CorridorLeft
            && ObstacleRight < CorridorRight;
        return BlocksVertical || BlocksHorizontal;
    }

    private static bool ObstacleBlocksRoute(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds Obstacle)
    {
        if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
            StartX,
            StartY,
            EndX,
            EndY,
            Obstacle,
            -CURVE_BOW_CLEARANCE_PX))
        {
            return true;
        }

        double MinX = Math.Min(StartX, EndX) - CURVE_BOW_CLEARANCE_PX;
        double MaxX = Math.Max(StartX, EndX) + CURVE_BOW_CLEARANCE_PX;
        double MinY = Math.Min(StartY, EndY) - CURVE_BOW_CLEARANCE_PX;
        double MaxY = Math.Max(StartY, EndY) + CURVE_BOW_CLEARANCE_PX;
        double Left = Obstacle.CenterX - (Obstacle.Width / 2.0);
        double Right = Obstacle.CenterX + (Obstacle.Width / 2.0);
        double Top = Obstacle.CenterY + (Obstacle.Height / 2.0);
        double Bottom = Obstacle.CenterY - (Obstacle.Height / 2.0);
        return Right >= MinX && Left <= MaxX && Top >= MinY && Bottom <= MaxY;
    }

    private static bool AreAxisAligned((double X, double Y) Start, (double X, double Y) End)
    {
        return Math.Abs(Start.X - End.X) <= 1.0 || Math.Abs(Start.Y - End.Y) <= 1.0;
    }

    private static List<(double X, double Y)> CopyPoints(IReadOnlyList<(double X, double Y)> Points)
    {
        List<(double X, double Y)> Copy = [];
        foreach ((double X, double Y) Point in Points)
        {
            Copy.Add(Point);
        }

        return Copy;
    }

    private static double PathLength(IReadOnlyList<(double X, double Y)> Samples)
    {
        double Length = 0.0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            Length += Distance(Samples[Index - 1].X, Samples[Index - 1].Y, Samples[Index].X, Samples[Index].Y);
        }

        return Length;
    }

    private static double Distance(double X1, double Y1, double X2, double Y2)
    {
        double Dx = X2 - X1;
        double Dy = Y2 - Y1;
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }

    private static double DistanceToVerticalBorder(double X, double Left, double Right)
    {
        return Math.Min(Math.Abs(X - Left), Math.Abs(X - Right));
    }

    private static double DistanceToHorizontalBorder(double Y, double Top, double Bottom)
    {
        return Math.Min(Math.Abs(Y - Top), Math.Abs(Y - Bottom));
    }

    private static double OverlapLength(
        double Ax,
        double Ay,
        double Bx,
        double By,
        double Cx,
        double Cy,
        double Dx,
        double Dy,
        double DirX,
        double DirY)
    {
        double A = (Ax * DirX) + (Ay * DirY);
        double B = (Bx * DirX) + (By * DirY);
        double C = (Cx * DirX) + (Cy * DirY);
        double D = (Dx * DirX) + (Dy * DirY);
        double Left = Math.Max(Math.Min(A, B), Math.Min(C, D));
        double Right = Math.Min(Math.Max(A, B), Math.Max(C, D));
        return Math.Max(0.0, Right - Left);
    }

    private static double PointToSegmentDistance(
        double Px,
        double Py,
        double Ax,
        double Ay,
        double Bx,
        double By)
    {
        double Dx = Bx - Ax;
        double Dy = By - Ay;
        double LengthSquared = (Dx * Dx) + (Dy * Dy);
        if (LengthSquared < 1e-9)
        {
            return Distance(Px, Py, Ax, Ay);
        }

        double T = (((Px - Ax) * Dx) + ((Py - Ay) * Dy)) / LengthSquared;
        T = Math.Max(0.0, Math.Min(1.0, T));
        return Distance(Px, Py, Ax + (Dx * T), Ay + (Dy * T));
    }

    private static int CompareGuideLength(FlowchartResolvedGuide Left, FlowchartResolvedGuide Right)
    {
        return PathLength(Left.Points).CompareTo(PathLength(Right.Points));
    }

    private enum PortFace
    {
        Top,
        Bottom,
        Left,
        Right
    }

    private sealed class CandidateRoute
    {
        public CandidateRoute(List<(double X, double Y)> Points, FlowchartEdgeRouteKind RouteKind)
        {
            this.Points = Points;
            this.RouteKind = RouteKind;
        }

        public List<(double X, double Y)> Points { get; }

        public FlowchartEdgeRouteKind RouteKind { get; }
    }

    private sealed class PortUse
    {
        public FlowchartResolvedGuide Guide { get; set; } = null!;

        public bool AtSource { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public PortFace Face { get; set; }

        public EdgeEndpointMarker Marker { get; set; }
    }
}
