using System.Text.RegularExpressions;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Parses Mermaid class diagram <c>note</c> directives.
/// </summary>
public static class ClassNoteParser
{
    private static readonly Regex NoteForRegex = new(
        @"^\s*note\s+for\s+(\S+)\s+(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NoteStandaloneRegex = new(
        @"^\s*note\s+(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a classified <c>note</c> line into class declarations or a synthetic note node.
    /// </summary>
    public static bool TryParse(string Line, int LineNumber, ClassDb Database)
    {
        MermaidSourceSpan SourceSpan = new MermaidSourceSpan(0, 0, LineNumber, 0, LineNumber, Line.Length);

        Match ForMatch = NoteForRegex.Match(Line);
        if (ForMatch.Success)
        {
            string ClassId = ForMatch.Groups[1].Value;
            List<string> QuotedStrings = ExtractQuotedStrings(ForMatch.Groups[2].Value.Trim());
            if (QuotedStrings.Count > 0)
            {
                Database.AddNoteForClass(ClassId, QuotedStrings[0], SourceSpan);
            }

            return true;
        }

        Match StandaloneMatch = NoteStandaloneRegex.Match(Line);
        if (StandaloneMatch.Success)
        {
            List<string> QuotedStrings = ExtractQuotedStrings(StandaloneMatch.Groups[1].Value.Trim());
            if (QuotedStrings.Count > 0)
            {
                Database.AddStandaloneNote(QuotedStrings[0], SourceSpan);
            }

            return true;
        }

        return false;
    }

    static List<string> ExtractQuotedStrings(string Text)
    {
        List<string> Results = [];
        int Index = 0;
        while (Index < Text.Length)
        {
            if (Text[Index] != '"')
            {
                Index++;
                continue;
            }

            int Start = Index + 1;
            int End = Start;
            while (End < Text.Length)
            {
                if (Text[End] == '"' && (End == Start || Text[End - 1] != '\\'))
                {
                    break;
                }

                End++;
            }

            if (End >= Text.Length)
            {
                break;
            }

            Results.Add(Text[Start..End]);
            Index = End + 1;
        }

        return Results;
    }
}
