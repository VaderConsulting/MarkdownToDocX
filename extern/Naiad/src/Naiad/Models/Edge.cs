namespace MermaidSharp.Models;

#pragma warning disable IDE0007 // Naiad prefers explicit types for clarity and debugging (repo analyser enforces var).
#pragma warning disable IDE0022 // Naiad prefers block bodies over expression bodies for debuggability.

public class Edge
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public string? Label { get; set; }
    public EdgeType Type { get; set; } = EdgeType.Arrow;
    public EdgeStyle LineStyle { get; set; } = EdgeStyle.Solid;
    public Style Style { get; set; } = new();
    public string? CssClass { get; set; }

    /// <summary>
    /// Minimum number of ranks (layers) this edge must span in the layout graph. Flowcharts map long <c>--</c> runs to larger values (Mermaid-style link length).
    /// </summary>
    public int MinRankSpan { get; set; } = 1;

    // Layout properties (set by layout engine)
    public List<Position> Points { get; } = [];

    public Position LabelPosition
    {
        get
        {
            if (Points.Count == 0)
            {
                return Position.Zero;
            }

            if (Points.Count == 1)
            {
                return Points[0];
            }

            // Mermaid positions edge labels by traversing half the total polyline distance,
            // not by choosing the middle point index.
            double TotalDistance = 0.0;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                TotalDistance += Distance(Points[Index - 1], Points[Index]);
            }

            double RemainingDistance = TotalDistance / 2.0;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                Position PreviousPoint = Points[Index - 1];
                Position Point = Points[Index];
                double SegmentDistance = Distance(PreviousPoint, Point);

                if (SegmentDistance <= 0.0000001)
                {
                    continue;
                }

                if (SegmentDistance < RemainingDistance)
                {
                    RemainingDistance -= SegmentDistance;
                    continue;
                }

                double DistanceRatio = RemainingDistance / SegmentDistance;
                if (DistanceRatio <= 0.0)
                {
                    return PreviousPoint;
                }
                if (DistanceRatio >= 1.0)
                {
                    return Point;
                }

                double X = RoundNumber(((1.0 - DistanceRatio) * PreviousPoint.X) + (DistanceRatio * Point.X), 5);
                double Y = RoundNumber(((1.0 - DistanceRatio) * PreviousPoint.Y) + (DistanceRatio * Point.Y), 5);
                return new Position(X, Y);
            }

            return Points[^1];
        }
    }

    private static double Distance(Position Point1, Position Point2)
    {
        double Dx = Point2.X - Point1.X;
        double Dy = Point2.Y - Point1.Y;
        return System.Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }

    private static double RoundNumber(double Value, int Decimals)
    {
        return System.Math.Round(Value, Decimals);
    }

    public bool HasArrowHead =>
        Type is
            EdgeType.Arrow or
            EdgeType.DottedArrow or
            EdgeType.ThickArrow or
            EdgeType.BiDirectional;

    public bool HasArrowTail => Type is
        EdgeType.BiDirectional;

    public bool HasCircleEnd => Type is EdgeType.CircleEnd or EdgeType.BiDirectionalCircle;
    public bool HasCrossEnd => Type is EdgeType.CrossEnd or EdgeType.BiDirectionalCross;
}

#pragma warning restore IDE0022
#pragma warning restore IDE0007