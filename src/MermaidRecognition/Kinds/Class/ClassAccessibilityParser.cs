using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Parses diagram-level accessibility directives (<c>accTitle</c>, <c>accDescr</c>) from class diagram source lines.
/// </summary>
public static class ClassAccessibilityParser
{
    private static readonly Regex AccTitleRegex = new(
        @"^\s*accTitle\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrInlineRegex = new(
        @"^\s*accDescr\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrBlockStartRegex = new(
        @"^\s*accDescr\s*\{\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses an inline <c>accTitle</c> value from the source line.
    /// </summary>
    public static bool TryParseAccTitle(string RawLine, out string Title)
    {
        Title = string.Empty;
        Match Match = AccTitleRegex.Match(RawLine);
        if (!Match.Success)
        {
            return false;
        }

        Title = Match.Groups[1].Value.Trim();
        return true;
    }

    /// <summary>
    /// Parses an inline <c>accDescr:</c> value from the source line.
    /// </summary>
    public static bool TryParseAccDescrInline(string RawLine, out string Description)
    {
        Description = string.Empty;
        Match Match = AccDescrInlineRegex.Match(RawLine);
        if (!Match.Success)
        {
            return false;
        }

        Description = Match.Groups[1].Value.Trim();
        return true;
    }

    /// <summary>
    /// Returns whether the line opens a multiline <c>accDescr {</c> block.
    /// </summary>
    public static bool IsAccDescrBlockStart(string RawLine) => AccDescrBlockStartRegex.IsMatch(RawLine);
}
