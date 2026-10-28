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
        ComputeHalfGapPx(LabelDimensions, FontSizePx, Horizontal: true);

    public static double ComputeHalfGapPx(FlowchartLabelDimensions LabelDimensions, double FontSizePx, bool Horizontal) =>
        ((Horizontal ? LabelDimensions.Width
            : FlowchartEdgeLabelPlacementHelper.ComputeRenderedEdgeLabelHeight(LabelDimensions, FontSizePx)) / 2.0)
        + ComputeCharacterGapPx(FontSizePx);

    public static double ComputeRequiredSegmentLengthPx(
        FlowchartLabelDimensions LabelDimensions, double FontSizePx, bool Horizontal = true) =>
        2.0 * ComputeHalfGapPx(LabelDimensions, FontSizePx, Horizontal);

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
                double RequiredLength = ComputeRequiredSegmentLengthPx(LabelDimensions, FontSizePx, Horizontal);
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
            if (!TryResolveDiagonalStraightBreak(Points, LabelDimensions, FontSizePx, out Break))
            {
                return false;
            }

            return true;
        }

        (double StartX, double StartY) = Points[BestStart];
        (double EndX, double EndY) = Points[BestEnd];
        bool SegmentHorizontal = Math.Abs(StartY - EndY) <= AXIS_ALIGNED_SEGMENT_EPSILON_PX;
        double HalfGap = ComputeHalfGapPx(LabelDimensions, FontSizePx, SegmentHorizontal);
        Break = new LabelSegmentBreak(
            BestStart,
            BestEnd,
            (StartX + EndX) / 2.0,
            (StartY + EndY) / 2.0,
            HalfGap,
            SegmentHorizontal);
        return true;
    }

    private static bool TryResolveDiagonalStraightBreak(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartLabelDimensions LabelDimensions,
        double FontSizePx,
        out LabelSegmentBreak Break)
    {
        Break = default!;
        if (!TryFindLongestCollinearRun(Points, out int Start, out int End, out double Length))
        {
            return false;
        }

        (double StartX, double StartY) = Points[Start];
        (double EndX, double EndY) = Points[End];
        if (IsAxisAlignedRun(Points, Start, End, out bool Horizontal, out _))
        {
            return false;
        }

        double HalfGap = ComputeHalfGapAlongLinePx(LabelDimensions, FontSizePx);
        double RequiredLength = (2.0 * HalfGap) + (2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        if (Length < RequiredLength)
        {
            return false;
        }

        Break = new LabelSegmentBreak(
            Start,
            End,
            (StartX + EndX) / 2.0,
            (StartY + EndY) / 2.0,
            HalfGap,
            Horizontal: false);
        return true;
    }

    private static double ComputeHalfGapAlongLinePx(FlowchartLabelDimensions LabelDimensions, double FontSizePx) =>
        (LabelDimensions.Width / 2.0) + ComputeCharacterGapPx(FontSizePx);

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
                ComputeHalfGapPx(LabelDimensions, FontSizePx, Horizontal: true),
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
                ComputeHalfGapPx(LabelDimensions, FontSizePx, Horizontal: false),
                out Before,
                out HasBefore,
                out After,
                out HasAfter);
        }

        return TryComputeBrokenDiagonalSegmentSpans(
            StartX,
            StartY,
            EndX,
            EndY,
            LabelCentreX,
            LabelCentreY,
            ComputeHalfGapAlongLinePx(LabelDimensions, FontSizePx),
            out Before,
            out HasBefore,
            out After,
            out HasAfter);
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

        return TryComputeBrokenDiagonalSegmentSpans(
            StartX,
            StartY,
            EndX,
            EndY,
            CentreX,
            CentreY,
            HalfGapWidthPx,
            out Before,
            out HasBefore,
            out After,
            out HasAfter);
    }

    private static bool TryComputeBrokenDiagonalSegmentSpans(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double CentreX,
        double CentreY,
        double HalfGapAlongLinePx,
        out (double X1, double Y1, double X2, double Y2) Before,
        out bool HasBefore,
        out (double X1, double Y1, double X2, double Y2) After,
        out bool HasAfter)
    {
        Before = default;
        After = default;
        HasBefore = false;
        HasAfter = false;
        double DeltaX = EndX - StartX;
        double DeltaY = EndY - StartY;
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (Length <= 1e-6 || HalfGapAlongLinePx <= 0.0)
        {
            return false;
        }

        double UnitX = DeltaX / Length;
        double UnitY = DeltaY / Length;
        double CentreT = ((CentreX - StartX) * UnitX) + ((CentreY - StartY) * UnitY);
        CentreT = Math.Clamp(CentreT, HalfGapAlongLinePx, Length - HalfGapAlongLinePx);
        double GapStartT = CentreT - HalfGapAlongLinePx;
        double GapEndT = CentreT + HalfGapAlongLinePx;
        HasBefore = GapStartT > 0.5;
        HasAfter = Length - GapEndT > 0.5;
        if (!HasBefore && !HasAfter)
        {
            return false;
        }

        if (HasBefore)
        {
            Before = (StartX, StartY, StartX + (UnitX * GapStartT), StartY + (UnitY * GapStartT));
        }

        if (HasAfter)
        {
            After = (StartX + (UnitX * GapEndT), StartY + (UnitY * GapEndT), EndX, EndY);
        }

        return true;
    }

    private static bool TryFindLongestCollinearRun(
        IReadOnlyList<(double X, double Y)> Points,
        out int BestStart,
        out int BestEnd,
        out double BestLength)
    {
        BestStart = -1;
        BestEnd = -1;
        BestLength = 0.0;
        for (int Start = 0; Start < Points.Count - 1; Start++)
        {
            for (int End = Start + 1; End < Points.Count; End++)
            {
                if (!IsCollinearRun(Points, Start, End, out double Length))
                {
                    break;
                }

                if (Length > BestLength)
                {
                    BestLength = Length;
                    BestStart = Start;
                    BestEnd = End;
                }
            }
        }

        return BestStart >= 0;
    }

    private static bool IsCollinearRun(
        IReadOnlyList<(double X, double Y)> Points,
        int Start,
        int End,
        out double Length)
    {
        Length = 0.0;
        (double Ax, double Ay) = Points[Start];
        (double Bx, double By) = Points[End];
        double RunDeltaX = Bx - Ax;
        double RunDeltaY = By - Ay;
        double RunLength = Math.Sqrt((RunDeltaX * RunDeltaX) + (RunDeltaY * RunDeltaY));
        if (RunLength <= 1e-6)
        {
            return false;
        }

        for (int Index = Start + 1; Index < End; Index++)
        {
            double Cross = Math.Abs(((Points[Index].X - Ax) * RunDeltaY) - ((Points[Index].Y - Ay) * RunDeltaX));
            if (Cross / RunLength > AXIS_ALIGNED_SEGMENT_EPSILON_PX)
            {
                return false;
            }
        }

        Length = RunLength;
        return true;
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
