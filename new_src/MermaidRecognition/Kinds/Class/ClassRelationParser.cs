namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Parsed class diagram relation from a source line.
/// </summary>
public sealed class ClassRelationParseResult
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

    private string _RelationToken = string.Empty;

    public string RelationToken
    {
        get => _RelationToken; set => _RelationToken = value;
    }

    private string? _SourceCardinality;

    public string? SourceCardinality
    {
        get => _SourceCardinality; set => _SourceCardinality = value;
    }

    private string? _TargetCardinality;

    public string? TargetCardinality
    {
        get => _TargetCardinality; set => _TargetCardinality = value;
    }

    private string? _RelationLabel;

    public string? RelationLabel
    {
        get => _RelationLabel; set => _RelationLabel = value;
    }
}

/// <summary>
/// Extracts class identifiers, cardinality markers, relation tokens, and labels from relation source lines.
/// </summary>
public static class ClassRelationParser
{
    private static readonly string[] RelationTokens =
    {
        "<|--",
        "<|..",
        "..>",
        "*--",
        "o--",
        "-->",
        "<--",
        "--",
        ".."
    };

    /// <summary>
    /// Attempts to parse a class relation line into source, target, token, cardinalities, and label.
    /// </summary>
    public static bool TryParse(string Line, out ClassRelationParseResult Result)
    {
        Result = new ClassRelationParseResult();
        string Trimmed = Line.Trim();
        if (Trimmed.Length == 0)
        {
            return false;
        }

        foreach (string Token in RelationTokens)
        {
            int TokenIndex = Trimmed.IndexOf(Token, StringComparison.Ordinal);
            if (TokenIndex < 0)
            {
                continue;
            }

            string SourcePart = Trimmed[..TokenIndex].Trim();
            string TargetPart = Trimmed[(TokenIndex + Token.Length)..].Trim();
            if (SourcePart.Length == 0 || TargetPart.Length == 0)
            {
                continue;
            }

            string? RelationLabel = null;
            int LabelIndex = TargetPart.LastIndexOf(" : ", StringComparison.Ordinal);
            if (LabelIndex >= 0)
            {
                RelationLabel = TargetPart[(LabelIndex + 3)..].Trim();
                TargetPart = TargetPart[..LabelIndex].Trim();
            }

            if (!TryParseSourceEndpoint(SourcePart, out string SourceId, out string? SourceCardinality))
            {
                continue;
            }

            if (!TryParseTargetEndpoint(TargetPart, out string TargetId, out string? TargetCardinality))
            {
                continue;
            }

            Result.SourceId = SourceId;
            Result.TargetId = TargetId;
            Result.RelationToken = Token;
            Result.SourceCardinality = SourceCardinality;
            Result.TargetCardinality = TargetCardinality;
            Result.RelationLabel = RelationLabel;
            return true;
        }

        return false;
    }

    static bool TryParseSourceEndpoint(string Part, out string ClassId, out string? Cardinality)
    {
        Part = Part.Trim();
        Cardinality = null;
        if (TryExtractTrailingQuotedValue(Part, out string ClassName, out string TrailingValue))
        {
            ClassId = ClassName;
            Cardinality = TrailingValue;
            return ClassId.Length > 0;
        }

        ClassId = ExtractSimpleClassId(Part);
        return ClassId.Length > 0;
    }

    static bool TryParseTargetEndpoint(string Part, out string ClassId, out string? Cardinality)
    {
        Part = Part.Trim();
        Cardinality = null;
        if (TryExtractLeadingQuotedValue(Part, out string LeadingValue, out string Remainder))
        {
            Cardinality = LeadingValue;
            ClassId = ExtractSimpleClassId(Remainder);
            return ClassId.Length > 0;
        }

        ClassId = ExtractSimpleClassId(Part);
        return ClassId.Length > 0;
    }

    static bool TryExtractTrailingQuotedValue(string Part, out string Prefix, out string QuotedValue)
    {
        Prefix = string.Empty;
        QuotedValue = string.Empty;
        if (Part.Length < 3 || Part[^1] != '"')
        {
            return false;
        }

        int OpenQuote = Part.LastIndexOf('"', Part.Length - 2);
        if (OpenQuote <= 0)
        {
            return false;
        }

        Prefix = Part[..OpenQuote].Trim();
        QuotedValue = Part[(OpenQuote + 1)..^1];
        return Prefix.Length > 0 && QuotedValue.Length > 0;
    }

    static bool TryExtractLeadingQuotedValue(string Part, out string QuotedValue, out string Remainder)
    {
        QuotedValue = string.Empty;
        Remainder = string.Empty;
        if (Part.Length < 2 || Part[0] != '"')
        {
            return false;
        }

        int CloseQuote = Part.IndexOf('"', 1);
        if (CloseQuote <= 0)
        {
            return false;
        }

        QuotedValue = Part[1..CloseQuote];
        Remainder = Part[(CloseQuote + 1)..].Trim();
        return QuotedValue.Length > 0;
    }

    static string ExtractSimpleClassId(string Part)
    {
        if (Part.Length == 0)
        {
            return string.Empty;
        }

        int SpaceIndex = Part.IndexOf(' ', StringComparison.Ordinal);
        if (SpaceIndex > 0)
        {
            return Part[..SpaceIndex];
        }

        return Part;
    }
}
