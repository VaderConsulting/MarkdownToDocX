using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Render;

/// <summary>
/// Writes flowchart node and edge labels with basic HTML and Markdown styling as SVG tspans (F89–F96).
/// </summary>
public static class FlowchartRichLabelSvgWriter
{
    private const double LABEL_CHAR_WIDTH_EM = 0.512;

    private const double BOLD_LABEL_CHAR_WIDTH_EM = 0.54;

    private const double MONOSPACE_LABEL_CHAR_WIDTH_EM = 0.62;

    private static readonly Regex BoldMarkdownPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    private static readonly Regex ItalicMarkdownPattern = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);

    private static readonly Regex CodeMarkdownPattern = new(@"`([^`]+)`", RegexOptions.Compiled);

    private static readonly Regex DivOpenPattern = new(@"<div[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TableRowPattern = new(@"<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TableCellPattern = new(@"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Builds the plain text that appears in rendered SVG for label matching in validation.
    /// </summary>
    public static string BuildRenderedPlainText(string RawLabel)
    {
        if (string.IsNullOrEmpty(RawLabel))
        {
            return string.Empty;
        }

        string Normalised = NormaliseLabelForRender(RawLabel);
        List<string> Lines = SplitLines(Normalised);
        StringBuilder Builder = new StringBuilder();
        foreach (string Line in Lines)
        {
            List<StyledRun> Runs = TokeniseInlineContent(Line);
            foreach (StyledRun Run in Runs)
            {
                if (!Run.IsLineBreak)
                {
                    Builder.Append(Run.Text);
                }
            }
        }

        return Builder.ToString();
    }

    /// <summary>
    /// Returns true when the label contains intentional HTML or Markdown styling (not bare decoded symbols).
    /// </summary>
    public static bool ContainsIntentionalRichMarkup(string Label)
    {
        if (string.IsNullOrEmpty(Label))
        {
            return false;
        }

        if (Label.Contains("<br", StringComparison.OrdinalIgnoreCase)
            || Label.Contains("<div", StringComparison.OrdinalIgnoreCase)
            || Label.Contains("<table", StringComparison.OrdinalIgnoreCase)
            || Label.Contains("<tr", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Label.Contains("<b>", StringComparison.OrdinalIgnoreCase)
            || Label.Contains("<strong>", StringComparison.OrdinalIgnoreCase)
            || Label.Contains("<i>", StringComparison.OrdinalIgnoreCase)
            || Label.Contains("<em>", StringComparison.OrdinalIgnoreCase)
            || Label.Contains("<span", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Label.Contains("**", StringComparison.Ordinal))
        {
            return true;
        }

        if (CodeMarkdownPattern.IsMatch(Label))
        {
            return true;
        }

        if (ItalicMarkdownPattern.IsMatch(Label))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalises rich label markup to the plain multi-line text used for layout and rendering.
    /// </summary>
    public static string NormaliseRichLabelText(string RawLabel)
    {
        return NormaliseLabelForRender(RawLabel);
    }

    /// <summary>
    /// Strips HTML and Markdown styling to the visible label text used for layout measurement.
    /// </summary>
    public static string StripRichMarkupToPlainText(string RawLabel)
    {
        string Normalised = NormaliseLabelForRender(RawLabel);
        List<string> Lines = SplitLines(Normalised);
        List<string> PlainLines = new List<string>();
        foreach (string Line in Lines)
        {
            List<StyledRun> Runs = TokeniseInlineContent(Line);
            StringBuilder PlainLine = new StringBuilder();
            foreach (StyledRun Run in Runs)
            {
                PlainLine.Append(Run.Text);
            }

            PlainLines.Add(PlainLine.ToString());
        }

        return string.Join("\n", PlainLines);
    }

    /// <summary>
    /// Returns true when every non-empty line is a Markdown bullet list entry.
    /// </summary>
    public static bool IsMarkdownListLabel(string Text)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return false;
        }

        string[] Lines = Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (Lines.Length < 2)
        {
            return false;
        }

        bool SawLine = false;
        foreach (string Line in Lines)
        {
            string Trimmed = Line.Trim();
            if (Trimmed.Length == 0)
            {
                continue;
            }

            SawLine = true;
            if (!Trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return SawLine;
    }

    /// <summary>
    /// Estimates the widest rendered line for a rich label.
    /// </summary>
    public static double EstimateStyledLabelMaxWidthPx(string RawLabel, double FontSizePx)
    {
        string Normalised = NormaliseLabelForRender(RawLabel);
        List<string> Lines = SplitLines(Normalised);
        return EstimateMaxLineWidthPx(Lines, FontSizePx);
    }

    /// <summary>
    /// Returns the left edge X for a multi-line label block centred at <paramref name="CentreX"/>.
    /// </summary>
    public static double GetMultilineBlockStartX(
        double CentreX,
        IReadOnlyList<string> Lines,
        double FontSizePx)
    {
        if (Lines.Count == 0)
        {
            return CentreX;
        }

        if (Lines.Count == 1)
        {
            return CentreX - (EstimateLineWidthPx(Lines[0], FontSizePx) / 2.0);
        }

        return CentreX - (EstimateMaxLineWidthPx(Lines, FontSizePx) / 2.0);
    }

    private static double EstimateMaxLineWidthPx(IReadOnlyList<string> Lines, double FontSizePx)
    {
        double MaxWidthPx = 0.0;
        foreach (string Line in Lines)
        {
            double LineWidthPx = EstimateLineWidthPx(Line, FontSizePx);
            if (LineWidthPx > MaxWidthPx)
            {
                MaxWidthPx = LineWidthPx;
            }
        }

        return MaxWidthPx;
    }

    /// <summary>
    /// Estimates rendered line width for horizontally centring styled inline labels.
    /// </summary>
    public static double EstimateLineWidthPx(string Line, double FontSizePx)
    {
        if (string.IsNullOrEmpty(Line))
        {
            return 0.0;
        }

        List<StyledRun> Runs = TokeniseInlineContent(Line);
        double TotalWidthPx = 0.0;
        foreach (StyledRun Run in Runs)
        {
            double CharWidthEm = LABEL_CHAR_WIDTH_EM;
            if (Run.IsMonospace)
            {
                CharWidthEm = MONOSPACE_LABEL_CHAR_WIDTH_EM;
            }
            else if (string.Equals(Run.FontWeight, "bold", StringComparison.Ordinal))
            {
                CharWidthEm = BOLD_LABEL_CHAR_WIDTH_EM;
            }

            TotalWidthPx += Run.Text.Length * FontSizePx * CharWidthEm;
        }

        return TotalWidthPx;
    }

    /// <summary>
    /// Returns the SVG <c>y</c> for the first line of a vertically centred multi-line label.
    /// </summary>
    public static double ComputeVerticallyCentredFirstLineY(
        double CentreY,
        int LineCount,
        double FontSizePx,
        double LineHeightFactor,
        bool UseMiddleDominantBaseline)
    {
        double LineHeightPx = FontSizePx * LineHeightFactor;
        if (UseMiddleDominantBaseline)
        {
            if (LineCount <= 1)
            {
                return CentreY;
            }

            return CentreY - (((LineCount - 1) * LineHeightPx) / 2.0);
        }

        double TotalTextHeightPx = LineCount == 1
            ? FontSizePx
            : LineCount * LineHeightPx;
        return CentreY - (TotalTextHeightPx / 2.0) + (LineHeightPx / 2.0);
    }

    /// <summary>
    /// Returns the vertical centre of a rendered multi-line label block.
    /// </summary>
    public static double ComputeLabelBlockCentreY(
        double FirstLineY,
        int LineCount,
        double FontSizePx,
        double LineHeightFactor,
        bool UseMiddleDominantBaseline)
    {
        double LineHeightPx = FontSizePx * LineHeightFactor;
        if (UseMiddleDominantBaseline)
        {
            if (LineCount <= 1)
            {
                return FirstLineY;
            }

            return FirstLineY + (((LineCount - 1) * LineHeightPx) / 2.0);
        }

        double TotalTextHeightPx = LineCount == 1
            ? FontSizePx
            : LineCount * LineHeightPx;
        return FirstLineY + (TotalTextHeightPx / 2.0) - (LineHeightPx / 2.0);
    }

    /// <summary>
    /// Appends a centred multi-line label with inline HTML/Markdown styling.
    /// </summary>
    public static void AppendCentredLabel(
        StringBuilder Builder,
        string RawLabel,
        double CentreX,
        double CentreY,
        double FontSizePx,
        string DefaultFill,
        string FontFamily,
        string DefaultFontWeight,
        double LineHeightFactor,
        bool UseMiddleDominantBaseline = false)
    {
        string Normalised = NormaliseLabelForRender(RawLabel);
        List<string> Lines = SplitLines(Normalised);
        double LineHeightPx = FontSizePx * LineHeightFactor;
        double FirstLineY = ComputeVerticallyCentredFirstLineY(
            CentreY,
            Lines.Count,
            FontSizePx,
            LineHeightFactor,
            UseMiddleDominantBaseline);

        Builder.Append("<text y=\"");
        Builder.Append(FirstLineY.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" text-anchor=\"start\"");
        if (UseMiddleDominantBaseline)
        {
            Builder.Append(" dominant-baseline=\"middle\"");
        }

        Builder.Append(" fill=\"");
        Builder.Append(EscapeXml(DefaultFill));
        Builder.Append("\" font-family=\"");
        Builder.Append(EscapeXml(FontFamily));
        Builder.Append("\" font-size=\"");
        Builder.Append(FontSizePx.ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" font-weight=\"");
        Builder.Append(EscapeXml(DefaultFontWeight));
        Builder.Append("\">");

        double BlockStartX = GetMultilineBlockStartX(CentreX, Lines, FontSizePx);

        for (int LineIndex = 0; LineIndex < Lines.Count; LineIndex++)
        {
            if (LineIndex == 0)
            {
                Builder.Append("<tspan x=\"");
                Builder.Append(BlockStartX.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\" dy=\"0\">");
            }
            else
            {
                Builder.Append("<tspan x=\"");
                Builder.Append(BlockStartX.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\" dy=\"");
                Builder.Append(LineHeightPx.ToString(CultureInfo.InvariantCulture));
                Builder.Append("\">");
            }

            AppendStyledInlineContent(
                Builder,
                Lines[LineIndex],
                DefaultFill,
                DefaultFontWeight,
                FontSizePx);
            Builder.Append("</tspan>");
        }

        Builder.Append("</text>");
    }

    private static string NormaliseLabelForRender(string RawLabel)
    {
        if (string.IsNullOrEmpty(RawLabel))
        {
            return string.Empty;
        }

        string Result = RawLabel.Replace("<br>", "<br/>", StringComparison.OrdinalIgnoreCase);
        Result = Result.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
        Result = DivOpenPattern.Replace(Result, string.Empty);
        Result = Result.Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase);

        MatchCollection Rows = TableRowPattern.Matches(Result);
        if (Rows.Count > 0)
        {
            List<string> TableLines = new List<string>();
            foreach (Match RowMatch in Rows)
            {
                MatchCollection Cells = TableCellPattern.Matches(RowMatch.Groups[1].Value);
                if (Cells.Count == 0)
                {
                    continue;
                }

                List<string> CellTexts = new List<string>();
                foreach (Match CellMatch in Cells)
                {
                    CellTexts.Add(StripTags(CellMatch.Groups[1].Value).Trim());
                }

                TableLines.Add(string.Join("  ", CellTexts));
            }

            if (TableLines.Count > 0)
            {
                Result = string.Join("\n", TableLines);
            }
        }

        Result = Result.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Result.Trim();
    }

    private static List<string> SplitLines(string Normalised)
    {
        string[] RawLines = Normalised.Split('\n');
        List<string> Lines = new List<string>();
        foreach (string RawLine in RawLines)
        {
            string Trimmed = RawLine.Trim();
            if (Trimmed.Length == 0 && Lines.Count > 0)
            {
                continue;
            }

            Lines.Add(Trimmed);
        }

        if (Lines.Count == 0)
        {
            Lines.Add(string.Empty);
        }

        return Lines;
    }

    private static void AppendStyledInlineContent(
        StringBuilder Builder,
        string Line,
        string DefaultFill,
        string DefaultFontWeight,
        double FontSizePx)
    {
        List<StyledRun> Runs = TokeniseInlineContent(Line);
        foreach (StyledRun Run in Runs)
        {
            if (Run.IsLineBreak)
            {
                continue;
            }

            bool NeedsSpan = !string.Equals(Run.FontWeight, DefaultFontWeight, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(Run.FontStyle)
                || !string.IsNullOrEmpty(Run.Fill)
                || Run.IsMonospace;

            if (NeedsSpan)
            {
                Builder.Append("<tspan");
                if (!string.Equals(Run.FontWeight, DefaultFontWeight, StringComparison.Ordinal))
                {
                    Builder.Append(" font-weight=\"");
                    Builder.Append(EscapeXml(Run.FontWeight));
                    Builder.Append('"');
                }

                if (!string.IsNullOrEmpty(Run.FontStyle))
                {
                    Builder.Append(" font-style=\"");
                    Builder.Append(EscapeXml(Run.FontStyle));
                    Builder.Append('"');
                }

                if (!string.IsNullOrEmpty(Run.Fill))
                {
                    Builder.Append(" fill=\"");
                    Builder.Append(EscapeXml(Run.Fill));
                    Builder.Append('"');
                }

                if (Run.IsMonospace)
                {
                    Builder.Append(" font-family=\"Consolas, monospace\"");
                }

                Builder.Append('>');
                Builder.Append(EscapeXml(Run.Text));
                Builder.Append("</tspan>");
            }
            else
            {
                Builder.Append(EscapeXml(Run.Text));
            }
        }
    }

    private static List<StyledRun> TokeniseInlineContent(string Line)
    {
        List<StyledRun> Runs = new List<StyledRun>();
        TokeniseHtmlAndMarkdown(Line, DefaultFontWeight: "normal", DefaultFill: null, Runs);
        if (Runs.Count == 0)
        {
            Runs.Add(new StyledRun { Text = StripTags(Line), FontWeight = "normal" });
        }

        return Runs;
    }

    private static void TokeniseHtmlAndMarkdown(
        string Input,
        string DefaultFontWeight,
        string? DefaultFill,
        List<StyledRun> Runs)
    {
        if (string.IsNullOrEmpty(Input))
        {
            return;
        }

        int Index = 0;
        while (Index < Input.Length)
        {
            if (TryConsumeTag(Input, ref Index, "<b>", "</b>", out string BoldText))
            {
                TokeniseHtmlAndMarkdown(BoldText, "bold", DefaultFill, Runs);
                continue;
            }

            if (TryConsumeTag(Input, ref Index, "<strong>", "</strong>", out string StrongText))
            {
                TokeniseHtmlAndMarkdown(StrongText, "bold", DefaultFill, Runs);
                continue;
            }

            if (TryConsumeTag(Input, ref Index, "<i>", "</i>", out string ItalicText))
            {
                AppendStyledRunsFromNested(ItalicText, DefaultFontWeight, DefaultFill, "italic", Runs);
                continue;
            }

            if (TryConsumeTag(Input, ref Index, "<em>", "</em>", out string EmText))
            {
                AppendStyledRunsFromNested(EmText, DefaultFontWeight, DefaultFill, "italic", Runs);
                continue;
            }

            if (TryConsumeSpanColor(Input, ref Index, out string SpanText, out string SpanColor))
            {
                AppendStyledRunsFromNested(SpanText, DefaultFontWeight, SpanColor, null, Runs);
                continue;
            }

            int NextTag = Input.IndexOf('<', Index);
            if (NextTag < 0)
            {
                AppendMarkdownRuns(Input.Substring(Index), DefaultFontWeight, DefaultFill, Runs);
                break;
            }

            if (NextTag > Index)
            {
                AppendMarkdownRuns(Input.Substring(Index, NextTag - Index), DefaultFontWeight, DefaultFill, Runs);
            }

            if (NextTag == Index)
            {
                AppendPlainRun("<", DefaultFontWeight, DefaultFill, Runs);
                Index++;
                continue;
            }

            Index = NextTag;
        }
    }

    private static void AppendMarkdownRuns(
        string PlainChunk,
        string DefaultFontWeight,
        string? DefaultFill,
        List<StyledRun> Runs)
    {
        if (string.IsNullOrEmpty(PlainChunk))
        {
            return;
        }

        string Remaining = PlainChunk;
        while (Remaining.Length > 0)
        {
            Match BoldMatch = BoldMarkdownPattern.Match(Remaining);
            Match CodeMatch = CodeMarkdownPattern.Match(Remaining);
            Match ItalicMatch = ItalicMarkdownPattern.Match(Remaining);

            int EarliestIndex = Remaining.Length;
            int SelectedKind = 0;
            Match SelectedMatch = BoldMatch;

            if (BoldMatch.Success && BoldMatch.Index < EarliestIndex)
            {
                EarliestIndex = BoldMatch.Index;
                SelectedKind = 1;
                SelectedMatch = BoldMatch;
            }

            if (CodeMatch.Success && CodeMatch.Index < EarliestIndex)
            {
                EarliestIndex = CodeMatch.Index;
                SelectedKind = 2;
                SelectedMatch = CodeMatch;
            }

            if (ItalicMatch.Success && ItalicMatch.Index < EarliestIndex)
            {
                EarliestIndex = ItalicMatch.Index;
                SelectedKind = 3;
                SelectedMatch = ItalicMatch;
            }

            if (SelectedKind == 0)
            {
                AppendPlainRun(Remaining, DefaultFontWeight, DefaultFill, Runs);
                break;
            }

            if (EarliestIndex > 0)
            {
                AppendPlainRun(Remaining.Substring(0, EarliestIndex), DefaultFontWeight, DefaultFill, Runs);
            }

            if (SelectedKind == 1)
            {
                Runs.Add(new StyledRun
                {
                    Text = SelectedMatch.Groups[1].Value,
                    FontWeight = "bold",
                    Fill = DefaultFill
                });
            }
            else if (SelectedKind == 2)
            {
                Runs.Add(new StyledRun
                {
                    Text = SelectedMatch.Groups[1].Value,
                    FontWeight = DefaultFontWeight,
                    Fill = DefaultFill,
                    IsMonospace = true
                });
            }
            else
            {
                Runs.Add(new StyledRun
                {
                    Text = SelectedMatch.Groups[1].Value,
                    FontWeight = DefaultFontWeight,
                    FontStyle = "italic",
                    Fill = DefaultFill
                });
            }

            Remaining = Remaining.Substring(EarliestIndex + SelectedMatch.Length);
        }
    }

    private static void AppendPlainRun(
        string Text,
        string DefaultFontWeight,
        string? DefaultFill,
        List<StyledRun> Runs)
    {
        if (Text.Length == 0)
        {
            return;
        }

        Runs.Add(new StyledRun
        {
            Text = Text,
            FontWeight = DefaultFontWeight,
            Fill = DefaultFill
        });
    }

    private static void AppendStyledRunsFromNested(
        string NestedText,
        string DefaultFontWeight,
        string? DefaultFill,
        string? FontStyle,
        List<StyledRun> Runs)
    {
        List<StyledRun> NestedRuns = new List<StyledRun>();
        TokeniseHtmlAndMarkdown(NestedText, DefaultFontWeight, DefaultFill, NestedRuns);
        foreach (StyledRun NestedRun in NestedRuns)
        {
            if (!string.IsNullOrEmpty(FontStyle) && string.IsNullOrEmpty(NestedRun.FontStyle))
            {
                NestedRun.FontStyle = FontStyle;
            }

            if (!string.IsNullOrEmpty(DefaultFill) && string.IsNullOrEmpty(NestedRun.Fill))
            {
                NestedRun.Fill = DefaultFill;
            }

            Runs.Add(NestedRun);
        }
    }

    private static bool TryConsumeTag(
        string Input,
        ref int Index,
        string OpenTag,
        string CloseTag,
        out string InnerText)
    {
        InnerText = string.Empty;
        if (Index >= Input.Length)
        {
            return false;
        }

        if (!Input.AsSpan(Index).StartsWith(OpenTag, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int ContentStart = Index + OpenTag.Length;
        int CloseIndex = Input.IndexOf(CloseTag, ContentStart, StringComparison.OrdinalIgnoreCase);
        if (CloseIndex < 0)
        {
            return false;
        }

        InnerText = Input.Substring(ContentStart, CloseIndex - ContentStart);
        Index = CloseIndex + CloseTag.Length;
        return true;
    }

    private static bool TryConsumeSpanColor(
        string Input,
        ref int Index,
        out string InnerText,
        out string Color)
    {
        InnerText = string.Empty;
        Color = string.Empty;
        if (Index >= Input.Length || Input[Index] != '<')
        {
            return false;
        }

        int CloseOpen = Input.IndexOf('>', Index);
        if (CloseOpen < 0)
        {
            return false;
        }

        string OpenTag = Input.Substring(Index, CloseOpen - Index + 1);
        if (!OpenTag.StartsWith("<span", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Match ColorMatch = Regex.Match(OpenTag, @"color\s*:\s*([^;'""]+)", RegexOptions.IgnoreCase);
        if (!ColorMatch.Success)
        {
            return false;
        }

        Color = ColorMatch.Groups[1].Value.Trim();
        int CloseIndex = Input.IndexOf("</span>", CloseOpen, StringComparison.OrdinalIgnoreCase);
        if (CloseIndex < 0)
        {
            return false;
        }

        InnerText = Input.Substring(CloseOpen + 1, CloseIndex - CloseOpen - 1);
        Index = CloseIndex + "</span>".Length;
        return true;
    }

    private static string StripTags(string Value)
    {
        return Regex.Replace(Value, "<[^>]+>", string.Empty);
    }

    private static string EscapeXml(string Value)
    {
        if (string.IsNullOrEmpty(Value))
        {
            return string.Empty;
        }

        return Value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private sealed class StyledRun
    {
        public string Text { get; set; } = string.Empty;

        public string FontWeight { get; set; } = "normal";

        public string? FontStyle { get; set; }

        public string? Fill { get; set; }

        public bool IsMonospace { get; set; }

        public bool IsLineBreak { get; set; }
    }
}
