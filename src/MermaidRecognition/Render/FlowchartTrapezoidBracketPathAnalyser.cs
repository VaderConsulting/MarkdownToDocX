using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Render;

/// <summary>
/// Analyses rendered SVG path data for legacy Mermaid trapezoid bracket node shapes (F81/F82).
/// </summary>
public static class FlowchartTrapezoidBracketPathAnalyser
{
    private const double Y_TOLERANCE_PX = 0.01;

    private const double MIN_WIDTH_ASYMMETRY_PX = 0.5;

    private static readonly HashSet<string> TrapezoidBracketShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "lean_right",
        "lean_left",
        "trapezoid",
        "inv_trapezoid"
    };

    /// <summary>
    /// Returns true when <paramref name="Shape"/> is a legacy trapezoid bracket delimiter shape.
    /// </summary>
    public static bool IsTrapezoidBracketShape(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return false;
        }

        return TrapezoidBracketShapes.Contains(Shape.Trim());
    }

    /// <summary>
    /// Returns true when path data is a pure axis-aligned rectangle (uses H/V commands).
    /// </summary>
    public static bool IsAxisAlignedRectanglePath(string PathData)
    {
        if (string.IsNullOrWhiteSpace(PathData))
        {
            return false;
        }

        string UpperPath = PathData.ToUpperInvariant();
        return UpperPath.IndexOf('H', StringComparison.Ordinal) >= 0
            || UpperPath.IndexOf('V', StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Validates that rendered path geometry matches the bracket shape intent.
    /// </summary>
    public static bool ValidateRenderedBracketGeometry(string? Shape, string PathData)
    {
        if (!IsTrapezoidBracketShape(Shape))
        {
            return false;
        }

        if (IsAxisAlignedRectanglePath(PathData))
        {
            return false;
        }

        string NormalisedShape = Shape!.Trim();
        double TopSpan = GetHorizontalSpanAtTopEdge(PathData);
        double BottomSpan = GetHorizontalSpanAtBottomEdge(PathData);

        if (string.Equals(NormalisedShape, "lean_right", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Abs(TopSpan - BottomSpan) <= MIN_WIDTH_ASYMMETRY_PX
                && GetLeftEdgeXAtExtremeY(PathData, UseMaxY: false) > GetLeftEdgeXAtExtremeY(PathData, UseMaxY: true);
        }

        if (string.Equals(NormalisedShape, "lean_left", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Abs(TopSpan - BottomSpan) <= MIN_WIDTH_ASYMMETRY_PX
                && GetLeftEdgeXAtExtremeY(PathData, UseMaxY: false) < GetLeftEdgeXAtExtremeY(PathData, UseMaxY: true);
        }

        if (string.Equals(NormalisedShape, "trapezoid", StringComparison.OrdinalIgnoreCase))
        {
            return TopSpan > BottomSpan + MIN_WIDTH_ASYMMETRY_PX;
        }

        if (string.Equals(NormalisedShape, "inv_trapezoid", StringComparison.OrdinalIgnoreCase))
        {
            return BottomSpan > TopSpan + MIN_WIDTH_ASYMMETRY_PX;
        }

        return false;
    }

    /// <summary>
    /// Returns the horizontal span of the path at its minimum Y coordinate.
    /// </summary>
    public static double GetHorizontalSpanAtTopEdge(string PathData) => GetHorizontalSpanAtExtremeY(PathData, UseMaxY: false);

    /// <summary>
    /// Returns the horizontal span of the path at its maximum Y coordinate.
    /// </summary>
    public static double GetHorizontalSpanAtBottomEdge(string PathData) => GetHorizontalSpanAtExtremeY(PathData, UseMaxY: true);

    private static double GetHorizontalSpanAtExtremeY(string PathData, bool UseMaxY)
    {
        List<(double X, double Y)> Points = ExtractMoveLinePoints(PathData);
        if (Points.Count == 0)
        {
            return 0.0;
        }

        double TargetY = UseMaxY
            ? Points.Max(Point => Point.Y)
            : Points.Min(Point => Point.Y);

        List<double> XValues = Points
            .Where(Point => Math.Abs(Point.Y - TargetY) <= Y_TOLERANCE_PX)
            .Select(Point => Point.X)
            .ToList();

        if (XValues.Count < 2)
        {
            return 0.0;
        }

        return XValues.Max() - XValues.Min();
    }

    /// <summary>
    /// Returns the minimum X on the path at the top or bottom edge.
    /// </summary>
    public static double GetLeftEdgeXAtExtremeY(string PathData, bool UseMaxY)
    {
        List<(double X, double Y)> Points = ExtractMoveLinePoints(PathData);
        if (Points.Count == 0)
        {
            return 0.0;
        }

        double TargetY = UseMaxY
            ? Points.Max(Point => Point.Y)
            : Points.Min(Point => Point.Y);

        List<double> XValues = Points
            .Where(Point => Math.Abs(Point.Y - TargetY) <= Y_TOLERANCE_PX)
            .Select(Point => Point.X)
            .ToList();

        if (XValues.Count == 0)
        {
            return 0.0;
        }

        return XValues.Min();
    }

    private static List<(double X, double Y)> ExtractMoveLinePoints(string PathData)
    {
        List<(double X, double Y)> Points = [];
        MatchCollection Matches = Regex.Matches(
            PathData,
            @"([ML])\s*([-\d.]+)\s*,\s*([-\d.]+)",
            RegexOptions.CultureInvariant);

        foreach (Match Match in Matches)
        {
            if (Match.Groups.Count > 3)
            {
                Points.Add((
                    double.Parse(Match.Groups[2].Value, CultureInfo.InvariantCulture),
                    double.Parse(Match.Groups[3].Value, CultureInfo.InvariantCulture)));
            }
        }

        return Points;
    }
}
