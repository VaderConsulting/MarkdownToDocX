using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Parses Mermaid sequence diagram title directives.
/// </summary>
public static class SequenceTitleParser
{
    private static readonly Regex TitleRegex = new(
        @"^\s*title\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a sequence title line.
    /// </summary>
    public static bool TryParse(string Line, out string Title)
    {
        Title = string.Empty;
        Match Match = TitleRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        Title = Match.Groups[1].Value.Trim();
        return Title.Length > 0;
    }
}
