using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Config;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Render;

/// <summary>
/// Typed render inputs derived from <see cref="DiagramContract.Configuration"/> and style directives (spec §4.1, §3.17).
/// </summary>
public sealed class FlowchartRenderOptions
{
    private const double THICK_EDGE_STROKE_WIDTH = 3.5;

    private const string DOTTED_EDGE_DASHARRAY = "3 3";

    private static readonly Regex CssColourRegex = new(
        @"^(#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|rgba?\([^)]+\)|[a-zA-Z]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly Dictionary<int, RecognitionStyle> _LinkStyleByCreationIndex =
        new Dictionary<int, RecognitionStyle>();

    private RecognitionStyle? _DefaultLinkStyle;

    private readonly Dictionary<string, RecognitionStyle> _ClassDefStylesById =
        new Dictionary<string, RecognitionStyle>(StringComparer.Ordinal);

    private FlowchartRenderOptions()
    {
    }

    /// <summary>
    /// Diagram canvas background from <c>themeVariables.background</c>.
    /// </summary>
    public string Background { get; private set; } = "white";

    /// <summary>
    /// Default node fill from <c>themeVariables.nodeBkg</c>.
    /// </summary>
    public string NodeFill { get; private set; } = "#ECECFF";

    /// <summary>
    /// Default node border from <c>themeVariables.nodeBorder</c>.
    /// </summary>
    public string NodeStroke { get; private set; } = "#9370DB";

    /// <summary>
    /// Default edge stroke from <c>themeVariables.defaultLinkColor</c>.
    /// </summary>
    public string EdgeStroke { get; private set; } = "#333333";

    /// <summary>
    /// Default label text colour from <c>themeVariables.textColor</c>.
    /// </summary>
    public string TextColor { get; private set; } = "#333";

    /// <summary>
    /// Default font family from <c>themeVariables.fontFamily</c>.
    /// </summary>
    public string FontFamily { get; private set; } = "\"trebuchet ms\", verdana, arial, sans-serif";

    /// <summary>
    /// Default font size in px from <c>themeVariables.fontSize</c>.
    /// </summary>
    public double FontSizePx { get; private set; } = 16.0;

    /// <summary>
    /// Font size in px for edge labels (Mermaid renders these smaller than node labels).
    /// </summary>
    public double EdgeLabelFontSizePx { get; private set; } = 12.0;

    /// <summary>
    /// Default stroke width from <c>themeVariables.strokeWidth</c>.
    /// </summary>
    public double StrokeWidthPx { get; private set; } = 1.0;

    /// <summary>
    /// Default font weight from <c>themeVariables.fontWeight</c>.
    /// </summary>
    public string FontWeight { get; private set; } = "normal";

    /// <summary>
    /// Edge label background from <c>themeVariables.edgeLabelBackground</c>.
    /// </summary>
    public string EdgeLabelBackground { get; private set; } = "rgba(232,232,232, 0.8)";

    /// <summary>
    /// Whether HTML labels are enabled (<c>htmlLabels</c> effective config, spec §3.17.4).
    /// </summary>
    public bool HtmlLabels { get; private set; } = true;

    /// <summary>
    /// Minimum horizontal gap between nodes from <c>flowchart.nodeSpacing</c>.
    /// </summary>
    public double NodeSpacingPx { get; private set; } = 50.0;

    /// <summary>
    /// Minimum gap between ranks from <c>flowchart.rankSpacing</c>.
    /// </summary>
    public double RankSpacingPx { get; private set; } = 50.0;

    /// <summary>
    /// Viewport padding from <c>flowchart.diagramPadding</c>.
    /// </summary>
    public double DiagramPaddingPx { get; private set; } = 8.0;

    /// <summary>
    /// Inner label padding from <c>flowchart.padding</c>.
    /// </summary>
    public double LabelPaddingPx { get; private set; } = 15.0;

    /// <summary>
    /// Label wrap width from <c>flowchart.wrappingWidth</c>.
    /// </summary>
    public double WrappingWidthPx { get; private set; } = 200.0;

    /// <summary>
    /// Edge curve style from <c>flowchart.curve</c> (applied to multi-point routed edges; simple links use straight segments in R1).
    /// </summary>
    public string Curve { get; private set; } = "basis";

    /// <summary>
    /// Builds render options from a recognised flowchart contract.
    /// </summary>
    public static FlowchartRenderOptions FromContract(DiagramContract Contract)
    {
        FlowchartRenderOptions Options = new FlowchartRenderOptions();
        Options.ApplyTheme(Contract.Configuration);
        Options.BuildLinkStyleMaps(Contract);
        Options.BuildClassDefMap(Contract);
        return Options;
    }

    /// <summary>
    /// Resolves node paint and typography (theme defaults + inline/class/style directives).
    /// </summary>
    public FlowchartResolvedStyle ResolveNodeStyle(NodeContract Node)
    {
        RecognitionStyle ResolvedStyle = CreateThemeNodeStyle();
        RecognitionStyleMerge.Apply(Node.InlineStyle, ResolvedStyle);
        ApplyClassDefStyles(Node, ResolvedStyle);
        return FromRecognitionStyle(ResolvedStyle);
    }

    /// <summary>
    /// Resolves edge paint (theme defaults + linkStyle + stroke token semantics).
    /// </summary>
    public FlowchartResolvedStyle ResolveEdgeStyle(EdgeContract Edge)
    {
        RecognitionStyle ResolvedStyle = CreateThemeEdgeStyle();

        if (_DefaultLinkStyle is not null)
        {
            RecognitionStyleMerge.Apply(_DefaultLinkStyle, ResolvedStyle);
        }

        if (_LinkStyleByCreationIndex.TryGetValue(Edge.CreationIndex, out RecognitionStyle? IndexedStyle))
        {
            RecognitionStyleMerge.Apply(IndexedStyle, ResolvedStyle);
        }

        ApplyEdgeClassDefStyles(Edge, ResolvedStyle);

        FlowchartResolvedStyle Resolved = FromRecognitionStyle(ResolvedStyle);
        ApplyEdgeStrokeToken(Edge.Stroke, Resolved);
        return Resolved;
    }

    private void ApplyTheme(DiagramConfigContract Configuration)
    {
        Background = ReadTheme(Configuration, "themeVariables.background", Background);
        NodeFill = ReadTheme(Configuration, "themeVariables.nodeBkg", NodeFill);
        NodeStroke = ReadTheme(Configuration, "themeVariables.nodeBorder", NodeStroke);
        EdgeStroke = ReadTheme(Configuration, "themeVariables.defaultLinkColor", EdgeStroke);
        TextColor = ReadTheme(Configuration, "themeVariables.textColor", TextColor);
        EdgeLabelBackground = ReadTheme(Configuration, "themeVariables.edgeLabelBackground", EdgeLabelBackground);
        FontFamily = ReadEffective(Configuration, "themeVariables.fontFamily", "fontFamily", FontFamily);
        FontSizePx = ParseFontSizePx(ReadEffective(Configuration, "themeVariables.fontSize", "fontSize", FontSizePx.ToString(CultureInfo.InvariantCulture) + "px"));
        EdgeLabelFontSizePx = Math.Max(10.0, FontSizePx * 0.75);
        StrokeWidthPx = ParseStrokeWidth(ReadTheme(Configuration, "themeVariables.strokeWidth", StrokeWidthPx.ToString(CultureInfo.InvariantCulture)));
        FontWeight = ReadTheme(Configuration, "themeVariables.fontWeight", FontWeight);
        HtmlLabels = ParseBoolean(ReadTheme(Configuration, "htmlLabels", "true"), true);
        NodeSpacingPx = ParsePositiveDouble(ReadTheme(Configuration, "flowchart.nodeSpacing", NodeSpacingPx.ToString(CultureInfo.InvariantCulture)), NodeSpacingPx);
        RankSpacingPx = ParsePositiveDouble(ReadTheme(Configuration, "flowchart.rankSpacing", RankSpacingPx.ToString(CultureInfo.InvariantCulture)), RankSpacingPx);
        DiagramPaddingPx = ParsePositiveDouble(ReadTheme(Configuration, "flowchart.diagramPadding", DiagramPaddingPx.ToString(CultureInfo.InvariantCulture)), DiagramPaddingPx);
        LabelPaddingPx = ParsePositiveDouble(ReadTheme(Configuration, "flowchart.padding", LabelPaddingPx.ToString(CultureInfo.InvariantCulture)), LabelPaddingPx);
        WrappingWidthPx = ParsePositiveDouble(ReadTheme(Configuration, "flowchart.wrappingWidth", WrappingWidthPx.ToString(CultureInfo.InvariantCulture)), WrappingWidthPx);
        Curve = ReadTheme(Configuration, "flowchart.curve", Curve);
    }

    private void BuildClassDefMap(DiagramContract Contract)
    {
        foreach (ClassDefContract ClassDef in Contract.ClassDefs)
        {
            if (!_ClassDefStylesById.TryGetValue(ClassDef.ClassId, out RecognitionStyle? Existing))
            {
                _ClassDefStylesById[ClassDef.ClassId] = CloneStyle(ClassDef.Style);
                continue;
            }

            RecognitionStyleMerge.Apply(ClassDef.Style, Existing);
        }
    }

    private void ApplyClassDefStyles(NodeContract Node, RecognitionStyle ResolvedStyle)
    {
        foreach (string ClassId in Node.Classes)
        {
            if (_ClassDefStylesById.TryGetValue(ClassId, out RecognitionStyle? ClassStyle))
            {
                RecognitionStyleMerge.Apply(ClassStyle, ResolvedStyle);
            }
        }
    }

    private void ApplyEdgeClassDefStyles(EdgeContract Edge, RecognitionStyle ResolvedStyle)
    {
        foreach (string ClassId in Edge.Classes)
        {
            if (_ClassDefStylesById.TryGetValue(ClassId, out RecognitionStyle? ClassStyle))
            {
                RecognitionStyleMerge.Apply(ClassStyle, ResolvedStyle);
            }
        }
    }

    private void BuildLinkStyleMaps(DiagramContract Contract)
    {
        foreach (LinkStyleDirective LinkStyle in Contract.LinkStyles)
        {
            if (LinkStyle.IsDefault)
            {
                if (_DefaultLinkStyle is null)
                {
                    _DefaultLinkStyle = CloneStyle(LinkStyle.Style);
                }
                else
                {
                    RecognitionStyleMerge.Apply(LinkStyle.Style, _DefaultLinkStyle);
                }

                continue;
            }

            foreach (int EdgeIndex in LinkStyle.EdgeIndexes)
            {
                if (!_LinkStyleByCreationIndex.TryGetValue(EdgeIndex, out RecognitionStyle? Existing))
                {
                    _LinkStyleByCreationIndex[EdgeIndex] = CloneStyle(LinkStyle.Style);
                    continue;
                }

                RecognitionStyleMerge.Apply(LinkStyle.Style, Existing);
            }
        }
    }

    private static void ApplyEdgeStrokeToken(string? StrokeToken, FlowchartResolvedStyle Resolved)
    {
        if (string.IsNullOrWhiteSpace(StrokeToken))
        {
            return;
        }

        string Token = StrokeToken.Trim();
        if (string.Equals(Token, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (IsEdgeStrokeToken(Token))
        {
            if (string.Equals(Token, "thick", StringComparison.OrdinalIgnoreCase))
            {
                Resolved.StrokeWidth = THICK_EDGE_STROKE_WIDTH;
            }
            else if (string.Equals(Token, "dotted", StringComparison.OrdinalIgnoreCase))
            {
                Resolved.StrokeDasharray = DOTTED_EDGE_DASHARRAY;
            }

            return;
        }

        if (LooksLikeCssColour(Token))
        {
            Resolved.Stroke = Token;
        }
    }

    private static bool IsEdgeStrokeToken(string Token)
    {
        return string.Equals(Token, "normal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Token, "thick", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Token, "dotted", StringComparison.OrdinalIgnoreCase);
    }

    private RecognitionStyle CreateThemeNodeStyle()
    {
        RecognitionStyle Style = new RecognitionStyle();
        Style.Fill = NodeFill;
        Style.Stroke = NodeStroke;
        Style.TextColor = TextColor;
        Style.FontFamily = FontFamily;
        Style.FontSize = FontSizePx;
        Style.FontWeight = FontWeight;
        Style.StrokeWidth = StrokeWidthPx;
        return Style;
    }

    private RecognitionStyle CreateThemeEdgeStyle()
    {
        RecognitionStyle Style = new RecognitionStyle();
        Style.Stroke = EdgeStroke;
        Style.TextColor = TextColor;
        Style.FontFamily = FontFamily;
        Style.FontSize = FontSizePx;
        Style.FontWeight = FontWeight;
        Style.StrokeWidth = StrokeWidthPx;
        return Style;
    }

    private static FlowchartResolvedStyle FromRecognitionStyle(RecognitionStyle Style)
    {
        FlowchartResolvedStyle Resolved = new FlowchartResolvedStyle();
        if (Style.Fill is not null)
        {
            Resolved.Fill = Style.Fill;
        }

        if (Style.Stroke is not null)
        {
            Resolved.Stroke = Style.Stroke;
        }

        if (Style.TextColor is not null)
        {
            Resolved.TextColor = Style.TextColor;
        }

        if (Style.FontFamily is not null)
        {
            Resolved.FontFamily = Style.FontFamily;
        }

        if (Style.FontSize.HasValue)
        {
            Resolved.FontSizePx = Style.FontSize.Value;
        }

        if (Style.FontWeight is not null)
        {
            Resolved.FontWeight = Style.FontWeight;
        }

        if (Style.StrokeWidth.HasValue)
        {
            Resolved.StrokeWidth = Style.StrokeWidth.Value;
        }

        Resolved.StrokeDasharray = Style.StrokeDasharray;
        return Resolved;
    }

    private static RecognitionStyle CloneStyle(RecognitionStyle Source)
    {
        RecognitionStyle Clone = new RecognitionStyle();
        RecognitionStyleMerge.Apply(Source, Clone);
        return Clone;
    }

    private static string ReadTheme(DiagramConfigContract Configuration, string Key, string Fallback)
    {
        if (Configuration.TryGetEffective(Key, out string Value) && !string.IsNullOrWhiteSpace(Value))
        {
            return Value.Trim();
        }

        return Fallback;
    }

    private static string ReadEffective(DiagramConfigContract Configuration, string PrimaryKey, string SecondaryKey, string Fallback)
    {
        if (Configuration.TryGetEffective(PrimaryKey, out string PrimaryValue) && !string.IsNullOrWhiteSpace(PrimaryValue))
        {
            return PrimaryValue.Trim();
        }

        if (Configuration.TryGetEffective(SecondaryKey, out string SecondaryValue) && !string.IsNullOrWhiteSpace(SecondaryValue))
        {
            return SecondaryValue.Trim();
        }

        return Fallback;
    }

    private static bool ParseBoolean(string Value, bool Fallback)
    {
        if (string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Fallback;
    }

    private static double ParsePositiveDouble(string Value, double Fallback)
    {
        if (double.TryParse(Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double Parsed)
            && Parsed >= 0.0)
        {
            return Parsed;
        }

        return Fallback;
    }

    private static double ParseFontSizePx(string Value)
    {
        string Trimmed = Value.Trim();
        if (Trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            Trimmed = Trimmed[..^2].Trim();
        }

        if (double.TryParse(Trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double Parsed))
        {
            return Parsed;
        }

        return 16.0;
    }

    private static double ParseStrokeWidth(string Value)
    {
        string Trimmed = Value.Trim();
        if (Trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            Trimmed = Trimmed[..^2].Trim();
        }

        if (double.TryParse(Trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double Parsed))
        {
            return Parsed;
        }

        return 1.0;
    }

    private static bool LooksLikeCssColour(string Value)
    {
        return CssColourRegex.IsMatch(Value);
    }
}
