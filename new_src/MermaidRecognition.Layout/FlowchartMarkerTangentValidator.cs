using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Validates that arrowhead directions match the rendered connector tangent at each boundary.
/// </summary>
public static class FlowchartMarkerTangentValidator
{
    private const double MIN_DIRECTION_LENGTH_PX = 1e-6;

    private const double MIN_TANGENT_ALIGNMENT_DOT = 0.98;

    /// <summary>
    /// Returns true when the marker approach direction matches the rendered path tangent at the endpoint.
    /// </summary>
    public static bool TryValidateEndpointMarkerTangent(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction,
        bool AtTargetEnd,
        out string FailureMessage)
    {
        FailureMessage = string.Empty;

        if (RouteKind == FlowchartEdgeRouteKind.Straight
            || RouteKind == FlowchartEdgeRouteKind.GroupedTwoByTwo)
        {
            return TryValidateStraightEndpointTangent(GuidePoints, AtTargetEnd, out FailureMessage);
        }

        if (GuidePoints.Count < 2)
        {
            FailureMessage = "Guide polyline has fewer than two points.";
            return false;
        }

        FlowchartRenderedPathKind PathKind = FlowchartCurvePathBuilder.ResolveRenderedPathKind(
            GuidePoints,
            RouteKind,
            Direction);
        string PathData = FlowchartCurvePathBuilder.BuildRenderedEdgePathData(
            GuidePoints,
            RouteKind,
            Direction);

        if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples)
            || Samples.Count < 2)
        {
            FailureMessage = "Rendered path could not be flattened for tangent validation.";
            return false;
        }

        if (!TryGetUnitDirectionFromSamples(Samples, AtTargetEnd, out double PathDirX, out double PathDirY))
        {
            FailureMessage = "Rendered path terminal segment is too short for tangent validation.";
            return false;
        }

        (double ApproachX, double ApproachY) = FlowchartCurvePathBuilder.ResolveMarkerApproachPoint(
            GuidePoints,
            PathKind,
            AtTargetEnd);
        (double EndpointX, double EndpointY) = AtTargetEnd
            ? GuidePoints[GuidePoints.Count - 1]
            : GuidePoints[0];

        if (!TryNormalize(EndpointX - ApproachX, EndpointY - ApproachY, out double MarkerDirX, out double MarkerDirY))
        {
            FailureMessage = "Marker approach segment is too short for tangent validation.";
            return false;
        }

        double AlignmentDot = (PathDirX * MarkerDirX) + (PathDirY * MarkerDirY);
        if (AlignmentDot < MIN_TANGENT_ALIGNMENT_DOT)
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

    private static bool TryValidateStraightEndpointTangent(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        bool AtTargetEnd,
        out string FailureMessage)
    {
        FailureMessage = string.Empty;

        if (GuidePoints.Count < 2)
        {
            FailureMessage = "Straight connector guide polyline has fewer than two points.";
            return false;
        }

        (double StartX, double StartY) = GuidePoints[0];
        (double EndX, double EndY) = GuidePoints[GuidePoints.Count - 1];
        if (!TryNormalize(EndX - StartX, EndY - StartY, out double PathDirX, out double PathDirY))
        {
            FailureMessage = "Straight connector segment is too short for tangent validation.";
            return false;
        }

        double MarkerDirX = AtTargetEnd ? PathDirX : -PathDirX;
        double MarkerDirY = AtTargetEnd ? PathDirY : -PathDirY;
        double AlignmentDot = (PathDirX * MarkerDirX) + (PathDirY * MarkerDirY);
        if (AlignmentDot < MIN_TANGENT_ALIGNMENT_DOT)
        {
            FailureMessage = "Straight connector marker tangent diverges from guide segment.";
            return false;
        }

        return true;
    }

    private static bool TryGetUnitDirectionFromSamples(
        IReadOnlyList<(double X, double Y)> Samples,
        bool AtTargetEnd,
        out double DirX,
        out double DirY)
    {
        if (AtTargetEnd)
        {
            (double StartX, double StartY) = Samples[Samples.Count - 2];
            (double EndX, double EndY) = Samples[Samples.Count - 1];
            return TryNormalize(EndX - StartX, EndY - StartY, out DirX, out DirY);
        }

        (double StartPointX, double StartPointY) = Samples[0];
        (double NextPointX, double NextPointY) = Samples[1];
        return TryNormalize(NextPointX - StartPointX, NextPointY - StartPointY, out DirX, out DirY);
    }

    private static bool TryNormalize(
        double DeltaX,
        double DeltaY,
        out double UnitX,
        out double UnitY)
    {
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (Length < MIN_DIRECTION_LENGTH_PX)
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
