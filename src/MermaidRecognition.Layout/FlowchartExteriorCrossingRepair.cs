using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Moves a crossing route to the nearest clear exterior track when that strictly reduces crossings.</summary>
internal static class FlowchartExteriorCrossingRepair
{
    internal static void Apply(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        DiagramContract? Contract = null)
    {
        if (Layout.EdgeRoutes.Count < 2 || Bounds.Count == 0) return;
        double Left = Bounds.Values.Min(Box => Box.CenterX - Box.Width / 2.0) - DiagramGridMetrics.GRID_UNIT_PX;
        double Right = Bounds.Values.Max(Box => Box.CenterX + Box.Width / 2.0) + DiagramGridMetrics.GRID_UNIT_PX;
        double Bottom = Bounds.Values.Min(Box => Box.CenterY - Box.Height / 2.0) - DiagramGridMetrics.GRID_UNIT_PX;
        double Top = Bounds.Values.Max(Box => Box.CenterY + Box.Height / 2.0) + DiagramGridMetrics.GRID_UNIT_PX;

        int routeOrdinal = 0;
        bool Moved;
        do
        {
            Moved = false;
            foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
            {
                int laneOrdinal = routeOrdinal++;
                if (Contract != null
                    && DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true)
                    continue;
                int Before = Score(Route.Points, Route, Layout.EdgeRoutes);
                if (Before == 0 || Route.Points.Count < 2) continue;
                FlowchartLayoutPoint Start = Route.Points[0], End = Route.Points[^1];
                var StartStub = Stub(Start, Route.ExitSide);
                var EndStub = Stub(End, Route.EntrySide);
                List<(List<(double X, double Y)> Points, string? Side)> Candidates =
                [
                    (Simplify([(Start.X, Start.Y), StartStub, (Left, StartStub.Y), (Left, EndStub.Y), EndStub, (End.X, End.Y)]), null),
                    (Simplify([(Start.X, Start.Y), StartStub, (Right, StartStub.Y), (Right, EndStub.Y), EndStub, (End.X, End.Y)]), null),
                    (Simplify([(Start.X, Start.Y), StartStub, (StartStub.X, Bottom), (EndStub.X, Bottom), EndStub, (End.X, End.Y)]), null),
                    (Simplify([(Start.X, Start.Y), StartStub, (StartStub.X, Top), (EndStub.X, Top), EndStub, (End.X, End.Y)]), null),
                    Exterior(Bounds[Route.SourceId], Bounds[Route.TargetId], "left", Left),
                    Exterior(Bounds[Route.SourceId], Bounds[Route.TargetId], "right", Right),
                    Exterior(Bounds[Route.SourceId], Bounds[Route.TargetId], "bottom", Bottom),
                    Exterior(Bounds[Route.SourceId], Bounds[Route.TargetId], "top", Top),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "top", Left),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "top", Right),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "bottom", Left),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "bottom", Right),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "left", Bottom),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "left", Top),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "right", Bottom),
                    ExteriorTurn(Bounds[Route.SourceId], Bounds[Route.TargetId], "right", Top)
                ];
                // Backward lines get private perimeter tracks. They keep their
                // allocated endpoint sides and never take a shortcut through the
                // centre of a diagram just because that path is shorter.
                double LaneStep = DiagramGridMetrics.GRID_UNIT_PX;
                foreach (double Lane in new[]
                {
                    Left - laneOrdinal * LaneStep,
                    Right + laneOrdinal * LaneStep,
                    Bottom - laneOrdinal * LaneStep,
                    Top + laneOrdinal * LaneStep
                })
                {
                    Candidates.Add((Simplify([(Start.X, Start.Y), StartStub,
                        (Lane, StartStub.Y), (Lane, EndStub.Y), EndStub, (End.X, End.Y)]), null));
                }
                foreach (double Lane in new[]
                {
                    Top + (laneOrdinal + 1) * LaneStep,
                    Bottom - (laneOrdinal + 1) * LaneStep
                })
                {
                    Candidates.Add((Simplify([(Start.X, Start.Y), StartStub,
                        (StartStub.X, Lane), (EndStub.X, Lane), EndStub, (End.X, End.Y)]), null));
                }
                foreach (double Lane in new[]
                {
                    Left - (laneOrdinal + 1) * LaneStep,
                    Right + (laneOrdinal + 1) * LaneStep
                })
                {
                    Candidates.Add((Simplify([(Start.X, Start.Y), StartStub,
                        (Lane, StartStub.Y), (Lane, EndStub.Y), EndStub, (End.X, End.Y)]), null));
                }
                var Best = Candidates.Where(Candidate => Candidate.Side == null
                        && Clear(Candidate.Points, Route, Bounds))
                    .Select(Candidate => (Candidate.Points, Candidate.Side, Score: Score(Candidate.Points, Route, Layout.EdgeRoutes),
                        Length: Length(Candidate.Points)))
                    .Where(Candidate => Candidate.Score < Before)
                    .OrderBy(Candidate => Candidate.Score).ThenBy(Candidate => Candidate.Length).FirstOrDefault();
                if (Best.Points == null) continue;
                Route.Points.Clear();
                foreach ((double X, double Y) in Best.Points)
                    Route.Points.Add(new FlowchartLayoutPoint { X = X, Y = Y });
                if (Best.Side != null) { Route.ExitSide = Best.Side; Route.EntrySide = Best.Side; }
                Moved = true;
            }
        } while (Moved);
    }

    private static (List<(double X, double Y)> Points, string? Side) ExteriorTurn(
        FlowchartLayoutNodeBounds Source, FlowchartLayoutNodeBounds Target, string Side, double Corridor)
    {
        double Radius = DiagramGridRouteBuilder.CORNER_RADIUS_PX + 5;
        bool VerticalSide = Side is "top" or "bottom";
        double Sign = Side is "top" or "right" ? 1 : -1;
        (double X, double Y) Start = Side switch
        {
            "left" => (Source.CenterX - Source.Width / 2.0, Source.CenterY),
            "right" => (Source.CenterX + Source.Width / 2.0, Source.CenterY),
            "bottom" => (Source.CenterX, Source.CenterY - Source.Height / 2.0),
            _ => (Source.CenterX, Source.CenterY + Source.Height / 2.0)
        };
        (double X, double Y) End = Side switch
        {
            "left" => (Target.CenterX - Target.Width / 2.0, Target.CenterY),
            "right" => (Target.CenterX + Target.Width / 2.0, Target.CenterY),
            "bottom" => (Target.CenterX, Target.CenterY - Target.Height / 2.0),
            _ => (Target.CenterX, Target.CenterY + Target.Height / 2.0)
        };
        (double X, double Y) StartStub = VerticalSide ? (Start.X, Start.Y + Sign * Radius) : (Start.X + Sign * Radius, Start.Y);
        (double X, double Y) EndStub = VerticalSide ? (End.X, End.Y + Sign * Radius) : (End.X + Sign * Radius, End.Y);
        List<(double X, double Y)> Points = VerticalSide
            ? Simplify([Start, StartStub, (Corridor, StartStub.Y), (Corridor, EndStub.Y), EndStub, End])
            : Simplify([Start, StartStub, (StartStub.X, Corridor), (EndStub.X, Corridor), EndStub, End]);
        return (Points, Side);
    }

    private static (List<(double X, double Y)> Points, string? Side) Exterior(
        FlowchartLayoutNodeBounds Source, FlowchartLayoutNodeBounds Target, string Side, double Corridor)
    {
        double Radius = DiagramGridRouteBuilder.CORNER_RADIUS_PX + 5;
        (double X, double Y) Start = Side switch
        {
            "left" => (Source.CenterX - Source.Width / 2.0, Source.CenterY),
            "right" => (Source.CenterX + Source.Width / 2.0, Source.CenterY),
            "bottom" => (Source.CenterX, Source.CenterY - Source.Height / 2.0),
            _ => (Source.CenterX, Source.CenterY + Source.Height / 2.0)
        };
        (double X, double Y) End = Side switch
        {
            "left" => (Target.CenterX - Target.Width / 2.0, Target.CenterY),
            "right" => (Target.CenterX + Target.Width / 2.0, Target.CenterY),
            "bottom" => (Target.CenterX, Target.CenterY - Target.Height / 2.0),
            _ => (Target.CenterX, Target.CenterY + Target.Height / 2.0)
        };
        List<(double X, double Y)> Points = Side is "left" or "right"
            ? Simplify([Start, (Start.X + (Side == "left" ? -Radius : Radius), Start.Y),
                (Corridor, Start.Y), (Corridor, End.Y),
                (End.X + (Side == "left" ? -Radius : Radius), End.Y), End])
            : Simplify([Start, (Start.X, Start.Y + (Side == "bottom" ? -Radius : Radius)),
                (Start.X, Corridor), (End.X, Corridor),
                (End.X, End.Y + (Side == "bottom" ? -Radius : Radius)), End]);
        return (Points, Side);
    }

    private static (double X, double Y) Stub(FlowchartLayoutPoint Point, string Side)
    {
        double Distance = DiagramGridRouteBuilder.CORNER_RADIUS_PX + 5;
        return Side.ToLowerInvariant() switch
        {
            "left" => (Point.X - Distance, Point.Y),
            "right" => (Point.X + Distance, Point.Y),
            "top" => (Point.X, Point.Y + Distance),
            "bottom" => (Point.X, Point.Y - Distance),
            _ => (Point.X, Point.Y)
        };
    }

    private static int Score(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Owner,
        IReadOnlyList<FlowchartLayoutEdgeRoute> Routes) =>
        Score(Points.Select(Point => (Point.X, Point.Y)).ToList(), Owner, Routes);

    private static int Score(IReadOnlyList<(double X, double Y)> Points, FlowchartLayoutEdgeRoute Owner,
        IReadOnlyList<FlowchartLayoutEdgeRoute> Routes)
    {
        int Result = 0;
        foreach (FlowchartLayoutEdgeRoute Other in Routes)
        {
            if (Other == Owner || Other.SourceId == Owner.SourceId || Other.SourceId == Owner.TargetId
                || Other.TargetId == Owner.SourceId || Other.TargetId == Owner.TargetId) continue;
            var Intersection = FlowchartBranchRouteOrder.Intersections(Points,
                Other.Points.Select(Point => (Point.X, Point.Y)).ToList());
            Result += Intersection.Crossings * 1000 + (int)Math.Ceiling(Intersection.Overlap);
        }
        return Result;
    }

    private static bool Clear(IReadOnlyList<(double X, double Y)> Points, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            var A = Points[Index - 1]; var B = Points[Index];
            if (Math.Abs(A.X - B.X) > 0.01 && Math.Abs(A.Y - B.Y) > 0.01) return false;
            foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
            {
                if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId) continue;
                if (Math.Max(A.X, B.X) > Box.CenterX - Box.Width / 2.0 - 4
                    && Math.Min(A.X, B.X) < Box.CenterX + Box.Width / 2.0 + 4
                    && Math.Max(A.Y, B.Y) > Box.CenterY - Box.Height / 2.0 - 4
                    && Math.Min(A.Y, B.Y) < Box.CenterY + Box.Height / 2.0 + 4) return false;
            }
        }
        return true;
    }

    private static List<(double X, double Y)> Simplify(List<(double X, double Y)> Points)
    {
        var Result = new List<(double X, double Y)>();
        foreach (var Point in Points)
        {
            if (Result.Count > 0 && Math.Abs(Result[^1].X - Point.X) < 0.01 && Math.Abs(Result[^1].Y - Point.Y) < 0.01) continue;
            while (Result.Count >= 2 && (Math.Abs(Result[^2].X - Result[^1].X) < 0.01 && Math.Abs(Result[^1].X - Point.X) < 0.01
                || Math.Abs(Result[^2].Y - Result[^1].Y) < 0.01 && Math.Abs(Result[^1].Y - Point.Y) < 0.01)) Result.RemoveAt(Result.Count - 1);
            Result.Add(Point);
        }
        return Result;
    }

    private static double Length(IReadOnlyList<(double X, double Y)> Points)
    {
        double Result = 0;
        for (int Index = 1; Index < Points.Count; Index++)
            Result += Math.Abs(Points[Index].X - Points[Index - 1].X) + Math.Abs(Points[Index].Y - Points[Index - 1].Y);
        return Result;
    }
}
