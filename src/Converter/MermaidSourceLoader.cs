using System.Text;
using System.Text.RegularExpressions;

namespace Converter;

internal static class MermaidSourceLoader
{
    private static readonly Regex MermaidFenceRegex = new(
        @"(?s)```mermaid\s*\r?\n(?<Body>.*?)(\r?\n```)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static int GetMermaidBlockCount(string InputPath)
    {
        if (!File.Exists(InputPath))
        {
            throw new FileNotFoundException("Input file not found.", InputPath);
        }

        string Text = File.ReadAllText(InputPath, Encoding.UTF8);
        string Extension = Path.GetExtension(InputPath);
        if (!string.Equals(Extension, ".md", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Extension, ".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(Text) ? 0 : 1;
        }

        int Count = 0;
        Match Match = MermaidFenceRegex.Match(Text);
        while (Match.Success)
        {
            Count++;
            Match = Match.NextMatch();
        }

        return Count;
    }

    public static string LoadMermaidSource(string InputPath)
    {
        return LoadMermaidSource(InputPath, 1);
    }

    /// <summary>
    /// Loads the <paramref name="OneBasedIndex"/>th ```mermaid block from a Markdown file (1 = first).
    /// </summary>
    public static string LoadMermaidSource(string InputPath, int OneBasedIndex)
    {
        if (!File.Exists(InputPath))
        {
            throw new FileNotFoundException("Input file not found.", InputPath);
        }

        if (OneBasedIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(OneBasedIndex), "Mermaid block index must be at least 1.");
        }

        string Text = File.ReadAllText(InputPath, Encoding.UTF8);
        string Extension = Path.GetExtension(InputPath);
        if (!string.Equals(Extension, ".md", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Extension, ".markdown", StringComparison.OrdinalIgnoreCase))
        {
            if (OneBasedIndex > 1)
            {
                throw new InvalidOperationException(
                    "Mermaid block index " + OneBasedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " is out of range; input is a single diagram file.");
            }

            return Text.Trim();
        }

        int BlockIndex = 0;
        Match Match = MermaidFenceRegex.Match(Text);
        while (Match.Success)
        {
            BlockIndex++;
            if (BlockIndex == OneBasedIndex)
            {
                return Match.Groups["Body"].Value.Trim();
            }

            Match = Match.NextMatch();
        }

        int BlockCount = BlockIndex;
        throw new InvalidOperationException(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Mermaid block index {OneBasedIndex} is out of range; file has {BlockCount} ```mermaid block(s): {InputPath}"));
    }
}
