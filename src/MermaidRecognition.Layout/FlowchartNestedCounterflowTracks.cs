using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Reserves distinct tracks for opposing container and descendant connectors.</summary>
internal static class FlowchartNestedCounterflowTracks
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        List<FlowchartLayoutEdgeRoute> Routes = [.. Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
            && Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)
            && !Contract.Edges.Any(Edge => Edge.Id == Route.EdgeId
                && string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))];
        foreach (FlowchartLayoutEdgeRoute? Route in Routes)
        {
            List<(double X, double Y)> Old = Points(Route);
            bool Vertical = Old.All(Point => Math.Abs(Point.X - Old[0].X) < 0.01);
            if (!Vertical && !Old.All(Point => Math.Abs(Point.Y - Old[0].Y) < 0.01))
            {
                continue;
            }

            double Cross((double X, double Y) Point) => Vertical ? Point.X : Point.Y;
            double Along((double X, double Y) Point) => Vertical ? Point.Y : Point.X;
            List<(FlowchartLayoutEdgeRoute Route, List<(double X, double Y)> Points)> Peers = [.. Routes.Where(Other => Other != Route
                && FlowchartCompoundEndpointHelper.Contains(Contract, Route.SourceId, Other.TargetId)
                && FlowchartCompoundEndpointHelper.Contains(Contract, Route.TargetId, Other.SourceId))
                .Select(Other => (Route: Other, Points: Points(Other)))
                .Where(Other => Other.Points.All(Point => Math.Abs(Cross(Point) - Cross(Other.Points[0])) < 0.01)
                    && (Along(Old[^1]) - Along(Old[0])) * (Along(Other.Points[^1]) - Along(Other.Points[0])) < 0
                    && Math.Max(Along(Old[0]), Along(Old[^1])) > Math.Min(Along(Other.Points[0]), Along(Other.Points[^1]))
                    && Math.Min(Along(Old[0]), Along(Old[^1])) < Math.Max(Along(Other.Points[0]), Along(Other.Points[^1])))];
            if (Peers.Count == 0)
            {
                continue;
            }

            double Gutter(FlowchartLayoutEdgeRoute Peer)
            {
                var Label = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Peer.EdgeId)?.Label ?? string.Empty;
                FlowchartLabelDimensions Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Label, Options, Options.EdgeLabelFontSizePx);
                return string.IsNullOrWhiteSpace(Label) ? 6 : ((Vertical ? Size.Width : Size.Height) / 2) + 12;
            }
            // Prefer the positive cross-axis side for returns, beyond every inner label gutter.
            double Positive = Peers.Max(Peer => Cross(Peer.Points[0]) + Gutter(Peer.Route));
            double Negative = Peers.Min(Peer => Cross(Peer.Points[0]) - Gutter(Peer.Route));
            if (Cross(Old[0]) >= Positive)
            {
                continue;
            }

            FlowchartLayoutNodeBounds Source = Bounds[Route.SourceId]; FlowchartLayoutNodeBounds Target = Bounds[Route.TargetId];
            foreach (double Track in new[] { Positive, Negative })
            {
                double Anchor = Cross(Peers[0].Points[0]);
                double Position = Anchor + (Math.Sign(Track - Anchor)
                    * DiagramGridMetrics.CeilToGrid(Math.Abs(Track - Anchor)));
                double Centre(FlowchartLayoutNodeBounds Box) => Vertical ? Box.CenterX : Box.CenterY;
                double Half(FlowchartLayoutNodeBounds Box) => (Vertical ? Box.Width : Box.Height) / 2;
                if (Math.Abs(Position - Centre(Source)) > Half(Source) - 20
                    || Math.Abs(Position - Centre(Target)) > Half(Target) - 20)
                {
                    continue;
                }

                List<(double X, double Y)> Candidate = [.. Old.Select(Point => Vertical ? (X: Position, Y: Point.Y) : (X: Point.X, Y: Position))];
                if (Routes.Where(Other => Other != Route).Any(Other =>
                {
                    List<(double X, double Y)> Path = Points(Other);
                    (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(Old, Path);
                    (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Candidate, Path);
                    return After.Crossings > Before.Crossings || After.Overlap > Before.Overlap + 0.01
                        || (Other.SourceId == Route.SourceId && Distance(Candidate[0], Path[0]) < 6)
                        || (Other.TargetId == Route.TargetId && Distance(Candidate[^1], Path[^1]) < 6)
                        || (Other.TargetId == Route.SourceId && Distance(Candidate[0], Path[^1]) < 6)
                        || (Other.SourceId == Route.TargetId && Distance(Candidate[^1], Path[0]) < 6);
                }))
                {
                    continue;
                }

                EdgeContract? Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
                if (Edge == null)
                {
                    continue;
                }

                List<FlowchartLayoutNodeBounds> Obstacles = [.. FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge)
                    .Where(Box => !FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.SourceId)
                        && !FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.TargetId))];
                if (Candidate.Zip(Candidate.Skip(1)).Any(Pair => Obstacles.Any(Box =>
                    FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                        Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box, -6))))
                {
                    continue;
                }

                Route.Points.Clear();
                foreach ((double X, double Y) Point in Candidate)
                {
                    Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                }

                break;
            }
        }
    }

    private static List<(double X, double Y)> Points(FlowchartLayoutEdgeRoute Route) => [.. Route.Points.Select(Point => (Point.X, Point.Y))];
    private static double Distance((double X, double Y) A, (double X, double Y) B) => Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
}
