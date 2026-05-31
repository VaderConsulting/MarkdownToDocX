namespace MermaidRecognition.Class;

/// <summary>
/// Parses <c>namespace</c> statements from class diagram source lines.
/// </summary>
public static class ClassNamespaceParser
{
    /// <summary>
    /// Parses a namespace line into its identifier, optional display title, and whether the block opens on the same line.
    /// </summary>
    public static bool TryParse(string RawLine, out string NamespaceId, out string? Title, out bool OpensBlock)
    {
        NamespaceId = string.Empty;
        Title = null;
        OpensBlock = false;

        string Trimmed = RawLine.Trim();
        if (Trimmed.Length == 0)
        {
            return false;
        }

        if (!Trimmed.StartsWith("namespace", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int Index = "namespace".Length;
        while (Index < Trimmed.Length && char.IsWhiteSpace(Trimmed[Index]))
        {
            Index++;
        }

        if (Index >= Trimmed.Length)
        {
            return false;
        }

        int IdStart = Index;
        while (Index < Trimmed.Length
            && !char.IsWhiteSpace(Trimmed[Index])
            && Trimmed[Index] != '{'
            && Trimmed[Index] != '[')
        {
            Index++;
        }

        if (Index == IdStart)
        {
            return false;
        }

        NamespaceId = Trimmed.Substring(IdStart, Index - IdStart);

        if (Index < Trimmed.Length && Trimmed[Index] == '[')
        {
            if (!TryParseBracketTitle(Trimmed, ref Index, out string? ParsedTitle))
            {
                return false;
            }

            Title = ParsedTitle;
        }

        while (Index < Trimmed.Length && char.IsWhiteSpace(Trimmed[Index]))
        {
            Index++;
        }

        if (Index < Trimmed.Length && Trimmed[Index] == '{')
        {
            OpensBlock = true;
        }

        return true;
    }

    static bool TryParseBracketTitle(string Trimmed, ref int Index, out string? Title)
    {
        Title = null;
        if (Trimmed[Index] != '[')
        {
            return false;
        }

        Index++;
        int ContentStart = Index;
        if (Index < Trimmed.Length && Trimmed[Index] == '"')
        {
            Index++;
            ContentStart = Index;
            while (Index < Trimmed.Length && Trimmed[Index] != '"')
            {
                Index++;
            }

            if (Index >= Trimmed.Length)
            {
                return false;
            }

            Title = Trimmed.Substring(ContentStart, Index - ContentStart);
            Index++;
        }
        else
        {
            while (Index < Trimmed.Length && Trimmed[Index] != ']')
            {
                Index++;
            }

            if (Index >= Trimmed.Length)
            {
                return false;
            }

            Title = Trimmed.Substring(ContentStart, Index - ContentStart).Trim();
            Index++;
        }

        if (Index >= Trimmed.Length || Trimmed[Index] != ']')
        {
            return false;
        }

        Index++;
        return true;
    }
}
