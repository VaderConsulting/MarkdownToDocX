using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Restores explicit endpoint and track directions after automatic route repair.</summary>
internal static class FlowchartRoutingHints
{
    internal static void Apply(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            RoutingHintContract? Hint = Edge?.RoutingHint;
            if (Hint?.IsActive != true
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
                continue;

            string Exit = Hint.Leave.HasValue
                ? DiagramGridLayout.HintSide(Hint.Leave.Value) : Route.ExitSide;
            string Entry = Hint.Enter.HasValue
                ? DiagramGridLayout.HintSide(Hint.Enter.Value) : Route.EntrySide;
            (double StartX, double StartY) = ResolveEndpoint(Route, Source, Exit, true);
            (double EndX, double EndY) = ResolveEndpoint(Route, Target, Entry, false);
            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
            Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
            List<(double X, double Y)> Path = DiagramGridRouteBuilder.BuildRoute(
                StartX, StartY, Source, SourceNode?.Shape, Exit,
                EndX, EndY, Target, TargetNode?.Shape, Entry,
                Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal, Hint.Travel);
            if (Path.Count < 2)
            {
                Hint.UnhonouredReason = Hint.Travel.HasValue
                    ? "no legal route has the requested travel direction"
                    : "no legal route has the requested endpoint directions";
                continue;
            }

            Hint.UnhonouredReason = null;
            Route.ExitSide = Exit;
            Route.EntrySide = Entry;
            Route.Points.Clear();
            foreach ((double X, double Y) in Path)
                Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
        }
    }

    private static (double X, double Y) ResolveEndpoint(
        FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutNodeBounds Bounds,
        string Side,
        bool Source)
    {
        FlowchartLayoutPoint? Existing = Route.Points.Count == 0
            ? null : Source ? Route.Points[0] : Route.Points[^1];
        double Left = Bounds.CenterX - Bounds.Width / 2.0;
        double Right = Bounds.CenterX + Bounds.Width / 2.0;
        double Bottom = Bounds.CenterY - Bounds.Height / 2.0;
        double Top = Bounds.CenterY + Bounds.Height / 2.0;
        if (Existing != null)
        {
            bool OnSide = Side switch
            {
                "left" => Math.Abs(Existing.X - Left) < 1.0,
                "right" => Math.Abs(Existing.X - Right) < 1.0,
                "top" => Math.Abs(Existing.Y - Top) < 1.0,
                "bottom" => Math.Abs(Existing.Y - Bottom) < 1.0,
                _ => false
            };
            if (OnSide) return (Existing.X, Existing.Y);
        }

        return Side switch
        {
            "left" => (Left, Bounds.CenterY),
            "right" => (Right, Bounds.CenterY),
            "top" => (Bounds.CenterX, Top),
            "bottom" => (Bounds.CenterX, Bottom),
            _ => (Bounds.CenterX, Bounds.CenterY)
        };
    }
}
