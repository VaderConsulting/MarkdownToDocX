using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Measures a routed layout using rules 2.13 and 2.14.</summary>
internal static partial class FlowchartLayoutScore
{
    internal static double BEND_PENALTY => FlowchartLayoutRules.BendScore;

    internal sealed record Result(double Width, double Height, int Overlaps, int Crossings, int ForcedMinimum,
        int Bends, int Disconnections, int HintsHonoured, int HintsTotal, double EmptyShare, double? Score,
        double OptimizationScore, IReadOnlyList<FlowchartLayoutQualityFailure> Violations, ComparisonCost Cost);

    internal readonly record struct CandidateMeasurement(double? Score, double OptimizationScore,
        int Overlaps, int Crossings, int RejectingFaults, ComparisonCost Cost);

    internal static Result Measure(DiagramContract Contract, FlowchartLayoutResult Layout,
        bool IncludeArrangementCheck = true)
        => MeasureCore(Contract, Layout, IncludeArrangementCheck, null, null);

    private static Result MeasureCore(DiagramContract Contract, FlowchartLayoutResult Layout,
        bool IncludeArrangementCheck, int? Crossings, int? Bends,
        IReadOnlyList<FlowchartLayoutQualityFailure>? CachedViolations = null,
        int? ForcedMinimumOverride = null, int? DisconnectionsOverride = null,
        int? SharedEndpointCrossings = null, double? RouteLength = null)
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
        var Cost = new ComparisonCost(OptimizationScore,
            (RouteLength ?? Layout.EdgeRoutes.Sum(Route => DrawnLength(Route.Points)))
                / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RouteLengthWeight,
            BendCount, (Math.Max(0, CrossingCount - ForcedMinimum) + SharedEndpointCrossingCount)
                * FlowchartLayoutRules.CrossingScore);
        OptimizationScore = Cost;
        double? Score = Rejected ? null : OptimizationScore;
        return new(Width, Height, Overlaps, CrossingCount, ForcedMinimum, BendCount, Disconnections,
            HintsHonoured, HintsTotal, EmptyShare, Score, OptimizationScore, Violations, Cost);
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
        private readonly Dictionary<FlowchartLayoutEdgeRoute, int> _routeIndexes;
        private readonly (double X, double Y)[][] _routePoints;
        private readonly int[] _routeCrossings;
        private readonly int[] _routeSharedEndpointCrossings;
        private readonly int[] _routeBends;
        private readonly int[,] _pairCrossings;
        private readonly int[,] _sharedEndpointPairCrossings;
        private readonly IReadOnlyList<FlowchartLayoutQualityFailure> _staticViolations;
        private readonly IReadOnlyList<FlowchartLayoutQualityFailure>[] _routeViolations;
        private readonly Dictionary<string, FlowchartLayoutNodeBounds> _disconnectBounds;
        private readonly Dictionary<string, string?> _disconnectShapes;
        private readonly Dictionary<string, EdgeContract> _disconnectEdges;
        private readonly int[] _routeDisconnections;
        private readonly int[] _routeFaults;
        private readonly bool[] _routeRejected;
        private readonly HashSet<(string SourceId, string TargetId)> _backEdges;
        private readonly double _width;
        private readonly double _height;
        private readonly double _emptyShare;
        private readonly double _staticScore;
        private readonly int _staticFaults;
        private readonly int _staticOverlaps;
        private int _otherRouteFaults;
        private int _otherRejectedRoutes;
        private readonly bool _staticRejected;
        private readonly int _forcedMinimum;
        private int _crossings;
        private int _sharedEndpointCrossings;
        private int _bends;
        private int _disconnections;
        private readonly double[] _routeLengths;
        private double _totalLength;

        internal IncrementalContext(DiagramContract Contract, FlowchartLayoutResult Layout,
            bool IncludeArrangementCheck = true)
        {
            _contract = Contract;
            _layout = Layout;
            _includeArrangementCheck = IncludeArrangementCheck;
            _routes = Layout.EdgeRoutes.ToArray();
            _routeLengths = _routes.Select(Route => DrawnLength(Route.Points)).ToArray();
            _totalLength = _routeLengths.Sum();
            _routeIndexes = _routes.Select((Route, Index) => (Route, Index))
                .ToDictionary(Item => Item.Route, Item => Item.Index);
            _routePoints = _routes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
            _routeCrossings = new int[_routes.Length];
            _routeSharedEndpointCrossings = new int[_routes.Length];
            _routeBends = new int[_routes.Length];
            _pairCrossings = new int[_routes.Length, _routes.Length];
            _sharedEndpointPairCrossings = new int[_routes.Length, _routes.Length];
            (_disconnectBounds, _disconnectShapes, _disconnectEdges) = DisconnectionIndexes(Contract, Layout);
            _routeDisconnections = new int[_routes.Length];
            _routeFaults = new int[_routes.Length];
            _routeRejected = new bool[_routes.Length];
            _forcedMinimum = ForcedCrossingMinimum(Contract, Layout);
            (_width, _height) = Bounds(Layout);
            _emptyShare = EmptyAreaShare(Layout, _width, _height);
            (_, _, double HintScore) = MeasureHints(Contract, Layout);
            _staticScore = Calculate(_width, _height, _forcedMinimum, _forcedMinimum, _emptyShare) + HintScore;
            _backEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract)
                .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
            var InitialViolations = new List<FlowchartLayoutQualityFailure>();
            DiagramGridQuality.Collect(Contract, Layout, InitialViolations, IncludeArrangementCheck);
            _staticViolations = InitialViolations.Where(Failure => !IsRouteFault(Failure)).ToArray();
            _staticOverlaps = _staticViolations.Count(Failure => Failure.Kind == "NodeOverlap");
            _staticFaults = _staticViolations.Count(Failure => Failure.Kind != "ArrangementKept");
            _staticRejected = _staticOverlaps > 0
                || _staticViolations.Any(Failure => Rejects(Failure, Contract, Layout, _backEdges));
            int OtherFaults = 0, OtherRejected = 0;
            _routeViolations = new IReadOnlyList<FlowchartLayoutQualityFailure>[_routes.Length];
            for (int Index = 0; Index < _routes.Length; Index++)
            {
                var RouteFailures = new List<FlowchartLayoutQualityFailure>();
                DiagramGridQuality.CollectRouteFailures(Layout, _routes[Index], RouteFailures);
                _routeViolations[Index] = RouteFailures;
                _routeFaults[Index] = RouteFailures.Count(Failure => Failure.Kind != "ArrangementKept");
                _routeRejected[Index] = RouteFailures.Any(Failure => Rejects(Failure, Contract, Layout, _backEdges));
                OtherFaults += _routeFaults[Index];
                if (_routeRejected[Index]) OtherRejected++;
            }
            _otherRouteFaults = OtherFaults;
            _otherRejectedRoutes = OtherRejected;
            for (int Left = 0; Left < _routes.Length; Left++)
            {
                _routeBends[Left] = CountCorners(_routes[Left].Points);
                _routeDisconnections[Left] = CountRouteDisconnections(_routes[Left],
                    _disconnectBounds, _disconnectShapes, _disconnectEdges);
                for (int Right = Left + 1; Right < _routes.Length; Right++)
                {
                    int Pair = PairCrossings(_routes[Left], _routePoints[Left], _routes[Right], _routePoints[Right]);
                    _pairCrossings[Left, Right] = Pair;
                    _pairCrossings[Right, Left] = Pair;
                    int SharedEndpointPair = PairSharedEndpointCrossings(
                        _routes[Left], _routePoints[Left], _routes[Right], _routePoints[Right]);
                    _sharedEndpointPairCrossings[Left, Right] = SharedEndpointPair;
                    _sharedEndpointPairCrossings[Right, Left] = SharedEndpointPair;
                    _sharedEndpointCrossings += SharedEndpointPair;
                    _routeSharedEndpointCrossings[Left] += SharedEndpointPair;
                    _routeSharedEndpointCrossings[Right] += SharedEndpointPair;
                    _routeCrossings[Left] += Pair;
                    _routeCrossings[Right] += Pair;
                }
            }
            _crossings = _routeCrossings.Sum() / 2;
            _bends = _routeBends.Sum();
            _disconnections = _routeDisconnections.Sum();
        }

        internal Result MeasureCandidate(FlowchartLayoutEdgeRoute ChangedRoute)
            => MeasureCandidate(ChangedRoute, null);

        internal Result MeasureCandidate(FlowchartLayoutEdgeRoute ChangedRoute,
            IReadOnlyCollection<FlowchartLayoutEdgeRoute>? InteractingRoutes)
        {
            int Index = FindRoute(ChangedRoute);
            int[] OtherIndices = ResolveOtherIndices(Index, InteractingRoutes);
            (double X, double Y)[] ChangedPoints = ChangedRoute.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            int CandidateCrossings = _crossings - _routeCrossings[Index]
                + CountRouteCrossings(Index, ChangedRoute, ChangedPoints, OtherIndices);
            int CandidateBends = _bends - _routeBends[Index] + CountCorners(ChangedRoute.Points);
            int CandidateDisconnections = _disconnections - _routeDisconnections[Index]
                + CountRouteDisconnections(ChangedRoute, _disconnectBounds, _disconnectShapes, _disconnectEdges);
            int CandidateSharedEndpointCrossings = _sharedEndpointCrossings
                - CountRouteSharedEndpointCrossings(Index)
                + CountRouteSharedEndpointCrossings(Index, ChangedRoute, ChangedPoints, OtherIndices);
            if (_includeArrangementCheck)
                return MeasureCore(_contract, _layout, true, CandidateCrossings, CandidateBends,
                    ForcedMinimumOverride: _forcedMinimum, DisconnectionsOverride: CandidateDisconnections,
                    SharedEndpointCrossings: CandidateSharedEndpointCrossings,
                    RouteLength: _totalLength - _routeLengths[Index] + DrawnLength(ChangedRoute.Points));

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
                CandidateSharedEndpointCrossings,
                _totalLength - _routeLengths[Index] + DrawnLength(ChangedRoute.Points));
        }

        internal CandidateMeasurement MeasureCandidateSummary(FlowchartLayoutEdgeRoute ChangedRoute,
            IReadOnlyCollection<FlowchartLayoutEdgeRoute>? InteractingRoutes)
        {
            int Index = FindRoute(ChangedRoute);
            int[] OtherIndices = ResolveOtherIndices(Index, InteractingRoutes);
            (double X, double Y)[] ChangedPoints = ChangedRoute.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            int CandidateCrossings = _crossings - _routeCrossings[Index]
                + CountRouteCrossings(Index, ChangedRoute, ChangedPoints, OtherIndices);
            int CandidateBends = _bends - _routeBends[Index] + CountCorners(ChangedRoute.Points);
            int CandidateDisconnections = _disconnections - _routeDisconnections[Index]
                + CountRouteDisconnections(ChangedRoute, _disconnectBounds, _disconnectShapes, _disconnectEdges);
            int CandidateSharedEndpointCrossings = _sharedEndpointCrossings
                - CountRouteSharedEndpointCrossings(Index)
                + CountRouteSharedEndpointCrossings(Index, ChangedRoute, ChangedPoints, OtherIndices);
            var CandidateViolations = new List<FlowchartLayoutQualityFailure>();
            DiagramGridQuality.CollectRouteFailures(_layout, ChangedRoute, CandidateViolations);
            int Faults = _staticFaults + _otherRouteFaults - _routeFaults[Index]
                + CandidateViolations.Count(Failure => Failure.Kind != "ArrangementKept");
            bool Rejected = _staticRejected || _otherRejectedRoutes - (_routeRejected[Index] ? 1 : 0) > 0
                || CandidateViolations.Any(Failure => Rejects(Failure, _contract, _layout, _backEdges));
            // Route cells contribute to empty-area cost. They are not static:
            // moving a track or extending a diagonal terminal changes occupancy.
            // Keep the cached pair counts, but measure occupancy on the candidate
            // currently installed in the layout, just as MeasureCore does.
            double CandidateEmptyShare = EmptyAreaShare(_layout, _width, _height);
            double OptimizationScore = _staticScore
                + Calculate(_width, _height, _forcedMinimum, _forcedMinimum, CandidateEmptyShare)
                - Calculate(_width, _height, _forcedMinimum, _forcedMinimum, _emptyShare)
                + Math.Max(0, CandidateCrossings - _forcedMinimum) * FlowchartLayoutRules.CrossingScore
                + CandidateSharedEndpointCrossings * FlowchartLayoutRules.CrossingScore
                + CandidateBends * FlowchartLayoutRules.BendScore
                + CandidateDisconnections * FlowchartLayoutRules.DisconnectedEndScore;
            var Cost = new ComparisonCost(OptimizationScore,
                (_totalLength - _routeLengths[Index] + DrawnLength(ChangedRoute.Points))
                    / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RouteLengthWeight,
                CandidateBends, (Math.Max(0, CandidateCrossings - _forcedMinimum) + CandidateSharedEndpointCrossings)
                    * FlowchartLayoutRules.CrossingScore);
            OptimizationScore = Cost;
            return new(Rejected ? null : OptimizationScore, OptimizationScore, _staticOverlaps,
                CandidateCrossings, Faults, Cost);
        }

        internal void CommitCurrent(FlowchartLayoutEdgeRoute ChangedRoute)
            => CommitCurrent(ChangedRoute, null);

        internal void CommitCurrent(FlowchartLayoutEdgeRoute ChangedRoute,
            IReadOnlyCollection<FlowchartLayoutEdgeRoute>? InteractingRoutes)
        {
            int Index = FindRoute(ChangedRoute);
            int[] OtherIndices = ResolveOtherIndices(Index, InteractingRoutes);
            (double X, double Y)[] ChangedPoints = ChangedRoute.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            int NewRouteCrossings = 0;
            int NewRouteSharedEndpointCrossings = 0;
            foreach (int OtherIndex in OtherIndices)
            {
                int NewPair = PairCrossings(ChangedRoute, ChangedPoints, _routes[OtherIndex], _routePoints[OtherIndex]);
                int Delta = NewPair - _pairCrossings[Index, OtherIndex];
                _crossings += Delta;
                _routeCrossings[OtherIndex] += Delta;
                _pairCrossings[Index, OtherIndex] = NewPair;
                _pairCrossings[OtherIndex, Index] = NewPair;
                NewRouteCrossings += NewPair;
                int NewSharedPair = PairSharedEndpointCrossings(
                    ChangedRoute, ChangedPoints, _routes[OtherIndex], _routePoints[OtherIndex]);
                int SharedDelta = NewSharedPair - _sharedEndpointPairCrossings[Index, OtherIndex];
                _sharedEndpointCrossings += SharedDelta;
                _routeSharedEndpointCrossings[OtherIndex] += SharedDelta;
                _sharedEndpointPairCrossings[Index, OtherIndex] = NewSharedPair;
                _sharedEndpointPairCrossings[OtherIndex, Index] = NewSharedPair;
                NewRouteSharedEndpointCrossings += NewSharedPair;
            }
            _routeCrossings[Index] = NewRouteCrossings;
            _routeSharedEndpointCrossings[Index] = NewRouteSharedEndpointCrossings;
            _routePoints[Index] = ChangedPoints;
            double UpdatedLength = DrawnLength(ChangedRoute.Points);
            _totalLength += UpdatedLength - _routeLengths[Index];
            _routeLengths[Index] = UpdatedLength;
            _bends += CountCorners(ChangedRoute.Points) - _routeBends[Index];
            _routeBends[Index] = CountCorners(ChangedRoute.Points);
            int NewDisconnections = CountRouteDisconnections(ChangedRoute,
                _disconnectBounds, _disconnectShapes, _disconnectEdges);
            _disconnections += NewDisconnections - _routeDisconnections[Index];
            _routeDisconnections[Index] = NewDisconnections;
            var UpdatedFaults = new List<FlowchartLayoutQualityFailure>();
            DiagramGridQuality.CollectRouteFailures(_layout, ChangedRoute, UpdatedFaults);
            _routeViolations[Index] = UpdatedFaults;
            int OldRouteFaults = _routeFaults[Index];
            bool OldRouteRejected = _routeRejected[Index];
            _routeFaults[Index] = UpdatedFaults.Count(Failure => Failure.Kind != "ArrangementKept");
            _routeRejected[Index] = UpdatedFaults.Any(Failure => Rejects(Failure, _contract, _layout, _backEdges));
            _otherRouteFaults += _routeFaults[Index] - OldRouteFaults;
            _otherRejectedRoutes += (_routeRejected[Index] ? 1 : 0) - (OldRouteRejected ? 1 : 0);
        }

        private int FindRoute(FlowchartLayoutEdgeRoute Route)
        {
            if (_routeIndexes.TryGetValue(Route, out int Index)) return Index;
            throw new ArgumentException("The changed route is not part of this score context.", nameof(Route));
        }

        private int[] ResolveOtherIndices(int ChangedIndex,
            IReadOnlyCollection<FlowchartLayoutEdgeRoute>? InteractingRoutes)
        {
            if (InteractingRoutes == null)
                return Enumerable.Range(0, _routes.Length).Where(Index => Index != ChangedIndex).ToArray();
            return InteractingRoutes.Select(FindRoute).Where(Index => Index != ChangedIndex)
                .Distinct().ToArray();
        }

        private int CountRouteCrossings(int ChangedIndex, FlowchartLayoutEdgeRoute ChangedRoute,
            (double X, double Y)[] ChangedPoints, IReadOnlyList<int> OtherIndices)
        {
            int Result = 0;
            foreach (int OtherIndex in OtherIndices)
                Result += PairCrossings(ChangedRoute, ChangedPoints, _routes[OtherIndex], _routePoints[OtherIndex]);
            return Result;
        }

        private int CountRouteSharedEndpointCrossings(int ChangedIndex) =>
            _routeSharedEndpointCrossings[ChangedIndex];

        private int CountRouteSharedEndpointCrossings(int ChangedIndex, FlowchartLayoutEdgeRoute ChangedRoute,
            (double X, double Y)[] ChangedPoints, IReadOnlyList<int> OtherIndices)
        {
            int Result = 0;
            foreach (int OtherIndex in OtherIndices)
                Result += PairSharedEndpointCrossings(
                    ChangedRoute, ChangedPoints, _routes[OtherIndex], _routePoints[OtherIndex]);
            return Result;
        }

        private static bool IsRouteFault(FlowchartLayoutQualityFailure Failure) =>
            Failure.Kind == "LineShape"
                || Failure.Kind == "WholePixel" && Failure.Description.Contains("->", StringComparison.Ordinal);
    }

    private static int PairCrossings(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B)
        => PairCrossings(A, A.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
            B, B.Points.Select(Point => (Point.X, Point.Y)).ToArray());

    private static int PairCrossings(FlowchartLayoutEdgeRoute A, IReadOnlyList<(double X, double Y)> APoints,
        FlowchartLayoutEdgeRoute B, IReadOnlyList<(double X, double Y)> BPoints)
    {
        if (A.SourceId == B.SourceId || A.SourceId == B.TargetId
            || A.TargetId == B.SourceId || A.TargetId == B.TargetId) return 0;
        return FlowchartBranchRouteOrder.Intersections(APoints, BPoints).Crossings;
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
        => PairSharedEndpointCrossings(A, A.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
            B, B.Points.Select(Point => (Point.X, Point.Y)).ToArray());

    private static int PairSharedEndpointCrossings(FlowchartLayoutEdgeRoute A,
        IReadOnlyList<(double X, double Y)> APoints, FlowchartLayoutEdgeRoute B,
        IReadOnlyList<(double X, double Y)> BPoints)
    {
        if (A.SourceId != B.SourceId && A.SourceId != B.TargetId
            && A.TargetId != B.SourceId && A.TargetId != B.TargetId)
            return 0;
        if (A.SourceId == B.SourceId && A.TargetId == B.TargetId) return 0;
        return FlowchartBranchRouteOrder.Intersections(APoints, BPoints).Crossings;
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

    /// <summary>Scores complete pre-routing node and frame geometry using raster empty-space terms.</summary>
    /// <param name="Layout">Projected candidate bounds, including every frame; no route has yet been selected.</param>
    /// <returns>The unchanged placement terms of the layout score, with no invented route penalties.</returns>
    internal static double MeasurePlacement(FlowchartLayoutResult Layout)
    {
        var (Width, Height) = Bounds(Layout);
        return Calculate(Width, Height, 0, EmptyAreaShare(Layout, Width, Height));
    }

    internal static double Calculate(double Width, double Height, int Crossings, double EmptyShare)
        => Calculate(Width, Height, Crossings, 0, EmptyShare);

    internal static double Calculate(double Width, double Height, int Crossings, int ForcedMinimum, double EmptyShare)
    {
        const double EmptyScoreStepPercent = 5.0;
        double EmptyPercent = EmptyShare * 100.0;
        double EmptyLowRate = FlowchartLayoutRules.EmptyPointsLow / EmptyScoreStepPercent;
        double EmptyHighRate = FlowchartLayoutRules.EmptyPointsHigh / EmptyScoreStepPercent;
        double Empty = EmptyPercent <= FlowchartLayoutRules.EmptyThreshold
            ? EmptyPercent * EmptyLowRate
            : FlowchartLayoutRules.EmptyThreshold * EmptyLowRate
                + (EmptyPercent - FlowchartLayoutRules.EmptyThreshold) * EmptyHighRate;
        double OverWidthPx = Math.Max(0.0, Width - FlowchartLayoutRules.DiagramUsableWidthPx);
        double OverWidthPercent = OverWidthPx / Math.Max(1.0, FlowchartLayoutRules.DiagramUsableWidthPx) * 100.0;
        double WrapWidthPenalty = OverWidthPercent / EmptyScoreStepPercent * 2.0;
        return Empty + WrapWidthPenalty + Math.Max(0, Crossings - ForcedMinimum) * FlowchartLayoutRules.CrossingScore;
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

    /// <summary>Counts direction changes, including permitted sloping diamond terminal runs.</summary>
    /// <param name="Points">The complete routed polyline in layout coordinates.</param>
    /// <returns>The bend count used by both full and incremental candidate scoring.</returns>
    internal static int CountCorners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Result = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            double BeforeX = Points[Index].X - Points[Index - 1].X;
            double BeforeY = Points[Index].Y - Points[Index - 1].Y;
            double AfterX = Points[Index + 1].X - Points[Index].X;
            double AfterY = Points[Index + 1].Y - Points[Index].Y;
            if (Math.Abs(BeforeX * AfterY - BeforeY * AfterX) > 0.001) Result++;
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
        var Rectangles = new List<(int Left, int Right, int Bottom, int Top)>();
        void Mark(double MinX, double MinY, double MaxX, double MaxY)
        {
            int FirstColumn = Math.Max(0, (int)Math.Floor((MinX - Left) / Cell));
            int LastColumn = Math.Min(Columns - 1, (int)Math.Floor((MaxX - Left) / Cell));
            int FirstRow = Math.Max(0, (int)Math.Floor((MinY - Bottom) / Cell));
            int LastRow = Math.Min(Rows - 1, (int)Math.Floor((MaxY - Bottom) / Cell));
            if (FirstColumn <= LastColumn && FirstRow <= LastRow)
                Rectangles.Add((FirstColumn, LastColumn + 1, FirstRow, LastRow + 1));
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
        // Count the same occupied grid cells by sweeping rectangle boundaries.
        // A long route or large frame must not require visiting every cell for
        // every candidate score. Half-open integer intervals preserve the
        // previous inclusive-cell marking, including cells on an outline.
        int[] Boundaries = Rectangles.SelectMany(Rectangle => new[] { Rectangle.Left, Rectangle.Right })
            .Distinct().OrderBy(Value => Value).ToArray();
        long Used = 0;
        for (int Index = 1; Index < Boundaries.Length; Index++)
        {
            int LeftColumn = Boundaries[Index - 1], RightColumn = Boundaries[Index];
            var Intervals = Rectangles.Where(Rectangle => Rectangle.Left < RightColumn && Rectangle.Right > LeftColumn)
                .OrderBy(Rectangle => Rectangle.Bottom).ThenBy(Rectangle => Rectangle.Top);
            int BottomRow = 0, TopRow = 0, OccupiedRows = 0;
            foreach (var Rectangle in Intervals)
            {
                if (Rectangle.Bottom > TopRow)
                {
                    OccupiedRows += TopRow - BottomRow;
                    BottomRow = Rectangle.Bottom;
                    TopRow = Rectangle.Top;
                }
                else TopRow = Math.Max(TopRow, Rectangle.Top);
            }
            OccupiedRows += TopRow - BottomRow;
            Used += (long)(RightColumn - LeftColumn) * OccupiedRows;
        }
        return 1.0 - Used / ((double)Columns * Rows);
    }
}
