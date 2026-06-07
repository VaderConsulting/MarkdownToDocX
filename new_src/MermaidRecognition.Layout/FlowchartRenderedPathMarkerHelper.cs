using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves marker approach points and tangents from flattened rendered SVG path geometry.
/// </summary>
public static class FlowchartRenderedPathMarkerHelper
{
    private const double MIN_APPROACH_RETREAT_PX = 8.0;

    private const double MIN_TANGENT_ALIGNMENT_DOT = 0.98;

    /// <summary>
    /// Returns a point on the rendered stroke immediately before the path endpoint.
    /// </summary>
    public static bool TryResolveEndpointApproachPoint(
        string PathData,
        bool AtTargetEnd,
        out double ApproachX,
        out double ApproachY)
    {
        ApproachX = 0.0;
        ApproachY = 0.0;

        if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples)
            || Samples.Count < 2)
        {
            return false;
        }

        return TryResolveEndpointApproachPoint(Samples, AtTargetEnd, out ApproachX, out ApproachY);
    }

    /// <summary>
    /// Returns a point on the polyline immediately before the endpoint, retreating at least
    /// <see cref="MIN_APPROACH_RETREAT_PX"/> along the stroke from the tip.
    /// </summary>
    public static bool TryResolveEndpointApproachPoint(
        IReadOnlyList<(double X, double Y)> Samples,
        bool AtTargetEnd,
        out double ApproachX,
        out double ApproachY)
    {
        ApproachX = 0.0;
        ApproachY = 0.0;

        if (Samples.Count < 2)
        {
            return false;
        }

        if (AtTargetEnd)
        {
            double AccumulatedLength = 0.0;
            for (int PointIndex = Samples.Count - 1; PointIndex > 0; PointIndex--)
            {
                (double EndX, double EndY) = Samples[PointIndex];
                (double StartX, double StartY) = Samples[PointIndex - 1];
                double SegmentLength = SegmentLengthBetween(StartX, StartY, EndX, EndY);
                if (AccumulatedLength + SegmentLength >= MIN_APPROACH_RETREAT_PX)
                {
                    double Remaining = MIN_APPROACH_RETREAT_PX - AccumulatedLength;
                    double Ratio = SegmentLength <= 1e-6 ? 0.0 : Remaining / SegmentLength;
                    ApproachX = EndX + ((StartX - EndX) * Ratio);
                    ApproachY = EndY + ((StartY - EndY) * Ratio);
                    return true;
                }

                AccumulatedLength += SegmentLength;
            }

            ApproachX = Samples[Samples.Count - 2].X;
            ApproachY = Samples[Samples.Count - 2].Y;
            return true;
        }

        double ForwardAccumulatedLength = 0.0;
        for (int PointIndex = 1; PointIndex < Samples.Count; PointIndex++)
        {
            (double StartX, double StartY) = Samples[PointIndex - 1];
            (double EndX, double EndY) = Samples[PointIndex];
            double SegmentLength = SegmentLengthBetween(StartX, StartY, EndX, EndY);
            if (ForwardAccumulatedLength + SegmentLength >= MIN_APPROACH_RETREAT_PX)
            {
                double Remaining = MIN_APPROACH_RETREAT_PX - ForwardAccumulatedLength;
                double Ratio = SegmentLength <= 1e-6 ? 0.0 : Remaining / SegmentLength;
                ApproachX = StartX + ((EndX - StartX) * Ratio);
                ApproachY = StartY + ((EndY - StartY) * Ratio);
                return true;
            }

            ForwardAccumulatedLength += SegmentLength;
        }

        ApproachX = Samples[1].X;
        ApproachY = Samples[1].Y;
        return true;
    }

    /// <summary>
    /// Returns true when the unit direction from <paramref name="FromX"/>,<paramref name="FromY"/>
    /// to the path endpoint matches the rendered stroke tangent at that end.
    /// </summary>
    public static bool TryValidateMarkerAxisAlignsWithPath(
        string PathData,
        double TipX,
        double TipY,
        double FromX,
        double FromY,
        bool AtTargetEnd,
        out string FailureMessage)
    {
        FailureMessage = string.Empty;

        if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples)
            || Samples.Count < 2)
        {
            FailureMessage = "Rendered path could not be flattened for marker tangent validation.";
            return false;
        }

        if (!TryNormalize(TipX - FromX, TipY - FromY, out double MarkerDirX, out double MarkerDirY))
        {
            FailureMessage = "Marker axis is degenerate.";
            return false;
        }

        if (!TryResolvePathTangentAtEndpoint(Samples, AtTargetEnd, out double PathDirX, out double PathDirY))
        {
            FailureMessage = "Rendered path terminal segment is too short for marker tangent validation.";
            return false;
        }

        double AlignmentDot = (PathDirX * MarkerDirX) + (PathDirY * MarkerDirY);
        bool Aligned = AtTargetEnd
            ? AlignmentDot >= MIN_TANGENT_ALIGNMENT_DOT
            : AlignmentDot <= -MIN_TANGENT_ALIGNMENT_DOT;
        if (!Aligned)
        {
            string EndpointLabel = AtTargetEnd ? "target" : "source";
            FailureMessage = "Marker tangent at "
                + EndpointLabel
                + " endpoint diverges from rendered path (dot="
                + AlignmentDot.ToString("F3")
                + ").";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the unit tangent of the rendered stroke at the path endpoint.
    /// </summary>
    public static bool TryResolvePathTangentAtEndpoint(
        IReadOnlyList<(double X, double Y)> Samples,
        bool AtTargetEnd,
        out double DirX,
        out double DirY)
    {
        DirX = 0.0;
        DirY = 0.0;

        if (!TryResolveEndpointApproachPoint(Samples, AtTargetEnd, out double ApproachX, out double ApproachY))
        {
            return false;
        }

        if (AtTargetEnd)
        {
            (double EndX, double EndY) = Samples[Samples.Count - 1];
            return TryNormalize(EndX - ApproachX, EndY - ApproachY, out DirX, out DirY);
        }

        (double StartX, double StartY) = Samples[0];
        return TryNormalize(ApproachX - StartX, ApproachY - StartY, out DirX, out DirY);
    }

    private static double SegmentLengthBetween(double StartX, double StartY, double EndX, double EndY)
    {
        double DeltaX = EndX - StartX;
        double DeltaY = EndY - StartY;
        return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
    }

    private static bool TryNormalize(double DeltaX, double DeltaY, out double UnitX, out double UnitY)
    {
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (Length < 1e-6)
        {
            UnitX = 0.0;
            UnitY = 0.0;
            return false;
        }

        UnitX = DeltaX / Length;
        UnitY = DeltaY / Length;
        return true;
    }
}
