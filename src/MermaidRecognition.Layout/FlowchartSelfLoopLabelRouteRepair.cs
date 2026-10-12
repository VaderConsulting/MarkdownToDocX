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

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .GroupBy(Box => Box.NodeId, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.SourceId == Route.TargetId))
        {
            EdgeContract? Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId)
                ?? Contract.Edges.FirstOrDefault(Edge => Edge.SourceId == Route.SourceId
                    && Edge.TargetId == Route.TargetId);
            if (string.IsNullOrWhiteSpace(Edge?.Label) || Edge.RoutingHint?.IsActive == true
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Box)) continue;
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
            if (Best == null) continue;
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
            Size, Options.EdgeLabelFontSizePx, true) + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        double Vertical = RoundGap(FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
            Size, Options.EdgeLabelFontSizePx, false) + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        double Left = Box.CenterX - Box.Width / 2.0, Right = Box.CenterX + Box.Width / 2.0;
        double Bottom = Box.CenterY - Box.Height / 2.0, Top = Box.CenterY + Box.Height / 2.0;
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
        Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToList(), Side, 0,
        Points.Zip(Points.Skip(1)).Sum(Pair => Math.Abs(Pair.First.X - Pair.Second.X)
            + Math.Abs(Pair.First.Y - Pair.Second.Y)));

    private static bool Clear(IReadOnlyList<FlowchartLayoutPoint> Points, string NodeId, DiagramContract Contract,
        IEnumerable<FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds)
        {
            if (Box.NodeId == NodeId || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, NodeId)) continue;
            for (int Index = 1; Index < Points.Count; Index++)
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Points[Index - 1].X, Points[Index - 1].Y, Points[Index].X, Points[Index].Y, Box)) return false;
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
            var Before = FlowchartBranchRouteOrder.Intersections(Tuples(Route.Points), Tuples(Other.Points));
            var After = FlowchartBranchRouteOrder.Intersections(Tuples(Candidate), Tuples(Other.Points));
            BeforeCrossings += Before.Crossings; BeforeOverlap += Before.Overlap;
            Crossings += After.Crossings; Overlap += After.Overlap;
        }
        return Crossings <= BeforeCrossings && Overlap <= BeforeOverlap + 0.01;
    }

    private static List<(double X, double Y)> Tuples(IEnumerable<FlowchartLayoutPoint> Points) =>
        Points.Select(Point => (Point.X, Point.Y)).ToList();
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Point) => new() { X = Point.X, Y = Point.Y };
    private static double RoundGap(double Value) => Math.Ceiling(Value / DiagramGridMetrics.GRID_UNIT_PX)
        * DiagramGridMetrics.GRID_UNIT_PX;
}
