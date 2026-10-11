using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Moves a crossed terminal fan branch to a clear track beyond the terminal row.</summary>
internal static class FlowchartTerminalFanBypass
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        if (Contract.Groups.Count > 0) return;
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        bool Box(string Id) => Contract.Nodes.Any(Node => Node.Id == Id && Node.Shape is null or "" or "rect" or "rounded" or "round");
        foreach (var Route in Layout.EdgeRoutes)
        {
            var Edge = Visible.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
            if (Edge == null || !string.IsNullOrEmpty(Edge.Label) || Route.Points.Count < 2
                || !Box(Route.SourceId) || !Box(Route.TargetId)
                || !Bounds.TryGetValue(Route.SourceId, out var Source) || !Bounds.TryGetValue(Route.TargetId, out var Target)
                || Visible.Count(Edge => Edge.SourceId == Route.SourceId) < 2
                || Visible.Count(Edge => Edge.TargetId == Route.TargetId) < 2
                || Visible.Any(Edge => Edge.SourceId == Route.TargetId)) continue;
            var Peers = Layout.EdgeRoutes.Where(Peer => Peer != Route && Peer.Points.Count >= 2)
                .Select(Peer => Peer.Points.Select(Point => (Point.X, Point.Y)).ToList()).ToList();
            var Original = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
            if (!Peers.Any(Peer => FlowchartBranchRouteOrder.Intersections(Original, Peer).Crossings > 0)) continue;
            List<(double X, double Y)>? Best = null;
            double BestLength = double.PositiveInfinity;
            foreach (bool Horizontal in new[] { false, true })
            {
                double Main(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterX : Node.CenterY;
                double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
                double HalfMain(FlowchartLayoutNodeBounds Node) => (Horizontal ? Node.Width : Node.Height) / 2;
                double HalfCross(FlowchartLayoutNodeBounds Node) => (Horizontal ? Node.Height : Node.Width) / 2;
                (double X, double Y) World(double Main, double Cross) => Horizontal ? (Main, Cross) : (Cross, Main);
                int Flow = Math.Sign(Main(Target) - Main(Source));
                if (Flow == 0 || Flow * (Main(Target) - Main(Source)) <= HalfMain(Source) + HalfMain(Target) + 20) continue;
                double Outer = Bounds.Values.Max(Node => Flow * Main(Node) + HalfMain(Node));
                foreach (int Lane in new[] { 0, 1, 2 })
                foreach (int SourceOffset in new[] { 0, -1, 1 })
                foreach (int TargetOffset in new[] { 0, -1, 1 })
                {
                    double Su = Cross(Source) + SourceOffset * DiagramGridMetrics.GRID_UNIT_PX;
                    double Tu = Cross(Target) + TargetOffset * DiagramGridMetrics.GRID_UNIT_PX;
                    if (HalfCross(Source) - Math.Abs(Su - Cross(Source)) < 20
                        || HalfCross(Target) - Math.Abs(Tu - Cross(Target)) < 20) continue;
                    double Track = Flow * (Outer + 40 + Lane * DiagramGridMetrics.GRID_UNIT_PX);
                    var Candidate = new List<(double X, double Y)> { World(Main(Source) + Flow * HalfMain(Source), Su),
                        World(Track, Su), World(Track, Tu), World(Main(Target) + Flow * HalfMain(Target), Tu) };
                    bool Clear = Candidate.Zip(Candidate.Skip(1)).All(Segment =>
                        Math.Abs(Segment.First.X - Segment.Second.X) + Math.Abs(Segment.First.Y - Segment.Second.Y) >= 40
                        && !Bounds.Values.Any(Node => FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                            Segment.First.X, Segment.First.Y, Segment.Second.X, Segment.Second.Y, Node)));
                    foreach (var Peer in Peers)
                    {
                        var Crossings = FlowchartBranchRouteOrder.Intersections(Candidate, Peer);
                        if (Crossings.Crossings > 0 || Crossings.Overlap > 0.01
                            || new[] { Peer[0], Peer[^1] }.Any(End => new[] { Candidate[0], Candidate[^1] }.Any(Point =>
                                Math.Abs(End.X - Point.X) + Math.Abs(End.Y - Point.Y) < 12))) Clear = false;
                    }
                    double Length = Candidate.Zip(Candidate.Skip(1)).Sum(Segment =>
                        Math.Abs(Segment.First.X - Segment.Second.X) + Math.Abs(Segment.First.Y - Segment.Second.Y));
                    if (Clear && Length < BestLength) { Best = Candidate; BestLength = Length; }
                }
            }
            if (Best == null) continue;
            Route.Points.Clear();
            foreach (var Point in Best) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
        }
    }
}
