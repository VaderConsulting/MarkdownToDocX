using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Layout;

/// <summary>
/// Inserts a semicircular hop where two edge strokes cross at a right angle.
/// One implementation, applied to every edge the shared flowchart SVG renderer emits.
/// </summary>
public static class FlowchartSvgLineJump
{
    private const double HOP_RADIUS_PX = 6.0;

    private const double MIN_CLEAR_PX = 8.0;

    private static readonly Regex EdgeRegex = new Regex(
        "<(?:path|line)\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AttributeRegex = new Regex(
        "([a-zA-Z_:][\\w:.-]*)=\"([^\"]*)\"",
        RegexOptions.CultureInvariant);

    private static List<DrawnJump> _LastJumps = [];

    /// <summary>
    /// Hops drawn by the most recent <see cref="Apply"/> call.
    /// </summary>
    public static IReadOnlyList<DrawnJump> LastJumps
    {
        get
        {
            return _LastJumps;
        }
    }

    /// <summary>
    /// Rewrites <paramref name="Svg"/> so each genuine perpendicular crossing carries one hop.
    /// </summary>
    /// <param name="Svg">Rendered SVG.</param>
    /// <param name="InsertHops">False for grid routes, which retain plain right-angle crossings.</param>
    /// <returns>SVG with hop arcs inserted.</returns>
    public static string Apply(string Svg, bool InsertHops = true)
    {
        _LastJumps = [];
        if (!InsertHops || string.IsNullOrWhiteSpace(Svg))
        {
            return Svg;
        }

        MatchCollection Matches = EdgeRegex.Matches(Svg);
        List<EdgeStroke> Strokes = [];
        foreach (Match Match in Matches)
        {
            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!Attributes.TryGetValue("data-edge-source", out string? SourceId)
                || !Attributes.TryGetValue("data-edge-target", out string? TargetId))
            {
                continue;
            }

            if (!TryReadPoints(Attributes, out List<(double X, double Y)> Points) || Points.Count < 2)
            {
                continue;
            }

            Strokes.Add(new EdgeStroke(Match.Index, Match.Length, Match.Value, Attributes, SourceId, TargetId, Points));
        }

        List<Crossing> Crossings = FindCrossings(Strokes);
        if (Crossings.Count == 0)
        {
            return Svg;
        }

        Dictionary<int, List<Crossing>> ByHopper = [];
        foreach (Crossing Crossing in Crossings)
        {
            if (!ByHopper.TryGetValue(Crossing.Hopper, out List<Crossing>? List))
            {
                List = [];
                ByHopper[Crossing.Hopper] = List;
            }

            List.Add(Crossing);
        }

        StringBuilder Builder = new StringBuilder(Svg.Length + (Crossings.Count * 48));
        int Cursor = 0;
        for (int Index = 0; Index < Strokes.Count; Index++)
        {
            EdgeStroke Stroke = Strokes[Index];
            Builder.Append(Svg, Cursor, Stroke.Index - Cursor);
            Cursor = Stroke.Index + Stroke.Length;
            if (!ByHopper.TryGetValue(Index, out List<Crossing>? Hops))
            {
                Builder.Append(Stroke.Markup);
                continue;
            }

            Builder.Append(RewriteWithHops(Stroke, Hops));
        }

        Builder.Append(Svg, Cursor, Svg.Length - Cursor);
        return Builder.ToString();
    }

    private static List<Crossing> FindCrossings(List<EdgeStroke> Strokes)
    {
        List<Crossing> Crossings = [];
        for (int LeftIndex = 0; LeftIndex < Strokes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Strokes.Count; RightIndex++)
            {
                EdgeStroke Left = Strokes[LeftIndex];
                EdgeStroke Right = Strokes[RightIndex];
                for (int LeftSegment = 1; LeftSegment < Left.Points.Count; LeftSegment++)
                {
                    (double AX, double AY) = Left.Points[LeftSegment - 1];
                    (double BX, double BY) = Left.Points[LeftSegment];
                    if (!IsAxisSegment(AX, AY, BX, BY, out bool LeftHorizontal))
                    {
                        continue;
                    }

                    for (int RightSegment = 1; RightSegment < Right.Points.Count; RightSegment++)
                    {
                        (double CX, double CY) = Right.Points[RightSegment - 1];
                        (double DX, double DY) = Right.Points[RightSegment];
                        if (!IsAxisSegment(CX, CY, DX, DY, out bool RightHorizontal) || LeftHorizontal == RightHorizontal)
                        {
                            continue;
                        }

                        if (!TryIntersect(AX, AY, BX, BY, CX, CY, DX, DY, out double CrossX, out double CrossY, out double Ua, out double Ub))
                        {
                            continue;
                        }

                        if (Ua <= 0.08 || Ua >= 0.92 || Ub <= 0.08 || Ub >= 0.92)
                        {
                            continue;
                        }

                        if (Distance(CrossX, CrossY, AX, AY) < MIN_CLEAR_PX + HOP_RADIUS_PX
                            || Distance(CrossX, CrossY, BX, BY) < MIN_CLEAR_PX + HOP_RADIUS_PX
                            || Distance(CrossX, CrossY, CX, CY) < MIN_CLEAR_PX + HOP_RADIUS_PX
                            || Distance(CrossX, CrossY, DX, DY) < MIN_CLEAR_PX + HOP_RADIUS_PX)
                        {
                            continue;
                        }

                        int Hopper = RightIndex;
                        int HopSegment = RightSegment;
                        string Reason = FindReason(Left, Right);
                        Crossings.Add(new Crossing(Hopper, HopSegment, CrossX, CrossY));
                        _LastJumps.Add(new DrawnJump(
                            Right.SourceId,
                            Right.TargetId,
                            Left.SourceId,
                            Left.TargetId,
                            CrossX,
                            CrossY,
                            Reason));
                    }
                }
            }
        }

        return Crossings;
    }

    private static string FindReason(EdgeStroke Left, EdgeStroke Right)
    {
        foreach (FlowchartLayoutQualityFailure Justification in FlowchartSharedEdgeRouter.LastJustifications)
        {
            if (!string.Equals(Justification.Kind, "EdgeCrossing", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(Justification.Justification))
            {
                continue;
            }

            if (Involves(Justification, Left.SourceId, Left.TargetId)
                && Involves(Justification, Right.SourceId, Right.TargetId))
            {
                return Justification.Justification;
            }
        }

        return "right-angle fallback: "
            + Right.SourceId
            + "->"
            + Right.TargetId
            + " crosses "
            + Left.SourceId
            + "->"
            + Left.TargetId
            + " because no failure-free route was recorded for this perpendicular crossing";
    }

    private static bool Involves(FlowchartLayoutQualityFailure Justification, string SourceId, string TargetId)
    {
        bool Source = false;
        bool Target = false;
        foreach (string Id in Justification.InvolvedIds)
        {
            if (string.Equals(Id, SourceId, StringComparison.Ordinal))
            {
                Source = true;
            }

            if (string.Equals(Id, TargetId, StringComparison.Ordinal))
            {
                Target = true;
            }
        }

        return Source && Target;
    }

    private static string RewriteWithHops(EdgeStroke Stroke, List<Crossing> Hops)
    {
        List<(double X, double Y)> Points = Stroke.Points;
        StringBuilder Data = new StringBuilder();
        Data.Append("M");
        Data.Append(Format(Points[0].X));
        Data.Append(',');
        Data.Append(Format(Points[0].Y));
        for (int Segment = 1; Segment < Points.Count; Segment++)
        {
            (double X1, double Y1) = Points[Segment - 1];
            (double X2, double Y2) = Points[Segment];
            List<Crossing> OnSegment = [];
            foreach (Crossing Hop in Hops)
            {
                if (Hop.Segment == Segment)
                {
                    OnSegment.Add(Hop);
                }
            }

            OnSegment.Sort((Left, Right) =>
                Distance(X1, Y1, Left.X, Left.Y).CompareTo(Distance(X1, Y1, Right.X, Right.Y)));
            double CursorX = X1;
            double CursorY = Y1;
            double Previous = -1.0;
            foreach (Crossing Hop in OnSegment)
            {
                double Along = Distance(X1, Y1, Hop.X, Hop.Y);
                if (Along - Previous < (HOP_RADIUS_PX * 2.0))
                {
                    continue;
                }

                Previous = Along;
                AppendHop(Data, CursorX, CursorY, X2, Y2, Hop.X, Hop.Y);
                double DirectionX = X2 - X1;
                double DirectionY = Y2 - Y1;
                double Length = Math.Sqrt((DirectionX * DirectionX) + (DirectionY * DirectionY));
                DirectionX /= Length;
                DirectionY /= Length;
                CursorX = Hop.X + (DirectionX * HOP_RADIUS_PX);
                CursorY = Hop.Y + (DirectionY * HOP_RADIUS_PX);
            }

            Data.Append('L');
            Data.Append(Format(X2));
            Data.Append(',');
            Data.Append(Format(Y2));
        }

        return BuildPathTag(Stroke, Data.ToString());
    }

    private static void AppendHop(
        StringBuilder Data,
        double FromX,
        double FromY,
        double ToX,
        double ToY,
        double CrossX,
        double CrossY)
    {
        double DirectionX = ToX - FromX;
        double DirectionY = ToY - FromY;
        double Length = Math.Sqrt((DirectionX * DirectionX) + (DirectionY * DirectionY));
        if (Length < 1.0)
        {
            return;
        }

        DirectionX /= Length;
        DirectionY /= Length;
        double StartX = CrossX - (DirectionX * HOP_RADIUS_PX);
        double StartY = CrossY - (DirectionY * HOP_RADIUS_PX);
        double EndX = CrossX + (DirectionX * HOP_RADIUS_PX);
        double EndY = CrossY + (DirectionY * HOP_RADIUS_PX);
        double PerpX = DirectionY;
        double PerpY = -DirectionX;
        double BulgeX = CrossX + (PerpX * HOP_RADIUS_PX);
        double BulgeY = CrossY + (PerpY * HOP_RADIUS_PX);
        double Cross = ((EndX - StartX) * (BulgeY - StartY)) - ((EndY - StartY) * (BulgeX - StartX));
        int Sweep = Cross < 0.0 ? 1 : 0;
        Data.Append('L');
        Data.Append(Format(StartX));
        Data.Append(',');
        Data.Append(Format(StartY));
        Data.Append("A");
        Data.Append(Format(HOP_RADIUS_PX));
        Data.Append(',');
        Data.Append(Format(HOP_RADIUS_PX));
        Data.Append(" 0 0 ");
        Data.Append(Sweep.ToString(CultureInfo.InvariantCulture));
        Data.Append(' ');
        Data.Append(Format(EndX));
        Data.Append(',');
        Data.Append(Format(EndY));
    }

    private static string BuildPathTag(EdgeStroke Stroke, string PathData)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.Append("<path d=\"");
        Builder.Append(PathData);
        Builder.Append("\" fill=\"none\"");
        AppendAttr(Builder, Stroke.Attributes, "stroke", "#333333");
        AppendAttr(Builder, Stroke.Attributes, "stroke-width", "1");
        Builder.Append(" stroke-linejoin=\"round\" stroke-linecap=\"round\"");
        Builder.Append(" data-edge-source=\"");
        Builder.Append(Escape(Stroke.SourceId));
        Builder.Append("\" data-edge-target=\"");
        Builder.Append(Escape(Stroke.TargetId));
        Builder.Append('"');
        if (Stroke.Attributes.TryGetValue("stroke-dasharray", out string? Dash))
        {
            Builder.Append(" stroke-dasharray=\"");
            Builder.Append(Escape(Dash));
            Builder.Append('"');
        }

        Builder.Append("/>");
        return Builder.ToString();
    }

    private static void AppendAttr(StringBuilder Builder, Dictionary<string, string> Attributes, string Name, string Fallback)
    {
        string Value = Attributes.TryGetValue(Name, out string? Found) ? Found : Fallback;
        Builder.Append(' ');
        Builder.Append(Name);
        Builder.Append("=\"");
        Builder.Append(Escape(Value));
        Builder.Append('"');
    }

    private static bool TryReadPoints(Dictionary<string, string> Attributes, out List<(double X, double Y)> Points)
    {
        Points = [];
        if (Attributes.TryGetValue("d", out string? PathData)
            && FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out Points)
            && Points.Count >= 2)
        {
            return true;
        }

        if (Attributes.TryGetValue("x1", out string? X1Text)
            && Attributes.TryGetValue("y1", out string? Y1Text)
            && Attributes.TryGetValue("x2", out string? X2Text)
            && Attributes.TryGetValue("y2", out string? Y2Text)
            && TryParse(X1Text, out double X1)
            && TryParse(Y1Text, out double Y1)
            && TryParse(X2Text, out double X2)
            && TryParse(Y2Text, out double Y2))
        {
            Points = [(X1, Y1), (X2, Y2)];
            return true;
        }

        return false;
    }

    private static bool IsAxisSegment(double X1, double Y1, double X2, double Y2, out bool Horizontal)
    {
        Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) >= 16.0;
        bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) >= 16.0;
        return Horizontal || Vertical;
    }

    private static bool TryIntersect(
        double AX,
        double AY,
        double BX,
        double BY,
        double CX,
        double CY,
        double DX,
        double DY,
        out double CrossX,
        out double CrossY,
        out double Ua,
        out double Ub)
    {
        CrossX = 0.0;
        CrossY = 0.0;
        Ua = 0.0;
        Ub = 0.0;
        double Denominator = ((BX - AX) * (DY - CY)) - ((BY - AY) * (DX - CX));
        if (Math.Abs(Denominator) < 1e-6)
        {
            return false;
        }

        Ua = (((CX - AX) * (DY - CY)) - ((CY - AY) * (DX - CX))) / Denominator;
        Ub = (((CX - AX) * (BY - AY)) - ((CY - AY) * (BX - AX))) / Denominator;
        if (Ua <= 0.02 || Ua >= 0.98 || Ub <= 0.02 || Ub >= 0.98)
        {
            return false;
        }

        CrossX = AX + (Ua * (BX - AX));
        CrossY = AY + (Ua * (BY - AY));
        return true;
    }

    private static Dictionary<string, string> ParseAttributes(string Tag)
    {
        Dictionary<string, string> Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match Match in AttributeRegex.Matches(Tag))
        {
            Attributes[Match.Groups[1].Value] = Match.Groups[2].Value;
        }

        return Attributes;
    }

    private static bool TryParse(string Text, out double Value)
    {
        return double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out Value);
    }

    private static string Format(double Value)
    {
        return Value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Escape(string Value)
    {
        return Value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static double Distance(double X1, double Y1, double X2, double Y2)
    {
        return Math.Sqrt(Math.Pow(X1 - X2, 2.0) + Math.Pow(Y1 - Y2, 2.0));
    }

    /// <summary>
    /// One hop drawn over a perpendicular crossing.
    /// </summary>
    /// <param name="HopperSource">Source of the edge that hops.</param>
    /// <param name="HopperTarget">Target of the edge that hops.</param>
    /// <param name="CrossedSource">Source of the edge underneath.</param>
    /// <param name="CrossedTarget">Target of the edge underneath.</param>
    /// <param name="X">Crossing X in SVG coordinates.</param>
    /// <param name="Y">Crossing Y in SVG coordinates.</param>
    /// <param name="Reason">Why no clean route was used.</param>
    public sealed class DrawnJump(
        string HopperSource,
        string HopperTarget,
        string CrossedSource,
        string CrossedTarget,
        double X,
        double Y,
        string Reason)
    {
        /// <summary>Source of the edge that hops.</summary>
        public string HopperSource { get; } = HopperSource;

        /// <summary>Target of the edge that hops.</summary>
        public string HopperTarget { get; } = HopperTarget;

        /// <summary>Source of the edge underneath.</summary>
        public string CrossedSource { get; } = CrossedSource;

        /// <summary>Target of the edge underneath.</summary>
        public string CrossedTarget { get; } = CrossedTarget;

        /// <summary>Crossing X in SVG coordinates.</summary>
        public double X { get; } = X;

        /// <summary>Crossing Y in SVG coordinates.</summary>
        public double Y { get; } = Y;

        /// <summary>Why no clean route was used.</summary>
        public string Reason { get; } = Reason;
    }

    private readonly struct EdgeStroke(
        int Index,
        int Length,
        string Markup,
        Dictionary<string, string> Attributes,
        string SourceId,
        string TargetId,
        List<(double X, double Y)> Points)
    {
        public int Index { get; } = Index;

        public int Length { get; } = Length;

        public string Markup { get; } = Markup;

        public Dictionary<string, string> Attributes { get; } = Attributes;

        public string SourceId { get; } = SourceId;

        public string TargetId { get; } = TargetId;

        public List<(double X, double Y)> Points { get; } = Points;
    }

    private readonly struct Crossing(int Hopper, int Segment, double X, double Y)
    {
        public int Hopper { get; } = Hopper;

        public int Segment { get; } = Segment;

        public double X { get; } = X;

        public double Y { get; } = Y;
    }
}
