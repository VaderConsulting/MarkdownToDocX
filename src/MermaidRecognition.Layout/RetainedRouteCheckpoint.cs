using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

internal sealed record RetainedRouteValue(string Source, string Target, string Exit, string Entry,
    double ExitX, double ExitY, double EntryX, double EntryY, ImmutableArray<(double X, double Y)> Points)
{
    internal bool Matches(FlowchartLayoutEdgeRoute Route) => Source == Route.SourceId && Target == Route.TargetId
        && Exit == Route.ExitSide && Entry == Route.EntrySide && ExitX == Route.ExitNormalX && ExitY == Route.ExitNormalY
        && EntryX == Route.EntryNormalX && EntryY == Route.EntryNormalY
        && Points.SequenceEqual(Route.Points.Select(Point => (Point.X, Point.Y)));
    internal static RetainedRouteValue Capture(FlowchartLayoutEdgeRoute Route) => new(Route.SourceId, Route.TargetId, Route.ExitSide, Route.EntrySide,
        Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY, Route.Points.Select(Point => (Point.X, Point.Y)).ToImmutableArray());
    internal void Apply(FlowchartLayoutEdgeRoute Route)
    {
        Route.ExitSide = Exit; Route.EntrySide = Entry; Route.ExitNormalX = ExitX; Route.ExitNormalY = ExitY;
        Route.EntryNormalX = EntryX; Route.EntryNormalY = EntryY;
        Route.Points.Clear(); Route.Points.AddRange(Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
    }
}

internal sealed record RetainedRouteCheckpoint(string BoundsKey, ImmutableDictionary<string, RetainedRouteValue> Routes,
    ImmutableArray<FlowchartRouteRuleChecker.Finding> Findings)
{
    internal static string GeometryKey(FlowchartLayoutResult Layout)
    {
        var Grid = Layout.CellNodes?.FirstOrDefault()?.CellNode?.Grid.Capture();
        string Json = JsonSerializer.Serialize(new
        {
            Boxes = Layout.NodeBounds.Concat(Layout.ClusterBounds).OrderBy(Box => Box.NodeId, StringComparer.Ordinal)
                .Select(Box => new { Box.NodeId, Box.CenterX, Box.CenterY, Box.Width, Box.Height }),
            Cells = Grid?.Regions.OrderBy(Pair => Pair.Key, StringComparer.Ordinal).Select(Pair => new
                { Id = Pair.Key, Pair.Value.StartRow, Pair.Value.StartColumn, Pair.Value.RowSpan, Pair.Value.ColumnSpan }),
            Grid = Grid?.Owner.Configuration, Columns = Grid?.ColumnGaps.OrderBy(Pair => Pair.Key), Rows = Grid?.RowGaps.OrderBy(Pair => Pair.Key)
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json)));
    }

    internal static RetainedRouteCheckpoint Capture(DiagramContract Model, FlowchartLayoutResult Layout, RetainedRouteCheckpoint? Previous)
    {
        var Routes = (Previous?.Routes ?? ImmutableDictionary<string, RetainedRouteValue>.Empty).ToBuilder();
        var Present = Layout.EdgeRoutes.Select(Route => Route.EdgeId).ToHashSet(StringComparer.Ordinal);
        foreach (string Removed in Routes.Keys.Where(Id => !Present.Contains(Id)).ToArray()) Routes.Remove(Removed);
        foreach (var Route in Layout.EdgeRoutes)
            if (!Routes.TryGetValue(Route.EdgeId, out var Old) || !Old.Matches(Route)) Routes[Route.EdgeId] = RetainedRouteValue.Capture(Route);
        return new(GeometryKey(Layout), Routes.ToImmutable(), FlowchartRouteRuleChecker.Check(Model, Layout).ToImmutableArray());
    }
}

public static partial class DiagramGridLayout
{
    internal sealed partial class RetainedRoutingState
    {
        internal string? RestoreAcceptedRoutes(RetainedRouteCheckpoint Checkpoint, out string[] Changed)
        {
            Changed = [];
            if (Checkpoint.BoundsKey != RetainedRouteCheckpoint.GeometryKey(Layout) || Checkpoint.Routes.Count != Layout.EdgeRoutes.Count)
                return "The recorded routes belong to different node/cell geometry.";
            var Before = FlowchartRouteRuleChecker.Check(State.Contract, Layout).ToHashSet();
            List<string> Changes = [];
            foreach (var Route in Layout.EdgeRoutes)
            {
                if (!Checkpoint.Routes.TryGetValue(Route.EdgeId, out var Value) || Value.Source != Route.SourceId || Value.Target != Route.TargetId)
                    return "The recorded route inventory differs from the source model.";
                if (Value.Matches(Route)) continue;
                Value.Apply(Route); Changes.Add(Route.EdgeId);
            }
            Changed = Changes.Order(StringComparer.Ordinal).ToArray();
            Layout.BumpGeometryVersion();
            string? Failure = FlowchartConnectionPoints.Validate(State.Contract, Layout, CheckRoutes: true);
            if (Failure != null) return Failure;
            foreach (var Route in Layout.EdgeRoutes)
            {
                if (FindContractEdge(State.Contract, Route)?.RoutingHint is not { IsActive: true } Hint) continue;
                if ((Hint.Leave.HasValue && Route.ExitSide != HintSide(Hint.Leave.Value))
                    || (Hint.Enter.HasValue && Route.EntrySide != HintSide(Hint.Enter.Value))
                    || (Hint.Travel.HasValue && !DiagramGridRouteBuilder.HasTravelDirection(Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Hint.Travel.Value)))
                    return "The recorded routes do not honour the current mandatory hints.";
            }
            if (FlowchartRouteRuleChecker.Check(State.Contract, Layout).Any(Finding => !Before.Contains(Finding) && !Checkpoint.Findings.Contains(Finding)))
                return "Recorded geometry introduces a new route legality finding.";
            var Boxes = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToArray();
            var Points = Layout.EdgeRoutes.SelectMany(Route => Route.Points).ToArray();
            var Xs = Boxes.SelectMany(Box => new[] { Box.CenterX - Box.Width / 2, Box.CenterX + Box.Width / 2 }).Concat(Points.Select(Point => Point.X)).ToArray();
            var Ys = Boxes.SelectMany(Box => new[] { Box.CenterY - Box.Height / 2, Box.CenterY + Box.Height / 2 }).Concat(Points.Select(Point => Point.Y)).ToArray();
            Layout.SetGraphSize(Xs.Length == 0 ? 0 : Xs.Max() - Xs.Min(), Ys.Length == 0 ? 0 : Ys.Max() - Ys.Min());
            PublishGuides(State, Layout.ResolvedEdgeGuides);
            return null;
        }
    }
}
