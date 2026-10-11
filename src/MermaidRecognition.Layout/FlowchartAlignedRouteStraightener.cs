using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Collapses clear aligned routes to the single segment required by rule 4.2.</summary>
internal static class FlowchartAlignedRouteStraightener
{
    internal static void Apply(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count > 2))
        {
            FlowchartLayoutPoint Start = Route.Points[0], End = Route.Points[^1];
            bool Vertical = Math.Abs(Start.X - End.X) < 0.5;
            bool Horizontal = Math.Abs(Start.Y - End.Y) < 0.5;
            if (!Vertical && !Horizontal || HitsBox(Route, Start, End, Bounds)) continue;
            var Candidate = new List<(double X, double Y)> { (Start.X, Start.Y), (End.X, End.Y) };
            int BeforeCrossings = 0, AfterCrossings = 0;
            double BeforeOverlap = 0.0, AfterOverlap = 0.0;
            foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
            {
                var Before = FlowchartBranchRouteOrder.Intersections(
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                    Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                var After = FlowchartBranchRouteOrder.Intersections(Candidate,
                    Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                BeforeCrossings += Before.Crossings; AfterCrossings += After.Crossings;
                BeforeOverlap += Before.Overlap; AfterOverlap += After.Overlap;
            }
            if (AfterCrossings > BeforeCrossings || AfterOverlap > BeforeOverlap + 0.01) continue;
            Route.Points.Clear();
            Route.Points.Add(new() { X = Start.X, Y = Start.Y });
            Route.Points.Add(new() { X = End.X, Y = End.Y });
        }
    }

    private static bool HitsBox(FlowchartLayoutEdgeRoute Route, FlowchartLayoutPoint A, FlowchartLayoutPoint B,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId) continue;
            double Left = Box.CenterX - Box.Width / 2.0, Right = Box.CenterX + Box.Width / 2.0;
            double Bottom = Box.CenterY - Box.Height / 2.0, Top = Box.CenterY + Box.Height / 2.0;
            if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top)) return true;
            if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right)) return true;
        }
        return false;
    }
}
