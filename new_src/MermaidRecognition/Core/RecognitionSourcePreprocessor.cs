using System.Text;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Core;

/// <summary>
/// Strips Mermaid front matter, directives, and line comments before diagram detection
/// (aligned with vendored <c>detectType.ts</c>).
/// </summary>
public static class RecognitionSourcePreprocessor
{
    private static readonly Regex FrontMatterRegex = new(
        @"^-{3}\s*[\r\n]+([\s\S]*?)[\r\n]+-{3}\s*[\r\n]+",
        RegexOptions.Compiled);

    private static readonly Regex DirectiveRegex = new(
        @"%%\{\s*(?:(\w+)\s*:|(\w+))\s*(?:(\w+)|((?:(?!}%%).|[\r\n])*))?\s*(?:}%%)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccTitleLineRegex = new(
        @"^\s*accTitle\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrLineRegex = new(
        @"^\s*accDescr\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrBlockOpenRegex = new(
        @"^\s*accDescr\s*\{\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns text suitable for diagram kind detection (init blocks remain in full source for parsers).
    /// </summary>
    public static string PrepareForDetection(string Source)
    {
        string Text = Source;
        Text = FrontMatterRegex.Replace(Text, string.Empty);
        Text = StripRemarkLinesForDetection(Text);
        Text = DirectiveRegex.Replace(Text, string.Empty);
        Text = StripAccessibilityPreamble(Text);
        return Text;
    }

    static string StripRemarkLinesForDetection(string Source)
    {
        if (string.IsNullOrEmpty(Source))
        {
            return Source;
        }

        string Normalized = Source.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] Lines = Normalized.Split('\n');
        StringBuilder Builder = new StringBuilder();

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line))
            {
                if (Builder.Length > 0)
                {
                    Builder.Append('\n');
                }

                continue;
            }

            if (MermaidRemarkAnalyzer.IsInitDirectiveLine(Line))
            {
                Builder.AppendLine(Line.Trim());
                continue;
            }

            if (MermaidRemarkAnalyzer.IsFullLineRemark(Line))
            {
                continue;
            }

            string Active = MermaidRemarkAnalyzer.StripTrailingRemark(Line);
            if (string.IsNullOrWhiteSpace(Active))
            {
                continue;
            }

            Builder.AppendLine(Active.Trim());
        }

        return Builder.ToString();
    }

    /// <summary>
    /// Removes leading <c>accTitle</c> / <c>accDescr</c> lines so the first substantive line is the diagram header.
    /// </summary>
    public static string StripAccessibilityPreamble(string Source)
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            return Source;
        }

        string Normalized = Source.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] Lines = Normalized.Split('\n');
        List<string> KeptLines = [];
        bool InAccDescrBlock = false;

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];

            if (InAccDescrBlock)
            {
                if (Line.Trim() == "}")
                {
                    InAccDescrBlock = false;
                }

                continue;
            }

            if (AccTitleLineRegex.IsMatch(Line)
                || AccDescrLineRegex.IsMatch(Line)
                || AccDescrBlockOpenRegex.IsMatch(Line))
            {
                if (AccDescrBlockOpenRegex.IsMatch(Line))
                {
                    InAccDescrBlock = true;
                }

                continue;
            }

            KeptLines.Add(Line);
        }

        return string.Join("\n", KeptLines).TrimStart('\n');
    }
}
