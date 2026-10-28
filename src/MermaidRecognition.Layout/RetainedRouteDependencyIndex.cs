using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Immutable endpoint and path-envelope values owned by one retained geometry revision.</summary>
internal sealed class RetainedRouteDependencyIndex
{
    private readonly record struct Envelope(FlowchartLayoutEdgeRoute Route, double Left, double Bottom, double Right, double Top);
    private readonly Dictionary<string, FlowchartLayoutEdgeRoute[]> Endpoints;
    private readonly Envelope[] Paths;
    private readonly FlowchartLayoutEdgeRoute[] Unbounded;
    private readonly double[] PrefixRight;

    internal RetainedRouteDependencyIndex(FlowchartLayoutResult Layout)
    {
        Endpoints = Layout.EdgeRoutes.SelectMany(Route => new[] { (Id: Route.SourceId, Route), (Id: Route.TargetId, Route) })
            .GroupBy(Item => Item.Id, StringComparer.Ordinal).ToDictionary(Group => Group.Key,
                Group => Group.Select(Item => Item.Route).Distinct().ToArray(), StringComparer.Ordinal);
        var Envelopes = Layout.EdgeRoutes.Where(Route => Route.Points.Count > 0).Select(Route => new Envelope(Route,
                Route.Points.Min(Point => Point.X), Route.Points.Min(Point => Point.Y),
                Route.Points.Max(Point => Point.X), Route.Points.Max(Point => Point.Y))).ToArray();
        static bool Finite(Envelope Item) => double.IsFinite(Item.Left) && double.IsFinite(Item.Bottom)
            && double.IsFinite(Item.Right) && double.IsFinite(Item.Top);
        Unbounded = Envelopes.Where(Item => !Finite(Item)).Select(Item => Item.Route).ToArray();
        Paths = Envelopes.Where(Finite).OrderBy(Item => Item.Left).ThenBy(Item => Item.Route.EdgeId, StringComparer.Ordinal).ToArray();
        PrefixRight = new double[Paths.Length];
        double Right = double.NegativeInfinity;
        for (int Index = 0; Index < Paths.Length; Index++) PrefixRight[Index] = Right = Math.Max(Right, Paths[Index].Right);
    }

    internal IEnumerable<FlowchartLayoutEdgeRoute> Incident(string Id)
    {
        if (!Endpoints.TryGetValue(Id, out var Routes)) yield break;
        foreach (var Route in Routes) yield return Route;
    }

    /// <summary>Returns a conservative set; the caller retains its exact frozen-segment intersection check.</summary>
    internal IEnumerable<FlowchartLayoutEdgeRoute> Near(FlowchartLayoutNodeBounds Box, double Margin)
    {
        foreach (var Route in Unbounded) yield return Route;
        double Left = Box.CenterX - Box.Width / 2 - Margin, Right = Box.CenterX + Box.Width / 2 + Margin;
        double Bottom = Box.CenterY - Box.Height / 2 - Margin, Top = Box.CenterY + Box.Height / 2 + Margin;
        int Low = 0, High = Paths.Length;
        while (Low < High)
        {
            int Middle = Low + (High - Low) / 2;
            if (Paths[Middle].Left <= Right) Low = Middle + 1;
            else High = Middle;
        }
        // No coordinate is converted to a bucket count: very large or negative
        // cell origins cannot create unbounded spatial-bucket allocations.
        for (int Index = Low - 1; Index >= 0 && PrefixRight[Index] >= Left; Index--)
        {
            var Path = Paths[Index];
            if (Path.Right >= Left && Path.Bottom <= Top && Path.Top >= Bottom) yield return Path.Route;
        }
    }
}
