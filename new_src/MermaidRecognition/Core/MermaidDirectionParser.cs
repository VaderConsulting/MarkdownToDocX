using System.Text.RegularExpressions;

namespace MermaidRecognition.Core;

/// <summary>
/// Parses Mermaid layout direction directives shared across diagram kinds.
/// </summary>
public static class MermaidDirectionParser
{
    private static readonly Regex DirectionRegex = new(
        @"^\s*direction\s+(TB|BT|RL|LR)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a direction line into a contract direction value.
    /// </summary>
    public static bool TryParse(string Line, out string Direction)
    {
        Direction = string.Empty;
        Match Match = DirectionRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        Direction = Match.Groups[1].Value.ToUpperInvariant() switch
        {
            "TB" => "TopToBottom",
            "BT" => "BottomToTop",
            "LR" => "LeftToRight",
            "RL" => "RightToLeft",
            _ => string.Empty
        };

        return Direction.Length > 0;
    }
}
