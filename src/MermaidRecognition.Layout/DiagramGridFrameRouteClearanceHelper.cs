using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Keeps orthogonal route segments clear of group frame borders (rulebook 4.6 / B8).
/// </summary>
internal static class DiagramGridFrameRouteClearanceHelper
{
    private const double BORDER_TOLERANCE_PX = 1.5;

    private const double MIN_OVERLAP_PX = 4.0;

    private const double CLEARANCE_PX = 16.0;

    /// <summary>
    /// Nudges interior route segments away from frame edges they hug.
    /// </summary>
    internal static void Apply(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        IReadOnlyList<DiagramGridLayout.GridFrame> Frames)
    {
        if (Contract == null || Layout == null || Frames.Count == 0)
        {
            return;
        }

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            for (int Index = 1; Index < Route.Points.Count; Index++)
            {
                FlowchartLayoutPoint Start = Route.Points[Index - 1];
                FlowchartLayoutPoint End = Route.Points[Index];
                double Length = Math.Sqrt(
                    ((End.X - Start.X) * (End.X - Start.X)) + ((End.Y - Start.Y) * (End.Y - Start.Y)));
                if (Length < MIN_OVERLAP_PX)
                {
                    continue;
                }

                bool Horizontal = Math.Abs(End.Y - Start.Y) <= BORDER_TOLERANCE_PX;
                bool Vertical = Math.Abs(End.X - Start.X) <= BORDER_TOLERANCE_PX;
                if (!Horizontal && !Vertical)
                {
                    continue;
                }

                foreach (DiagramGridLayout.GridFrame Frame in Frames)
                {
                    if (string.Equals(Frame.Id, Route.SourceId, StringComparison.Ordinal)
                        || string.Equals(Frame.Id, Route.TargetId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!SegmentHugsFrame(Start, End, Horizontal, Vertical, Frame, out double Nudge))
                    {
                        continue;
                    }

                    if (Horizontal)
                    {
                        Start.Y = Math.Round(Start.Y + Nudge);
                        End.Y = Math.Round(End.Y + Nudge);
                    }
                    else
                    {
                        Start.X = Math.Round(Start.X + Nudge);
                        End.X = Math.Round(End.X + Nudge);
                    }

                    Route.Points[Index - 1] = Start;
                    Route.Points[Index] = End;
                }
            }
        }
    }

    private static bool SegmentHugsFrame(
        FlowchartLayoutPoint Start,
        FlowchartLayoutPoint End,
        bool Horizontal,
        bool Vertical,
        DiagramGridLayout.GridFrame Frame,
        out double Nudge)
    {
        Nudge = 0.0;
        double Overlap;
        double Separation;
        if (Horizontal)
        {
            double Y = Start.Y;
            Overlap = AxisOverlap(Start.X, End.X, Frame.Left, Frame.Right);
            Separation = Math.Min(Math.Abs(Y - Frame.Top), Math.Abs(Y - Frame.Bottom));
            if (Overlap < MIN_OVERLAP_PX)
            {
                return false;
            }

            if (Separation > BORDER_TOLERANCE_PX && Separation >= CLEARANCE_PX)
            {
                return false;
            }

            double ToTop = Frame.Top - Y;
            double ToBottom = Y - Frame.Bottom;
            if (Math.Abs(ToTop) <= Math.Abs(ToBottom))
            {
                Nudge = CLEARANCE_PX - Math.Abs(ToTop);
                if (ToTop >= 0.0)
                {
                    Nudge = Math.Abs(Nudge);
                }
                else
                {
                    Nudge = -Math.Abs(Nudge);
                }
            }
            else
            {
                Nudge = CLEARANCE_PX - Math.Abs(ToBottom);
                if (ToBottom >= 0.0)
                {
                    Nudge = -Math.Abs(Nudge);
                }
                else
                {
                    Nudge = Math.Abs(Nudge);
                }
            }

            return Math.Abs(Nudge) > 0.5;
        }

        if (!Vertical)
        {
            return false;
        }

        double X = Start.X;
        Overlap = AxisOverlap(Start.Y, End.Y, Frame.Bottom, Frame.Top);
        Separation = Math.Min(Math.Abs(X - Frame.Left), Math.Abs(X - Frame.Right));
        if (Overlap < MIN_OVERLAP_PX)
        {
            return false;
        }

        if (Separation > BORDER_TOLERANCE_PX && Separation >= CLEARANCE_PX)
        {
            return false;
        }

        double ToLeft = X - Frame.Left;
        double ToRight = Frame.Right - X;
        if (Math.Abs(ToLeft) <= Math.Abs(ToRight))
        {
            Nudge = CLEARANCE_PX - Math.Abs(ToLeft);
            Nudge = ToLeft >= 0.0 ? -Math.Abs(Nudge) : Math.Abs(Nudge);
        }
        else
        {
            Nudge = CLEARANCE_PX - Math.Abs(ToRight);
            Nudge = ToRight >= 0.0 ? Math.Abs(Nudge) : -Math.Abs(Nudge);
        }

        return Math.Abs(Nudge) > 0.5;
    }

    private static double AxisOverlap(double Start, double End, double BoxStart, double BoxEnd)
    {
        double Left = Math.Min(Start, End);
        double Right = Math.Max(Start, End);
        return Math.Max(0.0, Math.Min(Right, BoxEnd) - Math.Max(Left, BoxStart));
    }
}
