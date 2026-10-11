using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Uses the two-corner joining Z required between consecutive wrapped chain pieces.</summary>
internal static class FlowchartWrappedChainRoutes
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        bool Horizontal = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        var Visible = Contract.Edges.Where(Edge => Bounds.ContainsKey(Edge.SourceId)
            && Bounds.ContainsKey(Edge.TargetId)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count > 4))
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)) continue;
            double MainSource = Horizontal ? Source.CenterX : Source.CenterY;
            double MainTarget = Horizontal ? Target.CenterX : Target.CenterY;
            double CrossSource = Horizontal ? Source.CenterY : Source.CenterX;
            double CrossTarget = Horizontal ? Target.CenterY : Target.CenterX;
            double MainSpan = Horizontal ? Math.Max(Source.Width, Target.Width) : Math.Max(Source.Height, Target.Height);
            double CrossSpan = Horizontal ? Math.Max(Source.Height, Target.Height) : Math.Max(Source.Width, Target.Width);
            if (Math.Abs(MainTarget - MainSource) < 2 * MainSpan
                || Math.Abs(CrossTarget - CrossSource) > CrossSpan + 2 * DiagramGridMetrics.NODE_GAP_PX) continue;
            double Sign = Math.Sign(CrossTarget - CrossSource);
            if (Sign == 0.0) continue;
            double SourceSide = CrossSource + Sign * (Horizontal ? Source.Height : Source.Width) / 2.0;
            double TargetSide = CrossTarget - Sign * (Horizontal ? Target.Height : Target.Width) / 2.0;
            double Track = (SourceSide + TargetSide) / 2.0;
            var Candidate = new List<FlowchartLayoutPoint>
            {
                Point(MainSource, SourceSide, Horizontal),
                Point(MainSource, Track, Horizontal),
                Point(MainTarget, Track, Horizontal),
                Point(MainTarget, TargetSide, Horizontal)
            };
            if (HitsOtherNode(Candidate, Source.NodeId, Target.NodeId, Bounds)) continue;
            if (!NoWorse(Candidate, Route, Layout)) continue;
            Route.Points.Clear();
            Route.Points.AddRange(Candidate);
            Route.ExitSide = CrossSide(Horizontal, Sign);
            Route.EntrySide = CrossSide(Horizontal, -Sign);
        }
    }

    private static bool NoWorse(IReadOnlyList<FlowchartLayoutPoint> Candidate,
        FlowchartLayoutEdgeRoute Route, FlowchartLayoutResult Layout)
    {
        int BeforeCrossings = 0, AfterCrossings = 0;
        double BeforeOverlap = 0.0, AfterOverlap = 0.0;
        foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
        {
            var Before = FlowchartBranchRouteOrder.Intersections(Tuples(Route.Points), Tuples(Other.Points));
            var After = FlowchartBranchRouteOrder.Intersections(Tuples(Candidate), Tuples(Other.Points));
            BeforeCrossings += Before.Crossings; AfterCrossings += After.Crossings;
            BeforeOverlap += Before.Overlap; AfterOverlap += After.Overlap;
        }
        return AfterCrossings <= BeforeCrossings && AfterOverlap <= BeforeOverlap + 0.01;
    }

    private static List<(double X, double Y)> Tuples(IEnumerable<FlowchartLayoutPoint> Points) =>
        Points.Select(Point => (Point.X, Point.Y)).ToList();

    private static bool HitsOtherNode(IReadOnlyList<FlowchartLayoutPoint> Points, string SourceId, string TargetId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == SourceId || Box.NodeId == TargetId) continue;
            double Left = Box.CenterX - Box.Width / 2.0, Right = Box.CenterX + Box.Width / 2.0;
            double Bottom = Box.CenterY - Box.Height / 2.0, Top = Box.CenterY + Box.Height / 2.0;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index];
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top)) return true;
                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right)) return true;
            }
        }
        return false;
    }

    private static FlowchartLayoutPoint Point(double Main, double Cross, bool Horizontal) => Horizontal
        ? new FlowchartLayoutPoint { X = Main, Y = Cross }
        : new FlowchartLayoutPoint { X = Cross, Y = Main };

    private static string CrossSide(bool Horizontal, double Sign) => Horizontal
        ? Sign > 0 ? "top" : "bottom"
        : Sign > 0 ? "right" : "left";
}
