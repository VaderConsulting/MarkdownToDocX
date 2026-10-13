using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Restores explicit endpoint and track directions after automatic route repair.</summary>
internal static class FlowchartRoutingHints
{
    internal static void Apply(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        bool RepairAutomaticRoutesEnabled = true)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> HintedPorts =
            SelectHintedPorts(Layout, Contract, Bounds);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2) continue;
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            RoutingHintContract? Hint = Edge?.RoutingHint;
            if (Hint?.IsActive != true
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
                continue;

            string Exit = Hint.Leave.HasValue
                ? DiagramGridLayout.HintSide(Hint.Leave.Value) : Route.ExitSide;
            string Entry = Hint.Enter.HasValue
                ? DiagramGridLayout.HintSide(Hint.Enter.Value) : Route.EntrySide;
            (double StartX, double StartY) = HintedPorts.TryGetValue((Route, true), out var StartPort)
                ? StartPort : ResolveEndpoint(Route, Source, Exit, true);
            (double EndX, double EndY) = HintedPorts.TryGetValue((Route, false), out var EndPort)
                ? EndPort : ResolveEndpoint(Route, Target, Entry, false);
            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
            Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
            List<(double X, double Y)> Path = DiagramGridRouteBuilder.BuildRoute(
                StartX, StartY, Source, SourceNode?.Shape, Exit,
                EndX, EndY, Target, TargetNode?.Shape, Entry,
                Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal, Hint.Travel,
                RequireLegal: true);
            if (Path.Count < 2)
            {
                Hint.UnhonouredReason = Hint.Travel.HasValue
                    ? "no legal route has the requested travel direction"
                    : "no legal route has the requested endpoint directions";
                continue;
            }

            Hint.UnhonouredReason = null;
            Route.ExitSide = Exit;
            Route.EntrySide = Entry;
            Route.Points.Clear();
            foreach ((double X, double Y) in Path)
                Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
        }

        if (RepairAutomaticRoutesEnabled
            && (Contract.Nodes.Any(Node => Node.PlacementHint?.IsActive == true)
                || Contract.Groups.Any(Group => Group.PlacementHint?.IsActive == true)))
            RepairAutomaticRoutes(Layout, Contract, Bounds, Nodes, Direction, Horizontal);
    }

    private static Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> SelectHintedPorts(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var Ends = new List<(FlowchartLayoutEdgeRoute Route, bool Source,
            FlowchartLayoutNodeBounds Box, FlowchartLayoutNodeBounds Other, string Side)>();
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            if (Edge?.RoutingHint?.IsActive != true
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)) continue;
            if (!IsDiamond(Route.SourceId))
                Ends.Add((Route, true, Source, Target, Edge.RoutingHint.Leave.HasValue
                    ? DiagramGridLayout.HintSide(Edge.RoutingHint.Leave.Value) : Route.ExitSide));
            if (!IsDiamond(Route.TargetId))
                Ends.Add((Route, false, Target, Source, Edge.RoutingHint.Enter.HasValue
                    ? DiagramGridLayout.HintSide(Edge.RoutingHint.Enter.Value) : Route.EntrySide));
        }

        var Result = new Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)>();
        foreach (var Group in Ends.GroupBy(End => (End.Box.NodeId, End.Side)))
        {
            var Ordered = Group.Key.Side is "left" or "right"
                ? Group.OrderByDescending(End => End.Other.CenterY).ThenBy(End => End.Route.EdgeId, StringComparer.Ordinal).ToArray()
                : Group.OrderBy(End => End.Other.CenterX).ThenBy(End => End.Route.EdgeId, StringComparer.Ordinal).ToArray();
            double Length = Group.Key.Side is "left" or "right" ? Ordered[0].Box.Height : Ordered[0].Box.Width;
            int[] Slots = DiagramGridMetrics.EqualCentres(Math.Max(1, (int)Math.Floor(Length)), Ordered.Length);
            for (int Index = 0; Index < Ordered.Length; Index++)
            {
                var End = Ordered[Index];
                double Offset = Slots[Index] - Length / 2.0;
                (double X, double Y) Port = End.Side switch
                {
                    "left" => (End.Box.CenterX - End.Box.Width / 2.0, End.Box.CenterY + Offset),
                    "right" => (End.Box.CenterX + End.Box.Width / 2.0, End.Box.CenterY + Offset),
                    "top" => (End.Box.CenterX + Offset, End.Box.CenterY + End.Box.Height / 2.0),
                    _ => (End.Box.CenterX + Offset, End.Box.CenterY - End.Box.Height / 2.0)
                };
                Result[(End.Route, End.Source)] = Port;
            }
        }

        return Result;

        bool IsDiamond(string Id) => Nodes.TryGetValue(Id, out NodeContract? Node)
            && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
    }

    private static void RepairAutomaticRoutes(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        bool Horizontal)
    {
        string[] Sides = ["top", "bottom", "left", "right"];
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2) continue;
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            List<(double X, double Y)> CurrentPath = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
            bool BadTerminal = !LeavesBy(CurrentPath, Route.ExitSide) || !EntersBy(CurrentPath, Route.EntrySide);
            if (Edge?.RoutingHint?.IsActive == true || (!BadTerminal && CountCorners(Route.Points) <= 2)
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
                continue;

            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
            Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
            List<(string Exit, string Entry, List<(double X, double Y)> Path, int Corners, double Length)> Candidates = [];
            foreach (string Exit in Sides)
            foreach (string Entry in Sides)
            foreach ((double StartX, double StartY) in Ports(Source, Exit,
                Exit == Route.ExitSide ? Route.Points[0] : null))
            foreach ((double EndX, double EndY) in Ports(Target, Entry,
                Entry == Route.EntrySide ? Route.Points[^1] : null))
            {
                if (EndpointTaken(Layout, Route, Route.SourceId, StartX, StartY)
                    || EndpointTaken(Layout, Route, Route.TargetId, EndX, EndY)) continue;
                foreach (List<(double X, double Y)> RawPath in CandidatePaths(
                    StartX, StartY, Source, SourceNode, Exit,
                    EndX, EndY, Target, TargetNode, Entry,
                    Bounds, Route, Contract, Direction, Horizontal))
                {
                    List<(double X, double Y)> Path = DiagramGridRouteBuilder.SimplifyCollinear(RawPath);
                    int Corners = CountCorners(Path);
                    if (Path.Count < 2 || Corners > 2
                        || !LeavesBy(Path, Exit) || !EntersBy(Path, Entry)
                        || !DiagramGridRouteBuilder.PathIsValid(
                            Path, Bounds, Route.SourceId, Route.TargetId, Contract)) continue;
                    double Length = Path.Zip(Path.Skip(1)).Sum(Pair =>
                        Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y));
                    Candidates.Add((Exit, Entry, Path, Corners, Length));
                }
            }

            var Best = Candidates.OrderBy(Candidate => Candidate.Corners)
                .ThenBy(Candidate => Candidate.Length).FirstOrDefault();
            if (Best.Path == null) continue;
            Route.ExitSide = Best.Exit;
            Route.EntrySide = Best.Entry;
            Route.Points.Clear();
            foreach ((double X, double Y) in Best.Path)
                Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
        }
    }

    private static (double X, double Y) CentrePort(FlowchartLayoutNodeBounds Bounds, string Side) => Side switch
    {
        "left" => (Bounds.CenterX - Bounds.Width / 2.0, Bounds.CenterY),
        "right" => (Bounds.CenterX + Bounds.Width / 2.0, Bounds.CenterY),
        "top" => (Bounds.CenterX, Bounds.CenterY + Bounds.Height / 2.0),
        _ => (Bounds.CenterX, Bounds.CenterY - Bounds.Height / 2.0)
    };

    private static IEnumerable<(double X, double Y)> Ports(
        FlowchartLayoutNodeBounds Bounds,
        string Side,
        FlowchartLayoutPoint? Existing)
    {
        if (Existing != null) yield return (Existing.X, Existing.Y);
        (double CentreX, double CentreY) = CentrePort(Bounds, Side);
        double Span = Side is "top" or "bottom" ? Bounds.Width : Bounds.Height;
        double Margin = Math.Min(DiagramGridRouteBuilder.CORNER_RADIUS_PX + 3.0, Span / 4.0);
        double UsableHalf = Math.Max(0.0, Span / 2.0 - Margin);
        double Pitch = DiagramGridMetrics.GRID_UNIT_PX;
        IEnumerable<double> Offsets = Enumerable.Range(0,
                (int)Math.Floor(UsableHalf / Pitch) + 1)
            .SelectMany(Step => Step == 0
                ? new[] { 0.0 }
                : new[] { -Step * Pitch, Step * Pitch })
            .Where(Offset => Math.Abs(Offset) <= UsableHalf + 0.5);
        foreach (double Offset in Offsets)
        {
            (double X, double Y) Port = Side is "top" or "bottom"
                ? (CentreX + Offset, CentreY)
                : (CentreX, CentreY + Offset);
            if (Existing == null || Math.Abs(Existing.X - Port.X) >= 1.0 || Math.Abs(Existing.Y - Port.Y) >= 1.0)
                yield return Port;
        }
    }

    private static IEnumerable<List<(double X, double Y)>> CandidatePaths(
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds Source,
        NodeContract? SourceNode,
        string Exit,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds Target,
        NodeContract? TargetNode,
        string Entry,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartLayoutEdgeRoute Route,
        DiagramContract Contract,
        FlowchartDirection Direction,
        bool Horizontal)
    {
        yield return [(StartX, StartY), (EndX, StartY), (EndX, EndY)];
        yield return [(StartX, StartY), (StartX, EndY), (EndX, EndY)];
        yield return DiagramGridRouteBuilder.BuildRoute(
            StartX, StartY, Source, SourceNode?.Shape, Exit,
            EndX, EndY, Target, TargetNode?.Shape, Entry,
            Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal);
    }

    private static bool LeavesBy(IReadOnlyList<(double X, double Y)> Path, string Side)
    {
        if (Path.Count < 2) return false;
        double Dx = Path[1].X - Path[0].X;
        double Dy = Path[1].Y - Path[0].Y;
        return Side switch
        {
            "left" => Dx < -0.5 && Math.Abs(Dy) < 0.5,
            "right" => Dx > 0.5 && Math.Abs(Dy) < 0.5,
            "top" => Dy > 0.5 && Math.Abs(Dx) < 0.5,
            _ => Dy < -0.5 && Math.Abs(Dx) < 0.5
        };
    }

    private static bool EntersBy(IReadOnlyList<(double X, double Y)> Path, string Side)
    {
        if (Path.Count < 2) return false;
        double Dx = Path[^1].X - Path[^2].X;
        double Dy = Path[^1].Y - Path[^2].Y;
        return Side switch
        {
            "left" => Dx > 0.5 && Math.Abs(Dy) < 0.5,
            "right" => Dx < -0.5 && Math.Abs(Dy) < 0.5,
            "top" => Dy < -0.5 && Math.Abs(Dx) < 0.5,
            _ => Dy > 0.5 && Math.Abs(Dx) < 0.5
        };
    }

    private static bool EndpointTaken(
        FlowchartLayoutResult Layout,
        FlowchartLayoutEdgeRoute Route,
        string NodeId,
        double X,
        double Y) => Layout.EdgeRoutes.Any(Other => !ReferenceEquals(Other, Route) && Other.Points.Count > 0
            && (Other.SourceId == NodeId && Near(Other.Points[0], X, Y)
                || Other.TargetId == NodeId && Near(Other.Points[^1], X, Y)));

    private static bool Near(FlowchartLayoutPoint Point, double X, double Y) =>
        Math.Abs(Point.X - X) < 1.0 && Math.Abs(Point.Y - Y) < 1.0;

    private static int CountCorners(IReadOnlyList<FlowchartLayoutPoint> Points) =>
        CountCorners(Points.Select(Point => (Point.X, Point.Y)).ToList());

    private static int CountCorners(IReadOnlyList<(double X, double Y)> Points)
    {
        int Result = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            bool BeforeHorizontal = Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5;
            bool AfterHorizontal = Math.Abs(Points[Index + 1].Y - Points[Index].Y) < 0.5;
            if (BeforeHorizontal != AfterHorizontal) Result++;
        }
        return Result;
    }

    private static (double X, double Y) ResolveEndpoint(
        FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutNodeBounds Bounds,
        string Side,
        bool Source)
    {
        FlowchartLayoutPoint? Existing = Route.Points.Count == 0
            ? null : Source ? Route.Points[0] : Route.Points[^1];
        double Left = Bounds.CenterX - Bounds.Width / 2.0;
        double Right = Bounds.CenterX + Bounds.Width / 2.0;
        double Bottom = Bounds.CenterY - Bounds.Height / 2.0;
        double Top = Bounds.CenterY + Bounds.Height / 2.0;
        if (Existing != null)
        {
            bool OnSide = Side switch
            {
                "left" => Math.Abs(Existing.X - Left) < 1.0,
                "right" => Math.Abs(Existing.X - Right) < 1.0,
                "top" => Math.Abs(Existing.Y - Top) < 1.0,
                "bottom" => Math.Abs(Existing.Y - Bottom) < 1.0,
                _ => false
            };
            if (OnSide) return (Existing.X, Existing.Y);
        }

        return Side switch
        {
            "left" => (Left, Bounds.CenterY),
            "right" => (Right, Bounds.CenterY),
            "top" => (Bounds.CenterX, Top),
            "bottom" => (Bounds.CenterX, Bottom),
            _ => (Bounds.CenterX, Bounds.CenterY)
        };
    }
}
