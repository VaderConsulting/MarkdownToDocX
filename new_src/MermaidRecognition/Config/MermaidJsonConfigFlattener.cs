using System.Text.Json;

namespace MermaidRecognition.Config;

/// <summary>
/// Flattens Mermaid init JSON into dotted-path string dictionaries (§4.1).
/// </summary>
public static class MermaidJsonConfigFlattener
{
    /// <summary>
    /// Flattens a JSON object into dotted keys with string values.
    /// </summary>
    public static void FlattenJson(JsonElement Element, string Prefix, IDictionary<string, string> Output)
    {
        if (Element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty Property in Element.EnumerateObject())
            {
                string Key = string.IsNullOrEmpty(Prefix) ? Property.Name : Prefix + "." + Property.Name;
                FlattenJson(Property.Value, Key, Output);
            }

            return;
        }

        if (Element.ValueKind == JsonValueKind.Array)
        {
            Output[Prefix] = Element.GetRawText();
            return;
        }

        if (Element.ValueKind == JsonValueKind.String)
        {
            Output[Prefix] = Element.GetString() ?? string.Empty;
            return;
        }

        if (Element.ValueKind == JsonValueKind.Number)
        {
            Output[Prefix] = Element.GetRawText();
            return;
        }

        if (Element.ValueKind == JsonValueKind.True || Element.ValueKind == JsonValueKind.False)
        {
            Output[Prefix] = Element.GetRawText();
            return;
        }

        Output[Prefix] = string.Empty;
    }

    /// <summary>
    /// Parses one <c>%%{init: {...}}%%</c> block and merges into <paramref name="Output"/>.
    /// </summary>
    public static void TryParseInitBlock(string InitBlock, IDictionary<string, string> Output)
    {
        if (string.IsNullOrWhiteSpace(InitBlock))
        {
            return;
        }

        int InitIndex = InitBlock.IndexOf("init:", StringComparison.OrdinalIgnoreCase);
        if (InitIndex < 0)
        {
            return;
        }

        int JsonStart = InitBlock.IndexOf('{', InitIndex);
        if (JsonStart < 0)
        {
            return;
        }

        if (!TryGetJsonObjectEndIndex(InitBlock, JsonStart, out int JsonEndIndex))
        {
            return;
        }

        string Json = InitBlock.Substring(JsonStart, (JsonEndIndex - JsonStart) + 1);
        try
        {
            using JsonDocument Document = JsonDocument.Parse(Json);
            FlattenJson(Document.RootElement, string.Empty, Output);
        }
        catch (JsonException)
        {
            // Invalid init blocks are optional in Mermaid; ignore.
        }
    }

    /// <summary>
    /// Parses leading <c>%%{init}%%</c> blocks from diagram source.
    /// </summary>
    public static Dictionary<string, string> ParseLeadingInitBlocks(string Input)
    {
        Dictionary<string, string> Result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Remaining = Input.TrimStart();
        while (TryExtractNextInitBlock(Remaining, out string Block, out int ConsumedLength))
        {
            TryParseInitBlock(Block, Result);
            Remaining = Remaining[ConsumedLength..].TrimStart();
        }

        return Result;
    }

    /// <summary>
    /// Extracts one leading <c>%%{init: ...}%%</c> block using brace matching on the init JSON object.
    /// </summary>
    public static bool TryExtractNextInitBlock(string Remaining, out string Block, out int ConsumedLength)
    {
        Block = string.Empty;
        ConsumedLength = 0;
        if (!Remaining.StartsWith("%%{", StringComparison.Ordinal))
        {
            return false;
        }

        int InitIndex = Remaining.IndexOf("init:", StringComparison.OrdinalIgnoreCase);
        if (InitIndex < 0)
        {
            return false;
        }

        int JsonStart = Remaining.IndexOf('{', InitIndex);
        if (JsonStart < 0)
        {
            return false;
        }

        int Depth = 0;
        for (int Index = JsonStart; Index < Remaining.Length; Index++)
        {
            char Character = Remaining[Index];
            if (Character == '{')
            {
                Depth++;
            }
            else if (Character == '}')
            {
                Depth--;
                if (Depth == 0)
                {
                    int WrapperCloseIndex = Index + 1;
                    while (WrapperCloseIndex < Remaining.Length && char.IsWhiteSpace(Remaining[WrapperCloseIndex]))
                    {
                        WrapperCloseIndex++;
                    }

                    if (WrapperCloseIndex >= Remaining.Length || Remaining[WrapperCloseIndex] != '}')
                    {
                        return false;
                    }

                    int PercentIndex = WrapperCloseIndex + 1;
                    if (PercentIndex + 1 >= Remaining.Length ||
                        Remaining[PercentIndex] != '%' ||
                        Remaining[PercentIndex + 1] != '%')
                    {
                        return false;
                    }

                    ConsumedLength = PercentIndex + 2;
                    Block = Remaining.Substring(0, ConsumedLength);
                    return true;
                }
            }
        }

        return false;
    }

    static bool TryGetJsonObjectEndIndex(string Text, int JsonStartIndex, out int JsonEndIndex)
    {
        JsonEndIndex = -1;
        int Depth = 0;
        for (int Index = JsonStartIndex; Index < Text.Length; Index++)
        {
            char Character = Text[Index];
            if (Character == '{')
            {
                Depth++;
            }
            else if (Character == '}')
            {
                Depth--;
                if (Depth == 0)
                {
                    JsonEndIndex = Index;
                    return true;
                }
            }
        }

        return false;
    }
}

