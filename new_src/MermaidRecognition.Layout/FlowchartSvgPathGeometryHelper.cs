using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Layout;

/// <summary>
/// Flattens rendered SVG path data into sample polylines for geometry validation.
/// </summary>
public static class FlowchartSvgPathGeometryHelper
{
    private const double CUBIC_FLATTEN_STEP_PX = 4.0;

    private const double MIN_KINK_DOT_PRODUCT = -0.42;

    private const double KINK_ENDPOINT_EXCLUSION_PX = 14.0;

    /// <summary>
    /// Returns true when <paramref name="PathData"/> can be flattened into at least two sample points.
    /// </summary>
    public static bool TryFlattenPathData(string PathData, out List<(double X, double Y)> Samples)
    {
        Samples = [];

        if (string.IsNullOrWhiteSpace(PathData))
        {
            return false;
        }

        double CurrentX = 0.0;
        double CurrentY = 0.0;
        MatchCollection CommandMatches = Regex.Matches(
            PathData.Trim(),
            @"([MLC])([^MLC]*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (Match CommandMatch in CommandMatches)
        {
            if (!CommandMatch.Success || CommandMatch.Groups.Count < 3)
            {
                continue;
            }

            string Command = CommandMatch.Groups[1].Value.ToUpperInvariant();
            string ArgumentText = CommandMatch.Groups[2].Value;
            List<double> Values = ParseNumericArguments(ArgumentText);
            if (Values.Count == 0)
            {
                continue;
            }

            if (string.Equals(Command, "M", StringComparison.Ordinal))
            {
                if (Values.Count < 2)
                {
                    continue;
                }

                CurrentX = Values[0];
                CurrentY = Values[1];
                AddDistinctSample(Samples, CurrentX, CurrentY);
                continue;
            }

            if (string.Equals(Command, "L", StringComparison.Ordinal))
            {
                for (int ValueIndex = 0; ValueIndex + 1 < Values.Count; ValueIndex += 2)
                {
                    CurrentX = Values[ValueIndex];
                    CurrentY = Values[ValueIndex + 1];
                    AddDistinctSample(Samples, CurrentX, CurrentY);
                }

                continue;
            }

            if (string.Equals(Command, "C", StringComparison.Ordinal))
            {
                for (int ValueIndex = 0; ValueIndex + 5 < Values.Count; ValueIndex += 6)
                {
                    double Control1X = Values[ValueIndex];
                    double Control1Y = Values[ValueIndex + 1];
                    double Control2X = Values[ValueIndex + 2];
                    double Control2Y = Values[ValueIndex + 3];
                    double EndX = Values[ValueIndex + 4];
                    double EndY = Values[ValueIndex + 5];
                    AppendFlattenedCubicSamples(
                        Samples,
                        CurrentX,
                        CurrentY,
                        Control1X,
                        Control1Y,
                        Control2X,
                        Control2Y,
                        EndX,
                        EndY);
                    CurrentX = EndX;
                    CurrentY = EndY;
                }
            }
        }

        return Samples.Count >= 2;
    }

    /// <summary>
    /// Returns true when the first explicit line segment in path data matches the guide stub direction.
    /// </summary>
    public static bool TryValidateSourceLineMatchesGuide(
        string PathData,
        (double X, double Y) BoundaryPoint,
        (double X, double Y) GuideStubPoint)
    {
        if (!TryGetFirstLineSegment(PathData, out double StartX, out double StartY, out double EndX, out double EndY))
        {
            return false;
        }

        double GuideDeltaX = GuideStubPoint.X - BoundaryPoint.X;
        double GuideDeltaY = GuideStubPoint.Y - BoundaryPoint.Y;
        double SegmentDeltaX = EndX - StartX;
        double SegmentDeltaY = EndY - StartY;
        return SegmentsAlign(GuideDeltaX, GuideDeltaY, SegmentDeltaX, SegmentDeltaY);
    }

    /// <summary>
    /// Returns true when the last explicit line segment in path data matches the guide stub direction.
    /// </summary>
    public static bool TryValidateTargetLineMatchesGuide(
        string PathData,
        (double X, double Y) GuideStubPoint,
        (double X, double Y) BoundaryPoint)
    {
        if (!TryGetLastLineSegment(PathData, out double StartX, out double StartY, out double EndX, out double EndY))
        {
            return false;
        }

        double GuideDeltaX = BoundaryPoint.X - GuideStubPoint.X;
        double GuideDeltaY = BoundaryPoint.Y - GuideStubPoint.Y;
        double SegmentDeltaX = EndX - StartX;
        double SegmentDeltaY = EndY - StartY;
        return SegmentsAlign(GuideDeltaX, GuideDeltaY, SegmentDeltaX, SegmentDeltaY);
    }

    private static bool SegmentsAlign(
        double GuideDeltaX,
        double GuideDeltaY,
        double SegmentDeltaX,
        double SegmentDeltaY)
    {
        double GuideLength = Math.Sqrt((GuideDeltaX * GuideDeltaX) + (GuideDeltaY * GuideDeltaY));
        double SegmentLength = Math.Sqrt((SegmentDeltaX * SegmentDeltaX) + (SegmentDeltaY * SegmentDeltaY));
        if (GuideLength <= 1e-6 || SegmentLength <= 1e-6)
        {
            return false;
        }

        double Alignment = ((GuideDeltaX / GuideLength) * (SegmentDeltaX / SegmentLength))
            + ((GuideDeltaY / GuideLength) * (SegmentDeltaY / SegmentLength));
        return Alignment >= 0.92;
    }

    private static bool TryGetFirstLineSegment(
        string PathData,
        out double StartX,
        out double StartY,
        out double EndX,
        out double EndY)
    {
        StartX = 0.0;
        StartY = 0.0;
        EndX = 0.0;
        EndY = 0.0;

        double CurrentX = 0.0;
        double CurrentY = 0.0;
        MatchCollection CommandMatches = Regex.Matches(
            PathData.Trim(),
            @"([MLC])([^MLC]*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (Match CommandMatch in CommandMatches)
        {
            string Command = CommandMatch.Groups[1].Value.ToUpperInvariant();
            List<double> Values = ParseNumericArguments(CommandMatch.Groups[2].Value);
            if (string.Equals(Command, "M", StringComparison.Ordinal) && Values.Count >= 2)
            {
                CurrentX = Values[0];
                CurrentY = Values[1];
                continue;
            }

            if (string.Equals(Command, "L", StringComparison.Ordinal) && Values.Count >= 2)
            {
                StartX = CurrentX;
                StartY = CurrentY;
                EndX = Values[0];
                EndY = Values[1];
                return true;
            }

            if (string.Equals(Command, "C", StringComparison.Ordinal) && Values.Count >= 6)
            {
                CurrentX = Values[4];
                CurrentY = Values[5];
            }
        }

        return false;
    }

    private static bool TryGetLastLineSegment(
        string PathData,
        out double StartX,
        out double StartY,
        out double EndX,
        out double EndY)
    {
        StartX = 0.0;
        StartY = 0.0;
        EndX = 0.0;
        EndY = 0.0;

        List<double> LineSegmentStarts = [];
        double CurrentX = 0.0;
        double CurrentY = 0.0;
        MatchCollection CommandMatches = Regex.Matches(
            PathData.Trim(),
            @"([MLC])([^MLC]*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (Match CommandMatch in CommandMatches)
        {
            string Command = CommandMatch.Groups[1].Value.ToUpperInvariant();
            List<double> Values = ParseNumericArguments(CommandMatch.Groups[2].Value);
            if (string.Equals(Command, "M", StringComparison.Ordinal) && Values.Count >= 2)
            {
                CurrentX = Values[0];
                CurrentY = Values[1];
                continue;
            }

            if (string.Equals(Command, "L", StringComparison.Ordinal))
            {
                for (int ValueIndex = 0; ValueIndex + 1 < Values.Count; ValueIndex += 2)
                {
                    LineSegmentStarts.Add(CurrentX);
                    LineSegmentStarts.Add(CurrentY);
                    CurrentX = Values[ValueIndex];
                    CurrentY = Values[ValueIndex + 1];
                }
            }
        }

        if (LineSegmentStarts.Count < 4)
        {
            return false;
        }

        int LastStartIndex = LineSegmentStarts.Count - 4;
        StartX = LineSegmentStarts[LastStartIndex];
        StartY = LineSegmentStarts[LastStartIndex + 1];
        EndX = CurrentX;
        EndY = CurrentY;
        return true;
    }

    /// <summary>
    /// Returns true when a dense sample polyline contains a visible kink away from the endpoints.
    /// </summary>
    public static bool HasVisibleKink(IReadOnlyList<(double X, double Y)> Samples)
    {
        if (Samples.Count < 4)
        {
            return false;
        }

        double TotalLength = ComputePolylineLength(Samples);
        double ExcludedLength = Math.Min(KINK_ENDPOINT_EXCLUSION_PX, TotalLength * 0.12);
        double StartExclusion = ExcludedLength;
        double EndExclusion = TotalLength - ExcludedLength;
        double TraversedLength = 0.0;

        for (int PointIndex = 1; PointIndex < Samples.Count - 2; PointIndex++)
        {
            double SegmentLength = Distance(Samples[PointIndex - 1], Samples[PointIndex]);
            TraversedLength += SegmentLength;
            if (TraversedLength < StartExclusion || TraversedLength > EndExclusion)
            {
                continue;
            }

            (double PreviousX, double PreviousY) = Samples[PointIndex - 1];
            (double CurrentX, double CurrentY) = Samples[PointIndex];
            (double NextX, double NextY) = Samples[PointIndex + 1];
            (double IncomingX, double IncomingY) = NormalizeVector(CurrentX - PreviousX, CurrentY - PreviousY);
            (double OutgoingX, double OutgoingY) = NormalizeVector(NextX - CurrentX, NextY - CurrentY);
            double DotProduct = (IncomingX * OutgoingX) + (IncomingY * OutgoingY);
            if (DotProduct < MIN_KINK_DOT_PRODUCT)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendFlattenedCubicSamples(
        List<(double X, double Y)> Samples,
        double StartX,
        double StartY,
        double Control1X,
        double Control1Y,
        double Control2X,
        double Control2Y,
        double EndX,
        double EndY)
    {
        double ApproximateLength = Distance((StartX, StartY), (Control1X, Control1Y))
            + Distance((Control1X, Control1Y), (Control2X, Control2Y))
            + Distance((Control2X, Control2Y), (EndX, EndY));
        int StepCount = Math.Max(2, (int)Math.Ceiling(ApproximateLength / CUBIC_FLATTEN_STEP_PX));

        for (int StepIndex = 1; StepIndex <= StepCount; StepIndex++)
        {
            double T = StepIndex / (double)StepCount;
            double OneMinusT = 1.0 - T;
            double SampleX =
                (OneMinusT * OneMinusT * OneMinusT * StartX)
                + (3.0 * OneMinusT * OneMinusT * T * Control1X)
                + (3.0 * OneMinusT * T * T * Control2X)
                + (T * T * T * EndX);
            double SampleY =
                (OneMinusT * OneMinusT * OneMinusT * StartY)
                + (3.0 * OneMinusT * OneMinusT * T * Control1Y)
                + (3.0 * OneMinusT * T * T * Control2Y)
                + (T * T * T * EndY);
            AddDistinctSample(Samples, SampleX, SampleY);
        }
    }

    private static List<double> ParseNumericArguments(string ArgumentText)
    {
        List<double> Values = [];
        MatchCollection NumberMatches = Regex.Matches(
            ArgumentText,
            @"-?\d+(?:\.\d+)?",
            RegexOptions.CultureInvariant);

        foreach (Match NumberMatch in NumberMatches)
        {
            if (double.TryParse(NumberMatch.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Value))
            {
                Values.Add(Value);
            }
        }

        return Values;
    }

    private static void AddDistinctSample(List<(double X, double Y)> Samples, double X, double Y)
    {
        if (Samples.Count == 0)
        {
            Samples.Add((X, Y));
            return;
        }

        (double LastX, double LastY) = Samples[Samples.Count - 1];
        if (Math.Abs(LastX - X) > 0.25 || Math.Abs(LastY - Y) > 0.25)
        {
            Samples.Add((X, Y));
        }
    }

    private static double ComputePolylineLength(IReadOnlyList<(double X, double Y)> Samples)
    {
        double TotalLength = 0.0;
        for (int PointIndex = 1; PointIndex < Samples.Count; PointIndex++)
        {
            TotalLength += Distance(Samples[PointIndex - 1], Samples[PointIndex]);
        }

        return TotalLength;
    }

    private static double Distance((double X, double Y) Left, (double X, double Y) Right)
    {
        double DeltaX = Right.X - Left.X;
        double DeltaY = Right.Y - Left.Y;
        return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
    }

    private static (double X, double Y) NormalizeVector(double VectorX, double VectorY)
    {
        double Length = Math.Sqrt((VectorX * VectorX) + (VectorY * VectorY));
        if (Length <= 1e-6)
        {
            return (0.0, 0.0);
        }

        return (VectorX / Length, VectorY / Length);
    }
}
