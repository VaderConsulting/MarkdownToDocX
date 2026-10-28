using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Uses free facing ports to shorten routes between nested groups and outside connectors.</summary>
internal static class FlowchartCrossGroupRouteSimplifier
{
    internal static Action<string, double, bool>? RouteTimingObserver { get; set; }
    internal static Action<string, int, double, int, double, int, double>? RouteEvaluationObserver { get; set; }

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        HashSet<string>? AttemptedRouteStates = null)
    {
        AttemptedRouteStates ??= new(StringComparer.Ordinal);
        FlowchartRouteSpatialIndex Spatial = new(Layout);
        List<GroupContract> Roots = [.. Contract.Groups.Where(Group => !Contract.Groups.Any(Other => Other.Id != Group.Id
            && FlowchartCompoundEndpointHelper.Contains(Contract, Other.Id, Group.Id)))];
        if (Roots.Count == 0)
        {
            return;
        }

        string? Owner(string Id) => Roots.FirstOrDefault(Group => Group.Id == Id
            || FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Id))?.Id;
        string? Parent(string Id) => Contract.Groups.FirstOrDefault(Group => Group.ChildNodeIds.Contains(Id)
            || Group.ChildGroupIds.Contains(Id))?.Id;
        Queue<FlowchartLayoutEdgeRoute> Pending = new(Layout.EdgeRoutes);
        HashSet<FlowchartLayoutEdgeRoute> Queued = new(Pending);
        Dictionary<string, (bool Found, List<(double X, double Y)> Points)> SearchCache = new(StringComparer.Ordinal);
        while (Pending.Count > 0)
        {
            FlowchartLayoutEdgeRoute Route = Pending.Dequeue();
            Queued.Remove(Route);
            Stopwatch? RouteTimer = RouteTimingObserver == null ? null : Stopwatch.StartNew();
            bool RouteChanged = false;
            double CandidateGenerationMs = 0, CandidateEvaluationMs = 0, SearchMs = 0, RescoreMs = 0;
            int CandidateCount = 0, SearchCount = 0, RescoreCount = 0;
            try
            {
            if (Route.Points.Count < 2 || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
                {
                    continue;
                }

                string AttemptKey = RouteKey(Route) + "|" + Spatial.DependencySignature(Route.Points);
            if (!AttemptedRouteStates.Add(AttemptKey))
                {
                    continue;
                }

                EdgeContract? Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
            if (Edge == null || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? AOwner = Owner(Route.SourceId), BOwner = Owner(Route.TargetId);
            // Direct members of partner groups already own their ordered tracks.
            if (AOwner != null && BOwner != null && AOwner != BOwner
                && Contract.Groups.First(Group => Group.Id == AOwner).ChildNodeIds.Count > 1
                && Contract.Groups.First(Group => Group.Id == BOwner).ChildNodeIds.Count > 1
                && Contract.Groups.First(Group => Group.Id == AOwner).ChildNodeIds.Contains(Route.SourceId)
                && Contract.Groups.First(Group => Group.Id == BOwner).ChildNodeIds.Contains(Route.TargetId))
                {
                    continue;
                }

                bool SelfCut = Route.Points.Zip(Route.Points.Skip(1)).Any(Pair =>
                FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Source)
                || FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Target));
            if (Contract.Nodes.Any(Node => (Node.Id == Route.SourceId || Node.Id == Route.TargetId)
                && Node.Shape is not (null or "" or "rect" or "rounded" or "round" or "note")))
                {
                    continue;
                }

                List<FlowchartLayoutNodeBounds> Obstacles = [.. FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge)
                .Where(Box => !Contract.Groups.Any(Group => Group.Id == Box.NodeId
                    && (FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.SourceId)
                        || FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.TargetId))))];
                List<(double X, double Y)> Best = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
            bool Diagonal = Best.Zip(Best.Skip(1)).Any(Pair => Math.Abs(Pair.First.X - Pair.Second.X) > 0.5
                && Math.Abs(Pair.First.Y - Pair.Second.Y) > 0.5);
            bool Blocked = Best.Zip(Best.Skip(1)).Any(Pair => Obstacles.Any(Box =>
                FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y,
                    Pair.Second.X, Pair.Second.Y, Box)));
            bool WrongSelectedSides = !SelectedSidesMatch(Best, Source, Target, Route.ExitSide, Route.EntrySide);
            if (AOwner == BOwner && Parent(Route.SourceId) == Parent(Route.TargetId) && !SelfCut)
                {
                    continue;
                }

                double BestCost = SelfCut || Diagonal || Blocked || WrongSelectedSides || CountCorners(Best) > 2
                ? double.PositiveInfinity : Cost(Best);
            Stopwatch? CandidateTimer = RouteEvaluationObserver == null ? null : Stopwatch.StartNew();
            foreach (bool Transpose in new[] { false, true })
            {
                double U(FlowchartLayoutNodeBounds Box) => Transpose ? Box.CenterY : Box.CenterX;
                double V(FlowchartLayoutNodeBounds Box) => Transpose ? Box.CenterX : Box.CenterY;
                double W(FlowchartLayoutNodeBounds Box) => (Transpose ? Box.Height : Box.Width) / 2;
                double H(FlowchartLayoutNodeBounds Box) => (Transpose ? Box.Width : Box.Height) / 2;
                (double X, double Y) World(double U, double V) => Transpose ? (V, U) : (U, V);
                int Sign = Math.Sign(U(Target) - U(Source));
                if (Sign == 0 || Math.Abs(U(Target) - U(Source)) < W(Source) + W(Target) + 20)
                    {
                        continue;
                    }

                    double StartU = U(Source) + (Sign * W(Source)), EndU = U(Target) - (Sign * W(Target));
                var SourceVs = new[] { V(Source), V(Source) - (H(Source) / 3), V(Source) + (H(Source) / 3) };
                var TargetVs = new[] { V(Target), V(Target) - (H(Target) / 3), V(Target) + (H(Target) / 3),
                    V(Target) - H(Target) + 20, V(Target) + H(Target) - 20 };
                foreach (int Outside in new[] { -1, 1 })
                {
                    double Sv = V(Source) + (Outside * H(Source)), Tv = V(Target) + (Outside * H(Target));
                    double NearTrack = (Outside < 0 ? Math.Min(Sv, Tv) : Math.Max(Sv, Tv)) + (Outside * 40);
                    double OuterTrack = Outside < 0
                        ? Obstacles.Select(Box => V(Box) - H(Box)).Append(Math.Min(Sv, Tv)).Min() - 40
                        : Obstacles.Select(Box => V(Box) + H(Box)).Append(Math.Max(Sv, Tv)).Max() + 40;
                    foreach (double Track in new[] { NearTrack, OuterTrack })
                        {
                            foreach (int SourceOffset in new[] { 0, -1, 1 })
                            {
                                foreach (int TargetOffset in new[] { 0, -1, 1 })
                    {
                        double Su = U(Source) + (SourceOffset * W(Source) / 3);
                        double Tu = U(Target) + (TargetOffset * W(Target) / 3);
                        Consider([World(Su, Sv), World(Su, Track), World(Tu, Track), World(Tu, Tv)]);
                    }
                            }
                        }
                    }
                foreach (double Line in SourceVs.Concat(TargetVs))
                    {
                        if (Math.Abs(Line - V(Source)) <= H(Source) - 20 && Math.Abs(Line - V(Target)) <= H(Target) - 20)
                        {
                            Consider([World(StartU, Line), World(EndU, Line)]);
                        }
                    }

                    foreach (double Sv in SourceVs)
                    {
                        foreach (double Tv in TargetVs)
                {
                    foreach (double Track in new[] { (StartU + EndU) / 2, StartU + (Sign * 40), EndU - (Sign * 40) })
                            {
                                Consider([World(StartU, Sv), World(Track, Sv), World(Track, Tv), World(EndU, Tv)]);
                            }

                            int SourceNear = Math.Sign(Tv - V(Source)), TargetNear = Math.Sign(Sv - V(Target));
                    if (TargetNear != 0)
                            {
                                Consider([World(StartU, Sv), World(U(Target), Sv), World(U(Target), V(Target) + (TargetNear * H(Target)))]);
                            }

                            if (SourceNear != 0)
                            {
                                Consider([World(U(Source), V(Source) + (SourceNear * H(Source))), World(U(Source), Tv), World(EndU, Tv)]);
                            }
                        }
                    }
                    // A deep container may need an outer corridor and a separate
                    // approach track to avoid a neighbouring service at its end.
                    foreach (int SourceSide in new[] { -1, 1 })
                    {
                        foreach (int TargetSide in new[] { -1, 1 })
                        {
                            foreach (double Sv in SourceVs)
                            {
                                foreach (int Offset in new[] { 0, -1, 1 })
                                {
                                    foreach (int TrackIndex in new[] { 0, 1, 2 })
                {
                    double Outer = SourceSide < 0
                        ? Obstacles.Select(Box => U(Box) - W(Box)).Append(U(Source) - W(Source)).Min() - 40 - (TrackIndex * DiagramGridMetrics.GRID_UNIT_PX)
                        : Obstacles.Select(Box => U(Box) + W(Box)).Append(U(Source) + W(Source)).Max() + 40 + (TrackIndex * DiagramGridMetrics.GRID_UNIT_PX);
                    double Tu = U(Target) + (Offset * W(Target) / 3);
                    double Tv = V(Target) + (TargetSide * H(Target));
                    double Approach = Tv + (TargetSide * 40);
                    Consider([World(U(Source) + (SourceSide * W(Source)), Sv), World(Outer, Sv),
                        World(Outer, Approach), World(Tu, Approach), World(Tu, Tv)]);
                }
                                }
                            }
                        }
                    }
                }
            if (CandidateTimer != null)
            {
                CandidateTimer.Stop();
                CandidateGenerationMs += Math.Max(0, CandidateTimer.Elapsed.TotalMilliseconds - CandidateEvaluationMs);
            }
                List<FlowchartLayoutNodeBounds> RoutingObstacles = [.. Obstacles, Source, Target];
                // Port allocation has already chosen the sides and positions. Searching from
                // synthetic centre ports needlessly repeats the same route search and can undo
                // the distributed edge ports assigned to parallel lines.
                (double X, double Y) Start = (X: Route.Points[0].X, Y: Route.Points[0].Y);
                (double X, double Y) End = (X: Route.Points[^1].X, Y: Route.Points[^1].Y);
            double SearchLowerBound = 200 + Math.Abs(End.X - Start.X) + Math.Abs(End.Y - Start.Y);
            if (SearchLowerBound < BestCost - 0.01)
            {
                string SearchKey = Route.EdgeId + $"|{Start.X:R},{Start.Y:R}|{End.X:R},{End.Y:R}|"
                    + string.Join(";", RoutingObstacles.Select(Box =>
                        $"{Box.NodeId}:{Box.CenterX:R},{Box.CenterY:R},{Box.Width:R},{Box.Height:R}"));
                if (!SearchCache.TryGetValue(SearchKey, out (bool Found, List<(double X, double Y)> Points) Cached))
                {
                    Stopwatch? SearchTimer = RouteEvaluationObserver == null ? null : Stopwatch.StartNew();
                    bool Found = FlowchartPaddedVisibilityRouter.TryRoute(Start.X, Start.Y, End.X, End.Y,
                        RoutingObstacles, Source, Target, out List<(double X, double Y)>? Detour, 30, 20);
                    SearchTimer?.Stop();
                    SearchCount++;
                    if (SearchTimer != null)
                        {
                            SearchMs += SearchTimer.Elapsed.TotalMilliseconds;
                        }

                        Cached = (Found, Found ? Detour : []);
                    SearchCache.Add(SearchKey, Cached);
                }
                if (Cached.Found)
                    {
                        Consider(Cached.Points);
                    }
                }
            string BeforeGeometry = RouteKey(Route);
            FlowchartLayoutPoint[] OldPoints = [.. Route.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y })];
            Route.Points.Clear();
            foreach ((double X, double Y) Point in Best)
                {
                    Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                }

                if (BeforeGeometry != RouteKey(Route))
            {
                RouteChanged = true;
                // Only routes whose spatial dependencies intersect the changed route's
                // old or new cells can have a different candidate score.
                List<FlowchartLayoutEdgeRoute> Affected = [.. Spatial.QueryRoutes(OldPoints, Route)];
                Spatial.ReplaceRoute(Route);
                Affected.AddRange(Spatial.QueryRoutes(Route.Points, Route));
                foreach (FlowchartLayoutEdgeRoute Other in Affected.Distinct())
                    {
                        if (Queued.Add(Other))
                        {
                            Pending.Enqueue(Other);
                        }
                    }
                }

            double Cost(List<(double X, double Y)> Path)
            {
                Stopwatch? Timer = RouteEvaluationObserver == null ? null : Stopwatch.StartNew();
                RescoreCount++;
                double Result = (Path.Count * 100) + Path.Zip(Path.Skip(1)).Sum(Pair =>
                    Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y));
                foreach (FlowchartLayoutEdgeRoute? Other in Layout.EdgeRoutes.Where(Other => Other != Route))
                {
                        (int Crossings, double Overlap) Intersections = FlowchartBranchRouteOrder.Intersections(Path, Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                    Result += (Intersections.Crossings * 1000000) + (Intersections.Overlap * 100000);
                }
                Timer?.Stop();
                if (Timer != null)
                    {
                        RescoreMs += Timer.Elapsed.TotalMilliseconds;
                    }

                    return Result;
            }
            void Consider(List<(double X, double Y)> Path)
            {
                CandidateCount++;
                Stopwatch? Timer = RouteEvaluationObserver == null ? null : Stopwatch.StartNew();
                double PriorRescoreMs = RescoreMs;
                if (!PortClear(Path[0], Path[1], Source) || !PortClear(Path[^1], Path[^2], Target)
                    || !SelectedSidesMatch(Path, Source, Target, Route.ExitSide, Route.EntrySide)
                    || CountCorners(Path) > 2) { FinishCandidateTimer(); return; }
                foreach (((double X, double Y) First, (double X, double Y) Second) Pair in Path.Zip(Path.Skip(1)))
                {
                    if (Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y) < 20)
                    { FinishCandidateTimer(); return; }
                    if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Source)
                        || FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Target)
                        || Obstacles.Any(Box => FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                            Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box, -6))) { FinishCandidateTimer(); return; }
                }
                foreach (FlowchartLayoutEdgeRoute? Other in Layout.EdgeRoutes.Where(Other => Other != Route && Other.Points.Count >= 2))
                {
                    foreach (FlowchartLayoutPoint? End in new[] { Other.Points[0], Other.Points[^1] })
                        {
                            if (Math.Abs(End.X - Path[0].X) + Math.Abs(End.Y - Path[0].Y) < 6
                            || Math.Abs(End.X - Path[^1].X) + Math.Abs(End.Y - Path[^1].Y) < 6) { FinishCandidateTimer(); return; }
                        }

                        List<(double X, double Y)> Points = [.. Other.Points.Select(Point => (Point.X, Point.Y))];
                        EdgeContract? OtherEdge = Contract.Edges.FirstOrDefault(Item => Item.Id == Other.EdgeId);
                    if (CaptionConflict(Path, Points, Edge.Label, OtherEdge?.Label)
                        > CaptionConflict(Best, Points, Edge.Label, OtherEdge?.Label) + 0.01) { FinishCandidateTimer(); return; }
                        (int Crossings, double Overlap) Old = FlowchartBranchRouteOrder.Intersections(Best, Points);
                        (int Crossings, double Overlap) New = FlowchartBranchRouteOrder.Intersections(Path, Points);
                    if (New.Overlap > Old.Overlap + 0.01) { FinishCandidateTimer(); return; }
                    foreach (((double X, double Y) First, (double X, double Y) Second) A in Path.Zip(Path.Skip(1)))
                        {
                            foreach (((double X, double Y) First, (double X, double Y) Second) B in Points.Zip(Points.Skip(1)))
                    {
                        bool AV = Math.Abs(A.First.X - A.Second.X) < 0.01;
                        bool BV = Math.Abs(B.First.X - B.Second.X) < 0.01;
                        if (AV == BV)
                                {
                                    continue;
                                }

                                ((double X, double Y) First, (double X, double Y) Second) V = AV ? A : B; ((double X, double Y) First, (double X, double Y) Second) H = AV ? B : A;
                                (double X, double Y) P = (X: V.First.X, Y: H.First.Y);
                        if (P.X < Math.Min(H.First.X, H.Second.X) || P.X > Math.Max(H.First.X, H.Second.X)
                            || P.Y < Math.Min(V.First.Y, V.Second.Y) || P.Y > Math.Max(V.First.Y, V.Second.Y))
                                {
                                    continue;
                                }

                                if (Path.Skip(1).SkipLast(1).Concat(Points.Skip(1).SkipLast(1)).Any(Corner =>
                            Math.Abs(P.X - Corner.X) + Math.Abs(P.Y - Corner.Y) < DiagramGridRouteBuilder.CORNER_RADIUS_PX))
                        { FinishCandidateTimer(); return; }
                    }
                        }
                    }
                double CandidateCost = Cost(Path);
                if (CandidateCost < BestCost - 0.01) { Best = Path; BestCost = CandidateCost; }
                FinishCandidateTimer();

                void FinishCandidateTimer()
                {
                    if (Timer != null)
                    {
                        Timer.Stop();
                        CandidateEvaluationMs += Timer.Elapsed.TotalMilliseconds;
                    }
                }
            }
            }
            finally
            {
                if (RouteTimer != null)
                {
                    RouteTimer.Stop();
                    RouteTimingObserver?.Invoke(Route.EdgeId, RouteTimer.Elapsed.TotalMilliseconds, RouteChanged);
                }
                RouteEvaluationObserver?.Invoke(Route.EdgeId, CandidateCount, CandidateGenerationMs,
    SearchCount, SearchMs, RescoreCount, RescoreMs);
            }
        }

        static string RouteKey(FlowchartLayoutEdgeRoute Route) => Route.EdgeId + ":" + Route.ExitSide + ":"
            + Route.EntrySide + ":" + string.Join(";", Route.Points.Select(Point =>
                BitConverter.DoubleToInt64Bits(Point.X).ToString("X16") + ","
                + BitConverter.DoubleToInt64Bits(Point.Y).ToString("X16")));
    }

    private static int CountCorners(IReadOnlyList<(double X, double Y)> Path)
    {
        int Corners = 0;
        for (int Index = 1; Index < Path.Count - 1; Index++)
        {
            if ((Math.Abs(Path[Index].Y - Path[Index - 1].Y) <= 0.5)
                != (Math.Abs(Path[Index + 1].Y - Path[Index].Y) <= 0.5))
            {
                Corners++;
            }
        }

        return Corners;
    }

    private static bool SelectedSidesMatch(IReadOnlyList<(double X, double Y)> Path,
        FlowchartLayoutNodeBounds Source, FlowchartLayoutNodeBounds Target, string ExitSide, string EntrySide)
    {
        if (Path.Count < 2)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ExitSide) || string.IsNullOrWhiteSpace(EntrySide))
        {
            return true;
        }

        bool Leaves = ExitSide.ToLowerInvariant() switch
        {
            "left" => Math.Abs(Path[0].X - (Source.CenterX - (Source.Width / 2))) <= 0.6 && Path[1].X < Path[0].X - 0.5,
            "right" => Math.Abs(Path[0].X - (Source.CenterX + (Source.Width / 2))) <= 0.6 && Path[1].X > Path[0].X + 0.5,
            "top" => Math.Abs(Path[0].Y - (Source.CenterY + (Source.Height / 2))) <= 0.6 && Path[1].Y > Path[0].Y + 0.5,
            "bottom" => Math.Abs(Path[0].Y - (Source.CenterY - (Source.Height / 2))) <= 0.6 && Path[1].Y < Path[0].Y - 0.5,
            _ => false
        };
        bool Enters = EntrySide.ToLowerInvariant() switch
        {
            "left" => Math.Abs(Path[^1].X - (Target.CenterX - (Target.Width / 2))) <= 0.6 && Path[^2].X < Path[^1].X - 0.5,
            "right" => Math.Abs(Path[^1].X - (Target.CenterX + (Target.Width / 2))) <= 0.6 && Path[^2].X > Path[^1].X + 0.5,
            "top" => Math.Abs(Path[^1].Y - (Target.CenterY + (Target.Height / 2))) <= 0.6 && Path[^2].Y > Path[^1].Y + 0.5,
            "bottom" => Math.Abs(Path[^1].Y - (Target.CenterY - (Target.Height / 2))) <= 0.6 && Path[^2].Y < Path[^1].Y - 0.5,
            _ => false
        };
        return Leaves && Enters;
    }

    private static IEnumerable<(double X, double Y)> Ports(FlowchartLayoutNodeBounds Box)
    {
        yield return (Box.CenterX - (Box.Width / 2), Box.CenterY);
        yield return (Box.CenterX + (Box.Width / 2), Box.CenterY);
        yield return (Box.CenterX, Box.CenterY - (Box.Height / 2));
        yield return (Box.CenterX, Box.CenterY + (Box.Height / 2));
    }

    private static bool PortClear((double X, double Y) End, (double X, double Y) Next, FlowchartLayoutNodeBounds Box)
    {
        bool Horizontal = Math.Abs(End.Y - Next.Y) < 0.01;
        double Face = Horizontal ? Box.Height : Box.Width;
        double Offset = Horizontal ? Math.Abs(End.Y - Box.CenterY) : Math.Abs(End.X - Box.CenterX);
        return (Face / 2) - Offset >= Math.Min(Face / 2, Face >= 100 ? 24 : 12) - 0.01;
    }

    // Keep parallel bundles far enough apart for a caption on either connector.
    private static double CaptionConflict(List<(double X, double Y)> Path, List<(double X, double Y)> Other,
        string? Label, string? OtherLabel)
    {
        if (string.IsNullOrWhiteSpace(Label) && string.IsNullOrWhiteSpace(OtherLabel))
        {
            return 0;
        }

        double Width = Math.Max(Label?.Length ?? 0, OtherLabel?.Length ?? 0) * 12 * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM;
        double Conflict = 0;
        foreach (((double X, double Y) First, (double X, double Y) Second) A in Path.Zip(Path.Skip(1)))
        {
            foreach (((double X, double Y) First, (double X, double Y) Second) B in Other.Zip(Other.Skip(1)))
        {
            bool Vertical = Math.Abs(A.First.X - A.Second.X) < 0.01;
            if (Vertical != (Math.Abs(B.First.X - B.Second.X) < 0.01))
                {
                    continue;
                }

                double Separation = Vertical ? Math.Abs(A.First.X - B.First.X) : Math.Abs(A.First.Y - B.First.Y);
            double Clearance = ((Vertical ? Width : 12) / 2) + 6;
            if (Separation >= Clearance)
                {
                    continue;
                }

                double Overlap = Vertical
                ? Math.Min(Math.Max(A.First.Y, A.Second.Y), Math.Max(B.First.Y, B.Second.Y)) - Math.Max(Math.Min(A.First.Y, A.Second.Y), Math.Min(B.First.Y, B.Second.Y))
                : Math.Min(Math.Max(A.First.X, A.Second.X), Math.Max(B.First.X, B.Second.X)) - Math.Max(Math.Min(A.First.X, A.Second.X), Math.Min(B.First.X, B.Second.X));
            Conflict += Math.Max(0, Overlap) * (Clearance - Separation);
        }
        }

        return Conflict;
    }
}
