using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Chooses a clear box side with enough measured straight-run room for a labelled self-loop.</summary>
internal static class FlowchartSelfLoopLabelRouteRepair
{
    private sealed record Candidate(List<FlowchartLayoutPoint> Points, string Side, int Crossings, double Length);

    internal static string? ApplyRetained(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, IReadOnlySet<FlowchartLayoutEdgeRoute> Affected)
    {
        var Loops = Affected.Where(Route => Route.SourceId == Route.TargetId && Route.Points.Count >= 2
            && !string.IsNullOrWhiteSpace(DiagramGridLayout.FindContractEdge(Contract, Route)?.Label)).ToArray();
        if (Loops.Length == 0) return null;
        var Options = FlowchartRenderOptions.FromContract(Contract);
        var Prepared = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds);
        foreach (var Route in Loops)
        {
            var Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            if (string.IsNullOrWhiteSpace(Edge?.Label) || !Bounds.TryGetValue(Route.SourceId, out var Box)) continue;
            var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label, Options, Options.EdgeLabelFontSizePx);
            double H = RoundGap(FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(Size, Options.EdgeLabelFontSizePx, true)
                + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            double V = RoundGap(FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(Size, Options.EdgeLabelFontSizePx, false)
                + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            bool Fits(IReadOnlyList<FlowchartLayoutPoint> Points) => Points.Zip(Points.Skip(1)).Any(Pair =>
            {
                bool Room = Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.01
                    ? Math.Abs(Pair.First.X - Pair.Second.X) >= H - 0.01 : Math.Abs(Pair.First.Y - Pair.Second.Y) >= V - 0.01;
                double X = (Pair.First.X + Pair.Second.X) / 2, Y = (Pair.First.Y + Pair.Second.Y) / 2;
                return Room && (Math.Abs(X - Box.CenterX) >= (Box.Width + Size.Width) / 2 + 6
                    || Math.Abs(Y - Box.CenterY) >= (Box.Height + Size.Height) / 2 + 6);
            });
            if (Fits(Route.Points)) continue;
            var Candidates = FixedPorts(Box, Route, H, V)
                .Where(Item => Fits(Item.Points) && FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Tuples(Item.Points), Prepared)
                    && NoWorse(Item.Points, Route, Layout, out _, out _)
                    && (Edge.RoutingHint is not { IsActive: true, Travel: not null } Hint
                        || DiagramGridRouteBuilder.HasTravelDirection(Tuples(Item.Points), Hint.Travel.Value)))
                .Select(Item => { NoWorse(Item.Points, Route, Layout, out int Crossings, out _); return Item with { Crossings = Crossings }; })
                .OrderBy(Item => Item.Crossings).ThenBy(Item => Item.Length).ThenBy(Item => Item.Side, StringComparer.Ordinal);
            var Best = Candidates.FirstOrDefault();
            if (Best == null) return "The self-relationship label needs more space than its fixed ports and neighboring routes permit.";
            Route.Points.Clear(); Route.Points.AddRange(Best.Points.Select(Copy));
        }
        return null;
    }

    private static IEnumerable<Candidate> FixedPorts(FlowchartLayoutNodeBounds Box, FlowchartLayoutEdgeRoute Route, double H, double V)
    {
        int Side(string Side) => Side switch { "top" => 0, "right" => 1, "bottom" => 2, _ => 3 };
        double Left = Box.CenterX - Box.Width / 2 - H, Right = Box.CenterX + Box.Width / 2 + H;
        double Top = Box.CenterY + Box.Height / 2 + V, Bottom = Box.CenterY - Box.Height / 2 - V;
        var Start = Route.Points[0]; var End = Route.Points[^1];
        (double X, double Y) Outside(FlowchartLayoutPoint Point, int Edge) => Edge switch
            { 0 => (Point.X, Top), 1 => (Right, Point.Y), 2 => (Point.X, Bottom), _ => (Left, Point.Y) };
        int From = Side(Route.ExitSide), To = Side(Route.EntrySide);
        foreach (int Step in new[] { 1, -1 })
        {
            List<(double X, double Y)> Path = [(Start.X, Start.Y), Outside(Start, From)];
            for (int Index = From; Index != To; Index = (Index + Step + 4) % 4)
            {
                int Corner = Step > 0 ? Index : (Index + 3) % 4;
                Path.Add(Corner switch { 0 => (Right, Top), 1 => (Right, Bottom), 2 => (Left, Bottom), _ => (Left, Top) });
            }
            Path.Add(Outside(End, To)); Path.Add((End.X, End.Y));
            yield return New(Route.ExitSide, DiagramGridRouteBuilder.SimplifyCollinear(Path));
        }
    }

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .GroupBy(Box => Box.NodeId, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.SourceId == Route.TargetId))
        {
            EdgeContract? Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId)
                ?? Contract.Edges.FirstOrDefault(Edge => Edge.SourceId == Route.SourceId
                    && Edge.TargetId == Route.TargetId);
            if (string.IsNullOrWhiteSpace(Edge?.Label) || Edge.RoutingHint?.IsActive == true
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Box))
            {
                continue;
            }

            FlowchartLabelDimensions Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                Edge.Label, Options, Options.EdgeLabelFontSizePx);
            Candidate? Best = Enumerate(Box, Size, Options)
                .Where(Item => Clear(Item.Points, Route.SourceId, Contract, Bounds.Values)
                    && NoWorse(Item.Points, Route, Layout, out _, out _))
                .Select(Item =>
                {
                    NoWorse(Item.Points, Route, Layout, out int Crossings, out double Overlap);
                    return Item with { Crossings = Crossings };
                })
                .OrderBy(Item => Item.Crossings).ThenBy(Item => Item.Length).ThenBy(Item => Item.Side, StringComparer.Ordinal)
                .FirstOrDefault();
            if (Best == null)
            {
                continue;
            }

            Route.Points.Clear(); Route.Points.AddRange(Best.Points.Select(Copy));
            Route.ExitSide = Route.EntrySide = Best.Side;
        }
    }

    private static IEnumerable<Candidate> Enumerate(FlowchartLayoutNodeBounds Box,
        FlowchartLabelDimensions Size, FlowchartRenderOptions Options)
    {
        double OffsetX = Math.Min(DiagramGridMetrics.GRID_UNIT_PX / 2.0, Box.Width / 4.0);
        double OffsetY = Math.Min(DiagramGridMetrics.GRID_UNIT_PX / 2.0, Box.Height / 4.0);
        double Horizontal = RoundGap(FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
            Size, Options.EdgeLabelFontSizePx, true) + (2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX));
        double Vertical = RoundGap(FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
            Size, Options.EdgeLabelFontSizePx, false) + (2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX));
        double Left = Box.CenterX - (Box.Width / 2.0), Right = Box.CenterX + (Box.Width / 2.0);
        double Bottom = Box.CenterY - (Box.Height / 2.0), Top = Box.CenterY + (Box.Height / 2.0);
        yield return New("right", [(Right, Box.CenterY + OffsetY), (Right + Horizontal, Box.CenterY + OffsetY),
            (Right + Horizontal, Box.CenterY - OffsetY), (Right, Box.CenterY - OffsetY)]);
        yield return New("left", [(Left, Box.CenterY - OffsetY), (Left - Horizontal, Box.CenterY - OffsetY),
            (Left - Horizontal, Box.CenterY + OffsetY), (Left, Box.CenterY + OffsetY)]);
        yield return New("top", [(Box.CenterX - OffsetX, Top), (Box.CenterX - OffsetX, Top + Vertical),
            (Box.CenterX + OffsetX, Top + Vertical), (Box.CenterX + OffsetX, Top)]);
        yield return New("bottom", [(Box.CenterX + OffsetX, Bottom), (Box.CenterX + OffsetX, Bottom - Vertical),
            (Box.CenterX - OffsetX, Bottom - Vertical), (Box.CenterX - OffsetX, Bottom)]);
    }

    private static Candidate New(string Side, IReadOnlyList<(double X, double Y)> Points) => new(
        [.. Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y })], Side, 0,
        Points.Zip(Points.Skip(1)).Sum(Pair => Math.Abs(Pair.First.X - Pair.Second.X)
            + Math.Abs(Pair.First.Y - Pair.Second.Y)));

    private static bool Clear(IReadOnlyList<FlowchartLayoutPoint> Points, string NodeId, DiagramContract Contract,
        IEnumerable<FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds)
        {
            if (Box.NodeId == NodeId || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, NodeId))
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
        return true;
    }

    private static bool NoWorse(IReadOnlyList<FlowchartLayoutPoint> Candidate,
        FlowchartLayoutEdgeRoute Route, FlowchartLayoutResult Layout, out int Crossings, out double Overlap)
    {
        Crossings = 0; Overlap = 0.0;
        int BeforeCrossings = 0; double BeforeOverlap = 0.0;
        foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
        {
            (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(Tuples(Route.Points), Tuples(Other.Points));
            (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Tuples(Candidate), Tuples(Other.Points));
            BeforeCrossings += Before.Crossings; BeforeOverlap += Before.Overlap;
            Crossings += After.Crossings; Overlap += After.Overlap;
        }
        return Crossings <= BeforeCrossings && Overlap <= BeforeOverlap + 0.01;
    }

    private static List<(double X, double Y)> Tuples(IEnumerable<FlowchartLayoutPoint> Points) =>
        [.. Points.Select(Point => (Point.X, Point.Y))];
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Point) => new() { X = Point.X, Y = Point.Y };
    private static double RoundGap(double Value) => Math.Ceiling(Value / DiagramGridMetrics.GRID_UNIT_PX)
        * DiagramGridMetrics.GRID_UNIT_PX;
}
