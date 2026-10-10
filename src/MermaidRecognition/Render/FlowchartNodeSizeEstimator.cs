using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Render;

/// <summary>
/// Estimates flowchart node dimensions from label text, font metrics, and shape (spec §3.17.2–§3.17.3).
/// Ported from Naiad <c>FlowchartRenderer</c> measurement heuristics aligned with dagre-wrapper.
/// </summary>
public static class FlowchartNodeSizeEstimator
{
    private const double FLOWCHART_HTML_LABEL_LINE_BOX_HEIGHT_EM = 2.5;

    private const double FLOWCHART_LABEL_CHAR_WIDTH_EM = 0.512;

    private const double FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO = 1.125;

    private const double FLOWCHART_HTML_LABEL_BBOX_WIDTH_TO_LINE_BOX_RATIO = 1.68;

    private const int FLOWCHART_LABEL_SHORT_WORD_MAX_LENGTH = 3;

    private const double FLOWCHART_HEXAGON_BEVEL_DIVISOR = 4.0;

    private const double FLOWCHART_HEXAGON_LABEL_CHAR_WIDTH_EM = 0.54;

    private const double FLOWCHART_HEXAGON_LINE_BOX_HEIGHT_EM = 3.0;

    private const double ICON_EXTRA_WIDTH_PX = 20.0;

    private static readonly Regex IconPattern = new(
        @"(fa[bsr]?):fa-([a-z0-9-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Estimates width and height for a node using effective config from <paramref name="Options"/>.
    /// </summary>
    public static FlowchartNodeDimensions Estimate(NodeContract Node, FlowchartRenderOptions Options)
    {
        if (double.IsFinite(Node.LayoutWidth) && double.IsFinite(Node.LayoutHeight)
            && Node.LayoutWidth > 0 && Node.LayoutHeight > 0)
        {
            return new FlowchartNodeDimensions
            {
                Width = Node.LayoutWidth,
                Height = Node.LayoutHeight,
                DisplayLabel = string.IsNullOrEmpty(Node.Label) ? Node.Id : Node.Label
            };
        }

        string RawLabel = string.IsNullOrEmpty(Node.Label) ? Node.Id : Node.Label;
        string NormalisedLabel = NormaliseHtmlLineBreaks(RawLabel);
        bool HasIcon = IconPattern.IsMatch(NormalisedLabel);
        bool HasExplicitBreak = HasIcon
            || NormalisedLabel.Contains("<br/>", StringComparison.OrdinalIgnoreCase)
            || NormalisedLabel.Contains("<table", StringComparison.OrdinalIgnoreCase)
            || NormalisedLabel.Contains("<div", StringComparison.OrdinalIgnoreCase)
            || NormalisedLabel.Contains("\\n", StringComparison.Ordinal)
            || NormalisedLabel.Contains('\n');

        FlowchartLabelDimensions LabelDimensions = MeasureWrappedLabel(RawLabel, Options);
        string WrappedForMeasure = LabelDimensions.DisplayText;

        double LabelPaddingPx = Options.LabelPaddingPx;
        double LabelPaddingX = LabelPaddingPx * 2.0;
        double LabelPaddingY = LabelPaddingPx;
        double IconExtraWidthPx = HasIcon ? ICON_EXTRA_WIDTH_PX : 0.0;

        double Width = LabelDimensions.Width + (2.0 * LabelPaddingX) + IconExtraWidthPx;
        double Height = LabelDimensions.Height + (2.0 * LabelPaddingY);

        string Shape = Node.Shape ?? string.Empty;
        if (IsCircleShape(Shape))
        {
            double Diameter = Math.Max(Width, Height);
            Width = Diameter;
            Height = Diameter;
        }
        else if (IsDiamondShape(Shape))
        {
            double W0 = LabelDimensions.Width + LabelPaddingPx;
            double H0 = LabelDimensions.Height + LabelPaddingPx;
            double S = W0 + H0;
            Width = S;
            Height = S;
        }
        else if (IsHexagonShape(Shape))
        {
            LabelBBox HexLabelSize = MeasureHexagonLabelBBox(WrappedForMeasure, Options.FontSizePx);
            double H0 = HexLabelSize.Height + LabelPaddingPx;
            double M = H0 / FLOWCHART_HEXAGON_BEVEL_DIVISOR;
            Height = H0;
            Width = HexLabelSize.Width + (2.0 * M) + LabelPaddingPx + IconExtraWidthPx;
        }
        else if (IsStadiumShape(Shape))
        {
            double LabelInnerWidthPx = MeasureStadiumInnerLabelWidth(WrappedForMeasure, Options.FontSizePx);
            double LabelInnerHeightPx = MeasureStadiumInnerLabelHeight(WrappedForMeasure, Options.FontSizePx);
            Height = LabelInnerHeightPx + LabelPaddingPx;
            Width = LabelInnerWidthPx + (Height / 4.0) + LabelPaddingPx + IconExtraWidthPx;
        }
        else if (IsMarkdownListLabel(WrappedForMeasure))
        {
            double SquareSide = Math.Max(Width, Height);
            Width = SquareSide;
            Height = SquareSide;
        }

        FlowchartNodeDimensions Dimensions = new FlowchartNodeDimensions
        {
            Width = Math.Max(1.0, Width),
            Height = Math.Max(1.0, Height)
        };
        string TextForMeasure = IconPattern.Replace(NormalisedLabel, string.Empty).Trim();
        TextForMeasure = TextForMeasure.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
        if (FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup(NormalisedLabel))
        {
            Dimensions.DisplayLabel = FlowchartRichLabelSvgWriter.StripRichMarkupToPlainText(NormalisedLabel);
        }
        else
        {
            Dimensions.DisplayLabel = HasExplicitBreak ? TextForMeasure : WrappedForMeasure;
        }

        return Dimensions;
    }

    /// <summary>
    /// Measures wrapped label text for nodes or edges using <c>flowchart.wrappingWidth</c> and <c>themeVariables.fontSize</c>.
    /// </summary>
    public static FlowchartLabelDimensions MeasureWrappedLabel(
        string RawLabel,
        FlowchartRenderOptions Options) => MeasureWrappedLabel(RawLabel, Options, Options.FontSizePx);

    /// <summary>
    /// Measures wrapped label text using an explicit font size (for edge labels).
    /// </summary>
    public static FlowchartLabelDimensions MeasureWrappedLabel(
        string RawLabel,
        FlowchartRenderOptions Options,
        double FontSizePx)
    {
        string NormalisedLabel = NormaliseHtmlLineBreaks(RawLabel);
        string TextForMeasure;
        bool UsesRichMarkupMeasurement = FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup(NormalisedLabel);
        if (UsesRichMarkupMeasurement)
        {
            TextForMeasure = FlowchartRichLabelSvgWriter.StripRichMarkupToPlainText(NormalisedLabel);
        }
        else
        {
            TextForMeasure = IconPattern.Replace(NormalisedLabel, string.Empty).Trim();
            TextForMeasure = TextForMeasure.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
        }

        string WrappedForMeasure;
        if (UsesRichMarkupMeasurement && !TextForMeasure.Contains('\n', StringComparison.Ordinal))
        {
            WrappedForMeasure = TextForMeasure;
        }
        else
        {
            WrappedForMeasure = WrapPlainTextToWidth(
                TextForMeasure,
                FontSizePx,
                Options.WrappingWidthPx);
        }

        LabelBBox TextSize = MeasureLabelBBox(WrappedForMeasure, FontSizePx);
        if (UsesRichMarkupMeasurement)
        {
            double StyledMaxWidthPx = FlowchartRichLabelSvgWriter.EstimateStyledLabelMaxWidthPx(
                NormalisedLabel,
                FontSizePx);
            TextSize = new LabelBBox(Math.Max(TextSize.Width, StyledMaxWidthPx), TextSize.Height);
        }

        return new FlowchartLabelDimensions
        {
            DisplayText = WrappedForMeasure,
            Width = TextSize.Width,
            Height = TextSize.Height
        };
    }

    private static bool IsCircleShape(string Shape) => string.Equals(Shape, "circle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "doublecircle", StringComparison.OrdinalIgnoreCase);

    private static bool IsDiamondShape(string Shape) => string.Equals(Shape, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rhombus", StringComparison.OrdinalIgnoreCase);

    private static bool IsHexagonShape(string Shape) => string.Equals(Shape, "hexagon", StringComparison.OrdinalIgnoreCase);

    private static bool IsStadiumShape(string Shape) => string.Equals(Shape, "stadium", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rounded", StringComparison.OrdinalIgnoreCase);

    private static bool IsMarkdownListLabel(string TextForMeasure) => FlowchartRichLabelSvgWriter.IsMarkdownListLabel(TextForMeasure);

    private static string NormaliseHtmlLineBreaks(string Label)
    {
        if (string.IsNullOrEmpty(Label))
        {
            return string.Empty;
        }

        return Label.Replace("<br>", "<br/>", StringComparison.OrdinalIgnoreCase);
    }

    private static string WrapPlainTextToWidth(string Text, double FontSizePx, double MaxWidthPx)
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            return Text ?? string.Empty;
        }

        string Normalised = Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] Paragraphs = Normalised.Split('\n');

        double MaxChars = Math.Max(1.0, MaxWidthPx / (FontSizePx * 0.62));
        int MaxCharsPerLine = (int)Math.Floor(MaxChars);

        List<string> Lines = [];
        for (int ParagraphIndex = 0; ParagraphIndex < Paragraphs.Length; ParagraphIndex++)
        {
            string Paragraph = Paragraphs[ParagraphIndex].Trim();
            if (Paragraph.Length == 0)
            {
                Lines.Add(string.Empty);
                continue;
            }

            string[] Words = Paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string Current = string.Empty;

            for (int WordIndex = 0; WordIndex < Words.Length; WordIndex++)
            {
                string Word = Words[WordIndex];
                string Candidate = Current.Length == 0 ? Word : Current + " " + Word;
                if (Candidate.Length <= MaxCharsPerLine)
                {
                    Current = Candidate;
                    continue;
                }

                if (Current.Length > 0)
                {
                    Lines.Add(Current);
                }

                Current = Word;
            }

            if (Current.Length > 0)
            {
                Lines.Add(Current);
            }
        }

        return string.Join("\n", Lines);
    }

    private static LabelBBox MeasureLabelBBox(string Text, double FontSizePx)
    {
        if (string.IsNullOrEmpty(Text))
        {
            double EmptyLineBoxHeightPx = FontSizePx * FLOWCHART_HTML_LABEL_LINE_BOX_HEIGHT_EM;
            return new LabelBBox(0.0, EmptyLineBoxHeightPx);
        }

        string[] Lines = Text.Split('\n');
        int MaxLineLength = 0;
        foreach (string Line in Lines)
        {
            int LineLength = Line.Length;
            if (LineLength > MaxLineLength)
            {
                MaxLineLength = LineLength;
            }
        }

        double InnerWidthPx = MaxLineLength * FontSizePx * FLOWCHART_LABEL_CHAR_WIDTH_EM;
        double SingleLineBoxHeightPx = FontSizePx * FLOWCHART_HTML_LABEL_LINE_BOX_HEIGHT_EM;
        double InnerHeightPx = Lines.Length == 1
            ? SingleLineBoxHeightPx
            : Lines.Length * FontSizePx * 1.5;

        if (MaxLineLength == 1)
        {
            InnerWidthPx = SingleLineBoxHeightPx;
        }
        else if (MaxLineLength <= FLOWCHART_LABEL_SHORT_WORD_MAX_LENGTH
            && InnerWidthPx < SingleLineBoxHeightPx * FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO)
        {
            InnerWidthPx = SingleLineBoxHeightPx * FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO;
        }

        if (Lines.Length == 1)
        {
            double BrowserStyleMinWidthPx = SingleLineBoxHeightPx * FLOWCHART_HTML_LABEL_BBOX_WIDTH_TO_LINE_BOX_RATIO;
            if (InnerWidthPx < BrowserStyleMinWidthPx)
            {
                InnerWidthPx = BrowserStyleMinWidthPx;
            }
        }

        return new LabelBBox(InnerWidthPx, InnerHeightPx);
    }

    private static LabelBBox MeasureHexagonLabelBBox(string Text, double FontSizePx)
    {
        if (string.IsNullOrEmpty(Text))
        {
            double EmptyHeightPx = FontSizePx * FLOWCHART_HEXAGON_LINE_BOX_HEIGHT_EM;
            return new LabelBBox(0.0, EmptyHeightPx);
        }

        string[] Lines = Text.Split('\n');
        int MaxLineLength = 0;
        foreach (string Line in Lines)
        {
            int LineLength = Line.Length;
            if (LineLength > MaxLineLength)
            {
                MaxLineLength = LineLength;
            }
        }

        double InnerWidthPx = MaxLineLength * FontSizePx * FLOWCHART_HEXAGON_LABEL_CHAR_WIDTH_EM;
        double SingleLineHeightPx = FontSizePx * FLOWCHART_HEXAGON_LINE_BOX_HEIGHT_EM;
        double InnerHeightPx = Lines.Length == 1
            ? SingleLineHeightPx
            : Lines.Length * FontSizePx * 1.5;

        return new LabelBBox(InnerWidthPx, InnerHeightPx);
    }

    private static double MeasureStadiumInnerLabelWidth(string Text, double FontSizePx)
    {
        LabelBBox Bbox = MeasureLabelBBox(Text, FontSizePx);
        return Bbox.Width;
    }

    private static double MeasureStadiumInnerLabelHeight(string Text, double FontSizePx)
    {
        LabelBBox Bbox = MeasureLabelBBox(Text, FontSizePx);
        return Bbox.Height;
    }

    private readonly struct LabelBBox(double Width, double Height)
    {
        public double Width { get; } = Width;

        public double Height { get; } = Height;
    }
}
