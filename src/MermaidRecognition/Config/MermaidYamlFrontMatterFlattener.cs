using System.Text.RegularExpressions;

namespace MermaidRecognition.Config;

/// <summary>
/// Extracts and flattens Mermaid YAML front matter (F86 / vendored <c>frontmatter.ts</c>).
/// </summary>
public static class MermaidYamlFrontMatterFlattener
{
    private static readonly Regex FrontMatterBlockRegex = new(
        @"^-{3}\s*[\r\n]+([\s\S]*?)[\r\n]+-{3}\s*[\r\n]+",
        RegexOptions.Compiled);

    /// <summary>
    /// When source begins with YAML front matter, merges flattened <c>config</c> keys into
    /// <paramref name="Output"/> and returns the remaining diagram text.
    /// </summary>
    public static bool TryExtractLeadingFrontMatter(
        string Source,
        IDictionary<string, string> Output,
        out string Remaining)
    {
        Remaining = Source;
        Match Match = FrontMatterBlockRegex.Match(Source);
        if (!Match.Success)
        {
            return false;
        }

        string YamlBody = Match.Groups[1].Value;
        Dictionary<string, object?> Root = ParseSimpleYamlMapping(YamlBody);
        if (Root.TryGetValue("config", out object? ConfigValue)
            && ConfigValue is Dictionary<string, object?> ConfigRoot)
        {
            FlattenMapping(ConfigRoot, string.Empty, Output);
        }
        else
        {
            FlattenMapping(Root, string.Empty, Output);
        }

        Remaining = Source.Substring(Match.Length);
        return true;
    }

    static Dictionary<string, object?> ParseSimpleYamlMapping(string YamlBody)
    {
        Dictionary<string, object?> Root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(YamlBody))
        {
            return Root;
        }

        string Normalized = YamlBody.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] Lines = Normalized.Split('\n');
        Stack<(int Indent, Dictionary<string, object?> Map)> Stack = new Stack<(int, Dictionary<string, object?>)>();
        Stack.Push((-1, Root));

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line) || Line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            int Indent = CountLeadingSpaces(Line);
            string Trimmed = Line.TrimStart();
            int ColonIndex = Trimmed.IndexOf(':');
            if (ColonIndex <= 0)
            {
                continue;
            }

            string Key = Trimmed.Substring(0, ColonIndex).Trim();
            string ValuePart = Trimmed.Substring(ColonIndex + 1).Trim();

            while (Stack.Count > 1 && Indent <= Stack.Peek().Indent)
            {
                Stack.Pop();
            }

            Dictionary<string, object?> Current = Stack.Peek().Map;
            if (string.IsNullOrEmpty(ValuePart))
            {
                Dictionary<string, object?> Child = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                Current[Key] = Child;
                Stack.Push((Indent, Child));
            }
            else
            {
                Current[Key] = UnquoteYamlScalar(ValuePart);
            }
        }

        return Root;
    }

    static void FlattenMapping(Dictionary<string, object?> Mapping, string Prefix, IDictionary<string, string> Output)
    {
        foreach (KeyValuePair<string, object?> Entry in Mapping)
        {
            string Key = string.IsNullOrEmpty(Prefix) ? Entry.Key : Prefix + "." + Entry.Key;
            if (Entry.Value is Dictionary<string, object?> Child)
            {
                FlattenMapping(Child, Key, Output);
            }
            else if (Entry.Value != null)
            {
                Output[Key] = Entry.Value.ToString() ?? string.Empty;
            }
        }
    }

    static string UnquoteYamlScalar(string Value)
    {
        if (Value.Length >= 2
            && ((Value.StartsWith('"') && Value.EndsWith('"'))
                || (Value.StartsWith('\'') && Value.EndsWith('\''))))
        {
            return Value.Substring(1, Value.Length - 2);
        }

        return Value;
    }

    static int CountLeadingSpaces(string Line)
    {
        int Count = 0;
        while (Count < Line.Length && Line[Count] == ' ')
        {
            Count++;
        }

        return Count;
    }
}
