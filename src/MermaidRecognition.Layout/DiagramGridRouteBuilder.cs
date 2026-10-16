using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds orthogonal grid routes with obstacle-aware elbow and Z-shape selection.
/// </summary>
public static class DiagramGridRouteBuilder
{
    /// <summary>
    /// Radius for rounded corners when the route is drawn.
    /// </summary>
    public const int CORNER_RADIUS_PX = 20;

    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double HOOK_DOT_PRODUCT_THRESHOLD = -0.15;

    /// <summary>Removes unnecessary backward steps between facing parallel ports.</summary>
    internal static void RemoveClearForwardDoglegs(FlowchartLayoutResult Layout, DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (var Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 6 || BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)) continue;
            var Start = Route.Points[0]; var Next = Route.Points[1];
            var End = Route.Points[^1]; var Previous = Route.Points[^2];
            bool Horizontal = Math.Abs(Start.Y - Next.Y) < 0.5 && Math.Abs(Previous.Y - End.Y) < 0.5;
            bool Vertical = Math.Abs(Start.X - Next.X) < 0.5 && Math.Abs(Previous.X - End.X) < 0.5;
            if (!Horizontal && !Vertical) continue;
            double Along(FlowchartLayoutPoint Point) => Horizontal ? Point.X : Point.Y;
            double Across(FlowchartLayoutPoint Point) => Horizontal ? Point.Y : Point.X;
            double Sign = Math.Sign(Along(Next) - Along(Start));
            if (Sign == 0 || Math.Sign(Along(End) - Along(Previous)) != Sign
                || (Along(End) - Along(Start)) * Sign < 3 * CORNER_RADIUS_PX + 10
                || Math.Abs(Across(End) - Across(Start)) < 2 * CORNER_RADIUS_PX) continue;
            (double X, double Y) Map(double AlongValue, double AcrossValue) => Horizontal
                ? (AlongValue, AcrossValue) : (AcrossValue, AlongValue);
            var Tracks = Route.Points.Skip(1).SkipLast(1).Select(Along)
                .Prepend(Math.Round((Along(Start) + Along(End)) / 2)).Distinct();
            foreach (double Track in Tracks)
            {
                if ((Track - Along(Start)) * Sign < 2 * CORNER_RADIUS_PX
                    || (Along(End) - Track) * Sign < CORNER_RADIUS_PX + 10) continue;
                List<(double X, double Y)> Candidate = [(Start.X, Start.Y), Map(Track, Across(Start)),
                    Map(Track, Across(End)), (End.X, End.Y)];
                if (!PathIsValid(Candidate, Bounds, Route.SourceId, Route.TargetId, Contract)
                    || !StaysInsideSharedFrames(Candidate, Route, Contract, Bounds)) continue;
                bool Crowded = false;
                foreach (var Other in Layout.EdgeRoutes.Where(Other => Other != Route))
                for (int A = 1; A < Candidate.Count; A++)
                for (int B = 1; B < Other.Points.Count; B++)
                {
                    // Shortened terminal runs occupy only their existing track.
                    if ((A == 1 && Math.Abs(Track - Along(Start)) <= Math.Abs(Along(Next) - Along(Start)))
                        || (A == Candidate.Count - 1 && Math.Abs(Along(End) - Track) <= Math.Abs(Along(End) - Along(Previous)))) continue;
                    var P = Candidate[A - 1]; var Q = Candidate[A];
                    var R = Other.Points[B - 1]; var S = Other.Points[B];
                    bool H = Math.Abs(P.Y - Q.Y) < 0.5;
                    if (H != (Math.Abs(R.Y - S.Y) < 0.5)) continue;
                    double Gap = H ? Math.Abs(P.Y - R.Y) : Math.Abs(P.X - R.X);
                    double Overlap = H ? Math.Min(Math.Max(P.X, Q.X), Math.Max(R.X, S.X)) - Math.Max(Math.Min(P.X, Q.X), Math.Min(R.X, S.X))
                        : Math.Min(Math.Max(P.Y, Q.Y), Math.Max(R.Y, S.Y)) - Math.Max(Math.Min(P.Y, Q.Y), Math.Min(R.Y, S.Y));
                    if (Gap < 6 && Overlap > 0.5) Crowded = true;
                }
                if (Crowded) continue;
                Route.Points.Clear();
                foreach (var Point in Candidate) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                break;
            }
        }
    }

    /// <summary>Rebuilds internal returns on clear tracks inside their common frame.</summary>
    internal static void RebuildContainedBackRoutes(FlowchartLayoutResult Layout,
        List<DiagramGridLayout.GridNode> Placed, Dictionary<string, NodeContract> Nodes,
        DiagramContract Contract, IReadOnlyList<DiagramGridLayout.GridFrame> Frames, FlowchartDirection Direction)
    {
        var Bounds = BuildBounds(Placed, Frames);
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        double Flow = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.BottomToTop ? 1 : -1;
        string Downstream = Horizontal ? (Flow > 0 ? "right" : "left") : (Flow > 0 ? "top" : "bottom");
        double Main(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterX : Node.CenterY;
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        (double X, double Y) Map(double Main, double Cross) => Horizontal ? (Main, Cross) : (Cross, Main);
        foreach (var Route in Layout.EdgeRoutes)
        {
            var Edge = BackEdges.FirstOrDefault(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId);
            if (Edge == null || !Bounds.TryGetValue(Route.SourceId, out var Source) || !Bounds.TryGetValue(Route.TargetId, out var Target)) continue;
            var Shared = Frames.Where(Frame => DiagramGridLayout.IsInsideGroup(Contract, Route.SourceId, Frame.Id)
                && DiagramGridLayout.IsInsideGroup(Contract, Route.TargetId, Frame.Id)).OrderBy(Frame => (Frame.Right - Frame.Left) * (Frame.Top - Frame.Bottom)).FirstOrDefault();
            if (Shared == null || StaysInsideSharedFrames(Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Route, Contract, Bounds)) continue;
            Nodes.TryGetValue(Route.SourceId, out var SourceNode);
            Nodes.TryGetValue(Route.TargetId, out var TargetNode);
            double Preferred = Math.Sign(Cross(Source) - Cross(Target));
            if (Preferred == 0) Preferred = Horizontal ? -1 : 1;
            foreach (double Side in new[] { Preferred, -Preferred })
            {
                var TowardSource = Map(Main(Source) + Flow * 1000, Cross(Source));
                var TowardTarget = Map(Main(Target), Cross(Target) + Side * 1000);
                if (!FlowchartShapeBoundaryAttachHelper.TryResolveOutboundAttach(Source, SourceNode?.Shape,
                        TowardSource.X, TowardSource.Y, Edge.MarkerStart, out double Sx, out double Sy)
                    || !FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(Target, TargetNode?.Shape,
                        TowardTarget.X, TowardTarget.Y, Edge.MarkerEnd, out double Tx, out double Ty)) continue;
                bool Occupied = Layout.EdgeRoutes.Any(Other => Other != Route && Other.Points.Count > 1
                    && ((Other.SourceId == Route.SourceId && Math.Abs(Other.Points[0].X - Sx) < 1 && Math.Abs(Other.Points[0].Y - Sy) < 1)
                        || (Other.TargetId == Route.TargetId && Math.Abs(Other.Points[^1].X - Tx) < 1 && Math.Abs(Other.Points[^1].Y - Ty) < 1)));
                if (Occupied) continue;
                var Members = Placed.Where(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Shared.Id)).ToList();
                double Lane = Side > 0 ? Members.Max(Node => Horizontal ? Node.Y + Node.Height / 2.0 : Node.X + Node.Width / 2.0) + 40
                    : Members.Min(Node => Horizontal ? Node.Y - Node.Height / 2.0 : Node.X - Node.Width / 2.0) - 40;
                double StartMain = (Horizontal ? Sx : Sy) + Flow * 40;
                List<(double X, double Y)> Path = [(Sx, Sy), Map(StartMain, Horizontal ? Sy : Sx),
                    Map(StartMain, Lane), Map(Main(Target), Lane), (Tx, Ty)];
                Path = SimplifyCollinear(Path);
                if (CountCorners(Path) > 3 || !PathIsValid(Path, Bounds, Route.SourceId, Route.TargetId, Contract)
                    || !StaysInsideSharedFrames(Path, Route, Contract, Bounds)) continue;
                Route.ExitSide = Downstream;
                Route.EntrySide = Horizontal ? (Side > 0 ? "top" : "bottom") : (Side > 0 ? "right" : "left");
                Route.Points.Clear();
                foreach (var Point in Path) Route.Points.Add(new() { X = Math.Round(Point.X), Y = Math.Round(Point.Y) });
                break;
            }
        }
    }

    /// <summary>
    /// Replaces diagonal legacy back-edge guides when a valid orthogonal route
    /// fits the already allocated ports without exceeding three corners.
    /// </summary>
    internal static void RebuildDiagonalBackRoutes(
        FlowchartLayoutResult Layout,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, NodeContract> Nodes,
        DiagramContract Contract,
        IReadOnlyList<DiagramGridLayout.GridFrame> Frames,
        FlowchartDirection Direction,
        IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            // A back-edge rebuild is an automatic-route repair. Explicit route hints
            // already own their geometry and must survive every later materialization.
            if (DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true)
                continue;
            if ((OnlyRoutes != null && !OnlyRoutes.Contains(Route))
                || Route.Points.Count < 2
                || !BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
                || (!Route.Points.Zip(Route.Points.Skip(1)).Any(Pair =>
                    Math.Abs(Pair.First.X - Pair.Second.X) > COLLINEAR_EPSILON_PX
                    && Math.Abs(Pair.First.Y - Pair.Second.Y) > COLLINEAR_EPSILON_PX)
                    && Math.Abs(Route.ExitNormalX) <= 1e-9 && Math.Abs(Route.ExitNormalY) <= 1e-9
                    && Math.Abs(Route.EntryNormalX) <= 1e-9 && Math.Abs(Route.EntryNormalY) <= 1e-9)
                || !BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds Source)
                || !BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds Target))
            {
                continue;
            }

            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
            Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
            List<(double X, double Y)> Path = BuildRoute(
                Route.Points[0].X, Route.Points[0].Y, Source, SourceNode?.Shape, Route.ExitSide,
                Route.Points[^1].X, Route.Points[^1].Y, Target, TargetNode?.Shape, Route.EntrySide,
                BoundsById, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal,
                ExitNormal: (Route.ExitNormalX, Route.ExitNormalY),
                EntryNormal: (Route.EntryNormalX, Route.EntryNormalY));
            (double SourceStubX, double SourceStubY) = ResolvePortStub(
                Path[0].X, Path[0].Y, Source, SourceNode?.Shape, Route.ExitSide, 1,
                Route.ExitNormalX, Route.ExitNormalY);
            (double TargetStubX, double TargetStubY) = ResolvePortStub(
                Path[^1].X, Path[^1].Y, Target, TargetNode?.Shape, Route.EntrySide, 1,
                Route.EntryNormalX, Route.EntryNormalY);
            if (CountCorners(Path) > 3
                || !HasNormalTerminals(Path, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                || !PathIsValid(Path, BoundsById, Route.SourceId, Route.TargetId, Contract)
                || !StaysInsideSharedFrames(Path, Route, Contract, BoundsById))
            {
                continue;
            }

            Route.Points.Clear();
            foreach ((double X, double Y) in Path)
            {
                Route.Points.Add(new() { X = Math.Round(X), Y = Math.Round(Y) });
            }
        }
    }

    private static bool StaysInsideSharedFrames(
        List<(double X, double Y)> Path, FlowchartLayoutEdgeRoute Route,
        DiagramContract Contract, Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!DiagramGridLayout.IsInsideGroup(Contract, Route.SourceId, Group.Id)
                || !DiagramGridLayout.IsInsideGroup(Contract, Route.TargetId, Group.Id)
                || !BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds Frame))
            {
                continue;
            }
            if (Path.Any(Point => Point.X < Frame.CenterX - Frame.Width / 2
                || Point.X > Frame.CenterX + Frame.Width / 2
                || Point.Y < Frame.CenterY - Frame.Height / 2
                || Point.Y > Frame.CenterY + Frame.Height / 2))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Rebuilds every edge route from assigned ports using obstacle-aware orthogonal routing.
    /// </summary>
    internal static void RebuildAllRoutes(
        FlowchartLayoutResult Copy,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, NodeContract> Nodes,
        DiagramContract Contract,
        IReadOnlyList<DiagramGridLayout.GridFrame> Frames,
        FlowchartDirection Direction,
        IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildBounds(Placed, Frames);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        string Geometry = string.Join(";", BoundsById.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
            .Select(Pair => FormattableString.Invariant($"{Pair.Key}:{Pair.Value.CenterX:R},{Pair.Value.CenterY:R},{Pair.Value.Width:R},{Pair.Value.Height:R}")));
        if (!Copy.RenderContext.GridRouteCache.TryGetValue((Contract, Geometry), out var RouteCache))
            Copy.RenderContext.GridRouteCache[(Contract, Geometry)] = RouteCache = new(StringComparer.Ordinal);

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (OnlyRoutes != null && !OnlyRoutes.Contains(Route))
            {
                continue;
            }

            if (Route.Points.Count < 2)
            {
                continue;
            }

            if (DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true)
            {
                continue;
            }

            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);

            EdgeContract? RouteEdge = null;
            foreach (EdgeContract Candidate in Contract.Edges)
            {
                if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetId, Route.TargetId, StringComparison.Ordinal))
                {
                    RouteEdge = Candidate;
                    break;
                }
            }

            if (!BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            string? SourceShape = SourceNode?.Shape;
            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;

            double StartX = Route.Points[0].X;
            double StartY = Route.Points[0].Y;
            double EndX = Route.Points[Route.Points.Count - 1].X;
            double EndY = Route.Points[Route.Points.Count - 1].Y;

            if (RouteEdge != null
                && FlowchartBackEdgeHelper.FindBackEdges(Contract).Contains(RouteEdge)
                && FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())
                && Math.Abs(Route.ExitNormalX) <= 1e-9 && Math.Abs(Route.ExitNormalY) <= 1e-9
                && Math.Abs(Route.EntryNormalX) <= 1e-9 && Math.Abs(Route.EntryNormalY) <= 1e-9)
            {
                (double sourceStubX, double sourceStubY) = ResolvePortStub(StartX, StartY,
                    SourceBounds, SourceShape, Route.ExitSide, CORNER_RADIUS_PX * 2);
                (double targetStubX, double targetStubY) = ResolvePortStub(EndX, EndY,
                    TargetBounds, TargetShape, Route.EntrySide, CORNER_RADIUS_PX + 10);
                if (HasNormalTerminals(Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                        sourceStubX, sourceStubY, targetStubX, targetStubY))
                    continue;
            }

            string CacheKey = FormattableString.Invariant($"{Route.EdgeId}:{Route.SourceId}:{Route.TargetId}:{SourceShape}:{TargetShape}:{Direction}:{Route.ExitSide}:{Route.EntrySide}:{StartX:R},{StartY:R},{EndX:R},{EndY:R}:{Route.ExitNormalX:R},{Route.ExitNormalY:R},{Route.EntryNormalX:R},{Route.EntryNormalY:R}");
            if (!RouteCache.TryGetValue(CacheKey, out List<(double X, double Y)>? Path))
            {
            FlowchartRenderContext.ReportOperationCount("automatic-route-cache-misses", 1);
            Path = BuildRoute(
                StartX,
                StartY,
                SourceBounds,
                SourceShape,
                Route.ExitSide,
                EndX,
                EndY,
                TargetBounds,
                TargetShape,
                Route.EntrySide,
                BoundsById,
                Route.SourceId,
                Route.TargetId,
                Contract,
                Direction,
                Horizontal,
                ExitNormal: (Route.ExitNormalX, Route.ExitNormalY),
                EntryNormal: (Route.EntryNormalX, Route.EntryNormalY));
            RouteCache[CacheKey] = Path;
            }
            else FlowchartRenderContext.ReportOperationCount("automatic-route-cache-hits", 1);

            Route.Points.Clear();
            for (int index = 0; index < Path.Count; index++)
            {
                (double PointX, double PointY) = Path[index];
                // Preserve the allocator's exact k/(n+1) slot at both ends.
                // Only interior track coordinates are quantised to whole pixels.
                bool endpoint = index == 0 || index == Path.Count - 1;
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = endpoint ? PointX : Math.Round(PointX),
                    Y = endpoint ? PointY : Math.Round(PointY)
                });
            }
        }
    }

    internal static List<(double X, double Y)> BuildRoute(
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        string? ExitSide,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        string? EntrySide,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        FlowchartDirection Direction,
        bool Horizontal,
        LayoutHintDirection? Travel = null,
        bool RequireLegal = false,
        (double X, double Y) ExitNormal = default,
        (double X, double Y) EntryNormal = default)
    {
        // The rounded corner consumes one radius of the terminal segment.
        // Keep another radius at the source and an arrow length at the target.
        double SourceStubLength = ResolveFrameExitLength(StartX, StartY, ExitSide,
            CORNER_RADIUS_PX * 2, SourceId, TargetId, Contract, BoundsById);
        double TargetStubLength = ResolveFrameExitLength(EndX, EndY, EntrySide,
            CORNER_RADIUS_PX + 10, TargetId, SourceId, Contract, BoundsById);
        (double SourceStubX, double SourceStubY) = ResolvePortStub(
            StartX, StartY, SourceBounds, SourceShape, ExitSide, SourceStubLength,
            ExitNormal.X, ExitNormal.Y);
        (double TargetStubX, double TargetStubY) = ResolvePortStub(
            EndX, EndY, TargetBounds, TargetShape, EntrySide, TargetStubLength,
            EntryNormal.X, EntryNormal.Y);
        Dictionary<string, FlowchartLayoutNodeBounds> RoutingBounds =
            FlowchartFrameTitleBandHelper.AddRoutingObstacles(Contract, BoundsById);
        // Frame outlines are visual boundaries, not solid obstacles. Links may
        // cross them, but the independently represented title strips remain
        // obstacles, and node boxes still block route geometry.
        foreach (GroupContract group in Contract.Groups)
            RoutingBounds.Remove(group.Id);

        if (Travel.HasValue)
        {
            return BuildHintedTravelRoute(
                StartX, StartY, SourceStubX, SourceStubY,
                TargetStubX, TargetStubY, EndX, EndY,
                BoundsById, SourceId, TargetId, Contract, Travel.Value);
        }

        if (FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(StartX, StartY, EndX, EndY)
            && HasNormalTerminals([(StartX, StartY), (EndX, EndY)],
                SourceStubX, SourceStubY, TargetStubX, TargetStubY)
            && !FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                StartX,
                StartY,
                EndX,
                EndY,
                RoutingBounds,
                SourceId,
                TargetId))
        {
            return [(StartX, StartY), (EndX, EndY)];
        }

        List<(double X, double Y)> Best = [];
        (int Bends, double Length) BestScore = (int.MaxValue, double.PositiveInfinity);
        // Extended terminal rays belong in the first search. Fixed-length stubs
        // alone introduce an extra corner when a diamond normal can continue
        // directly to the target's approach track.
        if (FlowchartDiamondAttachHelper.IsDiamondShape(SourceShape)
            || FlowchartDiamondAttachHelper.IsDiamondShape(TargetShape))
        {
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        foreach (var Candidate in MinimumBendTerminalCandidates(
                     new() { X = StartX, Y = StartY }, new() { X = EndX, Y = EndY },
                     SourceBounds, TargetBounds, ExitSide ?? "", EntrySide ?? "",
                     Nodes, RoutingBounds, Contract))
        {
            var Score = ScorePath(Candidate);
            if (Score.CompareTo(BestScore) < 0) { Best = Candidate; BestScore = Score; }
        }
        }
        foreach (List<(double X, double Y)> Candidate in EnumerateCandidates(
                     StartX,
                     StartY,
                     SourceStubX,
                     SourceStubY,
                     TargetStubX,
                     TargetStubY,
                     EndX,
                     EndY,
                     RoutingBounds,
                     SourceId,
                     TargetId,
                     Contract,
                     Direction,
                     Horizontal,
                     RequireLegal))
        {
            List<(double X, double Y)> Simplified = SimplifyCollinear(Candidate);
            if (!HasNormalTerminals(Simplified, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                || !ClearsEndpointBodiesAfterTerminalRuns(Simplified, BoundsById, SourceId, TargetId)
                || !PathIsValid(Simplified, RoutingBounds, SourceId, TargetId, Contract))
            {
                continue;
            }

            var Score = ScorePath(Simplified);
            if (Score.CompareTo(BestScore) < 0)
            {
                BestScore = Score;
                Best = Simplified;
            }
        }

        if (Best.Count > 0)
        {
            return Best;
        }

        List<(double X, double Y)> Fallback = [];
        FlowchartPerpendicularRouteBuilder.TryAppendStraightOrObstacleAvoidingRoute(
            Fallback,
            StartX,
            StartY,
            SourceBounds,
            SourceShape,
            EndX,
            EndY,
            TargetBounds,
            TargetShape,
            Direction,
            RoutingBounds,
            SourceId,
            TargetId);
        List<(double X, double Y)> SimplifiedFallback = SimplifyCollinear(Fallback);
        if (HasNormalTerminals(SimplifiedFallback, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
            && ClearsEndpointBodiesAfterTerminalRuns(SimplifiedFallback, BoundsById, SourceId, TargetId)
            && PathIsValid(SimplifiedFallback, RoutingBounds, SourceId, TargetId, Contract))
        {
            return SimplifiedFallback;
        }

        // The track candidates above are deliberately cheap, but a hinted port can
        // require a route around several obstacles with more bends than those
        // templates express. Use the sparse orthogonal visibility router as the
        // 5.10 re-route-this-line option before declaring that the allocated slots
        // are unreachable. Bend count remains part of the score; it is not a
        // legality limit.
        FlowchartLayoutNodeBounds[] VisibilityObstacles = RoutingBounds.Values
            .Where(Box => Box.NodeId != SourceId && Box.NodeId != TargetId
                && !Contract.Groups.Any(Group => Group.Id == Box.NodeId
                    && (DiagramGridLayout.IsInsideGroup(Contract, SourceId, Group.Id)
                        || DiagramGridLayout.IsInsideGroup(Contract, TargetId, Group.Id))))
            .ToArray();
        if (FlowchartPaddedVisibilityRouter.TryRoute(
                SourceStubX, SourceStubY, TargetStubX, TargetStubY, VisibilityObstacles,
                SourceBounds: null, TargetBounds: null, out List<(double X, double Y)> VisiblePath,
                PortStubPx: 0))
        {
            List<(double X, double Y)> VisibilityCandidate = [(StartX, StartY), (SourceStubX, SourceStubY)];
            VisibilityCandidate.AddRange(VisiblePath.Skip(1).SkipLast(1));
            VisibilityCandidate.Add((TargetStubX, TargetStubY));
            VisibilityCandidate.Add((EndX, EndY));
            VisibilityCandidate = SimplifyCollinear(VisibilityCandidate);
            if (HasNormalTerminals(VisibilityCandidate, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                && ClearsEndpointBodiesAfterTerminalRuns(VisibilityCandidate, BoundsById, SourceId, TargetId)
                && PathIsValid(VisibilityCandidate, RoutingBounds, SourceId, TargetId, Contract,
                    MaxCorners: int.MaxValue))
                return VisibilityCandidate;
        }

        if (RequireLegal && TryBuildHintedOuterLaneRoute(StartX, StartY,
                SourceStubX, SourceStubY, TargetStubX, TargetStubY, EndX, EndY,
                RoutingBounds, BoundsById, SourceId, TargetId, Contract,
                out List<(double X, double Y)> HintedDetour))
        {
            return HintedDetour;
        }

        return BuildForcedCorridorRoute(
            StartX,
            StartY,
            SourceStubX,
            SourceStubY,
            TargetStubX,
            TargetStubY,
            EndX,
            EndY,
            RoutingBounds,
            SourceId,
            TargetId,
            Contract,
            Horizontal,
            RequireLegal);
    }

    private static bool TryBuildHintedOuterLaneRoute(double StartX, double StartY,
        double SourceStubX, double SourceStubY, double TargetStubX, double TargetStubY,
        double EndX, double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> RoutingBounds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> AllBounds,
        string SourceId, string TargetId, DiagramContract Contract,
        out List<(double X, double Y)> Best)
    {
        Best = [];
        if (RoutingBounds.Count == 0) return false;
        double Step = DiagramGridMetrics.GRID_UNIT_PX;
        double Left = RoutingBounds.Values.Min(Box => Box.CenterX - Box.Width / 2.0) - Step;
        double Right = RoutingBounds.Values.Max(Box => Box.CenterX + Box.Width / 2.0) + Step;
        (int Bends, double Length) BestScore = (int.MaxValue, double.PositiveInfinity);
        List<double> CandidateLanesX = [Left, Right];
        List<double> CandidateLanesY = [];
        foreach (FlowchartLayoutNodeBounds obstacle in RoutingBounds.Values)
        {
            double ObstacleLeft = obstacle.CenterX - obstacle.Width / 2.0;
            double ObstacleRight = obstacle.CenterX + obstacle.Width / 2.0;
            double ObstacleBottom = obstacle.CenterY - obstacle.Height / 2.0;
            double ObstacleTop = obstacle.CenterY + obstacle.Height / 2.0;
            CandidateLanesX.Add(ObstacleLeft - (Step / 2.0));
            CandidateLanesX.Add(ObstacleRight + (Step / 2.0));
            CandidateLanesY.Add(ObstacleBottom - (Step / 2.0));
            CandidateLanesY.Add(ObstacleTop + (Step / 2.0));
        }
        CandidateLanesX = CandidateLanesX.Distinct().ToList();
        CandidateLanesY = CandidateLanesY.Distinct().ToList();
        foreach (double Lane in new[] { Left, Right })
        {
            List<(double X, double Y)> Candidate = SimplifyCollinear(
            [
                (StartX, StartY),
                (SourceStubX, SourceStubY),
                (Lane, SourceStubY),
                (Lane, TargetStubY),
                (TargetStubX, TargetStubY),
                (EndX, EndY)
            ]);
            bool Normal = HasNormalTerminals(Candidate, SourceStubX, SourceStubY, TargetStubX, TargetStubY);
            bool ClearEnds = ClearsEndpointBodiesAfterTerminalRuns(Candidate, AllBounds, SourceId, TargetId);
            bool Valid = PathIsValid(Candidate, RoutingBounds, SourceId, TargetId, Contract,
                MaxCorners: int.MaxValue);
            if (!Normal || !ClearEnds || !Valid) continue;
            var Score = ScorePath(Candidate);
            if (Score.CompareTo(BestScore) >= 0) continue;
            Best = Candidate;
            BestScore = Score;
        }

        // An exterior lane at the endpoint's current height can cross a nearby
        // blocker (for example a North-hinted link whose target sits above the
        // source). Try obstacle-edge tracks as well: first rise/fall to a clear
        // horizontal track, pass the blocker on a clear vertical track, then
        // approach the allocated target stub. This honors the selected sides and
        // keeps the normal terminal runs intact.
        foreach (double YLane in CandidateLanesY)
        foreach (double XLane in CandidateLanesX)
        {
            if (Math.Abs(YLane - SourceStubY) < 0.01 || Math.Abs(YLane - TargetStubY) < 0.01
                || Math.Abs(XLane - SourceStubX) < 0.01 || Math.Abs(XLane - TargetStubX) < 0.01)
                continue;
            List<(double X, double Y)> Candidate = SimplifyCollinear(
            [
                (StartX, StartY),
                (SourceStubX, SourceStubY),
                (SourceStubX, YLane),
                (XLane, YLane),
                (XLane, TargetStubY),
                (TargetStubX, TargetStubY),
                (EndX, EndY)
            ]);
            if (!HasNormalTerminals(Candidate, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                || !ClearsEndpointBodiesAfterTerminalRuns(Candidate, AllBounds, SourceId, TargetId)
                || !PathIsValid(Candidate, RoutingBounds, SourceId, TargetId, Contract,
                    MaxCorners: int.MaxValue)) continue;
            var Score = ScorePath(Candidate);
            if (Score.CompareTo(BestScore) >= 0) continue;
            Best = Candidate;
            BestScore = Score;
        }
        return Best.Count >= 2;
    }

    private static bool ClearsEndpointBodiesAfterTerminalRuns(
        IReadOnlyList<(double X, double Y)> Path,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        string SourceId,
        string TargetId)
    {
        if (!Bounds.TryGetValue(SourceId, out FlowchartLayoutNodeBounds Source)
            || !Bounds.TryGetValue(TargetId, out FlowchartLayoutNodeBounds Target)) return false;
        for (int Index = 1; Index < Path.Count; Index++)
        {
            (double X, double Y) A = Path[Index - 1], B = Path[Index];
            bool CrossesSourceAfterLeaving = Index > 1
                && FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Source);
            bool CrossesTargetBeforeEntering = Index < Path.Count - 1
                && FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Target);
            if (CrossesSourceAfterLeaving || CrossesTargetBeforeEntering)
                return false;
        }
        return true;
    }

    private static List<(double X, double Y)> BuildHintedTravelRoute(
        double StartX,
        double StartY,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        LayoutHintDirection Travel)
    {
        bool HorizontalTravel = Travel is LayoutHintDirection.East or LayoutHintDirection.West;
        double Step = DiagramGridMetrics.GRID_UNIT_PX;
        List<double> Lanes = HorizontalTravel
            ? BoundsById.Values.SelectMany(Bounds => new[]
                {
                    Bounds.CenterY - Bounds.Height / 2.0 - Step,
                    Bounds.CenterY + Bounds.Height / 2.0 + Step
                }).Append(SourceStubY).Append(TargetStubY)
                    .Append(SourceStubY - Step).Append(SourceStubY + Step)
                    .Append(TargetStubY - Step).Append(TargetStubY + Step).Distinct().ToList()
            : BoundsById.Values.SelectMany(Bounds => new[]
                {
                    Bounds.CenterX - Bounds.Width / 2.0 - Step,
                    Bounds.CenterX + Bounds.Width / 2.0 + Step
                }).Append(SourceStubX).Append(TargetStubX)
                    .Append(SourceStubX - Step).Append(SourceStubX + Step)
                    .Append(TargetStubX - Step).Append(TargetStubX + Step).Distinct().ToList();
        List<(double X, double Y)> Best = [];
        (int Bends, double Length) BestScore = (int.MaxValue, double.PositiveInfinity);
        double OuterMinX = BoundsById.Values.Min(Bounds => Bounds.CenterX - Bounds.Width / 2.0) - Step;
        double OuterMaxX = BoundsById.Values.Max(Bounds => Bounds.CenterX + Bounds.Width / 2.0) + Step;
        double OuterMinY = BoundsById.Values.Min(Bounds => Bounds.CenterY - Bounds.Height / 2.0) - Step;
        double OuterMaxY = BoundsById.Values.Max(Bounds => Bounds.CenterY + Bounds.Height / 2.0) + Step;
        foreach (double Lane in Lanes)
        {
            (double X, double Y) A = Travel switch
            {
                LayoutHintDirection.West => (OuterMaxX, Lane),
                LayoutHintDirection.East => (OuterMinX, Lane),
                LayoutHintDirection.North => (Lane, OuterMinY),
                _ => (Lane, OuterMaxY)
            };
            (double X, double Y) B = Travel switch
            {
                LayoutHintDirection.West => (OuterMinX, Lane),
                LayoutHintDirection.East => (OuterMaxX, Lane),
                LayoutHintDirection.North => (Lane, OuterMaxY),
                _ => (Lane, OuterMinY)
            };
            foreach (bool SourceHorizontalFirst in new[] { HorizontalTravel, !HorizontalTravel })
            foreach (bool TargetHorizontalFirst in new[] { HorizontalTravel, !HorizontalTravel })
            {
                var Candidate = new List<(double X, double Y)> { (StartX, StartY), (SourceStubX, SourceStubY) };
                AppendOrthogonal(Candidate, A, SourceHorizontalFirst);
                Candidate.Add(B);
                AppendOrthogonal(Candidate, (TargetStubX, TargetStubY), TargetHorizontalFirst);
                Candidate.Add((EndX, EndY));
                Candidate = SimplifyCollinear(Candidate);
                if (!HasNormalTerminals(Candidate, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                    || !HasTravelDirection(Candidate, Travel)
                    || !ClearsEndpointBodiesAfterTerminalRuns(Candidate, BoundsById, SourceId, TargetId)
                    || !PathIsValid(Candidate, BoundsById, SourceId, TargetId, Contract))
                    continue;
                var Score = ScorePath(Candidate);
                if (Score.CompareTo(BestScore) < 0)
                {
                    Best = Candidate;
                    BestScore = Score;
                }
            }
        }
        return Best;

    }

    private static void AppendOrthogonal(
        List<(double X, double Y)> Path,
        (double X, double Y) Target,
        bool HorizontalTravel)
    {
        (double X, double Y) Current = Path[^1];
        if (Math.Abs(Current.X - Target.X) < COLLINEAR_EPSILON_PX
            || Math.Abs(Current.Y - Target.Y) < COLLINEAR_EPSILON_PX)
        {
            Path.Add(Target);
            return;
        }
        Path.Add(HorizontalTravel ? (Target.X, Current.Y) : (Current.X, Target.Y));
        Path.Add(Target);
    }

    internal static bool HasTravelDirection(
        IReadOnlyList<(double X, double Y)> Path,
        LayoutHintDirection Direction)
    {
        if (Path.Count < 4) return false;
        for (int Segment = 1; Segment < Path.Count - 2; Segment++)
        {
            double Dx = Path[Segment + 1].X - Path[Segment].X;
            double Dy = Path[Segment + 1].Y - Path[Segment].Y;
            bool Matches = Direction switch
            {
                LayoutHintDirection.North => Dy > COLLINEAR_EPSILON_PX && Math.Abs(Dx) < COLLINEAR_EPSILON_PX,
                LayoutHintDirection.South => Dy < -COLLINEAR_EPSILON_PX && Math.Abs(Dx) < COLLINEAR_EPSILON_PX,
                LayoutHintDirection.East => Dx > COLLINEAR_EPSILON_PX && Math.Abs(Dy) < COLLINEAR_EPSILON_PX,
                _ => Dx < -COLLINEAR_EPSILON_PX && Math.Abs(Dy) < COLLINEAR_EPSILON_PX
            };
            if (Matches) return true;
        }
        return false;
    }

    private static double ResolveFrameExitLength(
        double X, double Y, string? Side, double Minimum,
        string EndpointId, string OtherId, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!DiagramGridLayout.IsInsideGroup(Contract, EndpointId, Group.Id)
                || DiagramGridLayout.IsInsideGroup(Contract, OtherId, Group.Id)
                || !BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds Frame))
            {
                continue;
            }

            if (BoundsById.TryGetValue(OtherId, out FlowchartLayoutNodeBounds Other))
            {
                bool BeyondSide = Side switch
                {
                    "left" => Other.CenterX < Frame.CenterX - Frame.Width / 2,
                    "right" => Other.CenterX > Frame.CenterX + Frame.Width / 2,
                    "top" => Other.CenterY > Frame.CenterY + Frame.Height / 2,
                    "bottom" => Other.CenterY < Frame.CenterY - Frame.Height / 2,
                    _ => false
                };
                // If the other end approaches a different face, the route
                // may turn inside the frame after crossing that nearer face.
                // Forcing this terminal past the far border causes reentry.
                if (!BeyondSide) continue;
            }

            double Distance = Side switch
            {
                "left" => X - (Frame.CenterX - Frame.Width / 2),
                "right" => Frame.CenterX + Frame.Width / 2 - X,
                "top" => Frame.CenterY + Frame.Height / 2 - Y,
                "bottom" => Y - (Frame.CenterY - Frame.Height / 2),
                _ => 0
            };
            Minimum = Math.Max(Minimum, Distance + DiagramGridMetrics.GRID_UNIT_PX);
        }
        // A port stub is only required to extend beyond the rounded corner. If a
        // nearby box blocks the usual stub, shorten it to the last clear grid track
        // so the route can turn before entering that box. Previously this check ran
        // only after a frame forced a longer stub, leaving ordinary adjacent nodes
        // able to block hinted exits.
        foreach (FlowchartLayoutNodeBounds Node in BoundsById.Values)
        {
            if (Node.NodeId == EndpointId || Node.NodeId == OtherId
                || Contract.Groups.Any(Group => Group.Id == Node.NodeId)) continue;
            double Left = Node.CenterX - Node.Width / 2;
            double Right = Node.CenterX + Node.Width / 2;
            double Bottom = Node.CenterY - Node.Height / 2;
            double Top = Node.CenterY + Node.Height / 2;
            double Distance = Side switch
            {
                "left" when Y > Bottom && Y < Top && Right < X => X - Right,
                "right" when Y > Bottom && Y < Top && Left > X => Left - X,
                "top" when X > Left && X < Right && Bottom > Y => Bottom - Y,
                "bottom" when X > Left && X < Right && Top < Y => Y - Top,
                _ => double.PositiveInfinity
            };
            Minimum = Math.Min(Minimum, Math.Max(CORNER_RADIUS_PX + 5,
                Distance - DiagramGridMetrics.GRID_UNIT_PX));
        }
        return Minimum;
    }

    /// <summary>Extends an allocated port along its explicit or actual outline normal.</summary>
    /// <param name="X">The port's horizontal coordinate.</param>
    /// <param name="Y">The port's vertical coordinate.</param>
    /// <param name="Bounds">The endpoint bounds.</param>
    /// <param name="Shape">The endpoint shape.</param>
    /// <param name="Side">The nominal rectangular side when no shape normal is available.</param>
    /// <param name="Length">Required outward distance.</param>
    /// <param name="NormalX">Allocated normal's horizontal component, or zero when unspecified.</param>
    /// <param name="NormalY">Allocated normal's vertical component, or zero when unspecified.</param>
    /// <returns>The end of the outward terminal run.</returns>
    internal static (double X, double Y) ResolvePortStub(
        double X, double Y, FlowchartLayoutNodeBounds Bounds, string? Shape,
        string? Side, double Length, double NormalX = 0, double NormalY = 0)
    {
        if (Math.Abs(NormalX) <= 1e-9 && Math.Abs(NormalY) <= 1e-9
            && FlowchartDiamondAttachHelper.IsDiamondShape(Shape))
            FlowchartDiamondAttachHelper.TryResolveOutwardNormal(Bounds, X, Y, out NormalX, out NormalY);
        if (Math.Abs(NormalX) > 1e-9 || Math.Abs(NormalY) > 1e-9)
        {
            double Magnitude = Math.Sqrt((NormalX * NormalX) + (NormalY * NormalY));
            return (X + NormalX / Magnitude * Length, Y + NormalY / Magnitude * Length);
        }
        return Side switch
        {
            "left" => (X - Length, Y),
            "right" => (X + Length, Y),
            "top" => (X, Y + Length),
            "bottom" => (X, Y - Length),
            _ => FlowchartPerpendicularRouteBuilder.ResolveStubOuterPoint(X, Y, Bounds, Shape, Length)
        };
    }

    private static bool HasNormalTerminals(
        IReadOnlyList<(double X, double Y)> Path,
        double SourceStubX, double SourceStubY, double TargetStubX, double TargetStubY)
    {
        if (Path.Count < 2)
        {
            return false;
        }

        return FollowsNormal(Path[0], Path[1], (SourceStubX, SourceStubY))
            && FollowsNormal(Path[^1], Path[^2], (TargetStubX, TargetStubY));
    }

    private static bool FollowsNormal(
        (double X, double Y) Port, (double X, double Y) Next, (double X, double Y) Stub)
    {
        double Dx = Next.X - Port.X;
        double Dy = Next.Y - Port.Y;
        double NormalX = Stub.X - Port.X;
        double NormalY = Stub.Y - Port.Y;
        return Math.Abs((Dx * NormalY) - (Dy * NormalX)) < COLLINEAR_EPSILON_PX
            && (Dx * NormalX) + (Dy * NormalY) > 0;
    }

    private static List<(double X, double Y)> BuildForcedCorridorRoute(
        double StartX,
        double StartY,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        bool Horizontal,
        bool RequireLegal)
    {
        if (!Horizontal)
        {
            double LeftCorridorX = FlowchartRouteObstacleHelper.ResolveCorridorXBesideVerticalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            double RightCorridorX = ResolveCorridorXOnOppositeSide(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            List<List<(double X, double Y)>> CorridorCandidates =
            [
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (LeftCorridorX, SourceStubY),
                    (LeftCorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ],
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (RightCorridorX, SourceStubY),
                    (RightCorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]
            ];

            foreach (List<(double X, double Y)> Candidate in CorridorCandidates)
            {
                List<(double X, double Y)> Simplified = SimplifyCollinear(Candidate);
                if (HasNormalTerminals(Simplified, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                    && ClearsEndpointBodiesAfterTerminalRuns(Simplified, BoundsById, SourceId, TargetId)
                    && PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
                {
                    return Simplified;
                }
            }
        }
        else
        {
            double CorridorY = FlowchartRouteObstacleHelper.ResolveCorridorYAboveHorizontalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubX,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            List<(double X, double Y)> Candidate =
            [
                (StartX, StartY),
                (SourceStubX, SourceStubY),
                (SourceStubX, CorridorY),
                (TargetStubX, CorridorY),
                (TargetStubX, TargetStubY),
                (EndX, EndY)
            ];
            List<(double X, double Y)> Simplified = SimplifyCollinear(Candidate);
            if (HasNormalTerminals(Simplified, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                && ClearsEndpointBodiesAfterTerminalRuns(Simplified, BoundsById, SourceId, TargetId)
                && PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
            {
                return Simplified;
            }
        }

        double ExteriorClearance = FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX;
        double ExteriorLeft = BoundsById.Values.Min(Box => Box.CenterX - Box.Width / 2.0) - ExteriorClearance;
        double ExteriorRight = BoundsById.Values.Max(Box => Box.CenterX + Box.Width / 2.0) + ExteriorClearance;
        foreach (double ExteriorX in new[] { ExteriorLeft, ExteriorRight })
        {
            List<(double X, double Y)> ExteriorCandidate = SimplifyCollinear(
            [
                (StartX, StartY), (SourceStubX, SourceStubY),
                (ExteriorX, SourceStubY), (ExteriorX, TargetStubY),
                (TargetStubX, TargetStubY), (EndX, EndY)
            ]);
            if (HasNormalTerminals(ExteriorCandidate, SourceStubX, SourceStubY,
                    TargetStubX, TargetStubY)
                && ClearsEndpointBodiesAfterTerminalRuns(ExteriorCandidate, BoundsById, SourceId, TargetId)
                && PathIsValid(ExteriorCandidate, BoundsById, SourceId, TargetId, Contract))
                return ExteriorCandidate;
        }

        List<(double X, double Y)> LastResort =
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (SourceStubX, TargetStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ];
        List<(double X, double Y)> SimplifiedLastResort = SimplifyCollinear(LastResort);
        if (HasNormalTerminals(SimplifiedLastResort, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
            && ClearsEndpointBodiesAfterTerminalRuns(SimplifiedLastResort, BoundsById, SourceId, TargetId)
            && PathIsValid(SimplifiedLastResort, BoundsById, SourceId, TargetId, Contract))
        {
            return SimplifiedLastResort;
        }

        return RequireLegal ? [] : LastResort;
    }

    private static IEnumerable<List<(double X, double Y)>> EnumerateCandidates(
        double StartX,
        double StartY,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        FlowchartDirection Direction,
        bool Horizontal,
        bool IncludeExteriorCorridors)
    {
        List<List<(double X, double Y)>> Results = BuildStubBridgeCandidates(
            StartX,
            StartY,
            SourceStubX,
            SourceStubY,
            TargetStubX,
            TargetStubY,
            EndX,
            EndY);

        if (!Horizontal)
        {
            double CorridorX = FlowchartRouteObstacleHelper.ResolveCorridorXBesideVerticalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            if (Math.Abs(CorridorX - SourceStubX) > COLLINEAR_EPSILON_PX)
            {
                Results.Add(
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (CorridorX, SourceStubY),
                    (CorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]);
            }

            double RightCorridorX = ResolveCorridorXOnOppositeSide(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            if (Math.Abs(RightCorridorX - SourceStubX) > COLLINEAR_EPSILON_PX
                && Math.Abs(RightCorridorX - CorridorX) > COLLINEAR_EPSILON_PX)
            {
                Results.Add(
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (RightCorridorX, SourceStubY),
                    (RightCorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]);
            }

            if (IncludeExteriorCorridors)
            {
                double Clearance = FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX;
                double ExteriorLeft = BoundsById.Values.Min(Box => Box.CenterX - Box.Width / 2.0) - Clearance;
                double ExteriorRight = BoundsById.Values.Max(Box => Box.CenterX + Box.Width / 2.0) + Clearance;
                foreach (double ExteriorX in new[] { ExteriorLeft, ExteriorRight })
                {
                    Results.Add(
                    [
                        (StartX, StartY),
                        (SourceStubX, SourceStubY),
                        (ExteriorX, SourceStubY),
                        (ExteriorX, TargetStubY),
                        (TargetStubX, TargetStubY),
                        (EndX, EndY)
                    ]);
                }
            }
        }
        else
        {
            double CorridorY = FlowchartRouteObstacleHelper.ResolveCorridorYAboveHorizontalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubX,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            if (Math.Abs(CorridorY - SourceStubY) > COLLINEAR_EPSILON_PX)
            {
                Results.Add(
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (SourceStubX, CorridorY),
                    (TargetStubX, CorridorY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]);
            }

            double BelowCorridorY = FlowchartRouteObstacleHelper.ResolveCorridorYBelowHorizontalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubX,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            if (Math.Abs(BelowCorridorY - SourceStubY) > COLLINEAR_EPSILON_PX
                && Math.Abs(BelowCorridorY - CorridorY) > COLLINEAR_EPSILON_PX)
            {
                Results.Add(
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (SourceStubX, BelowCorridorY),
                    (TargetStubX, BelowCorridorY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]);
            }

            if (IncludeExteriorCorridors)
            {
                double Clearance = FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX;
                double ExteriorTop = BoundsById.Values.Max(Box => Box.CenterY + Box.Height / 2.0) + Clearance;
                double ExteriorBottom = BoundsById.Values.Min(Box => Box.CenterY - Box.Height / 2.0) - Clearance;
                foreach (double ExteriorY in new[] { ExteriorTop, ExteriorBottom })
                {
                    Results.Add(
                    [
                        (StartX, StartY),
                        (SourceStubX, SourceStubY),
                        (SourceStubX, ExteriorY),
                        (TargetStubX, ExteriorY),
                        (TargetStubX, TargetStubY),
                        (EndX, EndY)
                    ]);
                }
            }
        }

        return Results;
    }

    private static List<List<(double X, double Y)>> BuildStubBridgeCandidates(
        double StartX,
        double StartY,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double EndX,
        double EndY)
    {
        List<List<(double X, double Y)>> Results = [];
        if (Math.Abs(StartX - EndX) <= COLLINEAR_EPSILON_PX || Math.Abs(StartY - EndY) <= COLLINEAR_EPSILON_PX)
        {
            Results.Add([(StartX, StartY), (EndX, EndY)]);
        }

        if (Math.Abs(SourceStubX - TargetStubX) <= COLLINEAR_EPSILON_PX
            || Math.Abs(SourceStubY - TargetStubY) <= COLLINEAR_EPSILON_PX)
        {
            Results.Add(
            [
                (StartX, StartY),
                (SourceStubX, SourceStubY),
                (TargetStubX, TargetStubY),
                (EndX, EndY)
            ]);
        }

        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (TargetStubX, SourceStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);
        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (SourceStubX, TargetStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);

        double MidX = Math.Round((SourceStubX + TargetStubX) / 2.0);
        double MidY = Math.Round((SourceStubY + TargetStubY) / 2.0);
        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (MidX, SourceStubY),
            (MidX, TargetStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);
        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (SourceStubX, MidY),
            (TargetStubX, MidY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);

        return Results;
    }

    /// <summary>Offers minimum-bend bridges from the final allocated ports, including extended diamond normals.</summary>
    /// <param name="Start">The allocated source outline point.</param>
    /// <param name="End">The allocated target outline point.</param>
    /// <param name="Source">The placed source bounds.</param>
    /// <param name="Target">The placed target bounds.</param>
    /// <param name="Exit">The allocated source side.</param>
    /// <param name="Entry">The allocated target side.</param>
    /// <param name="Nodes">Endpoint shape definitions.</param>
    /// <param name="Bounds">Node obstacles in layout coordinates.</param>
    /// <param name="Contract">The diagram's links and group membership.</param>
    /// <param name="PreparedObstacles">Optional immutable obstacle preparation shared by options against the same bounds.</param>
    /// <returns>Legal candidate polylines; selection remains the shared score's responsibility.</returns>
    internal static IEnumerable<List<(double X, double Y)>> MinimumBendTerminalCandidates(
        FlowchartLayoutPoint Start, FlowchartLayoutPoint End,
        FlowchartLayoutNodeBounds Source, FlowchartLayoutNodeBounds Target, string Exit, string Entry,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>? PreparedObstacles = null)
    {
        Nodes.TryGetValue(Source.NodeId, out var SourceNode);
        Nodes.TryGetValue(Target.NodeId, out var TargetNode);
        double SourceNx = 0, SourceNy = 0, TargetNx = 0, TargetNy = 0;
        if (FlowchartDiamondAttachHelper.IsDiamondShape(SourceNode?.Shape))
            FlowchartDiamondAttachHelper.TryResolveOutwardNormal(Source, Start.X, Start.Y, out SourceNx, out SourceNy);
        if (FlowchartDiamondAttachHelper.IsDiamondShape(TargetNode?.Shape))
            FlowchartDiamondAttachHelper.TryResolveOutwardNormal(Target, End.X, End.Y, out TargetNx, out TargetNy);
        var SourceStub = ResolvePortStub(Start.X, Start.Y, Source, SourceNode?.Shape, Exit, CORNER_RADIUS_PX * 2, SourceNx, SourceNy);
        // Candidate admission reserves two corner radii at each terminal.
        // Generating only the shorter arrow stub would discard every otherwise
        // legal two-bend bridge at the unchanged minimum-run gate.
        var TargetStub = ResolvePortStub(End.X, End.Y, Target, TargetNode?.Shape, Entry, CORNER_RADIUS_PX * 2, TargetNx, TargetNy);
        bool SourceDiamond = FlowchartDiamondAttachHelper.IsDiamondShape(SourceNode?.Shape);
        bool TargetDiamond = FlowchartDiamondAttachHelper.IsDiamondShape(TargetNode?.Shape);
        var RoutingObstacles = PreparedObstacles ?? PrepareTerminalObstacles(Contract, Bounds);
        var Obstacles = SourceDiamond || TargetDiamond
            ? RoutingObstacles.Where(Pair => !(SourceDiamond && Pair.Key == Source.NodeId)
                && !(TargetDiamond && Pair.Key == Target.NodeId)).ToDictionary(Pair => Pair.Key, Pair => Pair.Value)
            : RoutingObstacles;
        var StubPairs = new List<((double X, double Y) Source, (double X, double Y) Target)>
        {
            (SourceStub, TargetStub)
        };
        StubPairs.AddRange(ExtendTerminalRay((Start.X, Start.Y), SourceStub, TargetStub).Select(Point => (Point, TargetStub)));
        StubPairs.AddRange(ExtendTerminalRay((End.X, End.Y), TargetStub, SourceStub).Select(Point => (SourceStub, Point)));
        HashSet<string> Seen = new(StringComparer.Ordinal);
        foreach (var Pair in StubPairs)
        foreach (var Candidate in BuildStubBridgeCandidates(Start.X, Start.Y, Pair.Source.X, Pair.Source.Y,
                     Pair.Target.X, Pair.Target.Y, End.X, End.Y))
        {
            var Path = SimplifyCollinear(Candidate);
            if (!Seen.Add(string.Join(";", Path.Select(Point => FormattableString.Invariant($"{Point.X:R},{Point.Y:R}"))))) continue;
            if (!HasNormalTerminals(Path, SourceStub.X, SourceStub.Y, TargetStub.X, TargetStub.Y)
                || HasEndpointHook(Path)) continue;
            int First = Path.Count > 1 && Math.Abs(Path[0].X - Path[1].X) > COLLINEAR_EPSILON_PX
                && Math.Abs(Path[0].Y - Path[1].Y) > COLLINEAR_EPSILON_PX ? 1 : 0;
            int Last = Path.Count > 1 && Math.Abs(Path[^1].X - Path[^2].X) > COLLINEAR_EPSILON_PX
                && Math.Abs(Path[^1].Y - Path[^2].Y) > COLLINEAR_EPSILON_PX ? 1 : 0;
            if (First != 0 && !FlowchartDiamondAttachHelper.IsDiamondShape(SourceNode?.Shape)
                || Last != 0 && !FlowchartDiamondAttachHelper.IsDiamondShape(TargetNode?.Shape)) continue;
            var Body = Path.Skip(First).Take(Path.Count - First - Last).ToList();
            // Keep the existing orthogonal validator intact. Only the two
            // separately checked diamond-normal terminals sit outside its body.
            if (!PathIsValid(Body, Obstacles, Source.NodeId, Target.NodeId, Contract)) continue;
            bool Blocked = false;
            for (int Index = 1; Index < Path.Count; Index++)
                if (Index > 1 && SegmentEntersEndpoint(Path[Index - 1], Path[Index], Source, SourceDiamond)
                    || Index < Path.Count - 1 && SegmentEntersEndpoint(Path[Index - 1], Path[Index], Target, TargetDiamond)
                    || SegmentCrossesAnyNodeInterior(Index - 1, Path.Count - 2,
                        Path[Index - 1].X, Path[Index - 1].Y, Path[Index].X, Path[Index].Y,
                        Obstacles, Source.NodeId, Target.NodeId, Contract)) { Blocked = true; break; }
            if (!Blocked && !FlowchartFrameTitleBandHelper.RouteEntersAny(Path,
                    FlowchartFrameTitleBandHelper.Get(Contract, Bounds), Source.NodeId, Target.NodeId, Contract))
                yield return Path;
        }
    }

    /// <summary>Prepares the same node and title-band obstacles once for terminal options sharing immutable bounds.</summary>
    /// <param name="Contract">The declarations whose group frames are traversable except for their title strips.</param>
    /// <param name="Bounds">The complete, unchanged geometry for this candidate search.</param>
    /// <returns>An obstacle dictionary that callers must not mutate while evaluating port options.</returns>
    internal static IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> PrepareTerminalObstacles(
        DiagramContract Contract, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var Obstacles = FlowchartFrameTitleBandHelper.AddRoutingObstacles(Contract, Bounds);
        foreach (var Group in Contract.Groups) Obstacles.Remove(Group.Id);
        return Obstacles;
    }

    /// <summary>Checks terminal-body clearance against the drawn outline rather than a diamond's enclosing rectangle.</summary>
    /// <param name="A">The first point of the candidate run.</param>
    /// <param name="B">The second point of the candidate run.</param>
    /// <param name="Box">The endpoint outline bounds.</param>
    /// <param name="Diamond">True for a diamond; false for the existing rectangular clearance rule.</param>
    /// <returns>True when the segment crosses the endpoint interior.</returns>
    private static bool SegmentEntersEndpoint((double X, double Y) A, (double X, double Y) B,
        FlowchartLayoutNodeBounds Box, bool Diamond)
    {
        if (!Diamond) return FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Box);
        double Ratio = Box.Width / Box.Height;
        double Ax = A.X - Box.CenterX, Ay = (A.Y - Box.CenterY) * Ratio;
        double Bx = B.X - Box.CenterX, By = (B.Y - Box.CenterY) * Ratio;
        // In these coordinates the diamond becomes an axis-aligned square.
        // Empty triangular corners of its bounding box must not reject a legal
        // two-bend normal-to-grid candidate.
        return FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
            Ax + Ay, Ax - Ay, Bx + By, Bx - By,
            -Box.Width / 2, Box.Width / 2, Box.Width / 2, -Box.Width / 2);
    }

    /// <summary>Finds later turning points on a terminal normal at the opposite terminal's tracks.</summary>
    /// <param name="Port">The allocated outline point.</param>
    /// <param name="Stub">The minimum outward terminal point.</param>
    /// <param name="OtherStub">The opposite terminal's track coordinates.</param>
    /// <returns>Forward extensions that retain the minimum straight terminal run.</returns>
    private static IEnumerable<(double X, double Y)> ExtendTerminalRay(
        (double X, double Y) Port, (double X, double Y) Stub, (double X, double Y) OtherStub)
    {
        double Dx = Stub.X - Port.X, Dy = Stub.Y - Port.Y;
        if (Math.Abs(Dx) > 0.001)
        {
            double Scale = (OtherStub.X - Port.X) / Dx;
            if (Scale > 1) yield return (OtherStub.X, Port.Y + Scale * Dy);
        }
        if (Math.Abs(Dy) > 0.001)
        {
            double Scale = (OtherStub.Y - Port.Y) / Dy;
            if (Scale > 1) yield return (Port.X + Scale * Dx, OtherStub.Y);
        }
    }

    private static double ResolveCorridorXOnOppositeSide(
        double ChordX,
        double StartY,
        double EndY,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double ClearancePx)
    {
        double FlowMinY = Math.Min(StartY, EndY);
        double FlowMaxY = Math.Max(StartY, EndY);
        double CorridorX = ChordX;
        bool HasObstacle = false;

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            double NodeTop = CandidateBounds.CenterY + (CandidateBounds.Height / 2.0);
            double NodeBottom = CandidateBounds.CenterY - (CandidateBounds.Height / 2.0);
            if (NodeTop <= FlowMinY || NodeBottom >= FlowMaxY)
            {
                continue;
            }

            if (ChordX < NodeLeft || ChordX > NodeRight)
            {
                continue;
            }

            HasObstacle = true;
            if (NodeRight + ClearancePx > CorridorX)
            {
                CorridorX = NodeRight + ClearancePx;
            }
        }

        if (!HasObstacle)
        {
            return ChordX;
        }

        return CorridorX;
    }

    internal static bool PathIsValid(
        List<(double X, double Y)> Path,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        int MaxCorners = 4)
    {
        if (Path.Count < 2)
        {
            return false;
        }

        if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Path))
        {
            return false;
        }

        if (HasEndpointHook(Path))
        {
            return false;
        }

        if (CountCorners(Path) > MaxCorners)
        {
            return false;
        }

        for (int Index = 1; Index < Path.Count; Index++)
        {
            (double SegmentStartX, double SegmentStartY) = Path[Index - 1];
            (double SegmentEndX, double SegmentEndY) = Path[Index];
            if (SegmentCrossesAnyNodeInterior(
                    Index - 1,
                    Path.Count - 2,
                    SegmentStartX,
                    SegmentStartY,
                    SegmentEndX,
                    SegmentEndY,
                    BoundsById,
                    SourceId,
                    TargetId,
                    Contract))
            {
                return false;
            }
        }

        IReadOnlyList<FlowchartFrameTitleBandHelper.Band> TitleBands =
            FlowchartFrameTitleBandHelper.Get(Contract, BoundsById);
        if (FlowchartFrameTitleBandHelper.RouteEntersAny(Path, TitleBands, SourceId, TargetId, Contract))
        {
            return false;
        }

        return true;
    }

    /// <summary>Orders legal local candidates by bend count, then exact travelled length.</summary>
    /// <param name="Path">The simplified candidate, including its terminal runs.</param>
    /// <returns>A lexicographic cost: a shorter detour cannot buy an additional bend.</returns>
    private static (int Bends, double Length) ScorePath(List<(double X, double Y)> Path)
    {
        double Length = 0;
        for (int Index = 1; Index < Path.Count; Index++)
        {
            double Dx = Path[Index].X - Path[Index - 1].X;
            double Dy = Path[Index].Y - Path[Index - 1].Y;
            Length += Math.Sqrt(Dx * Dx + Dy * Dy);
        }
        return (CountCorners(Path), Length);
    }

    private static bool SegmentCrossesAnyNodeInterior(
        int SegmentIndex,
        int LastSegmentIndex,
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds NodeBounds = Entry.Value;
            // Most bounds are nowhere near this run. Perform the identical
            // geometric rejection before resolving group membership or title
            // crossing exceptions for every unrelated node and candidate.
            if (!FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(StartX, StartY, EndX, EndY, NodeBounds))
                continue;
            if (FlowchartFrameTitleBandHelper.TryGetObstacleBand(NodeBounds, out FlowchartFrameTitleBandHelper.Band TitleBand)
                && FlowchartFrameTitleBandHelper.IsLegalTopPortCrossing(TitleBand,
                    StartX, StartY, EndX, EndY, SourceId, TargetId, Contract))
            {
                continue;
            }
            bool IsSource = string.Equals(NodeBounds.NodeId, SourceId, StringComparison.Ordinal);
            bool IsTarget = string.Equals(NodeBounds.NodeId, TargetId, StringComparison.Ordinal);
            if (IsSource && SegmentIndex == 0)
            {
                continue;
            }

            if (IsTarget && SegmentIndex == LastSegmentIndex)
            {
                continue;
            }

            if (IsGroupFrameObstacle(Contract, NodeBounds.NodeId))
            {
                bool SourceInside = DiagramGridLayout.IsInsideGroup(Contract, SourceId, NodeBounds.NodeId);
                bool TargetInside = DiagramGridLayout.IsInsideGroup(Contract, TargetId, NodeBounds.NodeId);
                if (SourceInside || TargetInside)
                {
                    continue;
                }
            }

            return true;
        }

        return false;
    }

    private static bool IsGroupFrameObstacle(DiagramContract Contract, string NodeId)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts genuine changes of direction, including diamond-normal transitions.</summary>
    /// <param name="Path">The complete candidate polyline.</param>
    /// <returns>The number of non-collinear joins.</returns>
    private static int CountCorners(List<(double X, double Y)> Path)
    {
        if (Path.Count < 3)
        {
            return 0;
        }

        int Corners = 0;
        for (int Index = 1; Index < Path.Count - 1; Index++)
        {
            double Ax = Path[Index].X - Path[Index - 1].X;
            double Ay = Path[Index].Y - Path[Index - 1].Y;
            double Bx = Path[Index + 1].X - Path[Index].X;
            double By = Path[Index + 1].Y - Path[Index].Y;
            if (Math.Abs(Ax * By - Ay * Bx) > COLLINEAR_EPSILON_PX)
            {
                Corners++;
            }
        }

        return Corners;
    }

    private static bool HasEndpointHook(List<(double X, double Y)> Path)
    {
        if (Path.Count < 3)
        {
            return false;
        }

        return HasDirectionReversalNear(Path, NearStart: true) || HasDirectionReversalNear(Path, NearStart: false);
    }

    private static bool HasDirectionReversalNear(List<(double X, double Y)> Path, bool NearStart)
    {
        int SegmentCount = Path.Count - 1;
        if (SegmentCount < 2)
        {
            return false;
        }

        int StartIndex = NearStart ? 0 : Math.Max(0, SegmentCount - 2);
        int EndIndex = NearStart ? Math.Min(1, SegmentCount - 1) : SegmentCount - 1;
        for (int SegmentIndex = StartIndex; SegmentIndex < EndIndex; SegmentIndex++)
        {
            (double Ax, double Ay) = Normalize(
                Path[SegmentIndex + 1].X - Path[SegmentIndex].X,
                Path[SegmentIndex + 1].Y - Path[SegmentIndex].Y);
            (double Bx, double By) = Normalize(
                Path[SegmentIndex + 2].X - Path[SegmentIndex + 1].X,
                Path[SegmentIndex + 2].Y - Path[SegmentIndex + 1].Y);
            if ((Ax * Bx) + (Ay * By) < HOOK_DOT_PRODUCT_THRESHOLD)
            {
                return true;
            }
        }

        return false;
    }

    private static (double X, double Y) Normalize(double VectorX, double VectorY)
    {
        double Length = Math.Sqrt((VectorX * VectorX) + (VectorY * VectorY));
        if (Length <= 1e-6)
        {
            return (0.0, 0.0);
        }

        return (VectorX / Length, VectorY / Length);
    }

    internal static List<(double X, double Y)> SimplifyCollinear(List<(double X, double Y)> Path)
    {
        if (Path.Count < 3)
        {
            return Path;
        }

        List<(double X, double Y)> Result = [Path[0]];
        for (int Index = 1; Index < Path.Count - 1; Index++)
        {
            (double Ax, double Ay) = Result[Result.Count - 1];
            (double Bx, double By) = Path[Index];
            (double Cx, double Cy) = Path[Index + 1];
            double Cross = ((Bx - Ax) * (Cy - By)) - ((By - Ay) * (Cx - Bx));
            double Dot = ((Bx - Ax) * (Cx - Bx)) + ((By - Ay) * (Cy - By));
            if (Math.Abs(Cross) < 1e-3 && Dot >= 0.0)
            {
                continue;
            }

            Result.Add(Path[Index]);
        }

        Result.Add(Path[Path.Count - 1]);
        return Result;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildBounds(
        List<DiagramGridLayout.GridNode> Placed,
        IReadOnlyList<DiagramGridLayout.GridFrame> Frames)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height
            };
        }

        foreach (DiagramGridLayout.GridFrame Frame in Frames)
        {
            BoundsById[Frame.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            };
        }

        return BoundsById;
    }

    private static int CountVisibleOutgoing(DiagramContract Contract, string SourceId)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal)
                && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                Count++;
            }
        }

        return Count;
    }
}
