using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using MermaidRecognition.Layout;

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

    private bool _Selected;

    private PreviewObjectKind _Kind;

    private string _Id = string.Empty;

    private string _SourceId = string.Empty;

    private string _TargetId = string.Empty;

    private string _RelationshipKind = string.Empty;

    private Action? _Changed;

    /// <summary>Creates an item with a neutral visual style.</summary>
    public PreviewObject()
    {
        _Style.Changed = RaiseStyleChanged;
    }

    private PreviewStyle _Style = new PreviewStyle();

    private Action? _StyleChanged;

    private Action? _AppearanceChanged;

    /// <summary>
    /// Raised when a property that changes the diagram is edited.
    /// </summary>
    internal Action? Changed
    {
        get
        {
            return _Changed;
        }

        set
        {
            _Changed = value;
        }
    }

    /// <summary>
    /// Gets or sets whether this item is selected. A new item is not selected.
    /// Several items can be selected together. <see cref="PreviewCanvas.SelectedItems"/> lists them.
    /// </summary>
    public bool Selected
    {
        get
        {
            return _Selected;
        }

        set
        {
            if (_Selected == value)
            {
                return;
            }

            _Selected = value;
            _AppearanceChanged?.Invoke();
        }
    }

    /// <summary>
    /// Gets or sets the item's own visual properties.
    /// Reading them returns these values even while the item is selected.
    /// </summary>
    public PreviewStyle Style
    {
        get
        {
            return _Style;
        }

        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (ReferenceEquals(_Style, value))
            {
                return;
            }

            _Style.Changed = null;
            _Style = value;
            _Style.Changed = RaiseStyleChanged;
            RaiseStyleChanged();
        }
    }

    /// <summary>Raised when <see cref="Style"/> changes.</summary>
    internal Action? StyleChanged
    {
        get
        {
            return _StyleChanged;
        }

        set
        {
            _StyleChanged = value;
        }
    }

    /// <summary>Raised when <see cref="Selected"/> changes.</summary>
    internal Action? AppearanceChanged
    {
        get
        {
            return _AppearanceChanged;
        }

        set
        {
            _AppearanceChanged = value;
        }
    }

    /// <summary>Gets or sets the object kind.</summary>
    public PreviewObjectKind Kind
    {
        get
        {
            return _Kind;
        }

        set
        {
            if (_Kind == value)
            {
                return;
            }

            _Kind = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Gets or sets the node id; empty for relationships.</summary>
    public string Id
    {
        get
        {
            return _Id;
        }

        set
        {
            AssignText(ref _Id, value);
        }
    }

    /// <summary>Gets or sets the relationship source id; empty for nodes.</summary>
    public string SourceId
    {
        get
        {
            return _SourceId;
        }

        set
        {
            AssignText(ref _SourceId, value);
        }
    }

    /// <summary>Gets or sets the relationship target id; empty for nodes.</summary>
    public string TargetId
    {
        get
        {
            return _TargetId;
        }

        set
        {
            AssignText(ref _TargetId, value);
        }
    }

    /// <summary>Gets or sets the relationship kind name, such as flow, or connection for a plain line.</summary>
    public string RelationshipKind
    {
        get
        {
            return _RelationshipKind;
        }

        set
        {
            AssignText(ref _RelationshipKind, value);
        }
    }

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

    /// <summary>Gets or sets whether the node is a junction, which shows no connection points.</summary>
    public bool IsJunction { get; set; }

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

    private void AssignText(ref string Field, string? Value)
    {
        string Next = Value ?? string.Empty;
        if (string.Equals(Field, Next, StringComparison.Ordinal))
        {
            return;
        }

        Field = Next;
        Changed?.Invoke();
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

    private void RaiseStyleChanged()
    {
        _StyleChanged?.Invoke();
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

    /// <summary>Width and height of a connection point mark in screen pixels.</summary>
    public const float MARK_SIZE = 7.0F;

    /// <summary>Line width of a connection point mark in screen pixels.</summary>
    public const float MARK_WIDTH = 2.0F;

    /// <summary>How far beyond a route end, in SVG user units, an arrowhead or other end marker can reach.</summary>
    private const double MARKER_REACH = 30.0;

    /// <summary>Rounding allowance, in SVG user units, when checking a port's clearance from a corner.</summary>
    private const double CORNER_TOLERANCE = 0.01;

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
    /// Draws the selected object's strokes over the picture, and an x on each of its connection points.
    /// </summary>
    public static void Paint(Graphics Graphics, PreviewObject Selected, Color Colour, double ScaleX, double ScaleY, double OffsetX, double OffsetY)
    {
        PreviewStyle Drawn = new PreviewStyle();
        Drawn.LineColour = Colour;
        Drawn.Bold = true;
        Drawn.Width = LINE_WIDTH;
        Paint(Graphics, Selected, Drawn, ScaleX, ScaleY, OffsetX, OffsetY);
    }

    /// <summary>
    /// Draws an item's own strokes over the picture.
    /// </summary>
    public static void Paint(Graphics Graphics, PreviewObject Selected, PreviewStyle Style, double ScaleX, double ScaleY, double OffsetX, double OffsetY)
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

                using Pen Pen = new Pen(Style.LineColour, Style.DrawnWidth);
                Pen.LineJoin = LineJoin.Round;
                Pen.StartCap = LineCap.Round;
                Pen.EndCap = LineCap.Round;
                float[]? Pattern = DashPattern(Outline.Dash, Math.Min(ScaleX, ScaleY));
                if (Pattern != null)
                {
                    Pen.DashCap = DashCap.Flat;
                    Pen.DashPattern = Pattern;
                }

                if (Style.FillColour.A > 0 && Points.Length >= 3)
                {
                    using SolidBrush Brush = new SolidBrush(Style.FillColour);
                    Graphics.FillPolygon(Brush, Points);
                }

                Graphics.DrawLines(Pen, Points);
            }

            List<PointF> Marks = ConnectionPoints(Selected);
            if (Marks.Count > 0)
            {
                using Pen MarkPen = new Pen(Style.LineColour, MARK_WIDTH);
                float Half = MARK_SIZE / 2.0F;
                foreach (PointF Point in Marks)
                {
                    float X = (float)(OffsetX + (Point.X * ScaleX));
                    float Y = (float)(OffsetY + (Point.Y * ScaleY));
                    Graphics.DrawLine(MarkPen, X - Half, Y - Half, X + Half, Y + Half);
                    Graphics.DrawLine(MarkPen, X - Half, Y + Half, X + Half, Y - Half);
                }
            }
        }
        finally
        {
            Graphics.SmoothingMode = PreviousSmoothing;
        }
    }

    /// <summary>
    /// Works out where lines can attach to the selected object, in SVG user units. A node, note or container gets
    /// every potential port on each side of its outline box, a relationship gets its two ends, and a junction gets none.
    /// </summary>
    public static List<PointF> ConnectionPoints(PreviewObject Selected)
    {
        List<PointF> Points = new List<PointF>();
        if (Selected.Kind == PreviewObjectKind.Relationship)
        {
            AddLineEnds(Selected, Points);
            return Points;
        }

        if (Selected.IsJunction || Selected.Right <= Selected.Left || Selected.Bottom <= Selected.Top)
        {
            return Points;
        }

        foreach (double Offset in SideOffsets(Selected.Right - Selected.Left))
        {
            Points.Add(new PointF((float)(Selected.Left + Offset), (float)Selected.Top));
            Points.Add(new PointF((float)(Selected.Left + Offset), (float)Selected.Bottom));
        }

        foreach (double Offset in SideOffsets(Selected.Bottom - Selected.Top))
        {
            Points.Add(new PointF((float)Selected.Left, (float)(Selected.Top + Offset)));
            Points.Add(new PointF((float)Selected.Right, (float)(Selected.Top + Offset)));
        }

        return Points;
    }

    /// <summary>
    /// Places the ports on a side counting out from its centre: the midpoint, then every line_spacing either side
    /// of it, keeping only those at least line_spacing from both corners.
    /// </summary>
    /// <returns>Distances from the start of the side in ascending order; empty when the side is too short for a port.</returns>
    internal static List<double> SideOffsets(double Length)
    {
        List<double> Offsets = new List<double>();
        double Spacing = LineSpacing();
        double Middle = Length / 2.0;
        if (Spacing <= 0 || Middle < Spacing - CORNER_TOLERANCE)
        {
            return Offsets;
        }

        int Steps = (int)Math.Floor(((Middle - Spacing) + CORNER_TOLERANCE) / Spacing);
        for (int Step = -Steps; Step <= Steps; Step++)
        {
            Offsets.Add(Middle + (Step * Spacing));
        }

        return Offsets;
    }

    /// <summary>
    /// Reads line_spacing from the layout rules. The layout project only exposes it through the side length that
    /// holds one port, which is a line_spacing clearance either side of it.
    /// </summary>
    private static double LineSpacing()
    {
        return DiagramGridMetrics.MinSideLengthForPorts(1) / 2.0;
    }

    /// <summary>
    /// Adds the two ends of a relationship: where its route starts and stops, or the tip of a marker drawn beyond it.
    /// </summary>
    private static void AddLineEnds(PreviewObject Line, List<PointF> Points)
    {
        PreviewOutline? Route = null;
        double Longest = -1;
        foreach (PreviewOutline Outline in Line.Outlines)
        {
            double Length = 0;
            for (int Index = 1; Index < Outline.Points.Count; Index++)
            {
                Length += PointDistance(Outline.Points[Index - 1], Outline.Points[Index]);
            }

            if (Length > Longest)
            {
                Longest = Length;
                Route = Outline;
            }
        }

        if (Route == null || Route.Points.Count < 2)
        {
            return;
        }

        int Last = Route.Points.Count - 1;
        Points.Add(MarkerTip(Line, Route.Points[1], Route.Points[0]));
        Points.Add(MarkerTip(Line, Route.Points[Last - 1], Route.Points[Last]));
    }

    /// <summary>
    /// Returns the furthest point of the line's drawing that lies straight on beyond a route end, such as an
    /// arrowhead tip touching the target outline, or the route end itself when nothing is drawn beyond it.
    /// </summary>
    private static PointF MarkerTip(PreviewObject Line, PointF Before, PointF End)
    {
        double Length = PointDistance(Before, End);
        if (Length <= 0)
        {
            return End;
        }

        double DirectionX = (End.X - Before.X) / Length;
        double DirectionY = (End.Y - Before.Y) / Length;
        PointF Best = End;
        double BestAhead = 0;
        foreach (PreviewOutline Outline in Line.Outlines)
        {
            foreach (PointF Point in Outline.Points)
            {
                double OffsetX = Point.X - End.X;
                double OffsetY = Point.Y - End.Y;
                double Ahead = (OffsetX * DirectionX) + (OffsetY * DirectionY);
                double Across = Math.Abs((OffsetX * DirectionY) - (OffsetY * DirectionX));
                if (Ahead > BestAhead && Ahead <= MARKER_REACH && Across <= 0.5)
                {
                    Best = Point;
                    BestAhead = Ahead;
                }
            }
        }

        return Best;
    }

    private static double PointDistance(PointF From, PointF To)
    {
        double DeltaX = To.X - From.X;
        double DeltaY = To.Y - From.Y;
        return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
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
