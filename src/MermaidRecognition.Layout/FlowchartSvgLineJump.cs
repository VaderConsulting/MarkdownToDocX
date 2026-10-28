using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    private static readonly Regex EdgeRegex = new Regex(
        "<(?:path|line)\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AttributeRegex = new Regex(
        "([a-zA-Z_:][\\w:.-]*)=\"([^\"]*)\"",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Hops drawn by the most recent <see cref="Apply"/> call.
    /// </summary>
    public static IReadOnlyList<DrawnJump> LastJumps
    {
        get
        {
            return FlowchartRenderContext.Current.Jumps;
        }
    }

    /// <summary>
    /// Rewrites <paramref name="Svg"/> so each genuine perpendicular crossing carries one hop.
    /// </summary>
    /// <param name="Svg">Rendered SVG.</param>
    /// <param name="InsertHops">Whether to insert hops at perpendicular crossings.</param>
    /// <returns>SVG with hop arcs inserted.</returns>
    public static string Apply(string Svg, bool InsertHops = true)
    {
        FlowchartRenderContext.Current.Jumps.Clear();
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
        FlowchartRenderContext.ReportOperationCount("line-jump-stroke-pairs", (long)Strokes.Count * Math.Max(0, Strokes.Count - 1) / 2);
        List<SweepSegment> Segments = [];
        for (int StrokeIndex = 0; StrokeIndex < Strokes.Count; StrokeIndex++)
        {
            List<(double X, double Y)> Points = Strokes[StrokeIndex].Points;
            for (int SegmentIndex = 1; SegmentIndex < Points.Count; SegmentIndex++)
            {
                (double X1, double Y1) = Points[SegmentIndex - 1];
                (double X2, double Y2) = Points[SegmentIndex];
                if (!IsAxisSegment(X1, Y1, X2, Y2, out bool Horizontal))
                {
                    continue;
                }

                Segments.Add(new SweepSegment(StrokeIndex, SegmentIndex, X1, Y1, X2, Y2, Horizontal));
            }
        }

        Segments.Sort(static (Left, Right) => Left.MinX.CompareTo(Right.MinX));
        List<SweepSegment> Active = [];
        List<CandidatePair> Candidates = [];
        long BroadPhaseTests = 0;
        foreach (SweepSegment Current in Segments)
        {
            for (int Index = Active.Count - 1; Index >= 0; Index--)
            {
                if (Active[Index].MaxX < Current.MinX)
                {
                    Active.RemoveAt(Index);
                }
            }

            foreach (SweepSegment Prior in Active)
            {
                BroadPhaseTests++;
                if (Prior.StrokeIndex == Current.StrokeIndex
                    || Prior.Horizontal == Current.Horizontal
                    || Prior.MaxY < Current.MinY || Current.MaxY < Prior.MinY)
                {
                    continue;
                }

                SweepSegment Left = Prior.StrokeIndex < Current.StrokeIndex ? Prior : Current;
                SweepSegment Right = Prior.StrokeIndex < Current.StrokeIndex ? Current : Prior;
                Candidates.Add(new CandidatePair(Left, Right));
            }
            Active.Add(Current);
        }

        Candidates.Sort(static (Left, Right) =>
        {
            int Compare = Left.Left.StrokeIndex.CompareTo(Right.Left.StrokeIndex);
            if (Compare != 0)
            {
                return Compare;
            }

            Compare = Left.Right.StrokeIndex.CompareTo(Right.Right.StrokeIndex);
            if (Compare != 0)
            {
                return Compare;
            }

            Compare = Left.Left.SegmentIndex.CompareTo(Right.Left.SegmentIndex);
            return Compare != 0 ? Compare : Left.Right.SegmentIndex.CompareTo(Right.Right.SegmentIndex);
        });

        foreach (CandidatePair Pair in Candidates)
        {
            SweepSegment LeftSegment = Pair.Left;
            SweepSegment RightSegment = Pair.Right;
            EdgeStroke Left = Strokes[LeftSegment.StrokeIndex];
            EdgeStroke Right = Strokes[RightSegment.StrokeIndex];
            if (!TryIntersect(LeftSegment.X1, LeftSegment.Y1, LeftSegment.X2, LeftSegment.Y2,
                    RightSegment.X1, RightSegment.Y1, RightSegment.X2, RightSegment.Y2,
                    out double CrossX, out double CrossY, out double Ua, out double Ub))
            {
                continue;
            }

            if (Ua <= 1e-6 || Ua >= 1 - 1e-6 || Ub <= 1e-6 || Ub >= 1 - 1e-6)
            {
                continue;
            }

            double LeftClear = Math.Min(Distance(CrossX, CrossY, LeftSegment.X1, LeftSegment.Y1), Distance(CrossX, CrossY, LeftSegment.X2, LeftSegment.Y2));
            double RightClear = Math.Min(Distance(CrossX, CrossY, RightSegment.X1, RightSegment.Y1), Distance(CrossX, CrossY, RightSegment.X2, RightSegment.Y2));
            int Hopper = RightClear >= HOP_RADIUS_PX || RightClear >= LeftClear ? RightSegment.StrokeIndex : LeftSegment.StrokeIndex;
            int HopSegment = Hopper == RightSegment.StrokeIndex ? RightSegment.SegmentIndex : LeftSegment.SegmentIndex;
            string Reason = FindReason(Left, Right);
            Crossings.Add(new Crossing(Hopper, HopSegment, CrossX, CrossY));
            FlowchartRenderContext.Current.Jumps.Add(new DrawnJump(
                Strokes[Hopper].SourceId,
                Strokes[Hopper].TargetId,
                Strokes[Hopper == RightSegment.StrokeIndex ? LeftSegment.StrokeIndex : RightSegment.StrokeIndex].SourceId,
                Strokes[Hopper == RightSegment.StrokeIndex ? LeftSegment.StrokeIndex : RightSegment.StrokeIndex].TargetId,
                CrossX,
                CrossY,
                Reason));
        }

        FlowchartRenderContext.ReportOperationCount("line-jump-broadphase-pair-tests", BroadPhaseTests);
        FlowchartRenderContext.ReportOperationCount("line-jump-segment-pair-tests", Candidates.Count);
        FlowchartRenderContext.ReportOperationCount("line-jump-crossings-found", Crossings.Count);
        return Crossings;
    }

    private readonly record struct SweepSegment(
        int StrokeIndex, int SegmentIndex, double X1, double Y1, double X2, double Y2, bool Horizontal)
    {
        public double MinX => Math.Min(X1, X2);
        public double MaxX => Math.Max(X1, X2);
        public double MinY => Math.Min(Y1, Y2);
        public double MaxY => Math.Max(Y1, Y2);
    }

    private readonly record struct CandidatePair(SweepSegment Left, SweepSegment Right);

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
        // Edit straight commands only, preserving the actual rounded corners.
        string Original = Stroke.Attributes.TryGetValue("d", out string? Path) ? Path
            : "M" + Format(Stroke.Points[0].X) + "," + Format(Stroke.Points[0].Y)
                + "L" + Format(Stroke.Points[^1].X) + "," + Format(Stroke.Points[^1].Y);
        StringBuilder Data = new StringBuilder();
        foreach ((char Kind, double X, double Y, double Nx, double Ny, string Markup) Command in ReadCommands(Original))
        {
            double X = Command.X, Y = Command.Y, Nx = Command.Nx, Ny = Command.Ny;
            List<Crossing> OnSegment = [.. Hops.Where(Hop => (Command.Kind is 'L' or 'H' or 'V')
                && Distance(X, Y, Hop.X, Hop.Y) + Distance(Hop.X, Hop.Y, Nx, Ny)
                    <= Distance(X, Y, Nx, Ny) + 0.001)
                .GroupBy(Hop => (Hop.X, Hop.Y)).Select(Group => Group.First())
                .OrderBy(Hop => Distance(X, Y, Hop.X, Hop.Y))];
            if (OnSegment.Count == 0)
            {
                Data.Append(Command.Markup);
            }
            else
            {
                for (int Index = 0; Index < OnSegment.Count; Index++)
                {
                    Crossing Hop = OnSegment[Index];
                    double Radius = Math.Min(HOP_RADIUS_PX, Math.Min(Distance(X, Y, Hop.X, Hop.Y), Distance(Nx, Ny, Hop.X, Hop.Y)) / 2);
                    if (Index > 0)
                    {
                        Radius = Math.Min(Radius, Distance(Hop.X, Hop.Y, OnSegment[Index - 1].X, OnSegment[Index - 1].Y) / 2.5);
                    }

                    if (Index + 1 < OnSegment.Count)
                    {
                        Radius = Math.Min(Radius, Distance(Hop.X, Hop.Y, OnSegment[Index + 1].X, OnSegment[Index + 1].Y) / 2.5);
                    }

                    AppendHop(Data, X, Y, Nx, Ny, Hop.X, Hop.Y, Radius);
                }
                Data.Append('L').Append(Format(Nx)).Append(',').Append(Format(Ny));
            }
        }
        StringBuilder Builder = new StringBuilder("<path d=\"");
        Builder.Append(Data).Append("\" fill=\"none\" stroke=\"")
            .Append(Stroke.Attributes.TryGetValue("stroke", out string? Colour) ? Colour : "#333333").Append('"');
        foreach (KeyValuePair<string, string> Attribute in Stroke.Attributes)
        {
            if (Attribute.Key is "d" or "x1" or "x2" or "y1" or "y2" or "fill" or "stroke")
            {
                continue;
            }

            Builder.Append(' ').Append(Attribute.Key).Append("=\"").Append(Attribute.Value).Append('"');
        }
        return Builder.Append("/>").ToString();
    }

    private static void AppendHop(StringBuilder Data, double FromX, double FromY,
        double ToX, double ToY, double CrossX, double CrossY, double Radius)
    {
        double Length = Distance(FromX, FromY, ToX, ToY);
        double DirectionX = (ToX - FromX) / Length, DirectionY = (ToY - FromY) / Length;
        Data.Append('L').Append(Format(CrossX - (DirectionX * Radius))).Append(',').Append(Format(CrossY - (DirectionY * Radius)));
        Data.Append('A').Append(Format(Radius)).Append(',').Append(Format(Radius)).Append(" 0 0 1 ")
            .Append(Format(CrossX + (DirectionX * Radius))).Append(',').Append(Format(CrossY + (DirectionY * Radius)));
    }

    private static bool TryReadPoints(Dictionary<string, string> Attributes, out List<(double X, double Y)> Points)
    {
        Points = [];
        if (Attributes.TryGetValue("d", out string? PathData))
        {
            foreach ((char Kind, double X, double Y, double Nx, double Ny, string Markup) Command in ReadCommands(PathData))
            {
                if (Command.Kind is not ('L' or 'H' or 'V'))
                {
                    continue;
                }
                // Never treat a move or a rounded corner as a straight chord.
                Points.Add((double.NaN, double.NaN));
                Points.Add((Command.X, Command.Y));
                Points.Add((Command.Nx, Command.Ny));
            }
            return Points.Count >= 3;
        }
        if (Attributes.TryGetValue("x1", out string? X1Text)
            && Attributes.TryGetValue("y1", out string? Y1Text)
            && Attributes.TryGetValue("x2", out string? X2Text)
            && Attributes.TryGetValue("y2", out string? Y2Text)
            && TryParse(X1Text, out double X1) && TryParse(Y1Text, out double Y1)
            && TryParse(X2Text, out double X2) && TryParse(Y2Text, out double Y2))
        {
            Points = [(X1, Y1), (X2, Y2)]; return true;
        }
        return false;
    }

    private static IEnumerable<(char Kind, double X, double Y, double Nx, double Ny, string Markup)> ReadCommands(string Path)
    {
        double X = 0, Y = 0, StartX = 0, StartY = 0;
        foreach (Match Command in Regex.Matches(Path, @"[MLHVCSQTAZmlhvcsqtaz][^MLHVCSQTAZmlhvcsqtaz]*"))
        {
            char Kind = char.ToUpperInvariant(Command.Value[0]);
            MatchCollection Values = Regex.Matches(Command.Value.Substring(1), @"[-+]?(?:\d*\.)?\d+(?:[eE][-+]?\d+)?");
            double Nx = X, Ny = Y;
            if (Kind == 'Z') { Nx = StartX; Ny = StartY; }
            else if (Values.Count > 0)
            {
                double Last = double.Parse(Values[^1].Value, CultureInfo.InvariantCulture);
                if (Kind == 'H')
                {
                    Nx = Last;
                }
                else if (Kind == 'V')
                {
                    Ny = Last;
                }
                else if (Values.Count >= 2)
                {
                    Nx = double.Parse(Values[^2].Value, CultureInfo.InvariantCulture); Ny = Last;
                }
                if (char.IsLower(Command.Value[0]))
                {
                    if (Kind != 'V')
                    {
                        Nx += X;
                    }

                    if (Kind != 'H')
                    {
                        Ny += Y;
                    }
                }
            }
            yield return (Kind, X, Y, Nx, Ny, Command.Value);
            X = Nx; Y = Ny;
            if (Kind == 'M') { StartX = X; StartY = Y; }
        }
    }

    private static bool IsAxisSegment(double X1, double Y1, double X2, double Y2, out bool Horizontal)
    {
        Horizontal = Math.Abs(Y2 - Y1) <= 0.001 && Math.Abs(X2 - X1) > 0.001;
        bool Vertical = Math.Abs(X2 - X1) <= 0.001 && Math.Abs(Y2 - Y1) > 0.001;
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
        if (Ua <= 1e-6 || Ua >= 1 - 1e-6 || Ub <= 1e-6 || Ub >= 1 - 1e-6)
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
