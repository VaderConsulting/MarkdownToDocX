using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Routes dense adjacent layers from distinct bottom ports to distinct top ports.</summary>
internal static class FlowchartManyToManyRoutes
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        HashSet<(string SourceId, string TargetId)> CompletePairs = CompleteBlockPairs(Contract);
        var Routes = Layout.EdgeRoutes.Where(Route => Bounds.ContainsKey(Route.SourceId)
            && Bounds.ContainsKey(Route.TargetId)
            && !CompletePairs.Contains((Route.SourceId, Route.TargetId))
            && Bounds[Route.SourceId].CenterY > Bounds[Route.TargetId].CenterY + 1).ToList();
        foreach (var Rank in Routes.GroupBy(Route => (
            Source: Math.Round(Bounds[Route.SourceId].CenterY),
            Target: Math.Round(Bounds[Route.TargetId].CenterY))))
        {
            List<FlowchartLayoutEdgeRoute> Edges = Rank.ToList();
            if (Edges.Count < 2) continue;
            var SourceOrder = Edges.GroupBy(Route => Route.SourceId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.TargetId].CenterX).ThenBy(Route => Route.TargetId).ToList());
            var TargetOrder = Edges.GroupBy(Route => Route.TargetId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.SourceId].CenterX).ThenBy(Route => Route.SourceId).ToList());
            var Terminals = Edges.ToDictionary(Route => Route, Route =>
            {
                FlowchartLayoutNodeBounds Source = Bounds[Route.SourceId], Target = Bounds[Route.TargetId];
                List<FlowchartLayoutEdgeRoute> From = SourceOrder[Route.SourceId], Into = TargetOrder[Route.TargetId];
                double StartX = Slot(Source, From.IndexOf(Route), From.Count);
                double EndX = Slot(Target, Into.IndexOf(Route), Into.Count);
                return (StartX, StartY: Source.CenterY - Source.Height / 2.0,
                    EndX, EndY: Target.CenterY + Target.Height / 2.0);
            });
            double NearSource = Edges.Min(Route => Terminals[Route].StartY);
            double NearTarget = Edges.Max(Route => Terminals[Route].EndY);
            List<FlowchartLayoutEdgeRoute> Bent = Edges.Where(Route =>
                Math.Abs(Terminals[Route].StartX - Terminals[Route].EndX) > 0.5).ToList();
            if (Bent.Count == 0 || NearSource <= NearTarget) continue;

            List<FlowchartLayoutEdgeRoute> Order = Bent.OrderBy(Route => Bounds[Route.SourceId].CenterX)
                .ThenBy(Route => Bounds[Route.TargetId].CenterX).ToList();
            Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> PathsFor(
                IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                var Tracks = TrackOrder.Select((Route, Index) => (Route,
                    Y: Math.Round(NearSource - (Index + 1) * (NearSource - NearTarget) / (TrackOrder.Count + 1))))
                    .ToDictionary(Item => Item.Route, Item => Item.Y);
                return Edges.ToDictionary(Route => Route, Route =>
                {
                    var T = Terminals[Route];
                    return Math.Abs(T.StartX - T.EndX) <= 0.5
                        ? new List<(double X, double Y)> { (T.StartX, T.StartY), (T.EndX, T.EndY) }
                        : [(T.StartX, T.StartY), (T.StartX, Tracks[Route]),
                            (T.EndX, Tracks[Route]), (T.EndX, T.EndY)];
                });
            }
            int Crossings(IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                var Paths = PathsFor(TrackOrder);
                int Count = 0;
                for (int A = 0; A < Edges.Count; A++)
                for (int B = A + 1; B < Edges.Count; B++)
                {
                    if (SharesEndpoint(Edges[A], Edges[B])) continue;
                    Count += FlowchartBranchRouteOrder.Intersections(Paths[Edges[A]], Paths[Edges[B]]).Crossings;
                }
                return Count;
            }
            int Best = Crossings(Order);
            while (true)
            {
                bool Improved = false;
                for (int First = 0; First < Order.Count && !Improved; First++)
                for (int Second = First + 1; Second < Order.Count; Second++)
                {
                    (Order[First], Order[Second]) = (Order[Second], Order[First]);
                    int Candidate = Crossings(Order);
                    if (Candidate < Best) { Best = Candidate; Improved = true; break; }
                    (Order[First], Order[Second]) = (Order[Second], Order[First]);
                }
                if (!Improved) break;
            }
            var Paths = PathsFor(Order);
            if (Paths.Any(Item => HitsNode(Item.Value, Item.Key, Bounds))) continue;
            foreach (FlowchartLayoutEdgeRoute Route in Edges)
            {
                Route.Points.Clear();
                foreach ((double X, double Y) Point in Paths[Route])
                    Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                Route.ExitSide = "bottom";
                Route.EntrySide = "top";
            }
        }
    }

    private static HashSet<(string SourceId, string TargetId)> CompleteBlockPairs(DiagramContract Contract)
    {
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        var Sources = Visible.GroupBy(Edge => Edge.SourceId).Select(Group => (SourceId: Group.Key,
            Targets: Group.Select(Edge => Edge.TargetId).Distinct().Order().ToArray())).ToList();
        var Result = new HashSet<(string SourceId, string TargetId)>();
        foreach (var Block in Sources.Where(Item => Item.Targets.Length >= 2)
            .GroupBy(Item => string.Join("\u001f", Item.Targets))
            .Where(Group => Group.Count() >= 2))
        foreach (var Source in Block)
        foreach (string TargetId in Source.Targets)
            Result.Add((Source.SourceId, TargetId));
        return Result;
    }

    private static double Slot(FlowchartLayoutNodeBounds Box, int Index, int Count) => Math.Round(
        Box.CenterX - Box.Width / 2.0 + Box.Width * (Index + 1.0) / (Count + 1.0));

    private static bool SharesEndpoint(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B) =>
        A.SourceId == B.SourceId || A.SourceId == B.TargetId || A.TargetId == B.SourceId || A.TargetId == B.TargetId;

    private static bool HitsNode(IReadOnlyList<(double X, double Y)> Points, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId) continue;
            double Left = Box.CenterX - Box.Width / 2.0, Right = Box.CenterX + Box.Width / 2.0;
            double Bottom = Box.CenterY - Box.Height / 2.0, Top = Box.CenterY + Box.Height / 2.0;
            foreach (var Segment in Points.Zip(Points.Skip(1)))
            {
                var A = Segment.First; var B = Segment.Second;
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top)) return true;
                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right)) return true;
            }
        }
        return false;
    }
}
