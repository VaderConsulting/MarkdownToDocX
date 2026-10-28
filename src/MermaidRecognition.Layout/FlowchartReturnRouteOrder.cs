using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Moves return corridors and exchanges incident tracks when that removes crossings.</summary>
internal static class FlowchartReturnRouteOrder
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        List<FlowchartLayoutEdgeRoute> Routes = [.. Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
            && Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)
            && !Contract.Edges.Any(Edge => Edge.Id == Route.EdgeId
                && string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))];
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        HashSet<string> Rectangles = Contract.Nodes.Where(Node => Node.Shape is null or "" or "rect" or "rounded")
            .Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute? Route in Routes.Where(Route => Back.Any(Edge => Edge.Id == Route.EdgeId)))
        {
            if (!Rectangles.Contains(Route.SourceId) || !Rectangles.Contains(Route.TargetId))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Source = Bounds[Route.SourceId]; FlowchartLayoutNodeBounds Target = Bounds[Route.TargetId];
            List<(double X, double Y)> Old = Points(Route);
            foreach (bool Vertical in new[] { true, false })
            {
                double Across(FlowchartLayoutNodeBounds Box) => Vertical ? Box.CenterX : Box.CenterY;
                double Half(FlowchartLayoutNodeBounds Box) => (Vertical ? Box.Width : Box.Height) / 2;
                if (Math.Abs(Across(Source) - Across(Target)) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                {
                    continue;
                }

                foreach (int Sign in new[] { -1, 1 })
                {
                    double SourceSide = Across(Source) + (Sign * Half(Source));
                    double TargetSide = Across(Target) + (Sign * Half(Target));
                    (double, double) Start = Vertical ? (SourceSide, Old[0].Y) : (Old[0].X, SourceSide);
                    (double, double) End = Vertical ? (TargetSide, Old[^1].Y) : (Old[^1].X, TargetSide);
                    if (!PortFree(Route, true, Start) || !PortFree(Route, false, End))
                    {
                        continue;
                    }

                    double Track = (Sign < 0 ? Math.Min(SourceSide, TargetSide) : Math.Max(SourceSide, TargetSide))
                        + (Sign * 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
                    List<(double X, double Y)> Candidate = Vertical
                        ? [Start, (Track, Start.Item2), (Track, End.Item2), End]
                        : [Start, (Start.Item1, Track), (End.Item1, Track), End];
                    if (!Clear(Route, Candidate) || !Improves(new() { [Route] = Candidate }))
                    {
                        continue;
                    }

                    Replace(Route, Candidate); Old = Candidate;
                    Route.ExitSide = Route.EntrySide = Vertical ? Sign < 0 ? "left" : "right" : Sign < 0 ? "bottom" : "top";
                }
            }
        }

        // Incoming and outgoing edges can own adjacent ports on the same face.
        // Exchange their terminal tracks together; changing either alone creates a crossing.
        foreach (FlowchartLayoutNodeBounds? Node in Bounds.Values.OrderByDescending(PortSpread))
        {
            if (!Rectangles.Contains(Node.NodeId))
            {
                continue;
            }

            List<(FlowchartLayoutEdgeRoute Route, bool Start)> Ends = [.. Routes.SelectMany(Route => new[] { (Route, Start: true), (Route, Start: false) }).Where(End => (End.Start ? End.Route.SourceId : End.Route.TargetId) == Node.NodeId)];
            for (int First = 0; First < Ends.Count; First++)
            {
                for (int Second = First + 1; Second < Ends.Count; Second++)
            {
                    (FlowchartLayoutEdgeRoute Route, bool Start) A = Ends[First]; (FlowchartLayoutEdgeRoute Route, bool Start) B = Ends[Second];
                if (A.Route == B.Route || A.Start == B.Start)
                    {
                        continue;
                    }

                    List<(double X, double Y)> PA = Points(A.Route); List<(double X, double Y)> PB = Points(B.Route);
                    (double X, double Y) AP = A.Start ? PA[0] : PA[^1]; (double X, double Y) BP = B.Start ? PB[0] : PB[^1];
                    (double X, double Y) AN = A.Start ? PA[1] : PA[^2]; (double X, double Y) BN = B.Start ? PB[1] : PB[^2];
                bool Vertical = Math.Abs(AP.X - AN.X) < 0.01;
                if (Vertical != (Math.Abs(BP.X - BN.X) < 0.01)
                    || Math.Abs(Vertical ? AP.Y - BP.Y : AP.X - BP.X) > 0.01
                    || Math.Sign(Vertical ? AN.Y - AP.Y : AN.X - AP.X)
                        != Math.Sign(Vertical ? BN.Y - BP.Y : BN.X - BP.X))
                    {
                        continue;
                    }

                    if (FlowchartBranchRouteOrder.Intersections(PA, PB).Crossings == 0)
                    {
                        continue;
                    }

                    ShiftTerminal(PA, A.Start, Vertical, Vertical ? BP.X : BP.Y);
                ShiftTerminal(PB, B.Start, Vertical, Vertical ? AP.X : AP.Y);
                if (!Clear(A.Route, PA) || !Clear(B.Route, PB)
                    || !Improves(new() { [A.Route] = PA, [B.Route] = PB }))
                    {
                        continue;
                    }

                    Replace(A.Route, PA); Replace(B.Route, PB);
            }
            }
        }

        double PortSpread(FlowchartLayoutNodeBounds Node)
        {
            IEnumerable<(string Id, FlowchartLayoutPoint Point, FlowchartLayoutPoint Next)> Ports = Routes.SelectMany(Route => new[] {
                (Id: Route.SourceId, Point: Route.Points[0], Next: Route.Points[1]),
                (Id: Route.TargetId, Point: Route.Points[^1], Next: Route.Points[^2]) })
                .Where(Port => Port.Id == Node.NodeId);
            return Ports.GroupBy(Port =>
            {
                bool Vertical = Math.Abs(Port.Point.X - Port.Next.X) < 0.01;
                return (Vertical, Along: Vertical ? Port.Point.Y : Port.Point.X,
                    Sign: Math.Sign(Vertical ? Port.Next.Y - Port.Point.Y : Port.Next.X - Port.Point.X));
            }).Select(Group => Group.Max(Port => Group.Key.Vertical ? Port.Point.X : Port.Point.Y)
                - Group.Min(Port => Group.Key.Vertical ? Port.Point.X : Port.Point.Y)).DefaultIfEmpty().Max();
        }

        bool PortFree(FlowchartLayoutEdgeRoute Owner, bool Start, (double X, double Y) Point) =>
            Routes.Where(Route => Route != Owner).All(Route =>
                (Route.SourceId != (Start ? Owner.SourceId : Owner.TargetId)
                    || Distance(Point, (Route.Points[0].X, Route.Points[0].Y)) >= 6)
                && (Route.TargetId != (Start ? Owner.SourceId : Owner.TargetId)
                    || Distance(Point, (Route.Points[^1].X, Route.Points[^1].Y)) >= 6));

        bool Clear(FlowchartLayoutEdgeRoute Route, List<(double X, double Y)> Path)
        {
            EdgeContract? Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
            if (Edge == null)
            {
                return false;
            }

            List<FlowchartLayoutNodeBounds> Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge);
            if (Contract.Groups.Any(Group => Bounds.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? Frame)
                && FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.SourceId)
                && FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.TargetId)
                && Path.Any(Point => Point.X < Frame.CenterX - (Frame.Width / 2) || Point.X > Frame.CenterX + (Frame.Width / 2)
                    || Point.Y < Frame.CenterY - (Frame.Height / 2) || Point.Y > Frame.CenterY + (Frame.Height / 2))))
            {
                return false;
            }

            return Path.Zip(Path.Skip(1)).All(Pair =>
                (Math.Abs(Pair.First.X - Pair.Second.X) < 0.01 || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.01)
                && Obstacles.All(Box => !FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box, -DiagramGridRouteBuilder.CORNER_RADIUS_PX)));
        }

        bool Improves(Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> Candidates)
        {
            int Before = 0, After = 0;
            double OldOverlap = 0, NewOverlap = 0;
            for (int First = 0; First < Routes.Count; First++)
            {
                for (int Second = First + 1; Second < Routes.Count; Second++)
            {
                    FlowchartLayoutEdgeRoute A = Routes[First]; FlowchartLayoutEdgeRoute B = Routes[Second];
                if (!Candidates.ContainsKey(A) && !Candidates.ContainsKey(B))
                    {
                        continue;
                    }

                    (int Crossings, double Overlap) Old = FlowchartBranchRouteOrder.Intersections(Points(A), Points(B));
                    (int Crossings, double Overlap) New = FlowchartBranchRouteOrder.Intersections(Candidates.GetValueOrDefault(A) ?? Points(A),
                    Candidates.GetValueOrDefault(B) ?? Points(B));
                if (New.Crossings > Old.Crossings || New.Overlap > Old.Overlap + 0.01)
                    {
                        return false;
                    }

                    Before += Old.Crossings; After += New.Crossings;
                OldOverlap += Old.Overlap; NewOverlap += New.Overlap;
            }
            }

            return After < Before && NewOverlap <= OldOverlap + 0.01;
        }
    }

    private static void ShiftTerminal(List<(double X, double Y)> Points, bool Start, bool Vertical, double Track)
    {
        int Index = Start ? 0 : Points.Count - 1, Step = Start ? 1 : -1;
        double Old = Vertical ? Points[Index].X : Points[Index].Y;
        for (; Index >= 0 && Index < Points.Count; Index += Step)
        {
            (double X, double Y) Point = Points[Index];
            if (Math.Abs((Vertical ? Point.X : Point.Y) - Old) > 0.01)
            {
                break;
            }

            Points[Index] = Vertical ? (Track, Point.Y) : (Point.X, Track);
        }
    }

    private static double Distance((double X, double Y) A, (double X, double Y) B) => Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
    private static List<(double X, double Y)> Points(FlowchartLayoutEdgeRoute Route) => [.. Route.Points.Select(Point => (Point.X, Point.Y))];
    private static void Replace(FlowchartLayoutEdgeRoute Route, List<(double X, double Y)> Points)
    {
        Route.Points.Clear();
        foreach ((double X, double Y) Point in Points)
        {
            Route.Points.Add(new() { X = Point.X, Y = Point.Y });
        }
    }
}
