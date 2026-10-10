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
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2
                || !BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
                || !Route.Points.Zip(Route.Points.Skip(1)).Any(Pair =>
                    Math.Abs(Pair.First.X - Pair.Second.X) > COLLINEAR_EPSILON_PX
                    && Math.Abs(Pair.First.Y - Pair.Second.Y) > COLLINEAR_EPSILON_PX)
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
                BoundsById, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal);
            (double SourceStubX, double SourceStubY) = ResolvePortStub(
                Path[0].X, Path[0].Y, Source, SourceNode?.Shape, Route.ExitSide, 1);
            (double TargetStubX, double TargetStubY) = ResolvePortStub(
                Path[^1].X, Path[^1].Y, Target, TargetNode?.Shape, Route.EntrySide, 1);
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
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildBounds(Placed, Frames);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);

            if (SourceNode != null
                && FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape)
                && CountVisibleOutgoing(Contract, Route.SourceId) >= 2)
            {
                continue;
            }

            if (FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract,
                    Route.SourceId,
                    SourceNode?.Shape,
                    Direction,
                    BoundsById))
            {
                continue;
            }

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

            if (RouteEdge != null
                && FlowchartBackEdgeHelper.FindBackEdges(Contract).Contains(RouteEdge))
            {
                continue;
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

            List<(double X, double Y)> Path = BuildRoute(
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
                Horizontal);

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in Path)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
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
        bool Horizontal)
    {
        // The rounded corner consumes one radius of the terminal segment.
        // Keep another radius at the source and an arrow length at the target.
        double SourceStubLength = ResolveFrameExitLength(StartX, StartY, ExitSide,
            CORNER_RADIUS_PX * 2, SourceId, TargetId, Contract, BoundsById);
        double TargetStubLength = ResolveFrameExitLength(EndX, EndY, EntrySide,
            CORNER_RADIUS_PX + 10, TargetId, SourceId, Contract, BoundsById);
        (double SourceStubX, double SourceStubY) = ResolvePortStub(
            StartX, StartY, SourceBounds, SourceShape, ExitSide, SourceStubLength);
        (double TargetStubX, double TargetStubY) = ResolvePortStub(
            EndX, EndY, TargetBounds, TargetShape, EntrySide, TargetStubLength);

        if (FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(StartX, StartY, EndX, EndY)
            && HasNormalTerminals([(StartX, StartY), (EndX, EndY)],
                SourceStubX, SourceStubY, TargetStubX, TargetStubY)
            && !FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                StartX,
                StartY,
                EndX,
                EndY,
                BoundsById,
                SourceId,
                TargetId))
        {
            return [(StartX, StartY), (EndX, EndY)];
        }

        List<(double X, double Y)> Best = [];
        int BestScore = int.MaxValue;
        foreach (List<(double X, double Y)> Candidate in EnumerateCandidates(
                     StartX,
                     StartY,
                     SourceStubX,
                     SourceStubY,
                     TargetStubX,
                     TargetStubY,
                     EndX,
                     EndY,
                     BoundsById,
                     SourceId,
                     TargetId,
                     Contract,
                     Direction,
                     Horizontal))
        {
            List<(double X, double Y)> Simplified = SimplifyCollinear(Candidate);
            if (!HasNormalTerminals(Simplified, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
                || !PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
            {
                continue;
            }

            int Score = ScorePath(Simplified);
            if (Score < BestScore)
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
            BoundsById,
            SourceId,
            TargetId);
        List<(double X, double Y)> SimplifiedFallback = SimplifyCollinear(Fallback);
        if (HasNormalTerminals(SimplifiedFallback, SourceStubX, SourceStubY, TargetStubX, TargetStubY)
            && PathIsValid(SimplifiedFallback, BoundsById, SourceId, TargetId, Contract))
        {
            return SimplifiedFallback;
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
            BoundsById,
            SourceId,
            TargetId,
            Contract,
            Horizontal);
    }

    private static double ResolveFrameExitLength(
        double X, double Y, string? Side, double Minimum,
        string EndpointId, string OtherId, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double TerminalMinimum = Minimum;
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
        if (Minimum <= TerminalMinimum) return Minimum;

        // A containing frame must not extend a normal terminal through
        // another member. Leave room to turn before the nearest body;
        // the remaining route can cross the frame on a clear grid run.
        foreach (FlowchartLayoutNodeBounds Node in BoundsById.Values)
        {
            if (Node.NodeId == EndpointId || Node.NodeId == OtherId
                || Contract.Groups.Any(Group => Group.Id == Node.NodeId)
                || !Contract.Groups.Any(Group => DiagramGridLayout.IsInsideGroup(Contract, EndpointId, Group.Id)
                    && DiagramGridLayout.IsInsideGroup(Contract, Node.NodeId, Group.Id)
                    && !DiagramGridLayout.IsInsideGroup(Contract, OtherId, Group.Id))) continue;
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
            Minimum = Math.Min(Minimum,
                Math.Max(TerminalMinimum, Distance - DiagramGridMetrics.GRID_UNIT_PX));
        }
        return Minimum;
    }

    private static (double X, double Y) ResolvePortStub(
        double X, double Y, FlowchartLayoutNodeBounds Bounds, string? Shape,
        string? Side, double Length)
    {
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
        bool Horizontal)
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
                if (PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
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
            if (PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
            {
                return Simplified;
            }
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
        if (PathIsValid(SimplifiedLastResort, BoundsById, SourceId, TargetId, Contract))
        {
            return SimplifiedLastResort;
        }

        return LastResort;
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
        bool Horizontal)
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

    private static bool PathIsValid(
        List<(double X, double Y)> Path,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract)
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

        if (CountCorners(Path) > 4)
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

        return true;
    }

    private static int ScorePath(List<(double X, double Y)> Path)
    {
        int Score = CountCorners(Path) * 100;
        for (int Index = 1; Index < Path.Count; Index++)
        {
            double Length = Math.Abs(Path[Index].X - Path[Index - 1].X)
                + Math.Abs(Path[Index].Y - Path[Index - 1].Y);
            Score += (int)Math.Round(Length / 10.0);
        }

        return Score;
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

            if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(StartX, StartY, EndX, EndY, NodeBounds))
            {
                return true;
            }
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

    private static int CountCorners(List<(double X, double Y)> Path)
    {
        if (Path.Count < 3)
        {
            return 0;
        }

        int Corners = 0;
        for (int Index = 1; Index < Path.Count - 1; Index++)
        {
            bool IncomingHorizontal = Math.Abs(Path[Index].X - Path[Index - 1].X) > COLLINEAR_EPSILON_PX;
            bool OutgoingHorizontal = Math.Abs(Path[Index + 1].X - Path[Index].X) > COLLINEAR_EPSILON_PX;
            if (IncomingHorizontal != OutgoingHorizontal)
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

    private static List<(double X, double Y)> SimplifyCollinear(List<(double X, double Y)> Path)
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
