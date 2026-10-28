using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Applies rule 3.8 after the general route passes: an offset forward branch
/// from a two- or three-way diamond leaves the facing vertex and uses one corner.
/// </summary>
internal static class FlowchartDecisionBranchRouteRepair
{
    private const double Epsilon = 0.5;
    private static readonly double MinimumRun = DiagramGridRouteBuilder.CORNER_RADIUS_PX
        + DiagramGridMetrics.GRID_UNIT_PX;

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract)
    {
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        IReadOnlySet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.ToList())
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)
                || !Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode)
                || !FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape)
                || IsHinted(Contract, Route))
            {
                continue;
            }

            List<EdgeContract> Outgoing = [.. Contract.Edges.Where(Visible).Where(Edge => Edge.SourceId == Route.SourceId && !BackEdges.Contains(Edge))];
            EdgeContract? Edge = Outgoing.FirstOrDefault(Edge => Edge.TargetId == Route.TargetId);
            if (Edge == null || Outgoing.Count is not (2 or 3))
            {
                continue;
            }

            (double SourceCross, double SourceFlow, double SourceHalfCross, _) = Axes(Source, Direction);
            (double TargetCross, double TargetFlow, double TargetHalfCross, double TargetHalfFlow) = Axes(Target, Direction);
            if (TargetFlow <= SourceFlow + Epsilon || Math.Abs(TargetCross - SourceCross) <= Epsilon)
            {
                continue;
            }

            bool TargetIsRight = TargetCross > SourceCross;
            (double X, double Y) Start = Point(SourceCross + (TargetIsRight ? SourceHalfCross : -SourceHalfCross),
                SourceFlow, Direction);
            (double X, double Y) TopCentre = Point(TargetCross, TargetFlow - TargetHalfFlow, Direction);
            bool TopFree = !Layout.EdgeRoutes.Any(Other => Other != Route && Other.TargetId == Route.TargetId
                && Other.Points.Count > 0 && Near(Other.Points[^1], TopCentre));
            (double X, double Y) End = TopFree ? TopCentre : Point(
                TargetCross + (TargetIsRight ? -TargetHalfCross : TargetHalfCross), TargetFlow, Direction);
            (double X, double Y) Corner = Point(TopFree ? TargetCross : SourceCross
                + (TargetIsRight ? SourceHalfCross : -SourceHalfCross), TopFree ? SourceFlow : TargetFlow, Direction);
            List<FlowchartLayoutPoint> Candidate = Normalize([New(Start), New(Corner), New(End)]);
            if (Candidate.Count != 3 || !Legal(Candidate, Route, Layout, Contract, Bounds))
            {
                continue;
            }

            // Rules 3.8 and 3.9 are routing requirements, so a legal one-corner
            // branch replaces the rejected two-corner route even when its line
            // bounds make the diagram's shape component slightly worse.
            Route.Points.Clear(); Route.Points.AddRange(Candidate.Select(Copy));
            Route.ExitSide = CrossSide(!TargetIsRight, Direction);
            Route.EntrySide = TopFree ? UpstreamSide(Direction) : CrossSide(TargetIsRight, Direction);
        }
    }

    private static bool Legal(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        if (Distance(Points[0], Points[1]) + Epsilon < MinimumRun
            || Distance(Points[1], Points[2]) + Epsilon < MinimumRun)
        {
            return false;
        }

        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId
                || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.SourceId)
                || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.TargetId))
            {
                continue;
            }

            for (int Index = 1; Index < Points.Count; Index++)
            {
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Points[Index - 1].X, Points[Index - 1].Y, Points[Index].X, Points[Index].Y, Box))
                {
                    return false;
                }
            }
        }
        List<(double X, double Y)> Candidate = [.. Points.Select(Point => (Point.X, Point.Y))];
        List<(double X, double Y)> Original = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
        foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
        {
            List<(double X, double Y)> OtherPoints = [.. Other.Points.Select(Point => (Point.X, Point.Y))];
            (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(Original, OtherPoints);
            (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Candidate, OtherPoints);
            if (After.Crossings > Before.Crossings || After.Overlap > Before.Overlap + Epsilon)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsHinted(DiagramContract Contract, FlowchartLayoutEdgeRoute Route) => Contract.Edges.Any(Edge =>
        Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId && Edge.RoutingHint?.IsActive == true);
    private static bool Visible(EdgeContract Edge) =>
        !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);
    private static (double Cross, double Flow, double HalfCross, double HalfFlow) Axes(
        FlowchartLayoutNodeBounds Box, FlowchartDirection Direction)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        (double Cross, double Flow) = Axes(Box.CenterX, Box.CenterY, Direction);
        return (Cross, Flow, Horizontal ? Box.Height / 2 : Box.Width / 2,
            Horizontal ? Box.Width / 2 : Box.Height / 2);
    }
    private static (double Cross, double Flow) Axes(double X, double Y, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => (X, -Y), FlowchartDirection.BottomToTop => (X, Y),
        FlowchartDirection.LeftToRight => (Y, X), FlowchartDirection.RightToLeft => (Y, -X), _ => (X, -Y)
    };
    private static (double X, double Y) Point(double Cross, double Flow, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => (Cross, -Flow), FlowchartDirection.BottomToTop => (Cross, Flow),
        FlowchartDirection.LeftToRight => (Flow, Cross), FlowchartDirection.RightToLeft => (-Flow, Cross), _ => (Cross, -Flow)
    };
    private static string UpstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "top", FlowchartDirection.BottomToTop => "bottom",
        FlowchartDirection.LeftToRight => "left", _ => "right"
    };
    private static string CrossSide(bool Negative, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom or FlowchartDirection.BottomToTop => Negative ? "left" : "right",
        _ => Negative ? "bottom" : "top"
    };
    private static List<FlowchartLayoutPoint> Normalize(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        List<FlowchartLayoutPoint> Result = [];
        foreach (FlowchartLayoutPoint Point in Points)
        {
            if (Result.Count > 0 && Near(Result[^1], (Point.X, Point.Y)))
            {
                continue;
            }

            Result.Add(Copy(Point));
            while (Result.Count >= 3 && Collinear(Result[^3], Result[^2], Result[^1]))
            {
                Result.RemoveAt(Result.Count - 2);
            }
        }
        return Result;
    }
    private static bool Collinear(FlowchartLayoutPoint A, FlowchartLayoutPoint B, FlowchartLayoutPoint C) =>
        (Math.Abs(A.X - B.X) <= Epsilon && Math.Abs(B.X - C.X) <= Epsilon)
        || (Math.Abs(A.Y - B.Y) <= Epsilon && Math.Abs(B.Y - C.Y) <= Epsilon);
    private static FlowchartLayoutPoint New((double X, double Y) Point) => new() { X = Math.Round(Point.X), Y = Math.Round(Point.Y) };
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Point) => new() { X = Point.X, Y = Point.Y };
    private static double Distance(FlowchartLayoutPoint A, FlowchartLayoutPoint B) => Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
    private static bool Near(FlowchartLayoutPoint Point, (double X, double Y) Other) =>
        Math.Abs(Point.X - Other.X) <= Epsilon && Math.Abs(Point.Y - Other.Y) <= Epsilon;
}
