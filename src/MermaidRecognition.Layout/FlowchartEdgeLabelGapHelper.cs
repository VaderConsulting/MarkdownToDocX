using System;
using System.Collections.Generic;
using System.Globalization;

using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Rule B13: edge labels sit centred on a straight run with an even character-width gap on each side.
/// </summary>
public static class FlowchartEdgeLabelGapHelper
{
    public const double LABEL_CHAR_WIDTH_EM = 0.512;

    private const double AXIS_ALIGNED_SEGMENT_EPSILON_PX = 0.5;

    public static double ComputeCharacterGapPx(double FontSizePx) =>
        FontSizePx * LABEL_CHAR_WIDTH_EM;

    public static double ComputeHalfGapWidthPx(FlowchartLabelDimensions LabelDimensions, double FontSizePx) =>
        (LabelDimensions.Width / 2.0) + ComputeCharacterGapPx(FontSizePx);

    public static double ComputeRequiredSegmentLengthPx(FlowchartLabelDimensions LabelDimensions, double FontSizePx) =>
        LabelDimensions.Width + (2.0 * ComputeCharacterGapPx(FontSizePx));

    public sealed record LabelSegmentBreak(
        int SegmentStartIndex,
        int SegmentEndIndex,
        double CentreX,
        double CentreY,
        double HalfGapWidthPx,
        bool Horizontal);

    public static bool TryResolveLabelSegmentBreak(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartLabelDimensions LabelDimensions,
        double FontSizePx,
        out LabelSegmentBreak Break)
    {
        Break = default!;
        if (Points.Count < 2)
        {
            return false;
        }

        double RequiredLength = ComputeRequiredSegmentLengthPx(LabelDimensions, FontSizePx);
        int BestStart = -1;
        int BestEnd = -1;
        double BestLength = 0.0;

        for (int Start = 0; Start < Points.Count - 1; Start++)
        {
            for (int End = Start + 1; End < Points.Count; End++)
            {
                if (!IsAxisAlignedRun(Points, Start, End, out bool Horizontal, out double Length))
                {
                    break;
                }

                double CornerClearance = Start > 0 || End < Points.Count - 1
                    ? 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX : 20;
                if (Length < RequiredLength + CornerClearance || Length <= BestLength)
                {
                    continue;
                }

                BestLength = Length;
                BestStart = Start;
                BestEnd = End;
            }
        }

        if (BestStart < 0)
        {
            return false;
        }

        (double StartX, double StartY) = Points[BestStart];
        (double EndX, double EndY) = Points[BestEnd];
        bool SegmentHorizontal = Math.Abs(StartY - EndY) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX;
        double HalfGap = ComputeHalfGapWidthPx(LabelDimensions, FontSizePx);
        Break = new LabelSegmentBreak(
            BestStart,
            BestEnd,
            (StartX + EndX) / 2.0,
            (StartY + EndY) / 2.0,
            HalfGap,
            SegmentHorizontal);
        return true;
    }

    public static bool TryComputeBrokenSegmentSpansForLabelCentre(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double LabelCentreX,
        double LabelCentreY,
        FlowchartLabelDimensions LabelDimensions,
        double FontSizePx,
        out (double X1, double Y1, double X2, double Y2) Before,
        out bool HasBefore,
        out (double X1, double Y1, double X2, double Y2) After,
        out bool HasAfter)
    {
        double HalfGapWidthPx = ComputeHalfGapWidthPx(LabelDimensions, FontSizePx);
        if (Math.Abs(StartY - EndY) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX)
        {
            double Left = Math.Min(StartX, EndX);
            double Right = Math.Max(StartX, EndX);
            double CentreX = Math.Clamp(LabelCentreX, Left, Right);
            return TryComputeBrokenSegmentSpans(
                StartX,
                StartY,
                EndX,
                EndY,
                CentreX,
                StartY,
                HalfGapWidthPx,
                out Before,
                out HasBefore,
                out After,
                out HasAfter);
        }

        if (Math.Abs(StartX - EndX) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX)
        {
            double Bottom = Math.Min(StartY, EndY);
            double Top = Math.Max(StartY, EndY);
            double CentreY = Math.Clamp(LabelCentreY, Bottom, Top);
            return TryComputeBrokenSegmentSpans(
                StartX,
                StartY,
                EndX,
                EndY,
                StartX,
                CentreY,
                HalfGapWidthPx,
                out Before,
                out HasBefore,
                out After,
                out HasAfter);
        }

        Before = default;
        After = default;
        HasBefore = false;
        HasAfter = false;
        return false;
    }

    public static bool TryComputeBrokenSegmentSpans(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double CentreX,
        double CentreY,
        double HalfGapWidthPx,
        out (double X1, double Y1, double X2, double Y2) Before,
        out bool HasBefore,
        out (double X1, double Y1, double X2, double Y2) After,
        out bool HasAfter)
    {
        Before = default;
        After = default;
        HasBefore = false;
        HasAfter = false;
        if (Math.Abs(StartY - EndY) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX)
        {
            double Left = Math.Min(StartX, EndX);
            double Right = Math.Max(StartX, EndX);
            double GapLeft = Math.Max(Left, CentreX - HalfGapWidthPx);
            double GapRight = Math.Min(Right, CentreX + HalfGapWidthPx);
            HasBefore = GapLeft - Left > 0.5;
            HasAfter = Right - GapRight > 0.5;
            if (!HasBefore && !HasAfter)
            {
                return false;
            }

            if (HasBefore)
            {
                Before = (Left, StartY, GapLeft, StartY);
            }

            if (HasAfter)
            {
                After = (GapRight, StartY, Right, StartY);
            }

            return true;
        }

        if (Math.Abs(StartX - EndX) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX)
        {
            double Bottom = Math.Min(StartY, EndY);
            double Top = Math.Max(StartY, EndY);
            double GapBottom = Math.Max(Bottom, CentreY - HalfGapWidthPx);
            double GapTop = Math.Min(Top, CentreY + HalfGapWidthPx);
            HasBefore = GapBottom - Bottom > 0.5;
            HasAfter = Top - GapTop > 0.5;
            if (!HasBefore && !HasAfter)
            {
                return false;
            }

            if (HasBefore)
            {
                Before = (StartX, Bottom, StartX, GapBottom);
            }

            if (HasAfter)
            {
                After = (StartX, GapTop, StartX, Top);
            }

            return true;
        }

        return false;
    }

    private static bool IsAxisAlignedRun(
        IReadOnlyList<(double X, double Y)> Points,
        int Start,
        int End,
        out bool Horizontal,
        out double Length)
    {
        Horizontal = false;
        Length = 0.0;
        bool? RunHorizontal = null;
        double Total = 0.0;
        for (int Index = Start + 1; Index <= End; Index++)
        {
            double DeltaX = Points[Index].X - Points[Index - 1].X;
            double DeltaY = Points[Index].Y - Points[Index - 1].Y;
            if (Math.Abs(DeltaX) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX
                && Math.Abs(DeltaY) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX)
            {
                continue;
            }

            bool SegmentHorizontal = Math.Abs(DeltaY) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX;
            bool SegmentVertical = Math.Abs(DeltaX) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX;
            if (!SegmentHorizontal && !SegmentVertical)
            {
                return false;
            }

            if (RunHorizontal is null)
            {
                RunHorizontal = SegmentHorizontal;
            }
            else if (RunHorizontal.Value != SegmentHorizontal)
            {
                return false;
            }

            Total += Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        if (RunHorizontal is null || Total <= 1e-6)
        {
            return false;
        }

        Horizontal = RunHorizontal.Value;
        Length = Total;
        return true;
    }
}
