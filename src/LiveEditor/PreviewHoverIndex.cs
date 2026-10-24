using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Maps a point in the preview picture back to the node or relationship under it.
/// </summary>
internal sealed class PreviewHoverIndex
{
    private const double EDGE_TOLERANCE = 8.0;

    private readonly List<HoverShape> _Shapes = new List<HoverShape>();

    private readonly List<HoverStroke> _Strokes = new List<HoverStroke>();

    private double _Width;

    private double _Height;

    private PreviewHoverIndex()
    {
    }

    /// <summary>Gets the SVG width in user units.</summary>
    public double Width
    {
        get
        {
            return _Width;
        }
    }

    /// <summary>Gets the SVG height in user units.</summary>
    public double Height
    {
        get
        {
            return _Height;
        }
    }

    /// <summary>
    /// Builds a hit index from the rendered SVG and the editor source.
    /// </summary>
    /// <param name="Svg">Last rendered SVG.</param>
    /// <param name="Source">Editor text that produced the SVG.</param>
    /// <returns>An index, empty when the source is not an enterprise diagram.</returns>
    public static PreviewHoverIndex Build(string? Svg, string? Source)
    {
        PreviewHoverIndex Index = new PreviewHoverIndex();
        if (string.IsNullOrWhiteSpace(Svg) || !EnterpriseParser.IsEnterpriseSource(Source))
        {
            return Index;
        }

        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Source);
        Index.ReadSize(Svg);
        Index.ReadGeometry(Svg, Parsed.Document);
        return Index;
    }

    /// <summary>
    /// Returns the hover sentence for a point in SVG user units.
    /// </summary>
    /// <param name="SvgX">Horizontal position.</param>
    /// <param name="SvgY">Vertical position.</param>
    /// <returns>The node type or relationship sentence, or null when nothing is under the point.</returns>
    public string? Hit(double SvgX, double SvgY)
    {
        for (int Index = _Shapes.Count - 1; Index >= 0; Index--)
        {
            HoverShape Shape = _Shapes[Index];
            if (SvgX >= Shape.Left && SvgX <= Shape.Right && SvgY >= Shape.Top && SvgY <= Shape.Bottom)
            {
                return Shape.Text;
            }
        }

        string? BestText = null;
        double BestDistance = EDGE_TOLERANCE;
        foreach (HoverStroke Stroke in _Strokes)
        {
            foreach (HoverSegment Segment in Stroke.Segments)
            {
                double Distance = DistanceToSegment(SvgX, SvgY, Segment);
                if (Distance <= BestDistance)
                {
                    BestDistance = Distance;
                    BestText = Stroke.Text;
                }
            }
        }

        return BestText;
    }

    /// <summary>
    /// Converts a picture-box client point into SVG user units when the picture is zoomed to fit.
    /// </summary>
    /// <param name="ClientX">Pointer position in the picture box.</param>
    /// <param name="ClientY">Pointer position in the picture box.</param>
    /// <param name="ControlWidth">Picture box client width.</param>
    /// <param name="ControlHeight">Picture box client height.</param>
    /// <param name="ImageWidth">Raster width in pixels.</param>
    /// <param name="ImageHeight">Raster height in pixels.</param>
    /// <param name="SvgWidth">SVG width.</param>
    /// <param name="SvgHeight">SVG height.</param>
    /// <param name="SvgX">SVG horizontal position.</param>
    /// <param name="SvgY">SVG vertical position.</param>
    /// <returns>True when the pointer is over the drawn image.</returns>
    public static bool TryMapClientToSvg(
        int ClientX,
        int ClientY,
        int ControlWidth,
        int ControlHeight,
        int ImageWidth,
        int ImageHeight,
        double SvgWidth,
        double SvgHeight,
        out double SvgX,
        out double SvgY)
    {
        SvgX = 0;
        SvgY = 0;
        if (ControlWidth <= 0 || ControlHeight <= 0 || ImageWidth <= 0 || ImageHeight <= 0 || SvgWidth <= 0 || SvgHeight <= 0)
        {
            return false;
        }

        double Scale = Math.Min(ControlWidth / (double)ImageWidth, ControlHeight / (double)ImageHeight);
        if (Scale <= 0)
        {
            return false;
        }

        double DisplayWidth = ImageWidth * Scale;
        double DisplayHeight = ImageHeight * Scale;
        double OffsetX = (ControlWidth - DisplayWidth) / 2.0;
        double OffsetY = (ControlHeight - DisplayHeight) / 2.0;
        if (ClientX < OffsetX || ClientY < OffsetY || ClientX > OffsetX + DisplayWidth || ClientY > OffsetY + DisplayHeight)
        {
            return false;
        }

        double ImageX = (ClientX - OffsetX) / Scale;
        double ImageY = (ClientY - OffsetY) / Scale;
        SvgX = ImageX / ImageWidth * SvgWidth;
        SvgY = ImageY / ImageHeight * SvgHeight;
        return true;
    }

    private void ReadSize(string Svg)
    {
        int Start = Svg.IndexOf("<svg", StringComparison.Ordinal);
        if (Start < 0)
        {
            return;
        }

        int End = Svg.IndexOf('>', Start);
        if (End < 0)
        {
            return;
        }

        string Tag = Svg.Substring(Start, End - Start);
        _Width = ReadAttribute(Tag, "width");
        _Height = ReadAttribute(Tag, "height");
    }

    private void ReadGeometry(string Svg, EnterpriseDocument Document)
    {
        double ShiftX = 0;
        double ShiftY = 0;
        int Translate = Svg.IndexOf("translate(", StringComparison.Ordinal);
        if (Translate >= 0)
        {
            int Open = Translate + "translate(".Length;
            int Close = Svg.IndexOf(')', Open);
            if (Close > Open)
            {
                string Body = Svg.Substring(Open, Close - Open);
                string[] Parts = Body.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (Parts.Length >= 2)
                {
                    ShiftX = ParseNumber(Parts[0]);
                    ShiftY = ParseNumber(Parts[1]);
                }
            }
        }

        int Index = 0;
        while (Index < Svg.Length)
        {
            int Group = Svg.IndexOf("<g ", Index, StringComparison.Ordinal);
            if (Group < 0)
            {
                break;
            }

            int TagEnd = Svg.IndexOf('>', Group);
            if (TagEnd < 0)
            {
                break;
            }

            string Tag = Svg.Substring(Group, TagEnd - Group);
            string ClassName = ReadStringAttribute(Tag, "class");
            bool IsShape = ClassName.StartsWith("element ", StringComparison.Ordinal)
                || ClassName.Equals("group", StringComparison.Ordinal)
                || ClassName.Equals("note", StringComparison.Ordinal);
            bool IsStroke = ClassName.StartsWith("relationship ", StringComparison.Ordinal)
                || ClassName.Equals("plain-connection", StringComparison.Ordinal);
            if (!IsShape && !IsStroke)
            {
                Index = TagEnd + 1;
                continue;
            }

            int Close = FindGroupClose(Svg, TagEnd);
            if (Close < 0)
            {
                break;
            }

            string Body = Svg.Substring(TagEnd + 1, Close - TagEnd - 1);
            Index = Close + 4;
            if (IsShape)
            {
                AddShape(Document, Tag, Body, ShiftX, ShiftY);
            }
            else
            {
                AddStroke(Document, Tag, ClassName, Body, ShiftX, ShiftY);
            }
        }
    }

    private void AddShape(EnterpriseDocument Document, string Tag, string Body, double ShiftX, double ShiftY)
    {
        string Id = ReadStringAttribute(Tag, "data-id");
        if (Id.Length == 0 || !TryBounds(Body, ShiftX, ShiftY, out double Left, out double Top, out double Right, out double Bottom))
        {
            return;
        }

        HoverShape Shape = new HoverShape();
        Shape.Left = Left;
        Shape.Top = Top;
        Shape.Right = Right;
        Shape.Bottom = Bottom;
        Shape.Text = ShapeSentence(Document, Id);
        _Shapes.Add(Shape);
    }

    private void AddStroke(EnterpriseDocument Document, string Tag, string ClassName, string Body, double ShiftX, double ShiftY)
    {
        string SourceId = ReadStringAttribute(Tag, "data-source");
        string TargetId = ReadStringAttribute(Tag, "data-target");
        if (SourceId.Length == 0 || TargetId.Length == 0)
        {
            return;
        }

        List<HoverSegment> Segments = new List<HoverSegment>();
        int Path = 0;
        while (Path < Body.Length)
        {
            int Marker = Body.IndexOf(" d=\"", Path, StringComparison.Ordinal);
            if (Marker < 0)
            {
                break;
            }

            int Start = Marker + 4;
            int End = Body.IndexOf('"', Start);
            if (End < 0)
            {
                break;
            }

            Flatten(Body.Substring(Start, End - Start), ShiftX, ShiftY, Segments);
            Path = End + 1;
        }

        if (Segments.Count == 0)
        {
            return;
        }

        string KindName = string.Empty;
        if (ClassName.StartsWith("relationship ", StringComparison.Ordinal))
        {
            KindName = ClassName.Substring("relationship ".Length).Trim();
        }

        HoverStroke Stroke = new HoverStroke();
        Stroke.Text = RelationshipSentence(Document, SourceId, TargetId, KindName);
        Stroke.Segments = Segments;
        _Strokes.Add(Stroke);
    }

    private static string ShapeSentence(EnterpriseDocument Document, string Id)
    {
        if (Document.Elements.TryGetValue(Id, out EnterpriseElement? Element))
        {
            string TypeName = TypeSentence(Element);
            return NameOf(Element.Label, Id) + " is " + Article(TypeName) + " " + TypeName;
        }

        if (Document.Groups.TryGetValue(Id, out EnterpriseGroup? Group))
        {
            return NameOf(Group.Label, Id) + " is a group";
        }

        if (Document.Notes.TryGetValue(Id, out EnterpriseNote? Note))
        {
            return NameOf(Note.Label, Id) + " is a note";
        }

        return Id;
    }

    private static string TypeSentence(EnterpriseElement Element)
    {
        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            if (Element.JunctionKind == EnterpriseJunctionKind.Or)
            {
                return "or junction";
            }

            return "and junction";
        }

        return ElementShapePainter.DisplayName(EnterpriseElementCatalog.GetKebabName(Element.ElementType)).ToLowerInvariant();
    }

    private static string RelationshipSentence(EnterpriseDocument Document, string SourceId, string TargetId, string KindName)
    {
        EnterpriseRelationship? Match = null;
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            if (!string.Equals(Relationship.SourceId, SourceId, StringComparison.Ordinal)
                || !string.Equals(Relationship.TargetId, TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (KindName.Length > 0
                && !string.Equals(EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind), KindName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Match = Relationship;
            break;
        }

        string SourceName = ElementName(Document, SourceId);
        string TargetName = ElementName(Document, TargetId);
        if (Match == null)
        {
            return SourceName + " connects to " + TargetName;
        }

        string Sentence = SourceName + " " + Verb(Match) + " " + TargetName;
        if (Match.Label.Length > 0)
        {
            Sentence = Sentence + ": " + Match.Label;
        }

        return Sentence;
    }

    private static string Verb(EnterpriseRelationship Relationship)
    {
        switch (Relationship.Kind)
        {
            case EnterpriseRelationshipKind.Composition:
                return "is composed of";
            case EnterpriseRelationshipKind.Aggregation:
                return "aggregates";
            case EnterpriseRelationshipKind.Assignment:
                return "is assigned to";
            case EnterpriseRelationshipKind.Realization:
                return "realises";
            case EnterpriseRelationshipKind.Serving:
                return "serves";
            case EnterpriseRelationshipKind.Access:
                return AccessVerb(Relationship.AccessKind);
            case EnterpriseRelationshipKind.Influence:
                return InfluenceVerb(Relationship.InfluenceStrength);
            case EnterpriseRelationshipKind.Triggering:
                return "triggers";
            case EnterpriseRelationshipKind.Flow:
                return "flows to";
            case EnterpriseRelationshipKind.Specialization:
                return "specialises";
            case EnterpriseRelationshipKind.Association:
                if (Relationship.DirectedAssociation)
                {
                    return "is directed toward";
                }

                return "is associated with";
            default:
                return "connects to";
        }
    }

    private static string AccessVerb(EnterpriseAccessKind? Kind)
    {
        switch (Kind)
        {
            case EnterpriseAccessKind.Read:
                return "reads";
            case EnterpriseAccessKind.ReadWrite:
                return "reads and writes";
            case EnterpriseAccessKind.Access:
                return "accesses";
            default:
                return "writes";
        }
    }

    private static string InfluenceVerb(string? Strength)
    {
        switch (Strength)
        {
            case "+":
                return "positively influences";
            case "++":
                return "strongly influences";
            case "-":
                return "negatively influences";
            case "--":
                return "strongly opposes";
            default:
                return "influences";
        }
    }

    private static string ElementName(EnterpriseDocument Document, string Id)
    {
        if (Document.Elements.TryGetValue(Id, out EnterpriseElement? Element))
        {
            return NameOf(Element.Label, Id);
        }

        if (Document.Groups.TryGetValue(Id, out EnterpriseGroup? Group))
        {
            return NameOf(Group.Label, Id);
        }

        return Id;
    }

    private static string NameOf(string Label, string Id)
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            return Id;
        }

        return Label;
    }

    private static string Article(string TypeName)
    {
        if (TypeName.Length == 0)
        {
            return "a";
        }

        char First = char.ToLowerInvariant(TypeName[0]);
        if (First == 'a' || First == 'e' || First == 'i' || First == 'o' || First == 'u')
        {
            return "an";
        }

        return "a";
    }

    private static bool TryBounds(string Body, double ShiftX, double ShiftY, out double Left, out double Top, out double Right, out double Bottom)
    {
        Left = double.MaxValue;
        Top = double.MaxValue;
        Right = double.MinValue;
        Bottom = double.MinValue;
        IncludeBoxes(Body, ShiftX, ShiftY, ref Left, ref Top, ref Right, ref Bottom);
        IncludeEllipses(Body, ShiftX, ShiftY, ref Left, ref Top, ref Right, ref Bottom);
        IncludeCircles(Body, ShiftX, ShiftY, ref Left, ref Top, ref Right, ref Bottom);
        IncludeLines(Body, ShiftX, ShiftY, ref Left, ref Top, ref Right, ref Bottom);
        IncludePolygons(Body, ShiftX, ShiftY, ref Left, ref Top, ref Right, ref Bottom);
        IncludePaths(Body, ShiftX, ShiftY, ref Left, ref Top, ref Right, ref Bottom);
        IncludeText(Body, ShiftX, ShiftY, ref Left, ref Top, ref Right, ref Bottom);
        return Right > Left && Bottom > Top;
    }

    private static int FindGroupClose(string Svg, int TagEnd)
    {
        int Depth = 1;
        int Index = TagEnd + 1;
        while (Index < Svg.Length && Depth > 0)
        {
            int NextOpen = Svg.IndexOf("<g", Index, StringComparison.Ordinal);
            int NextClose = Svg.IndexOf("</g>", Index, StringComparison.Ordinal);
            if (NextClose < 0)
            {
                return -1;
            }

            if (NextOpen >= 0 && NextOpen < NextClose && IsGroupOpen(Svg, NextOpen))
            {
                Depth++;
                Index = NextOpen + 2;
            }
            else
            {
                Depth--;
                if (Depth == 0)
                {
                    return NextClose;
                }

                Index = NextClose + 4;
            }
        }

        return -1;
    }

    private static bool IsGroupOpen(string Svg, int Index)
    {
        if (Index + 2 >= Svg.Length)
        {
            return false;
        }

        char Next = Svg[Index + 2];
        return Next == ' ' || Next == '>' || Next == '\r' || Next == '\n' || Next == '\t';
    }

    private static void IncludeBoxes(string Body, double ShiftX, double ShiftY, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        int Index = 0;
        while (Index < Body.Length)
        {
            int Found = Body.IndexOf("<rect ", Index, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            int End = Body.IndexOf('>', Found);
            if (End < 0)
            {
                break;
            }

            string Tag = Body.Substring(Found, End - Found);
            double X = ReadAttribute(Tag, "x") + ShiftX;
            double Y = ReadAttribute(Tag, "y") + ShiftY;
            double Width = ReadAttribute(Tag, "width");
            double Height = ReadAttribute(Tag, "height");
            Include(X, Y, ref Left, ref Top, ref Right, ref Bottom);
            Include(X + Width, Y + Height, ref Left, ref Top, ref Right, ref Bottom);
            Index = End + 1;
        }
    }

    private static void IncludeEllipses(string Body, double ShiftX, double ShiftY, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        int Index = 0;
        while (Index < Body.Length)
        {
            int Found = Body.IndexOf("<ellipse ", Index, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            int End = Body.IndexOf('>', Found);
            if (End < 0)
            {
                break;
            }

            string Tag = Body.Substring(Found, End - Found);
            double CentreX = ReadAttribute(Tag, "cx") + ShiftX;
            double CentreY = ReadAttribute(Tag, "cy") + ShiftY;
            double RadiusX = ReadAttribute(Tag, "rx");
            double RadiusY = ReadAttribute(Tag, "ry");
            Include(CentreX - RadiusX, CentreY - RadiusY, ref Left, ref Top, ref Right, ref Bottom);
            Include(CentreX + RadiusX, CentreY + RadiusY, ref Left, ref Top, ref Right, ref Bottom);
            Index = End + 1;
        }
    }

    private static void IncludeCircles(string Body, double ShiftX, double ShiftY, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        int Index = 0;
        while (Index < Body.Length)
        {
            int Found = Body.IndexOf("<circle ", Index, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            int End = Body.IndexOf('>', Found);
            if (End < 0)
            {
                break;
            }

            string Tag = Body.Substring(Found, End - Found);
            double CentreX = ReadAttribute(Tag, "cx") + ShiftX;
            double CentreY = ReadAttribute(Tag, "cy") + ShiftY;
            double Radius = ReadAttribute(Tag, "r");
            Include(CentreX - Radius, CentreY - Radius, ref Left, ref Top, ref Right, ref Bottom);
            Include(CentreX + Radius, CentreY + Radius, ref Left, ref Top, ref Right, ref Bottom);
            Index = End + 1;
        }
    }

    private static void IncludeLines(string Body, double ShiftX, double ShiftY, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        int Index = 0;
        while (Index < Body.Length)
        {
            int Found = Body.IndexOf("<line ", Index, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            int End = Body.IndexOf('>', Found);
            if (End < 0)
            {
                break;
            }

            string Tag = Body.Substring(Found, End - Found);
            Include(ReadAttribute(Tag, "x1") + ShiftX, ReadAttribute(Tag, "y1") + ShiftY, ref Left, ref Top, ref Right, ref Bottom);
            Include(ReadAttribute(Tag, "x2") + ShiftX, ReadAttribute(Tag, "y2") + ShiftY, ref Left, ref Top, ref Right, ref Bottom);
            Index = End + 1;
        }
    }

    private static void IncludePolygons(string Body, double ShiftX, double ShiftY, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        int Index = 0;
        while (Index < Body.Length)
        {
            int Found = Body.IndexOf("<polygon ", Index, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            int End = Body.IndexOf('>', Found);
            if (End < 0)
            {
                break;
            }

            string Points = ReadStringAttribute(Body.Substring(Found, End - Found), "points");
            List<double> Numbers = new List<double>();
            StringBuilder Number = new StringBuilder();
            for (int CharacterIndex = 0; CharacterIndex < Points.Length; CharacterIndex++)
            {
                char Character = Points[CharacterIndex];
                if (Character == ',' || char.IsWhiteSpace(Character))
                {
                    FlushNumber(Number, Numbers);
                }
                else
                {
                    Number.Append(Character);
                }
            }

            FlushNumber(Number, Numbers);
            for (int Point = 0; Point + 1 < Numbers.Count; Point += 2)
            {
                Include(Numbers[Point] + ShiftX, Numbers[Point + 1] + ShiftY, ref Left, ref Top, ref Right, ref Bottom);
            }

            Index = End + 1;
        }
    }

    private static void IncludeText(string Body, double ShiftX, double ShiftY, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        int Index = 0;
        while (Index < Body.Length)
        {
            int Found = Body.IndexOf("<text ", Index, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            int End = Body.IndexOf('>', Found);
            if (End < 0)
            {
                break;
            }

            int ContentEnd = Body.IndexOf('<', End + 1);
            if (ContentEnd < 0)
            {
                break;
            }

            string Tag = Body.Substring(Found, End - Found);
            string Content = Unescape(Body.Substring(End + 1, ContentEnd - End - 1)).Trim();
            double FontSize = ReadAttribute(Tag, "font-size");
            if (FontSize <= 0)
            {
                FontSize = 12;
            }

            double Width = Math.Max(FontSize, Content.Length * FontSize * 0.6);
            double X = ReadAttribute(Tag, "x") + ShiftX;
            double Y = ReadAttribute(Tag, "y") + ShiftY;
            string Anchor = ReadStringAttribute(Tag, "text-anchor");
            double TextLeft = X;
            if (Anchor == "middle")
            {
                TextLeft = X - (Width / 2.0);
            }
            else if (Anchor == "end")
            {
                TextLeft = X - Width;
            }

            Include(TextLeft, Y - FontSize, ref Left, ref Top, ref Right, ref Bottom);
            Include(TextLeft + Width, Y + (FontSize * 0.25), ref Left, ref Top, ref Right, ref Bottom);
            Index = ContentEnd + 1;
        }
    }

    private static void IncludePaths(string Body, double ShiftX, double ShiftY, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        List<HoverSegment> Segments = new List<HoverSegment>();
        int Index = 0;
        while (Index < Body.Length)
        {
            int Marker = Body.IndexOf(" d=\"", Index, StringComparison.Ordinal);
            if (Marker < 0)
            {
                break;
            }

            int Start = Marker + 4;
            int End = Body.IndexOf('"', Start);
            if (End < 0)
            {
                break;
            }

            Flatten(Body.Substring(Start, End - Start), ShiftX, ShiftY, Segments);
            Index = End + 1;
        }

        foreach (HoverSegment Segment in Segments)
        {
            Include(Segment.X1, Segment.Y1, ref Left, ref Top, ref Right, ref Bottom);
            Include(Segment.X2, Segment.Y2, ref Left, ref Top, ref Right, ref Bottom);
        }
    }

    private static void Include(double X, double Y, ref double Left, ref double Top, ref double Right, ref double Bottom)
    {
        if (X < Left)
        {
            Left = X;
        }

        if (Y < Top)
        {
            Top = Y;
        }

        if (X > Right)
        {
            Right = X;
        }

        if (Y > Bottom)
        {
            Bottom = Y;
        }
    }

    private static void Flatten(string Data, double ShiftX, double ShiftY, List<HoverSegment> Segments)
    {
        List<double> Numbers = new List<double>();
        List<char> Commands = new List<char>();
        List<int> CommandAt = new List<int>();
        StringBuilder Number = new StringBuilder();
        for (int Index = 0; Index < Data.Length; Index++)
        {
            char Character = Data[Index];
            if (IsCommand(Character))
            {
                FlushNumber(Number, Numbers);
                Commands.Add(Character);
                CommandAt.Add(Numbers.Count);
            }
            else if (Character == ',' || char.IsWhiteSpace(Character))
            {
                FlushNumber(Number, Numbers);
            }
            else
            {
                Number.Append(Character);
            }
        }

        FlushNumber(Number, Numbers);
        CommandAt.Add(Numbers.Count);
        double CurrentX = 0;
        double CurrentY = 0;
        double StartX = 0;
        double StartY = 0;
        double LastControlX = 0;
        double LastControlY = 0;
        for (int CommandIndex = 0; CommandIndex < Commands.Count; CommandIndex++)
        {
            char Command = Commands[CommandIndex];
            int From = CommandAt[CommandIndex];
            int To = CommandAt[CommandIndex + 1];
            bool Relative = char.IsLower(Command);
            char Absolute = char.ToUpperInvariant(Command);
            int Cursor = From;
            if (Absolute == 'M')
            {
                bool First = true;
                while (Cursor + 1 < To)
                {
                    double X = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y = Take(Numbers, ref Cursor, Relative, CurrentY);
                    if (!First)
                    {
                        AddSegment(CurrentX, CurrentY, X, Y, ShiftX, ShiftY, Segments);
                    }

                    CurrentX = X;
                    CurrentY = Y;
                    if (First)
                    {
                        StartX = X;
                        StartY = Y;
                        First = false;
                    }
                }
            }
            else if (Absolute == 'L')
            {
                while (Cursor + 1 < To)
                {
                    double X = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y = Take(Numbers, ref Cursor, Relative, CurrentY);
                    AddSegment(CurrentX, CurrentY, X, Y, ShiftX, ShiftY, Segments);
                    CurrentX = X;
                    CurrentY = Y;
                }
            }
            else if (Absolute == 'H')
            {
                while (Cursor < To)
                {
                    double X = Take(Numbers, ref Cursor, Relative, CurrentX);
                    AddSegment(CurrentX, CurrentY, X, CurrentY, ShiftX, ShiftY, Segments);
                    CurrentX = X;
                }
            }
            else if (Absolute == 'V')
            {
                while (Cursor < To)
                {
                    double Y = Take(Numbers, ref Cursor, Relative, CurrentY);
                    AddSegment(CurrentX, CurrentY, CurrentX, Y, ShiftX, ShiftY, Segments);
                    CurrentY = Y;
                }
            }
            else if (Absolute == 'C')
            {
                while (Cursor + 5 < To)
                {
                    double X1 = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y1 = Take(Numbers, ref Cursor, Relative, CurrentY);
                    double X2 = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y2 = Take(Numbers, ref Cursor, Relative, CurrentY);
                    double X = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y = Take(Numbers, ref Cursor, Relative, CurrentY);
                    SampleCubic(CurrentX, CurrentY, X1, Y1, X2, Y2, X, Y, ShiftX, ShiftY, Segments);
                    LastControlX = X2;
                    LastControlY = Y2;
                    CurrentX = X;
                    CurrentY = Y;
                }
            }
            else if (Absolute == 'S')
            {
                while (Cursor + 3 < To)
                {
                    double X1 = CurrentX * 2.0 - LastControlX;
                    double Y1 = CurrentY * 2.0 - LastControlY;
                    double X2 = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y2 = Take(Numbers, ref Cursor, Relative, CurrentY);
                    double X = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y = Take(Numbers, ref Cursor, Relative, CurrentY);
                    SampleCubic(CurrentX, CurrentY, X1, Y1, X2, Y2, X, Y, ShiftX, ShiftY, Segments);
                    LastControlX = X2;
                    LastControlY = Y2;
                    CurrentX = X;
                    CurrentY = Y;
                }
            }
            else if (Absolute == 'Q')
            {
                while (Cursor + 3 < To)
                {
                    double X1 = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y1 = Take(Numbers, ref Cursor, Relative, CurrentY);
                    double X = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y = Take(Numbers, ref Cursor, Relative, CurrentY);
                    SampleQuadratic(CurrentX, CurrentY, X1, Y1, X, Y, ShiftX, ShiftY, Segments);
                    LastControlX = X1;
                    LastControlY = Y1;
                    CurrentX = X;
                    CurrentY = Y;
                }
            }
            else if (Absolute == 'A')
            {
                while (Cursor + 6 < To)
                {
                    Take(Numbers, ref Cursor, false, 0);
                    Take(Numbers, ref Cursor, false, 0);
                    Take(Numbers, ref Cursor, false, 0);
                    Take(Numbers, ref Cursor, false, 0);
                    Take(Numbers, ref Cursor, false, 0);
                    double X = Take(Numbers, ref Cursor, Relative, CurrentX);
                    double Y = Take(Numbers, ref Cursor, Relative, CurrentY);
                    AddSegment(CurrentX, CurrentY, X, Y, ShiftX, ShiftY, Segments);
                    CurrentX = X;
                    CurrentY = Y;
                }
            }
            else if (Absolute == 'Z')
            {
                AddSegment(CurrentX, CurrentY, StartX, StartY, ShiftX, ShiftY, Segments);
                CurrentX = StartX;
                CurrentY = StartY;
            }
        }
    }

    private static bool IsCommand(char Character)
    {
        char Upper = char.ToUpperInvariant(Character);
        return Upper == 'M' || Upper == 'L' || Upper == 'H' || Upper == 'V' || Upper == 'C' || Upper == 'S' || Upper == 'Q' || Upper == 'A' || Upper == 'Z';
    }

    private static void FlushNumber(StringBuilder Number, List<double> Numbers)
    {
        if (Number.Length == 0)
        {
            return;
        }

        Numbers.Add(ParseNumber(Number.ToString()));
        Number.Clear();
    }

    private static double Take(List<double> Numbers, ref int Cursor, bool Relative, double Origin)
    {
        double Value = Numbers[Cursor];
        Cursor++;
        if (Relative)
        {
            return Origin + Value;
        }

        return Value;
    }

    private static void AddSegment(double X1, double Y1, double X2, double Y2, double ShiftX, double ShiftY, List<HoverSegment> Segments)
    {
        if (Math.Abs(X1 - X2) < 0.01 && Math.Abs(Y1 - Y2) < 0.01)
        {
            return;
        }

        HoverSegment Segment = new HoverSegment();
        Segment.X1 = X1 + ShiftX;
        Segment.Y1 = Y1 + ShiftY;
        Segment.X2 = X2 + ShiftX;
        Segment.Y2 = Y2 + ShiftY;
        Segments.Add(Segment);
    }

    private static void SampleCubic(
        double X0,
        double Y0,
        double X1,
        double Y1,
        double X2,
        double Y2,
        double X3,
        double Y3,
        double ShiftX,
        double ShiftY,
        List<HoverSegment> Segments)
    {
        double PreviousX = X0;
        double PreviousY = Y0;
        for (int Step = 1; Step <= 8; Step++)
        {
            double T = Step / 8.0;
            double U = 1.0 - T;
            double X = (U * U * U * X0) + (3.0 * U * U * T * X1) + (3.0 * U * T * T * X2) + (T * T * T * X3);
            double Y = (U * U * U * Y0) + (3.0 * U * U * T * Y1) + (3.0 * U * T * T * Y2) + (T * T * T * Y3);
            AddSegment(PreviousX, PreviousY, X, Y, ShiftX, ShiftY, Segments);
            PreviousX = X;
            PreviousY = Y;
        }
    }

    private static void SampleQuadratic(
        double X0,
        double Y0,
        double X1,
        double Y1,
        double X2,
        double Y2,
        double ShiftX,
        double ShiftY,
        List<HoverSegment> Segments)
    {
        double PreviousX = X0;
        double PreviousY = Y0;
        for (int Step = 1; Step <= 8; Step++)
        {
            double T = Step / 8.0;
            double U = 1.0 - T;
            double X = (U * U * X0) + (2.0 * U * T * X1) + (T * T * X2);
            double Y = (U * U * Y0) + (2.0 * U * T * Y1) + (T * T * Y2);
            AddSegment(PreviousX, PreviousY, X, Y, ShiftX, ShiftY, Segments);
            PreviousX = X;
            PreviousY = Y;
        }
    }

    private static double DistanceToSegment(double X, double Y, HoverSegment Segment)
    {
        double DeltaX = Segment.X2 - Segment.X1;
        double DeltaY = Segment.Y2 - Segment.Y1;
        double LengthSquared = (DeltaX * DeltaX) + (DeltaY * DeltaY);
        double Ratio = 0;
        if (LengthSquared > 0)
        {
            Ratio = ((X - Segment.X1) * DeltaX + (Y - Segment.Y1) * DeltaY) / LengthSquared;
            if (Ratio < 0)
            {
                Ratio = 0;
            }
            else if (Ratio > 1)
            {
                Ratio = 1;
            }
        }

        double NearX = Segment.X1 + (Ratio * DeltaX);
        double NearY = Segment.Y1 + (Ratio * DeltaY);
        double OffsetX = X - NearX;
        double OffsetY = Y - NearY;
        return Math.Sqrt((OffsetX * OffsetX) + (OffsetY * OffsetY));
    }

    private static double ReadAttribute(string Tag, string Name)
    {
        return ParseNumber(ReadStringAttribute(Tag, Name));
    }

    private static string ReadStringAttribute(string Tag, string Name)
    {
        string Marker = Name + "=\"";
        int Start = Tag.IndexOf(Marker, StringComparison.Ordinal);
        if (Start < 0)
        {
            return string.Empty;
        }

        Start += Marker.Length;
        int End = Tag.IndexOf('"', Start);
        if (End < 0)
        {
            return string.Empty;
        }

        return Unescape(Tag.Substring(Start, End - Start));
    }

    private static string Unescape(string Value)
    {
        return Value.Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal);
    }

    private static double ParseNumber(string Text)
    {
        if (double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double Value))
        {
            return Value;
        }

        return 0;
    }

    private sealed class HoverShape
    {
        public double Left;

        public double Top;

        public double Right;

        public double Bottom;

        public string Text = string.Empty;
    }

    private sealed class HoverStroke
    {
        public string Text = string.Empty;

        public List<HoverSegment> Segments = new List<HoverSegment>();
    }

    private struct HoverSegment
    {
        public double X1;

        public double Y1;

        public double X2;

        public double Y2;
    }
}
