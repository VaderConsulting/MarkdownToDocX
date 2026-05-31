using System.Text.RegularExpressions;

namespace MermaidRecognition.Sequence;

/// <summary>
/// Extracts participant identifiers from explicit activate and deactivate lines.
/// </summary>
public static class SequenceActivateParser
{
    private static readonly Regex ActivateRegex = new(
        @"^\s*activate\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeactivateRegex = new(
        @"^\s*deactivate\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse an explicit activate line.
    /// </summary>
    public static bool TryParseActivate(string Line, out string ParticipantId)
    {
        ParticipantId = string.Empty;
        Match Match = ActivateRegex.Match(Line.Trim());
        if (!Match.Success)
        {
            return false;
        }

        ParticipantId = Match.Groups[1].Value;
        return true;
    }

    /// <summary>
    /// Attempts to parse an explicit deactivate line.
    /// </summary>
    public static bool TryParseDeactivate(string Line, out string ParticipantId)
    {
        ParticipantId = string.Empty;
        Match Match = DeactivateRegex.Match(Line.Trim());
        if (!Match.Success)
        {
            return false;
        }

        ParticipantId = Match.Groups[1].Value;
        return true;
    }
}
