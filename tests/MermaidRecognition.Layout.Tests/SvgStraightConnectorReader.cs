using System.Globalization;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// One straight edge connector read from rendered SVG, from its first point to its last point.
/// </summary>
/// <param name="X1">X of the first point.</param>
/// <param name="Y1">Y of the first point.</param>
/// <param name="X2">X of the last point.</param>
/// <param name="Y2">Y of the last point.</param>
internal readonly record struct SvgStraightConnector(double X1, double Y1, double X2, double Y2);

/// <summary>
/// Reads straight edge connectors from rendered flowchart SVG.
/// </summary>
/// <remarks>
/// Straight edges are emitted as unfilled <c>&lt;path d="M… L… L…"&gt;</c> elements whose points all
/// lie on one line (spec §15.0A). A few legacy code paths still emit <c>&lt;line&gt;</c>. Both forms are
/// returned so tests do not depend on which element the renderer picks.
/// </remarks>
internal static class SvgStraightConnectorReader
{
    private const double COLLINEAR_TOLERANCE_PX = 0.5;

    /// <summary>
    /// Returns every straight connector in the SVG, in document order.
    /// </summary>
    /// <param name="Svg">The rendered SVG text.</param>
    /// <returns>The straight connectors, each from its first to its last point.</returns>
    public static List<SvgStraightConnector> Read(string Svg)
    {
        List<SvgStraightConnector> Connectors = [];
        MatchCollection ElementMatches = Regex.Matches(
            Svg,
            @"<line x1=""([^""]+)"" y1=""([^""]+)"" x2=""([^""]+)"" y2=""([^""]+)""|<path d=""(M[^""]+)"" fill=""none""",
            RegexOptions.CultureInvariant);

        foreach (Match ElementMatch in ElementMatches)
        {
            if (ElementMatch.Groups[1].Success)
            {
                Connectors.Add(new SvgStraightConnector(
                    ParseValue(ElementMatch.Groups[1].Value),
                    ParseValue(ElementMatch.Groups[2].Value),
                    ParseValue(ElementMatch.Groups[3].Value),
                    ParseValue(ElementMatch.Groups[4].Value)));
                continue;
            }

            if (TryReadStraightPath(ElementMatch.Groups[5].Value, out SvgStraightConnector Connector))
            {
                Connectors.Add(Connector);
            }
        }

        return Connectors;
    }

    private static bool TryReadStraightPath(string PathData, out SvgStraightConnector Connector)
    {
        Connector = default;
        if (Regex.IsMatch(PathData, "[^MLml0-9eE.,\\s+-]", RegexOptions.CultureInvariant))
        {
            return false;
        }

        MatchCollection PointMatches = Regex.Matches(
            PathData,
            @"[ML]\s*(-?[\d.]+(?:[eE][-+]?\d+)?)[,\s]+(-?[\d.]+(?:[eE][-+]?\d+)?)",
            RegexOptions.CultureInvariant);
        if (PointMatches.Count < 2)
        {
            return false;
        }

        List<(double X, double Y)> Points = [];
        foreach (Match PointMatch in PointMatches)
        {
            Points.Add((ParseValue(PointMatch.Groups[1].Value), ParseValue(PointMatch.Groups[2].Value)));
        }

        (double X, double Y) First = Points[0];
        (double X, double Y) Last = Points[Points.Count - 1];
        double DeltaX = Last.X - First.X;
        double DeltaY = Last.Y - First.Y;
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (Length <= 1e-6)
        {
            return false;
        }

        foreach ((double X, double Y) Point in Points)
        {
            double Distance = Math.Abs((DeltaX * (Point.Y - First.Y)) - (DeltaY * (Point.X - First.X))) / Length;
            if (Distance > COLLINEAR_TOLERANCE_PX)
            {
                return false;
            }
        }

        Connector = new SvgStraightConnector(First.X, First.Y, Last.X, Last.Y);
        return true;
    }

    private static double ParseValue(string Text)
    {
        return double.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
