using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Repairs routes invalidated by container placement or track separation.</summary>
internal static class FlowchartObstacleRouteRepair
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        HashSet<string>? AttemptedRouteStates = null)
    {
        AttemptedRouteStates ??= new(StringComparer.Ordinal);
        FlowchartRouteSpatialIndex Spatial = new(Layout);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            if (!AttemptedRouteStates.Add(RouteStateKey(Route) + "|"
                    + Spatial.DependencySignature(Route.Points)))
            {
                continue;
            }

            EdgeContract? Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
            if (Edge == null)
            {
                continue;
            }

            List<FlowchartLayoutNodeBounds> Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge);
            HashSet<FlowchartLayoutNodeBounds> ObstacleSet = [.. Obstacles];
            List<FlowchartLayoutNodeBounds> Nearby = [.. Spatial.QueryBoxes(Route.Points).Where(ObstacleSet.Contains)];
            bool Blocked = IsBlocked(Route.Points, Nearby);
            if (!Blocked)
            {
                continue;
            }

            Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source);
            Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target);
            if (Source != null) { Obstacles.Add(Source); ObstacleSet.Add(Source); }
            if (Target != null && Target != Source) { Obstacles.Add(Target); ObstacleSet.Add(Target); }
            List<FlowchartLayoutNodeBounds> NearbyWithEndpoints = [.. Spatial.QueryBoxes(Route.Points).Where(ObstacleSet.Contains)];
            if (Source != null)
            {
                NearbyWithEndpoints.Add(Source);
            }

            if (Target != null && Target != Source)
            {
                NearbyWithEndpoints.Add(Target);
            }

            FlowchartLayoutPoint Start = Route.Points[0]; FlowchartLayoutPoint End = Route.Points[^1];
            bool Routed = FlowchartPaddedVisibilityRouter.TryRoute(Start.X, Start.Y, End.X, End.Y,
                NearbyWithEndpoints, Source, Target, out List<(double X, double Y)>? Points,
                DiagramGridRouteBuilder.CORNER_RADIUS_PX + 10, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            FlowchartLayoutPoint[] CandidatePoints = [.. Points.Select(Point =>
                new FlowchartLayoutPoint { X = Point.X, Y = Point.Y })];
            if (Routed && IsBlocked(Points, Spatial.QueryBoxes(CandidatePoints).Where(ObstacleSet.Contains).ToArray()))
            {
                Routed = FlowchartPaddedVisibilityRouter.TryRoute(Start.X, Start.Y, End.X, End.Y,
                    Obstacles, Source, Target, out Points,
                    DiagramGridRouteBuilder.CORNER_RADIUS_PX + 10, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            }

            if (!Routed)
            {
                continue;
            }

            Route.Points.Clear();
            foreach ((double X, double Y) Point in Points)
            {
                Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }

            Spatial.ReplaceRoute(Route);
        }
    }

    private static bool IsBlocked(IReadOnlyList<FlowchartLayoutPoint> Points,
        IReadOnlyCollection<FlowchartLayoutNodeBounds> Obstacles) =>
        Points.Zip(Points.Skip(1)).Any(Pair => Obstacles.Any(Box =>
            FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box, 0)));

    private static bool IsBlocked(IReadOnlyList<(double X, double Y)> Points,
        IReadOnlyCollection<FlowchartLayoutNodeBounds> Obstacles) =>
        Points.Zip(Points.Skip(1)).Any(Pair => Obstacles.Any(Box =>
            FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box, 0)));

    private static string RouteStateKey(FlowchartLayoutEdgeRoute Route) => Route.EdgeId + "|"
        + string.Join(";", Route.Points.Select(Point =>
            BitConverter.DoubleToInt64Bits(Point.X).ToString("X16") + ","
            + BitConverter.DoubleToInt64Bits(Point.Y).ToString("X16")));
}
