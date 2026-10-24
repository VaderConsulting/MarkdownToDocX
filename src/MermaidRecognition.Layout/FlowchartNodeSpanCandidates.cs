using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Layout;

/// <summary>A measured wrapping choice and its minimum whole-cell reservation.</summary>
/// <param name="RowSpan">Minimum positive row span.</param>
/// <param name="ColumnSpan">Minimum positive column span.</param>
/// <param name="DisplayLabel">Text with the exact line breaks measured for this choice.</param>
/// <param name="TextWidth">Maximum measured line width, before padding.</param>
/// <param name="TextHeight">Measured line-box height, before padding.</param>
internal sealed record FlowchartNodeSpanCandidate(int RowSpan, int ColumnSpan,
    string DisplayLabel, double TextWidth, double TextHeight);

/// <summary>Measures word wrapping before deriving a finite set of independent width and height spans.</summary>
internal static class FlowchartNodeSpanCandidates
{
    /// <summary>Builds undominated fitting candidates without deriving base cells from any label or neighbour count.</summary>
    /// <param name="Label">Plain display text; explicit newlines are preserved.</param>
    /// <param name="Grid">The diagram's fixed base cells, gaps and padding.</param>
    /// <param name="MeasureWidth">Renderer measurement using the resolved font, size and style.</param>
    /// <param name="LineHeight">Renderer line-box height, in pixels.</param>
    /// <param name="MinimumWidth">Measured mandatory shape or allocated-port capacity requirement.</param>
    /// <param name="MinimumHeight">Measured mandatory shape or allocated-port capacity requirement.</param>
    /// <returns>Deterministic fitting span choices for complete layout scoring; no local area score selects a winner.</returns>
    internal static IReadOnlyList<FlowchartNodeSpanCandidate> Measure(string Label,
        FlowchartLogicalGridConfiguration Grid, Func<string, double> MeasureWidth, double LineHeight,
        double MinimumWidth = 0, double MinimumHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(MeasureWidth);
        _ = new FlowchartLogicalGrid(Grid);
        if (!double.IsFinite(LineHeight) || LineHeight <= 0) throw new ArgumentOutOfRangeException(nameof(LineHeight));
        if (!double.IsFinite(MinimumWidth) || MinimumWidth < 0) throw new ArgumentOutOfRangeException(nameof(MinimumWidth));
        if (!double.IsFinite(MinimumHeight) || MinimumHeight < 0) throw new ArgumentOutOfRangeException(nameof(MinimumHeight));
        string[] Paragraphs = (Label ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n');
        string[][] Words = Paragraphs.Select(Paragraph => Regex.Split(Paragraph.Trim(), @"\s+")
            .Where(Word => Word.Length > 0).ToArray()).ToArray();
        var WidthCache = new Dictionary<string, double>(StringComparer.Ordinal);
        double Width(string Text)
        {
            if (WidthCache.TryGetValue(Text, out double Cached)) return Cached;
            double Measured = Text.Length == 0 ? 0 : MeasureWidth(Text);
            if (!double.IsFinite(Measured) || Measured < 0)
                throw new InvalidOperationException("The renderer returned an invalid text width.");
            WidthCache[Text] = Measured;
            return Measured;
        }

        // Keep the renderer's existing twice-padding horizontal inset on each side.
        double HorizontalPadding = 4 * Grid.TextPadding, VerticalPadding = 2 * Grid.TextPadding;
        int MinimumColumns = SpanFor(MinimumWidth, Grid.CellWidth, Grid.ColumnGap);
        int MinimumRows = SpanFor(MinimumHeight, Grid.CellHeight, Grid.RowGap);
        double WidestWord = Words.SelectMany(Paragraph => Paragraph).Select(Width).DefaultIfEmpty(0).Max();
        // With no maximum span, every word fits some candidate. Never split it prematurely.
        MinimumColumns = Math.Max(MinimumColumns, SpanFor(WidestWord + HorizontalPadding, Grid.CellWidth, Grid.ColumnGap));
        var Columns = new SortedSet<int> { MinimumColumns };
        foreach (string[] Paragraph in Words)
        for (int Start = 0; Start < Paragraph.Length; Start++)
        for (int End = Start + 1; End <= Paragraph.Length; End++)
        {
            // Wrapping changes only when a contiguous word sequence starts fitting.
            // Deduplicate whole-cell thresholds instead of searching arbitrary span limits.
            double RunWidth = Width(string.Join(" ", Paragraph[Start..End]));
            Columns.Add(Math.Max(MinimumColumns, SpanFor(RunWidth + HorizontalPadding, Grid.CellWidth, Grid.ColumnGap)));
        }
        var Candidates = new List<FlowchartNodeSpanCandidate>();
        foreach (int ColumnSpan in Columns)
        {
            double Available = ColumnSpan * Grid.CellWidth + (ColumnSpan - 1.0) * Grid.ColumnGap - HorizontalPadding;
            var Lines = new List<string>();
            foreach (string[] Paragraph in Words)
            {
                string Line = string.Empty;
                foreach (string Word in Paragraph)
                {
                    string Joined = Line.Length == 0 ? Word : Line + " " + Word;
                    if (Line.Length > 0 && Width(Joined) > Available)
                    {
                        Lines.Add(Line);
                        Line = Word;
                    }
                    else Line = Joined;
                }
                Lines.Add(Line);
            }
            double TextWidth = Lines.Select(Width).DefaultIfEmpty(0).Max();
            double TextHeight = Lines.Count * LineHeight;
            int RowSpan = Math.Max(MinimumRows, SpanFor(TextHeight + VerticalPadding, Grid.CellHeight, Grid.RowGap));
            // Wider candidates are useful only when they reduce height. Mandatory minima
            // were applied before pruning, so no required geometry is pruned away.
            if (Candidates.Any(Candidate => Candidate.ColumnSpan <= ColumnSpan && Candidate.RowSpan <= RowSpan)) continue;
            Candidates.Add(new(RowSpan, ColumnSpan, string.Join("\n", Lines), TextWidth, TextHeight));
            if (ColumnSpan == 1 && RowSpan == 1) break;
        }
        return Candidates;
    }

    /// <summary>Finds the smallest contiguous whole-cell extent that holds a measured size.</summary>
    /// <param name="Needed">Measured extent including required padding.</param>
    /// <param name="Cell">Fixed base cell extent.</param>
    /// <param name="Gap">Initial internal gap extent.</param>
    /// <returns>A positive whole-cell span; throws rather than silently truncating overflow.</returns>
    private static int SpanFor(double Needed, double Cell, double Gap) =>
        Math.Max(1, checked((int)Math.Ceiling((Needed + Gap) / (Cell + Gap))));
}
