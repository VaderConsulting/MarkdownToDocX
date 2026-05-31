using System.Text.RegularExpressions;

namespace MermaidRecognition.Er;

/// <summary>
/// Extracts entity identifiers from ER entity block open lines.
/// </summary>
public static class ErEntityBlockParser
{
    private static readonly Regex EntityBlockOpenRegex = new(@"^\s*(\S+)\s*\{\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse an entity block open line into an entity identifier.
    /// </summary>
    public static bool TryParseEntityOpen(string Line, out string EntityId)
    {
        EntityId = string.Empty;
        Match Match = EntityBlockOpenRegex.Match(Line.Trim());
        if (!Match.Success)
        {
            return false;
        }

        EntityId = Match.Groups[1].Value;
        return EntityId.Length > 0;
    }
}
