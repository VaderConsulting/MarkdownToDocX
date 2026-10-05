using System;
using System.Globalization;
using System.Text;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout;

/// <summary>
/// Turns an adopted MSAGL curve into SVG path data in a Y-down view.
/// </summary>
public static class AdoptedMsaglCurveToSvg
{
    /// <summary>
    /// Builds path data for <paramref name="Curve"/>.
    /// </summary>
    /// <param name="Curve">Routed edge curve.</param>
    /// <param name="ToSvg">Maps a layout point into SVG coordinates.</param>
    /// <returns>SVG path <c>d</c> attribute, or empty when the curve is missing.</returns>
    public static string ToPathData(ICurve? Curve, Func<Point, Point> ToSvg)
    {
        if (Curve == null)
        {
            return string.Empty;
        }

        StringBuilder Builder = new StringBuilder();
        AppendCurve(Builder, Curve, ToSvg, MoveFirst: true);
        return Builder.ToString();
    }

    private static void AppendCurve(StringBuilder Builder, ICurve Curve, Func<Point, Point> ToSvg, bool MoveFirst)
    {
        if (Curve is LineSegment Line)
        {
            AppendMoveOrLine(Builder, ToSvg(Line.Start), MoveFirst);
            AppendLine(Builder, ToSvg(Line.End));
            return;
        }

        if (Curve is CubicBezierSegment Bezier)
        {
            AppendMoveOrLine(Builder, ToSvg(Bezier.B(0)), MoveFirst);
            Point ControlOne = ToSvg(Bezier.B(1));
            Point ControlTwo = ToSvg(Bezier.B(2));
            Point End = ToSvg(Bezier.B(3));
            Builder.Append("C");
            AppendNumber(Builder, ControlOne.X);
            Builder.Append(',');
            AppendNumber(Builder, ControlOne.Y);
            Builder.Append(' ');
            AppendNumber(Builder, ControlTwo.X);
            Builder.Append(',');
            AppendNumber(Builder, ControlTwo.Y);
            Builder.Append(' ');
            AppendNumber(Builder, End.X);
            Builder.Append(',');
            AppendNumber(Builder, End.Y);
            return;
        }

        if (Curve is Polyline Poly)
        {
            bool First = MoveFirst;
            foreach (Point LayoutPoint in Poly)
            {
                Point SvgPoint = ToSvg(LayoutPoint);
                if (First)
                {
                    AppendMoveOrLine(Builder, SvgPoint, MoveFirst: true);
                    First = false;
                }
                else
                {
                    AppendLine(Builder, SvgPoint);
                }
            }

            return;
        }

        if (Curve is Curve Compound)
        {
            bool First = MoveFirst;
            foreach (ICurve Segment in Compound.Segments)
            {
                AppendCurve(Builder, Segment, ToSvg, First);
                First = false;
            }

            return;
        }

        if (Curve is RoundedRect Rounded)
        {
            AppendCurve(Builder, Rounded.Curve, ToSvg, MoveFirst);
            return;
        }

        AppendSampledCurve(Builder, Curve, ToSvg, MoveFirst);
    }

    private static void AppendSampledCurve(StringBuilder Builder, ICurve Curve, Func<Point, Point> ToSvg, bool MoveFirst)
    {
        const int SAMPLE_COUNT = 24;
        double Start = Curve.ParStart;
        double Span = Curve.ParEnd - Curve.ParStart;
        if (Math.Abs(Span) < 0.000001)
        {
            AppendMoveOrLine(Builder, ToSvg(Curve.Start), MoveFirst);
            AppendLine(Builder, ToSvg(Curve.End));
            return;
        }

        for (int Index = 0; Index <= SAMPLE_COUNT; Index++)
        {
            Point SvgPoint = ToSvg(Curve[Start + (Span * Index / SAMPLE_COUNT)]);
            if (Index == 0)
            {
                AppendMoveOrLine(Builder, SvgPoint, MoveFirst);
            }
            else
            {
                AppendLine(Builder, SvgPoint);
            }
        }
    }

    private static void AppendMoveOrLine(StringBuilder Builder, Point Point, bool MoveFirst)
    {
        Builder.Append(MoveFirst ? "M" : "L");
        AppendNumber(Builder, Point.X);
        Builder.Append(',');
        AppendNumber(Builder, Point.Y);
    }

    private static void AppendLine(StringBuilder Builder, Point Point)
    {
        Builder.Append('L');
        AppendNumber(Builder, Point.X);
        Builder.Append(',');
        AppendNumber(Builder, Point.Y);
    }

    private static void AppendNumber(StringBuilder Builder, double Value)
    {
        Builder.Append(Value.ToString("0.###", CultureInfo.InvariantCulture));
    }
}
