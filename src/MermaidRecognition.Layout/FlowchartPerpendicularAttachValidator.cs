using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Validates that edge guide polylines meet node boundaries at right angles on both ends with visible terminal stubs.
/// </summary>
public static class FlowchartPerpendicularAttachValidator
{
    private const double FACE_TOLERANCE_PX = 2.5;

    private const double AXIS_TOLERANCE_PX = 0.75;

    private const double NORMAL_ALIGNMENT_TOLERANCE = 0.08;

    /// <summary>
    /// Returns true when the first and last segments of <paramref name="Points"/> are perpendicular
    /// to the joined faces on <paramref name="SourceBounds"/> and <paramref name="TargetBounds"/>.
    /// </summary>
    public static bool TryValidateGuidePoints(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        bool AllowDiagonalCrossEdge,
        out string FailureMessage) => TryValidateGuidePoints(
            Points,
            SourceBounds,
            null,
            TargetBounds,
            null,
            AllowDiagonalCrossEdge,
            out FailureMessage);

    /// <summary>
    /// Returns true when terminal segments are perpendicular to the joined face and meet minimum stub length.
    /// </summary>
    public static bool TryValidateGuidePoints(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        bool AllowDiagonalCrossEdge,
        out string FailureMessage)
    {
        FailureMessage = string.Empty;

        if (Points.Count < 2)
        {
            FailureMessage = "Route must contain at least two guide points.";
            return false;
        }

        if (Points.Count == 2)
        {
            if (AllowDiagonalCrossEdge
                || FlowchartEdgeConnectionGeometry.AreAttachPointsAxisAligned(
                    Points[0].X,
                    Points[0].Y,
                    Points[1].X,
                    Points[1].Y))
            {
                return true;
            }
        }

        (double SourceX, double SourceY) = Points[0];
        (double SecondX, double SecondY) = Points[1];
        if (!TryValidateTerminalSegment(
                SourceBounds,
                SourceShape,
                SourceX,
                SourceY,
                SecondX,
                SecondY,
                IsExit: true,
                out string SourceFailure))
        {
            FailureMessage = "Source attach: " + SourceFailure;
            return false;
        }

        (double TargetX, double TargetY) = Points[Points.Count - 1];
        (double PenultimateX, double PenultimateY) = Points[Points.Count - 2];
        if (!TryValidateTerminalSegment(
                TargetBounds,
                TargetShape,
                TargetX,
                TargetY,
                PenultimateX,
                PenultimateY,
                IsExit: false,
                out string TargetFailure))
        {
            FailureMessage = "Target attach: " + TargetFailure;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that flattened rendered samples meet node faces at right angles on both ends.
    /// </summary>
    public static bool TryValidateRenderedPathTerminals(
        IReadOnlyList<(double X, double Y)> FlattenedLayoutSamples,
        IReadOnlyList<(double X, double Y)> LayoutGuidePoints,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        out string FailureMessage)
    {
        FailureMessage = string.Empty;

        if (FlattenedLayoutSamples.Count < 2 || LayoutGuidePoints.Count < 2)
        {
            FailureMessage = "Rendered path must contain at least two samples.";
            return false;
        }

        if (!TryValidateGuidePoints(
                LayoutGuidePoints,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                AllowDiagonalCrossEdge: false,
                out string GuideFailure))
        {
            FailureMessage = "Layout guide attach: " + GuideFailure;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a unit direction along the rendered stroke within the terminal stub length at the given end.
    /// </summary>
    public static bool TryGetRenderedTerminalTangentDirection(
        IReadOnlyList<(double X, double Y)> Samples,
        FlowchartLayoutNodeBounds TerminalBounds,
        bool NearStart,
        out double DirX,
        out double DirY)
    {
        DirX = 0.0;
        DirY = 0.0;

        if (!TryResolveOuterSamplePoint(Samples, TerminalBounds, NearStart, out double OuterX, out double OuterY))
        {
            return false;
        }

        if (NearStart)
        {
            if (!TryNormalize(Samples[0].X - OuterX, Samples[0].Y - OuterY, out DirX, out DirY))
            {
                return false;
            }
        }
        else
        {
            int LastIndex = Samples.Count - 1;
            if (!TryNormalize(Samples[LastIndex].X - OuterX, Samples[LastIndex].Y - OuterY, out DirX, out DirY))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryNormalize(double DeltaX, double DeltaY, out double UnitX, out double UnitY)
    {
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (Length <= AXIS_TOLERANCE_PX)
        {
            UnitX = 0.0;
            UnitY = 0.0;
            return false;
        }

        UnitX = DeltaX / Length;
        UnitY = DeltaY / Length;
        return true;
    }

    private static bool TryResolveOuterSamplePoint(
        IReadOnlyList<(double X, double Y)> Samples,
        FlowchartLayoutNodeBounds TerminalBounds,
        bool NearStart,
        out double OuterX,
        out double OuterY)
    {
        OuterX = 0.0;
        OuterY = 0.0;

        double MinimumStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TerminalBounds);
        MinimumStubLength = Math.Max(MinimumStubLength, 6.0);

        if (NearStart)
        {
            double AccumulatedLength = 0.0;
            for (int PointIndex = 1; PointIndex < Samples.Count; PointIndex++)
            {
                (double StartX, double StartY) = Samples[PointIndex - 1];
                (double EndX, double EndY) = Samples[PointIndex];
                double SegmentLength = Math.Sqrt(
                    ((EndX - StartX) * (EndX - StartX))
                    + ((EndY - StartY) * (EndY - StartY)));
                if (AccumulatedLength + SegmentLength >= MinimumStubLength)
                {
                    double Remaining = MinimumStubLength - AccumulatedLength;
                    double Ratio = SegmentLength <= 1e-6 ? 0.0 : Remaining / SegmentLength;
                    OuterX = StartX + ((EndX - StartX) * Ratio);
                    OuterY = StartY + ((EndY - StartY) * Ratio);
                    return true;
                }

                AccumulatedLength += SegmentLength;
            }

            if (Samples.Count >= 2)
            {
                OuterX = Samples[1].X;
                OuterY = Samples[1].Y;
                return true;
            }

            return false;
        }

        double ReverseAccumulatedLength = 0.0;
        for (int PointIndex = Samples.Count - 1; PointIndex > 0; PointIndex--)
        {
            (double EndX, double EndY) = Samples[PointIndex];
            (double StartX, double StartY) = Samples[PointIndex - 1];
            double SegmentLength = Math.Sqrt(
                ((EndX - StartX) * (EndX - StartX))
                + ((EndY - StartY) * (EndY - StartY)));
            if (ReverseAccumulatedLength + SegmentLength >= MinimumStubLength)
            {
                double Remaining = MinimumStubLength - ReverseAccumulatedLength;
                double Ratio = SegmentLength <= 1e-6 ? 0.0 : Remaining / SegmentLength;
                OuterX = EndX + ((StartX - EndX) * Ratio);
                OuterY = EndY + ((StartY - EndY) * Ratio);
                return true;
            }

            ReverseAccumulatedLength += SegmentLength;
        }

        if (Samples.Count >= 2)
        {
            OuterX = Samples[Samples.Count - 2].X;
            OuterY = Samples[Samples.Count - 2].Y;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves which face of the clip box the attach coordinate lies on (MSAGL Y-up layout space).
    /// </summary>
    public static FlowchartEdgeAttachFace ResolveAttachFace(
        FlowchartLayoutNodeBounds Bounds,
        double AttachX,
        double AttachY)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);

        bool NearLeft = Math.Abs(AttachX - Left) <= FACE_TOLERANCE_PX;
        bool NearRight = Math.Abs(AttachX - Right) <= FACE_TOLERANCE_PX;
        bool NearBottom = Math.Abs(AttachY - Bottom) <= FACE_TOLERANCE_PX;
        bool NearTop = Math.Abs(AttachY - Top) <= FACE_TOLERANCE_PX;

        if (NearTop && !NearBottom)
        {
            return FlowchartEdgeAttachFace.Top;
        }

        if (NearBottom && !NearTop)
        {
            return FlowchartEdgeAttachFace.Bottom;
        }

        if (NearLeft && !NearRight)
        {
            return FlowchartEdgeAttachFace.Left;
        }

        if (NearRight && !NearLeft)
        {
            return FlowchartEdgeAttachFace.Right;
        }

        return FlowchartEdgeAttachFace.Unknown;
    }

    private static bool TryValidateTerminalSegment(
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double BoundaryX,
        double BoundaryY,
        double OuterX,
        double OuterY,
        bool IsExit,
        out string FailureMessage)
    {
        FailureMessage = string.Empty;
        double DeltaX = OuterX - BoundaryX;
        double DeltaY = OuterY - BoundaryY;
        double SegmentLength = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));

        if (SegmentLength <= AXIS_TOLERANCE_PX)
        {
            FailureMessage = "Terminal segment is degenerate.";
            return false;
        }

        double MinimumStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(Bounds);
        if (SegmentLength + AXIS_TOLERANCE_PX < MinimumStubLength)
        {
            FailureMessage = "Terminal segment is shorter than the minimum stub length (" + MinimumStubLength.ToString("F1") + " px).";
            return false;
        }

        double SegmentDirX;
        double SegmentDirY;
        if (IsExit)
        {
            SegmentDirX = DeltaX / SegmentLength;
            SegmentDirY = DeltaY / SegmentLength;
        }
        else
        {
            SegmentDirX = (BoundaryX - OuterX) / SegmentLength;
            SegmentDirY = (BoundaryY - OuterY) / SegmentLength;
        }

        if (FlowchartDiamondAttachHelper.IsDiamondShape(Shape)
            && FlowchartDiamondAttachHelper.TryResolveOutwardNormal(
                Bounds,
                BoundaryX,
                BoundaryY,
                out double DiamondNormalX,
                out double DiamondNormalY))
        {
            double Alignment = (SegmentDirX * DiamondNormalX) + (SegmentDirY * DiamondNormalY);
            double ExpectedAlignment = IsExit ? 1.0 : -1.0;
            if (Math.Abs(Alignment - ExpectedAlignment) > NORMAL_ALIGNMENT_TOLERANCE)
            {
                FailureMessage = "Expected a straight stub perpendicular to the diamond edge normal.";
                return false;
            }

            return true;
        }

        FlowchartEdgeAttachFace Face = ResolveAttachFace(Bounds, BoundaryX, BoundaryY);
        if (IsExit)
        {
            if (!IsSegmentPerpendicularToFace(Face, BoundaryX, BoundaryY, OuterX, OuterY, out string FaceFailure))
            {
                FailureMessage = FaceFailure;
                return false;
            }
        }
        else if (!IsSegmentPerpendicularToFace(Face, OuterX, OuterY, BoundaryX, BoundaryY, out string FaceFailure))
        {
            FailureMessage = FaceFailure;
            return false;
        }

        return true;
    }

    private static bool IsSegmentPerpendicularToFace(
        FlowchartEdgeAttachFace Face,
        double FromX,
        double FromY,
        double ToX,
        double ToY,
        out string FailureMessage)
    {
        FailureMessage = string.Empty;
        double DeltaX = Math.Abs(ToX - FromX);
        double DeltaY = Math.Abs(ToY - FromY);

        if (DeltaX <= AXIS_TOLERANCE_PX && DeltaY <= AXIS_TOLERANCE_PX)
        {
            FailureMessage = "Adjacent segment is degenerate.";
            return false;
        }

        switch (Face)
        {
            case FlowchartEdgeAttachFace.Top:
            case FlowchartEdgeAttachFace.Bottom:
                if (DeltaX > AXIS_TOLERANCE_PX)
                {
                    FailureMessage = "Expected a vertical stub on a horizontal face.";
                    return false;
                }

                return true;

            case FlowchartEdgeAttachFace.Left:
            case FlowchartEdgeAttachFace.Right:
                if (DeltaY > AXIS_TOLERANCE_PX)
                {
                    FailureMessage = "Expected a horizontal stub on a vertical face.";
                    return false;
                }

                return true;

            default:
                if ((DeltaX <= AXIS_TOLERANCE_PX) || (DeltaY <= AXIS_TOLERANCE_PX))
                {
                    return true;
                }

                FailureMessage = "Attach face is ambiguous and the segment is diagonal.";
                return false;
        }
    }
}
