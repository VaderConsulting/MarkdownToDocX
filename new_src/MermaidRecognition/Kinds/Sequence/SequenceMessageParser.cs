namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Parsed sequence message from a source line.
/// </summary>
public sealed class SequenceMessageParseResult
{
    private string _SourceId = string.Empty;

    public string SourceId
    {
        get => _SourceId; set => _SourceId = value;
    }

    private string _TargetId = string.Empty;

    public string TargetId
    {
        get => _TargetId; set => _TargetId = value;
    }

    private string? _Label;

    public string? Label
    {
        get => _Label; set => _Label = value;
    }

    private string _ArrowToken = string.Empty;

    public string ArrowToken
    {
        get => _ArrowToken; set => _ArrowToken = value;
    }

    private string? _ActivateTargetId;

    public string? ActivateTargetId
    {
        get => _ActivateTargetId; set => _ActivateTargetId = value;
    }

    private string? _DeactivateSourceId;

    public string? DeactivateSourceId
    {
        get => _DeactivateSourceId; set => _DeactivateSourceId = value;
    }
}

/// <summary>
/// Extracts source, target, label, arrow token, and inline activation markers from message lines.
/// </summary>
public static class SequenceMessageParser
{
    private static readonly string[] ArrowTokens =
    {
        "<<-->>",
        "<<->>",
        "-->>",
        "->>",
        "-->",
        "->",
        "-x>>",
        "x->",
        "--x",
        "-x",
        "--)",
        "-)",
        "--\\",
        "-\\",
        "--/",
        "-/",
        "\\\\-",
        "//-"
    };

    /// <summary>
    /// Attempts to parse a sequence message line.
    /// </summary>
    public static bool TryParse(string Line, out SequenceMessageParseResult Result)
    {
        Result = new SequenceMessageParseResult();
        string Trimmed = Line.Trim();
        if (Trimmed.Length == 0)
        {
            return false;
        }

        string? ArrowToken = FindArrowToken(Trimmed, out int ArrowIndex);
        if (ArrowToken is null)
        {
            return false;
        }

        int ArrowEndIndex = ArrowIndex + ArrowToken.Length;
        int ColonIndex = Trimmed.IndexOf(':', ArrowEndIndex);
        string LabelPart = ColonIndex >= 0 ? Trimmed[(ColonIndex + 1)..].Trim() : string.Empty;
        string MessagePart = ColonIndex >= 0 ? Trimmed[..ColonIndex].Trim() : Trimmed;
        if (MessagePart.Length == 0)
        {
            return false;
        }

        string LeftPart = MessagePart[..ArrowIndex].Trim();
        string RightPart = MessagePart[ArrowEndIndex..].Trim();
        if (LeftPart.Length == 0 || RightPart.Length == 0)
        {
            return false;
        }

        string SourceId = LeftPart;
        string TargetId = RightPart;
        string? ActivateTargetId = null;
        string? DeactivateSourceId = null;

        if (TargetId.StartsWith('+'))
        {
            ActivateTargetId = TargetId[1..].Trim();
            TargetId = ActivateTargetId;
        }
        else if (TargetId.StartsWith('-'))
        {
            DeactivateSourceId = SourceId;
            TargetId = TargetId[1..].Trim();
        }

        if (TargetId.Length == 0 || SourceId.Length == 0)
        {
            return false;
        }

        Result.SourceId = SourceId;
        Result.TargetId = TargetId;
        Result.Label = LabelPart.Length == 0 ? null : StripOptionalQuotes(LabelPart);
        Result.ArrowToken = ArrowToken;
        Result.ActivateTargetId = ActivateTargetId;
        Result.DeactivateSourceId = DeactivateSourceId;
        return true;
    }

    static string? FindArrowToken(string MessagePart, out int ArrowIndex)
    {
        ArrowIndex = -1;
        string? BestToken = null;
        int BestIndex = int.MaxValue;

        foreach (string Token in ArrowTokens)
        {
            int Index = MessagePart.IndexOf(Token, StringComparison.Ordinal);
            if (Index < 0)
            {
                continue;
            }

            if (Index < BestIndex
                || (Index == BestIndex && BestToken is not null && Token.Length > BestToken.Length))
            {
                BestIndex = Index;
                BestToken = Token;
            }
        }

        if (BestToken is null)
        {
            return null;
        }

        ArrowIndex = BestIndex;
        return BestToken;
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
