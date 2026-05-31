using System.Text;
using System.Text.RegularExpressions;
using MermaidRecognition;
using MermaidRecognition.Contract;

namespace MDConverter.Pipeline;

/// <summary>
/// Resolves Mermaid diagram accessibility metadata for DOCX image embedding.
/// </summary>
public static class MermaidAccessibilityResolver
{
    private static readonly Regex AccTitleLineRegex = new(
        @"^\s*accTitle\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrLineRegex = new(
        @"^\s*accDescr\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrBlockOpenRegex = new(
        @"^\s*accDescr\s*\{\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Resolves <c>accTitle</c> / <c>accDescr</c> from Mermaid source for Word picture properties.
    /// </summary>
    /// <param name="MermaidSource">Raw Mermaid fenced-block source.</param>
    /// <param name="UseRecognitionParser">When true, uses <see cref="MermaidRecognizer"/> first.</param>
    public static MermaidImageAccessibility Resolve(string MermaidSource, bool UseRecognitionParser)
    {
        if (string.IsNullOrWhiteSpace(MermaidSource))
        {
            return MermaidImageAccessibility.Default;
        }

        if (UseRecognitionParser)
        {
            try
            {
                DiagramContract Contract = MermaidRecognizer.Parse(MermaidSource);
                if (!string.IsNullOrWhiteSpace(Contract.Accessibility.Title)
                    || !string.IsNullOrWhiteSpace(Contract.Accessibility.Description))
                {
                    return new MermaidImageAccessibility(
                        Contract.Accessibility.Title,
                        Contract.Accessibility.Description);
                }
            }
            catch
            {
            }
        }

        if (TryScanAccessibilityLines(MermaidSource, out string? Title, out string? Description))
        {
            return new MermaidImageAccessibility(Title, Description);
        }

        return MermaidImageAccessibility.Default;
    }

    static bool TryScanAccessibilityLines(string MermaidSource, out string? Title, out string? Description)
    {
        Title = null;
        Description = null;
        bool Found = false;
        bool InAccDescrBlock = false;
        StringBuilder AccDescrBuilder = new StringBuilder();

        string Normalized = MermaidSource.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] Lines = Normalized.Split('\n');

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];

            if (InAccDescrBlock)
            {
                if (Line.Trim() == "}")
                {
                    Description = AccDescrBuilder.ToString().Trim();
                    InAccDescrBlock = false;
                    Found = true;
                }
                else
                {
                    if (AccDescrBuilder.Length > 0)
                    {
                        AccDescrBuilder.Append('\n');
                    }

                    AccDescrBuilder.Append(Line);
                }

                continue;
            }

            Match AccTitleMatch = AccTitleLineRegex.Match(Line);
            if (AccTitleMatch.Success)
            {
                Title = AccTitleMatch.Groups[1].Value.Trim();
                Found = true;
                continue;
            }

            Match AccDescrMatch = AccDescrLineRegex.Match(Line);
            if (AccDescrMatch.Success)
            {
                Description = AccDescrMatch.Groups[1].Value.Trim();
                Found = true;
                continue;
            }

            if (AccDescrBlockOpenRegex.IsMatch(Line))
            {
                InAccDescrBlock = true;
                AccDescrBuilder.Clear();
                Found = true;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(Line))
            {
                break;
            }
        }

        if (InAccDescrBlock && AccDescrBuilder.Length > 0)
        {
            Description = AccDescrBuilder.ToString().Trim();
        }

        return Found;
    }
}
