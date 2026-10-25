using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Selectable objects in the preview: nodes, containers and relationships, with their outlines and source lines.
/// </summary>
internal sealed partial class PreviewHoverIndex
{
    private const double TOUCH_TOLERANCE = 0.75;

    private const string PLAIN_CONNECTION = "connection";

    private readonly List<PreviewObject> _Objects = new List<PreviewObject>();

    /// <summary>Gets every selectable object in drawing order.</summary>
    public IReadOnlyList<PreviewObject> Objects
    {
        get
        {
            return _Objects;
        }
    }

    /// <summary>
    /// Creates an empty index that <see cref="AddObject"/> can fill.
    /// </summary>
    /// <returns>An index with no items.</returns>
    internal static PreviewHoverIndex CreateEmpty()
    {
        return new PreviewHoverIndex();
    }

    /// <summary>
    /// Builds items from an enterprise script, without a picture.
    /// </summary>
    /// <param name="Source">Diagram script.</param>
    /// <returns>An index of the declared nodes and relationships. Empty when the script is not enterprise.</returns>
    internal static PreviewHoverIndex FromScript(string? Source)
    {
        PreviewHoverIndex Index = new PreviewHoverIndex();
        if (!EnterpriseParser.IsEnterpriseSource(Source))
        {
            return Index;
        }

        EnterpriseParseResult Parsed = EnterpriseParser.ParseDocument(Source);
        foreach (EnterpriseElement Element in Parsed.Document.Elements.Values)
        {
            PreviewObject Node = new PreviewObject
            {
                Kind = Parsed.Document.Groups.ContainsKey(Element.Id) ? PreviewObjectKind.Container : PreviewObjectKind.Node,
                Id = Element.Id,
                LineNumber = Element.LineNumber
            };
            Index._Objects.Add(Node);
        }

        foreach (EnterpriseNote Note in Parsed.Document.Notes.Values)
        {
            if (Parsed.Document.Elements.ContainsKey(Note.Id))
            {
                continue;
            }

            Index._Objects.Add(new PreviewObject
            {
                Kind = PreviewObjectKind.Node,
                Id = Note.Id,
                LineNumber = Note.LineNumber
            });
        }

        foreach (EnterpriseRelationship Relationship in Parsed.Document.Relationships)
        {
            Index.AddRelationship(Relationship.SourceId, Relationship.TargetId, EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind));
        }

        foreach (EnterprisePlainConnection Connection in Parsed.Document.PlainConnections)
        {
            Index.AddRelationship(Connection.SourceId, Connection.TargetId, PLAIN_CONNECTION);
        }

        Index.FinishEnterpriseObjects(Parsed.Document);
        return Index;
    }

    /// <summary>
    /// Adds an item. An item that is already present is left where it is.
    /// </summary>
    /// <param name="Item">Item to add.</param>
    /// <returns>False when the item was already present.</returns>
    internal bool AddObject(PreviewObject Item)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (_Objects.Contains(Item))
        {
            return false;
        }

        _Objects.Add(Item);
        return true;
    }

    /// <summary>
    /// Removes an item.
    /// </summary>
    /// <param name="Item">Item to remove.</param>
    /// <returns>False when the item was not present.</returns>
    internal bool RemoveObject(PreviewObject Item)
    {
        if (Item == null)
        {
            return false;
        }

        return _Objects.Remove(Item);
    }

    private void AddRelationship(string SourceId, string TargetId, string KindName)
    {
        PreviewObject Line = new PreviewObject
        {
            Kind = PreviewObjectKind.Relationship,
            SourceId = SourceId,
            TargetId = TargetId,
            RelationshipKind = KindName
        };
        foreach (PreviewObject Earlier in _Objects)
        {
            if (Earlier.Kind == PreviewObjectKind.Relationship
                && string.Equals(Earlier.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Earlier.TargetId, TargetId, StringComparison.Ordinal)
                && string.Equals(Earlier.RelationshipKind, Line.RelationshipKind, StringComparison.Ordinal))
            {
                Line.Occurrence++;
            }
        }

        _Objects.Add(Line);
    }

    /// <summary>
    /// Returns the object a click at an SVG point selects: the smallest node under it, else the nearest line
    /// within the tolerance, else the smallest container under it.
    /// </summary>
    /// <param name="SvgX">Horizontal position.</param>
    /// <param name="SvgY">Vertical position.</param>
    /// <param name="Tolerance">How far from a line still counts, in SVG user units.</param>
    /// <returns>The object, or null for empty space.</returns>
    public PreviewObject? HitObject(double SvgX, double SvgY, double Tolerance)
    {
        PreviewObject? Node = SmallestContaining(PreviewObjectKind.Node, SvgX, SvgY);
        if (Node != null)
        {
            return Node;
        }

        PreviewObject? Line = null;
        double BestDistance = Tolerance;
        foreach (PreviewObject Candidate in _Objects)
        {
            if (Candidate.Kind != PreviewObjectKind.Relationship)
            {
                continue;
            }

            double Distance = Candidate.DistanceTo(SvgX, SvgY);
            if (Distance <= BestDistance)
            {
                BestDistance = Distance;
                Line = Candidate;
            }
        }

        if (Line != null)
        {
            return Line;
        }

        return SmallestContaining(PreviewObjectKind.Container, SvgX, SvgY);
    }

    /// <summary>
    /// Finds the object with the same identity as one from an earlier render.
    /// </summary>
    /// <param name="Key">The earlier object's <see cref="PreviewObject.Key"/>.</param>
    /// <returns>The object, or null when it is no longer drawn.</returns>
    public PreviewObject? FindObject(string? Key)
    {
        if (string.IsNullOrEmpty(Key))
        {
            return null;
        }

        foreach (PreviewObject Candidate in _Objects)
        {
            if (string.Equals(Candidate.Key, Key, StringComparison.Ordinal))
            {
                return Candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the object declared on a source line. A relationship wins over a node created by the same line.
    /// </summary>
    /// <param name="LineNumber">1-based source line.</param>
    /// <returns>The object, or null when the line declares nothing drawn.</returns>
    public PreviewObject? ObjectAtLine(int LineNumber)
    {
        if (LineNumber < 1)
        {
            return null;
        }

        PreviewObject? Shape = null;
        foreach (PreviewObject Candidate in _Objects)
        {
            if (Candidate.LineNumber != LineNumber)
            {
                continue;
            }

            if (Candidate.Kind == PreviewObjectKind.Relationship)
            {
                return Candidate;
            }

            Shape ??= Candidate;
        }

        return Shape;
    }

    private PreviewObject? SmallestContaining(PreviewObjectKind Kind, double SvgX, double SvgY)
    {
        PreviewObject? Best = null;
        foreach (PreviewObject Candidate in _Objects)
        {
            if (Candidate.Kind != Kind || !Candidate.Contains(SvgX, SvgY))
            {
                continue;
            }

            if (Best == null || Candidate.Area < Best.Area)
            {
                Best = Candidate;
            }
        }

        return Best;
    }

    private void AddShapeObject(string Id, bool IsGroup, string Body, double ShiftX, double ShiftY, double Left, double Top, double Right, double Bottom)
    {
        List<PreviewOutline> Outlines = OuterOutlines(ReadOutlines(Body, ShiftX, ShiftY));
        if (Outlines.Count == 0)
        {
            PreviewOutline Box = new PreviewOutline();
            AddRectangle(Box, Left, Top, Right - Left, Bottom - Top, 0, 0);
            Outlines.Add(Box);
        }

        PreviewObject Shape = new PreviewObject
        {
            Kind = IsGroup ? PreviewObjectKind.Container : PreviewObjectKind.Node,
            Id = Id
        };
        Shape.Outlines.AddRange(Outlines);
        MeasureOutlines(Shape);
        PreviewObject? Existing = FindObject(Shape.Key);
        if (Existing == null)
        {
            _Objects.Add(Shape);
            return;
        }

        Existing.Outlines.AddRange(Shape.Outlines);
        MeasureOutlines(Existing);
        if (IsGroup)
        {
            Existing.Kind = PreviewObjectKind.Container;
        }
    }

    private void AddLineObject(string SourceId, string TargetId, string KindName, List<PreviewOutline> Outlines)
    {
        if (Outlines.Count == 0)
        {
            return;
        }

        PreviewObject Line = new PreviewObject
        {
            Kind = PreviewObjectKind.Relationship,
            SourceId = SourceId,
            TargetId = TargetId,
            RelationshipKind = KindName.Length == 0 ? PLAIN_CONNECTION : KindName
        };
        foreach (PreviewObject Earlier in _Objects)
        {
            if (Earlier.Kind == PreviewObjectKind.Relationship
                && string.Equals(Earlier.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Earlier.TargetId, TargetId, StringComparison.Ordinal)
                && string.Equals(Earlier.RelationshipKind, Line.RelationshipKind, StringComparison.Ordinal))
            {
                Line.Occurrence++;
            }
        }

        Line.Outlines.AddRange(Outlines);
        MeasureOutlines(Line);
        _Objects.Add(Line);
    }

    /// <summary>
    /// Marks nodes that have other nodes drawn inside them as containers and looks up every declaration line.
    /// </summary>
    private void FinishEnterpriseObjects(EnterpriseDocument Document)
    {
        foreach (PreviewObject Outer in _Objects)
        {
            if (Outer.Kind != PreviewObjectKind.Node)
            {
                continue;
            }

            foreach (PreviewObject Inner in _Objects)
            {
                if (ReferenceEquals(Inner, Outer) || Inner.Kind == PreviewObjectKind.Relationship || Inner.Area >= Outer.Area)
                {
                    continue;
                }

                if (Inner.Left >= Outer.Left - TOUCH_TOLERANCE && Inner.Right <= Outer.Right + TOUCH_TOLERANCE
                    && Inner.Top >= Outer.Top - TOUCH_TOLERANCE && Inner.Bottom <= Outer.Bottom + TOUCH_TOLERANCE)
                {
                    Outer.Kind = PreviewObjectKind.Container;
                    break;
                }
            }
        }

        foreach (PreviewObject Item in _Objects)
        {
            Item.LineNumber = Item.Kind == PreviewObjectKind.Relationship
                ? RelationshipLine(Document, Item)
                : NodeLine(Document, Item.Id);
        }
    }

    private static int NodeLine(EnterpriseDocument Document, string Id)
    {
        if (Document.Groups.TryGetValue(Id, out EnterpriseGroup? Group))
        {
            return Group.LineNumber;
        }

        if (Document.Notes.TryGetValue(Id, out EnterpriseNote? Note))
        {
            return Note.LineNumber;
        }

        if (Document.Elements.TryGetValue(Id, out EnterpriseElement? Element))
        {
            return Element.LineNumber;
        }

        return 0;
    }

    private static int RelationshipLine(EnterpriseDocument Document, PreviewObject Line)
    {
        List<int> Lines = new List<int>();
        if (Line.RelationshipKind == PLAIN_CONNECTION)
        {
            foreach (EnterprisePlainConnection Connection in Document.PlainConnections)
            {
                if (string.Equals(Connection.SourceId, Line.SourceId, StringComparison.Ordinal)
                    && string.Equals(Connection.TargetId, Line.TargetId, StringComparison.Ordinal))
                {
                    Lines.Add(Connection.LineNumber);
                }
            }
        }
        else
        {
            foreach (EnterpriseRelationship Relationship in Document.Relationships)
            {
                if (!Relationship.HiddenByNesting
                    && string.Equals(Relationship.SourceId, Line.SourceId, StringComparison.Ordinal)
                    && string.Equals(Relationship.TargetId, Line.TargetId, StringComparison.Ordinal)
                    && string.Equals(EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind), Line.RelationshipKind, StringComparison.OrdinalIgnoreCase))
                {
                    Lines.Add(Relationship.LineNumber);
                }
            }
        }

        if (Lines.Count == 0)
        {
            return 0;
        }

        return Line.Occurrence < Lines.Count ? Lines[Line.Occurrence] : Lines[0];
    }

    /// <summary>
    /// Reads the edges of a non-enterprise diagram that carry data-edge-source and data-edge-target.
    /// Pieces of one edge drawn back to back become one object.
    /// </summary>
    private void ReadEdgeObjects(string Svg, string Source)
    {
        const string MARKER = "data-edge-source=\"";
        string LastSource = string.Empty;
        string LastTarget = string.Empty;
        PreviewObject? Last = null;
        int Search = 0;
        while (Search < Svg.Length)
        {
            int Found = Svg.IndexOf(MARKER, Search, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            int Open = Svg.LastIndexOf('<', Found);
            int End = Svg.IndexOf('>', Found);
            if (Open < 0 || End < 0)
            {
                break;
            }

            Search = End + 1;
            string Tag = Svg.Substring(Open, End - Open + 1);
            string SourceId = ReadExactAttribute(Tag, "data-edge-source");
            string TargetId = ReadExactAttribute(Tag, "data-edge-target");
            if (SourceId.Length == 0 || TargetId.Length == 0)
            {
                continue;
            }

            List<PreviewOutline> Outlines = ReadOutlines(Tag, 0, 0);
            if (Outlines.Count == 0)
            {
                continue;
            }

            if (Last != null && SourceId == LastSource && TargetId == LastTarget)
            {
                Last.Outlines.AddRange(Outlines);
                MeasureOutlines(Last);
                continue;
            }

            AddLineObject(SourceId, TargetId, PLAIN_CONNECTION, Outlines);
            Last = _Objects[_Objects.Count - 1];
            LastSource = SourceId;
            LastTarget = TargetId;
        }

        foreach (PreviewObject Item in _Objects)
        {
            Item.LineNumber = EdgeLine(Source, Item);
        }
    }

    /// <summary>
    /// Finds the source line of a flowchart-style edge: the nth line naming the source, an arrow, then the target.
    /// </summary>
    private static int EdgeLine(string Source, PreviewObject Line)
    {
        string Pattern = @"(?<![\w])" + Regex.Escape(Line.SourceId) + @"(?![\w])[^\n]*?(?:--|==|-\.|~~)[^\n]*?(?<![\w])"
            + Regex.Escape(Line.TargetId) + @"(?![\w])";
        Regex Edge = new Regex(Pattern, RegexOptions.CultureInvariant);
        string[] Lines = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        List<int> Found = new List<int>();
        for (int Index = 0; Index < Lines.Length; Index++)
        {
            string Text = Lines[Index];
            int Comment = Text.IndexOf("%%", StringComparison.Ordinal);
            if (Comment >= 0)
            {
                Text = Text.Substring(0, Comment);
            }

            if (Edge.IsMatch(Text))
            {
                Found.Add(Index + 1);
            }
        }

        if (Found.Count == 0)
        {
            return 0;
        }

        return Line.Occurrence < Found.Count ? Found[Line.Occurrence] : Found[0];
    }

    /// <summary>
    /// Keeps the strokes that reach the shape's outer edge, which drops icons drawn inside the box.
    /// </summary>
    private static List<PreviewOutline> OuterOutlines(List<PreviewOutline> Outlines)
    {
        double Left = double.MaxValue;
        double Top = double.MaxValue;
        double Right = double.MinValue;
        double Bottom = double.MinValue;
        foreach (PreviewOutline Outline in Outlines)
        {
            foreach (PointF Point in Outline.Points)
            {
                Include(Point.X, Point.Y, ref Left, ref Top, ref Right, ref Bottom);
            }
        }

        List<PreviewOutline> Outer = new List<PreviewOutline>();
        foreach (PreviewOutline Outline in Outlines)
        {
            double OutlineLeft = double.MaxValue;
            double OutlineTop = double.MaxValue;
            double OutlineRight = double.MinValue;
            double OutlineBottom = double.MinValue;
            foreach (PointF Point in Outline.Points)
            {
                Include(Point.X, Point.Y, ref OutlineLeft, ref OutlineTop, ref OutlineRight, ref OutlineBottom);
            }

            if (Math.Abs(OutlineLeft - Left) <= TOUCH_TOLERANCE || Math.Abs(OutlineTop - Top) <= TOUCH_TOLERANCE
                || Math.Abs(OutlineRight - Right) <= TOUCH_TOLERANCE || Math.Abs(OutlineBottom - Bottom) <= TOUCH_TOLERANCE)
            {
                Outer.Add(Outline);
            }
        }

        return Outer;
    }

    private static void MeasureOutlines(PreviewObject Item)
    {
        double Left = double.MaxValue;
        double Top = double.MaxValue;
        double Right = double.MinValue;
        double Bottom = double.MinValue;
        foreach (PreviewOutline Outline in Item.Outlines)
        {
            foreach (PointF Point in Outline.Points)
            {
                Include(Point.X, Point.Y, ref Left, ref Top, ref Right, ref Bottom);
            }
        }

        if (Right < Left)
        {
            return;
        }

        Item.Left = Left;
        Item.Top = Top;
        Item.Right = Right;
        Item.Bottom = Bottom;
    }

    /// <summary>
    /// Turns the stroked primitives in an SVG fragment into point lists in SVG user units.
    /// </summary>
    private static List<PreviewOutline> ReadOutlines(string Body, double ShiftX, double ShiftY)
    {
        List<PreviewOutline> Outlines = new List<PreviewOutline>();
        int Index = 0;
        while (Index < Body.Length)
        {
            int Open = Body.IndexOf('<', Index);
            if (Open < 0)
            {
                break;
            }

            int End = Body.IndexOf('>', Open);
            if (End < 0)
            {
                break;
            }

            string Tag = Body.Substring(Open, End - Open + 1);
            Index = End + 1;
            string Name = TagName(Tag);
            if (Name.Length == 0)
            {
                continue;
            }

            float[]? Dash = ReadDash(Tag);
            if (Name == "path")
            {
                List<HoverSegment> Segments = new List<HoverSegment>();
                Flatten(ReadExactAttribute(Tag, "d"), ShiftX, ShiftY, Segments);
                AddSegmentOutlines(Segments, Dash, Outlines);
                continue;
            }

            PreviewOutline Outline = new PreviewOutline
            {
                Dash = Dash
            };
            if (Name == "rect")
            {
                double RadiusX = Number(Tag, "rx");
                double RadiusY = Number(Tag, "ry");
                AddRectangle(
                    Outline,
                    Number(Tag, "x") + ShiftX,
                    Number(Tag, "y") + ShiftY,
                    Number(Tag, "width"),
                    Number(Tag, "height"),
                    RadiusX > 0 ? RadiusX : RadiusY,
                    RadiusY > 0 ? RadiusY : RadiusX);
            }
            else if (Name == "ellipse" || Name == "circle")
            {
                double RadiusX = Name == "circle" ? Number(Tag, "r") : Number(Tag, "rx");
                double RadiusY = Name == "circle" ? RadiusX : Number(Tag, "ry");
                AddEllipse(Outline, Number(Tag, "cx") + ShiftX, Number(Tag, "cy") + ShiftY, RadiusX, RadiusY);
            }
            else if (Name == "line")
            {
                Outline.Points.Add(new PointF((float)(Number(Tag, "x1") + ShiftX), (float)(Number(Tag, "y1") + ShiftY)));
                Outline.Points.Add(new PointF((float)(Number(Tag, "x2") + ShiftX), (float)(Number(Tag, "y2") + ShiftY)));
            }
            else if (Name == "polygon" || Name == "polyline")
            {
                List<double> PointNumbers = Numbers(ReadExactAttribute(Tag, "points"));
                for (int Point = 0; Point + 1 < PointNumbers.Count; Point += 2)
                {
                    Outline.Points.Add(new PointF((float)(PointNumbers[Point] + ShiftX), (float)(PointNumbers[Point + 1] + ShiftY)));
                }

                if (Name == "polygon" && Outline.Points.Count > 2)
                {
                    Outline.Points.Add(Outline.Points[0]);
                }
            }

            if (Outline.Points.Count >= 2)
            {
                Outlines.Add(Outline);
            }
        }

        return Outlines;
    }

    private static string TagName(string Tag)
    {
        string[] Names = { "path", "rect", "ellipse", "circle", "line", "polygon", "polyline" };
        foreach (string Name in Names)
        {
            if (Tag.Length > Name.Length + 1
                && string.CompareOrdinal(Tag, 1, Name, 0, Name.Length) == 0
                && (char.IsWhiteSpace(Tag[Name.Length + 1]) || Tag[Name.Length + 1] == '/' || Tag[Name.Length + 1] == '>'))
            {
                return Name;
            }
        }

        return string.Empty;
    }

    private static void AddSegmentOutlines(List<HoverSegment> Segments, float[]? Dash, List<PreviewOutline> Outlines)
    {
        PreviewOutline? Current = null;
        foreach (HoverSegment Segment in Segments)
        {
            PointF From = new PointF((float)Segment.X1, (float)Segment.Y1);
            PointF To = new PointF((float)Segment.X2, (float)Segment.Y2);
            if (Current == null || !Near(Current.Points[Current.Points.Count - 1], From))
            {
                Current = new PreviewOutline
                {
                    Dash = Dash
                };
                Current.Points.Add(From);
                Outlines.Add(Current);
            }

            Current.Points.Add(To);
        }
    }

    private static bool Near(PointF Left, PointF Right)
    {
        return Math.Abs(Left.X - Right.X) < 0.01 && Math.Abs(Left.Y - Right.Y) < 0.01;
    }

    private static void AddRectangle(PreviewOutline Outline, double X, double Y, double Width, double Height, double RadiusX, double RadiusY)
    {
        RadiusX = Math.Max(0, Math.Min(RadiusX, Width / 2.0));
        RadiusY = Math.Max(0, Math.Min(RadiusY, Height / 2.0));
        if (RadiusX <= 0 || RadiusY <= 0)
        {
            Outline.Points.Add(new PointF((float)X, (float)Y));
            Outline.Points.Add(new PointF((float)(X + Width), (float)Y));
            Outline.Points.Add(new PointF((float)(X + Width), (float)(Y + Height)));
            Outline.Points.Add(new PointF((float)X, (float)(Y + Height)));
            Outline.Points.Add(new PointF((float)X, (float)Y));
            return;
        }

        Outline.Points.Add(new PointF((float)(X + RadiusX), (float)Y));
        AddCorner(Outline, X + Width - RadiusX, Y + RadiusY, RadiusX, RadiusY, -90);
        AddCorner(Outline, X + Width - RadiusX, Y + Height - RadiusY, RadiusX, RadiusY, 0);
        AddCorner(Outline, X + RadiusX, Y + Height - RadiusY, RadiusX, RadiusY, 90);
        AddCorner(Outline, X + RadiusX, Y + RadiusY, RadiusX, RadiusY, 180);
        Outline.Points.Add(new PointF((float)(X + RadiusX), (float)Y));
    }

    private static void AddCorner(PreviewOutline Outline, double CentreX, double CentreY, double RadiusX, double RadiusY, double StartDegrees)
    {
        for (int Step = 0; Step <= 6; Step++)
        {
            double Angle = (StartDegrees + (Step * 15.0)) * Math.PI / 180.0;
            Outline.Points.Add(new PointF((float)(CentreX + (RadiusX * Math.Cos(Angle))), (float)(CentreY + (RadiusY * Math.Sin(Angle)))));
        }
    }

    private static void AddEllipse(PreviewOutline Outline, double CentreX, double CentreY, double RadiusX, double RadiusY)
    {
        if (RadiusX <= 0 || RadiusY <= 0)
        {
            return;
        }

        for (int Step = 0; Step <= 48; Step++)
        {
            double Angle = Step * Math.PI * 2.0 / 48.0;
            Outline.Points.Add(new PointF((float)(CentreX + (RadiusX * Math.Cos(Angle))), (float)(CentreY + (RadiusY * Math.Sin(Angle)))));
        }
    }

    private static float[]? ReadDash(string Tag)
    {
        List<double> Values = Numbers(ReadExactAttribute(Tag, "stroke-dasharray"));
        if (Values.Count == 0)
        {
            return null;
        }

        float[] Dash = new float[Values.Count];
        double Total = 0;
        for (int Index = 0; Index < Values.Count; Index++)
        {
            Dash[Index] = (float)Math.Max(0, Values[Index]);
            Total += Dash[Index];
        }

        return Total > 0 ? Dash : null;
    }

    private static List<double> Numbers(string Text)
    {
        List<double> Values = new List<double>();
        foreach (string Part in Text.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(Part, NumberStyles.Float, CultureInfo.InvariantCulture, out double Value))
            {
                Values.Add(Value);
            }
        }

        return Values;
    }

    private static double Number(string Tag, string Name)
    {
        return ParseNumber(ReadExactAttribute(Tag, Name));
    }

    /// <summary>
    /// Reads an attribute by its whole name, so width does not match stroke-width.
    /// </summary>
    private static string ReadExactAttribute(string Tag, string Name)
    {
        string Marker = Name + "=\"";
        int Search = 0;
        while (Search < Tag.Length)
        {
            int Start = Tag.IndexOf(Marker, Search, StringComparison.Ordinal);
            if (Start < 0)
            {
                return string.Empty;
            }

            if (Start > 0 && char.IsWhiteSpace(Tag[Start - 1]))
            {
                int ValueStart = Start + Marker.Length;
                int End = Tag.IndexOf('"', ValueStart);
                return End < 0 ? string.Empty : Unescape(Tag.Substring(ValueStart, End - ValueStart));
            }

            Search = Start + Marker.Length;
        }

        return string.Empty;
    }
}
