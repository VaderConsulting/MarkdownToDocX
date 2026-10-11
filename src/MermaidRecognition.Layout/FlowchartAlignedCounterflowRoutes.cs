using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Uses distinct straight ports for an opposing pair between aligned boxes.</summary>
internal static class FlowchartAlignedCounterflowRoutes
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var Seen = new HashSet<FlowchartLayoutEdgeRoute>();
        foreach (var Route in Layout.EdgeRoutes)
        {
            if (!Seen.Add(Route) || !Bounds.TryGetValue(Route.SourceId, out var A)
                || !Bounds.TryGetValue(Route.TargetId, out var B) || A == B) continue;
            var Pair = Layout.EdgeRoutes.Where(Other => Other.SourceId == Route.SourceId && Other.TargetId == Route.TargetId
                || Other.SourceId == Route.TargetId && Other.TargetId == Route.SourceId).ToList();
            if (Pair.Count != 2 || Pair.All(Other => Other.SourceId == Route.SourceId)
                || Pair.Any(Other => Other.Points.Count < 2)) continue;
            var Edges = Pair.Select(Other => Contract.Edges.FirstOrDefault(Edge => Edge.Id == Other.EdgeId)).ToList();
            if (Edges.Any(Edge => Edge == null || !string.IsNullOrEmpty(Edge.Label)
                || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))) continue;
            if (Contract.Groups.Any(Group => Group.Id == A.NodeId || Group.Id == B.NodeId)
                || Contract.Nodes.Any(Node => (Node.Id == A.NodeId || Node.Id == B.NodeId)
                    && Node.Shape is not (null or "" or "rect" or "rounded" or "round"))) continue;
            bool Vertical = Math.Abs(A.CenterX - B.CenterX) < 0.5;
            if (!Vertical && Math.Abs(A.CenterY - B.CenterY) >= 0.5) continue;
            double Cross(FlowchartLayoutNodeBounds Box) => Vertical ? Box.CenterX : Box.CenterY;
            double Main(FlowchartLayoutNodeBounds Box) => Vertical ? Box.CenterY : Box.CenterX;
            double HalfMain(FlowchartLayoutNodeBounds Box) => (Vertical ? Box.Height : Box.Width) / 2;
            double HalfCross(FlowchartLayoutNodeBounds Box) => (Vertical ? Box.Width : Box.Height) / 2;
            double Offset = Math.Min(DiagramGridMetrics.GRID_UNIT_PX,
                Math.Floor((Math.Min(HalfCross(A), HalfCross(B)) - 12) / 15) * 15);
            if (Offset < 15 || Math.Abs(Main(A) - Main(B)) - HalfMain(A) - HalfMain(B) < 20) continue;
            Pair = Pair.OrderBy(Other => Vertical ? Other.Points[0].X : Other.Points[0].Y).ToList();
            var Candidates = new List<List<(double X, double Y)>>();
            bool Clear = true;
            for (int Index = 0; Index < Pair.Count; Index++)
            {
                var Other = Pair[Index]; var Source = Bounds[Other.SourceId]; var Target = Bounds[Other.TargetId];
                double Track = Cross(A) + (Index == 0 ? -Offset : Offset);
                int Sign = Math.Sign(Main(Target) - Main(Source));
                (double X, double Y) Map(double Along) => Vertical ? (Track, Along) : (Along, Track);
                var Candidate = new List<(double X, double Y)> { Map(Main(Source) + Sign * HalfMain(Source)),
                    Map(Main(Target) - Sign * HalfMain(Target)) };
                var Edge = Contract.Edges.First(Item => Item.Id == Other.EdgeId);
                var Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge);
                if (Obstacles.Any(Box => FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Candidate[0].X, Candidate[0].Y, Candidate[1].X, Candidate[1].Y, Box, -6))) Clear = false;
                foreach (var Peer in Layout.EdgeRoutes.Where(Peer => !Pair.Contains(Peer) && Peer.Points.Count >= 2))
                {
                    var Points = Peer.Points.Select(Point => (Point.X, Point.Y)).ToList();
                    var Before = FlowchartBranchRouteOrder.Intersections(Other.Points.Select(Point => (Point.X, Point.Y)).ToList(), Points);
                    var After = FlowchartBranchRouteOrder.Intersections(Candidate, Points);
                    if (After.Crossings > Before.Crossings || After.Overlap > Before.Overlap + 0.01) Clear = false;
                    foreach (var End in new[] { Points[0], Points[^1] })
                        if (Candidate.Any(Point => Math.Abs(Point.X - End.X) + Math.Abs(Point.Y - End.Y) < 6)) Clear = false;
                }
                Candidates.Add(Candidate);
            }
            if (!Clear) continue;
            for (int Index = 0; Index < Pair.Count; Index++)
            {
                var Other = Pair[Index]; Seen.Add(Other); Other.Points.Clear();
                foreach (var Point in Candidates[Index]) Other.Points.Add(new() { X = Point.X, Y = Point.Y });
                bool Positive = Vertical ? Other.Points[^1].Y > Other.Points[0].Y : Other.Points[^1].X > Other.Points[0].X;
                Other.ExitSide = Vertical ? (Positive ? "top" : "bottom") : (Positive ? "right" : "left");
                Other.EntrySide = Vertical ? (Positive ? "bottom" : "top") : (Positive ? "left" : "right");
            }
        }
    }
}
