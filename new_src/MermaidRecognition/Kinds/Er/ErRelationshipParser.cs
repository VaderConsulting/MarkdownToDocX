using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Er;

/// <summary>
/// Extracts entity identifiers, cardinality markers, link type, and role label from ER relationship lines.
/// </summary>
public static class ErRelationshipParser
{
    private const string ER_CARDINALITY_MARKER = @"(?:\|\||\|o|o\||\}\||\|\{|o\{|\}o)";

    private static readonly Regex RelationshipRegex = new(
        @"^\s*(\S+)\s+(" + ER_CARDINALITY_MARKER + @")(--|\.\.)(" + ER_CARDINALITY_MARKER + @")\s+(\S+)(\s*:\s*(.+))?\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse an ER relationship line.
    /// </summary>
    public static bool TryParse(string Line, out ErRelationshipParseResult Result)
    {
        Result = new ErRelationshipParseResult();
        Match Match = RelationshipRegex.Match(Line.Trim());
        if (!Match.Success)
        {
            return false;
        }

        Result.SourceId = Match.Groups[1].Value;
        Result.LeftMarker = Match.Groups[2].Value;
        Result.Link = Match.Groups[3].Value;
        Result.RightMarker = Match.Groups[4].Value;
        Result.TargetId = Match.Groups[5].Value;
        if (Match.Groups[7].Success)
        {
            Result.RoleLabel = StripOptionalQuotes(Match.Groups[7].Value.Trim());
        }

        return Result.SourceId.Length > 0 && Result.TargetId.Length > 0;
    }

    static string StripOptionalQuotes(string Value)
    {
        if (Value.Length >= 2 && Value[0] == '"' && Value[Value.Length - 1] == '"')
        {
            return Value[1..^1];
        }

        return Value;
    }
}
