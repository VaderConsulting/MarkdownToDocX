using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

internal enum DiagramRouteAssessment { Measured, Investigate, Invalid }

internal sealed record DiagramRouteMeasurement(string Identity, string SourceId, string TargetId,
    DiagramRouteAssessment Assessment, int Bends, ImmutableArray<string> CrossingPartners,
    ImmutableArray<string> SharedTrackPartners, double? LengthPx, double? DirectDistancePx,
    double? ExcessLengthPx, double? DetourRatio, ImmutableArray<FlowchartRouteRuleChecker.Finding> Findings);

internal sealed record DiagramRouteReport(ImmutableArray<DiagramRouteMeasurement> Routes,
    int Bends, int CrossingPairs, int SharedTrackPairs, ImmutableArray<FlowchartRouteRuleChecker.Finding> Findings);

/// <summary>Measures accepted paths without constructing routes or inferring that a detour is avoidable.</summary>
internal static class DiagramRouteDiagnostics
{
    internal static DiagramRouteReport Measure(DiagramContract Model, FlowchartLayoutResult Layout)
    {
        var Edges = Model.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Edge => Edge.Id, StringComparer.Ordinal).ToArray();
        if (Edges.Any(Edge => string.IsNullOrEmpty(Edge.Id)) || Edges.Select(Edge => Edge.Id).Distinct(StringComparer.Ordinal).Count() != Edges.Length)
            throw new ArgumentException("Route diagnostics require unique stable relationship identities.", nameof(Model));
        var Paths = Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId, StringComparer.Ordinal);
        var Findings = FlowchartRouteRuleChecker.Check(Model, Layout).ToImmutableArray();
        Dictionary<string, List<string>> Crossings = Edges.ToDictionary(Edge => Edge.Id!, _ => new List<string>(), StringComparer.Ordinal);
        Dictionary<string, List<string>> Shared = Edges.ToDictionary(Edge => Edge.Id!, _ => new List<string>(), StringComparer.Ordinal);
        Dictionary<string, List<FlowchartRouteRuleChecker.Finding>> PerRoute = Edges.ToDictionary(Edge => Edge.Id!,
            Edge => Findings.Where(Item => Item.Rule is not ("5.1" or "5.9")
                && (Item.Subject == Edge.Id || Item.Subject == Edge.SourceId || Item.Subject == Edge.TargetId)).ToList(), StringComparer.Ordinal);
        for (int A = 0; A < Edges.Length; A++)
        for (int B = A + 1; B < Edges.Length; B++)
        {
            var First = Edges[A]; var Second = Edges[B];
            if (!Paths.TryGetValue(First.Id!, out var Left) || !Paths.TryGetValue(Second.Id!, out var Right)
                || Left.Points.Count < 2 || Right.Points.Count < 2) continue;
            if (!(First.SourceId == Second.SourceId && First.TargetId == Second.TargetId)
                && FlowchartRouteRuleChecker.HasProperCrossing(Left, Right)) Pair(Crossings, "5.9", "routes cross away from a shared endpoint");
            if (FlowchartBranchRouteOrder.Intersections(Left.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                Right.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Overlap > 0.001)
                Pair(Shared, "5.1", "routes share a positive-length track");
            void Pair(Dictionary<string, List<string>> Inventory, string Rule, string Message)
            {
                Inventory[First.Id!].Add(Second.Id!); Inventory[Second.Id!].Add(First.Id!);
                var Finding = new FlowchartRouteRuleChecker.Finding(Rule, First.Id + " x " + Second.Id, Message);
                PerRoute[First.Id!].Add(Finding); PerRoute[Second.Id!].Add(Finding);
            }
        }
        var Measurements = Edges.Select(Edge =>
        {
            Paths.TryGetValue(Edge.Id!, out var Route);
            bool Finite = Route is { Points.Count: >= 2 } && Route.Points.All(Point => double.IsFinite(Point.X) && double.IsFinite(Point.Y));
            double? Length = Finite ? Route!.Points.Zip(Route.Points.Skip(1)).Sum(Pair =>
                Math.Sqrt(Math.Pow(Pair.Second.X - Pair.First.X, 2) + Math.Pow(Pair.Second.Y - Pair.First.Y, 2))) : null;
            double? Direct = Finite ? Math.Abs(Route!.Points[^1].X - Route.Points[0].X) + Math.Abs(Route.Points[^1].Y - Route.Points[0].Y) : null;
            if (Length.HasValue && (!double.IsFinite(Length.Value) || !double.IsFinite(Direct!.Value)))
            { Finite = false; Length = Direct = null; }
            int Bends = Route == null ? 0 : FlowchartLayoutScore.CountCorners(Route.Points);
            if (Route is { Points.Count: >= 2 } && !Finite)
                PerRoute[Edge.Id!].Add(new("geometry", Edge.Id!, "stored path has nonfinite geometry or measurements"));
            var Issues = PerRoute[Edge.Id!].ToImmutableArray();
            var Assessment = !Finite || Issues.Length > 0 ? DiagramRouteAssessment.Invalid
                : Bends > 2 || Length > Direct * 1.5 ? DiagramRouteAssessment.Investigate : DiagramRouteAssessment.Measured;
            return new DiagramRouteMeasurement(Edge.Id!, Edge.SourceId, Edge.TargetId, Assessment, Bends,
                Crossings[Edge.Id!].ToImmutableArray(), Shared[Edge.Id!].ToImmutableArray(), Length, Direct,
                Length.HasValue ? Math.Max(0, Length.Value - Direct!.Value) : null,
                Direct > 0 ? Length / Direct : null, Issues);
        }).ToImmutableArray();
        return new(Measurements, Measurements.Sum(Route => Route.Bends), Crossings.Values.Sum(Partners => Partners.Count) / 2,
            Shared.Values.Sum(Partners => Partners.Count) / 2,
            Findings.AddRange(Measurements.SelectMany(Route => Route.Findings).Where(Item => Item.Rule == "geometry")));
    }
}
