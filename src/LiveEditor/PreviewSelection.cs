using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LiveEditor;

/// <summary>
/// What a selected preview object is.
/// </summary>
internal enum PreviewObjectKind
{
    /// <summary>An element or note with nothing drawn inside it.</summary>
    Node,

    /// <summary>A group, or an element that other nodes are drawn inside.</summary>
    Container,

    /// <summary>A relationship or plain connection line.</summary>
    Relationship,
}

/// <summary>
/// One selectable object in the preview, with its outline in SVG user units and its declaration line.
/// </summary>
internal sealed class PreviewObject
{
    private readonly List<PreviewOutline> _Outlines = new List<PreviewOutline>();

    /// <summary>Gets or sets the object kind.</summary>
    public PreviewObjectKind Kind { get; set; }

    /// <summary>Gets or sets the node id; empty for relationships.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the relationship source id; empty for nodes.</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the relationship target id; empty for nodes.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Gets or sets the relationship kind name, such as flow, or connection for a plain line.</summary>
    public string RelationshipKind { get; set; } = string.Empty;

    /// <summary>Gets or sets which drawn line with the same source, target and kind this is, from zero.</summary>
    public int Occurrence { get; set; }

    /// <summary>Gets or sets the 1-based source line that declares the object; 0 when unknown.</summary>
    public int LineNumber { get; set; }

    /// <summary>Gets or sets the left edge of the drawn outline.</summary>
    public double Left { get; set; }

    /// <summary>Gets or sets the top edge of the drawn outline.</summary>
    public double Top { get; set; }

    /// <summary>Gets or sets the right edge of the drawn outline.</summary>
    public double Right { get; set; }

    /// <summary>Gets or sets the bottom edge of the drawn outline.</summary>
    public double Bottom { get; set; }

    /// <summary>Gets the strokes to draw bold when the object is selected.</summary>
    public List<PreviewOutline> Outlines
    {
        get
        {
            return _Outlines;
        }
    }

    /// <summary>Gets the area of the outline bounds.</summary>
    public double Area
    {
        get
        {
            return Math.Max(0, Right - Left) * Math.Max(0, Bottom - Top);
        }
    }

    /// <summary>
    /// Gets an identity that survives a re-render: the node id, or source, target, kind and occurrence.
    /// </summary>
    public string Key
    {
        get
        {
            if (Kind == PreviewObjectKind.Relationship)
            {
                return "line\u001f" + SourceId + "\u001f" + TargetId + "\u001f" + RelationshipKind + "\u001f" + Occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return "node\u001f" + Id;
        }
    }

    /// <summary>
    /// Returns true when the point is inside the outline bounds.
    /// </summary>
    public bool Contains(double X, double Y)
    {
        return X >= Left && X <= Right && Y >= Top && Y <= Bottom;
    }

    /// <summary>
    /// Returns the shortest distance from the point to any outline stroke.
    /// </summary>
    public double DistanceTo(double X, double Y)
    {
        double Best = double.MaxValue;
        foreach (PreviewOutline Outline in _Outlines)
        {
            for (int Index = 1; Index < Outline.Points.Count; Index++)
            {
                PointF From = Outline.Points[Index - 1];
                PointF To = Outline.Points[Index];
                Best = Math.Min(Best, DistanceToSegment(X, Y, From.X, From.Y, To.X, To.Y));
            }
        }

        return Best;
    }

    private static double DistanceToSegment(double X, double Y, double X1, double Y1, double X2, double Y2)
    {
        double DeltaX = X2 - X1;
        double DeltaY = Y2 - Y1;
        double LengthSquared = (DeltaX * DeltaX) + (DeltaY * DeltaY);
        double Ratio = 0;
        if (LengthSquared > 0)
        {
            Ratio = Math.Max(0, Math.Min(1, ((X - X1) * DeltaX + (Y - Y1) * DeltaY) / LengthSquared));
        }

        double OffsetX = X - (X1 + (Ratio * DeltaX));
        double OffsetY = Y - (Y1 + (Ratio * DeltaY));
        return Math.Sqrt((OffsetX * OffsetX) + (OffsetY * OffsetY));
    }
}

/// <summary>
/// One connected stroke of a selected object in SVG user units, with the dash pattern it is drawn with.
/// </summary>
internal sealed class PreviewOutline
{
    /// <summary>Gets the points of the stroke in drawing order.</summary>
    public List<PointF> Points { get; } = new List<PointF>();

    /// <summary>Gets or sets the SVG dash lengths, or null for a solid stroke.</summary>
    public float[]? Dash { get; set; }
}

/// <summary>
/// Maps between editor lines and preview objects, and draws the selection overlay.
/// </summary>
internal static class PreviewSelection
{
    /// <summary>Line width of the selection overlay in screen pixels.</summary>
    public const float LINE_WIDTH = 3.0F;

    /// <summary>How far from a line, in screen pixels, a click still selects it.</summary>
    public const double CLICK_TOLERANCE = 6.0;

    /// <summary>Colour of the selection overlay: the diagram's line colour, drawn bold.</summary>
    public static readonly Color LineColour = Color.FromArgb(0x1E, 0x1E, 0x1E);

    /// <summary>
    /// Returns the 1-based logical line that holds a character index. Only line feeds count, so word wrap does not.
    /// </summary>
    public static int LineNumberAt(string Text, int CharacterIndex)
    {
        int Limit = Math.Max(0, Math.Min(CharacterIndex, Text.Length));
        int Line = 1;
        for (int Index = 0; Index < Limit; Index++)
        {
            if (Text[Index] == '\n')
            {
                Line++;
            }
        }

        return Line;
    }

    /// <summary>
    /// Finds the text of a 1-based line from its first non-blank character to its end.
    /// </summary>
    /// <returns>False when the line does not exist.</returns>
    public static bool TryGetLineSelection(string Text, int LineNumber, out int Start, out int Length)
    {
        Start = 0;
        Length = 0;
        if (LineNumber < 1)
        {
            return false;
        }

        int LineStart = 0;
        for (int Line = 1; Line < LineNumber; Line++)
        {
            int Feed = Text.IndexOf('\n', LineStart);
            if (Feed < 0)
            {
                return false;
            }

            LineStart = Feed + 1;
        }

        int LineEnd = Text.IndexOf('\n', LineStart);
        if (LineEnd < 0)
        {
            LineEnd = Text.Length;
        }

        if (LineEnd > LineStart && Text[LineEnd - 1] == '\r')
        {
            LineEnd--;
        }

        int First = LineStart;
        while (First < LineEnd && char.IsWhiteSpace(Text[First]))
        {
            First++;
        }

        Start = First;
        Length = LineEnd - First;
        return true;
    }

    /// <summary>
    /// Gets the transform from SVG user units to picture-box client pixels for a zoom-mode picture.
    /// </summary>
    /// <returns>False when any size is empty.</returns>
    public static bool TryGetSvgToClient(
        int ControlWidth,
        int ControlHeight,
        int ImageWidth,
        int ImageHeight,
        double SvgWidth,
        double SvgHeight,
        out double ScaleX,
        out double ScaleY,
        out double OffsetX,
        out double OffsetY)
    {
        ScaleX = 0;
        ScaleY = 0;
        OffsetX = 0;
        OffsetY = 0;
        if (ControlWidth <= 0 || ControlHeight <= 0 || ImageWidth <= 0 || ImageHeight <= 0 || SvgWidth <= 0 || SvgHeight <= 0)
        {
            return false;
        }

        double Scale = Math.Min(ControlWidth / (double)ImageWidth, ControlHeight / (double)ImageHeight);
        if (Scale <= 0)
        {
            return false;
        }

        OffsetX = (ControlWidth - (ImageWidth * Scale)) / 2.0;
        OffsetY = (ControlHeight - (ImageHeight * Scale)) / 2.0;
        ScaleX = ImageWidth * Scale / SvgWidth;
        ScaleY = ImageHeight * Scale / SvgHeight;
        return true;
    }

    /// <summary>
    /// Draws the selected object's strokes bold over the picture.
    /// </summary>
    public static void Paint(Graphics Graphics, PreviewObject Selected, double ScaleX, double ScaleY, double OffsetX, double OffsetY)
    {
        SmoothingMode PreviousSmoothing = Graphics.SmoothingMode;
        Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            foreach (PreviewOutline Outline in Selected.Outlines)
            {
                if (Outline.Points.Count < 2)
                {
                    continue;
                }

                PointF[] Points = new PointF[Outline.Points.Count];
                for (int Index = 0; Index < Points.Length; Index++)
                {
                    PointF Point = Outline.Points[Index];
                    Points[Index] = new PointF((float)(OffsetX + (Point.X * ScaleX)), (float)(OffsetY + (Point.Y * ScaleY)));
                }

                using Pen Pen = new Pen(LineColour, LINE_WIDTH);
                Pen.LineJoin = LineJoin.Round;
                Pen.StartCap = LineCap.Round;
                Pen.EndCap = LineCap.Round;
                float[]? Pattern = DashPattern(Outline.Dash, Math.Min(ScaleX, ScaleY));
                if (Pattern != null)
                {
                    Pen.DashCap = DashCap.Flat;
                    Pen.DashPattern = Pattern;
                }

                Graphics.DrawLines(Pen, Points);
            }
        }
        finally
        {
            Graphics.SmoothingMode = PreviousSmoothing;
        }
    }

    /// <summary>
    /// Converts SVG dash lengths into a pen pattern, which GDI+ measures in multiples of the pen width,
    /// so the bold dashes sit on the dashes underneath.
    /// </summary>
    internal static float[]? DashPattern(float[]? Dash, double Scale)
    {
        if (Dash == null || Dash.Length == 0 || Scale <= 0)
        {
            return null;
        }

        int Count = Dash.Length % 2 == 0 ? Dash.Length : Dash.Length * 2;
        float[] Pattern = new float[Count];
        for (int Index = 0; Index < Count; Index++)
        {
            double Length = Dash[Index % Dash.Length] * Scale / LINE_WIDTH;
            Pattern[Index] = (float)Math.Max(0.1, Length);
        }

        return Pattern;
    }
}
