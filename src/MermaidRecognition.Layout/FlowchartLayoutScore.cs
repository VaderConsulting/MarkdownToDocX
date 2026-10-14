using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Measures a routed layout using rules 2.13 and 2.14.</summary>
internal static class FlowchartLayoutScore
{
    internal static double BEND_PENALTY => FlowchartLayoutRules.BendScore;

    internal sealed record Result(double Width, double Height, int Overlaps, int Crossings, int ForcedMinimum,
        int Bends, int Disconnections, int HintsHonoured, int HintsTotal, double EmptyShare, double? Score,
        double OptimizationScore, IReadOnlyList<FlowchartLayoutQualityFailure> Violations);

    internal static Result Measure(DiagramContract Contract, FlowchartLayoutResult Layout,
        bool IncludeArrangementCheck = true)
        => MeasureCore(Contract, Layout, IncludeArrangementCheck, null, null);

    private static Result MeasureCore(DiagramContract Contract, FlowchartLayoutResult Layout,
        bool IncludeArrangementCheck, int? Crossings, int? Bends,
        IReadOnlyList<FlowchartLayoutQualityFailure>? CachedViolations = null,
        int? ForcedMinimumOverride = null, int? DisconnectionsOverride = null,
        int? SharedEndpointCrossings = null)
    {
        (double Width, double Height) = Bounds(Layout);
        var Violations = CachedViolations == null
            ? new List<FlowchartLayoutQualityFailure>()
            : new List<FlowchartLayoutQualityFailure>(CachedViolations);
        if (CachedViolations == null)
            DiagramGridQuality.Collect(Contract, Layout, Violations, IncludeArrangementCheck);
        int Overlaps = Violations.Count(Failure => Failure.Kind == "NodeOverlap");
        int CrossingCount = Crossings ?? CountCrossings(Layout);
        int SharedEndpointCrossingCount = SharedEndpointCrossings ?? CountSharedEndpointCrossings(Layout);
        int ForcedMinimum = ForcedMinimumOverride ?? ForcedCrossingMinimum(Contract, Layout);
        int BendCount = Bends ?? CountBends(Layout);
        int Disconnections = DisconnectionsOverride ?? CountDisconnections(Contract, Layout);
        (int HintsHonoured, int HintsTotal, double HintScore) = MeasureHints(Contract, Layout);
        double EmptyShare = EmptyAreaShare(Layout, Width, Height);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract)
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
        bool Rejected = Overlaps > 0 || Violations.Any(Failure => Rejects(Failure, Contract, Layout, Back));
        double OptimizationScore = Calculate(Width, Height, CrossingCount, ForcedMinimum, EmptyShare)
            + SharedEndpointCrossingCount * FlowchartLayoutRules.CrossingScore
            + BendCount * FlowchartLayoutRules.BendScore
            + Disconnections * FlowchartLayoutRules.DisconnectedEndScore + HintScore;
        double? Score = Rejected ? null : OptimizationScore;
        return new(Width, Height, Overlaps, CrossingCount, ForcedMinimum, BendCount, Disconnections,
            HintsHonoured, HintsTotal, EmptyShare, Score, OptimizationScore, Violations);
    }

    /// <summary>
    /// Re-scores a candidate route while reusing route-pair crossing and per-route bend
    /// totals from the current pass. Call CommitCurrent after accepting a candidate.
    /// </summary>
    internal sealed class IncrementalContext
    {
        private readonly DiagramContract _contract;
        private readonly FlowchartLayoutResult _layout;
        private readonly bool _includeArrangementCheck;
        private readonly FlowchartLayoutEdgeRoute[] _routes;
        private readonly int[] _routeCrossings;
        private readonly int[] _routeBends;
        private readonly int[,] _pairCrossings;
        private readonly int[,] _sharedEndpointPairCrossings;
        private readonly IReadOnlyList<FlowchartLayoutQualityFailure> _staticViolations;
        private readonly IReadOnlyList<FlowchartLayoutQualityFailure>[] _routeViolations;
        private readonly Dictionary<string, FlowchartLayoutNodeBounds> _disconnectBounds;
        private readonly Dictionary<string, string?> _disconnectShapes;
        private readonly Dictionary<string, EdgeContract> _disconnectEdges;
        private readonly int[] _routeDisconnections;
        private readonly int _forcedMinimum;
        private int _crossings;
        private int _sharedEndpointCrossings;
        private int _bends;
        private int _disconnections;

        internal IncrementalContext(DiagramContract Contract, FlowchartLayoutResult Layout,
            bool IncludeArrangementCheck = true)
        {
            _contract = Contract;
            _layout = Layout;
            _includeArrangementCheck = IncludeArrangementCheck;
            _routes = Layout.EdgeRoutes.ToArray();
            _routeCrossings = new int[_routes.Length];
            _routeBends = new int[_routes.Length];
            _pairCrossings = new int[_routes.Length, _routes.Length];
            _sharedEndpointPairCrossings = new int[_routes.Length, _routes.Length];
            (_disconnectBounds, _disconnectShapes, _disconnectEdges) = DisconnectionIndexes(Contract, Layout);
            _routeDisconnections = new int[_routes.Length];
            _forcedMinimum = ForcedCrossingMinimum(Contract, Layout);
            var InitialViolations = new List<FlowchartLayoutQualityFailure>();
            DiagramGridQuality.Collect(Contract, Layout, InitialViolations, IncludeArrangementCheck);
            _staticViolations = InitialViolations.Where(Failure => !IsRouteFault(Failure)).ToArray();
            _routeViolations = new IReadOnlyList<FlowchartLayoutQualityFailure>[_routes.Length];
            for (int Index = 0; Index < _routes.Length; Index++)
            {
                var RouteFailures = new List<FlowchartLayoutQualityFailure>();
                DiagramGridQuality.CollectRouteFailures(Layout, _routes[Index], RouteFailures);
                _routeViolations[Index] = RouteFailures;
            }
            for (int Left = 0; Left < _routes.Length; Left++)
            {
                _routeBends[Left] = CountCorners(_routes[Left].Points);
                _routeDisconnections[Left] = CountRouteDisconnections(_routes[Left],
                    _disconnectBounds, _disconnectShapes, _disconnectEdges);
                for (int Right = Left + 1; Right < _routes.Length; Right++)
                {
                    int Pair = PairCrossings(_routes[Left], _routes[Right]);
                    _pairCrossings[Left, Right] = Pair;
                    _pairCrossings[Right, Left] = Pair;
                    int SharedEndpointPair = PairSharedEndpointCrossings(_routes[Left], _routes[Right]);
                    _sharedEndpointPairCrossings[Left, Right] = SharedEndpointPair;
                    _sharedEndpointPairCrossings[Right, Left] = SharedEndpointPair;
                    _sharedEndpointCrossings += SharedEndpointPair;
                    _routeCrossings[Left] += Pair;
                    _routeCrossings[Right] += Pair;
                }
            }
            _crossings = _routeCrossings.Sum() / 2;
            _bends = _routeBends.Sum();
            _disconnections = _routeDisconnections.Sum();
        }

        internal Result MeasureCandidate(FlowchartLayoutEdgeRoute ChangedRoute)
        {
            int Index = FindRoute(ChangedRoute);
            int CandidateCrossings = _crossings - _routeCrossings[Index]
                + CountRouteCrossings(Index, ChangedRoute);
            int CandidateBends = _bends - _routeBends[Index] + CountCorners(ChangedRoute.Points);
            int CandidateDisconnections = _disconnections - _routeDisconnections[Index]
                + CountRouteDisconnections(ChangedRoute, _disconnectBounds, _disconnectShapes, _disconnectEdges);
            int CandidateSharedEndpointCrossings = _sharedEndpointCrossings
                - CountRouteSharedEndpointCrossings(Index)
                + CountRouteSharedEndpointCrossings(Index, ChangedRoute);
            if (_includeArrangementCheck)
                return MeasureCore(_contract, _layout, true, CandidateCrossings, CandidateBends,
                    ForcedMinimumOverride: _forcedMinimum, DisconnectionsOverride: CandidateDisconnections,
                    SharedEndpointCrossings: CandidateSharedEndpointCrossings);

            var Violations = new List<FlowchartLayoutQualityFailure>(_staticViolations);
            for (int RouteIndex = 0; RouteIndex < _routes.Length; RouteIndex++)
            {
                if (RouteIndex == Index)
                    DiagramGridQuality.CollectRouteFailures(_layout, ChangedRoute, Violations);
                else
                    Violations.AddRange(_routeViolations[RouteIndex]);
            }
            return MeasureCore(_contract, _layout, _includeArrangementCheck,
                CandidateCrossings, CandidateBends, Violations, _forcedMinimum, CandidateDisconnections,
                CandidateSharedEndpointCrossings);
        }

        internal void CommitCurrent(FlowchartLayoutEdgeRoute ChangedRoute)
        {
            int Index = FindRoute(ChangedRoute);
            int NewRouteCrossings = 0;
            for (int OtherIndex = 0; OtherIndex < _routes.Length; OtherIndex++)
            {
                if (OtherIndex == Index) continue;
                int NewPair = PairCrossings(ChangedRoute, _routes[OtherIndex]);
                int Delta = NewPair - _pairCrossings[Index, OtherIndex];
                _crossings += Delta;
                _routeCrossings[OtherIndex] += Delta;
                _pairCrossings[Index, OtherIndex] = NewPair;
                _pairCrossings[OtherIndex, Index] = NewPair;
                NewRouteCrossings += NewPair;
                int NewSharedPair = PairSharedEndpointCrossings(ChangedRoute, _routes[OtherIndex]);
                _sharedEndpointCrossings += NewSharedPair - _sharedEndpointPairCrossings[Index, OtherIndex];
                _sharedEndpointPairCrossings[Index, OtherIndex] = NewSharedPair;
                _sharedEndpointPairCrossings[OtherIndex, Index] = NewSharedPair;
            }
            _routeCrossings[Index] = NewRouteCrossings;
            _bends += CountCorners(ChangedRoute.Points) - _routeBends[Index];
            _routeBends[Index] = CountCorners(ChangedRoute.Points);
            int NewDisconnections = CountRouteDisconnections(ChangedRoute,
                _disconnectBounds, _disconnectShapes, _disconnectEdges);
            _disconnections += NewDisconnections - _routeDisconnections[Index];
            _routeDisconnections[Index] = NewDisconnections;
            var UpdatedFaults = new List<FlowchartLayoutQualityFailure>();
            DiagramGridQuality.CollectRouteFailures(_layout, ChangedRoute, UpdatedFaults);
            _routeViolations[Index] = UpdatedFaults;
        }

        private int FindRoute(FlowchartLayoutEdgeRoute Route)
        {
            for (int Index = 0; Index < _routes.Length; Index++)
                if (ReferenceEquals(_routes[Index], Route)) return Index;
            throw new ArgumentException("The changed route is not part of this score context.", nameof(Route));
        }

        private int CountRouteCrossings(int ChangedIndex, FlowchartLayoutEdgeRoute ChangedRoute)
        {
            int Result = 0;
            for (int OtherIndex = 0; OtherIndex < _routes.Length; OtherIndex++)
                if (OtherIndex != ChangedIndex)
                    Result += PairCrossings(ChangedRoute, _routes[OtherIndex]);
            return Result;
        }

        private int CountRouteSharedEndpointCrossings(int ChangedIndex)
        {
            int Result = 0;
            for (int OtherIndex = 0; OtherIndex < _routes.Length; OtherIndex++)
                if (OtherIndex != ChangedIndex)
                    Result += _sharedEndpointPairCrossings[ChangedIndex, OtherIndex];
            return Result;
        }

        private int CountRouteSharedEndpointCrossings(int ChangedIndex, FlowchartLayoutEdgeRoute ChangedRoute)
        {
            int Result = 0;
            for (int OtherIndex = 0; OtherIndex < _routes.Length; OtherIndex++)
                if (OtherIndex != ChangedIndex)
                    Result += PairSharedEndpointCrossings(ChangedRoute, _routes[OtherIndex]);
            return Result;
        }

        private static bool IsRouteFault(FlowchartLayoutQualityFailure Failure) =>
            Failure.Kind == "LineShape"
                || Failure.Kind == "WholePixel" && Failure.Description.Contains("->", StringComparison.Ordinal);
    }

    private static int PairCrossings(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B)
    {
        if (A.SourceId == B.SourceId || A.SourceId == B.TargetId
            || A.TargetId == B.SourceId || A.TargetId == B.TargetId) return 0;
        return FlowchartBranchRouteOrder.Intersections(
            A.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
            B.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
    }

    private static int CountSharedEndpointCrossings(FlowchartLayoutResult Layout)
    {
        int Result = 0;
        for (int Left = 0; Left < Layout.EdgeRoutes.Count; Left++)
        for (int Right = Left + 1; Right < Layout.EdgeRoutes.Count; Right++)
            Result += PairSharedEndpointCrossings(Layout.EdgeRoutes[Left], Layout.EdgeRoutes[Right]);
        return Result;
    }

    private static int PairSharedEndpointCrossings(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B)
    {
        if (A.SourceId != B.SourceId && A.SourceId != B.TargetId
            && A.TargetId != B.SourceId && A.TargetId != B.TargetId)
            return 0;
        if (A.SourceId == B.SourceId && A.TargetId == B.TargetId) return 0;
        return FlowchartBranchRouteOrder.Intersections(
            A.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
            B.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
    }

    private static bool Rejects(FlowchartLayoutQualityFailure Failure, DiagramContract Contract,
        FlowchartLayoutResult Layout,
        IReadOnlySet<(string SourceId, string TargetId)> Back)
    {
        if (Failure.Kind is "NodeOverlap" or "ArrangementKept") return false;
        if (Failure.Kind != "LineShape" || !Failure.Description.Contains("more than two corners", StringComparison.Ordinal)
            || Failure.InvolvedIds.Count < 2) return true;
        if (Contract.Edges.Any(Edge => Edge.SourceId == Failure.InvolvedIds[0]
            && Edge.TargetId == Failure.InvolvedIds[1] && Edge.RoutingHint?.IsActive == true)) return false;
        if (Contract.Groups.Any(Group => Group.Id == Failure.InvolvedIds[0]
            || Group.Id == Failure.InvolvedIds[1])) return false;
        if (!Back.Contains((Failure.InvolvedIds[0], Failure.InvolvedIds[1]))) return true;
        FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.FirstOrDefault(Edge =>
            Edge.SourceId == Failure.InvolvedIds[0] && Edge.TargetId == Failure.InvolvedIds[1]);
        if (Route == null) return true;
        int Corners = 0;
        for (int Index = 1; Index < Route.Points.Count - 1; Index++)
        {
            bool BeforeHorizontal = Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y) < 0.5;
            bool AfterHorizontal = Math.Abs(Route.Points[Index + 1].Y - Route.Points[Index].Y) < 0.5;
            if (BeforeHorizontal != AfterHorizontal) Corners++;
        }
        return Corners > 3;
    }

    internal static double Calculate(double Width, double Height, int Crossings, double EmptyShare)
        => Calculate(Width, Height, Crossings, 0, EmptyShare);

    internal static double Calculate(double Width, double Height, int Crossings, int ForcedMinimum, double EmptyShare)
    {
        double TargetRatio = FlowchartLayoutRules.TargetAspectWidth / FlowchartLayoutRules.TargetAspectHeight;
        double ShapePercent = Math.Abs((Width / Math.Max(1.0, Height)) - TargetRatio) / TargetRatio * 100.0;
        double Shape = ShapePercent / FlowchartLayoutRules.ShapeStep * FlowchartLayoutRules.ShapePoints;
        double EmptyPercent = EmptyShare * 100.0;
        double EmptyLowRate = FlowchartLayoutRules.EmptyPointsLow / FlowchartLayoutRules.ShapeStep;
        double EmptyHighRate = FlowchartLayoutRules.EmptyPointsHigh / FlowchartLayoutRules.ShapeStep;
        double Empty = EmptyPercent <= FlowchartLayoutRules.EmptyThreshold
            ? EmptyPercent * EmptyLowRate
            : FlowchartLayoutRules.EmptyThreshold * EmptyLowRate
                + (EmptyPercent - FlowchartLayoutRules.EmptyThreshold) * EmptyHighRate;
        return Shape + Empty + Math.Max(0, Crossings - ForcedMinimum) * FlowchartLayoutRules.CrossingScore;
    }

    internal static int ForcedCrossingMinimum(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        var Bounds = Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        bool Horizontal = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        double Rank(FlowchartLayoutNodeBounds Box) => Math.Round(Horizontal ? Box.CenterX : Box.CenterY);
        List<double> Ranks = Bounds.Values.Select(Rank).Distinct().Order().ToList();
        var RankIndex = Ranks.Select((Value, Index) => (Value, Index)).ToDictionary(Item => Item.Value, Item => Item.Index);
        var Pairs = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                && !Edge.Metadata.ContainsKey("enterprisePlainConnection")
                && Bounds.ContainsKey(Edge.SourceId) && Bounds.ContainsKey(Edge.TargetId)
                && Math.Abs(RankIndex[Rank(Bounds[Edge.SourceId])] - RankIndex[Rank(Bounds[Edge.TargetId])]) == 1)
            .Select(Edge => (Edge.SourceId, Edge.TargetId, SourceRank: Rank(Bounds[Edge.SourceId]),
                TargetRank: Rank(Bounds[Edge.TargetId])))
            .Distinct().GroupBy(Edge => (Edge.SourceRank, Edge.TargetRank));
        int Result = 0;
        foreach (var RankPair in Pairs)
        {
            var Edges = RankPair.Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
            var RemainingSources = Edges.Select(Edge => Edge.SourceId).ToHashSet(StringComparer.Ordinal);
            while (RemainingSources.Count > 0)
            {
                string Seed = RemainingSources.Order(StringComparer.Ordinal).First();
                var Sources = new HashSet<string>(StringComparer.Ordinal) { Seed };
                var Targets = new HashSet<string>(StringComparer.Ordinal);
                bool Changed;
                do
                {
                    Changed = false;
                    foreach ((string SourceId, string TargetId) in Edges)
                    {
                        if (Sources.Contains(SourceId) && Targets.Add(TargetId)) Changed = true;
                        if (Targets.Contains(TargetId) && Sources.Add(SourceId)) Changed = true;
                    }
                } while (Changed);
                RemainingSources.ExceptWith(Sources);
                if (Sources.Count < 2 || Targets.Count < 2) continue;
                int Present = Edges.Count(Edge => Sources.Contains(Edge.SourceId) && Targets.Contains(Edge.TargetId));
                if (Present != Sources.Count * Targets.Count) continue;
                Result += ChooseTwo(Sources.Count) * ChooseTwo(Targets.Count);
            }
        }
        return Result;
    }

    private static int ChooseTwo(int Count) => Count * (Count - 1) / 2;

    private static (double Width, double Height) Bounds(FlowchartLayoutResult Layout)
    {
        var X = new List<double>();
        var Y = new List<double>();
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
        {
            X.Add(Box.CenterX - Box.Width / 2.0); X.Add(Box.CenterX + Box.Width / 2.0);
            Y.Add(Box.CenterY - Box.Height / 2.0); Y.Add(Box.CenterY + Box.Height / 2.0);
        }
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        foreach (FlowchartLayoutPoint Point in Route.Points)
        {
            X.Add(Point.X); Y.Add(Point.Y);
        }
        return X.Count == 0 ? (0.0, 0.0) : (X.Max() - X.Min(), Y.Max() - Y.Min());
    }

    internal static int CountCrossings(FlowchartLayoutResult Layout)
    {
        // A candidate score compares every route pair. Convert each route once
        // here instead of allocating two point lists for every pair.
        var Routes = Layout.EdgeRoutes
            .Select(Route => (Route.SourceId, Route.TargetId,
                Points: Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()))
            .ToArray();
        int Result = 0;
        for (int Left = 0; Left < Routes.Length; Left++)
        for (int Right = Left + 1; Right < Routes.Length; Right++)
        {
            var A = Routes[Left];
            var B = Routes[Right];
            if (A.SourceId == B.SourceId || A.SourceId == B.TargetId
                || A.TargetId == B.SourceId || A.TargetId == B.TargetId) continue;
            Result += FlowchartBranchRouteOrder.Intersections(A.Points, B.Points).Crossings;
        }
        return Result;
    }

    private static int CountBends(FlowchartLayoutResult Layout) =>
        Layout.EdgeRoutes.Sum(Route => CountCorners(Route.Points));

    private static int CountDisconnections(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        var (Bounds, Shapes, Edges) = DisconnectionIndexes(Contract, Layout);
        return Layout.EdgeRoutes.Sum(Route =>
            CountRouteDisconnections(Route, Bounds, Shapes, Edges));
    }

    private static (Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
        Dictionary<string, string?> Shapes, Dictionary<string, EdgeContract> Edges)
        DisconnectionIndexes(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds
            .Concat(Layout.ClusterBounds)
            .GroupBy(Box => Box.NodeId, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        Dictionary<string, string?> Shapes = Contract.Nodes
            .GroupBy(Node => Node.Id, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First().Shape, StringComparer.Ordinal);
        Dictionary<string, EdgeContract> Edges = Contract.Edges
            .Where(Edge => !string.IsNullOrEmpty(Edge.Id))
            .GroupBy(Edge => Edge.Id!, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        return (Bounds, Shapes, Edges);
    }

    private static int CountRouteDisconnections(FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, string?> Shapes,
        IReadOnlyDictionary<string, EdgeContract> Edges)
    {
        int Result = 0;
        if (Route.Points.Count < 2 || !Edges.TryGetValue(Route.EdgeId, out EdgeContract? Edge)
            || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source))
        {
            Shapes.TryGetValue(Route.SourceId, out string? Shape);
            if (!EndpointAttached(Route.Points[0], Route.Points[1], Source, Shape, Edge.MarkerStart))
                Result++;
        }

        if (Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
        {
            Shapes.TryGetValue(Route.TargetId, out string? Shape);
            if (!EndpointAttached(Route.Points[^1], Route.Points[^2], Target, Shape, Edge.MarkerEnd))
                Result++;
        }
        return Result;
    }

    private static bool EndpointAttached(FlowchartLayoutPoint Endpoint, FlowchartLayoutPoint Neighbour,
        FlowchartLayoutNodeBounds Bounds, string? Shape, EdgeEndpointMarker Marker)
    {
        bool BoxOutline = Shape is null or "" or "rect" or "rounded" or "round" or "stadium";
        if (BoxOutline && Marker != EdgeEndpointMarker.Cross)
        {
            double Left = Bounds.CenterX - Bounds.Width / 2.0;
            double Right = Bounds.CenterX + Bounds.Width / 2.0;
            double Bottom = Bounds.CenterY - Bounds.Height / 2.0;
            double Top = Bounds.CenterY + Bounds.Height / 2.0;
            const double BoxTolerance = 1.0;
            bool OnVertical = (Math.Abs(Endpoint.X - Left) <= BoxTolerance
                    || Math.Abs(Endpoint.X - Right) <= BoxTolerance)
                && Endpoint.Y >= Bottom - BoxTolerance && Endpoint.Y <= Top + BoxTolerance;
            bool OnHorizontal = (Math.Abs(Endpoint.Y - Bottom) <= BoxTolerance
                    || Math.Abs(Endpoint.Y - Top) <= BoxTolerance)
                && Endpoint.X >= Left - BoxTolerance && Endpoint.X <= Right + BoxTolerance;
            return OnVertical || OnHorizontal;
        }

        if (Shape is "circle" or "ellipse" && Marker == EdgeEndpointMarker.None)
        {
            double RadiusX = Shape == "circle" ? Math.Min(Bounds.Width, Bounds.Height) / 2.0 : Bounds.Width / 2.0;
            double RadiusY = Shape == "circle" ? Math.Min(Bounds.Width, Bounds.Height) / 2.0 : Bounds.Height / 2.0;
            if (RadiusX > 0.0 && RadiusY > 0.0)
            {
                double Dx = (Endpoint.X - Bounds.CenterX) / RadiusX;
                double Dy = (Endpoint.Y - Bounds.CenterY) / RadiusY;
                return Math.Abs(Dx * Dx + Dy * Dy - 1.0) <= 0.08;
            }
        }

        (double ExpectedX, double ExpectedY) = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds, Shape, Neighbour.X, Neighbour.Y, Marker);
        const double Tolerance = 1.0;
        return Math.Abs(Endpoint.X - ExpectedX) <= Tolerance
            && Math.Abs(Endpoint.Y - ExpectedY) <= Tolerance;
    }

    private static int CountCorners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Result = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            bool BeforeHorizontal = Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5;
            bool AfterHorizontal = Math.Abs(Points[Index + 1].Y - Points[Index].Y) < 0.5;
            if (BeforeHorizontal != AfterHorizontal) Result++;
        }
        return Result;
    }

    private static (int Honoured, int Total, double Score) MeasureHints(
        DiagramContract Contract,
        FlowchartLayoutResult Layout)
    {
        int Honoured = 0;
        int Total = 0;
        foreach (PlacementHintContract Hint in Contract.Nodes.Select(Node => Node.PlacementHint)
            .Concat(Contract.Groups.Select(Group => Group.PlacementHint)).OfType<PlacementHintContract>())
        {
            Total++;
            if (Hint.IsActive && Hint.UnhonouredReason == null) Honoured++;
        }

        foreach (EdgeContract Edge in Contract.Edges.Where(Edge => Edge.RoutingHint != null))
        {
            Total++;
            RoutingHintContract Hint = Edge.RoutingHint!;
            FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.FirstOrDefault(Item =>
                !string.IsNullOrEmpty(Edge.Id) && Item.EdgeId == Edge.Id
                || string.IsNullOrEmpty(Edge.Id) && Item.SourceId == Edge.SourceId && Item.TargetId == Edge.TargetId);
            bool Matches = Hint.IsActive && Hint.UnhonouredReason == null && Route != null;
            if (Matches && Hint.Leave.HasValue)
                Matches = Route!.ExitSide == DiagramGridLayout.HintSide(Hint.Leave.Value);
            if (Matches && Hint.Enter.HasValue)
                Matches = Route!.EntrySide == DiagramGridLayout.HintSide(Hint.Enter.Value);
            if (Matches && Hint.Travel.HasValue)
                Matches = DiagramGridRouteBuilder.HasTravelDirection(
                    Route!.Points.Select(Point => (Point.X, Point.Y)).ToList(), Hint.Travel.Value);
            if (Matches) Honoured++;
        }

        int PlacementHonoured = Contract.Nodes.Count(Node => Node.PlacementHint?.IsActive == true
                && Node.PlacementHint.UnhonouredReason == null)
            + Contract.Groups.Count(Group => Group.PlacementHint?.IsActive == true
                && Group.PlacementHint.UnhonouredReason == null);
        int LineHonoured = Honoured - PlacementHonoured;
        return (Honoured, Total,
            PlacementHonoured * FlowchartLayoutRules.NodeGroupHintScore
                + LineHonoured * FlowchartLayoutRules.LineHintScore);
    }

    private static double EmptyAreaShare(FlowchartLayoutResult Layout, double Width, double Height)
    {
        if (Width <= 0.0 || Height <= 0.0) return 0.0;
        const double Cell = DiagramGridMetrics.GRID_UNIT_PX;
        double Left = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .Min(Box => Box.CenterX - Box.Width / 2.0);
        double Bottom = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .Min(Box => Box.CenterY - Box.Height / 2.0);
        int Columns = Math.Max(1, (int)Math.Ceiling(Width / Cell));
        int Rows = Math.Max(1, (int)Math.Ceiling(Height / Cell));
        var Used = new HashSet<(int Column, int Row)>();
        void Mark(double MinX, double MinY, double MaxX, double MaxY)
        {
            int FirstColumn = (int)Math.Floor((MinX - Left) / Cell);
            int LastColumn = (int)Math.Floor((MaxX - Left) / Cell);
            int FirstRow = (int)Math.Floor((MinY - Bottom) / Cell);
            int LastRow = (int)Math.Floor((MaxY - Bottom) / Cell);
            for (int Column = FirstColumn; Column <= LastColumn; Column++)
            for (int Row = FirstRow; Row <= LastRow; Row++)
                if (Column >= 0 && Column < Columns && Row >= 0 && Row < Rows)
                    Used.Add((Column, Row));
        }
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
            Mark(Box.CenterX - Box.Width / 2.0, Box.CenterY - Box.Height / 2.0,
                Box.CenterX + Box.Width / 2.0, Box.CenterY + Box.Height / 2.0);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            FlowchartLayoutPoint A = Route.Points[Index - 1], B = Route.Points[Index];
            Mark(Math.Min(A.X, B.X) - Cell / 2.0, Math.Min(A.Y, B.Y) - Cell / 2.0,
                Math.Max(A.X, B.X) + Cell / 2.0, Math.Max(A.Y, B.Y) + Cell / 2.0);
        }
        return 1.0 - Used.Count / (double)(Columns * Rows);
    }
}
