using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Collapses clear aligned routes to the single segment required by rule 4.2.</summary>
internal static class FlowchartAlignedRouteStraightener
{
    internal static void ApplyCenteredFacing(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                continue;
            }

            bool Vertical = Math.Abs(Source.CenterX - Target.CenterX) < 0.5;
            bool Horizontal = Math.Abs(Source.CenterY - Target.CenterY) < 0.5;
            if (!Vertical && !Horizontal)
            {
                continue;
            }

            string Exit, Entry;
            FlowchartLayoutPoint Start, End;
            if (Vertical)
            {
                bool Up = Target.CenterY > Source.CenterY;
                Exit = Up ? "top" : "bottom";
                Entry = Up ? "bottom" : "top";
                Start = new() { X = Source.CenterX, Y = Source.CenterY + ((Up ? Source.Height : -Source.Height) / 2.0) };
                End = new() { X = Target.CenterX, Y = Target.CenterY + ((Up ? -Target.Height : Target.Height) / 2.0) };
            }
            else
            {
                bool Right = Target.CenterX > Source.CenterX;
                Exit = Right ? "right" : "left";
                Entry = Right ? "left" : "right";
                Start = new() { X = Source.CenterX + ((Right ? Source.Width : -Source.Width) / 2.0), Y = Source.CenterY };
                End = new() { X = Target.CenterX + ((Right ? -Target.Width : Target.Width) / 2.0), Y = Target.CenterY };
            }
            if (!TryGeometryPort(Source, Exit, 0.5, ref Start)
                || !TryGeometryPort(Target, Entry, 0.5, ref End))
            {
                continue;
            }

            if (Math.Abs(Start.X - End.X) > 0.5 && Math.Abs(Start.Y - End.Y) > 0.5)
            {
                continue;
            }

            if (HitsBox(Route, Start, End, Bounds) || PortUsed(Layout, Route, Start, AtSource: true)
                || PortUsed(Layout, Route, End, AtSource: false))
            {
                continue;
            }

            if (WouldWorsenInteractions(Layout, Route, [Start, End]))
            {
                continue;
            }

            Route.ExitSide = Exit;
            Route.EntrySide = Entry;
            Route.Points.Clear();
            Route.Points.Add(Start);
            Route.Points.Add(End);
        }
    }

    internal static void RepairSameSideDirectRoutes(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count == 2))
        {
            if (Route.ExitSide != Route.EntrySide || Route.ExitSide is not ("left" or "right" or "top" or "bottom")
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                continue;
            }

            string Side = Route.ExitSide;
            double Gap = Math.Max(DiagramGridMetrics.NODE_GAP_PX,
                2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            double Track = Side switch
            {
                "left" => Math.Min(Source.CenterX - (Source.Width / 2.0), Target.CenterX - (Target.Width / 2.0)) - Gap,
                "right" => Math.Max(Source.CenterX + (Source.Width / 2.0), Target.CenterX + (Target.Width / 2.0)) + Gap,
                "bottom" => Math.Min(Source.CenterY - (Source.Height / 2.0), Target.CenterY - (Target.Height / 2.0)) - Gap,
                _ => Math.Max(Source.CenterY + (Source.Height / 2.0), Target.CenterY + (Target.Height / 2.0)) + Gap
            };
            FlowchartLayoutPoint Start = Route.Points[0], End = Route.Points[^1];
            List<FlowchartLayoutPoint> Candidate = Side is "left" or "right"
                ? [Start, new() { X = Track, Y = Start.Y }, new() { X = Track, Y = End.Y }, End]
                : [Start, new() { X = Start.X, Y = Track }, new() { X = End.X, Y = Track }, End];
            if (Candidate.Zip(Candidate.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Bounds, Route.SourceId, Route.TargetId)))
            {
                continue;
            }

            Route.Points.Clear();
            Route.Points.AddRange(Candidate);
        }
    }

    internal static void ApplyOneCornerCandidates(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        string[] Sides = ["top", "right", "bottom", "left"];
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => CornerCount(Route.Points) > 1))
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                continue;
            }

            foreach (string Exit in Sides)
            {
                foreach (string Entry in Sides)
                {
                    foreach (double ExitFraction in new[] { 0.5, 1.0 / 3.0, 2.0 / 3.0 })
                    {
                        foreach (double EntryFraction in new[] { 0.5, 1.0 / 3.0, 2.0 / 3.0 })
            {
                bool ExitHorizontal = Exit is "left" or "right";
                if (ExitHorizontal == (Entry is "left" or "right"))
                            {
                                continue;
                            }

                            FlowchartLayoutPoint Start = Port(Source, Exit, ExitFraction), End = Port(Target, Entry, EntryFraction);
                if (!TryGeometryPort(Source, Exit, ExitFraction, ref Start)
                    || !TryGeometryPort(Target, Entry, EntryFraction, ref End))
                            {
                                continue;
                            }

                            FlowchartLayoutPoint Bend = ExitHorizontal
                    ? new() { X = End.X, Y = Start.Y }
                    : new() { X = Start.X, Y = End.Y };
                List<FlowchartLayoutPoint> Candidate = [Start, Bend, End];
                if (!Leaves(Start, Bend, Exit) || !Enters(Bend, End, Entry)
                    || Candidate.Zip(Candidate.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                        Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Bounds, Route.SourceId, Route.TargetId))
                    || PortUsed(Layout, Route, Start, AtSource: true)
                    || PortUsed(Layout, Route, End, AtSource: false)
                    || WouldWorsenInteractions(Layout, Route, Candidate))
                            {
                                continue;
                            }

                            Route.ExitSide = Exit;
                Route.EntrySide = Entry;
                Route.Points.Clear();
                Route.Points.AddRange(Candidate);
                goto NextRoute;
            }
                    }
                }
            }

        NextRoute:;
        }
    }

    internal static void Apply(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count > 2))
        {
            FlowchartLayoutPoint Start = Route.Points[0], End = Route.Points[^1];
            bool Vertical = Math.Abs(Start.X - End.X) < 0.5;
            bool Horizontal = Math.Abs(Start.Y - End.Y) < 0.5;
            if ((!Vertical && !Horizontal) || HitsBox(Route, Start, End, Bounds))
            {
                continue;
            }

            List<(double X, double Y)> Candidate = [(Start.X, Start.Y), (End.X, End.Y)];
            int BeforeCrossings = 0, AfterCrossings = 0;
            double BeforeOverlap = 0.0, AfterOverlap = 0.0;
            foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
            {
                (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                    Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Candidate,
                    Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                BeforeCrossings += Before.Crossings; AfterCrossings += After.Crossings;
                BeforeOverlap += Before.Overlap; AfterOverlap += After.Overlap;
            }
            if (AfterCrossings > BeforeCrossings || AfterOverlap > BeforeOverlap + 0.01)
            {
                continue;
            }

            Route.Points.Clear();
            Route.Points.Add(new() { X = Start.X, Y = Start.Y });
            Route.Points.Add(new() { X = End.X, Y = End.Y });
        }
    }

    internal static void ApplyNearlyAlignedFacing(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count > 2))
        {
            FlowchartLayoutPoint Start = Route.Points[0], End = Route.Points[^1];
            double DeltaX = Math.Abs(Start.X - End.X), DeltaY = Math.Abs(Start.Y - End.Y);
            bool Vertical = DeltaX <= DiagramGridMetrics.GRID_UNIT_PX && DeltaY > DeltaX
                && IsVerticalFacingPair(Route);
            bool Horizontal = DeltaY <= DiagramGridMetrics.GRID_UNIT_PX && DeltaX > DeltaY
                && IsHorizontalFacingPair(Route);
            if (!Vertical && !Horizontal)
            {
                continue;
            }

            double SharedX = (Start.X + End.X) / 2.0;
            double SharedY = (Start.Y + End.Y) / 2.0;
            FlowchartLayoutPoint CandidateStart = new FlowchartLayoutPoint
            {
                X = Vertical ? SharedX : Start.X,
                Y = Horizontal ? SharedY : Start.Y
            };
            FlowchartLayoutPoint CandidateEnd = new FlowchartLayoutPoint
            {
                X = Vertical ? SharedX : End.X,
                Y = Horizontal ? SharedY : End.Y
            };
            if (HitsBox(Route, CandidateStart, CandidateEnd, Bounds))
            {
                continue;
            }

            List<(double X, double Y)> Candidate =
            [
                (CandidateStart.X, CandidateStart.Y), (CandidateEnd.X, CandidateEnd.Y)
            ];
            int BeforeCrossings = 0, AfterCrossings = 0;
            double BeforeOverlap = 0.0, AfterOverlap = 0.0;
            foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
            {
                (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                    Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Candidate,
                    Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
                BeforeCrossings += Before.Crossings; AfterCrossings += After.Crossings;
                BeforeOverlap += Before.Overlap; AfterOverlap += After.Overlap;
            }
            if (AfterCrossings > BeforeCrossings || AfterOverlap > BeforeOverlap + 0.01)
            {
                continue;
            }

            Route.Points.Clear();
            Route.Points.Add(CandidateStart);
            Route.Points.Add(CandidateEnd);
        }
    }

    private static bool IsVerticalFacingPair(FlowchartLayoutEdgeRoute Route) =>
        Route.ExitSide is "top" or "bottom"
        && Route.EntrySide is "top" or "bottom"
        && !string.Equals(Route.ExitSide, Route.EntrySide, StringComparison.Ordinal);

    private static bool IsHorizontalFacingPair(FlowchartLayoutEdgeRoute Route) =>
        Route.ExitSide is "left" or "right"
        && Route.EntrySide is "left" or "right"
        && !string.Equals(Route.ExitSide, Route.EntrySide, StringComparison.Ordinal);

    private static bool HitsBox(FlowchartLayoutEdgeRoute Route, FlowchartLayoutPoint A, FlowchartLayoutPoint B,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId)
            {
                continue;
            }

            double Left = Box.CenterX - (Box.Width / 2.0), Right = Box.CenterX + (Box.Width / 2.0);
            double Bottom = Box.CenterY - (Box.Height / 2.0), Top = Box.CenterY + (Box.Height / 2.0);
            if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top))
            {
                return true;
            }

            if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right))
            {
                return true;
            }
        }
        return false;
    }

    private static bool PortUsed(FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutPoint Port, bool AtSource) => Layout.EdgeRoutes.Where(Other => Other != Route && Other.Points.Count >= 2)
        .Any(Other => Same(Other.Points[0], Port) || Same(Other.Points[^1], Port));

    private static bool Same(FlowchartLayoutPoint A, FlowchartLayoutPoint B) =>
        Math.Abs(A.X - B.X) < 0.5 && Math.Abs(A.Y - B.Y) < 0.5;

    private static bool WouldWorsenInteractions(FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Route,
        IReadOnlyList<FlowchartLayoutPoint> Candidate)
    {
        int BeforeCrossings = 0, AfterCrossings = 0;
        double BeforeOverlap = 0, AfterOverlap = 0;
        foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
        {
            (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(Route.Points.Select(P => (P.X, P.Y)).ToList(),
                Other.Points.Select(P => (P.X, P.Y)).ToList());
            (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Candidate.Select(P => (P.X, P.Y)).ToList(),
                Other.Points.Select(P => (P.X, P.Y)).ToList());
            BeforeCrossings += Before.Crossings; AfterCrossings += After.Crossings;
            BeforeOverlap += Before.Overlap; AfterOverlap += After.Overlap;
        }
        return AfterCrossings > BeforeCrossings || AfterOverlap > BeforeOverlap + 0.01;
    }

    private static int CornerCount(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Count = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            if ((Math.Abs(Points[Index].X - Points[Index - 1].X) < 0.5)
                != (Math.Abs(Points[Index + 1].X - Points[Index].X) < 0.5))
            {
                Count++;
            }
        }

        return Count;
    }

    /// <summary>Uses the renderer connection body when it differs from the visual rectangle.</summary>
    /// <param name="Box">The current measured node geometry.</param>
    /// <param name="Side">Requested compass side.</param>
    /// <param name="Fraction">Position along the eligible edge.</param>
    /// <param name="Point">Receives the routing-space port; unchanged for legacy outlines.</param>
    /// <returns>Whether the requested position is eligible.</returns>
    private static bool TryGeometryPort(FlowchartLayoutNodeBounds Box, string Side, double Fraction, ref FlowchartLayoutPoint Point)
    {
        if (Box.Geometry == null)
        {
            return true;
        }

        if (!Box.Geometry.ConnectionBoundary.TryPort(Side, Fraction, out NodeGeometryPort Port))
        {
            return false;
        }

        Point = new() { X = Box.CenterX + Port.X, Y = Box.CenterY - Port.Y };
        return true;
    }

    private static FlowchartLayoutPoint Port(FlowchartLayoutNodeBounds Box, string Side, double Fraction)
    {
        double Horizontal = Box.CenterX - (Box.Width / 2.0) + (Box.Width * Fraction);
        double Vertical = Box.CenterY - (Box.Height / 2.0) + (Box.Height * Fraction);
        return Side switch
        {
            "left" => new() { X = Box.CenterX - (Box.Width / 2.0), Y = Vertical },
            "right" => new() { X = Box.CenterX + (Box.Width / 2.0), Y = Vertical },
            "top" => new() { X = Horizontal, Y = Box.CenterY + (Box.Height / 2.0) },
            _ => new() { X = Horizontal, Y = Box.CenterY - (Box.Height / 2.0) }
        };
    }

    private static bool Leaves(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X < A.X, "right" => B.X > A.X, "top" => B.Y > A.Y, _ => B.Y < A.Y
    };

    private static bool Enters(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X > A.X, "right" => B.X < A.X, "top" => B.Y < A.Y, _ => B.Y > A.Y
    };
}
