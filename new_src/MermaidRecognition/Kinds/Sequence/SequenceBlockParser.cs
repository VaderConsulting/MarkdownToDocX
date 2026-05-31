using System.Text.RegularExpressions;

namespace MermaidRecognition.Sequence;

/// <summary>
/// Parsed sequence control-block keyword from a source line.
/// </summary>
public sealed class SequenceBlockParseResult
{
    private string _Keyword = string.Empty;

    public string Keyword
    {
        get
        {
            return _Keyword;
        }
        set
        {
            _Keyword = value;
        }
    }

    private string? _Title;

    public string? Title
    {
        get
        {
            return _Title;
        }
        set
        {
            _Title = value;
        }
    }

    public bool IsBranchKeyword
    {
        get
        {
            return string.Equals(_Keyword, "else", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_Keyword, "and", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_Keyword, "option", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsMultiBranchFrame
    {
        get
        {
            return string.Equals(_Keyword, "alt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_Keyword, "par", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_Keyword, "critical", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_Keyword, "par_over", StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// Extracts control-block keywords and labels from sequence block source lines.
/// </summary>
public static class SequenceBlockParser
{
    private static readonly Regex BlockRegex = new(
        @"^\s*(loop|alt|par_over|option|opt|par|rect|critical|break|else|and)\s*(.*)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RectRgbRegex = new(
        @"^\s*rgb\s*\([^)]+\)\s*(.*)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a sequence control-block line.
    /// </summary>
    public static bool TryParse(string Line, out SequenceBlockParseResult Result)
    {
        Result = new SequenceBlockParseResult();
        string Trimmed = Line.Trim();
        if (Trimmed.Length == 0)
        {
            return false;
        }

        Match Match = BlockRegex.Match(Trimmed);
        if (!Match.Success)
        {
            return false;
        }

        Result.Keyword = Match.Groups[1].Value.ToLowerInvariant();
        string Remainder = Match.Groups[2].Value.Trim();
        if (string.Equals(Result.Keyword, "rect", StringComparison.Ordinal)
            && Remainder.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            Match RgbMatch = RectRgbRegex.Match(Remainder);
            if (RgbMatch.Success)
            {
                Remainder = RgbMatch.Groups[1].Value.Trim();
            }
        }

        Result.Title = NormalizeTitle(Remainder);
        return true;
    }

    static string? NormalizeTitle(string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            return null;
        }

        return Value.Trim();
    }
}
