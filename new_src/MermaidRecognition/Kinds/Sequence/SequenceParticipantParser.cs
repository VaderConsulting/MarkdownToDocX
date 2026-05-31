using System.Text.RegularExpressions;

namespace MermaidRecognition.Sequence;

/// <summary>
/// Parsed sequence participant declaration from a source line.
/// </summary>
public sealed class SequenceParticipantParseResult
{
    private string _Id = string.Empty;

    public string Id
    {
        get
        {
            return _Id;
        }
        set
        {
            _Id = value;
        }
    }

    private string? _Label;

    public string? Label
    {
        get
        {
            return _Label;
        }
        set
        {
            _Label = value;
        }
    }

    private string _DrawType = "participant";

    public string DrawType
    {
        get
        {
            return _DrawType;
        }
        set
        {
            _DrawType = value;
        }
    }
}

/// <summary>
/// Extracts participant identifiers and aliases from participant source lines.
/// </summary>
public static class SequenceParticipantParser
{
    private static readonly Regex ParticipantRegex = new(
        @"^\s*participant\s+(\S+)(?:\s+as\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ActorRegex = new(
        @"^\s*actor\s+(\S+)(?:\s+as\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CreateParticipantRegex = new(
        @"^\s*create\s+participant\s+(\S+)(?:\s+as\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a participant, actor, or create-participant line.
    /// </summary>
    public static bool TryParse(string Line, out SequenceParticipantParseResult Result)
    {
        Result = new SequenceParticipantParseResult();
        string Trimmed = Line.Trim();
        if (Trimmed.Length == 0)
        {
            return false;
        }

        Match ActorMatch = ActorRegex.Match(Trimmed);
        if (ActorMatch.Success)
        {
            Result.Id = ActorMatch.Groups[1].Value;
            Result.Label = NormalizeAlias(ActorMatch.Groups[2].Value);
            Result.DrawType = "actor";
            return true;
        }

        Match CreateMatch = CreateParticipantRegex.Match(Trimmed);
        if (CreateMatch.Success)
        {
            Result.Id = CreateMatch.Groups[1].Value;
            Result.Label = NormalizeAlias(CreateMatch.Groups[2].Value);
            Result.DrawType = "participant";
            return true;
        }

        Match ParticipantMatch = ParticipantRegex.Match(Trimmed);
        if (ParticipantMatch.Success)
        {
            Result.Id = ParticipantMatch.Groups[1].Value;
            Result.Label = NormalizeAlias(ParticipantMatch.Groups[2].Value);
            Result.DrawType = "participant";
            return true;
        }

        return false;
    }

    static string? NormalizeAlias(string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            return null;
        }

        return Value.Trim();
    }
}
