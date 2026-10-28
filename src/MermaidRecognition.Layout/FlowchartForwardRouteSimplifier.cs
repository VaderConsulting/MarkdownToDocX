using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Shortens forward approaches without adding crossings or sharing ports.</summary>
internal static class FlowchartForwardRouteSimplifier
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes, FlowchartDirection Direction)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        double Sign = Direction is FlowchartDirection.RightToLeft or FlowchartDirection.TopToBottom ? -1 : 1;
        (double U, double V) Local(double X, double Y) => Horizontal ? (Y, Sign * X) : (X, Sign * Y);
        (double X, double Y) World(double U, double V) => Horizontal ? (Sign * V, U) : (U, Sign * V);
        string Side(int U, int V) => Horizontal
            ? (U != 0 ? (U > 0 ? "top" : "bottom") : (V * Sign > 0 ? "right" : "left"))
            : (U != 0 ? (U > 0 ? "right" : "left") : (V * Sign > 0 ? "top" : "bottom"));
        bool Box(string Id) => Nodes.TryGetValue(Id, out NodeContract? Node)
            && Node.Shape is null or "" or "rect" or "rounded" or "round";
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 4 || !Box(Route.SourceId) || !Box(Route.TargetId)
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source) || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)
                || Back.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId))
            {
                continue;
            }

            (double U, double V) S = Local(Source.CenterX, Source.CenterY); (double U, double V) T = Local(Target.CenterX, Target.CenterY);
            double Sw = (Horizontal ? Source.Height : Source.Width) / 2;
            double Sh = (Horizontal ? Source.Width : Source.Height) / 2;
            double Tw = (Horizontal ? Target.Height : Target.Width) / 2;
            double Th = (Horizontal ? Target.Width : Target.Height) / 2;
            if (T.V - Th <= S.V + Sh)
            {
                continue;
            }

            List<(double X, double Y)> Best = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
            string? BestExit = Route.ExitSide, BestEntry = Route.EntrySide;
            bool OrdinaryFan = FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                Contract, Route.SourceId, Nodes[Route.SourceId].Shape, Direction, Bounds);
            List<EdgeContract> Outgoing = [.. Contract.Edges.Where(Edge => Edge.SourceId == Route.SourceId
                && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                && Bounds.ContainsKey(Edge.TargetId))];
            List<FlowchartLayoutNodeBounds> Children = [.. Outgoing.Select(Edge => Bounds[Edge.TargetId])];
            bool CollectorFan = Children.Count >= 2 && Children.Select(Child => Local(Child.CenterX, Child.CenterY).V).Distinct().Count() == 1
                && Children.All(Child => !Contract.Edges.Any(Edge => Edge.SourceId == Child.NodeId))
                && Children.Any(Child => Contract.Edges.Count(Edge => Edge.TargetId == Child.NodeId) == 1)
                && Children.Any(Child => Contract.Edges.Count(Edge => Edge.TargetId == Child.NodeId) > 1);
            OrdinaryFan |= Children.Count >= 2 && Children.Select(Child =>
                Local(Child.CenterX, Child.CenterY).V).Distinct().Count() == 1;
            // A fan that already has at most two bends owns its allocated
            // tracks and ports. Only simplify its longer detours.
            if (OrdinaryFan && Route.Points.Count <= 4 && !CollectorFan)
            {
                continue;
            }

            if (OrdinaryFan && Math.Abs(T.U - S.U) > Sw + Tw
                && Route.ExitSide == Side(Math.Sign(T.U - S.U), 0) && !CollectorFan)
            {
                continue;
            }
            // Independent branches at different ranks retain their existing
            // fan allocation. A chain bypass has dependent children instead.
            if (!OrdinaryFan && Route.Points.Count <= 4 && Children.Count > 1
                && !FlowchartOrdinaryFanOutRouteBuilder.HasForwardPathBetweenTargets(Contract, Outgoing))
            {
                continue;
            }

            bool SkipsRank = Bounds.Values.Any(Obstacle =>
                Local(Obstacle.CenterX, Obstacle.CenterY).V > S.V + Sh
                && Local(Obstacle.CenterX, Obstacle.CenterY).V < T.V - Th);
            bool DownstreamTaken = Layout.EdgeRoutes.Any(Other => Other != Route && Other.SourceId == Route.SourceId
                && Other.Points.Count == 2 && Other.ExitSide == Side(0, 1));
            bool UpstreamTaken = Layout.EdgeRoutes.Any(Other => Other != Route && Other.TargetId == Route.TargetId
                && Other.Points.Count == 2 && Other.EntrySide == Side(0, -1));
            foreach (int Offset in new[] { 0, -1, 1, -2, 2 })
            {
                foreach (int TargetOffset in new[] { 0, -1, 1, -2, 2 })
            {
                double Su = S.U + (Offset * Sw / 3), Tu = T.U + (TargetOffset * Tw / 3);
                if (Sw - Math.Abs(Su - S.U) < 20 || Tw - Math.Abs(Tu - T.U) < 20)
                    {
                        continue;
                    }
                    // Flow-facing ports allow a Z clear of the central chain.
                    foreach (double Track in new[] { S.V + Sh + 40, T.V - Th - 40, (S.V + Sh + T.V - Th) / 2 })
                    {
                        Consider([(Su, S.V + Sh), (Su, Track), (Tu, Track), (Tu, T.V - Th)], Side(0, 1), Side(0, -1));
                    }

                    int Facing = Math.Sign(T.U - S.U);
                double SideStart = S.V + (Offset * Sh / 3);
                if (!OrdinaryFan && DownstreamTaken && Sh - Math.Abs(SideStart - S.V) >= 20
                    && Facing != 0 && Math.Abs(T.U - S.U) > Sw + Tw)
                    {
                        Consider([(S.U + (Facing * Sw), SideStart), (Tu, SideStart), (Tu, T.V - Th)], Side(Facing, 0), Side(0, -1));
                    }

                    if ((!OrdinaryFan || CollectorFan) && UpstreamTaken)
                {
                    int Near = Math.Sign(S.U - T.U);
                    double Tv = T.V + (TargetOffset * Th / 3);
                    if (Near != 0 && Near * (Su - T.U - (Near * Tw)) >= 40
                        && Th - Math.Abs(Tv - T.V) >= 20)
                        {
                            Consider([(Su, S.V + Sh), (Su, Tv), (T.U + (Near * Tw), Tv)], Side(0, 1), Side(Near, 0));
                        }
                    }
                if (OrdinaryFan || !SkipsRank)
                    {
                        continue;
                    }

                    foreach (int Outside in new[] { -1, 1 })
                {
                    double Track = Outside < 0 ? Math.Min(S.U - Sw, T.U - Tw) - 40 : Math.Max(S.U + Sw, T.U + Tw) + 40;
                    foreach (FlowchartLayoutNodeBounds Obstacle in Bounds.Values)
                    {
                            (double U, double V) C = Local(Obstacle.CenterX, Obstacle.CenterY);
                        double W = (Horizontal ? Obstacle.Height : Obstacle.Width) / 2;
                        double H = (Horizontal ? Obstacle.Width : Obstacle.Height) / 2;
                        if (C.V + H < S.V || C.V - H > T.V)
                            {
                                continue;
                            }

                            Track = Outside < 0 ? Math.Min(Track, C.U - W - 40) : Math.Max(Track, C.U + W + 40);
                    }
                    double Sv = S.V + (Offset * Sh / 3), Tv = T.V + (TargetOffset * Th / 3);
                    if (Sh - Math.Abs(Sv - S.V) >= 20 && Th - Math.Abs(Tv - T.V) >= 20)
                        {
                            Consider([(S.U + (Outside * Sw), Sv), (Track, Sv), (Track, Tv), (T.U + (Outside * Tw), Tv)], Side(Outside, 0), Side(Outside, 0));
                        }
                        // A free downstream port can feed the exterior side of a later node.
                        double Turn = S.V + Sh + 40;
                    if (Th - Math.Abs(Tv - T.V) >= 20 && Outside * (Su - T.U - (Outside * Tw)) > 40)
                        {
                            Consider([(Su, S.V + Sh), (Su, Tv), (T.U + (Outside * Tw), Tv)], Side(0, 1), Side(Outside, 0));
                        }

                        if (Th - Math.Abs(Tv - T.V) >= 20)
                        {
                            Consider([(Su, S.V + Sh), (Su, Turn), (Track, Turn), (Track, Tv), (T.U + (Outside * Tw), Tv)], Side(0, 1), Side(Outside, 0));
                        }
                    }
            }
            }

            if (CollectorFan && Contract.Edges.Count(Edge => Edge.TargetId == Route.TargetId) == 1)
            {
                int Facing = Math.Sign(T.U - S.U);
                double StartU = S.U + (Facing * Sw), EndU = T.U - (Facing * Tw);
                double Track = (StartU + EndU) / 2;
                if (Facing != 0 && Facing * (EndU - StartU) >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX)
                {
                    Consider([(StartU, S.V), (Track, S.V), (Track, T.V), (EndU, T.V)],
                        Side(Facing, 0), Side(-Facing, 0), DiagramGridRouteBuilder.CORNER_RADIUS_PX);
                }
            }
            Route.ExitSide = BestExit; Route.EntrySide = BestEntry;
            Route.Points.Clear();
            foreach ((double X, double Y) Point in Best)
            {
                Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }

            void Consider(List<(double U, double V)> LocalPath, string Exit, string Entry, double MinimumRun = 40)
            {
                List<(double X, double Y)> Path = [.. LocalPath.Select(Point => World(Point.U, Point.V))];
                if (Path.Count > Best.Count)
                {
                    return;
                }

                for (int Index = 1; Index < Path.Count; Index++)
                {
                    (double X, double Y) A = Path[Index - 1]; (double X, double Y) B = Path[Index];
                    if (Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y) < MinimumRun)
                    {
                        return;
                    }

                    if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Source)
                        || FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Target))
                    {
                        return;
                    }

                    foreach (FlowchartLayoutNodeBounds Obstacle in Bounds.Values)
                    {
                        if (Obstacle.NodeId == Route.SourceId || Obstacle.NodeId == Route.TargetId)
                        {
                            continue;
                        }

                        if (Math.Max(A.X, B.X) > Obstacle.CenterX - (Obstacle.Width / 2) - 6
                            && Math.Min(A.X, B.X) < Obstacle.CenterX + (Obstacle.Width / 2) + 6
                            && Math.Max(A.Y, B.Y) > Obstacle.CenterY - (Obstacle.Height / 2) - 6
                            && Math.Min(A.Y, B.Y) < Obstacle.CenterY + (Obstacle.Height / 2) + 6)
                        {
                            return;
                        }
                    }
                }
                int Before = 0, After = 0;
                foreach (FlowchartLayoutEdgeRoute? Other in Layout.EdgeRoutes.Where(Other => Other != Route && Other.Points.Count >= 2))
                {
                    foreach (FlowchartLayoutPoint? End in new[] { Other.Points[0], Other.Points[^1] })
                    {
                        if (Math.Abs(End.X - Path[0].X) + Math.Abs(End.Y - Path[0].Y) < 6
                            || Math.Abs(End.X - Path[^1].X) + Math.Abs(End.Y - Path[^1].Y) < 6)
                        {
                            return;
                        }
                    }

                    List<(double X, double Y)> Points = [.. Other.Points.Select(Point => (Point.X, Point.Y))];
                    (int Crossings, double Overlap) Old = FlowchartBranchRouteOrder.Intersections(Best, Points);
                    (int Crossings, double Overlap) New = FlowchartBranchRouteOrder.Intersections(Path, Points);
                    if (New.Crossings > Old.Crossings || New.Overlap > Old.Overlap + 0.01)
                    {
                        return;
                    }

                    Before += Old.Crossings; After += New.Crossings;
                }
                double Length(List<(double X, double Y)> Points) => Points.Zip(Points.Skip(1))
                    .Sum(Pair => Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y));
                double PortOffset(List<(double X, double Y)> Points, string? ExitSide, string? EntrySide) =>
                    (ExitSide is "left" or "right" ? Math.Abs(Points[0].Y - Source.CenterY) : Math.Abs(Points[0].X - Source.CenterX))
                    + (EntrySide is "left" or "right" ? Math.Abs(Points[^1].Y - Target.CenterY) : Math.Abs(Points[^1].X - Target.CenterX));
                if (After == Before && Path.Count == Best.Count)
                {
                    double OffsetCost = PortOffset(Path, Exit, Entry) - PortOffset(Best, BestExit, BestEntry);
                    if (OffsetCost > 0.01 || (Math.Abs(OffsetCost) < 0.01 && Length(Path) >= Length(Best) - 0.01))
                    {
                        return;
                    }
                }
                Best = Path; BestExit = Exit; BestEntry = Entry;
            }
        }
    }
}
