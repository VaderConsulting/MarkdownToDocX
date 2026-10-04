using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Parsed sequence participant box from a source line.
/// </summary>
public sealed class SequenceBoxParseResult
{
    private string? _Title;

    public string? Title
    {
        get => _Title; set => _Title = value;
    }

    private string? _RgbToken;

    public string? RgbToken
    {
        get => _RgbToken; set => _RgbToken = value;
    }
}

/// <summary>
/// Extracts participant box labels and optional colour tokens from box source lines.
/// </summary>
public static class SequenceBoxParser
{
    private static readonly Regex BoxRegex = new(
        @"^\s*box\b(?:\s+(rgb\s*\([^)]+\)))?\s*(.*)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a sequence box line.
    /// </summary>
    public static bool TryParse(string Line, out SequenceBoxParseResult Result)
    {
        Result = new SequenceBoxParseResult();
        string Trimmed = Line.Trim();
        if (Trimmed.Length == 0)
        {
            return false;
        }

        Match Match = BoxRegex.Match(Trimmed);
        if (!Match.Success)
        {
            return false;
        }

        Result.RgbToken = NormalizeToken(Match.Groups[1].Value);
        Result.Title = NormalizeTitle(Match.Groups[2].Value);
        return true;
    }

    static string? NormalizeToken(string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            return null;
        }

        return Value.Trim();
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
