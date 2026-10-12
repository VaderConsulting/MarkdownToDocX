using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Centres circular collectors and assigns distinct cardinal fan ports.</summary>
internal static class FlowchartCircularJunctionLayout
{
    internal static void AlignFans(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        foreach (var Junction in Nodes.Where(Node => Node.Shape == "circle"))
        {
            var Outgoing = Edges.Where(Edge => Edge.SourceId == Junction.Id).ToList();
            if (Outgoing.Count < 2 || Outgoing.Count > 3 || Outgoing.Any(Edge => !ById.ContainsKey(Edge.TargetId))) continue;
            var Children = Outgoing.Select(Edge => ById[Edge.TargetId]).ToList();
            if (Children.Select(Node => Node.Rank).Distinct().Count() != 1
                || Children.Any(Node => Node.GroupId != Junction.GroupId)) continue;
            double Centre = Children.Count == 3 ? Cross(Children.OrderBy(Cross).ElementAt(1))
                : (Children.Min(Cross) + Children.Max(Cross)) / 2;
            var Current = Junction;
            var Seen = new HashSet<string>();
            while (Seen.Add(Current.Id))
            {
                double Half = (Horizontal ? Current.Height : Current.Width) / 2.0;
                if (Nodes.Any(Node => Node != Current && Node.Rank == Current.Rank
                    && Math.Abs(Cross(Node) - Centre) < Half + (Horizontal ? Node.Height : Node.Width) / 2.0
                        + DiagramGridMetrics.NODE_GAP_PX)) break;
                if (Horizontal) Current.Y = Centre; else Current.X = Centre;
                var Incoming = Edges.Where(Edge => Edge.TargetId == Current.Id).ToList();
                if (Incoming.Count != 1 || !ById.TryGetValue(Incoming[0].SourceId, out var Previous)
                    || Previous.Rank >= Current.Rank || Previous.GroupId != Current.GroupId
                    || Edges.Count(Edge => Edge.SourceId == Previous.Id) != 1) break;
                Current = Previous;
            }
        }
        foreach (var Junction in Nodes.Where(Node => Node.Shape == "circle"))
        {
            var Incoming = Edges.Where(Edge => Edge.TargetId == Junction.Id).ToList();
            if (Incoming.Count < 2 || Incoming.Count > 3 || Incoming.Any(Edge => !ById.ContainsKey(Edge.SourceId))) continue;
            var Parents = Incoming.Select(Edge => ById[Edge.SourceId]).ToList();
            if (Parents.Select(Node => Node.Rank).Distinct().Count() != 1
                || Parents.Any(Node => Node.GroupId != Junction.GroupId)) continue;
            double Centre = Parents.Count == 3 ? Cross(Parents.OrderBy(Cross).ElementAt(1))
                : (Parents.Min(Cross) + Parents.Max(Cross)) / 2;
            var Current = Junction;
            var Seen = new HashSet<string>();
            while (Seen.Add(Current.Id))
            {
                double Half = (Horizontal ? Current.Height : Current.Width) / 2.0;
                if (Nodes.Any(Node => Node != Current && Node.Rank == Current.Rank
                    && Math.Abs(Cross(Node) - Centre) < Half + (Horizontal ? Node.Height : Node.Width) / 2.0
                        + DiagramGridMetrics.NODE_GAP_PX)) break;
                if (Horizontal) Current.Y = Centre; else Current.X = Centre;
                var Outgoing = Edges.Where(Edge => Edge.SourceId == Current.Id).ToList();
                if (Outgoing.Count != 1 || !ById.TryGetValue(Outgoing[0].TargetId, out var Next)
                    || Next.Rank <= Current.Rank || Next.GroupId != Current.GroupId
                    || Edges.Count(Edge => Edge.TargetId == Next.Id) != 1) break;
                Current = Next;
            }
        }
    }

    internal static void RebuildFans(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft;
        bool Positive = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.BottomToTop;
        string Down = Horizontal ? (Positive ? "right" : "left") : (Positive ? "top" : "bottom");
        string Up = Horizontal ? (Positive ? "left" : "right") : (Positive ? "bottom" : "top");
        double Main(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterX : Box.CenterY;
        double Cross(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterY : Box.CenterX;
        var Changes = new Dictionary<FlowchartLayoutEdgeRoute, (string Exit, string Entry)>();
        foreach (var Junction in Nodes.Values.Where(Node => Node.Shape == "circle" && Bounds.ContainsKey(Node.Id)))
        foreach (bool Outgoing in new[] { true, false })
        {
            var Box = Bounds[Junction.Id];
            var Fan = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
                && (Outgoing ? Route.SourceId : Route.TargetId) == Junction.Id).ToList();
            if (Fan.Count < 2 || Fan.Count > 3) continue;
            var Others = Fan.Select(Route => Outgoing ? Route.TargetId : Route.SourceId).ToList();
            if (Others.Distinct().Count() != Fan.Count || Others.Any(Id => !Bounds.ContainsKey(Id))
                || Others.Any(Id => (Main(Bounds[Id]) - Main(Box)) * (Positive ? 1 : -1) * (Outgoing ? 1 : -1) <= 0)) continue;
            var Sides = Others.Select(Id => Math.Abs(Cross(Bounds[Id]) - Cross(Box)) <= 0.5
                ? (Outgoing ? Down : Up)
                : Horizontal ? (Cross(Bounds[Id]) > Cross(Box) ? "top" : "bottom")
                    : (Cross(Bounds[Id]) > Cross(Box) ? "right" : "left")).ToList();
            if (Sides.Distinct().Count() != Fan.Count) continue;
            for (int Index = 0; Index < Fan.Count; Index++)
            {
                var Route = Fan[Index];
                var Previous = Changes.TryGetValue(Route, out var Existing) ? Existing : (Route.ExitSide, Route.EntrySide);
                Changes[Route] = Outgoing ? (Sides[Index], Previous.Item2) : (Previous.Item1, Sides[Index]);
            }
        }
        foreach (var Change in Changes)
        {
            var Route = Change.Key;
            var Source = Bounds[Route.SourceId]; var Target = Bounds[Route.TargetId];
            var Start = Change.Value.Exit == Route.ExitSide ? (Route.Points[0].X, Route.Points[0].Y)
                : Cardinal(Source, Change.Value.Exit);
            var End = Change.Value.Entry == Route.EntrySide ? (Route.Points[^1].X, Route.Points[^1].Y)
                : Cardinal(Target, Change.Value.Entry);
            var Path = DiagramGridRouteBuilder.BuildRoute(Start.Item1, Start.Item2, Source,
                Nodes.TryGetValue(Route.SourceId, out var SourceNode) ? SourceNode.Shape : "rect",
                Change.Value.Exit, End.Item1, End.Item2, Target,
                Nodes.TryGetValue(Route.TargetId, out var TargetNode) ? TargetNode.Shape : "rect", Change.Value.Entry,
                Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal);
            // A side port can feed an offset flow-facing port with one bend.
            // Keep a full grid run before the corner and room beside the arrow.
            if (Nodes.TryGetValue(Route.SourceId, out var JunctionNode) && JunctionNode.Shape == "circle"
                && Nodes.TryGetValue(Route.TargetId, out var TargetBox) && TargetBox.Shape is null or "" or "rect" or "rounded" or "round"
                && Change.Value.Exit != Down && Change.Value.Exit != Up && Change.Value.Entry == Up
                && !Contract.Edges.Any(Edge => Edge.Id == Route.EdgeId && !string.IsNullOrEmpty(Edge.Label)))
            {
                int Side = Change.Value.Exit is "right" or "top" ? 1 : -1;
                double Track = (Horizontal ? Start.Item2 : Start.Item1) + Side * DiagramGridMetrics.GRID_UNIT_PX;
                double Half = (Horizontal ? Target.Height : Target.Width) / 2;
                var OffsetEnd = Horizontal ? (End.Item1, Track) : (Track, End.Item2);
                var Corner = Horizontal ? (Start.Item1, Track) : (Track, Start.Item2);
                var Candidate = new List<(double X, double Y)> { Start, Corner, OffsetEnd };
                var Edge = Contract.Edges.First(Item => Item.Id == Route.EdgeId);
                var Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(Contract, Bounds, Edge);
                bool Clear = Math.Abs(Track - Cross(Target)) <= Half - 20
                    && Math.Abs(Main(Target) - Main(Source)) - (Horizontal ? Target.Width : Target.Height) / 2 >= 40
                    && Candidate.Zip(Candidate.Skip(1)).All(Segment => !Obstacles.Any(Box =>
                        FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X, Segment.First.Y,
                            Segment.Second.X, Segment.Second.Y, Box, -6)));
                foreach (var Peer in Layout.EdgeRoutes.Where(Peer => Peer != Route && Peer.Points.Count >= 2))
                {
                    var Points = Peer.Points.Select(Point => (Point.X, Point.Y)).ToList();
                    var Before = FlowchartBranchRouteOrder.Intersections(Path, Points);
                    var After = FlowchartBranchRouteOrder.Intersections(Candidate, Points);
                    if (After.Crossings > Before.Crossings || After.Overlap > Before.Overlap + 0.01
                        || new[] { Points[0], Points[^1] }.Any(Point => Math.Abs(Point.X - OffsetEnd.Item1)
                            + Math.Abs(Point.Y - OffsetEnd.Item2) < 6)) Clear = false;
                }
                if (Clear && Candidate.Count < Path.Count) Path = Candidate;
            }
            Route.ExitSide = Change.Value.Exit; Route.EntrySide = Change.Value.Entry;
            Route.Points.Clear();
            foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
        }
        NormalizeCirclePorts(Layout, Bounds, Nodes);
    }

    private static void NormalizeCirclePorts(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes)
    {
        foreach (NodeContract Node in Nodes.Values.Where(Node => Node.Shape == "circle" && Bounds.ContainsKey(Node.Id)))
        {
            FlowchartLayoutNodeBounds Box = Bounds[Node.Id];
            var Ends = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
                    && (Route.SourceId == Node.Id || Route.TargetId == Node.Id))
                .Select(Route => (Route, AtSource: Route.SourceId == Node.Id,
                    Side: Route.SourceId == Node.Id ? Route.ExitSide : Route.EntrySide))
                .GroupBy(End => End.Side, StringComparer.Ordinal);
            foreach (var SideGroup in Ends)
            {
                var Ordered = SideGroup.OrderBy(End => OtherCross(End.Route, End.AtSource, SideGroup.Key, Bounds)).ToList();
                for (int Index = 0; Index < Ordered.Count; Index++)
                {
                    double Fraction = (Index + 1.0) / (Ordered.Count + 1.0);
                    SetCirclePort(Ordered[Index].Route, Ordered[Index].AtSource, SideGroup.Key, Box, Fraction);
                }
            }
        }
    }

    private static double OtherCross(FlowchartLayoutEdgeRoute Route, bool AtSource, string Side,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        string OtherId = AtSource ? Route.TargetId : Route.SourceId;
        if (!Bounds.TryGetValue(OtherId, out FlowchartLayoutNodeBounds? Other)) return 0;
        return Side is "top" or "bottom" ? Other.CenterX : Other.CenterY;
    }

    private static void SetCirclePort(FlowchartLayoutEdgeRoute Route, bool AtSource, string Side,
        FlowchartLayoutNodeBounds Box, double Fraction)
    {
        double Radius = Math.Min(Box.Width, Box.Height) / 2.0;
        // Keep the outer slots away from the tangent, where rounding would leave no
        // useful straight run. Additional lines divide the usable arc evenly.
        double Along = (Fraction - 0.5) * 1.2 * Radius;
        double Normal = Math.Sqrt(Math.Max(0, Radius * Radius - Along * Along));
        FlowchartLayoutPoint Port = Side switch
        {
            "left" => NewPoint(Box.CenterX - Normal, Box.CenterY + Along),
            "right" => NewPoint(Box.CenterX + Normal, Box.CenterY + Along),
            "top" => NewPoint(Box.CenterX + Along, Box.CenterY + Normal),
            _ => NewPoint(Box.CenterX + Along, Box.CenterY - Normal)
        };
        if (AtSource)
        {
            Route.Points[0] = Port;
            MakeTerminalRunOrthogonal(Route.Points, AtStart: true, Side, Port);
        }
        else
        {
            Route.Points[^1] = Port;
            MakeTerminalRunOrthogonal(Route.Points, AtStart: false, Side, Port);
        }
        CollapseRoute(Route.Points);
    }

    private static void MakeTerminalRunOrthogonal(List<FlowchartLayoutPoint> Points, bool AtStart,
        string Side, FlowchartLayoutPoint Port)
    {
        int NeighbourIndex = AtStart ? 1 : Points.Count - 2;
        FlowchartLayoutPoint Neighbour = Points[NeighbourIndex];
        bool Horizontal = Side is "left" or "right";
        if (Horizontal) Neighbour.Y = Port.Y; else Neighbour.X = Port.X;
        Points[NeighbourIndex] = Neighbour;
        if (Points.Count != 2) return;
        FlowchartLayoutPoint Other = AtStart ? Points[^1] : Points[0];
        if (Horizontal ? Math.Abs(Other.Y - Port.Y) < 0.5 : Math.Abs(Other.X - Port.X) < 0.5) return;
        FlowchartLayoutPoint Corner = Horizontal
            ? NewPoint(Other.X, Port.Y)
            : NewPoint(Port.X, Other.Y);
        Points.Insert(1, Corner);
    }

    private static void CollapseRoute(List<FlowchartLayoutPoint> Points)
    {
        for (int Index = Points.Count - 2; Index > 0; Index--)
        {
            FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index], C = Points[Index + 1];
            if ((Math.Abs(A.X - B.X) < 0.5 && Math.Abs(B.X - C.X) < 0.5)
                || (Math.Abs(A.Y - B.Y) < 0.5 && Math.Abs(B.Y - C.Y) < 0.5))
                Points.RemoveAt(Index);
        }
    }

    private static FlowchartLayoutPoint NewPoint(double X, double Y) =>
        new() { X = Math.Round(X), Y = Math.Round(Y) };

    private static (double X, double Y) Cardinal(FlowchartLayoutNodeBounds Box, string Side)
    {
        double Radius = Math.Min(Box.Width, Box.Height) / 2;
        return Side switch
        {
            "left" => (Box.CenterX - Radius, Box.CenterY),
            "right" => (Box.CenterX + Radius, Box.CenterY),
            "top" => (Box.CenterX, Box.CenterY + Radius),
            _ => (Box.CenterX, Box.CenterY - Radius)
        };
    }
}
