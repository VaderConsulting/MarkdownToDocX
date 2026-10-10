using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Uses free facing ports to shorten routes between nested groups and outside connectors.</summary>
internal static class FlowchartCrossGroupRouteSimplifier
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var Roots = Contract.Groups.Where(Group => !Contract.Groups.Any(Other => Other.Id != Group.Id
            && FlowchartCompoundEndpointHelper.Contains(Contract, Other.Id, Group.Id))).ToList();
        if (Roots.Count < 2) return;
        string? Owner(string Id) => Roots.FirstOrDefault(Group => Group.Id == Id
            || FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Id))?.Id;
        for (int Pass = 0; Pass < 3; Pass++)
        foreach (var Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2 || !Bounds.TryGetValue(Route.SourceId, out var Source)
                || !Bounds.TryGetValue(Route.TargetId, out var Target)) continue;
            var Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
            if (Edge == null || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)) continue;
            string? AOwner = Owner(Route.SourceId), BOwner = Owner(Route.TargetId);
            // Direct members of partner groups already own their ordered tracks.
            if (AOwner != null && BOwner != null && AOwner != BOwner
                && Contract.Groups.First(Group => Group.Id == AOwner).ChildNodeIds.Count > 1
                && Contract.Groups.First(Group => Group.Id == BOwner).ChildNodeIds.Count > 1
                && Contract.Groups.First(Group => Group.Id == AOwner).ChildNodeIds.Contains(Route.SourceId)
                && Contract.Groups.First(Group => Group.Id == BOwner).ChildNodeIds.Contains(Route.TargetId)) continue;
            bool SelfCut = Route.Points.Zip(Route.Points.Skip(1)).Any(Pair =>
                FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Source)
                || FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Target));
            if (AOwner == BOwner && !SelfCut) continue;
            if (Contract.Nodes.Any(Node => (Node.Id == Route.SourceId || Node.Id == Route.TargetId)
                && Node.Shape is not (null or "" or "rect" or "rounded" or "round"))) continue;
            var Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge)
                .Where(Box => !Contract.Groups.Any(Group => Group.Id == Box.NodeId
                    && (FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.SourceId)
                        || FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Route.TargetId)))).ToList();
            var Best = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
            double BestCost = SelfCut ? double.PositiveInfinity : Cost(Best);
            foreach (bool Transpose in new[] { false, true })
            {
                double U(FlowchartLayoutNodeBounds Box) => Transpose ? Box.CenterY : Box.CenterX;
                double V(FlowchartLayoutNodeBounds Box) => Transpose ? Box.CenterX : Box.CenterY;
                double W(FlowchartLayoutNodeBounds Box) => (Transpose ? Box.Height : Box.Width) / 2;
                double H(FlowchartLayoutNodeBounds Box) => (Transpose ? Box.Width : Box.Height) / 2;
                (double X, double Y) World(double U, double V) => Transpose ? (V, U) : (U, V);
                int Sign = Math.Sign(U(Target) - U(Source));
                if (Sign == 0 || Math.Abs(U(Target) - U(Source)) < W(Source) + W(Target) + 20) continue;
                double StartU = U(Source) + Sign * W(Source), EndU = U(Target) - Sign * W(Target);
                var SourceVs = new[] { V(Source), V(Source) - H(Source) / 3, V(Source) + H(Source) / 3 };
                var TargetVs = new[] { V(Target), V(Target) - H(Target) / 3, V(Target) + H(Target) / 3,
                    V(Target) - H(Target) + 20, V(Target) + H(Target) - 20 };
                foreach (int Outside in new[] { -1, 1 })
                {
                    double Sv = V(Source) + Outside * H(Source), Tv = V(Target) + Outside * H(Target);
                    double Track = (Outside < 0 ? Math.Min(Sv, Tv) : Math.Max(Sv, Tv)) + Outside * 40;
                    foreach (int SourceOffset in new[] { 0, -1, 1 })
                    foreach (int TargetOffset in new[] { 0, -1, 1 })
                    {
                        double Su = U(Source) + SourceOffset * W(Source) / 3;
                        double Tu = U(Target) + TargetOffset * W(Target) / 3;
                        Consider([World(Su, Sv), World(Su, Track), World(Tu, Track), World(Tu, Tv)]);
                    }
                }
                foreach (double Line in SourceVs.Concat(TargetVs))
                    if (Math.Abs(Line - V(Source)) <= H(Source) - 20 && Math.Abs(Line - V(Target)) <= H(Target) - 20)
                        Consider([World(StartU, Line), World(EndU, Line)]);
                foreach (double Sv in SourceVs)
                foreach (double Tv in TargetVs)
                {
                    foreach (double Track in new[] { (StartU + EndU) / 2, StartU + Sign * 40, EndU - Sign * 40 })
                        Consider([World(StartU, Sv), World(Track, Sv), World(Track, Tv), World(EndU, Tv)]);
                    int SourceNear = Math.Sign(Tv - V(Source)), TargetNear = Math.Sign(Sv - V(Target));
                    if (TargetNear != 0)
                        Consider([World(StartU, Sv), World(U(Target), Sv), World(U(Target), V(Target) + TargetNear * H(Target))]);
                    if (SourceNear != 0)
                        Consider([World(U(Source), V(Source) + SourceNear * H(Source)), World(U(Source), Tv), World(EndU, Tv)]);
                }
            }
            Route.Points.Clear();
            foreach (var Point in Best) Route.Points.Add(new() { X = Point.X, Y = Point.Y });

            double Cost(List<(double X, double Y)> Path)
            {
                double Result = Path.Count * 100 + Path.Zip(Path.Skip(1)).Sum(Pair =>
                    Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y));
                foreach (var Other in Layout.EdgeRoutes.Where(Other => Other != Route))
                {
                    var Intersections = FlowchartBranchRouteOrder.Intersections(Path, Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                    Result += Intersections.Crossings * 1000000 + Intersections.Overlap * 100000;
                }
                return Result;
            }
            void Consider(List<(double X, double Y)> Path)
            {
                if (!PortClear(Path[0], Path[1], Source) || !PortClear(Path[^1], Path[^2], Target)) return;
                foreach (var Pair in Path.Zip(Path.Skip(1)))
                {
                    if (Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y) < 20) return;
                    if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Source)
                        || FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Target)
                        || Obstacles.Any(Box => FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                            Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Box, -6))) return;
                }
                foreach (var Other in Layout.EdgeRoutes.Where(Other => Other != Route && Other.Points.Count >= 2))
                {
                    foreach (var End in new[] { Other.Points[0], Other.Points[^1] })
                        if (Math.Abs(End.X - Path[0].X) + Math.Abs(End.Y - Path[0].Y) < 6
                            || Math.Abs(End.X - Path[^1].X) + Math.Abs(End.Y - Path[^1].Y) < 6) return;
                    var Points = Other.Points.Select(Point => (Point.X, Point.Y)).ToList();
                    var OtherEdge = Contract.Edges.FirstOrDefault(Item => Item.Id == Other.EdgeId);
                    if (CaptionConflict(Path, Points, Edge.Label, OtherEdge?.Label)
                        > CaptionConflict(Best, Points, Edge.Label, OtherEdge?.Label) + 0.01) return;
                    var Old = FlowchartBranchRouteOrder.Intersections(Best, Points);
                    var New = FlowchartBranchRouteOrder.Intersections(Path, Points);
                    if (New.Overlap > Old.Overlap + 0.01) return;
                    foreach (var A in Path.Zip(Path.Skip(1)))
                    foreach (var B in Points.Zip(Points.Skip(1)))
                    {
                        bool AV = Math.Abs(A.First.X - A.Second.X) < 0.01;
                        bool BV = Math.Abs(B.First.X - B.Second.X) < 0.01;
                        if (AV == BV) continue;
                        var V = AV ? A : B; var H = AV ? B : A;
                        var P = (X: V.First.X, Y: H.First.Y);
                        if (P.X < Math.Min(H.First.X, H.Second.X) || P.X > Math.Max(H.First.X, H.Second.X)
                            || P.Y < Math.Min(V.First.Y, V.Second.Y) || P.Y > Math.Max(V.First.Y, V.Second.Y)) continue;
                        if (Path.Skip(1).SkipLast(1).Concat(Points.Skip(1).SkipLast(1)).Any(Corner =>
                            Math.Abs(P.X - Corner.X) + Math.Abs(P.Y - Corner.Y) < DiagramGridRouteBuilder.CORNER_RADIUS_PX)) return;
                    }
                }
                double CandidateCost = Cost(Path);
                if (CandidateCost >= BestCost - 0.01) return;
                Best = Path; BestCost = CandidateCost;
            }
        }
    }

    private static bool PortClear((double X, double Y) End, (double X, double Y) Next, FlowchartLayoutNodeBounds Box)
    {
        bool Horizontal = Math.Abs(End.Y - Next.Y) < 0.01;
        double Face = Horizontal ? Box.Height : Box.Width;
        double Offset = Horizontal ? Math.Abs(End.Y - Box.CenterY) : Math.Abs(End.X - Box.CenterX);
        return Face / 2 - Offset >= Math.Min(Face / 2, Face >= 100 ? 24 : 12) - 0.01;
    }

    // Keep parallel bundles far enough apart for a caption on either connector.
    private static double CaptionConflict(List<(double X, double Y)> Path, List<(double X, double Y)> Other,
        string? Label, string? OtherLabel)
    {
        if (string.IsNullOrWhiteSpace(Label) && string.IsNullOrWhiteSpace(OtherLabel)) return 0;
        double Width = Math.Max(Label?.Length ?? 0, OtherLabel?.Length ?? 0) * 12 * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM;
        double Conflict = 0;
        foreach (var A in Path.Zip(Path.Skip(1)))
        foreach (var B in Other.Zip(Other.Skip(1)))
        {
            bool Vertical = Math.Abs(A.First.X - A.Second.X) < 0.01;
            if (Vertical != (Math.Abs(B.First.X - B.Second.X) < 0.01)) continue;
            double Separation = Vertical ? Math.Abs(A.First.X - B.First.X) : Math.Abs(A.First.Y - B.First.Y);
            double Clearance = (Vertical ? Width : 12) / 2 + 6;
            if (Separation >= Clearance) continue;
            double Overlap = Vertical
                ? Math.Min(Math.Max(A.First.Y, A.Second.Y), Math.Max(B.First.Y, B.Second.Y)) - Math.Max(Math.Min(A.First.Y, A.Second.Y), Math.Min(B.First.Y, B.Second.Y))
                : Math.Min(Math.Max(A.First.X, A.Second.X), Math.Max(B.First.X, B.Second.X)) - Math.Max(Math.Min(A.First.X, A.Second.X), Math.Min(B.First.X, B.Second.X));
            Conflict += Math.Max(0, Overlap) * (Clearance - Separation);
        }
        return Conflict;
    }
}
