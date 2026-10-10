using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Repairs routes invalidated by container placement or track separation.</summary>
internal static class FlowchartObstacleRouteRepair
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (var Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2) continue;
            var Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
            if (Edge == null) continue;
            var Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge);
            bool Blocked = Route.Points.Zip(Route.Points.Skip(1)).Any(Pair => Obstacles.Any(Box =>
                FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box, 0)));
            if (!Blocked) continue;
            Bounds.TryGetValue(Route.SourceId, out var Source);
            Bounds.TryGetValue(Route.TargetId, out var Target);
            if (Source != null) Obstacles.Add(Source);
            if (Target != null && Target != Source) Obstacles.Add(Target);
            var Start = Route.Points[0]; var End = Route.Points[^1];
            if (!FlowchartPaddedVisibilityRouter.TryRoute(Start.X, Start.Y, End.X, End.Y,
                Obstacles, Source, Target, out var Points,
                DiagramGridRouteBuilder.CORNER_RADIUS_PX + 10, DiagramGridRouteBuilder.CORNER_RADIUS_PX)) continue;
            Route.Points.Clear();
            foreach (var Point in Points) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
        }
    }
}
