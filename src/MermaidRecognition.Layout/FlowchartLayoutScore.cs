using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Measures a routed layout using rules 2.13 and 2.14.</summary>
internal static class FlowchartLayoutScore
{
    internal sealed record Result(double Width, double Height, int Overlaps, int Crossings,
        double EmptyShare, double? Score, IReadOnlyList<FlowchartLayoutQualityFailure> Violations);

    internal static Result Measure(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        (double Width, double Height) = Bounds(Layout);
        var Violations = new List<FlowchartLayoutQualityFailure>();
        DiagramGridQuality.Collect(Contract, Layout, Violations);
        int Overlaps = Violations.Count(Failure => Failure.Kind == "NodeOverlap");
        int Crossings = CountCrossings(Layout);
        double EmptyShare = EmptyAreaShare(Layout, Width, Height);
        bool Rejected = Overlaps > 0 || Violations.Any(Failure => Failure.Kind != "NodeOverlap"
            && Failure.Kind != "ArrangementKept");
        double? Score = Rejected ? null : Calculate(Width, Height, Crossings, EmptyShare);
        return new(Width, Height, Overlaps, Crossings, EmptyShare, Score, Violations);
    }

    internal static double Calculate(double Width, double Height, int Crossings, double EmptyShare)
    {
        const double TargetRatio = 4.0 / 3.0;
        double Shape = Math.Abs((Width / Math.Max(1.0, Height)) - TargetRatio) / TargetRatio * 100.0 * 0.4;
        double EmptyPercent = EmptyShare * 100.0;
        double Empty = EmptyPercent <= 30.0 ? EmptyPercent * 2.0 : 60.0 + (EmptyPercent - 30.0) * 8.0;
        return Shape + Empty + Crossings * 20.0;
    }

    private static (double Width, double Height) Bounds(FlowchartLayoutResult Layout)
    {
        var X = new List<double>();
        var Y = new List<double>();
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
        {
            X.Add(Box.CenterX - Box.Width / 2.0); X.Add(Box.CenterX + Box.Width / 2.0);
            Y.Add(Box.CenterY - Box.Height / 2.0); Y.Add(Box.CenterY + Box.Height / 2.0);
        }
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        foreach (FlowchartLayoutPoint Point in Route.Points)
        {
            X.Add(Point.X); Y.Add(Point.Y);
        }
        return X.Count == 0 ? (0.0, 0.0) : (X.Max() - X.Min(), Y.Max() - Y.Min());
    }

    private static int CountCrossings(FlowchartLayoutResult Layout)
    {
        int Result = 0;
        for (int Left = 0; Left < Layout.EdgeRoutes.Count; Left++)
        for (int Right = Left + 1; Right < Layout.EdgeRoutes.Count; Right++)
        {
            FlowchartLayoutEdgeRoute A = Layout.EdgeRoutes[Left];
            FlowchartLayoutEdgeRoute B = Layout.EdgeRoutes[Right];
            if (A.SourceId == B.SourceId || A.SourceId == B.TargetId
                || A.TargetId == B.SourceId || A.TargetId == B.TargetId) continue;
            Result += FlowchartBranchRouteOrder.Intersections(
                A.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                B.Points.Select(Point => (Point.X, Point.Y)).ToList()).Crossings;
        }
        return Result;
    }

    private static double EmptyAreaShare(FlowchartLayoutResult Layout, double Width, double Height)
    {
        if (Width <= 0.0 || Height <= 0.0) return 0.0;
        const double Cell = DiagramGridMetrics.GRID_UNIT_PX;
        double Left = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .Min(Box => Box.CenterX - Box.Width / 2.0);
        double Bottom = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .Min(Box => Box.CenterY - Box.Height / 2.0);
        int Columns = Math.Max(1, (int)Math.Ceiling(Width / Cell));
        int Rows = Math.Max(1, (int)Math.Ceiling(Height / Cell));
        var Used = new HashSet<(int Column, int Row)>();
        void Mark(double MinX, double MinY, double MaxX, double MaxY)
        {
            int FirstColumn = (int)Math.Floor((MinX - Left) / Cell);
            int LastColumn = (int)Math.Floor((MaxX - Left) / Cell);
            int FirstRow = (int)Math.Floor((MinY - Bottom) / Cell);
            int LastRow = (int)Math.Floor((MaxY - Bottom) / Cell);
            for (int Column = FirstColumn; Column <= LastColumn; Column++)
            for (int Row = FirstRow; Row <= LastRow; Row++)
                if (Column >= 0 && Column < Columns && Row >= 0 && Row < Rows)
                    Used.Add((Column, Row));
        }
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
            Mark(Box.CenterX - Box.Width / 2.0, Box.CenterY - Box.Height / 2.0,
                Box.CenterX + Box.Width / 2.0, Box.CenterY + Box.Height / 2.0);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            FlowchartLayoutPoint A = Route.Points[Index - 1], B = Route.Points[Index];
            Mark(Math.Min(A.X, B.X) - Cell / 2.0, Math.Min(A.Y, B.Y) - Cell / 2.0,
                Math.Max(A.X, B.X) + Cell / 2.0, Math.Max(A.Y, B.Y) + Cell / 2.0);
        }
        return 1.0 - Used.Count / (double)(Columns * Rows);
    }
}
