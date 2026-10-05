using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Parsed nodes, groups, edges, labels, and arrowheads from a rendered SVG.
/// </summary>
public sealed class FlowchartRenderedSvgDocument
{
    private static readonly Regex PathOrLineRegex = new Regex(
        "<(?:path|line)\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex GroupRegex = new Regex(
        "<g\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RectRegex = new Regex(
        "<rect\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PolygonRegex = new Regex(
        "<polygon\\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TextRegex = new Regex(
        "<text\\b[^>]*>(.*?)</text>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex = new Regex(
        "([a-zA-Z_:][\\w:.-]*)=\"([^\"]*)\"",
        RegexOptions.CultureInvariant);

    private FlowchartRenderedSvgDocument()
    {
    }

    /// <summary>
    /// Edge strokes with source and target identifiers.
    /// </summary>
    public List<FlowchartRenderedSvgStroke> Strokes { get; } = [];

    /// <summary>
    /// Node and group boxes in SVG coordinates.
    /// </summary>
    public List<FlowchartRenderedSvgBox> Boxes { get; } = [];

    /// <summary>
    /// Edge labels.
    /// </summary>
    public List<FlowchartRenderedSvgLabel> Labels { get; } = [];

    /// <summary>
    /// Arrowhead tips.
    /// </summary>
    public List<(double X, double Y)> Arrowheads { get; } = [];

    /// <summary>
    /// Parses a rendered flowchart or enterprise SVG.
    /// </summary>
    /// <param name="Svg">SVG markup.</param>
    /// <param name="Contract">Contract used to map labels to identifiers.</param>
    /// <returns>The parsed document.</returns>
    public static FlowchartRenderedSvgDocument Parse(string Svg, DiagramContract Contract)
    {
        FlowchartRenderedSvgDocument Document = new FlowchartRenderedSvgDocument();
        if (string.IsNullOrWhiteSpace(Svg))
        {
            return Document;
        }

        Dictionary<string, string> LabelToId = BuildLabelMap(Contract);
        ParseStrokes(Svg, Contract, Document);
        ParseBoxes(Svg, LabelToId, Document);
        ParseLabels(Svg, Contract, Document);
        ParseArrowheads(Svg, Document);
        return Document;
    }

    /// <summary>
    /// Finds a box by node or group identifier.
    /// </summary>
    /// <param name="NodeId">Identifier.</param>
    /// <returns>The box, or null.</returns>
    public FlowchartRenderedSvgBox? FindBox(string NodeId)
    {
        foreach (FlowchartRenderedSvgBox Box in Boxes)
        {
            if (string.Equals(Box.NodeId, NodeId, StringComparison.Ordinal))
            {
                return Box;
            }
        }

        return null;
    }

    private static Dictionary<string, string> BuildLabelMap(DiagramContract Contract)
    {
        Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (NodeContract Node in Contract.Nodes)
        {
            AddLabel(Map, Node.Label, Node.Id);
            AddLabel(Map, Node.Id, Node.Id);
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            AddLabel(Map, Group.Title, Group.Id);
            AddLabel(Map, Group.Id, Group.Id);
        }

        return Map;
    }

    private static void AddLabel(Dictionary<string, string> Map, string? Text, string Id)
    {
        string Normalised = NormaliseLabel(Text);
        if (Normalised.Length == 0)
        {
            return;
        }

        Map[Normalised] = Id;
    }

    private static string NormaliseLabel(string? Text)
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            return string.Empty;
        }

        return Regex.Replace(Text.Trim(), @"\s+", " ");
    }

    private static void ParseStrokes(string Svg, DiagramContract Contract, FlowchartRenderedSvgDocument Document)
    {
        string? OpenSource = null;
        string? OpenTarget = null;
        foreach (Match Match in Regex.Matches(
            Svg,
            "<g\\b[^>]*>|</g>|(<(?:path|line)\\b[^>]*>)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            string Token = Match.Value;
            if (Token.StartsWith("<g", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, string> Attributes = ParseAttributes(Token);
                Attributes.TryGetValue("data-source", out OpenSource);
                Attributes.TryGetValue("data-target", out OpenTarget);
                continue;
            }

            if (Token.StartsWith("</g", StringComparison.OrdinalIgnoreCase))
            {
                OpenSource = null;
                OpenTarget = null;
                continue;
            }

            Dictionary<string, string> StrokeAttributes = ParseAttributes(Token);
            if (!TryReadSamples(StrokeAttributes, out List<(double X, double Y)> Samples))
            {
                continue;
            }

            StrokeAttributes.TryGetValue("data-edge-source", out string? SourceId);
            StrokeAttributes.TryGetValue("data-edge-target", out string? TargetId);
            if (string.IsNullOrEmpty(SourceId))
            {
                SourceId = OpenSource;
            }

            if (string.IsNullOrEmpty(TargetId))
            {
                TargetId = OpenTarget;
            }

            if (string.IsNullOrEmpty(SourceId) || string.IsNullOrEmpty(TargetId))
            {
                continue;
            }

            if (!IsKnownEdge(Contract, SourceId, TargetId))
            {
                continue;
            }

            FlowchartRenderedSvgStroke? Existing = FindStroke(Document.Strokes, SourceId, TargetId);
            if (Existing is not null)
            {
                if (PathLength(Samples) > PathLength(Existing.Samples))
                {
                    Existing.Samples.Clear();
                    Existing.Samples.AddRange(Samples);
                }

                continue;
            }

            Document.Strokes.Add(new FlowchartRenderedSvgStroke(SourceId, TargetId, Samples));
        }
    }

    private static void ParseBoxes(
        string Svg,
        Dictionary<string, string> LabelToId,
        FlowchartRenderedSvgDocument Document)
    {
        string? OpenId = null;
        foreach (Match Match in Regex.Matches(
            Svg,
            "<g\\b[^>]*>|</g>|(<(?:rect|path)\\b[^>]*>)|(<text\\b[^>]*>.*?</text>)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string Token = Match.Value;
            if (Token.StartsWith("<g", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, string> Attributes = ParseAttributes(Token);
                Attributes.TryGetValue("data-id", out OpenId);
                continue;
            }

            if (Token.StartsWith("</g", StringComparison.OrdinalIgnoreCase))
            {
                OpenId = null;
                continue;
            }

            if (Token.StartsWith("<text", StringComparison.OrdinalIgnoreCase))
            {
                string Text = NormaliseLabel(Regex.Replace(Match.Groups[1].Success
                    ? Match.Groups[1].Value
                    : Regex.Replace(Token, "<[^>]+>", " "), @"\s+", " "));
                if (Text.Length == 0)
                {
                    Text = NormaliseLabel(Regex.Replace(Token, "<[^>]+>", " "));
                }

                if (Text.Length == 0 || !LabelToId.TryGetValue(Text, out string? MappedId))
                {
                    continue;
                }

                if (Document.FindBox(MappedId) is not null)
                {
                    continue;
                }

                Dictionary<string, string> TextAttributes = ParseAttributes(Token);
                if (TextAttributes.TryGetValue("x", out string? XText)
                    && TextAttributes.TryGetValue("y", out string? YText)
                    && TryParseDouble(XText, out double TextX)
                    && TryParseDouble(YText, out double TextY))
                {
                    FlowchartRenderedSvgBox? Nearest = FindNearestUnnamedBox(Document, TextX, TextY);
                    if (Nearest is not null && string.IsNullOrEmpty(Nearest.NodeId))
                    {
                        Nearest.NodeId = MappedId;
                    }
                    else
                    {
                        Document.Boxes.Add(new FlowchartRenderedSvgBox(
                            MappedId,
                            TextX - 20.0,
                            TextY - 20.0,
                            TextX + 20.0,
                            TextY + 20.0,
                            IsGroup: false));
                    }
                }

                continue;
            }

            Dictionary<string, string> ShapeAttributes = ParseAttributes(Token);
            if (ShapeAttributes.TryGetValue("data-edge-source", out _))
            {
                continue;
            }

            if (!TryReadBox(ShapeAttributes, Token, out double Left, out double Top, out double Right, out double Bottom))
            {
                continue;
            }

            if (Right - Left < 8.0 || Bottom - Top < 8.0)
            {
                continue;
            }

            if (ShapeAttributes.TryGetValue("fill", out string? Fill)
                && (string.Equals(Fill, "white", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Fill, "#ffffff", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Fill, "#fff", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            bool IsGroup = LooksLikeGroup(ShapeAttributes);
            string BoxId = OpenId ?? string.Empty;
            Document.Boxes.Add(new FlowchartRenderedSvgBox(BoxId, Left, Top, Right, Bottom, IsGroup));
        }
    }

    private static FlowchartRenderedSvgBox? FindNearestUnnamedBox(
        FlowchartRenderedSvgDocument Document,
        double X,
        double Y)
    {
        FlowchartRenderedSvgBox? Best = null;
        double BestDistance = 80.0;
        foreach (FlowchartRenderedSvgBox Box in Document.Boxes)
        {
            if (!string.IsNullOrEmpty(Box.NodeId))
            {
                continue;
            }

            double MidX = (Box.Left + Box.Right) / 2.0;
            double MidY = (Box.Top + Box.Bottom) / 2.0;
            double Distance = Math.Sqrt(((MidX - X) * (MidX - X)) + ((MidY - Y) * (MidY - Y)));
            if (X >= Box.Left - 8.0 && X <= Box.Right + 8.0 && Y >= Box.Top - 8.0 && Y <= Box.Bottom + 24.0)
            {
                Distance = Math.Min(Distance, 8.0);
            }

            if (Distance < BestDistance)
            {
                BestDistance = Distance;
                Best = Box;
            }
        }

        return Best;
    }

    private static bool LooksLikeGroup(Dictionary<string, string> Attributes)
    {
        if (Attributes.TryGetValue("fill", out string? Fill)
            && string.Equals(Fill, "#ffffde", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Attributes.TryGetValue("stroke-dasharray", out string? Dash)
            && !string.IsNullOrWhiteSpace(Dash))
        {
            return true;
        }

        return false;
    }

    private static void ParseLabels(string Svg, DiagramContract Contract, FlowchartRenderedSvgDocument Document)
    {
        HashSet<string> EdgeLabels = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.IsNullOrWhiteSpace(Edge.Label))
            {
                EdgeLabels.Add(Edge.Label.Trim());
            }
        }

        foreach (Match Match in TextRegex.Matches(Svg))
        {
            string Text = Regex.Replace(Match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!EdgeLabels.Contains(Text))
            {
                continue;
            }

            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!Attributes.TryGetValue("x", out string? XText)
                || !Attributes.TryGetValue("y", out string? YText)
                || !TryParseDouble(XText, out double X)
                || !TryParseDouble(YText, out double Y))
            {
                continue;
            }

            string? SourceId = null;
            string? TargetId = null;
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Label, Text, StringComparison.Ordinal))
                {
                    SourceId = Edge.SourceId;
                    TargetId = Edge.TargetId;
                    break;
                }
            }

            double FontSizePx = 12.0;
            if (Attributes.TryGetValue("font-size", out string? FontSizeText)
                && TryParseDouble(FontSizeText, out double ParsedFontSize))
            {
                FontSizePx = ParsedFontSize;
            }

            Document.Labels.Add(new FlowchartRenderedSvgLabel(Text, X, Y - (FontSizePx * 0.2), SourceId, TargetId));
        }
    }

    private static void ParseArrowheads(string Svg, FlowchartRenderedSvgDocument Document)
    {
        foreach (Match Match in PolygonRegex.Matches(Svg))
        {
            Dictionary<string, string> Attributes = ParseAttributes(Match.Value);
            if (!Attributes.TryGetValue("points", out string? PointsText))
            {
                continue;
            }

            List<double> Values = [];
            foreach (string Token in PointsText.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParseDouble(Token, out double Value))
                {
                    Values.Add(Value);
                }
            }

            if (Values.Count >= 2)
            {
                Document.Arrowheads.Add((Values[0], Values[1]));
            }
        }
    }

    private static bool TryReadSamples(
        Dictionary<string, string> Attributes,
        out List<(double X, double Y)> Samples)
    {
        Samples = [];
        if (Attributes.TryGetValue("d", out string? PathData)
            && FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out Samples)
            && Samples.Count >= 2)
        {
            return true;
        }

        if (Attributes.TryGetValue("x1", out string? X1Text)
            && Attributes.TryGetValue("y1", out string? Y1Text)
            && Attributes.TryGetValue("x2", out string? X2Text)
            && Attributes.TryGetValue("y2", out string? Y2Text)
            && TryParseDouble(X1Text, out double X1)
            && TryParseDouble(Y1Text, out double Y1)
            && TryParseDouble(X2Text, out double X2)
            && TryParseDouble(Y2Text, out double Y2))
        {
            Samples = [(X1, Y1), (X2, Y2)];
            return true;
        }

        return false;
    }

    private static bool TryReadBox(
        Dictionary<string, string> Attributes,
        string Token,
        out double Left,
        out double Top,
        out double Right,
        out double Bottom)
    {
        Left = 0.0;
        Top = 0.0;
        Right = 0.0;
        Bottom = 0.0;
        if (Attributes.TryGetValue("x", out string? XText)
            && Attributes.TryGetValue("y", out string? YText)
            && Attributes.TryGetValue("width", out string? WidthText)
            && Attributes.TryGetValue("height", out string? HeightText)
            && TryParseDouble(XText, out double X)
            && TryParseDouble(YText, out double Y)
            && TryParseDouble(WidthText, out double Width)
            && TryParseDouble(HeightText, out double Height)
            && Width > 0.0
            && Height > 0.0)
        {
            Left = X;
            Top = Y;
            Right = X + Width;
            Bottom = Y + Height;
            return true;
        }

        if (Attributes.TryGetValue("d", out string? PathData)
            && FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples)
            && Samples.Count >= 2)
        {
            Left = double.PositiveInfinity;
            Top = double.PositiveInfinity;
            Right = double.NegativeInfinity;
            Bottom = double.NegativeInfinity;
            foreach ((double SampleX, double SampleY) in Samples)
            {
                Left = Math.Min(Left, SampleX);
                Top = Math.Min(Top, SampleY);
                Right = Math.Max(Right, SampleX);
                Bottom = Math.Max(Bottom, SampleY);
            }

            return !double.IsInfinity(Left);
        }

        _ = Token;
        return false;
    }

    private static bool IsKnownEdge(DiagramContract Contract, string SourceId, string TargetId)
    {
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static FlowchartRenderedSvgStroke? FindStroke(
        IReadOnlyList<FlowchartRenderedSvgStroke> Strokes,
        string SourceId,
        string TargetId)
    {
        foreach (FlowchartRenderedSvgStroke Stroke in Strokes)
        {
            if (string.Equals(Stroke.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Stroke.TargetId, TargetId, StringComparison.Ordinal))
            {
                return Stroke;
            }
        }

        return null;
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

    private static double PathLength(IReadOnlyList<(double X, double Y)> Samples)
    {
        double Length = 0.0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double Dx = Samples[Index].X - Samples[Index - 1].X;
            double Dy = Samples[Index].Y - Samples[Index - 1].Y;
            Length += Math.Sqrt((Dx * Dx) + (Dy * Dy));
        }

        return Length;
    }

    private static bool TryParseDouble(string Text, out double Value)
    {
        return double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out Value);
    }
}

/// <summary>
/// A rendered edge stroke.
/// </summary>
public sealed class FlowchartRenderedSvgStroke
{
    /// <summary>
    /// Creates a stroke.
    /// </summary>
    public FlowchartRenderedSvgStroke(string SourceId, string TargetId, List<(double X, double Y)> Samples)
    {
        this.SourceId = SourceId;
        this.TargetId = TargetId;
        this.Samples = Samples;
    }

    /// <summary>
    /// Source node identifier.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Target node identifier.
    /// </summary>
    public string TargetId { get; }

    /// <summary>
    /// Flattened path samples.
    /// </summary>
    public List<(double X, double Y)> Samples { get; }
}

/// <summary>
/// A rendered node or group box.
/// </summary>
public sealed class FlowchartRenderedSvgBox
{
    /// <summary>
    /// Creates a box.
    /// </summary>
    public FlowchartRenderedSvgBox(string NodeId, double Left, double Top, double Right, double Bottom, bool IsGroup)
    {
        this.NodeId = NodeId;
        this.Left = Left;
        this.Top = Top;
        this.Right = Right;
        this.Bottom = Bottom;
        this.IsGroup = IsGroup;
    }

    /// <summary>
    /// Node or group identifier. May be assigned after construction when matching a label.
    /// </summary>
    public string NodeId { get; set; }

    /// <summary>
    /// Left edge in SVG coordinates.
    /// </summary>
    public double Left { get; }

    /// <summary>
    /// Top edge in SVG coordinates.
    /// </summary>
    public double Top { get; }

    /// <summary>
    /// Right edge in SVG coordinates.
    /// </summary>
    public double Right { get; }

    /// <summary>
    /// Bottom edge in SVG coordinates.
    /// </summary>
    public double Bottom { get; }

    /// <summary>
    /// True when the box is a group or subgraph.
    /// </summary>
    public bool IsGroup { get; }
}

/// <summary>
/// A rendered edge label.
/// </summary>
public sealed class FlowchartRenderedSvgLabel
{
    /// <summary>
    /// Creates a label.
    /// </summary>
    public FlowchartRenderedSvgLabel(string Text, double X, double Y, string? SourceId, string? TargetId)
    {
        this.Text = Text;
        this.X = X;
        this.Y = Y;
        this.SourceId = SourceId;
        this.TargetId = TargetId;
    }

    /// <summary>
    /// Label text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Anchor X.
    /// </summary>
    public double X { get; }

    /// <summary>
    /// Anchor Y, adjusted toward the visual centre.
    /// </summary>
    public double Y { get; }

    /// <summary>
    /// Owning edge source.
    /// </summary>
    public string? SourceId { get; }

    /// <summary>
    /// Owning edge target.
    /// </summary>
    public string? TargetId { get; }
}
