using System.Globalization;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Layout.Tests;

internal static class OrthogonalRouteTestHelper
{
    /// <summary>Allows straight strokes but rejects a direction change at an unrounded line vertex.</summary>
    internal static void AssertEveryBendRounded(string PathData)
    {
        AssertOrthogonalPathWithRoundedCorners(PathData);
        (double X, double Y) Current = default, PreviousVector = default;
        bool PreviousWasLine = false;
        foreach (Match Command in Regex.Matches(PathData, @"([MLC])([^MLC]*)"))
        {
            double[] Values = ParseNumbers(Command.Groups[2].Value);
            string Kind = Command.Groups[1].Value;
            var Next = (X: Values[^2], Y: Values[^1]);
            var Vector = (X: Next.X - Current.X, Y: Next.Y - Current.Y);
            if (Kind == "L" && PreviousWasLine)
                Assert.That(Math.Abs(PreviousVector.X * Vector.Y - PreviousVector.Y * Vector.X),
                    Is.LessThan(0.01), "Every direction change must use a rounded corner.");
            PreviousWasLine = Kind == "L";
            PreviousVector = Vector;
            Current = Next;
        }
    }

    internal static string ExtractEdgePath(string Svg, string SourceId, string TargetId)
    {
        string Marker = $"data-edge-source=\"{SourceId}\" data-edge-target=\"{TargetId}\"";
        Match PathMatch = Regex.Match(
            Svg,
            $@"<path d=""([^""]+)""[^>]*{Marker}",
            RegexOptions.CultureInvariant);
        if (PathMatch.Success)
        {
            return PathMatch.Groups[1].Value;
        }

        Match LineMatch = Regex.Match(
            Svg,
            $@"<line[^>]+{Marker}[^>]*/>",
            RegexOptions.CultureInvariant);
        if (!LineMatch.Success)
        {
            return string.Empty;
        }

        Match X1 = Regex.Match(LineMatch.Value, @"x1=""([^""]+)""");
        Match Y1 = Regex.Match(LineMatch.Value, @"y1=""([^""]+)""");
        Match X2 = Regex.Match(LineMatch.Value, @"x2=""([^""]+)""");
        Match Y2 = Regex.Match(LineMatch.Value, @"y2=""([^""]+)""");
        return $"M{X1.Groups[1].Value},{Y1.Groups[1].Value}L{X2.Groups[1].Value},{Y2.Groups[1].Value}";
    }

    internal static void AssertOrthogonalPathWithRoundedCorners(string PathData)
    {
        Assert.That(PathData, Is.Not.Empty);
        Assert.That(PathData, Does.Not.Contain('S'), "Routes must not use smooth cubic continuation");
        Assert.That(PathData, Does.Not.Contain('Q'), "Routes must not use quadratic curves");
        Assert.That(PathData, Does.Not.Match(@"[Aa]"), "Routes must not use elliptical arcs");

        double CurrentX = 0.0;
        double CurrentY = 0.0;
        int CubicCount = 0;
        int LineCommandCount = 0;
        foreach (Match Match in Regex.Matches(
                     PathData,
                     @"([MLC])([^MLC]*)",
                     RegexOptions.CultureInvariant))
        {
            string Command = Match.Groups[1].Value;
            double[] Values = ParseNumbers(Match.Groups[2].Value);
            switch (Command)
            {
                case "M":
                    CurrentX = Values[0];
                    CurrentY = Values[1];
                    break;
                case "L":
                    LineCommandCount++;
                    AssertSegmentAxisAligned(CurrentX, CurrentY, Values[0], Values[1]);
                    CurrentX = Values[0];
                    CurrentY = Values[1];
                    break;
                case "C":
                    CubicCount++;
                    CurrentX = Values[4];
                    CurrentY = Values[5];
                    break;
            }
        }

        Assert.That(CubicCount, Is.LessThanOrEqualTo(3), "Side branches use rounded corners, not free curves");
        Assert.That(LineCommandCount, Is.LessThanOrEqualTo(4),
            "Routes must not regress to long angular polylines");
    }

    internal static (double X, double Y)? LongestHorizontalRunMidpoint(string PathData)
    {
        List<(double X, double Y)> Points = ParsePathPoints(PathData);
        if (Points.Count < 2)
        {
            return null;
        }

        double BestLength = 0.0;
        (double X, double Y)? BestMidpoint = null;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            (double X0, double Y0) = Points[Index - 1];
            (double X1, double Y1) = Points[Index];
            if (Math.Abs(Y0 - Y1) > 0.5)
            {
                continue;
            }

            double Length = Math.Abs(X1 - X0);
            if (Length > BestLength)
            {
                BestLength = Length;
                BestMidpoint = ((X0 + X1) / 2.0, (Y0 + Y1) / 2.0);
            }
        }

        return BestMidpoint;
    }

    internal static bool IsStraightVerticalConnector(string Svg, string SourceId, string TargetId)
    {
        string Marker = $"data-edge-source=\"{SourceId}\" data-edge-target=\"{TargetId}\"";
        List<(double X, double Y1, double Y2)> VerticalSegments = [];
        foreach (Match LineMatch in Regex.Matches(Svg, $@"<line[^>]+{Marker}[^>]*/>", RegexOptions.CultureInvariant))
        {
            double X1 = ReadAttribute(LineMatch.Value, "x1");
            double Y1 = ReadAttribute(LineMatch.Value, "y1");
            double X2 = ReadAttribute(LineMatch.Value, "x2");
            double Y2 = ReadAttribute(LineMatch.Value, "y2");
            if (Math.Abs(X1 - X2) <= 0.5)
            {
                VerticalSegments.Add(((X1 + X2) / 2.0, Y1, Y2));
            }
        }

        if (VerticalSegments.Count > 0)
        {
            double SpineX = VerticalSegments[0].X;
            if (VerticalSegments.All(Segment => Math.Abs(Segment.X - SpineX) <= 0.5))
            {
                double MinY = VerticalSegments.Min(Segment => Math.Min(Segment.Y1, Segment.Y2));
                double MaxY = VerticalSegments.Max(Segment => Math.Max(Segment.Y1, Segment.Y2));
                if (MaxY - MinY > 20.0)
                {
                    return true;
                }
            }
        }

        string PathData = ExtractEdgePath(Svg, SourceId, TargetId);
        if (SvgStraightConnectorReader.Read($"<path d=\"{PathData}\" fill=\"none\"/>").Any(
                Connector => Math.Abs(Connector.X1 - Connector.X2) < 0.5
                    && Math.Abs(Connector.Y2 - Connector.Y1) > 20.0))
        {
            return true;
        }

        List<(double X, double Y)> Points = ParsePathPoints(PathData);
        if (Points.Count < 2)
        {
            return false;
        }

        (double StartX, double StartY) = Points[0];
        (double EndX, double EndY) = Points[^1];
        return Math.Abs(StartX - EndX) <= 0.5 && Math.Abs(EndY - StartY) > 20.0;
    }

    private static double ReadAttribute(string Element, string Name)
    {
        Match Match = Regex.Match(Element, $@"{Name}=""([^""]+)""", RegexOptions.CultureInvariant);
        return double.Parse(Match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static void AssertSegmentAxisAligned(double X0, double Y0, double X1, double Y1)
    {
        if (Math.Abs(X0 - X1) <= 0.5 && Math.Abs(Y0 - Y1) <= 0.5)
        {
            return;
        }

        bool Horizontal = Math.Abs(Y0 - Y1) <= 0.5;
        bool Vertical = Math.Abs(X0 - X1) <= 0.5;
        Assert.That(Horizontal || Vertical, Is.True, "Each straight run must be horizontal or vertical");
    }

    private static double[] ParseNumbers(string Text)
    {
        return Regex.Matches(Text, @"-?\d+(?:\.\d+)?")
            .Select(Match => double.Parse(Match.Value, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static List<(double X, double Y)> ParsePathPoints(string PathData)
    {
        List<(double X, double Y)> Points = [];
        foreach (Match Match in Regex.Matches(
                     PathData,
                     @"([ML])(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)",
                     RegexOptions.CultureInvariant))
        {
            Points.Add((
                double.Parse(Match.Groups[2].Value, CultureInfo.InvariantCulture),
                double.Parse(Match.Groups[3].Value, CultureInfo.InvariantCulture)));
        }

        return Points;
    }
}
